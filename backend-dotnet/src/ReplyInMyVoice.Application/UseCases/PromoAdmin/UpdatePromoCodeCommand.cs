namespace ReplyInMyVoice.Application.UseCases.PromoAdmin;

public sealed record UpdatePromoCodeCommand(
    string AdminExternalAuthUserId,
    string? AdminEmail,
    Guid PromoCodeId,
    string? Description,
    int? CreditsGranted,
    int? GrantTtlDays,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    int? MaxRedemptionsGlobal,
    int? MaxRedemptionsPerUser,
    DateTimeOffset Now);
