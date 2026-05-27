using FluentAssertions;
using ReplyInMyVoice.Domain.Quality;
using ReplyInMyVoice.Domain.RewriteEngine;

namespace ReplyInMyVoice.Tests;

public class QualityGateChainTests
{
    private static RewriteFact Fact(string text, RewriteFactCategory category) =>
        new($"id_{text}", text, "roughDraftReply", RewriteFactImportance.Critical, category, true, text);

    private static QualityContext Context(
        IReadOnlyList<ProtectedTerm>? protectedTerms = null,
        IReadOnlyList<Boundary>? boundaries = null,
        RewriteFactLedger? factLedger = null) =>
        new(
            factLedger ?? new RewriteFactLedger(Array.Empty<RewriteFact>()),
            new ProtectedTermLedger(protectedTerms ?? Array.Empty<ProtectedTerm>()),
            new BoundaryLedger(boundaries ?? Array.Empty<Boundary>()));

    [Fact]
    public void Passes_when_all_gates_pass()
    {
        var ctx = Context(
            protectedTerms: new[] { new ProtectedTerm("planter", ProtectedTermKind.BusinessObject), new ProtectedTerm("Mark", ProtectedTermKind.ProperName) },
            boundaries: new[] { new Boundary("I can't waive the late fee", BoundaryKind.NegativeConstraint, BoundaryPolarity.Negative) });

        var report = QualityGateChain.Evaluate(
            "Hi Mark, your planter ships Monday. I can't waive the late fee.", ctx);

        report.Passed.Should().BeTrue();
        report.FactPass.Should().BeTrue();
        report.ProtectedTermPass.Should().BeTrue();
        report.BoundaryPass.Should().BeTrue();
        report.SendabilityTier.Should().Be(SendabilityTier.Sendable);
        report.Reasons.Should().BeEmpty();
    }

    [Fact]
    public void Fails_on_protected_term_substitution()
    {
        var ctx = Context(protectedTerms: new[] { new ProtectedTerm("planter", ProtectedTermKind.BusinessObject) });

        var report = QualityGateChain.Evaluate("Hi Mark, your flowerpot ships Monday.", ctx);

        report.Passed.Should().BeFalse();
        report.ProtectedTermPass.Should().BeFalse();
        report.DriftedTerms.Should().Contain("planter");
    }

    [Fact]
    public void Fails_on_boundary_flip()
    {
        var ctx = Context(boundaries: new[] { new Boundary("I cannot waive the late fee for this order", BoundaryKind.NegativeConstraint, BoundaryPolarity.Negative) });

        var report = QualityGateChain.Evaluate("Sure, I can waive the late fee for this order.", ctx);

        report.Passed.Should().BeFalse();
        report.BoundaryPass.Should().BeFalse();
        report.FlippedBoundaries.Should().NotBeEmpty();
    }

    [Fact]
    public void Fails_on_unsendable_garble()
    {
        var report = QualityGateChain.Evaluate("Hi Mark, your order [[A0]] ships Monday. Thanks, Dana", Context());

        report.Passed.Should().BeFalse();
        report.SendabilityTier.Should().Be(SendabilityTier.Unsendable);
    }

    [Fact]
    public void Aggregates_multiple_failures()
    {
        var ctx = Context(
            protectedTerms: new[] { new ProtectedTerm("planter", ProtectedTermKind.BusinessObject) },
            boundaries: new[] { new Boundary("I cannot offer a refund on opened items", BoundaryKind.RefundLimit, BoundaryPolarity.Negative) });

        var report = QualityGateChain.Evaluate(
            "Your flowerpot ships Monday and I can offer a refund on opened items.", ctx);

        report.Passed.Should().BeFalse();
        report.ProtectedTermPass.Should().BeFalse();
        report.BoundaryPass.Should().BeFalse();
        report.Reasons.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public void Build_assembles_both_ledgers_from_draft_and_fact_ledger()
    {
        const string draft = "Hi Mark, your planter ships Monday. If the box is unopened, we can refund it.";
        var factLedger = new RewriteFactLedger(new[] { Fact("Mark", RewriteFactCategory.Person) });

        var ctx = QualityContext.Build(draft, factLedger, protectedSpans: new[] { "planter" });

        ctx.ProtectedTerms.Terms.Should().Contain(t => t.Text == "planter");
        ctx.ProtectedTerms.Terms.Should().Contain(t => t.Text == "Mark"); // from fact ledger
        ctx.Boundaries.Items.Should().Contain(b => b.Polarity == BoundaryPolarity.Conditional && b.Text.Contains("unopened"));
    }
}
