namespace ReplyInMyVoice.Domain.Entities;

public sealed class LearningFinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public LearningRun? Run { get; set; }
    public string? Scenario { get; set; }
    public string? CommonTone { get; set; }
    public string? PrimaryDiagnosisTag { get; set; }
    public required string FailureType { get; set; }
    public required string DiagnosisTags { get; set; }
    public int EvidenceCount { get; set; }
    public required string Severity { get; set; }
    public required string Recommendation { get; set; }
    public required string PromotionRecommendation { get; set; }
    public required string SampleRefs { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid RowVersion { get; set; } = Guid.NewGuid();

    public ICollection<StrategyCandidate> Candidates { get; } = new List<StrategyCandidate>();
}
