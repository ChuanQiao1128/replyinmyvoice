namespace ReplyInMyVoice.Application.Common;

public sealed record CheckoutSessionDto(string Url);

public sealed record PortalSessionDto(string Url);

public sealed record CancelSubscriptionResultDto(
    bool Canceled,
    string? SubscriptionId);

public sealed record RefundPaymentResultDto(
    string RefundId,
    string PaymentIntentId,
    long Amount,
    string? Currency,
    string? Status);

public sealed record StripePaidPaymentDto(
    string PaymentIntentId,
    long AmountReceived,
    string Currency,
    DateTimeOffset PaidAt);

public sealed record TaxTurnoverReportDto(
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    string Currency,
    long GrossAmountTotal,
    long RegistrationThresholdAmountTotal,
    decimal WarningFraction,
    long WarningAmountTotal,
    decimal FractionOfThreshold,
    int IgnoredNonNzdPaymentCount,
    TaxTurnoverWarningDto? Warning,
    TaxTurnoverNotificationResultDto? Notification);

public sealed record TaxTurnoverWarningDto(
    string Code,
    string Severity,
    string Message);

public sealed record TaxTurnoverNotificationResultDto(
    bool Attempted,
    bool Sent,
    string? Provider,
    string? Reason);

public sealed record TaxTurnoverSettings(
    long RegistrationThresholdAmountTotal,
    decimal WarningFraction);
