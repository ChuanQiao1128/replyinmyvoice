namespace ReplyInMyVoice.Application.Common;

public sealed record StripeReconciliationReportDto(
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    DateTimeOffset CompletedAt,
    int StripePaymentCount,
    int PurchaseGrantCount,
    int PaidButNoGrantCount,
    int GrantButNoPaymentCount,
    int AmountMismatchCount,
    IReadOnlyList<StripeReconciliationDiscrepancyDto> Discrepancies)
{
    public int DiscrepancyCount => PaidButNoGrantCount + GrantButNoPaymentCount + AmountMismatchCount;

    public static StripeReconciliationReportDto Create(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        DateTimeOffset completedAt,
        int stripePaymentCount,
        int purchaseGrantCount,
        IReadOnlyList<StripeReconciliationDiscrepancyDto> discrepancies)
    {
        return new StripeReconciliationReportDto(
            windowStart,
            windowEnd,
            completedAt,
            stripePaymentCount,
            purchaseGrantCount,
            discrepancies.Count(x => x.Kind == StripeReconciliationDiscrepancyKindDto.PaidButNoGrant),
            discrepancies.Count(x => x.Kind == StripeReconciliationDiscrepancyKindDto.GrantButNoPayment),
            discrepancies.Count(x => x.Kind == StripeReconciliationDiscrepancyKindDto.AmountMismatch),
            discrepancies);
    }
}

public sealed record StripeReconciliationDiscrepancyDto(
    StripeReconciliationDiscrepancyKindDto Kind,
    string? PaymentIntentId,
    Guid? CreditId,
    long? StripeAmount,
    long? LedgerAmount,
    string? StripeCurrency,
    string? LedgerCurrency,
    DateTimeOffset? StripePaidAt,
    DateTimeOffset? LedgerGrantedAt);

public enum StripeReconciliationDiscrepancyKindDto
{
    PaidButNoGrant,
    GrantButNoPayment,
    AmountMismatch,
}
