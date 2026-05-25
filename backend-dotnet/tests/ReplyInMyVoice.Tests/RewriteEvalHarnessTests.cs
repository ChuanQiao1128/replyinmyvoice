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
}
