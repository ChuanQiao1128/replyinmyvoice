using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.RewriteEngine;

namespace ReplyInMyVoice.Infrastructure.Providers;

public interface IRewriteModelClient
{
    Task<RewriteModelResult> GenerateCandidateAsync(
        RewriteModelRequest request,
        CancellationToken cancellationToken);
}

public interface IWritingSignalClient
{
    Task<WritingSignalResult> MeasureAsync(string text, CancellationToken cancellationToken);
}

public sealed record RewriteModelRequest(
    Guid AttemptId,
    RewriteRequest UserRequest,
    RewriteInputAnalysis InputAnalysis,
    RewriteFactLedger FactLedger,
    RewriteStrategy Strategy,
    IReadOnlyList<RewriteAttemptHistoryItem> AttemptHistory);

public sealed record RewriteAttemptHistoryItem(
    int AttemptNo,
    RewriteStrategy Strategy,
    string CandidateText,
    IReadOnlyList<RewriteFailureKind> FailureKinds,
    string FailureAnalysis,
    int? DraftAiLikePercent,
    int? RewriteAiLikePercent);

public sealed record RewriteModelResult(
    string? CandidateText,
    bool Success,
    string? ErrorCode);

public sealed record WritingSignalResult(
    bool Available,
    int? AiLikePercent,
    string? ErrorCode,
    IReadOnlyList<SentenceSignalScore>? SentenceScores = null);

// Per-sentence AI-like scores from the writing-signal provider. The adaptive refinement loop
// uses these to localize which sentence(s) drive the overall score up, so it can ask the model
// to repair specifically those sentences instead of rewriting blindly.
public sealed record SentenceSignalScore(string Sentence, int AiLikePercent);

public sealed record FactReconstructRewriteOptions(
    int NaturalnessThreshold = 40,
    int RequestedMaxAttempts = 10,
    // Soft AI-signal goal for the adaptive refinement loop. The loop keeps refining (up to
    // RequestedMaxAttempts) until a send-ready candidate reaches this score, then returns it; if
    // none reaches it, the lowest-scoring send-ready candidate is returned (soft target — never
    // fail-closed when a fact-safe result exists). The default equals the naturalness floor, so
    // the loop returns the first gate-passing candidate exactly as before, leaving the production
    // composition root (which builds this provider with no options) unaffected until this is
    // wired up explicitly. The hard naturalness floor (NaturalnessThreshold) is unchanged;
    // refinement never returns anything above it.
    int TargetAiLikePercent = 40,
    // Wall-clock budget for the whole rewrite (all loops combined). Zero = unlimited. When set,
    // a linked token cancels in-flight model/signal calls past the budget and the loop returns
    // the best candidate found so far, bounding worst-case latency regardless of loop count.
    TimeSpan TotalTimeBudget = default,
    // Eval-only experiment lever: force the initial routing strategy (e.g. FactsFirstReconstruct)
    // regardless of the length/policy router. Default null = current production routing.
    RewriteStrategy? ForceInitialStrategy = null);
