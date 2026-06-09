namespace ReplyInMyVoice.Application.Common;

public sealed record AccountSummaryDto(
    Guid Id,
    string ExternalAuthUserId,
    string? Email,
    string SubscriptionStatus,
    DateTimeOffset? PaymentGraceEndsAt,
    AccountUsageSummaryDto Usage,
    AccountPromoSummaryDto Promo);

public sealed record AccountUsageSummaryDto(
    string Scope,
    string PeriodKey,
    int Quota,
    int Used,
    int Reserved,
    int Remaining,
    bool Exhausted)
{
    public IReadOnlyList<AccountUsageSourceDto> Sources { get; init; } = Array.Empty<AccountUsageSourceDto>();
}

public sealed record AccountUsageSourceDto(
    string Source,
    string Label,
    int Used,
    int Limit,
    int Reserved,
    int Remaining,
    DateTimeOffset? ExpiresAt,
    int? ExpiresInDays);

public sealed record AccountPromoSummaryDto(
    bool HasRedeemed,
    bool Eligible,
    int TrialRemaining,
    DateTimeOffset? TrialExpiresAt);

public sealed record AccountPaymentDto(
    string? Sku,
    string? PaymentIntentId,
    long? Amount,
    string? Currency,
    string? ReceiptUrl,
    DateTimeOffset Date,
    DateTimeOffset? Expiry,
    int Remaining);

public sealed record AccountBillingHistoryItemDto(
    string Type,
    DateTimeOffset Date,
    string Description,
    long? Amount,
    string? Currency,
    string Status,
    string? ReceiptUrl,
    string? HostedInvoiceUrl);

public sealed record AccountUsagePlanDto(string Scope, string PeriodKey, int QuotaLimit);
