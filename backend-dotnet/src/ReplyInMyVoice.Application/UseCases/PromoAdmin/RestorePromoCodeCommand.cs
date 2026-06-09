namespace ReplyInMyVoice.Application.UseCases.PromoAdmin;

public sealed record RestorePromoCodeCommand(
    string AdminExternalAuthUserId,
    string? AdminEmail,
    Guid PromoCodeId,
    DateTimeOffset Now);
