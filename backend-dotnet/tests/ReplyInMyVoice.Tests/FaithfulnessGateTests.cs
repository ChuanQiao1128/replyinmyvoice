using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;

// Regression suite for the eval-only faithfulness gate ("B", plans/faithfulness-gate-spec.md).
// Pins exactly the drifts the old SemanticEvalJudge passed on 2026-05-29. Layer 1 is deterministic
// (no LLM); Layer 2 is exercised with a fake `complete` so parse / repair-parse / fail-closed are tested
// without a live model call.
public class FaithfulnessGateTests
{
    private static Func<string, string, CancellationToken, Task<string?>> Fake(string? canned) =>
        (_, _, _) => Task.FromResult(canned);

    private static FaithfulnessGate GateReturning(string? canned) => new(Fake(canned));

    // ---------------- Layer 1 — deterministic hard anchors ----------------

    [Fact]
    public void Layer1_flags_currency_change_dollars_to_yuan()
    {
        var drifts = FaithfulnessGate.Layer1HardAnchors("Your refund is $12 total.", "Your refund is 12 yuan total.");
        drifts.Should().Contain(d => d.Kind == DriftKind.CurrencyChanged && d.SourceValue == "$12");
    }

    [Fact]
    public void Layer1_flags_proper_name_change_Dev_to_Dave()
    {
        var drifts = FaithfulnessGate.Layer1HardAnchors("Hi Dev, the quote is ready.", "Hi Dave, the quote is ready.");
        drifts.Should().Contain(d => d.Kind == DriftKind.HardAnchorChanged && d.SourceValue == "Dev" && d.CandidateSpan == "Dave");
    }

    [Fact]
    public void Layer1_flags_missing_identifier()
    {
        var drifts = FaithfulnessGate.Layer1HardAnchors("Record FieldTrip-4A-09 is set.", "The record is set.");
        drifts.Should().Contain(d => d.Kind == DriftKind.HardAnchorMissing && d.SourceValue == "FieldTrip-4A-09");
    }

    [Fact]
    public void Layer1_flags_missing_date()
    {
        var drifts = FaithfulnessGate.Layer1HardAnchors("Reply by April 2 please.", "Reply soon please.");
        drifts.Should().Contain(d => d.Kind == DriftKind.HardAnchorMissing && d.SourceValue == "April 2");
    }

    [Fact]
    public void Layer1_passes_when_all_anchors_preserved_through_paraphrase()
    {
        var drifts = FaithfulnessGate.Layer1HardAnchors(
            "Hi Dev, your $12 for FieldTrip-4A-09 is due April 2.",
            "Dev, the $12 for FieldTrip-4A-09 is due by April 2nd.");
        drifts.Should().BeEmpty();
    }

    [Fact]
    public void Layer1_name_presence_is_case_insensitive()
    {
        var drifts = FaithfulnessGate.Layer1HardAnchors("Visit the Science museum.", "visit the science museum.");
        drifts.Should().BeEmpty();
    }

    // ---------------- Layer 2 — fake LLM: parse / repair / fail-closed ----------------

    [Fact]
    public async Task Layer2_parses_drift_from_valid_json()
    {
        var canned = "{\"drifts\":[{\"kind\":\"object_substituted\",\"source_value\":\"permission slip\","
            + "\"candidate_span\":\"receipts\",\"expected_fix\":\"permission slip\",\"why\":\"different item\"}]}";
        var report = await GateReturning(canned).EvaluateAsync("Please reply to confirm.", "Please reply to confirm.", CancellationToken.None);

        report.Passed.Should().BeFalse();
        report.Error.Should().BeNull();
        report.Drifts.Should().ContainSingle(d => d.Kind == DriftKind.ObjectSubstituted && d.SourceValue == "permission slip" && d.CandidateSpan == "receipts");
    }

