using FluentAssertions;
using ReplyInMyVoice.Domain.Quality;
using ReplyInMyVoice.Domain.RewriteEngine;

namespace ReplyInMyVoice.Tests;

// Tests the ledger builder: deterministic fact-anchor mapping, exact-substring validation of proposed
// spans (the proposer can never inject an invented term), dedup, and the proposer->ledger->gate pipeline.
public class ProtectedTermLedgerExtractorTests
{
    private static RewriteFact Fact(string text, RewriteFactCategory category) =>
        new($"id_{text}", text, "roughDraftReply", RewriteFactImportance.Critical, category, true, text);

    [Fact]
    public void FromFactLedger_maps_categories_to_protected_term_kinds()
    {
        var ledger = new RewriteFactLedger(new[]
        {
            Fact("Mark", RewriteFactCategory.Person),
            Fact("$1,250.00", RewriteFactCategory.Amount),
            Fact("June 10", RewriteFactCategory.DateOrDeadline),
            Fact("INV-204", RewriteFactCategory.Identifier),
            Fact("18", RewriteFactCategory.Count),
        });

        var terms = ProtectedTermLedgerExtractor.FromFactLedger(ledger);

        terms.Should().Contain(t => t.Text == "Mark" && t.Kind == ProtectedTermKind.ProperName);
        terms.Should().Contain(t => t.Text == "$1,250.00" && t.Kind == ProtectedTermKind.Amount);
        terms.Should().Contain(t => t.Text == "June 10" && t.Kind == ProtectedTermKind.DateTime);
        terms.Should().Contain(t => t.Text == "INV-204" && t.Kind == ProtectedTermKind.Identifier);
        terms.Should().Contain(t => t.Text == "18" && t.Kind == ProtectedTermKind.Amount);
        terms.Should().OnlyContain(t => t.ExactRequired);
    }

    [Fact]
    public void FromFactLedger_skips_number_word_counts_and_boundary_categories()
    {
        var ledger = new RewriteFactLedger(new[]
        {
            Fact("two", RewriteFactCategory.Count),                                     // number-word count -> skip
            Fact("cannot refund the deposit", RewriteFactCategory.NegativeConstraint), // boundary -> skip
            Fact("may be eligible", RewriteFactCategory.Condition),                     // boundary -> skip
            Fact("ask the customer to reply", RewriteFactCategory.NextStep),            // boundary -> skip
        });

        ProtectedTermLedgerExtractor.FromFactLedger(ledger).Should().BeEmpty();
    }

    [Fact]
    public void Build_keeps_only_proposed_spans_that_are_exact_substrings_of_the_draft()
    {
        const string draft = "Your planter is on back-order; the seat credit still applies.";
        var emptyLedger = new RewriteFactLedger(Array.Empty<RewriteFact>());

        var result = ProtectedTermLedgerExtractor.Build(
            draft,
            emptyLedger,
            new[] { "planter", "seat credit", "flowerpot" }); // "flowerpot" is NOT in the draft

        result.Terms.Should().Contain(t => t.Text == "planter" && t.Kind == ProtectedTermKind.BusinessObject);
        result.Terms.Should().Contain(t => t.Text == "seat credit");
        result.Terms.Should().NotContain(t => t.Text == "flowerpot"); // dropped: invented span
    }

    [Fact]
    public void Build_merges_fact_terms_and_proposed_spans_without_duplicating()
    {
        const string draft = "Hi Mark, your planter ships Monday.";
        var ledger = new RewriteFactLedger(new[] { Fact("Mark", RewriteFactCategory.Person) });

        var result = ProtectedTermLedgerExtractor.Build(draft, ledger, new[] { "planter", "Mark" });

        result.Terms.Count(t => string.Equals(t.Text, "Mark", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
        result.Terms.Should().Contain(t => t.Text == "planter");
    }

    [Fact]
    public async Task BuildAsync_fetches_spans_from_the_proposer_then_validates()
    {
        const string draft = "The saucer is fine; the dish rack is cracked.";
        var emptyLedger = new RewriteFactLedger(Array.Empty<RewriteFact>());
        var proposer = new FakeProposer(new[] { "saucer", "dish rack", "teapot" }); // "teapot" not in draft

        var result = await ProtectedTermLedgerExtractor.BuildAsync(draft, emptyLedger, proposer, CancellationToken.None);

        result.Terms.Select(t => t.Text).Should().Contain(new[] { "saucer", "dish rack" });
        result.Terms.Should().NotContain(t => t.Text == "teapot");
    }

    [Fact]
    public void Built_ledger_marks_proposed_spans_soft_and_enforces_hard_anchors()
    {
        const string draft = "Hi Mark, your planter ships Monday.";
        var ledger = ProtectedTermLedgerExtractor.Build(
            draft,
            new RewriteFactLedger(new[] { Fact("Mark", RewriteFactCategory.Person) }),
            new[] { "planter" });

        // A proposed business-object span is SOFT (ExactRequired: false) so the deterministic gate never
        // false-positives on a legit rephrase of it — object substitution on these is the FidelityJudge's
        // job (rule 4). So a planter->flowerpot swap is NOT a deterministic fail here.
        ledger.Terms.Should().Contain(t => t.Text == "planter" && !t.ExactRequired);
        ProtectedTermGate.Check("Hi Mark, your flowerpot ships Monday.", ledger).Passed.Should().BeTrue();

        // But the hard fact-ledger anchor (the name) IS verbatim-enforced: dropping "Mark" fails.
        var nameDropped = ProtectedTermGate.Check("Hi Sam, your planter ships Monday.", ledger);
        nameDropped.Passed.Should().BeFalse();
        nameDropped.DriftedTerms.Should().Contain("Mark");
    }

    [Fact]
    public void Acronyms_are_exact_required_and_catch_object_drift()
    {
        // "advanced SSO setup" -> "advanced Settings" (the loop's real drift the LLM judge missed).
        const string draft = "I can't add the advanced SSO setup without a new approval cycle.";
        var ledger = ProtectedTermLedgerExtractor.Build(draft, new RewriteFactLedger(Array.Empty<RewriteFact>()), Array.Empty<string>());

        ledger.Terms.Should().Contain(t => t.Text == "SSO" && t.ExactRequired);

        ProtectedTermGate.Check("If you need advanced Settings or a discount, that needs approval.", ledger)
            .Passed.Should().BeFalse();
        ProtectedTermGate.Check("I can't add the advanced SSO setup without approval.", ledger)
            .Passed.Should().BeTrue();
    }

    [Fact]
    public void Common_all_caps_words_are_not_treated_as_acronyms()
    {
        var ledger = ProtectedTermLedgerExtractor.Build(
            "OK, I'll send the FAQ ASAP.", new RewriteFactLedger(Array.Empty<RewriteFact>()), Array.Empty<string>());

        ledger.Terms.Should().NotContain(t => t.Text == "OK" || t.Text == "FAQ" || t.Text == "ASAP");
    }

    private sealed class FakeProposer(IReadOnlyList<string> spans) : IProtectedTermProposer
    {
        public Task<IReadOnlyList<string>> ProposeAsync(string draft, CancellationToken cancellationToken) =>
            Task.FromResult(spans);
    }
}
