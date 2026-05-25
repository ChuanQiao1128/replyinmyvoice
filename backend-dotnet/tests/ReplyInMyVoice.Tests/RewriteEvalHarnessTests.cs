using FluentAssertions;
using ReplyInMyVoice.Domain.RewriteEngine;
using ReplyInMyVoice.Infrastructure.Providers;

namespace ReplyInMyVoice.Tests;

// Zero-spend tests for the C# rewrite eval harness (backend-dotnet/tools/ReplyInMyVoice.Eval).
// No provider calls: they exercise the section-format parser and the scoring helpers only.
public class RewriteEvalHarnessTests
{
    private static string CorpusPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "rewrite-email-eval-cases-100.md");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate docs/rewrite-email-eval-cases-100.md from the test base directory.");
    }

    private static IReadOnlyList<EvalCase> ParseCorpus() =>
        EvalCaseParser.Parse(File.ReadAllText(CorpusPath()));

    [Fact]
    public void Parser_reads_all_100_cases_with_required_fields()
    {
        var cases = ParseCorpus();

        cases.Should().HaveCount(100);
        cases.Select(c => c.CaseNumber).Should().BeEquivalentTo(Enumerable.Range(1, 100));
        cases.Should().OnlyContain(c =>
            !string.IsNullOrWhiteSpace(c.Id)
            && !string.IsNullOrWhiteSpace(c.InputDraft)
            && !string.IsNullOrWhiteSpace(c.WhatHappened)
            && c.MustKeep.Count > 0
            && c.MustNotClaim.Count > 0);

        // The 081-100 holdout band is materialized.
        cases.Count(c => c.CaseNumber >= 81).Should().Be(20);
    }

    [Fact]
    public void ToRewriteRequest_sends_only_draft_and_tone()
    {
        // Faithful to the corpus contract: the engine sees only the draft + tone. We must
        // never feed it must_keep / what_happened, or we measure an easier task than prod.
        var sample = ParseCorpus().First();

        var request = sample.ToRewriteRequest();

        request.RoughDraftReply.Should().Be(sample.InputDraft);
        request.Tone.Should().BeOneOf("warm", "direct");
        request.MessageToReplyTo.Should().BeNull();
        request.Audience.Should().BeNull();
        request.Purpose.Should().BeNull();
        request.WhatHappened.Should().BeNull();
        request.FactsToPreserve.Should().BeNull();
    }

    [Fact]
    public void ForbiddenClaimScreen_flags_unnegated_refund_promise()
    {
        var result = ForbiddenClaimScreen.Check(
            "Thanks for your patience. We will refund you the full amount today.",
            new[] { "Do not promise a cash refund." });

        result.Violations.Should().ContainSingle();
        result.Abstained.Should().BeEmpty();
    }

    [Fact]
    public void ForbiddenClaimScreen_ignores_negated_refund()
    {
        var result = ForbiddenClaimScreen.Check(
            "I am sorry, but we cannot offer a refund while the replacement is active.",
            new[] { "Do not promise a cash refund." });

        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public void ForbiddenClaimScreen_abstains_on_non_lexical_constraint()
    {
        var result = ForbiddenClaimScreen.Check(
            "Here is one combined plan that handles both problems together.",
            new[] { "Do not merge the two issues into a single resolution path." });

        result.Violations.Should().BeEmpty();
        result.Abstained.Should().ContainSingle();
    }

    [Theory]
    [InlineData(true, true, true, 0, true)]   // all gates pass
    [InlineData(false, true, true, 0, false)] // engine failed
    [InlineData(true, false, true, 0, false)] // no output
    [InlineData(true, true, false, 0, false)] // missing facts
    [InlineData(true, true, true, 1, false)]  // forbidden violation
    public void CustomerUsable_composite(bool success, bool hasOutput, bool facts, int forbidden, bool expected)
    {
        CustomerUsableEvaluator.IsCustomerUsable(success, hasOutput, facts, forbidden)
            .Should().Be(expected);
    }

    [Fact]
    public void RelaxedGate_recovers_naturalness_failure_with_clean_candidate()
    {
        // An attempt scored 30% (below the 40 threshold) with facts intact: under the TS
        // relaxed rule it would pass, so the C# gate-rule divergence is the cause of failure.
        var history = new[]
        {
            new RewriteAttemptHistoryItem(
                1,
                RewriteStrategy.TargetedSentenceRepair,
                "Your order ORD-1 shipped on May 19 and arrives soon.",
                new[] { RewriteFailureKind.SignalNotImproved },
                "Naturalness gate failed.",
                8,
                30),
        };

        RelaxedNaturalnessProbe.WouldPassUnderRelaxedGate(
                history,
                mustKeep: new[] { "The order is ORD-1.", "It shipped on May 19." },
                mustNotClaim: Array.Empty<string>(),
                naturalnessThreshold: 40)
            .Should().BeTrue();
    }

    [Fact]
    public void RelaxedGate_does_not_recover_when_no_candidate_is_clean()
    {
        // Every attempt stayed above the threshold, so this is a genuine naturalness miss.
        var history = new[]
        {
            new RewriteAttemptHistoryItem(
                1,
                RewriteStrategy.TargetedSentenceRepair,
                "Your order ORD-1 shipped on May 19.",
                new[] { RewriteFailureKind.SignalNotImproved },
                "Naturalness gate failed.",
                8,
                55),
        };

        RelaxedNaturalnessProbe.WouldPassUnderRelaxedGate(
                history,
                mustKeep: new[] { "The order is ORD-1." },
                mustNotClaim: Array.Empty<string>(),
                naturalnessThreshold: 40)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("The recipient is Ren.", "Hi Ren, I need to move our appointment.")]
    [InlineData("The account is Northstar.", "Thanks again for the call about the Northstar rollout.")]
    [InlineData("The candidate is Alina.", "Hi Alina, thank you for meeting with the product team.")]
    [InlineData("The order identifier is ORD-66120.", "I looked into order ORD-66120 this morning.")]
    [InlineData("The invoice INV-8842 was for $186.00.", "I reviewed invoice INV-8842 for $186.00.")]
    public void FactChecker_matches_declarative_facts_via_anchors(string fact, string rewrite)
    {
        // These were false-negatives before the anchor-first matcher fix (2026-05-26 baseline).
        FactExpectationChecker.Check(rewrite, new[] { fact }).Passed.Should().BeTrue();
    }

    [Fact]
    public void FactChecker_still_fails_a_genuinely_absent_fact()
    {
        FactExpectationChecker.Check(
                "Hi Ren, your appointment is set.",
                new[] { "The order identifier is ORD-99999." })
            .Passed.Should().BeFalse();
    }

    [Fact]
    public void ForbiddenScreen_does_not_flag_negated_refund_in_policy_text()
    {
        // The 2026-05-26 baseline false-flagged these; sentence-level negation now clears them.
        var result = ForbiddenClaimScreen.Check(
            "I cannot issue a cash refund for both items; our policy allows a replacement, not a direct refund.",
            new[] { "Do not promise a cash refund." });

        result.Violations.Should().BeEmpty();
    }

    [Theory]
    // Decimal point no longer splits the sentence away from its "cannot" hedge (case 032).
    [InlineData("Our policy lets me issue a prorated credit if confirmed, but I cannot promise a full $74.00 refund at this point.")]
    // Partial refund is not the forbidden full refund (case 041).
    [InlineData("Because you reported within the window, I can process a partial refund for the mixing bowl set only.")]
    // Refund conditioned on receipt; decimal-safe sentence keeps the "once" hedge (case 061).
    [InlineData("Once it arrives, I can process a $34.00 refund to your card within 3-5 business days.")]
    // Completed past action reports a fact, not a forward promise (case 061).
    [InlineData("The $22.50 refund was issued on June 6 and should appear on your statement within 2 business days.")]
    // "whether ... or a full refund" is an open question, not a promise (case 071).
    [InlineData("I understand you need an answer by May 2 — whether a replacement ships or a full refund of $94.00 will be issued to your card.")]
    // Conditional "if ... we will refund" (case 092).
    [InlineData("If the duplicate is confirmed on our end, we will refund $247.00 to the original payment method.")]
    public void ForbiddenScreen_does_not_flag_conditional_partial_or_completed_refund(string rewrite)
    {
        ForbiddenClaimScreen.Check(rewrite, new[] { "Do not promise a full refund." })
            .Violations.Should().BeEmpty();
    }

    [Fact]
    public void ForbiddenScreen_still_flags_unconditional_promise_with_decimal_amount()
    {
        // Guard: the decimal-aware sentence split must not blind the screen to a genuine,
        // un-hedged commitment that happens to contain a "$94.00"-style amount.
        ForbiddenClaimScreen.Check(
                "I will issue a full refund of $94.00 to your original card today.",
                new[] { "Do not promise a full refund." })
            .Violations.Should().ContainSingle();
    }

    [Theory]
    // Verb-inflection / paraphrase pairs that false-negatived on the 2026-05-26 baseline.
    [InlineData("No fix date can be promised.", "I cannot promise a fix date at this point — the team is still working on it.")]
    [InlineData("No fix can be promised at this time.", "I don't have a fix to announce yet and I can't promise it'll be resolved soon.")]
    [InlineData("The team is actively investigating.", "Our team is looking into it and will post an update by noon.")]
    [InlineData("No perishable donations.", "Bring items to donate to the silent auction; please no perishables and nothing above $75.")]
    [InlineData("The teacher is not making an assessment or diagnosis.", "I'm not drawing conclusions and I'm not qualified to make any kind of assessment.")]
    [InlineData("Missing the new deadline risks the June 4 staging sign-off.", "Wednesday noon is the latest we can accept it and still hit the June 4 staging sign-off.")]
    [InlineData("No further detailed feedback beyond those two points can be shared.", "I am not able to go further than that in terms of feedback, but I can share the two specific points the panel noted.")]
    public void FactChecker_matches_paraphrased_facts(string fact, string rewrite)
    {
        FactExpectationChecker.Check(rewrite, new[] { fact }).Passed.Should().BeTrue();
    }

    [Fact]
    public void FactChecker_paraphrase_aliases_do_not_pass_a_dropped_anchor()
    {
        // Guard: a fact whose proper-noun/ID anchor is absent stays missing even when some
        // of its paraphrasable words appear — aliases must not manufacture a false pass.
        FactExpectationChecker.Check(
                "I cannot promise a fix date for your ticket yet.",
                new[] { "The ticket is SUP-20847." })
            .Passed.Should().BeFalse();
    }
}
