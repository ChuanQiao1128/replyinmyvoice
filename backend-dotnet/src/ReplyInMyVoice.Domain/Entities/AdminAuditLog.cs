namespace ReplyInMyVoice.Domain.Entities;

public sealed class AdminAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string AdminExternalAuthUserId { get; set; }
    public required string AdminEmail { get; set; }
    public required string Action { get; set; }
    public Guid? TargetUserId { get; set; }
    public string? DetailsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
