namespace ReplyInMyVoice.Domain.Entities;

public sealed class RewriteCostLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }

    // Faithful to Prisma: loose reference to a RewriteLearningSample with no declared FK.
    public Guid? LearningSampleId { get; set; }
    public required string RequestId { get; set; }
    public required string StrategyVersion { get; set; }
    public required string Scenario { get; set; }
    public required string TonePreset { get; set; }
    public required string Status { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int? DurationMs { get; set; }
    public int InputCharCount { get; set; }
    public int DraftWordCount { get; set; }
    public int? RewriteWordCount { get; set; }
    public int? DraftAiLikePercent { get; set; }
    public int? RewriteAiLikePercent { get; set; }
    public int? ChangePoints { get; set; }
    public int InternalStrategies { get; set; }
    public int RepairCandidates { get; set; }
    public int RejectedCandidates { get; set; }
    public bool UsedEscalation { get; set; }
    public int OpenAiInputTokens { get; set; }
    public int OpenAiOutputTokens { get; set; }
    public decimal OpenAiCostUsd { get; set; }
    public int SaplingCallCount { get; set; }
    public int SaplingCharacters { get; set; }
    public decimal SaplingCostUsd { get; set; }
    public decimal TotalEstimatedCostUsd { get; set; }
    public string ModelsUsedJson { get; set; } = "[]";
    public string ProviderCallsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid RowVersion { get; set; } = Guid.NewGuid();

    public ICollection<RewriteProviderCall> ProviderCalls { get; } = new List<RewriteProviderCall>();
}
