namespace ReplyInMyVoice.Domain.Entities;

public sealed class StripeEvent
{
    public required string EventId { get; set; }
    public required string Type { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}
