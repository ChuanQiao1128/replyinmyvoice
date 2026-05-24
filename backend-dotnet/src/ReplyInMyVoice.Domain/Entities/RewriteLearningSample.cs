namespace ReplyInMyVoice.Domain.Entities;

public sealed class RewriteLearningSample
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }
    public required string Scenario { get; set; }
    public required string TonePreset { get; set; }
    public string? MessageToReplyTo { get; set; }
    public required string RoughDraftReply { get; set; }
    public string? RewrittenText { get; set; }
    public int? DraftAiLikePercent { get; set; }
    public int? RewriteAiLikePercent { get; set; }
    public int? ChangePoints { get; set; }
    public string? Label { get; set; }
    public required string DiagnosisTags { get; set; }
    public string? RewritePlanSummary { get; set; }
    public required string CandidateSignals { get; set; }
    public int InternalStrategies { get; set; }
    public int RepairCandidates { get; set; }
    public int RejectedCandidates { get; set; }
    public required string Status { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
