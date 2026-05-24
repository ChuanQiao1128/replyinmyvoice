using System.Text.Json;
using FluentAssertions;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.RewriteEngine;
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
