using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using ReplyInMyVoice.Domain.Quality;
using ReplyInMyVoice.Domain.RewriteEngine;

namespace ReplyInMyVoice.Tests;

// The promoted Domain semantic fidelity judge (Voice+Fidelity track). Layer-2 LLM behavior is exercised
// with a fake `complete`; the live prompt's precision/recall is pinned by the eval fixtures
// (tools/ReplyInMyVoice.Eval/fixtures/gate-regression/), not here.
public class FidelityJudgeTests
{
    private static Func<string, string, CancellationToken, Task<string?>> Fake(string? canned) =>
        (_, _, _) => Task.FromResult(canned);

    private static FidelityJudge JudgeReturning(string? canned) => new(Fake(canned));

    // ---------------- semantic judge: parse / fail-closed / prune ----------------

    [Fact]
    public async Task Flags_object_substitution_seat_credit_to_letter_of_credit()
    {
        // The documented miss of the old SemanticEvalJudge: object substitution must FAIL.
        var canned = "{\"drifts\":[{\"kind\":\"object_substituted\",\"source_value\":\"seat credit\","
            + "\"candidate_span\":\"letter of credit\",\"expected_fix\":\"seat credit\",\"why\":\"different instrument\"}]}";
        var result = await JudgeReturning(canned).EvaluateAsync(
            "We applied a seat credit.", "We issued a letter of credit.", new[] { "seat credit" }, CancellationToken.None);

        result.Passed.Should().BeFalse();
        result.Error.Should().BeNull();
        result.Drifts.Should().ContainSingle(d =>
            d.Kind == FidelityDriftKind.ObjectSubstituted && d.SourceValue == "seat credit" && d.CandidateSpan == "letter of credit");
    }

    [Fact]
    public async Task Passes_when_no_drifts()
    {
        var result = await JudgeReturning("{\"drifts\":[]}").EvaluateAsync("Reply by Friday.", "Please reply by Friday.", null, CancellationToken.None);

        result.Passed.Should().BeTrue();
        result.Drifts.Should().BeEmpty();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task Repair_parses_json_wrapped_in_prose()
    {
        var canned = "Here you go:\n{\"drifts\":[{\"kind\":\"polarity_flipped\",\"source_value\":\"cannot refund\","
            + "\"candidate_span\":\"can refund\",\"expected_fix\":\"cannot refund\",\"why\":\"flip\"}]}\nDone.";
        var result = await JudgeReturning(canned).EvaluateAsync("We cannot refund.", "We can refund.", null, CancellationToken.None);

        result.Error.Should().BeNull();
        result.Drifts.Should().Contain(d => d.Kind == FidelityDriftKind.PolarityFlipped);
    }

    [Fact]
    public async Task Fails_closed_on_unparseable()
    {
        var result = await JudgeReturning("I could not analyze this.").EvaluateAsync("x", "y", null, CancellationToken.None);

        result.Passed.Should().BeFalse();
        result.Error.Should().Be("judge_json_parse_failed");
    }

    [Fact]
    public async Task Fails_closed_on_empty_response()
    {
        var result = await JudgeReturning(null).EvaluateAsync("x", "y", null, CancellationToken.None);

        result.Passed.Should().BeFalse();
        result.Error.Should().Be("judge_empty");
    }

    [Fact]
    public async Task Fails_closed_when_complete_throws()
    {
        var judge = new FidelityJudge((_, _, _) => throw new InvalidOperationException("boom"));
        var result = await judge.EvaluateAsync("x", "y", null, CancellationToken.None);

        result.Passed.Should().BeFalse();
        result.Error.Should().Be("judge_call_failed");
    }

    [Fact]
    public async Task Ignores_unknown_drift_kind()
    {
        var result = await JudgeReturning("{\"drifts\":[{\"kind\":\"made_up\",\"source_value\":\"x\"}]}")
            .EvaluateAsync("a", "b", null, CancellationToken.None);

        result.Passed.Should().BeTrue();
        result.Drifts.Should().BeEmpty();
    }

    [Fact]
    public void Prune_drops_no_op_and_phantom_keeps_genuine()
    {
        var drifts = new[]
        {
            new FidelityDrift(FidelityDriftKind.ObjectSubstituted, "x", "onboarding", "onboarding", "noop"),      // span == fix
            new FidelityDrift(FidelityDriftKind.ObjectSubstituted, "x", "入职", "onboarding", "phantom"),          // span absent, fix already present
            new FidelityDrift(FidelityDriftKind.ObjectSubstituted, "planter", "flowerpot", "planter", "genuine"), // span present, fix absent
        };

        var kept = FidelityJudge.PruneNoOpDrifts(drifts, "the onboarding flowerpot");

        kept.Should().ContainSingle(d => d.CandidateSpan == "flowerpot");
    }

    // ---------------- combiner: judge layers onto the deterministic chain, adds failures only, fail-closed ----------------

    private sealed class StubJudge : IFidelityJudge
    {
        private readonly FidelityJudgeResult _result;
        public StubJudge(FidelityJudgeResult result) => _result = result;
        public Task<FidelityJudgeResult> EvaluateAsync(string s, string c, IReadOnlyList<string>? p, CancellationToken ct) => Task.FromResult(_result);
    }

    private static QualityContext CleanContext() =>
        new(
            new RewriteFactLedger(Array.Empty<RewriteFact>()),
            new ProtectedTermLedger(Array.Empty<ProtectedTerm>()),
            new BoundaryLedger(Array.Empty<Boundary>()));

    [Fact]
    public async Task Combiner_passes_when_deterministic_and_judge_pass()
    {
        var report = await QualityGateChain.EvaluateWithFidelityAsync(
            "Hello, your order ships Monday.", "Your order ships Monday.", CleanContext(),
            new StubJudge(FidelityJudgeResult.Clean), CancellationToken.None);

        report.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task Combiner_fails_when_judge_finds_object_substitution_even_if_deterministic_passes()
    {
        var judge = new StubJudge(new FidelityJudgeResult(
            false,
            new[] { new FidelityDrift(FidelityDriftKind.ObjectSubstituted, "seat credit", "letter of credit", "seat credit", "diff") },
            null));

        var report = await QualityGateChain.EvaluateWithFidelityAsync(
            "Hello, your order ships Monday.", "Your order ships Monday.", CleanContext(), judge, CancellationToken.None);

        report.Passed.Should().BeFalse();
        report.DriftedTerms.Should().Contain("seat credit");
        report.Reasons.Should().Contain(r => r.Contains("FidelityJudge") && r.Contains("ObjectSubstituted"));
    }

    [Fact]
    public async Task Combiner_fails_closed_when_judge_errors()
    {
        var judge = new StubJudge(new FidelityJudgeResult(false, Array.Empty<FidelityDrift>(), "judge_call_failed"));

        var report = await QualityGateChain.EvaluateWithFidelityAsync(
            "Hello, your order ships Monday.", "Your order ships Monday.", CleanContext(), judge, CancellationToken.None);

        report.Passed.Should().BeFalse();
        report.Reasons.Should().Contain(r => r.Contains("FidelityJudge unavailable"));
    }
}
