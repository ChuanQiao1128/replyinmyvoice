namespace ReplyInMyVoice.Domain.Entities;

public sealed class RewriteProviderCall
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CostLogId { get; set; }
    public RewriteCostLog? CostLog { get; set; }
    public required string Provider { get; set; }
    public required string Role { get; set; }
    public string? Model { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? Characters { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public int? LatencyMs { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
