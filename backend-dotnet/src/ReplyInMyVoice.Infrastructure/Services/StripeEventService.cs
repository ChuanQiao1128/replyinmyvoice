using System.Text.Json;
using System.Data;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class StripeEventService(Func<AppDbContext> dbContextFactory)
{
    public async Task<bool> TryMarkProcessedAsync(
        string eventId,
        string type,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory();

        db.StripeEvents.Add(new StripeEvent
        {
            EventId = eventId,
            Type = type,
            Status = StripeEventStatus.Processed,
            AttemptCount = 1,
            CreatedAt = now,
            LastAttemptAt = now,
            ProcessedAt = now,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    public async Task<bool> ProcessWebhookEventAsync(
        string eventId,
        string type,
        string rawBody,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var acquired = await TryBeginProcessingAsync(eventId, type, now, cancellationToken);
        if (!acquired)
        {
            return false;
        }

        try
        {
            await SyncEntitlementAsync(eventId, type, rawBody, now, cancellationToken);
            await MarkProcessedAsync(eventId, now, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await MarkFailedAsync(eventId, ex.Message, now, cancellationToken);
            throw;
        }
    }

    private async Task<bool> TryBeginProcessingAsync(
        string eventId,
        string type,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await ExecuteInTransactionAsync(async db =>
        {
            var stripeEvent = await db.StripeEvents
                .AsTracking()
                .SingleOrDefaultAsync(x => x.EventId == eventId, cancellationToken);

            if (stripeEvent is null)
            {
                db.StripeEvents.Add(new StripeEvent
                {
                    EventId = eventId,
                    Type = type,
                    Status = StripeEventStatus.Processing,
                    AttemptCount = 1,
                    CreatedAt = now,
                    LastAttemptAt = now,
                    LockedUntil = now.AddMinutes(2),
                });
                await db.SaveChangesAsync(cancellationToken);
                return true;
            }

            if (stripeEvent.Status == StripeEventStatus.Processed)
            {
                return false;
            }

            if (stripeEvent.Status == StripeEventStatus.Processing && stripeEvent.LockedUntil > now)
            {
                return false;
            }

            stripeEvent.Type = type;
            stripeEvent.Status = StripeEventStatus.Processing;
            stripeEvent.AttemptCount += 1;
            stripeEvent.LastAttemptAt = now;
            stripeEvent.LockedUntil = now.AddMinutes(2);
            stripeEvent.LastError = null;
            stripeEvent.RowVersion = Guid.NewGuid();

            await db.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    private async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<AppDbContext, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await using var strategyDb = dbContextFactory();
        var strategy = strategyDb.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var db = dbContextFactory();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var result = await operation(db);

            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private async Task MarkProcessedAsync(
        string eventId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var db = dbContextFactory();
        var stripeEvent = await db.StripeEvents
            .AsTracking()
            .SingleAsync(x => x.EventId == eventId, cancellationToken);

        stripeEvent.Status = StripeEventStatus.Processed;
        stripeEvent.ProcessedAt = now;
        stripeEvent.LockedUntil = null;
        stripeEvent.LastError = null;
        stripeEvent.RowVersion = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkFailedAsync(
        string eventId,
        string error,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var db = dbContextFactory();
        var stripeEvent = await db.StripeEvents
            .AsTracking()
            .SingleAsync(x => x.EventId == eventId, cancellationToken);

        stripeEvent.Status = StripeEventStatus.Failed;
        stripeEvent.LastAttemptAt = now;
        stripeEvent.LockedUntil = null;
        stripeEvent.LastError = error.Length > 1000 ? error[..1000] : error;
        stripeEvent.RowVersion = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncEntitlementAsync(
        string eventId,
        string type,
        string rawBody,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(rawBody);
        if (!document.RootElement.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("object", out var stripeObject))
        {
            return;
        }

        if (type.StartsWith("customer.subscription.", StringComparison.Ordinal))
        {
            await SyncSubscriptionObjectAsync(type, stripeObject, now, cancellationToken);
            return;
        }

        if (type == "checkout.session.completed")
        {
            await SyncCheckoutSessionAsync(eventId, stripeObject, now, cancellationToken);
            return;
        }

        if (type == "charge.refunded")
        {
            await RevokeRefundedChargeCreditsAsync(stripeObject, cancellationToken);
            return;
        }

        if (type is "charge.dispute.created" or "charge.dispute.closed")
        {
            await RevokeDisputedChargeCreditsAsync(stripeObject, cancellationToken);
        }
    }

    private async Task SyncSubscriptionObjectAsync(
        string type,
        JsonElement stripeObject,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var customerId = GetString(stripeObject, "customer");
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return;
        }

        await using var db = dbContextFactory();
        var user = await db.AppUsers
            .AsTracking()
            .SingleOrDefaultAsync(x => x.StripeCustomerId == customerId, cancellationToken);
        if (user is null)
        {
            return;
        }

        var status = type == "customer.subscription.deleted"
            ? SubscriptionStatus.Canceled
            : MapSubscriptionStatus(GetString(stripeObject, "status"));

        user.StripeSubscriptionId = GetString(stripeObject, "id") ?? user.StripeSubscriptionId;
        user.SubscriptionStatus = status;
        user.CurrentPeriodEnd = GetUnixDateTime(stripeObject, "current_period_end");
        user.UpdatedAt = now;
        user.RowVersion = Guid.NewGuid();

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncCheckoutSessionAsync(
        string eventId,
        JsonElement stripeObject,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var externalAuthUserId = GetString(stripeObject, "client_reference_id") ??
            GetMetadataString(stripeObject, "externalAuthUserId") ??
            GetMetadataString(stripeObject, "clerkUserId");
        var customerId = GetString(stripeObject, "customer");

        if (string.IsNullOrWhiteSpace(externalAuthUserId) && string.IsNullOrWhiteSpace(customerId))
        {
            return;
        }

        await using var db = dbContextFactory();
        var user = await db.AppUsers.AsTracking().SingleOrDefaultAsync(
            x => (!string.IsNullOrWhiteSpace(externalAuthUserId) && x.ExternalAuthUserId == externalAuthUserId) ||
                 (!string.IsNullOrWhiteSpace(customerId) && x.StripeCustomerId == customerId),
            cancellationToken);
        if (user is null)
        {
            return;
        }

        user.StripeCustomerId = customerId ?? user.StripeCustomerId;
        user.StripeSubscriptionId = GetString(stripeObject, "subscription") ?? user.StripeSubscriptionId;
        user.UpdatedAt = now;
        user.RowVersion = Guid.NewGuid();

        if (IsPaidPaymentSession(stripeObject) &&
            !await db.RewriteCredits.AnyAsync(x => x.StripeEventId == eventId, cancellationToken) &&
            ResolveGrantedRewrites(stripeObject) is { } rewrites)
        {
            var sku = GetMetadataString(stripeObject, "sku");
            db.RewriteCredits.Add(new RewriteCredit
            {
                UserId = user.Id,
                Source = "PURCHASE",
                AmountGranted = rewrites,
                AmountConsumed = 0,
                GrantedAt = now,
                ExpiresAt = now.AddDays(90),
                StripeEventId = eventId,
                StripePaymentIntentId = GetString(stripeObject, "payment_intent"),
                StripeSku = sku,
                StripeAmountTotal = GetLong(stripeObject, "amount_total"),
                StripeCurrency = GetString(stripeObject, "currency"),
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RevokeRefundedChargeCreditsAsync(
        JsonElement stripeObject,
        CancellationToken cancellationToken)
    {
        var paymentIntentId = GetString(stripeObject, "payment_intent");
        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            return;
        }

        await using var db = dbContextFactory();
        var credits = await db.RewriteCredits
            .AsTracking()
            .Where(x => x.StripePaymentIntentId == paymentIntentId)
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var credit in credits)
        {
            var previousGranted = credit.AmountGranted;
            if (IsFullRefund(stripeObject))
            {
                credit.AmountGranted = credit.AmountConsumed;
            }
            else if (ResolveRefundedCreditCount(stripeObject, credit) is { } refundedCredits && refundedCredits > 0)
            {
                credit.AmountGranted = Math.Max(credit.AmountConsumed, credit.AmountGranted - refundedCredits);
            }

            if (credit.AmountGranted != previousGranted)
            {
                credit.RowVersion = Guid.NewGuid();
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task RevokeDisputedChargeCreditsAsync(
        JsonElement stripeObject,
        CancellationToken cancellationToken)
    {
        var paymentIntentId = GetString(stripeObject, "payment_intent");
        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            return;
        }

        await using var db = dbContextFactory();
        var credits = await db.RewriteCredits
            .AsTracking()
            .Where(x => x.StripePaymentIntentId == paymentIntentId)
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var credit in credits)
        {
            if (credit.AmountGranted == credit.AmountConsumed)
            {
                continue;
            }

            credit.AmountGranted = credit.AmountConsumed;
            credit.RowVersion = Guid.NewGuid();
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static bool IsPaidPaymentSession(JsonElement stripeObject) =>
        GetString(stripeObject, "mode") == "payment" &&
        GetString(stripeObject, "payment_status") == "paid";

    private static bool IsFullRefund(JsonElement stripeObject)
    {
        if (GetBool(stripeObject, "refunded") == true)
        {
            return true;
        }

        var amount = GetLong(stripeObject, "amount");
        var amountRefunded = GetLong(stripeObject, "amount_refunded");
        return amount is > 0 && amountRefunded >= amount;
    }

    private static int? ResolveRefundedCreditCount(JsonElement stripeObject, RewriteCredit credit)
    {
        var amount = GetLong(stripeObject, "amount") ?? credit.StripeAmountTotal;
        var amountRefunded = GetLong(stripeObject, "amount_refunded");
        if (amount is not > 0 || amountRefunded is not > 0)
        {
            return null;
        }

        var boundedRefundedAmount = Math.Min(amountRefunded.Value, amount.Value);
        var proportionalCredits = (decimal)credit.AmountGranted * boundedRefundedAmount / amount.Value;
        return (int)Math.Ceiling(proportionalCredits);
    }

    private static int? ResolveGrantedRewrites(JsonElement stripeObject)
    {
        var metadataRewrites = GetMetadataString(stripeObject, "rewrites");
        if (int.TryParse(metadataRewrites, out var parsedRewrites) && parsedRewrites > 0)
        {
            return parsedRewrites;
        }

        var sku = GetMetadataString(stripeObject, "sku");
        return StripeBillingService.TryGetSkuDefinition(sku, out var definition)
            ? definition!.Rewrites
            : null;
    }

    private static SubscriptionStatus MapSubscriptionStatus(string? status) =>
        status switch
        {
            "active" => SubscriptionStatus.Active,
            "trialing" => SubscriptionStatus.Trialing,
            "canceled" => SubscriptionStatus.Canceled,
            _ => SubscriptionStatus.Inactive,
        };

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.ToString();
    }

    private static string? GetMetadataString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty("metadata", out var metadata) ||
            metadata.ValueKind != JsonValueKind.Object ||
            !metadata.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static long? GetLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var parsed) => parsed,
            JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private static bool? GetBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private static DateTimeOffset? GetUnixDateTime(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        var seconds = value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var parsed) => parsed,
            JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
            _ => (long?)null,
        };

        return seconds is null ? null : DateTimeOffset.FromUnixTimeSeconds(seconds.Value);
    }
}
