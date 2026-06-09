namespace ReplyInMyVoice.Application.UseCases.PromoAdmin;

public sealed record ArchivePromoCodeCommand(
    string AdminExternalAuthUserId,
    string? AdminEmail,
    Guid PromoCodeId,
    DateTimeOffset Now);
