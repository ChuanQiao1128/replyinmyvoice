using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Domain.Entities;

public sealed class UsageReservation : IConcurrencyStamped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public Guid UsagePeriodId { get; set; }
    public UsagePeriod? UsagePeriod { get; set; }
    public Guid RewriteAttemptId { get; set; }
    public RewriteAttempt? RewriteAttempt { get; set; }
    public Guid? RewriteCreditId { get; set; }
    public RewriteCredit? RewriteCredit { get; set; }
    public UsageReservationStatus Status { get; set; } = UsageReservationStatus.Pending;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinalizedAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
