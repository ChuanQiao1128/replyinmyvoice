using FluentAssertions;
using ReplyInMyVoice.Domain.Quality;

namespace ReplyInMyVoice.Tests;

// Regression tests for the object/term-substitution drift the LLM judge kept passing in the eval.
// The deterministic gate must FAIL each known miss and must NOT false-positive on faithful reformats.
public class ProtectedTermGateTests
{
    private static ProtectedTermLedger Ledger(params ProtectedTerm[] terms) => new(terms);

    [Theory]
    [InlineData("seat credit", "We have applied a letter of credit to your account.")]
    [InlineData("planter", "Your flowerpot will ship on Monday.")]
    [InlineData("saucer", "The tea tray is included with your order.")]
    [InlineData("dish rack", "The draining rack was not damaged in your photos.")]
    public void Flags_business_object_substitution(string protectedTerm, string candidate)
    {
        var result = ProtectedTermGate.Check(
            candidate,
            Ledger(new ProtectedTerm(protectedTerm, ProtectedTermKind.BusinessObject)));

        result.Passed.Should().BeFalse();
        result.DriftedTerms.Should().Contain(protectedTerm);
    }

    [Fact]
    public void Flags_proper_name_substitution_Celestine_to_Celeste()
    {
        var result = ProtectedTermGate.Check(
            "Hi Celeste, thanks so much for your patience.",
            Ledger(new ProtectedTerm("Celestine", ProtectedTermKind.ProperName)));

        result.Passed.Should().BeFalse();
        result.DriftedTerms.Should().Contain("Celestine");
    }

    [Fact]
    public void Passes_business_object_preserved_verbatim()
    {
        var result = ProtectedTermGate.Check(
            "Your seat credit will be applied to the next invoice.",
            Ledger(new ProtectedTerm("seat credit", ProtectedTermKind.BusinessObject)));

        result.Passed.Should().BeTrue();
        result.DriftedTerms.Should().BeEmpty();
    }

    [Fact]
    public void Passes_business_object_match_is_case_insensitive()
    {
        var result = ProtectedTermGate.Check(
            "Your SEAT CREDIT is noted.",
            Ledger(new ProtectedTerm("seat credit", ProtectedTermKind.BusinessObject)));

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void Passes_identifier_reformatted_keeping_digit_run()
    {
        // "INV-8842" -> "invoice 8842" is a faithful reformat: the >=3-digit core survives, so no drift.
        var result = ProtectedTermGate.Check(
            "Your invoice 8842 is still open.",
            Ledger(new ProtectedTerm("INV-8842", ProtectedTermKind.Identifier)));

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void Flags_identifier_digit_corruption()
    {
        var result = ProtectedTermGate.Check(
            "Your invoice 8843 is still open.",
            Ledger(new ProtectedTerm("INV-8842", ProtectedTermKind.Identifier)));

        result.Passed.Should().BeFalse();
        result.DriftedTerms.Should().Contain("INV-8842");
    }

    [Fact]
    public void Passes_amount_and_date_with_normalization()
    {
        var result = ProtectedTermGate.Check(
            "The balance of $1,250.00 is due June 10.",
            Ledger(
                new ProtectedTerm("$1,250.00", ProtectedTermKind.Amount),
                new ProtectedTerm("June 10", ProtectedTermKind.DateTime)));

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void Flags_amount_change()
    {
        var result = ProtectedTermGate.Check(
            "The balance of $1,520.00 is due June 10.",
            Ledger(new ProtectedTerm("$1,250.00", ProtectedTermKind.Amount)));

        result.Passed.Should().BeFalse();
        result.DriftedTerms.Should().Contain("$1,250.00");
    }

    [Fact]
    public void Does_not_match_term_inside_a_larger_word()
    {
        // "rack" must not be satisfied by "tracking"; "dish rack" is genuinely absent here.
        var result = ProtectedTermGate.Check(
            "We are tracking your shipment.",
            Ledger(new ProtectedTerm("dish rack", ProtectedTermKind.BusinessObject)));

        result.Passed.Should().BeFalse();
    }

    [Fact]
    public void Empty_ledger_passes()
    {
        ProtectedTermGate.Check("anything at all", ProtectedTermLedger.Empty).Passed.Should().BeTrue();
    }

    [Fact]
    public void Non_exact_terms_are_not_enforced_deterministically()
    {
        // Non-exact terms are deferred to the FidelityJudge (it can accept a genuine paraphrase), so the
        // deterministic gate ignores them even when absent.
        var result = ProtectedTermGate.Check(
            "Totally different wording.",
            Ledger(new ProtectedTerm("we can discuss the options", ProtectedTermKind.ActionPhrase, ExactRequired: false)));

        result.Passed.Should().BeTrue();
    }
}
