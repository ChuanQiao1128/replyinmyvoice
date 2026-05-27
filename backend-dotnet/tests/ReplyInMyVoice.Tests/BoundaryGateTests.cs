using FluentAssertions;
using ReplyInMyVoice.Domain.Quality;

namespace ReplyInMyVoice.Tests;

// Polarity-flip regression tests. Each flip (cannot->can, may->will, "no refund"->refund, dropped
// condition) MUST fail; a faithful rephrase that keeps the same polarity class MUST pass.
public class BoundaryGateTests
{
    private static BoundaryLedger Ledger(params Boundary[] items) => new(items);

    [Fact]
    public void Flags_negative_flip_cannot_to_can()
    {
        var ledger = Ledger(new Boundary(
            "I cannot add the SSO setup without a new approval cycle", BoundaryKind.NegativeConstraint, BoundaryPolarity.Negative));

        var result = BoundaryGate.Check("I can add the SSO setup without a new approval cycle.", ledger);

        result.Passed.Should().BeFalse();
        result.FlippedBoundaries.Should().ContainSingle();
    }

    [Theory]
    [InlineData("I'm not able to add the SSO setup without a new approval cycle.")]
    [InlineData("I am unable to add the SSO setup without a new approval cycle.")]
    public void Passes_negative_preserved_by_rephrase(string candidate)
    {
        var ledger = Ledger(new Boundary(
            "I cannot add the SSO setup without a new approval cycle", BoundaryKind.NegativeConstraint, BoundaryPolarity.Negative));

        BoundaryGate.Check(candidate, ledger).Passed.Should().BeTrue();
    }

    [Fact]
    public void Flags_uncertain_flip_may_to_will()
    {
        var ledger = Ledger(new Boundary(
            "The refund may take up to five business days", BoundaryKind.Modality, BoundaryPolarity.Uncertain));

        BoundaryGate.Check("The refund will take up to five business days.", ledger).Passed.Should().BeFalse();
    }

    [Fact]
    public void Passes_uncertain_preserved_by_rephrase_may_to_might()
    {
        var ledger = Ledger(new Boundary(
            "The refund may take up to five business days", BoundaryKind.Modality, BoundaryPolarity.Uncertain));

        BoundaryGate.Check("The refund might take up to five business days.", ledger).Passed.Should().BeTrue();
    }

    [Fact]
    public void Flags_negative_flip_no_refund_to_refund_available()
    {
        var ledger = Ledger(new Boundary(
            "No refund is available for opened grooming kits", BoundaryKind.RefundLimit, BoundaryPolarity.Negative));

        BoundaryGate.Check("A refund is available for opened grooming kits.", ledger).Passed.Should().BeFalse();
    }

    [Fact]
    public void Flags_dropped_condition()
    {
        var ledger = Ledger(new Boundary(
            "If the box is unopened, we can refund the full amount", BoundaryKind.PolicyLimit, BoundaryPolarity.Conditional));

        // Condition dropped, content preserved -> flip.
        BoundaryGate.Check("We can refund the full amount for the box.", ledger).Passed.Should().BeFalse();
    }

    [Fact]
    public void Passes_condition_preserved()
    {
        var ledger = Ledger(new Boundary(
            "If the box is unopened, we can refund the full amount", BoundaryKind.PolicyLimit, BoundaryPolarity.Conditional));

        BoundaryGate.Check("If the box is unopened, we'll refund the full amount.", ledger).Passed.Should().BeTrue();
    }

    [Fact]
    public void Does_not_flag_when_no_sentence_overlaps_the_boundary()
    {
        // Conservative: a boundary with no clearly-matching sentence is not flagged here (omission is the
        // fact/LLM gate's concern), so the gate stays free of false positives on unrelated rewrites.
        var ledger = Ledger(new Boundary(
            "I cannot waive the late fee", BoundaryKind.NegativeConstraint, BoundaryPolarity.Negative));

        BoundaryGate.Check("Thanks for your patience — your order ships Monday.", ledger).Passed.Should().BeTrue();
    }

    [Fact]
    public void Empty_ledger_passes()
    {
        BoundaryGate.Check("anything at all", BoundaryLedger.Empty).Passed.Should().BeTrue();
    }

    [Theory]
    [InlineData("We cannot refund the deposit unless you cancel in writing.", BoundaryPolarity.Negative)]
    [InlineData("You may be eligible for a partial credit.", BoundaryPolarity.Uncertain)]
    [InlineData("If the seal is intact, we will replace it.", BoundaryPolarity.Conditional)]
    [InlineData("Your order ships Monday.", null)]
    public void InferPolarity_classifies_by_strongest_marker(string text, BoundaryPolarity? expected)
    {
        BoundaryGate.InferPolarity(text).Should().Be(expected);
    }
}
