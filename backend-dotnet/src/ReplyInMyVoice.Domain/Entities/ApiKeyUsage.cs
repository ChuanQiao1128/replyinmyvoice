namespace ReplyInMyVoice.Domain.Entities;

public sealed class ApiKeyUsage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApiKeyId { get; set; }
    public ApiKey? ApiKey { get; set; }
    public required string RequestId { get; set; }
    public required string Endpoint { get; set; }
    public int StatusCode { get; set; }
    public int? LatencyMs { get; set; }
    public decimal CostUsdEstimate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
