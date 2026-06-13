namespace ReplyInMyVoice.Application.Common;

public sealed record StripeReconciliationOptions(
    int AutoGrantMaxPerRun,
    int MinPaymentAgeMinutes,
    int WindowDays);
