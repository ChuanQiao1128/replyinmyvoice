using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Domain.Entities;

public sealed class StrategyCandidate : IConcurrencyStamped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FindingId { get; set; }
    public LearningFinding? Finding { get; set; }
    public required string Title { get; set; }
    public string? Scenario { get; set; }
    public string? PatchTarget { get; set; }
    public string? PatchAction { get; set; }
    public string? PatchText { get; set; }
    public required string ProposedChangeSummary { get; set; }
    public required string RiskLevel { get; set; }
    public required string Status { get; set; }
    public string? RequiredRegressionTest { get; set; }
    public required string RequiredEval { get; set; }
    public int EvidenceCount { get; set; }
    public string? LinkedCommitHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
