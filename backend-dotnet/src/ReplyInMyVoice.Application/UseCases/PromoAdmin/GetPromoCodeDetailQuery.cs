namespace ReplyInMyVoice.Application.UseCases.PromoAdmin;

public sealed record GetPromoCodeDetailQuery(
    Guid PromoCodeId,
    DateTimeOffset Now);
