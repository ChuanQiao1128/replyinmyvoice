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
    IReadOnlyList<StripeReconciliationDiscrepancyDto> Discrepancies,
    IReadOnlyList<StripeReconciliationAutoGrantDto> AutoGrants,
    IReadOnlyList<StripeReconciliationManualReviewDto> ManualReview,
    IReadOnlyList<StripeSubscriptionDiscrepancyDto> SubscriptionMismatches,
    int AutoGrantedCount,
    int AutoGrantSkippedCount,
    int ManualReviewCount,
    int SubscriptionMismatchCount)
{
    public int DiscrepancyCount => PaidButNoGrantCount + GrantButNoPaymentCount + AmountMismatchCount;

    public static StripeReconciliationReportDto Create(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        DateTimeOffset completedAt,
        int stripePaymentCount,
        int purchaseGrantCount,
        IReadOnlyList<StripeReconciliationDiscrepancyDto> discrepancies,
        IReadOnlyList<StripeReconciliationAutoGrantDto>? autoGrants = null,
        IReadOnlyList<StripeReconciliationManualReviewDto>? manualReview = null,
        IReadOnlyList<StripeSubscriptionDiscrepancyDto>? subscriptionMismatches = null,
        int autoGrantSkippedCount = 0)
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
            discrepancies,
            autoGrants ?? [],
            manualReview ?? [],
            subscriptionMismatches ?? [],
            autoGrants?.Count ?? 0,
            autoGrantSkippedCount,
            manualReview?.Count ?? 0,
            subscriptionMismatches?.Count ?? 0);
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

public sealed record StripeReconciliationAutoGrantDto(
    string PaymentIntentId,
    Guid CreditId,
    Guid UserId,
    int Rewrites,
    string? Sku);

public sealed record StripeReconciliationManualReviewDto(
    string PaymentIntentId,
    string Reason);

public sealed record StripeCheckoutSessionSnapshotDto(
    string SessionId,
    string? PaymentIntentId,
    string? Mode,
    string? PaymentStatus,
    string? ExternalAuthUserId,
    string? CustomerId,
    string? Sku,
    int? GrantedRewrites,
    long? AmountTotal,
    string? Currency);

public sealed record StripeSubscriptionSnapshotDto(
    string SubscriptionId,
    string? CustomerId,
    string? Status);

public sealed record StripeSubscriptionDiscrepancyDto(
    string Kind,
    string? SubscriptionId,
    string? CustomerId,
    Guid? UserId,
    string? StripeStatus,
    string LocalStatus);
