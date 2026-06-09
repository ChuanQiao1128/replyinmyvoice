namespace ReplyInMyVoice.Application.UseCases.Promo;

public sealed record RedeemPromoCommand(
    string ExternalAuthUserId,
    string? Email,
    string RawCode,
    string? IpHash,
    DateTimeOffset Now);
