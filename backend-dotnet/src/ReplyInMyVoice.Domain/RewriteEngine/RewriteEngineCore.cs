using System.Text.RegularExpressions;
using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Domain.RewriteEngine;

public enum RewriteScenario
{
    ShortCasualReply,
    NormalEmail,
    LongSupportEmail,
    SupportPolicy,
    MessyThread,
    QuoteListHeavy,
    DraftOnly,
    AlreadyNatural,
    General,
}

public enum RewriteRiskLevel
{
    Low,
    Medium,
    High,
}

public enum RewriteFactualDensity
{
    Low,
    Medium,
    High,
}

public enum RewriteStructureRisk
{
    Low,
    Medium,
    High,
}

public enum RewriteFreedom
{
    Minimal,
    Moderate,
    High,
}

public enum RewriteStrategy
{
    MinimalPolish,
    TargetedSentenceRepair,
    FactsFirstReconstruct,
    FullStructureRewrite,
    SupportPolicyOptionsRewrite,
    QuoteListSafeRewrite,
    MessyThreadCleanupRewrite,
    StrongModelRestructure,
    QualityFailure,
}

public enum RewriteModelTier
{
    Standard,
    Strong,
}

public enum RewriteFailureKind
{
    FactLoss,
    UnsupportedFact,
    BrokenNumberedList,
    BrokenQuoteBoundary,
    SentencePerParagraph,
    LineSplitParaphrase,
    SupportMacroVoice,
    MessyThreadLeak,
    QuoteOrListRisk,
    SignalNotImproved,
    LowSignalGotWorse,
    TooGeneric,
    UniformStructure,
    PolicyIntentDrift,
    NoChangeWithoutConfirmationMissing,
}

public enum RewriteFactImportance
{
    Critical,
    Supporting,
    Optional,
}

public enum RewriteFactCategory
{
    Person,
    DateOrDeadline,
    Amount,
    Count,
    Policy,
    Condition,
    NegativeConstraint,
    NextStep,
    SupportAvailability,
    Other,
}

public sealed record RewriteInputAnalysis(
    RewriteScenario Scenario,
    RewriteRiskLevel RiskLevel,
    RewriteFactualDensity FactualDensity,
    RewriteStructureRisk StructureRisk,
    RewriteFreedom RewriteFreedom,
    bool RequiresPolicyCare,
    bool RequiresStructurePreservation,
    RewriteStrategy RecommendedInitialStrategy,
    IReadOnlyList<string> Reasons);

public sealed record RewriteFact(
    string Id,
    string Text,
    string Source,
    RewriteFactImportance Importance,
    RewriteFactCategory Category,
    bool CanBeRephrased,
    string? SourceSpan = null);

public sealed record RewriteFactLedger(IReadOnlyList<RewriteFact> Facts);

public sealed record RewriteStrategyDecision(
    RewriteStrategy Strategy,
    RewriteModelTier ModelTier,
    string Reason);

public sealed record RewriteFailureEvidence(
    IReadOnlyCollection<RewriteFailureKind> FailureKinds,
    IReadOnlyCollection<RewriteStrategy> PreviousStrategies,
    int AttemptsUsed);

public sealed record RewriteBudget(
    int MaxAttempts,
    bool AllowStrongModel,
    int MaxStrongAttempts);

public sealed record RewriteGateResult(
    bool Passed,
    IReadOnlyList<RewriteFailureKind> FailureKinds,
    IReadOnlyList<string> Reasons);

public static class RewriteInputAnalyzer
{
    private static readonly Regex PolicyRegex = new(
        @"\b(refund|credit|charge|invoice|subscription|seat|transfer|enrollment|registration|eligible|eligibility|availability|policy|cancel|cancellation|confirm|confirmation|approval|approve|no change|without confirmation)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ListOrQuoteRegex = new(
        @"(?m)^\s*(?:\d+[.)]|[-*•]|>|"")",
        RegexOptions.Compiled);

