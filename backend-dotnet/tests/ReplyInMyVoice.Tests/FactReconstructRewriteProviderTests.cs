using System.Text.Json;
using FluentAssertions;
using ReplyInMyVoice.Domain.Contracts;
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
            new WritingSignalResult(false, null, "sapling_unavailable"));
        var provider = new FactReconstructRewriteProvider(model, signal);

        var result = await provider.RewriteAsync(Guid.NewGuid(), ValidRequest(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("quality_signal_unavailable");
        model.CallCount.Should().Be(0);
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
        var provider = new FactReconstructRewriteProvider(model, signal);

        var result = await provider.RewriteAsync(Guid.NewGuid(), PolicyRequest(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("structure_gate_failed");
    }

    [Fact]
    public async Task RewriteAsync_returns_success_json_when_candidate_passes_gates_and_naturalness_rule()
    {
        var model = new RecordingRewriteModelClient(
            new RewriteModelResult(
                "Hi Jordan,\n\nThe NZD $200 preview looks like it is from the three temporary contractor seats added on May 3. I will not change the account unless you confirm.\n\nPlease let me know how you want to proceed.",
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
            new RewriteModelResult("Hi Jordan,\n\nThanks for reaching out about this. I will look into it and follow up soon.", true, null));
        var signal = new QueueWritingSignalClient(
            new WritingSignalResult(true, 35, null),
            new WritingSignalResult(true, 51, null));
        var provider = new FactReconstructRewriteProvider(model, signal);

        var result = await provider.RewriteAsync(Guid.NewGuid(), ValidRequest(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("naturalness_gate_failed");
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

    public Task<RewriteModelResult> GenerateCandidateAsync(
        RewriteModelRequest request,
        CancellationToken cancellationToken)
    {
        CallCount += 1;
        LastRequest = request;
        return Task.FromResult(_results.Dequeue());
    }
}

internal sealed class QueueWritingSignalClient(params WritingSignalResult[] results) : IWritingSignalClient
{
    private readonly Queue<WritingSignalResult> _results = new(results);

    public Task<WritingSignalResult> MeasureAsync(string text, CancellationToken cancellationToken) =>
        Task.FromResult(_results.Dequeue());
}
