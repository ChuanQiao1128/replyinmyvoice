namespace ReplyInMyVoice.Application.Common;

public sealed record AdminUsersListDto(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyList<AdminUserListItemDto> Users);

public sealed record AdminUserListItemDto(
    Guid Id,
    string ExternalAuthUserId,
    string? Email,
    string SubscriptionStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int UsedRewrites,
    int ReservedRewrites,
    int CreditRemaining,
    decimal CostToDateUsd);

public sealed record AdminUserDetailDto(
    Guid Id,
    string ExternalAuthUserId,
    string? Email,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    AdminSubscriptionSummaryDto Subscription,
    IReadOnlyList<AdminUsagePeriodDto> Usage,
    IReadOnlyList<AdminCreditDto> Credits,
    IReadOnlyList<AdminPaymentDto> Payments,
    decimal CostToDateUsd);

public sealed record AdminSubscriptionSummaryDto(
    string Status,
    string? StripeCustomerId,
    string? StripeSubscriptionId,
    DateTimeOffset? CurrentPeriodEnd);

public sealed record AdminUsagePeriodDto(
    Guid Id,
    string PeriodKey,
    int Quota,
    int Used,
    int Reserved,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminCreditDto(
    Guid Id,
    string Source,
    int AmountGranted,
    int AmountConsumed,
    int Remaining,
    DateTimeOffset GrantedAt,
    DateTimeOffset? ExpiresAt,
    string? StripeEventId,
    string? PaymentIntentId,
    string? Sku,
    long? AmountTotal,
    string? Currency,
    string? ReceiptUrl);

public sealed record AdminPaymentDto(
    Guid CreditId,
    string Source,
    string? EventId,
    string? PaymentIntentId,
    string? Sku,
    long? AmountTotal,
    string? Currency,
    string? ReceiptUrl,
    DateTimeOffset GrantedAt,
    DateTimeOffset? ExpiresAt,
    int CreditsGranted,
    int CreditsConsumed,
    int CreditsRemaining);

public sealed record AdminStatsDto(
    int TotalUsers,
    int PaidUsers,
    int FreeUsers,
    int UsageUsed,
    int UsageReserved,
    int CreditRemaining,
    int PaymentCount,
    long PaymentAmountTotal,
    decimal CostToDateUsd,
    TaxTurnoverReportDto GstTurnover,
    AdminPaymentReconciliationSummaryDto? PaymentReconciliation,
    AdminRefundReviewStatsDto RefundReview);

public sealed record AdminPaymentReconciliationSummaryDto(
    DateTimeOffset LastCompletedAt,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    int DiscrepancyCount,
    int PaidButNoGrantCount,
    int GrantButNoPaymentCount,
    int AmountMismatchCount,
    int StripePaymentCount,
    int PurchaseGrantCount);

public sealed record AdminRefundReviewStatsDto(
    int FlaggedUserCount,
    int RefundCountThreshold,
    long RefundAmountThreshold,
    int TotalRefundCount,
    long TotalRefundAmount);

public sealed record AdminCreditGrantResponseDto(
    Guid TargetUserId,
    Guid CreditId,
    string Source,
    int AmountGranted,
    int AmountConsumed,
    int Remaining,
    DateTimeOffset GrantedAt,
    DateTimeOffset? ExpiresAt);

public sealed record AdminCreditGrantResultDto(
    AdminCreditGrantResultKind Kind,
    AdminCreditGrantResponseDto? Response,
    string? Detail)
{
    public static AdminCreditGrantResultDto Success(AdminCreditGrantResponseDto response) =>
        new(AdminCreditGrantResultKind.Success, response, null);

    public static AdminCreditGrantResultDto InvalidRequest(string detail) =>
        new(AdminCreditGrantResultKind.InvalidRequest, null, detail);

    public static AdminCreditGrantResultDto UserNotFound(string detail) =>
        new(AdminCreditGrantResultKind.UserNotFound, null, detail);
}

public enum AdminCreditGrantResultKind
{
    Success,
    InvalidRequest,
    UserNotFound,
}

public sealed record AdminCreditGrantAuditDetailsDto(
    Guid CreditId,
    string Source,
    int AmountGranted,
    DateTimeOffset GrantedAt,
    DateTimeOffset? ExpiresAt,
    string? Reason);

public sealed record AdminDeleteUserResponseDto(
    Guid UserId,
    string Status);

public sealed record AdminDeleteUserResultDto(
    AdminDeleteUserResultKind Kind,
    AdminDeleteUserResponseDto? Response,
    string? Detail)
{
    public static AdminDeleteUserResultDto Success(AdminDeleteUserResponseDto response) =>
        new(AdminDeleteUserResultKind.Success, response, null);

    public static AdminDeleteUserResultDto UserNotFound(string detail) =>
        new(AdminDeleteUserResultKind.UserNotFound, null, detail);

    public static AdminDeleteUserResultDto Forbidden(string detail) =>
        new(AdminDeleteUserResultKind.Forbidden, null, detail);
}

public enum AdminDeleteUserResultKind
{
    Success,
    UserNotFound,
    Forbidden,
}

public sealed record AdminDeleteUserAuditDetailsDto(
    string Status,
    DateTimeOffset DeletedAt);

public sealed record AdminDeleteUserLookupDto(
    Guid UserId,
    string ExternalAuthUserId,
    string? StripeSubscriptionId);

public sealed record AdminBillingSupportRequestDto(
    Guid Id,
    Guid UserId,
    string? UserEmail,
    string? ExternalAuthUserId,
    string Type,
    string? RelatedPaymentIntentId,
    string Message,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt);

public sealed record AdminBillingSupportResolveAuditDetailsDto(
    Guid RequestId,
    string Type,
    string? RelatedPaymentIntentId,
    DateTimeOffset ResolvedAt);

public sealed record AdminAccountingRevenueExportDto(
    IReadOnlyList<AdminAccountingRevenueRowDto> Rows);

public sealed record AdminAccountingRevenueRowDto(
    Guid CreditId,
    Guid UserId,
    DateTimeOffset GrantedAt,
    string? Sku,
    long? AmountTotal,
    string? Currency,
    string? PaymentIntentId,
    int AmountGranted,
    int AmountConsumed,
    int CreditsRemaining);

public sealed record AdminSuspensionMutationDto(
    Guid TargetUserId,
    bool Suspended,
    DateTimeOffset? SuspendedAt);

public sealed record AdminSuspensionResponseDto(
    Guid TargetUserId,
    bool Suspended,
    DateTimeOffset? SuspendedAt);

public sealed record AdminSuspensionResultDto(
    AdminSuspensionResultKind Kind,
    AdminSuspensionResponseDto? Response,
    string? Detail)
{
    public static AdminSuspensionResultDto Success(AdminSuspensionResponseDto response) =>
        new(AdminSuspensionResultKind.Success, response, null);

    public static AdminSuspensionResultDto UserNotFound(string detail) =>
        new(AdminSuspensionResultKind.UserNotFound, null, detail);
}

public enum AdminSuspensionResultKind
{
    Success,
    UserNotFound,
}

public sealed record AdminSuspensionAuditDetailsDto(
    bool Suspended,
    DateTimeOffset? SuspendedAt);

public sealed record AdminRefundPaymentLookupDto(
    string? PaymentIntentId,
    long? AmountTotal,
    string? Currency);

public sealed record AdminRefundResponseDto(
    Guid TargetUserId,
    string PaymentIntentId,
    long Amount,
    string? Currency,
    string? RefundId,
    bool AlreadyRefunded);

public sealed record AdminRefundResultDto(
    AdminRefundResultKind Kind,
    AdminRefundResponseDto? Response,
    string? Detail)
{
    public static AdminRefundResultDto Success(AdminRefundResponseDto response) =>
        new(AdminRefundResultKind.Success, response, null);

    public static AdminRefundResultDto InvalidRequest(string detail) =>
        new(AdminRefundResultKind.InvalidRequest, null, detail);

    public static AdminRefundResultDto UserNotFound(string detail) =>
        new(AdminRefundResultKind.UserNotFound, null, detail);

    public static AdminRefundResultDto PaymentNotFound(string detail) =>
        new(AdminRefundResultKind.PaymentNotFound, null, detail);

    public static AdminRefundResultDto RefundUnavailable(string detail) =>
        new(AdminRefundResultKind.RefundUnavailable, null, detail);
}

public enum AdminRefundResultKind
{
    Success,
    InvalidRequest,
    UserNotFound,
    PaymentNotFound,
    RefundUnavailable,
}

public sealed record AdminRefundAuditDetailsDto(
    string PaymentIntentId,
    string? RefundId,
    long Amount,
    string? Currency,
    string? Status);
