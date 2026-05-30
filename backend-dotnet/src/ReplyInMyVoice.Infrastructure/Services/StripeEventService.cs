using System.Text.Json;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class StripeEventService
{
    private readonly Func<AppDbContext> dbContextFactory;
    private readonly ILogger<StripeEventService>? logger;

    public StripeEventService(
        Func<AppDbContext> dbContextFactory,
        ILogger<StripeEventService>? logger = null)
    {
        StripeBillingService.EnsureStripeApiVersionPinned();
        this.dbContextFactory = dbContextFactory;
        this.logger = logger;
    }

    public async Task<bool> TryMarkProcessedAsync(
        string eventId,
        string type,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        using var scope = BeginEventScope(eventId);
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
            logger?.LogInformation(
                "Stripe webhook event marked processed for event {EventId} of type {EventType}.",
                eventId,
                type);
            return true;
        }
        catch (DbUpdateException ex)
        {
            logger?.LogInformation(
                ex,
                "Stripe webhook event already exists for event {EventId} of type {EventType}; skipping duplicate processed mark.",
                eventId,
                type);
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
        using var scope = BeginEventScope(eventId);
        try
        {
            var processed = await ExecuteInTransactionAsync(async db =>
            {
                var stripeEvent = await TryBeginProcessingAsync(db, eventId, type, now, cancellationToken);
                if (stripeEvent is null)
                {
                    return false;
                }

                var syncFailure = await SyncEntitlementAsync(db, eventId, type, rawBody, now, cancellationToken);
                if (syncFailure is not null)
                {
                    MarkFailed(stripeEvent, syncFailure, now);
                    await db.SaveChangesAsync(cancellationToken);
                    return false;
                }

                MarkProcessed(stripeEvent, now);
                await db.SaveChangesAsync(cancellationToken);
                return true;
            }, cancellationToken);

            if (processed)
            {
                logger?.LogInformation(
                    "Stripe webhook processed for event {EventId} of type {EventType}.",
                    eventId,
                    type);
            }
            else
            {
                logger?.LogInformation(
                    "Stripe webhook skipped for event {EventId} of type {EventType} because it is already processed, locked, or awaiting retry.",
                    eventId,
                    type);
            }

            return processed;
        }
        catch (DbUpdateException ex)
            when (type == "checkout.session.completed" && IsStripeEventCreditUniqueConstraintViolation(ex))
        {
            await MarkProcessedAfterCheckoutGrantConflictAsync(eventId, type, now, cancellationToken);
            logger?.LogInformation(
                ex,
                "Stripe checkout credit grant already exists for event {EventId}; treating webhook as an idempotent success.",
                eventId);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var failure = await MarkFailedAsync(eventId, type, ex.Message, now, cancellationToken);
            logger?.LogError(
                ex,
                "Stripe webhook failed for event {EventId} of type {EventType} after {AttemptCount} attempt(s).",
                eventId,
                type,
                failure?.AttemptCount ?? 0);
            throw;
        }
    }

    private IDisposable? BeginEventScope(string eventId) =>
        logger?.BeginScope(new Dictionary<string, object>
        {
            ["eventId"] = eventId,
        });

    private async Task<StripeEvent?> TryBeginProcessingAsync(
        AppDbContext db,
        string eventId,
        string type,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var stripeEvent = await db.StripeEvents
            .AsTracking()
            .SingleOrDefaultAsync(x => x.EventId == eventId, cancellationToken);

        if (stripeEvent is null)
        {
            stripeEvent = new StripeEvent
            {
                EventId = eventId,
                Type = type,
                Status = StripeEventStatus.Processing,
                AttemptCount = 1,
                CreatedAt = now,
                LastAttemptAt = now,
                LockedUntil = now.AddMinutes(2),
            };
            db.StripeEvents.Add(stripeEvent);
            return stripeEvent;
        }

        if (stripeEvent.Status == StripeEventStatus.Processed)
        {
            return null;
        }

        if (stripeEvent.Status == StripeEventStatus.Processing && stripeEvent.LockedUntil > now)
        {
            return null;
        }

        stripeEvent.Type = type;
        stripeEvent.Status = StripeEventStatus.Processing;
        stripeEvent.AttemptCount += 1;
        stripeEvent.LastAttemptAt = now;
        stripeEvent.LockedUntil = now.AddMinutes(2);
        stripeEvent.LastError = null;
        stripeEvent.RowVersion = Guid.NewGuid();

        return stripeEvent;
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

    private static void MarkProcessed(StripeEvent stripeEvent, DateTimeOffset now)
    {
        stripeEvent.Status = StripeEventStatus.Processed;
        stripeEvent.ProcessedAt = now;
        stripeEvent.LockedUntil = null;
        stripeEvent.LastError = null;
        stripeEvent.RowVersion = Guid.NewGuid();
    }

    private static void MarkFailed(StripeEvent stripeEvent, string error, DateTimeOffset now)
    {
        stripeEvent.Status = StripeEventStatus.Failed;
        stripeEvent.ProcessedAt = null;
        stripeEvent.LockedUntil = null;
        stripeEvent.LastAttemptAt = now;
        stripeEvent.LastError = TruncateStripeEventError(error);
        stripeEvent.RowVersion = Guid.NewGuid();
    }

    private async Task<StripeFailureLogInfo?> MarkFailedAsync(
        string eventId,
        string type,
        string error,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await ExecuteInTransactionAsync(async db =>
        {
            var stripeEvent = await db.StripeEvents
                .AsTracking()
                .SingleOrDefaultAsync(x => x.EventId == eventId, cancellationToken);

            if (stripeEvent?.Status == StripeEventStatus.Processed)
            {
                return new StripeFailureLogInfo(stripeEvent.AttemptCount, stripeEvent.Status);
            }

            var truncatedError = error.Length > 1000 ? error[..1000] : error;
            if (stripeEvent is null)
            {
                stripeEvent = new StripeEvent
                {
                    EventId = eventId,
                    Type = type,
                    Status = StripeEventStatus.Failed,
                    AttemptCount = 1,
                    CreatedAt = now,
                    LastAttemptAt = now,
                    LastError = truncatedError,
                };
                db.StripeEvents.Add(stripeEvent);
            }
            else
            {
                stripeEvent.Type = type;
                stripeEvent.AttemptCount += 1;
                MarkFailed(stripeEvent, truncatedError, now);
            }

            await db.SaveChangesAsync(cancellationToken);
            return new StripeFailureLogInfo(stripeEvent.AttemptCount, stripeEvent.Status);
        }, cancellationToken);
    }

    private async Task MarkProcessedAfterCheckoutGrantConflictAsync(
        string eventId,
        string type,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await ExecuteInTransactionAsync(async db =>
        {
            var stripeEvent = await db.StripeEvents
                .AsTracking()
                .SingleOrDefaultAsync(x => x.EventId == eventId, cancellationToken);

            if (stripeEvent is null)
            {
                stripeEvent = new StripeEvent
                {
                    EventId = eventId,
                    Type = type,
                    Status = StripeEventStatus.Processed,
                    AttemptCount = 1,
                    CreatedAt = now,
                    LastAttemptAt = now,
                    ProcessedAt = now,
                };
                db.StripeEvents.Add(stripeEvent);
            }
            else if (stripeEvent.Status != StripeEventStatus.Processed)
            {
                stripeEvent.Type = type;
                stripeEvent.AttemptCount += 1;
                stripeEvent.LastAttemptAt = now;
                MarkProcessed(stripeEvent, now);
            }

            await db.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    private async Task<string?> SyncEntitlementAsync(
        AppDbContext db,
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
            return null;
        }

        if (type.StartsWith("customer.subscription.", StringComparison.Ordinal))
        {
            return await SyncSubscriptionObjectAsync(db, type, stripeObject, now, cancellationToken);
        }

        if (type == "checkout.session.completed")
        {
            return await SyncCheckoutSessionAsync(db, eventId, stripeObject, now, cancellationToken);
        }

        if (type == "charge.refunded")
        {
            await RevokeRefundedChargeCreditsAsync(db, stripeObject, cancellationToken);
            return null;
        }

        if (type is "charge.dispute.created" or "charge.dispute.closed")
        {
            await RevokeDisputedChargeCreditsAsync(db, stripeObject, cancellationToken);
        }

        return null;
    }

    private async Task<string?> SyncSubscriptionObjectAsync(
        AppDbContext db,
        string type,
        JsonElement stripeObject,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var customerId = GetString(stripeObject, "customer");
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return null;
        }

        var user = await db.AppUsers
            .AsTracking()
            .SingleOrDefaultAsync(x => x.StripeCustomerId == customerId, cancellationToken);
        if (user is null)
        {
            return $"No matching user for Stripe subscription customer {customerId}.";
        }

        var status = type == "customer.subscription.deleted"
            ? SubscriptionStatus.Canceled
            : MapSubscriptionStatus(GetString(stripeObject, "status"));

        user.StripeSubscriptionId = GetString(stripeObject, "id") ?? user.StripeSubscriptionId;
        user.SubscriptionStatus = status;
        user.CurrentPeriodEnd = GetSubscriptionPeriodEnd(stripeObject);
        user.UpdatedAt = now;
        user.RowVersion = Guid.NewGuid();
        return null;
    }

    private async Task<string?> SyncCheckoutSessionAsync(
        AppDbContext db,
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
            return null;
        }

        var user = await db.AppUsers.AsTracking().SingleOrDefaultAsync(
            x => (!string.IsNullOrWhiteSpace(externalAuthUserId) && x.ExternalAuthUserId == externalAuthUserId) ||
                 (!string.IsNullOrWhiteSpace(customerId) && x.StripeCustomerId == customerId),
            cancellationToken);
        if (user is null)
        {
            return RequiresCheckoutUser(stripeObject)
                ? $"No matching user for Stripe checkout session customer {customerId ?? "unknown"}."
                : null;
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

        return null;
    }

    private async Task RevokeRefundedChargeCreditsAsync(
        AppDbContext db,
        JsonElement stripeObject,
        CancellationToken cancellationToken)
    {
        var paymentIntentId = GetString(stripeObject, "payment_intent");
        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            return;
        }

        var credits = await db.RewriteCredits
            .AsTracking()
            .Where(x => x.StripePaymentIntentId == paymentIntentId)
            .ToListAsync(cancellationToken);

        foreach (var credit in credits)
        {
            var previousGranted = credit.AmountGranted;
            if (IsFullRefund(stripeObject))
            {
                credit.AmountGranted = credit.AmountConsumed;
            }
            else if (ResolveRemainingGrantedAfterRefund(stripeObject, credit) is { } targetGranted)
            {
                credit.AmountGranted = targetGranted;
            }

            if (credit.AmountGranted != previousGranted)
            {
                credit.RowVersion = Guid.NewGuid();
            }
        }
    }

    private async Task RevokeDisputedChargeCreditsAsync(
        AppDbContext db,
        JsonElement stripeObject,
        CancellationToken cancellationToken)
    {
        var paymentIntentId = GetString(stripeObject, "payment_intent");
        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            return;
        }

        var credits = await db.RewriteCredits
            .AsTracking()
            .Where(x => x.StripePaymentIntentId == paymentIntentId)
            .ToListAsync(cancellationToken);

        foreach (var credit in credits)
        {
            if (credit.AmountGranted == credit.AmountConsumed)
            {
                continue;
            }

            credit.AmountGranted = credit.AmountConsumed;
            credit.RowVersion = Guid.NewGuid();
        }
    }

    private static bool IsPaidPaymentSession(JsonElement stripeObject) =>
        GetString(stripeObject, "mode") == "payment" &&
        GetString(stripeObject, "payment_status") == "paid";

    private static bool RequiresCheckoutUser(JsonElement stripeObject) =>
        IsPaidPaymentSession(stripeObject) ||
        GetString(stripeObject, "mode") == "subscription" ||
        !string.IsNullOrWhiteSpace(GetString(stripeObject, "subscription"));

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

    private static int? ResolveRemainingGrantedAfterRefund(JsonElement stripeObject, RewriteCredit credit)
    {
        var amount = GetLong(stripeObject, "amount") ?? credit.StripeAmountTotal;
        var amountRefunded = GetLong(stripeObject, "amount_refunded");
        var originalGranted = ResolveOriginalGrantedRewrites(credit);
        if (amount is not > 0 || amountRefunded is not > 0 || originalGranted is not > 0)
        {
            return null;
        }

        var boundedRefundedAmount = Math.Min(amountRefunded.Value, amount.Value);
        var refundedCredits = (int)Math.Ceiling((decimal)originalGranted.Value * boundedRefundedAmount / amount.Value);
        return Math.Max(credit.AmountConsumed, originalGranted.Value - refundedCredits);
    }

    private static int? ResolveOriginalGrantedRewrites(RewriteCredit credit)
    {
        return StripeBillingService.TryGetSkuDefinition(credit.StripeSku, out var definition)
            ? definition!.Rewrites
            : null;
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

    private static string TruncateStripeEventError(string error) =>
        error.Length > 1000 ? error[..1000] : error;

    private static bool IsStripeEventCreditUniqueConstraintViolation(DbUpdateException exception)
    {
        var message = exception.ToString();
        return message.Contains("IX_RewriteCredits_StripeEventId", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("RewriteCredits.StripeEventId", StringComparison.OrdinalIgnoreCase);
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

    private static DateTimeOffset? GetSubscriptionPeriodEnd(JsonElement stripeObject)
    {
        var topLevelPeriodEnd = GetUnixDateTime(stripeObject, "current_period_end");
        if (topLevelPeriodEnd is not null)
        {
            return topLevelPeriodEnd;
        }

        if (!stripeObject.TryGetProperty("items", out var items))
        {
            return null;
        }

        if (items.ValueKind == JsonValueKind.Array)
        {
            return GetFirstItemPeriodEnd(items);
        }

        if (items.ValueKind == JsonValueKind.Object &&
            items.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Array)
        {
            return GetFirstItemPeriodEnd(data);
        }

        return null;
    }

    private static DateTimeOffset? GetFirstItemPeriodEnd(JsonElement items)
    {
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var periodEnd = GetUnixDateTime(item, "current_period_end");
            if (periodEnd is not null)
            {
                return periodEnd;
            }
        }

        return null;
    }

    private sealed record StripeFailureLogInfo(int AttemptCount, StripeEventStatus Status);
}
