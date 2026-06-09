namespace ReplyInMyVoice.Application.UseCases.PromoAdmin;

public sealed record CreatePromoCodeCommand(
    string AdminExternalAuthUserId,
    string? AdminEmail,
    string? Code,
    string? Description,
    int? CreditsGranted,
    int? GrantTtlDays,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    int? MaxRedemptionsGlobal,
    int? MaxRedemptionsPerUser,
    DateTimeOffset Now);
