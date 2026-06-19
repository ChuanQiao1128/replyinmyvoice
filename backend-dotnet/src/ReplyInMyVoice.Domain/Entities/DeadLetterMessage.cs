using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Domain.Entities;

public sealed class DeadLetterMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DeadLetterEntityType EntityType { get; set; }
    public required string EntityId { get; set; }
    public Guid? OutboxMessageId { get; set; }
    public string? StripeEventId { get; set; }
    public required string FailureReason { get; set; }
    public int FailureCount { get; set; }
    public DateTimeOffset FirstFailedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastFailedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid RowVersion { get; set; } = Guid.NewGuid();

    public OutboxMessage? OutboxMessage { get; set; }
    public StripeEvent? StripeEvent { get; set; }
}
