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
    RewriteStrategy Strategy);

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
        var draftSignal = await writingSignalClient.MeasureAsync(request.RoughDraftReply, cancellationToken);
        if (!HasSignal(draftSignal))
        {
            return new RewriteProviderResult(null, false, "quality_signal_unavailable");
        }

        var analysis = RewriteInputAnalyzer.Analyze(request);
        var ledger = FactLedgerExtractor.Extract(request);
        var budget = RewriteBudgetManager.Create(analysis, _options.RequestedMaxAttempts);
        _ = budget;

        var decision = RewriteStrategyRouter.ChooseInitial(analysis);
        var modelResult = await modelClient.GenerateCandidateAsync(
            new RewriteModelRequest(attemptId, request, analysis, ledger, decision.Strategy),
            cancellationToken);

        if (!modelResult.Success || string.IsNullOrWhiteSpace(modelResult.CandidateText))
        {
            return new RewriteProviderResult(null, false, modelResult.ErrorCode ?? "rewrite_model_failed");
        }

        var structureGate = RewriteStructureGate.Check(modelResult.CandidateText, analysis);
        if (!structureGate.Passed)
        {
            return new RewriteProviderResult(null, false, "structure_gate_failed");
        }

        var rewriteSignal = await writingSignalClient.MeasureAsync(modelResult.CandidateText, cancellationToken);
        if (!HasSignal(rewriteSignal))
        {
            return new RewriteProviderResult(null, false, "quality_signal_unavailable");
        }

        if (!PassesNaturalnessRule(draftSignal.AiLikePercent!.Value, rewriteSignal.AiLikePercent!.Value))
        {
            return new RewriteProviderResult(null, false, "naturalness_gate_failed");
        }

        return new RewriteProviderResult(
            JsonSerializer.Serialize(
                new RewriteProviderSuccessPayload(
                    modelResult.CandidateText.Trim(),
                    ["Rebuilt the reply from request facts."],
                    [],
                    NaturalnessPayload.From(
                        draftSignal.AiLikePercent.Value,
                        rewriteSignal.AiLikePercent.Value),
                    new RewriteOptimizationPayload(
                        decision.Strategy.ToString(),
                        analysis.Scenario.ToString(),
                        ledger.Facts.Count)),
                JsonOptions),
            true,
            null);
    }

    private static bool HasSignal(WritingSignalResult result) =>
        result.Available && result.AiLikePercent is >= 0 and <= 100;

    private bool PassesNaturalnessRule(int draftPercent, int rewritePercent) =>
        draftPercent > _options.NaturalnessThreshold
            ? rewritePercent <= _options.NaturalnessThreshold
            : rewritePercent <= draftPercent;

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
        int FactCount);
}