    private static readonly Regex MessyThreadRegex = new(
        @"\b(forwarded message|from:|sent:|subject:|wrote:|on .+ wrote)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static RewriteInputAnalysis Analyze(RewriteRequest request)
    {
        var combined = CombineRequestText(request);
        var reasons = new List<string>();
        var wordCount = CountWords(combined);
        var policyMatches = PolicyRegex.Matches(combined).Count;
        var containsListsOrQuotes = ListOrQuoteRegex.IsMatch(combined);
        var messyThread = MessyThreadRegex.IsMatch(combined);

        var requiresPolicyCare = policyMatches > 0;
        var factualDensity = policyMatches >= 4 || CountHighSignalFacts(combined) >= 4
            ? RewriteFactualDensity.High
            : policyMatches >= 2 || CountHighSignalFacts(combined) >= 2
                ? RewriteFactualDensity.Medium
                : RewriteFactualDensity.Low;

        var structureRisk = containsListsOrQuotes || messyThread
            ? RewriteStructureRisk.High
            : requiresPolicyCare || wordCount > 80
                ? RewriteStructureRisk.Medium
                : RewriteStructureRisk.Low;

        RewriteScenario scenario;
        RewriteStrategy strategy;

        if (messyThread)
        {
            scenario = RewriteScenario.MessyThread;
            strategy = RewriteStrategy.MessyThreadCleanupRewrite;
            reasons.Add("Input looks like a pasted thread and needs cleanup before rewriting.");
        }
        else if (containsListsOrQuotes)
        {
            scenario = RewriteScenario.QuoteListHeavy;
            strategy = RewriteStrategy.QuoteListSafeRewrite;
            reasons.Add("Input contains lists or quotes that require structure preservation.");
        }
        else if (requiresPolicyCare)
        {
            scenario = RewriteScenario.SupportPolicy;
            strategy = RewriteStrategy.SupportPolicyOptionsRewrite;
            reasons.Add("Input contains policy or eligibility language that must stay conditional.");
        }
        else if (wordCount <= 35)
        {
            scenario = string.IsNullOrWhiteSpace(request.MessageToReplyTo)
                ? RewriteScenario.DraftOnly
                : RewriteScenario.ShortCasualReply;
            strategy = RewriteStrategy.MinimalPolish;
            reasons.Add("Short low-risk input should receive minimal polishing.");
        }
        else if (wordCount >= 120)
        {
            scenario = RewriteScenario.LongSupportEmail;
            strategy = RewriteStrategy.FullStructureRewrite;
            reasons.Add("Long input should be regrouped into send-ready structure.");
        }
        else
        {
            scenario = string.IsNullOrWhiteSpace(request.MessageToReplyTo)
                ? RewriteScenario.DraftOnly
                : RewriteScenario.NormalEmail;
            strategy = RewriteStrategy.FactsFirstReconstruct;
            reasons.Add("Normal email input should be reconstructed from facts.");
        }

        var riskLevel = requiresPolicyCare || factualDensity == RewriteFactualDensity.High
            ? RewriteRiskLevel.High
            : structureRisk == RewriteStructureRisk.High || factualDensity == RewriteFactualDensity.Medium
                ? RewriteRiskLevel.Medium
                : RewriteRiskLevel.Low;

        var rewriteFreedom = riskLevel == RewriteRiskLevel.High
            ? RewriteFreedom.Moderate
            : wordCount <= 35
                ? RewriteFreedom.Minimal
                : RewriteFreedom.High;

        return new RewriteInputAnalysis(
            scenario,
            riskLevel,
            factualDensity,
            structureRisk,
            rewriteFreedom,
            requiresPolicyCare,
            containsListsOrQuotes || messyThread,
            strategy,
            reasons);
    }

    internal static string CombineRequestText(RewriteRequest request) =>
        string.Join(
            "\n",
            new[]
            {
                request.MessageToReplyTo,
                request.RoughDraftReply,
                request.Audience,
                request.Purpose,
                request.WhatHappened,
                request.FactsToPreserve,
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

    private static int CountWords(string text) =>
        Regex.Matches(text, @"\b[\p{L}\p{N}'-]+\b").Count;

    private static int CountHighSignalFacts(string text)
    {
        var patterns = new[]
        {
            @"\b(?:NZD|USD|AUD)?\s?\$\d+(?:[.,]\d{2})?\b",
            @"\b\d+\b",
            @"\b(?:Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)\b",
            @"\b(?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2}\b",
            @"\b(?:not|no|never|without|unless|cannot|can't|will not|do not)\b",
        };

        return patterns.Sum(pattern => Regex.Matches(text, pattern, RegexOptions.IgnoreCase).Count);
    }
}

public static class FactLedgerExtractor
{
    private static readonly Regex AmountRegex = new(
        @"\b(?:NZD|USD|AUD)?\s?\$\d+(?:[.,]\d{2})?\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DateRegex = new(
        @"\b(?:(?:Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)|(?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CountRegex = new(
        @"\b(?:one|two|three|four|five|six|seven|eight|nine|ten|\d+)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NegativeConstraintRegex = new(
        @"(?<sentence>[^.!?\n]*(?:not|no|never|without confirmation|unless|cannot|can't|will not|do not|does not|did not)[^.!?\n]*[.!?]?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PersonRegex = new(
        @"\b[A-Z][a-z]{2,}\b",
        RegexOptions.Compiled);

    private static readonly HashSet<string> NonNameCapitalizedWords = new(StringComparer.Ordinal)
    {
        "Hi",
        "The",
        "Tell",
        "Preserve",
        "Message",
        "Rough",
        "Audience",
        "Purpose",
        "Facts",
        "Customer",
        "Finance",
        "Friday",
        "Monday",
        "Tuesday",
        "Wednesday",
        "Thursday",
        "Saturday",
        "Sunday",
        "January",
        "February",
        "March",
        "April",
        "May",
        "June",
        "July",
        "August",
        "September",
        "October",
        "November",
        "December",
        "NZD",
        "USD",
        "AUD",
    };

    public static RewriteFactLedger Extract(RewriteRequest request)
    {
        var facts = new List<RewriteFact>();
        ExtractFromSource(facts, "messageToReplyTo", request.MessageToReplyTo);
        ExtractFromSource(facts, "roughDraftReply", request.RoughDraftReply);
        ExtractFromSource(facts, "audience", request.Audience);
        ExtractFromSource(facts, "purpose", request.Purpose);
        ExtractFromSource(facts, "whatHappened", request.WhatHappened);
        ExtractFromSource(facts, "factsToPreserve", request.FactsToPreserve);

        var deduped = facts
            .GroupBy(fact => (fact.Category, Text: fact.Text.Trim()), StringTupleComparer.Instance)
            .Select(group => group.First() with { Id = $"fact_{group.Key.Category.ToString().ToLowerInvariant()}_{facts.IndexOf(group.First()) + 1}" })
            .ToList();

        return new RewriteFactLedger(deduped);
    }

    private static void ExtractFromSource(List<RewriteFact> facts, string source, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        AddMatches(facts, source, text, AmountRegex, RewriteFactCategory.Amount);
        AddMatches(facts, source, text, DateRegex, RewriteFactCategory.DateOrDeadline);
        AddMatches(facts, source, text, CountRegex, RewriteFactCategory.Count);

        foreach (Match match in NegativeConstraintRegex.Matches(text))
        {
            var sentence = match.Groups["sentence"].Value.Trim();
            if (sentence.Length > 0)
            {
                facts.Add(CreateFact(source, sentence, RewriteFactCategory.NegativeConstraint));
            }
        }

        foreach (Match match in PersonRegex.Matches(text))
        {
            var value = match.Value.Trim();
            if (!NonNameCapitalizedWords.Contains(value))
            {
                facts.Add(CreateFact(source, value, RewriteFactCategory.Person));
            }
        }
    }

    private static void AddMatches(
        List<RewriteFact> facts,
        string source,
        string text,
        Regex regex,
        RewriteFactCategory category)
    {
        foreach (Match match in regex.Matches(text))
        {
            facts.Add(CreateFact(source, match.Value.Trim(), category));
        }
    }

    private static RewriteFact CreateFact(string source, string text, RewriteFactCategory category)
    {
        var importance = category is RewriteFactCategory.Person
            or RewriteFactCategory.DateOrDeadline
            or RewriteFactCategory.Amount
            or RewriteFactCategory.Count
            or RewriteFactCategory.Policy
            or RewriteFactCategory.Condition
            or RewriteFactCategory.NegativeConstraint
            ? RewriteFactImportance.Critical
            : RewriteFactImportance.Supporting;

        return new RewriteFact(
            Id: $"fact_{Guid.NewGuid():N}",
            Text: text,
            Source: source,
            Importance: importance,
            Category: category,
            CanBeRephrased: category is not (RewriteFactCategory.Amount or RewriteFactCategory.DateOrDeadline or RewriteFactCategory.Count),
            SourceSpan: text);
    }

    private sealed class StringTupleComparer : IEqualityComparer<(RewriteFactCategory Category, string Text)>
    {
        public static readonly StringTupleComparer Instance = new();

        public bool Equals((RewriteFactCategory Category, string Text) x, (RewriteFactCategory Category, string Text) y) =>
            x.Category == y.Category && string.Equals(x.Text, y.Text, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((RewriteFactCategory Category, string Text) obj) =>
            HashCode.Combine(obj.Category, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Text));
    }
}

public static class RewriteStrategyRouter
{
    public static RewriteStrategyDecision ChooseInitial(RewriteInputAnalysis analysis) =>
        new(
            analysis.RecommendedInitialStrategy,
            analysis.RiskLevel == RewriteRiskLevel.High ? RewriteModelTier.Standard : RewriteModelTier.Standard,
            "Initial strategy selected from input analysis.");

    public static RewriteStrategyDecision ChooseNext(
        RewriteInputAnalysis analysis,
        RewriteFailureEvidence evidence)
    {
        if (evidence.AttemptsUsed >= 10)
        {
            return new RewriteStrategyDecision(
                RewriteStrategy.QualityFailure,
                RewriteModelTier.Standard,
                "Attempt budget exhausted.");
        }

        if (evidence.FailureKinds.Contains(RewriteFailureKind.FactLoss) ||
            evidence.FailureKinds.Contains(RewriteFailureKind.UnsupportedFact) ||
            evidence.FailureKinds.Contains(RewriteFailureKind.PolicyIntentDrift) ||
            evidence.FailureKinds.Contains(RewriteFailureKind.NoChangeWithoutConfirmationMissing))
        {
            return new RewriteStrategyDecision(
                analysis.RequiresPolicyCare ? RewriteStrategy.SupportPolicyOptionsRewrite : RewriteStrategy.FactsFirstReconstruct,
                RewriteModelTier.Standard,
                "Fact or policy failure requires a facts-first policy-safe strategy.");
        }

        if (evidence.FailureKinds.Contains(RewriteFailureKind.BrokenNumberedList) ||
            evidence.FailureKinds.Contains(RewriteFailureKind.BrokenQuoteBoundary) ||
            evidence.FailureKinds.Contains(RewriteFailureKind.SentencePerParagraph) ||
            evidence.FailureKinds.Contains(RewriteFailureKind.LineSplitParaphrase))
        {
            return new RewriteStrategyDecision(
                analysis.RequiresStructurePreservation
                    ? RewriteStrategy.QuoteListSafeRewrite
                    : RewriteStrategy.FullStructureRewrite,
                RewriteModelTier.Standard,
                "Structure failure requires a structure-level rewrite, not sentence repair.");
        }

        if (evidence.FailureKinds.Contains(RewriteFailureKind.SignalNotImproved) ||
            evidence.FailureKinds.Contains(RewriteFailureKind.LowSignalGotWorse))
        {
            return new RewriteStrategyDecision(
                evidence.AttemptsUsed >= 8 ? RewriteStrategy.StrongModelRestructure : RewriteStrategy.TargetedSentenceRepair,
                evidence.AttemptsUsed >= 8 ? RewriteModelTier.Strong : RewriteModelTier.Standard,
                "Naturalness failure can use targeted repair before one strong escalation.");
        }

        return new RewriteStrategyDecision(
            analysis.RecommendedInitialStrategy,
            RewriteModelTier.Standard,
            "Reuse the safest strategy from the input analysis.");
    }
}

public static class RewriteBudgetManager
{
    public static RewriteBudget Create(RewriteInputAnalysis analysis, int requestedMaxAttempts)
    {
        var defaultAttempts = analysis.RiskLevel == RewriteRiskLevel.High ||
            analysis.StructureRisk == RewriteStructureRisk.High
            ? 10
            : 6;

        var maxAttempts = Math.Clamp(
            requestedMaxAttempts <= 0 ? defaultAttempts : Math.Min(defaultAttempts, requestedMaxAttempts),
            1,
            10);

        var allowStrong = analysis.RiskLevel == RewriteRiskLevel.High ||
            analysis.StructureRisk == RewriteStructureRisk.High;

        return new RewriteBudget(maxAttempts, allowStrong, allowStrong ? 1 : 0);
    }
}

public static class RewriteStructureGate
{
    private static readonly Regex DetachedNumberedMarkerRegex = new(
        @"(?m)^\s*\d+[.)]\s*$",
        RegexOptions.Compiled);

    private static readonly Regex ListOrQuoteRegex = new(
        @"(?m)^\s*(?:\d+[.)]|[-*•]|>|"")",
        RegexOptions.Compiled);

    public static RewriteGateResult Check(string candidateText, RewriteInputAnalysis analysis)
    {
        var failureKinds = new List<RewriteFailureKind>();
        var reasons = new List<string>();

        if (DetachedNumberedMarkerRegex.IsMatch(candidateText))
        {
            failureKinds.Add(RewriteFailureKind.BrokenNumberedList);
            reasons.Add("Candidate contains detached numbered-list markers.");
        }

        if (LooksSentencePerParagraph(candidateText))
        {
            failureKinds.Add(RewriteFailureKind.SentencePerParagraph);
            reasons.Add("Candidate overuses one-sentence paragraphs instead of natural grouped structure.");
        }

        if (analysis.RequiresStructurePreservation && HasLikelyBrokenQuoteBoundary(candidateText))
        {
            failureKinds.Add(RewriteFailureKind.BrokenQuoteBoundary);
            reasons.Add("Candidate may have broken quote or list boundaries.");
        }

        return new RewriteGateResult(failureKinds.Count == 0, failureKinds, reasons);
    }

    private static bool LooksSentencePerParagraph(string text)
    {
        var paragraphs = text
            .Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(paragraph => paragraph.Length > 0)
            .Where(paragraph => !Regex.IsMatch(paragraph, @"^\s*\d+[.)]\s*$"))
            .ToList();

        if (paragraphs.Count < 4)
        {
            return false;
        }

        var oneSentenceParagraphs = paragraphs.Count(paragraph =>
            Regex.Matches(paragraph, @"[.!?]").Count <= 1 &&
            Regex.Matches(paragraph, @"\b[\p{L}\p{N}'-]+\b").Count >= 4);

        return oneSentenceParagraphs >= 3;
    }

    private static bool HasLikelyBrokenQuoteBoundary(string text) =>
        ListOrQuoteRegex.IsMatch(text) && DetachedNumberedMarkerRegex.IsMatch(text);
}
