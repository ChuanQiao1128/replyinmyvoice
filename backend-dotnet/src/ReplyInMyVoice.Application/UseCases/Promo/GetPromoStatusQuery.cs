namespace ReplyInMyVoice.Application.UseCases.Promo;

public sealed record GetPromoStatusQuery(
    string ExternalAuthUserId,
    string? Email,
    DateTimeOffset Now);
