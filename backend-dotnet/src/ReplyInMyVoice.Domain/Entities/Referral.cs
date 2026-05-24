namespace ReplyInMyVoice.Domain.Entities;

public sealed class Referral
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReferrerId { get; set; }
    public AppUser? Referrer { get; set; }

    // Faithful to Prisma: refereeId is a unique user reference with no declared FK.
    // Modeling it as a second cascade FK to AppUser would create multiple cascade
    // paths to AppUser, which SQL Server rejects. Kept as an indexed unique column.
    public Guid RefereeId { get; set; }
    public string Status { get; set; } = "pending";
    public DateTimeOffset? CreditedAt { get; set; }
    public string? SignupIpHash { get; set; }
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
