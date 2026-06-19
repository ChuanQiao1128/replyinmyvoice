using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Domain.Entities;

public sealed class DeadLetterMessage : IConcurrencyStamped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string SourceType { get; set; }
    public required string SourceId { get; set; }
    public required string SourceData { get; set; }
    public required string FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RequeuedAt { get; set; }
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
