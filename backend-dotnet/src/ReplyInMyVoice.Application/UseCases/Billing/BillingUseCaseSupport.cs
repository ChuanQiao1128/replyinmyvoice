namespace ReplyInMyVoice.Application.UseCases.Billing;

internal static class BillingUseCaseSupport
{
    public static string NormalizeExternalAuthUserId(string externalAuthUserId)
    {
        var normalized = externalAuthUserId.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 160)
        {
            throw new ArgumentException("A valid external auth user id is required.", nameof(externalAuthUserId));
        }

        return normalized;
    }

    public static string NormalizePaymentIntentId(string paymentIntentId)
    {
        var normalized = paymentIntentId.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("A valid payment intent id is required.", nameof(paymentIntentId));
        }

        return normalized;
    }

    public static string? NormalizeEmail(string? email)
    {
        var normalized = email?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= 320 ? normalized : normalized[..320];
    }

    public static string? NormalizeCurrency(string? currency)
    {
        var normalized = currency?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized.ToLowerInvariant();
    }
}
