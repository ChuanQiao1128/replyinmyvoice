using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Domain.Entities;

public sealed class RewriteAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public Guid? ApiKeyId { get; set; }
    public ApiKey? ApiKey { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string RequestHash { get; set; }
    public required string RequestJson { get; set; }
    public RewriteAttemptStatus Status { get; set; } = RewriteAttemptStatus.Pending;
    public string? ResultJson { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public Guid RowVersion { get; set; } = Guid.NewGuid();

    public UsageReservation? Reservation { get; set; }
}
