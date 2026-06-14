using System.Globalization;
using System.Text.Json;

namespace ReplyInMyVoice.Application.Common;

public sealed record StripeWebhookPayloadDto(
    string EventId,
    string Type,
    StripeWebhookObjectDto Object,
    DateTimeOffset? EventCreatedAt = null)
{
    public StripeWebhookPayloadDto(
        string eventId,
        string type,
        string rawBody)
        : this(eventId, type, ParseObject(rawBody), ParseEventCreatedAt(rawBody))
    {
    }

    private static StripeWebhookObjectDto ParseObject(string rawBody)
    {
        using var document = JsonDocument.Parse(rawBody);
        if (!document.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("object", out var stripeObject) ||
            stripeObject.ValueKind != JsonValueKind.Object)
        {
            return new StripeWebhookObjectDto();
        }

        return new StripeWebhookObjectDto(
            Id: GetString(stripeObject, "id"),
            CustomerId: GetString(stripeObject, "customer"),
            SubscriptionId: GetInvoiceSubscriptionId(stripeObject) ?? GetString(stripeObject, "subscription"),
            ExternalAuthUserId: ResolveExternalAuthUserId(stripeObject),
            Status: GetString(stripeObject, "status"),
            CurrentPeriodEnd: GetSubscriptionPeriodEnd(stripeObject),
            CheckoutMode: GetString(stripeObject, "mode"),
            PaymentStatus: GetString(stripeObject, "payment_status"),
            Sku: GetMetadataString(stripeObject, "sku"),
            GrantedRewrites: GetMetadataInt32(stripeObject, "rewrites"),
            PaymentIntentId: ResolvePaymentIntentId(stripeObject),
            ReceiptUrl: ResolveReceiptUrl(stripeObject),
            AmountTotal: GetLong(stripeObject, "amount_total"),
            Currency: GetString(stripeObject, "currency"),
            Amount: GetLong(stripeObject, "amount"),
            AmountRefunded: GetLong(stripeObject, "amount_refunded"),
            Refunded: GetBool(stripeObject, "refunded"),
            AmountDue: GetLong(stripeObject, "amount_due"),
            AmountPaid: GetLong(stripeObject, "amount_paid"),
            PeriodStart: GetInvoicePeriodDate(stripeObject, "period_start", "start"),
            PeriodEnd: GetInvoicePeriodDate(stripeObject, "period_end", "end"),
            AttemptCount: GetInt32(stripeObject, "attempt_count"),
            NextPaymentAttempt: GetUnixDateTime(stripeObject, "next_payment_attempt"),
            DueDate: GetUnixDateTime(stripeObject, "due_date"),
            HostedInvoiceUrl: GetString(stripeObject, "hosted_invoice_url"),
            InvoicePdf: GetString(stripeObject, "invoice_pdf"),
            BillingReason: GetString(stripeObject, "billing_reason"),
            CardBrand: GetString(stripeObject, "brand"),
            CardLast4: GetString(stripeObject, "last4"),
            CardExpMonth: GetNullableInt32(stripeObject, "exp_month"),
            CardExpYear: GetNullableInt32(stripeObject, "exp_year"));
    }

    private static DateTimeOffset? ParseEventCreatedAt(string rawBody)
    {
        using var document = JsonDocument.Parse(rawBody);
        return GetUnixDateTime(document.RootElement, "created");
    }

    private static string? ResolveExternalAuthUserId(JsonElement stripeObject) =>
        GetString(stripeObject, "client_reference_id") ??
        GetMetadataString(stripeObject, "externalAuthUserId") ??
        GetMetadataString(stripeObject, "clerkUserId");

    private static string? ResolvePaymentIntentId(JsonElement stripeObject)
    {
        if (!stripeObject.TryGetProperty("payment_intent", out var paymentIntent))
        {
            return null;
        }

        return paymentIntent.ValueKind == JsonValueKind.Object
            ? GetString(paymentIntent, "id")
            : GetString(stripeObject, "payment_intent");
    }

    private static string? ResolveReceiptUrl(JsonElement stripeObject)
    {
        if (!stripeObject.TryGetProperty("payment_intent", out var paymentIntent) ||
            paymentIntent.ValueKind != JsonValueKind.Object ||
            !paymentIntent.TryGetProperty("latest_charge", out var latestCharge) ||
            latestCharge.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var receiptUrl = GetString(latestCharge, "receipt_url");
        return string.IsNullOrWhiteSpace(receiptUrl) ? null : receiptUrl;
    }

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

    private static int? GetMetadataInt32(JsonElement element, string propertyName)
    {
        var value = GetMetadataString(element, propertyName);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
            parsed > 0
            ? parsed
            : null;
    }

    private static string? GetInvoiceSubscriptionId(JsonElement stripeObject)
    {
        var subscriptionId = GetString(stripeObject, "subscription");
        if (!string.IsNullOrWhiteSpace(subscriptionId))
        {
            return subscriptionId;
        }

        if (stripeObject.TryGetProperty("parent", out var parent) &&
            parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty("subscription_details", out var details) &&
            details.ValueKind == JsonValueKind.Object)
        {
            return GetString(details, "subscription");
        }

        return null;
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
            JsonValueKind.String when long.TryParse(
                value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => null,
        };
    }

    private static int GetInt32(JsonElement element, string propertyName)
    {
        var value = GetLong(element, propertyName);
        return value is null
            ? 0
            : (int)Math.Clamp(value.Value, int.MinValue, int.MaxValue);
    }

    private static int? GetNullableInt32(JsonElement element, string propertyName)
    {
        var value = GetLong(element, propertyName);
        return value is null
            ? null
            : (int)Math.Clamp(value.Value, int.MinValue, int.MaxValue);
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
            JsonValueKind.String when long.TryParse(
                value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => (long?)null,
        };

        return seconds is null ? null : DateTimeOffset.FromUnixTimeSeconds(seconds.Value);
    }

    private static DateTimeOffset? GetInvoicePeriodDate(
        JsonElement stripeObject,
        string topLevelPropertyName,
        string nestedPropertyName)
    {
        var topLevelDate = GetUnixDateTime(stripeObject, topLevelPropertyName);
        if (topLevelDate is not null)
        {
            return topLevelDate;
        }

        if (!stripeObject.TryGetProperty("lines", out var lines) ||
            lines.ValueKind != JsonValueKind.Object ||
            !lines.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var line in data.EnumerateArray())
        {
            if (line.ValueKind != JsonValueKind.Object ||
                !line.TryGetProperty("period", out var period) ||
                period.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var date = GetUnixDateTime(period, nestedPropertyName);
            if (date is not null)
            {
                return date;
            }
        }

        return null;
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
}

public sealed record StripeWebhookObjectDto(
    string? Id = null,
    string? CustomerId = null,
    string? SubscriptionId = null,
    string? ExternalAuthUserId = null,
    string? Status = null,
    DateTimeOffset? CurrentPeriodEnd = null,
    string? CheckoutMode = null,
    string? PaymentStatus = null,
    string? Sku = null,
    int? GrantedRewrites = null,
    string? PaymentIntentId = null,
    string? ReceiptUrl = null,
    long? AmountTotal = null,
    string? Currency = null,
    long? Amount = null,
    long? AmountRefunded = null,
    bool? Refunded = null,
    long? AmountDue = null,
    long? AmountPaid = null,
    DateTimeOffset? PeriodStart = null,
    DateTimeOffset? PeriodEnd = null,
    int AttemptCount = 0,
    DateTimeOffset? NextPaymentAttempt = null,
    DateTimeOffset? DueDate = null,
    string? HostedInvoiceUrl = null,
    string? InvoicePdf = null,
    string? BillingReason = null,
    string? CardBrand = null,
    string? CardLast4 = null,
    int? CardExpMonth = null,
    int? CardExpYear = null);
