namespace ReplyInMyVoice.Application.Common;

public sealed record PromoRedeemResultDto(
    PromoRedeemResultKind Kind,
    int CreditsGranted = 0,
    DateTimeOffset? ExpiresAt = null,
    Guid? RewriteCreditId = null)
{
    public static PromoRedeemResultDto Success(
        int creditsGranted,
        DateTimeOffset expiresAt,
        Guid rewriteCreditId) =>
        new(PromoRedeemResultKind.Success, creditsGranted, expiresAt, rewriteCreditId);

    public static PromoRedeemResultDto InvalidCode() => new(PromoRedeemResultKind.InvalidCode);

    public static PromoRedeemResultDto Expired() => new(PromoRedeemResultKind.Expired);

    public static PromoRedeemResultDto AlreadyRedeemed() => new(PromoRedeemResultKind.AlreadyRedeemed);

    public static PromoRedeemResultDto CapReached() => new(PromoRedeemResultKind.CapReached);

    public static PromoRedeemResultDto IpVelocityBlocked() => new(PromoRedeemResultKind.IpVelocityBlocked);

    public static PromoRedeemResultDto ServerConfig() => new(PromoRedeemResultKind.ServerConfig);
}

public enum PromoRedeemResultKind
{
    Success,
    InvalidCode,
    Expired,
    AlreadyRedeemed,
    CapReached,
    IpVelocityBlocked,
    ServerConfig,
}
