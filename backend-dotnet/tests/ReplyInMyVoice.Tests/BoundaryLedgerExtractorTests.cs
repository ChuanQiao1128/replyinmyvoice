using FluentAssertions;
using ReplyInMyVoice.Domain.Quality;
using ReplyInMyVoice.Domain.RewriteEngine;

namespace ReplyInMyVoice.Tests;

public class BoundaryLedgerExtractorTests
{
    private static RewriteFact Fact(string text, RewriteFactCategory category) =>
        new($"id_{text}", text, "roughDraftReply", RewriteFactImportance.Critical, category, true, text);

    [Fact]
    public void FromFactLedger_maps_negative_and_uncertain_and_ignores_others()
    {
        var ledger = new RewriteFactLedger(new[]
        {
            Fact("I cannot waive the late fee", RewriteFactCategory.NegativeConstraint),
            Fact("you may be eligible for a partial credit", RewriteFactCategory.Condition),
            Fact("$1,250.00", RewriteFactCategory.Amount),
            Fact("Mark", RewriteFactCategory.Person),
        });

        var items = BoundaryLedgerExtractor.FromFactLedger(ledger);

        items.Should().Contain(b => b.Polarity == BoundaryPolarity.Negative && b.Text.Contains("late fee"));
        items.Should().Contain(b => b.Polarity == BoundaryPolarity.Uncertain && b.Text.Contains("eligible"));
        items.Should().NotContain(b => b.Text.Contains("1,250") || b.Text == "Mark");
    }

    [Fact]
    public void Build_picks_up_conditional_sentences_from_the_draft()
    {
        const string draft = "Thanks for the photos. If the box is unopened, we can refund the full amount. Let me know.";
        var emptyLedger = new RewriteFactLedger(Array.Empty<RewriteFact>());

        var ledger = BoundaryLedgerExtractor.Build(draft, emptyLedger);

        ledger.Items.Should().Contain(b =>
            b.Polarity == BoundaryPolarity.Conditional && b.Text.Contains("unopened"));
    }

    [Fact]
    public void Build_validates_augmented_spans_substring_and_marker()
    {
        const string draft = "We will not ship to PO boxes. Standard delivery applies.";
        var emptyLedger = new RewriteFactLedger(Array.Empty<RewriteFact>());

        var ledger = BoundaryLedgerExtractor.Build(
            draft,
            emptyLedger,
            augmentedSpans: new[]
            {
                "will not ship to PO boxes",      // substring + negative marker -> kept
                "no returns after 30 days",       // NOT a substring of the draft -> dropped
                "Standard delivery applies",      // substring but no marker -> dropped (not a boundary)
            });

        ledger.Items.Should().Contain(b => b.Text == "will not ship to PO boxes" && b.Polarity == BoundaryPolarity.Negative);
        ledger.Items.Should().NotContain(b => b.Text.Contains("no returns after 30 days"));
        ledger.Items.Should().NotContain(b => b.Text.Contains("Standard delivery"));
    }

    [Fact]
    public void Built_ledger_then_gate_catches_negation_flip_end_to_end()
    {
        var factLedger = new RewriteFactLedger(new[]
        {
            Fact("I cannot waive the forty dollar fee for late returns", RewriteFactCategory.NegativeConstraint),
        });

        var ledger = BoundaryLedgerExtractor.Build("draft text", factLedger);

        var flipped = BoundaryGate.Check("I can waive the forty dollar fee for late returns.", ledger);
        flipped.Passed.Should().BeFalse();

        var faithful = BoundaryGate.Check("I cannot waive the forty dollar fee for late returns.", ledger);
        faithful.Passed.Should().BeTrue();
    }

    [Fact]
    public void Soft_first_person_volition_is_not_a_boundary_but_a_hard_refusal_is()
    {
        var ledger = new RewriteFactLedger(new[]
        {
            Fact("I do not want any of them to get lost", RewriteFactCategory.NegativeConstraint),
            Fact("I cannot waive the late fee", RewriteFactCategory.NegativeConstraint),
        });

        var items = BoundaryLedgerExtractor.FromFactLedger(ledger);

        // Soft volition ("I do not want...") is a preference, not a policy constraint -> dropped, so a
        // faithful affirmative rephrase ("I'll make sure each is handled") is not a false flip.
        items.Should().NotContain(b => b.Text.Contains("get lost"));
        // A hard capability refusal is still a boundary.
        items.Should().Contain(b => b.Text.Contains("late fee"));
    }
}
