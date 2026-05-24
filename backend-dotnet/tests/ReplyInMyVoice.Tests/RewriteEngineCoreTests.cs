using FluentAssertions;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.RewriteEngine;

namespace ReplyInMyVoice.Tests;

public sealed class RewriteEngineCoreTests
{
    [Fact]
    public void Analyze_routes_policy_heavy_support_replies_to_policy_options_strategy()
    {
        var request = new RewriteRequest(
            "The customer asked whether they can transfer their course seat or receive a refund.",
            "Tell Daniel he may be eligible for a partial credit, but we cannot change the enrollment unless he confirms.",
            "Customer",
            "Explain transfer and refund options without promising approval.",
            "The transfer window is closed. A partial credit may be available after review.",
            "Do not change the enrollment without confirmation. Preserve partial-credit eligibility language.",
            "warm");

        var analysis = RewriteInputAnalyzer.Analyze(request);

        analysis.Scenario.Should().Be(RewriteScenario.SupportPolicy);
        analysis.RiskLevel.Should().Be(RewriteRiskLevel.High);
        analysis.FactualDensity.Should().Be(RewriteFactualDensity.High);
        analysis.StructureRisk.Should().Be(RewriteStructureRisk.Medium);
        analysis.RequiresPolicyCare.Should().BeTrue();
        analysis.RecommendedInitialStrategy.Should().Be(RewriteStrategy.SupportPolicyOptionsRewrite);
        analysis.Reasons.Should().Contain(reason => reason.Contains("policy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExtractFacts_marks_names_deadlines_money_and_negative_constraints_as_critical()
    {
        var request = new RewriteRequest(
            "Jordan asked whether the NZD $200 invoice preview can be changed by Friday.",
            "Hi Jordan, the NZD $200 invoice preview is from three contractor seats. I will not change the account unless you confirm.",
            "Finance manager",
            "Clarify the invoice preview.",
            "Three temporary contractor seats were added on May 3.",
            "Preserve Jordan, NZD $200, three seats, May 3, Friday, and no account change without confirmation.",
            "direct");

        var ledger = FactLedgerExtractor.Extract(request);

        ledger.Facts.Should().Contain(fact =>
            fact.Category == RewriteFactCategory.Person &&
            fact.Importance == RewriteFactImportance.Critical &&
            fact.Text.Contains("Jordan", StringComparison.Ordinal));
        ledger.Facts.Should().Contain(fact =>
            fact.Category == RewriteFactCategory.Amount &&
            fact.Importance == RewriteFactImportance.Critical &&
            fact.Text.Contains("NZD $200", StringComparison.Ordinal));
        ledger.Facts.Should().Contain(fact =>
            fact.Category == RewriteFactCategory.Count &&
            fact.Importance == RewriteFactImportance.Critical &&
            fact.Text.Contains("three", StringComparison.OrdinalIgnoreCase));
        ledger.Facts.Should().Contain(fact =>
            fact.Category == RewriteFactCategory.DateOrDeadline &&
            fact.Importance == RewriteFactImportance.Critical &&
            fact.Text.Contains("May 3", StringComparison.Ordinal));
        ledger.Facts.Should().Contain(fact =>
            fact.Category == RewriteFactCategory.NegativeConstraint &&
            fact.Importance == RewriteFactImportance.Critical &&
            fact.Text.Contains("without confirmation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StrategyRouter_switches_to_structure_level_strategy_for_sentence_per_paragraph_failure()
    {
        var request = new RewriteRequest(
            null,
            "Thanks for the update. I can send the details today.",
            "Client",
            "Reply naturally.",
            null,
            null,
            "warm");
        var analysis = RewriteInputAnalyzer.Analyze(request);

        var decision = RewriteStrategyRouter.ChooseNext(
            analysis,
            new RewriteFailureEvidence(
                FailureKinds: [RewriteFailureKind.SentencePerParagraph, RewriteFailureKind.LineSplitParaphrase],
                PreviousStrategies: [RewriteStrategy.FactsFirstReconstruct],
                AttemptsUsed: 1));

        decision.Strategy.Should().Be(RewriteStrategy.FullStructureRewrite);
        decision.ModelTier.Should().Be(RewriteModelTier.Standard);
        decision.Reason.ToLowerInvariant().Should().Contain("structure");
    }

    [Fact]
    public void BudgetManager_caps_attempts_at_ten_and_allows_one_strong_attempt_for_high_risk_policy_input()
    {
        var request = new RewriteRequest(
            "The customer asked about a refund and transfer.",
            "Explain that a refund is not guaranteed and no transfer will happen without confirmation.",
            "Customer",
            "Explain policy options.",
            "Transfer depends on eligibility review.",
            "No transfer without confirmation.",
            "direct");
        var analysis = RewriteInputAnalyzer.Analyze(request);

        var budget = RewriteBudgetManager.Create(analysis, requestedMaxAttempts: 99);

        budget.MaxAttempts.Should().Be(10);
        budget.AllowStrongModel.Should().BeTrue();
        budget.MaxStrongAttempts.Should().Be(1);
    }

    [Fact]
    public void StructureGate_blocks_detached_numbered_markers_and_long_sentence_per_paragraph_candidates()
    {
        var request = new RewriteRequest(
            null,
            "Please write a normal support reply.",
            "Customer",
            "Reply with options.",
            null,
            null,
            "warm");
        var analysis = RewriteInputAnalyzer.Analyze(request);
        var candidate = """
            Hi Daniel,

            1.

            I can look into the transfer request.

            2.

            I can also check whether a partial credit is available.

            Please confirm what you would prefer.
            """;

        var result = RewriteStructureGate.Check(candidate, analysis);

        result.Passed.Should().BeFalse();
        result.FailureKinds.Should().Contain(RewriteFailureKind.BrokenNumberedList);
        result.FailureKinds.Should().Contain(RewriteFailureKind.SentencePerParagraph);
    }
}
