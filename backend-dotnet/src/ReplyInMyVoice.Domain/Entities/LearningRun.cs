using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Domain.Entities;

public sealed class LearningRun : IConcurrencyStamped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
    public required string Status { get; set; }
    public int SampleCount { get; set; }
    public int MeasuredCount { get; set; }
    public int FindingCount { get; set; }
    public int CandidateCount { get; set; }
    public required string PromotionDecision { get; set; }
    public string? ValidationStatus { get; set; }
    public string? DigestPath { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid RowVersion { get; set; } = Guid.NewGuid();

    public ICollection<LearningFinding> Findings { get; } = new List<LearningFinding>();
}
