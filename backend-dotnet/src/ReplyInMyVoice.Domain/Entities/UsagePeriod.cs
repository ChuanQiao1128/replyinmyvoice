using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Domain.Entities;

public sealed class UsagePeriod : IConcurrencyStamped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public required string PeriodKey { get; set; }
    public int QuotaLimit { get; set; }
    public int UsedCount { get; set; }
    public int ReservedCount { get; set; }
    public DateTimeOffset? PeriodStart { get; set; }
    public DateTimeOffset? PeriodEnd { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid RowVersion { get; set; } = Guid.NewGuid();

    public ICollection<UsageReservation> Reservations { get; } = new List<UsageReservation>();
}
