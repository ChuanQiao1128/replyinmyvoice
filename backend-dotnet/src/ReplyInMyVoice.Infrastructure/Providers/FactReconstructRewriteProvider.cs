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
    TimeSpan TotalTimeBudget = default);

public sealed class FactReconstructRewriteProvider(
    IRewriteModelClient modelClient,
    IWritingSignalClient writingSignalClient,
    FactReconstructRewriteOptions? options = null) : IRewriteProvider
{
    private const int MaxWritingSignalAttempts = 3;
    private const int MaxModelGenerationAttempts = 3;

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
        // Optional wall-clock budget across all loops: a linked token cancels in-flight model
        // and signal calls once the budget is spent, so the loop stops and returns the best
        // candidate found so far instead of running the full attempt count.
        using var budgetCts = _options.TotalTimeBudget > TimeSpan.Zero
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;
        budgetCts?.CancelAfter(_options.TotalTimeBudget);
        var token = budgetCts?.Token ?? cancellationToken;

        var draftSignal = await MeasureWritingSignalWithRetryAsync(request.RoughDraftReply, token);
        if (!HasSignal(draftSignal))
        {
            return new RewriteProviderResult(null, false, "quality_signal_unavailable");
        }

        var analysis = RewriteInputAnalyzer.Analyze(request);
        var ledger = FactLedgerExtractor.Extract(request);
        var budget = RewriteBudgetManager.Create(analysis, _options.RequestedMaxAttempts);
        var decision = RewriteStrategyRouter.ChooseInitial(analysis);
        var history = new List<RewriteAttemptHistoryItem>();

        // Adaptive sentence-targeted refinement loop. Each candidate must clear the same
        // structure + fact (incl. identifier fidelity + certainty drift) + naturalness-floor
        // gates as before, so refinement can never lower fact fidelity. A candidate at or below
        // TargetAiLikePercent is returned immediately (the common case — most drafts clear it on
        // attempt 1, so no extra calls). Otherwise we feed the candidate's overall score and its
        // most AI-like sentences back into the next attempt and ask the model to repair those
        // sentences specifically — converging instead of resampling — and always keep the
        // lowest-scoring send-ready candidate to return (soft target, never fail-closed).
        string? bestCandidate = null;
        var bestSignal = int.MaxValue;
        var bestAttemptNo = 0;
        var bestStrategy = decision.Strategy;

        for (var attemptNo = 1; attemptNo <= budget.MaxAttempts; attemptNo++)
        {
            if (decision.Strategy == RewriteStrategy.QualityFailure)
            {
                break;
            }

            // Time budget spent: stop and return the best candidate found so far.
            if (token.IsCancellationRequested)
            {
                break;
            }

            // While we still have no usable candidate, retry a transient model failure (e.g. a
            // timeout) a few times instead of fail-closing the whole request on one slow call.
            // Once a candidate exists, a later model failure simply stops with the best found.
            var modelResult = await GenerateCandidateWithRetryAsync(
                new RewriteModelRequest(
                    attemptId,
                    request,
                    analysis,
                    ledger,
                    decision.Strategy,
                    history),
                retryWhileEmpty: bestCandidate is null,
                token);

            if (!modelResult.Success || string.IsNullOrWhiteSpace(modelResult.CandidateText))
            {
                // A transient model failure must not discard a usable candidate already found.
                if (bestCandidate is not null)
                {
                    break;
                }

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

            var rewriteSignal = await MeasureWritingSignalWithRetryAsync(candidate, token);
            if (!HasSignal(rewriteSignal))
            {
                if (bestCandidate is not null)
                {
                    break;
                }

                return new RewriteProviderResult(null, false, "quality_signal_unavailable");
            }

            var signal = rewriteSignal.AiLikePercent!.Value;

            var passesFloor = PassesNaturalnessRule(draftSignal.AiLikePercent!.Value, signal);

            if (passesFloor)
            {
                // Send-ready: keep the lowest-scoring candidate seen so far.
                if (signal < bestSignal)
                {
                    bestCandidate = candidate;
                    bestSignal = signal;
                    bestAttemptNo = attemptNo;
                    bestStrategy = decision.Strategy;
                }

                // Reached the soft target — good enough, stop and return it.
                if (signal <= _options.TargetAiLikePercent)
                {
                    break;
                }
            }

            // Not at target yet — either above the naturalness floor (a real gate failure) or
            // send-ready but above the soft target. Either way, feed the overall score and the
            // candidate's most AI-like sentences back so the next attempt repairs those
            // sentences specifically. This never instructs fact loss or weaker writing.
            history.Add(CreateHistoryItem(
                attemptNo,
                decision.Strategy,
                candidate,
                [passesFloor
                    ? RewriteFailureKind.SignalNotImproved
                    : NaturalnessFailureKind(draftSignal.AiLikePercent.Value, signal)],
                BuildRefinementFeedback(signal, rewriteSignal.SentenceScores),
                draftSignal.AiLikePercent,
                signal));
            decision = ChooseNext(analysis, history);
        }

        if (bestCandidate is not null)
        {
            return new RewriteProviderResult(
                JsonSerializer.Serialize(
                    new RewriteProviderSuccessPayload(
                        bestCandidate,
                        ["Rebuilt the reply from request facts."],
                        [],
                        NaturalnessPayload.From(
                            draftSignal.AiLikePercent!.Value,
                            bestSignal),
                        new RewriteOptimizationPayload(
                            bestStrategy.ToString(),
                            analysis.Scenario.ToString(),
                            ledger.Facts.Count,
                            bestAttemptNo,
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

    // Generates one candidate, retrying transient model failures only while no usable candidate
    // exists yet (caller passes retryWhileEmpty). This keeps a single slow/timed-out call from
    // fail-closing the whole request, while staying bounded: once a candidate is in hand, later
    // failures fall through to "stop with the best found", so the worst-case call count stays
    // well within the reservation TTL.
    private async Task<RewriteModelResult> GenerateCandidateWithRetryAsync(
        RewriteModelRequest modelRequest,
        bool retryWhileEmpty,
        CancellationToken cancellationToken)
    {
        var result = await modelClient.GenerateCandidateAsync(modelRequest, cancellationToken);
        var attempts = 1;
        while (retryWhileEmpty
            && (!result.Success || string.IsNullOrWhiteSpace(result.CandidateText))
            && attempts < MaxModelGenerationAttempts)
        {
            attempts++;
            result = await modelClient.GenerateCandidateAsync(modelRequest, cancellationToken);
        }

        return result;
    }

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

    // Builds the targeted-repair feedback fed into the next attempt: the overall score plus the
    // candidate's most AI-like sentences (from Sapling's per-sentence scores), so the model
    // repairs those sentences specifically instead of rewriting blindly. Never instructs fact
    // loss or weaker writing — it asks for natural human phrasing while preserving every fact.
    private string BuildRefinementFeedback(int signal, IReadOnlyList<SentenceSignalScore>? sentenceScores)
    {
        var offenders = (sentenceScores ?? [])
            .Where(sentence => sentence.AiLikePercent >= 50)
            .OrderByDescending(sentence => sentence.AiLikePercent)
            .Take(3)
            .ToArray();

        var header =
            $"The previous reply scored {signal}% AI-like overall; the target is {_options.TargetAiLikePercent}% or lower.";

        if (offenders.Length == 0)
        {
            return header
                + " Rewrite it to read like a real person wrote it: vary sentence openings, replace formulaic or"
                + " template phrasing with specific concrete wording, fold reassurance into factual sentences, and"
                + " cut throat-clearing openers such as \"Just a reminder\" or \"I wanted to reach out\". Keep every"
                + " name, date, amount, count, identifier, condition, and negative constraint exactly as written.";
        }

        var offenderList = string.Join(
            "\n",
            offenders.Select(sentence => $"- ({sentence.AiLikePercent}%) \"{sentence.Sentence.Trim()}\""));

        return header
            + " These specific sentences read as machine-generated and are driving the score up:\n"
            + offenderList
            + "\nRewrite specifically these sentences so they sound natural and human — you may merge, shorten, cut,"
            + " or rephrase them and lightly adjust neighboring wording for flow. Keep all other sentences and every"
            + " name, date, amount, count, identifier, condition, and negative constraint exactly as written. Do not"
            + " add new claims, pleasantries, or template lines.";
    }

    // A rewrite passes when its robust-median AI-like score is at or below the human
    // threshold. We do NOT additionally require rewrite <= draft when the draft is already
    // clean: that older rule punished normal rewriting of an already-human draft and, in the
    // 2026-05-26 100-case eval, was the only cause of recoverable naturalness failures
    // (e.g. case 078). Matches the validated TS gate. Because it only relaxes the
    // clean-draft branch (draft <= threshold), it cannot regress a rewrite that already
    // passed — the pass set is a strict superset of the old one.
    private bool PassesNaturalnessRule(int draftPercent, int rewritePercent) =>
        rewritePercent <= _options.NaturalnessThreshold;

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
