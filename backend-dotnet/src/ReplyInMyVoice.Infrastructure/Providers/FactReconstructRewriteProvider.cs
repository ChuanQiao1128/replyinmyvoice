using System.Text.Json;
using System.Text.Json.Serialization;
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
    string? ErrorCode);

public sealed record FactReconstructRewriteOptions(
    int NaturalnessThreshold = 40,
    int RequestedMaxAttempts = 10);

public sealed class FactReconstructRewriteProvider(
    IRewriteModelClient modelClient,
    IWritingSignalClient writingSignalClient,
    FactReconstructRewriteOptions? options = null) : IRewriteProvider
{
    private const int MaxWritingSignalAttempts = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly FactReconstructRewriteOptions _options = options ?? new FactReconstructRewriteOptions();

    public async Task<RewriteProviderResult> RewriteAsync(
        Guid attemptId,
        RewriteRequest request,
        CancellationToken cancellationToken)
    {
        var draftSignal = await MeasureWritingSignalWithRetryAsync(request.RoughDraftReply, cancellationToken);
        if (!HasSignal(draftSignal))
        {
            return new RewriteProviderResult(null, false, "quality_signal_unavailable");
        }

        var analysis = RewriteInputAnalyzer.Analyze(request);
        var ledger = FactLedgerExtractor.Extract(request);
        var budget = RewriteBudgetManager.Create(analysis, _options.RequestedMaxAttempts);
        var decision = RewriteStrategyRouter.ChooseInitial(analysis);
        var history = new List<RewriteAttemptHistoryItem>();

        for (var attemptNo = 1; attemptNo <= budget.MaxAttempts; attemptNo++)
        {
            if (decision.Strategy == RewriteStrategy.QualityFailure)
            {
                break;
            }

            var modelResult = await modelClient.GenerateCandidateAsync(
                new RewriteModelRequest(
                    attemptId,
                    request,
                    analysis,
                    ledger,
                    decision.Strategy,
                    history),
                cancellationToken);

            if (!modelResult.Success || string.IsNullOrWhiteSpace(modelResult.CandidateText))
            {
                return new RewriteProviderResult(null, false, modelResult.ErrorCode ?? "rewrite_model_failed");
            }

            var candidate = modelResult.CandidateText.Trim();
            var structureGate = RewriteStructureGate.Check(candidate, analysis);
            if (!structureGate.Passed)
            {
                history.Add(CreateHistoryItem(
                    attemptNo,
                    decision.Strategy,
                    candidate,
                    structureGate.FailureKinds,
                    string.Join(" ", structureGate.Reasons),
                    draftSignal.AiLikePercent,
                    null));
                decision = ChooseNext(analysis, history);
                continue;
            }

            var factGate = RewriteFactGate.Check(candidate, ledger);
            if (!factGate.Passed)
            {
                history.Add(CreateHistoryItem(
                    attemptNo,
                    decision.Strategy,
                    candidate,
                    factGate.FailureKinds,
                    string.Join(" ", factGate.Reasons),
                    draftSignal.AiLikePercent,
                    null));
                decision = ChooseNext(analysis, history);
                continue;
            }

            var rewriteSignal = await MeasureWritingSignalWithRetryAsync(candidate, cancellationToken);
            if (!HasSignal(rewriteSignal))
            {
                return new RewriteProviderResult(null, false, "quality_signal_unavailable");
            }

            if (!PassesNaturalnessRule(draftSignal.AiLikePercent!.Value, rewriteSignal.AiLikePercent!.Value))
            {
                history.Add(CreateHistoryItem(
                    attemptNo,
                    decision.Strategy,
                    candidate,
                    [NaturalnessFailureKind(draftSignal.AiLikePercent.Value, rewriteSignal.AiLikePercent.Value)],
                    $"Naturalness gate failed: draft {draftSignal.AiLikePercent.Value}%, rewrite {rewriteSignal.AiLikePercent.Value}%.",
                    draftSignal.AiLikePercent,
                    rewriteSignal.AiLikePercent));
                decision = ChooseNext(analysis, history);
                continue;
            }

            return new RewriteProviderResult(
                JsonSerializer.Serialize(
                    new RewriteProviderSuccessPayload(
                        candidate,
                        ["Rebuilt the reply from request facts."],
                        [],
                        NaturalnessPayload.From(
                            draftSignal.AiLikePercent.Value,
                            rewriteSignal.AiLikePercent.Value),
                        new RewriteOptimizationPayload(
                            decision.Strategy.ToString(),
                            analysis.Scenario.ToString(),
                            ledger.Facts.Count,
                            attemptNo,
                            history.Count)),
                    JsonOptions),
                true,
                null);
        }

        var lastFailure = history.LastOrDefault();
        var errorCode = lastFailure?.FailureKinds.Contains(RewriteFailureKind.SignalNotImproved) == true ||
            lastFailure?.FailureKinds.Contains(RewriteFailureKind.LowSignalGotWorse) == true
                ? "naturalness_gate_failed"
                : lastFailure?.FailureKinds.Contains(RewriteFailureKind.FactLoss) == true ||
                    lastFailure?.FailureKinds.Contains(RewriteFailureKind.UnsupportedFact) == true ||
                    lastFailure?.FailureKinds.Contains(RewriteFailureKind.PolicyIntentDrift) == true ||
                    lastFailure?.FailureKinds.Contains(RewriteFailureKind.NoChangeWithoutConfirmationMissing) == true
                    ? "fact_gate_failed"
                    : lastFailure?.FailureKinds.Count > 0
                        ? "structure_gate_failed"
                        : "rewrite_quality_failed";

        return new RewriteProviderResult(
            JsonSerializer.Serialize(
                new RewriteProviderFailurePayload(errorCode, history),
                JsonOptions),
            false,
            errorCode);
    }

    private static bool HasSignal(WritingSignalResult result) =>
        result.Available && result.AiLikePercent is >= 0 and <= 100;

    private async Task<WritingSignalResult> MeasureWritingSignalWithRetryAsync(
        string text,
        CancellationToken cancellationToken)
    {
        WritingSignalResult? lastResult = null;
        for (var attempt = 1; attempt <= MaxWritingSignalAttempts; attempt++)
        {
            lastResult = await writingSignalClient.MeasureAsync(text, cancellationToken);
            if (HasSignal(lastResult))
            {
                return lastResult;
            }
        }

        return lastResult ?? new WritingSignalResult(false, null, "quality_signal_unavailable");
    }

    private bool PassesNaturalnessRule(int draftPercent, int rewritePercent) =>
        draftPercent > _options.NaturalnessThreshold
            ? rewritePercent <= _options.NaturalnessThreshold
            : rewritePercent <= draftPercent;

    private static RewriteStrategyDecision ChooseNext(
        RewriteInputAnalysis analysis,
        IReadOnlyList<RewriteAttemptHistoryItem> history) =>
        RewriteStrategyRouter.ChooseNext(
            analysis,
            new RewriteFailureEvidence(
                history.Last().FailureKinds,
                history.Select(item => item.Strategy).ToArray(),
                history.Count));

    private static RewriteAttemptHistoryItem CreateHistoryItem(
        int attemptNo,
        RewriteStrategy strategy,
        string candidate,
        IReadOnlyList<RewriteFailureKind> failureKinds,
        string failureAnalysis,
        int? draftAiLikePercent,
        int? rewriteAiLikePercent) =>
        new(
            attemptNo,
            strategy,
            candidate,
            failureKinds,
            string.IsNullOrWhiteSpace(failureAnalysis) ? "Candidate failed rewrite gates." : failureAnalysis,
            draftAiLikePercent,
            rewriteAiLikePercent);

    private static RewriteFailureKind NaturalnessFailureKind(int draftPercent, int rewritePercent) =>
        rewritePercent > draftPercent
            ? RewriteFailureKind.LowSignalGotWorse
            : RewriteFailureKind.SignalNotImproved;

    private sealed record RewriteProviderSuccessPayload(
        string RewrittenText,
        IReadOnlyList<string> ChangeSummary,
        IReadOnlyList<string> RiskNotes,
        NaturalnessPayload Naturalness,
        RewriteOptimizationPayload Optimization);

    private sealed record NaturalnessPayload(
        int DraftAiLikePercent,
        int RewriteAiLikePercent,
        int ChangePoints,
        string Label)
    {
        public static NaturalnessPayload From(int draftAiLikePercent, int rewriteAiLikePercent)
        {
            var changePoints = rewriteAiLikePercent - draftAiLikePercent;
            var label = changePoints < 0
                ? "lower"
                : rewriteAiLikePercent <= 50
                    ? "low_signal"
                    : "still_high";

            return new NaturalnessPayload(
                draftAiLikePercent,
                rewriteAiLikePercent,
                changePoints,
                label);
        }
    }

    private sealed record RewriteOptimizationPayload(
        string Strategy,
        string Scenario,
        int FactCount,
        int AttemptsUsed,
        int FailedAttempts);

    private sealed record RewriteProviderFailurePayload(
        string ErrorCode,
        IReadOnlyList<RewriteAttemptHistoryItem> AttemptHistory);
}
