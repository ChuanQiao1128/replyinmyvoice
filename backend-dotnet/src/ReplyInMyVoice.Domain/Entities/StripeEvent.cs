using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Domain.Entities;

public sealed class StripeEvent
{
    public required string EventId { get; set; }
    public required string Type { get; set; }
    public StripeEventStatus Status { get; set; } = StripeEventStatus.Processing;
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
