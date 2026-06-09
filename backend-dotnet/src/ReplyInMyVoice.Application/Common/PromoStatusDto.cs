namespace ReplyInMyVoice.Application.Common;

public sealed record PromoStatusDto(
    bool HasRedeemed,
    bool Eligible,
    int TrialRemaining,
    DateTimeOffset? TrialExpiresAt);
