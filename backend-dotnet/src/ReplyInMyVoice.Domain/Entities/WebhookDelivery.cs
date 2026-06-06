using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Domain.Entities;

public sealed class WebhookDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApiKeyId { get; set; }
    public ApiKey? ApiKey { get; set; }
    public Guid RewriteAttemptId { get; set; }
    public RewriteAttempt? RewriteAttempt { get; set; }
    public required string Url { get; set; }
    public WebhookDeliveryStatus Status { get; set; } = WebhookDeliveryStatus.Pending;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public string? LockedBy { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public string? LastError { get; set; }
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
