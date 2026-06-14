using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Domain.Entities;

public sealed class ApiKeyRateLimitWindow : IConcurrencyStamped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApiKeyId { get; set; }
    public ApiKey? ApiKey { get; set; }
    public DateTimeOffset WindowStart { get; set; }
    public int Count { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
