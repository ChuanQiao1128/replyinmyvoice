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

    [Fact]
    public void StructureGate_blocks_dangling_title_case_labels_at_the_end()
    {
        var request = new RewriteRequest(
            "Can I turn this in Monday?",
            "Accept by Monday at 8:00 AM with no late penalty.",
            "Student",
            "Reply with the extension boundary.",
            "The student can submit Monday at 8:00 AM.",
            "No late penalty if submitted by Monday at 8:00 AM.",
            "warm");
        var analysis = RewriteInputAnalyzer.Analyze(request);
        var candidate = "Hi, I can accept this by Monday at 8:00 AM with no late penalty. Please email me if uploading still does not work. New Student";

        var result = RewriteStructureGate.Check(candidate, analysis);

        result.Passed.Should().BeFalse();
        result.FailureKinds.Should().Contain(RewriteFailureKind.SupportMacroVoice);
    }

    [Fact]
    public void FactLedger_extracts_thousands_amounts_for_exact_fact_gate()
    {
        var request = new RewriteRequest(
            null,
            "Reply to the customer.",
            "Customer",
            "Explain invoice discrepancy.",
            null,
            "1,200 M8 brackets at $1.85 each, invoice $2,220. Short 240 worth $444.",
            "direct");

        var ledger = FactLedgerExtractor.Extract(request);

        ledger.Facts.Should().Contain(fact =>
            fact.Category == RewriteFactCategory.Amount &&
            fact.Text == "$2,220");

        var result = RewriteFactGate.Check(
            "The 1,200 M8 brackets were $1.85 each, and the short 240 units are worth $444.",
            ledger);

        result.Passed.Should().BeFalse();
        result.Reasons.Should().Contain(reason => reason.Contains("$2,220", StringComparison.Ordinal));
    }

    [Fact]
    public void FactGate_accepts_both_as_equivalent_to_two_count_fact()
    {
        var request = new RewriteRequest(
            null,
            "Reply to the customer.",
            "Customer",
            "Explain return status.",
            null,
            "Two jackets returned June 4. $64 refund posted June 7 for one jacket. $52 refund is pending.",
            "warm");
        var ledger = FactLedgerExtractor.Extract(request);

        var result = RewriteFactGate.Check(
            "I can confirm we received both jackets on June 4. The $64 refund posted June 7 for one jacket, and the $52 refund is pending.",
            ledger);

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void FactGate_accepts_number_words_as_equivalent_to_digit_count_facts()
    {
        var request = new RewriteRequest(
            null,
            "Reply to the candidate.",
            "Candidate",
            "Confirm interview details.",
            null,
            "60-minute panel with 4 interviewers.",
            "warm");
        var ledger = FactLedgerExtractor.Extract(request);

        var result = RewriteFactGate.Check(
            "The interview is a 60-minute panel with four interviewers.",
            ledger);

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void FactGate_blocks_dense_procurement_reply_missing_invoice_total()
    {
        var request = new RewriteRequest(
            null,
            "Reply to the customer.",
            "Customer",
            "Explain shortage review.",
            null,
            "Bellwick Manufacturing PO-944. 1,200 M8 brackets at $1.85 each, invoice $2,220. Receiving counted 960, short 240 worth $444.",
            "direct");
        var ledger = FactLedgerExtractor.Extract(request);

        var result = RewriteFactGate.Check(
            "Hi Bellwick Manufacturing, thank you for flagging the shortage on PO-944. We can discuss a $444 credit for the 240 short units at $1.85 each.",
            ledger);

        result.Passed.Should().BeFalse();
        result.Reasons.Should().Contain(reason => reason.Contains("$2,220", StringComparison.Ordinal));
    }

    [Fact]
    public void FactGate_blocks_membership_reply_missing_original_payment_and_start_date()
    {
        var request = new RewriteRequest(
            null,
            "Reply to the member.",
            "Member",
            "Explain transfer options.",
            null,
            "Hana Wells paid $540 for six-month membership starting March 1. Four months remain as of May 1 and original end date is August 31. One transfer allowed with $35 admin fee.",
            "warm");
        var ledger = FactLedgerExtractor.Extract(request);

        var result = RewriteFactGate.Check(
            "Hi Hana, you can transfer the membership to Mia with a $35 admin fee. Four months remain as of May 1 and the original end date is August 31.",
            ledger);

        result.Passed.Should().BeFalse();
        result.Reasons.Should().Contain(reason => reason.Contains("$540", StringComparison.Ordinal));
        result.Reasons.Should().Contain(reason => reason.Contains("March 1", StringComparison.Ordinal));
    }

    [Fact]
    public void FactGate_blocks_unsupported_workplace_judgment_labels()
    {
        var request = new RewriteRequest(
            null,
            "Reply to the employee.",
            "Employee",
            "Schedule a private follow-up.",
            "Two teammates reported feeling interrupted during roadmap discussion.",
            "Do not name teammates. Do not promise HR will never be involved.",
            "direct");
        var ledger = FactLedgerExtractor.Extract(request);

        var result = RewriteFactGate.Check(
            "Two teammates said they felt interrupted, and your comments came across as dismissive.",
            ledger);

        result.Passed.Should().BeFalse();
        result.FailureKinds.Should().Contain(RewriteFailureKind.UnsupportedFact);
    }
}
