namespace ReplyInMyVoice.Domain.Entities;

public sealed class PromoCodeRedemption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PromoCodeId { get; set; }
    public PromoCode? PromoCode { get; set; }
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public Guid RewriteCreditId { get; set; }
    public int CreditsGranted { get; set; }
    public string CodeSnapshot { get; set; } = "";
    public string? RedeemIpHash { get; set; }
    public PromoCodeRedemptionStatus Status { get; set; } = PromoCodeRedemptionStatus.Applied;
    public DateTimeOffset RedeemedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReversedAt { get; set; }
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}

public enum PromoCodeRedemptionStatus
{
    Applied,
    Reversed,
}
