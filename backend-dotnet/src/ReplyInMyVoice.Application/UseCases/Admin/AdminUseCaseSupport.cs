using System.Text.Json;

namespace ReplyInMyVoice.Application.UseCases.Admin;

internal static class AdminUseCaseSupport
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string SerializeAuditDetails<T>(T details) =>
        JsonSerializer.Serialize(details, JsonOptions);

    public static bool IsPaymentCredit(
        string source,
        string? stripeEventId,
        string? paymentIntentId,
        string? sku,
        long? amountTotal,
        string? currency) =>
        !string.IsNullOrWhiteSpace(paymentIntentId) ||
        !string.IsNullOrWhiteSpace(stripeEventId) ||
        !string.IsNullOrWhiteSpace(sku) ||
        !string.IsNullOrWhiteSpace(currency) ||
        amountTotal is not null ||
        string.Equals(source, "PURCHASE", StringComparison.OrdinalIgnoreCase);
}
