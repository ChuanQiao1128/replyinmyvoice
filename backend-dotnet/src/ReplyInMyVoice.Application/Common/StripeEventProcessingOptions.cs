namespace ReplyInMyVoice.Application.Common;

public sealed record StripeEventProcessingOptions(
    int MaxAttempts,
    int InlineBudgetSeconds);