    [Fact]
    public async Task Layer2_repair_parses_when_json_wrapped_in_prose()
    {
        var canned = "Sure, here is the result:\n{\"drifts\":[{\"kind\":\"polarity_flipped\","
            + "\"source_value\":\"cannot refund\",\"candidate_span\":\"can refund\",\"expected_fix\":\"cannot refund\","
            + "\"why\":\"negation flipped\"}]}\nLet me know if you need more.";
        var report = await GateReturning(canned).EvaluateAsync("We cannot refund this.", "We cannot refund this.", CancellationToken.None);

        report.Error.Should().BeNull();
        report.Drifts.Should().Contain(d => d.Kind == DriftKind.PolarityFlipped && d.SourceValue == "cannot refund");
    }

    [Fact]
    public async Task Layer2_fails_closed_on_unparseable_response()
    {
        var report = await GateReturning("I could not analyze this request.").EvaluateAsync("x", "x", CancellationToken.None);

        report.Passed.Should().BeFalse();
        report.Error.Should().Be("layer2_json_parse_failed");
    }

    [Fact]
    public async Task Layer2_fails_closed_on_empty_response()
    {
        var report = await GateReturning(null).EvaluateAsync("x", "x", CancellationToken.None);

        report.Passed.Should().BeFalse();
        report.Error.Should().Be("layer2_empty");
    }

    [Fact]
    public async Task Gate_passes_when_no_drifts_and_anchors_intact()
    {
        var report = await GateReturning("{\"drifts\":[]}").EvaluateAsync("Please reply by today.", "Please reply by today.", CancellationToken.None);

        report.Passed.Should().BeTrue();
        report.Drifts.Should().BeEmpty();
        report.Error.Should().BeNull();
    }

    [Fact]
    public async Task Layer2_ignores_unknown_drift_kind()
    {
        var canned = "{\"drifts\":[{\"kind\":\"made_up_kind\",\"source_value\":\"whatever\"}]}";
        var report = await GateReturning(canned).EvaluateAsync("Please reply.", "Please reply.", CancellationToken.None);

        report.Drifts.Should().BeEmpty();
        report.Passed.Should().BeTrue();
    }

    // ---------------- PruneNoOpDrifts — cross-lingual precision guard ----------------

    [Fact]
    public void Prune_drops_literal_no_op_span_equals_fix()
    {
        var drifts = new[] { new DriftSpan(DriftKind.HardAnchorChanged, "onboarding", "onboarding", "onboarding", "noop") };
        FaithfulnessGate.PruneNoOpDrifts(drifts, "报价含 onboarding、admin workspace。").Should().BeEmpty();
    }

    [Fact]
    public void Prune_drops_phantom_when_corrected_token_already_present()
    {
        // Gate claims a Chinese rendering "入职" that isn't in the candidate; the English "onboarding" already is.
        var drifts = new[] { new DriftSpan(DriftKind.HardAnchorChanged, "onboarding", "入职", "onboarding", "phantom") };
        FaithfulnessGate.PruneNoOpDrifts(drifts, "报价含 onboarding 和支持。").Should().BeEmpty();
    }

    [Fact]
    public void Prune_keeps_genuine_term_drift()
    {
        // Candidate really translated the term; the English token is absent → a real fix to apply.
        var drifts = new[] { new DriftSpan(DriftKind.HardAnchorChanged, "onboarding", "入职", "onboarding", "real") };
        FaithfulnessGate.PruneNoOpDrifts(drifts, "报价含入职和支持。").Should().ContainSingle(d => d.CandidateSpan == "入职");
    }

    [Fact]
    public void Prune_keeps_deletion_of_unsupported_addition()
    {
        // expected_fix empty = delete an addition; span is present in the candidate → genuine, must be kept.
        var drifts = new[] { new DriftSpan(DriftKind.UnsupportedAddition, "(none)", "正好可以往前赶", "", "addition") };
        FaithfulnessGate.PruneNoOpDrifts(drifts, "那个房间空出来了，正好可以往前赶。").Should().ContainSingle(d => d.ExpectedFix == "");
    }
}
