using System.Text.Json;
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
            CreatedAt = now,
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
        var processed = await TryMarkProcessedAsync(eventId, type, now, cancellationToken);
        if (!processed)
        {
            return false;
        }

        await SyncEntitlementAsync(type, rawBody, now, cancellationToken);
        return true;
    }

    private async Task SyncEntitlementAsync(
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
            await SyncCheckoutSessionAsync(stripeObject, now, cancellationToken);
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
            ? SubscriptionStatus.Inactive
            : MapSubscriptionStatus(GetString(stripeObject, "status"));

        user.StripeSubscriptionId = GetString(stripeObject, "id") ?? user.StripeSubscriptionId;
        user.SubscriptionStatus = status;
        user.CurrentPeriodEnd = GetUnixDateTime(stripeObject, "current_period_end");
        user.UpdatedAt = now;
        user.RowVersion = Guid.NewGuid();

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncCheckoutSessionAsync(
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

        await db.SaveChangesAsync(cancellationToken);
    }

    private static SubscriptionStatus MapSubscriptionStatus(string? status) =>
        status switch
        {
            "active" => SubscriptionStatus.Active,
            "trialing" => SubscriptionStatus.Trialing,
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
