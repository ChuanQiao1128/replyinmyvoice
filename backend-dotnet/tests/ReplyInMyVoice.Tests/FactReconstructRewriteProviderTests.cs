using System.Text.Json;
using FluentAssertions;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.RewriteEngine;
using ReplyInMyVoice.Domain.Quality;
using ReplyInMyVoice.Infrastructure.Providers;

namespace ReplyInMyVoice.Tests;

public sealed class FactReconstructRewriteProviderTests
{
    [Fact]
    public async Task RewriteAsync_returns_quality_failure_when_draft_signal_is_unavailable()
    {
        var model = new RecordingRewriteModelClient(
            new RewriteModelResult("Hi Jordan,\n\nThanks for the update. I can send the details today.", true, null));
        var signal = new QueueWritingSignalClient(
            new WritingSignalResult(false, null, "sapling_unavailable"),
            new WritingSignalResult(false, null, "sapling_unavailable"),
            new WritingSignalResult(false, null, "sapling_unavailable"));
        var provider = new FactReconstructRewriteProvider(model, signal);

        var result = await provider.RewriteAsync(Guid.NewGuid(), ValidRequest(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("quality_signal_unavailable");
        model.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task RewriteAsync_retries_transient_draft_signal_unavailability_before_calling_model()
    {
        var model = new RecordingRewriteModelClient(
            new RewriteModelResult(
                "Hi Jordan,\n\nThe NZD $200 preview comes from the three contractor seats added on May 3. I will not change the account unless you confirm, and I can review it before Friday.",
                true,
                null));
        var signal = new QueueWritingSignalClient(
            new WritingSignalResult(false, null, "sapling_timeout"),
            new WritingSignalResult(true, 88, null),
            new WritingSignalResult(true, 12, null));
        var provider = new FactReconstructRewriteProvider(model, signal);

        var result = await provider.RewriteAsync(Guid.NewGuid(), ValidRequest(), CancellationToken.None);

        result.Success.Should().BeTrue();
        signal.CallCount.Should().Be(3);
        model.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task RewriteAsync_retries_transient_rewrite_signal_unavailability_before_failing_candidate()
    {
        var model = new RecordingRewriteModelClient(
            new RewriteModelResult(
                "Hi Jordan,\n\nThe NZD $200 preview comes from the three contractor seats added on May 3. I will not change the account unless you confirm, and I can review it before Friday.",
                true,
                null));
        var signal = new QueueWritingSignalClient(
            new WritingSignalResult(true, 88, null),
            new WritingSignalResult(false, null, "sapling_timeout"),
            new WritingSignalResult(true, 12, null));
        var provider = new FactReconstructRewriteProvider(model, signal);

        var result = await provider.RewriteAsync(Guid.NewGuid(), ValidRequest(), CancellationToken.None);

        result.Success.Should().BeTrue();
        signal.CallCount.Should().Be(3);
        model.CallCount.Should().Be(1);
    }


    [Fact]
    public async Task RewriteAsync_returns_quality_failure_when_candidate_breaks_structure_gate()
    {
        var model = new RecordingRewriteModelClient(
            new RewriteModelResult(
                """
                Hi Daniel,

                1.

                I can check the transfer option.

                2.

                I can check whether a partial credit is available.

                Please confirm what you prefer.
                """,
                true,
                null));
        var signal = new QueueWritingSignalClient(
            new WritingSignalResult(true, 87, null));
        var provider = new FactReconstructRewriteProvider(
            model,
            signal,
            new FactReconstructRewriteOptions(RequestedMaxAttempts: 1));

        var result = await provider.RewriteAsync(Guid.NewGuid(), PolicyRequest(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("structure_gate_failed");
    }

    [Fact]
    public async Task RewriteAsync_returns_success_json_when_candidate_passes_gates_and_naturalness_rule()
    {
        var model = new RecordingRewriteModelClient(
            new RewriteModelResult(
                "Hi Jordan,\n\nThe NZD $200 preview looks like it is from the three temporary contractor seats added on May 3. I will not change the account unless you confirm, and I can review it with you before Friday.\n\nPlease let me know how you want to proceed.",
                true,
                null));
        var signal = new QueueWritingSignalClient(
            new WritingSignalResult(true, 88, null),
            new WritingSignalResult(true, 22, null));
        var provider = new FactReconstructRewriteProvider(model, signal);

        var result = await provider.RewriteAsync(Guid.NewGuid(), ValidRequest(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
        using var doc = JsonDocument.Parse(result.ResultJson!);
        doc.RootElement.GetProperty("rewrittenText").GetString().Should().Contain("NZD $200");
        doc.RootElement.GetProperty("changeSummary").ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetProperty("riskNotes").ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetProperty("naturalness").GetProperty("draftAiLikePercent").GetInt32().Should().Be(88);
        doc.RootElement.GetProperty("naturalness").GetProperty("rewriteAiLikePercent").GetInt32().Should().Be(22);
        doc.RootElement.GetProperty("naturalness").GetProperty("changePoints").GetInt32().Should().Be(-66);
        doc.RootElement.GetProperty("naturalness").GetProperty("label").GetString().Should().Be("lower");
    }

    [Fact]
    public async Task RewriteAsync_returns_quality_failure_when_rewrite_signal_does_not_improve()
    {
        var model = new RecordingRewriteModelClient(
            new RewriteModelResult("Hi Jordan,\n\nThe NZD $200 invoice preview is from the three contractor seats added on May 3. I will not change the account unless you confirm, and I can review it before Friday.", true, null));
        var signal = new QueueWritingSignalClient(
            new WritingSignalResult(true, 35, null),
            new WritingSignalResult(true, 51, null));
        var provider = new FactReconstructRewriteProvider(
            model,
            signal,
            new FactReconstructRewriteOptions(RequestedMaxAttempts: 1));

        var result = await provider.RewriteAsync(Guid.NewGuid(), ValidRequest(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("naturalness_gate_failed");
    }

    [Fact]
    public async Task RewriteAsync_retries_with_attempt_history_after_naturalness_failure()
    {
        var model = new RecordingRewriteModelClient(
            new RewriteModelResult("Hi Jordan,\n\nThank you for reaching out regarding the NZD $200 invoice preview from the three contractor seats added on May 3. I will review this matter before Friday and will not change the account unless you confirm.", true, null),
            new RewriteModelResult("Hi Jordan,\n\nThe NZD $200 preview comes from the three contractor seats added on May 3. I will not change the account unless you confirm, and I can review it with you before Friday.\n\nPlease let me know what you want to do next.", true, null));
        var signal = new QueueWritingSignalClient(
            new WritingSignalResult(true, 88, null),
            new WritingSignalResult(true, 76, null),
            new WritingSignalResult(true, 18, null));
        var provider = new FactReconstructRewriteProvider(
            model,
            signal,
            new FactReconstructRewriteOptions(RequestedMaxAttempts: 2));

        var result = await provider.RewriteAsync(Guid.NewGuid(), ValidRequest(), CancellationToken.None);

        result.Success.Should().BeTrue();
        model.CallCount.Should().Be(2);
        model.Requests[1].AttemptHistory.Should().ContainSingle();
        model.Requests[1].AttemptHistory[0].FailureKinds.Should().Contain(RewriteFailureKind.SignalNotImproved);
        model.Requests[1].AttemptHistory[0].CandidateText.Should().Contain("Thank you for reaching out");
    }

    [Fact]
    public async Task RewriteAsync_returns_quality_failure_after_attempt_budget_is_exhausted()
    {
        var model = new RecordingRewriteModelClient(
            new RewriteModelResult("Hi Jordan,\n\nThe NZD $200 invoice preview is from the three contractor seats added on May 3. I will not change the account unless you confirm, and I can review it before Friday.", true, null),
            new RewriteModelResult("Hi Jordan,\n\nThe NZD $200 preview is from the three contractor seats added on May 3. I will not change the account unless you confirm, and I can review it before Friday.", true, null));
        var signal = new QueueWritingSignalClient(
            new WritingSignalResult(true, 88, null),
            new WritingSignalResult(true, 78, null),
            new WritingSignalResult(true, 74, null));
        var provider = new FactReconstructRewriteProvider(
            model,
            signal,
            new FactReconstructRewriteOptions(RequestedMaxAttempts: 2));

        var result = await provider.RewriteAsync(Guid.NewGuid(), ValidRequest(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("naturalness_gate_failed");
        result.ResultJson.Should().Contain("attemptHistory");
        model.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task RewriteAsync_accepts_rewrite_at_threshold_when_draft_is_already_clean()
    {
        // Draft already human (10%), rewrite 30% — above the draft but at/under the 40
        // threshold. The old rewrite<=draft rule failed this; the relaxed rule accepts it
        // (the case-078 class of recoverable naturalness failures from the 2026-05-26 eval).
        var model = new RecordingRewriteModelClient(
            new RewriteModelResult("Hi Jordan,\n\nThe NZD $200 invoice preview is from the three contractor seats added on May 3. I will not change the account unless you confirm, and I can review it before Friday.", true, null));
        var signal = new QueueWritingSignalClient(
            new WritingSignalResult(true, 10, null),
            new WritingSignalResult(true, 30, null));
        var provider = new FactReconstructRewriteProvider(
            model,
            signal,
            new FactReconstructRewriteOptions(RequestedMaxAttempts: 1));

        var result = await provider.RewriteAsync(Guid.NewGuid(), ValidRequest(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task RewriteAsync_refines_past_soft_target_and_carries_sentence_feedback()
    {
        // Adaptive loop: attempt 1 is send-ready (35%) but above the 25% soft target, so the
        // loop refines and attempt 2 (18%) clears the target and is returned. The offending
        // sentence is fed back into attempt 2 as targeted repair feedback.
        var model = new RecordingRewriteModelClient(
            new RewriteModelResult(
                "Hi Jordan,\n\nI wanted to reach out regarding the NZD $200 invoice preview. It comes from the three contractor seats added on May 3. I will not change the account unless you confirm, and I can review it before Friday.",
                true,
                null),
            new RewriteModelResult(
                "Hi Jordan,\n\nThe NZD $200 preview is the three contractor seats added on May 3. I won't touch the account unless you confirm, and I'm happy to review it with you before Friday.",
                true,
                null));
        var signal = new QueueWritingSignalClient(
            new WritingSignalResult(true, 88, null),
            new WritingSignalResult(
                true,
                35,
                null,
                [
                    new SentenceSignalScore("I wanted to reach out regarding the NZD $200 invoice preview.", 100),
                    new SentenceSignalScore("It comes from the three contractor seats added on May 3.", 0),
                ]),
            new WritingSignalResult(true, 18, null));
        var provider = new FactReconstructRewriteProvider(
            model,
            signal,
            new FactReconstructRewriteOptions(RequestedMaxAttempts: 3, TargetAiLikePercent: 25));

        var result = await provider.RewriteAsync(Guid.NewGuid(), ValidRequest(), CancellationToken.None);

        result.Success.Should().BeTrue();
        model.CallCount.Should().Be(2);
        using var doc = JsonDocument.Parse(result.ResultJson!);
        doc.RootElement.GetProperty("naturalness").GetProperty("rewriteAiLikePercent").GetInt32().Should().Be(18);
        // Attempt 2 was driven by the score plus the named offending sentence, not a blind retry.
        model.Requests[1].AttemptHistory.Should().ContainSingle();
        model.Requests[1].AttemptHistory[0].FailureKinds.Should().Contain(RewriteFailureKind.SignalNotImproved);
        model.Requests[1].AttemptHistory[0].FailureAnalysis.Should().Contain("I wanted to reach out regarding the NZD $200 invoice preview.");
    }

    [Fact]
    public async Task RewriteAsync_returns_lowest_send_ready_candidate_when_soft_target_never_reached()
    {
        // Soft target: no attempt reaches 25%, but all three are send-ready (<= 40). The loop
        // must return the lowest-scoring candidate (30%), not fail-closed.
        var model = new RecordingRewriteModelClient(
            new RewriteModelResult(
                "Hi Jordan,\n\nThe NZD $200 invoice preview reflects the three contractor seats added on May 3. I will not change the account unless you confirm, and I can review it before Friday.",
                true,
                null),
            new RewriteModelResult(
                "Hi Jordan,\n\nThe NZD $200 preview is the three contractor seats added on May 3. I won't change the account unless you confirm; I can review it with you before Friday.",
                true,
                null),
            new RewriteModelResult(
                "Hi Jordan,\n\nThat NZD $200 preview comes from the three contractor seats added on May 3. No account change unless you confirm, and I can go through it before Friday.",
                true,
                null));
        var signal = new QueueWritingSignalClient(
            new WritingSignalResult(true, 88, null),
            new WritingSignalResult(true, 38, null),
            new WritingSignalResult(true, 30, null),
            new WritingSignalResult(true, 33, null));
        var provider = new FactReconstructRewriteProvider(
            model,
            signal,
            new FactReconstructRewriteOptions(RequestedMaxAttempts: 3, TargetAiLikePercent: 25));

        var result = await provider.RewriteAsync(Guid.NewGuid(), ValidRequest(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
        model.CallCount.Should().Be(3);
        using var doc = JsonDocument.Parse(result.ResultJson!);
        doc.RootElement.GetProperty("naturalness").GetProperty("rewriteAiLikePercent").GetInt32().Should().Be(30);
    }

    [Fact]
    public async Task RewriteAsync_retries_transient_model_failure_before_first_candidate()
    {
        // Two transient model failures then a success on the first loop: the request recovers
        // (retry while no candidate exists) instead of fail-closing on the first timeout.
        var model = new RecordingRewriteModelClient(
            new RewriteModelResult(null, false, "model_timeout"),
            new RewriteModelResult(null, false, "model_timeout"),
            new RewriteModelResult(
                "Hi Jordan,\n\nThe NZD $200 preview is the three contractor seats added on May 3. I won't change the account unless you confirm, and I can review it before Friday.",
                true,
                null));
        var signal = new QueueWritingSignalClient(
            new WritingSignalResult(true, 88, null),
            new WritingSignalResult(true, 18, null));
        var provider = new FactReconstructRewriteProvider(model, signal);

        var result = await provider.RewriteAsync(Guid.NewGuid(), ValidRequest(), CancellationToken.None);

        result.Success.Should().BeTrue();
        model.CallCount.Should().Be(3);
    }

    [Fact]
    public async Task RewriteAsync_bounds_model_retries_when_failure_is_persistent()
    {
        // A persistent model failure with no candidate yet fails after the bounded retry count,
        // not after burning the whole attempt budget (so a dead API fails fast).
        var model = new RecordingRewriteModelClient(
            new RewriteModelResult(null, false, "model_timeout"),
            new RewriteModelResult(null, false, "model_timeout"),
            new RewriteModelResult(null, false, "model_timeout"));
        var signal = new QueueWritingSignalClient(
            new WritingSignalResult(true, 88, null));
        var provider = new FactReconstructRewriteProvider(model, signal);

        var result = await provider.RewriteAsync(Guid.NewGuid(), ValidRequest(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("model_timeout");
        model.CallCount.Should().Be(3);
    }

    [Fact]
    public async Task RewriteAsync_with_quality_gate_chain_on_rejects_protected_term_drift()
    {
        // Candidate clears structure + fact gates (no fact-ledger term lost) but drops the acronym "SSO"
        // (rephrased to "single sign-on"). With the deterministic quality chain ON, ProtectedTermGate
        // catches the object drift and the rewrite is rejected as a fidelity loss.
        var model = new RecordingRewriteModelClient(
            new RewriteModelResult(
                "Hi Jordan,\n\nThe single sign-on setup needs a new approval cycle before we can enable it.\n\nLet me know if you want me to start that.",
                true,
                null));
        var signal = new QueueWritingSignalClient(new WritingSignalResult(true, 88, null));
        var provider = new FactReconstructRewriteProvider(
            model,
            signal,
            new FactReconstructRewriteOptions(RequestedMaxAttempts: 1, QualityGateChainEnabled: true));

        var result = await provider.RewriteAsync(Guid.NewGuid(), AcronymRequest(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("fact_gate_failed");
    }

    [Fact]
    public async Task RewriteAsync_with_quality_gate_chain_off_keeps_default_behavior()
    {
        // Same candidate, gate OFF (default): the "SSO" -> "single sign-on" drift is not enforced, so the
        // candidate passes exactly as it does today. Proves the flag is inert until turned on.
        var model = new RecordingRewriteModelClient(
            new RewriteModelResult(
                "Hi Jordan,\n\nThe single sign-on setup needs a new approval cycle before we can enable it.\n\nLet me know if you want me to start that.",
                true,
                null));
        var signal = new QueueWritingSignalClient(
            new WritingSignalResult(true, 88, null),
            new WritingSignalResult(true, 15, null));
        var provider = new FactReconstructRewriteProvider(
            model,
            signal,
            new FactReconstructRewriteOptions(RequestedMaxAttempts: 1));

        var result = await provider.RewriteAsync(Guid.NewGuid(), AcronymRequest(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task RewriteAsync_with_fidelity_judge_rejects_semantic_drift()
    {
        // A clean candidate clears the deterministic chain (the minimal draft has no protected anchors to
        // drop), so the LLM FidelityJudge alone decides. Here it flags an object substitution, so the
        // candidate is rejected and re-routed exactly like a deterministic fidelity failure.
        var model = new RecordingRewriteModelClient(
            new RewriteModelResult("Hi there,\n\nThanks for the update, I will review the details and follow up with you shortly.\n\nBest.", true, null));
        var signal = new QueueWritingSignalClient(new WritingSignalResult(true, 88, null));
        var judge = new FakeFidelityJudge(new FidelityJudgeResult(
            false,
            new[] { new FidelityDrift(FidelityDriftKind.ObjectSubstituted, "the details", "the invoice", "the details", "object swapped") },
            null));
        var provider = new FactReconstructRewriteProvider(
            model,
            signal,
            new FactReconstructRewriteOptions(RequestedMaxAttempts: 1, QualityGateChainEnabled: true),
            judge);

        var result = await provider.RewriteAsync(Guid.NewGuid(), MinimalRequest(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("fact_gate_failed");
        judge.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task RewriteAsync_with_fidelity_judge_passes_when_no_drift()
    {
        var model = new RecordingRewriteModelClient(
            new RewriteModelResult("Hi there,\n\nThanks for the update, I will review the details and follow up with you shortly.\n\nBest.", true, null));
        var signal = new QueueWritingSignalClient(
            new WritingSignalResult(true, 88, null),
            new WritingSignalResult(true, 15, null));
        var judge = new FakeFidelityJudge(FidelityJudgeResult.Clean);
        var provider = new FactReconstructRewriteProvider(
            model,
            signal,
            new FactReconstructRewriteOptions(RequestedMaxAttempts: 1, QualityGateChainEnabled: true),
            judge);

        var result = await provider.RewriteAsync(Guid.NewGuid(), MinimalRequest(), CancellationToken.None);

        result.Success.Should().BeTrue();
        judge.CallCount.Should().Be(1);
    }

    private static RewriteRequest MinimalRequest() =>
        new(
            null,
            "Thanks for the update. I will review the details and follow up shortly.",
            null,
            null,
            null,
            null,
            "warm");

    private static RewriteRequest AcronymRequest() =>
        new(
            "Jordan asked whether the SSO setup can be enabled this week.",
            "Hi Jordan, the SSO setup needs a new approval cycle before we can enable it.",
            "IT manager",
            "Explain the SSO setup status.",
            "The SSO setup requires an approval cycle.",
            "Preserve Jordan and the SSO setup.",
            "direct");

    private static RewriteRequest ValidRequest() =>
        new(
            "Jordan asked whether the NZD $200 invoice preview can be changed by Friday.",
            "Hi Jordan, the NZD $200 invoice preview is from three contractor seats. I will not change the account unless you confirm.",
            "Finance manager",
            "Clarify the invoice preview.",
            "Three temporary contractor seats were added on May 3.",
            "Preserve Jordan, NZD $200, three seats, May 3, Friday, and no account change without confirmation.",
            "direct");

    private static RewriteRequest PolicyRequest() =>
        new(
            "Daniel asked whether he can transfer his course seat or receive a refund.",
            "Tell Daniel he may be eligible for a partial credit, but we cannot change the enrollment unless he confirms.",
            "Customer",
            "Explain transfer and refund options without promising approval.",
            "The transfer window is closed. A partial credit may be available after review.",
            "Do not change the enrollment without confirmation. Preserve partial-credit eligibility language.",
            "warm");
}

internal sealed class RecordingRewriteModelClient(params RewriteModelResult[] results) : IRewriteModelClient
{
    private readonly Queue<RewriteModelResult> _results = new(results);

    public int CallCount { get; private set; }
    public RewriteModelRequest? LastRequest { get; private set; }
    public List<RewriteModelRequest> Requests { get; } = [];

    public Task<RewriteModelResult> GenerateCandidateAsync(
        RewriteModelRequest request,
        CancellationToken cancellationToken)
    {
        CallCount += 1;
        LastRequest = request;
        Requests.Add(request);
        return Task.FromResult(_results.Dequeue());
    }
}

internal sealed class QueueWritingSignalClient(params WritingSignalResult[] results) : IWritingSignalClient
{
    private readonly Queue<WritingSignalResult> _results = new(results);

    public int CallCount { get; private set; }

    public Task<WritingSignalResult> MeasureAsync(string text, CancellationToken cancellationToken)
    {
        CallCount += 1;
        return Task.FromResult(_results.Dequeue());
    }
}

internal sealed class FakeFidelityJudge(FidelityJudgeResult result) : IFidelityJudge
{
    public int CallCount { get; private set; }

    public Task<FidelityJudgeResult> EvaluateAsync(
        string sourceText,
        string candidateText,
        IReadOnlyList<string>? protectedTerms,
        CancellationToken ct)
    {
        CallCount += 1;
        return Task.FromResult(result);
    }
}
