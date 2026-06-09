namespace ReplyInMyVoice.Application.UseCases.PromoAdmin;

public sealed record SetPromoCodeActiveCommand(
    string AdminExternalAuthUserId,
    string? AdminEmail,
    Guid PromoCodeId,
    bool IsActive,
    DateTimeOffset Now);
