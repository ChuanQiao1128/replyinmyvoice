namespace ReplyInMyVoice.Domain.Entities;

public sealed class PromoCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public string? DisplayCode { get; set; }
    public string? Description { get; set; }
    public PromoCodeKind Kind { get; set; } = PromoCodeKind.TrialCredits;
    public int CreditsGranted { get; set; }
    public int GrantTtlDays { get; set; } = 90;
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
    public int? MaxRedemptionsGlobal { get; set; }
    public int MaxRedemptionsPerUser { get; set; } = 1;
    public int RedemptionCount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? ArchivedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid RowVersion { get; set; } = Guid.NewGuid();

    public ICollection<PromoCodeRedemption> Redemptions { get; } = new List<PromoCodeRedemption>();
}

public enum PromoCodeKind
{
    TrialCredits,
}
