using System.Text.RegularExpressions;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Infrastructure.Providers;

// Testable eval-harness logic for the C# rewrite eval tool. Types are public so
// backend-dotnet/tests/ReplyInMyVoice.Tests can exercise the parser and scoring
// without invoking real providers (zero spend). Scoring runs over saved outputs,
// so fact / forbidden / naturalness can be re-scored offline ($0 engine cost).

/// <summary>
/// One eval case parsed from the section-format corpus
/// (docs/rewrite-email-eval-cases-100.md). Mirrors the TS parser schema in
/// lib/rewrite-eval-cases.ts: inline fields + #### sections.
/// </summary>
public sealed record EvalCase(
    int CaseNumber,
    string Title,
    string Id,
    string Category,
    string SourceType,
    string TonePreset,
    string InputWordCountBand,
    string InputDraft,
    string WhatHappened,
    IReadOnlyList<string> MustKeep,
    IReadOnlyList<string> MustNotClaim,
    string RewriteQualityTargets,
    string ExpectedRewriteChallenges)
{
    /// <summary>
    /// Faithful production input: the live flow rewrites from the draft only
    /// (corpus contract — "engine sees only input_draft, every must_keep anchor is
    /// in it"). We deliberately leave MessageToReplyTo/Audience/Purpose/WhatHappened/
    /// FactsToPreserve null so the eval does not hand the engine ground-truth facts it
    /// never receives in production (RewriteEngineCore extracts facts from every
    /// populated request field).
    /// </summary>
    public RewriteRequest ToRewriteRequest() =>
        new(
            MessageToReplyTo: null,
            RoughDraftReply: InputDraft,
            Audience: null,
            Purpose: null,
            WhatHappened: null,
            FactsToPreserve: null,
            Tone: NormalizeTone(TonePreset));

    private static string NormalizeTone(string tonePreset) =>
        tonePreset.Trim().ToLowerInvariant() == "direct" ? "direct" : "warm";
}

/// <summary>Parses the section-format corpus into <see cref="EvalCase"/> records.</summary>
public static class EvalCaseParser
{
    private static readonly Regex HeaderRegex =
        new(@"^### Case (?<number>\d{3}) - (?<title>.+)$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex SectionRegex =
        new(@"^#### (?<name>[a-z_]+)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex InlineFieldRegex =
        new(@"^- (?<name>[a-z_]+):\s*(?<value>.*)$", RegexOptions.Compiled);

    public static IReadOnlyList<EvalCase> Parse(string markdown)
    {
        var headers = HeaderRegex.Matches(markdown).ToArray();
        var cases = new List<EvalCase>(headers.Length);
        for (var i = 0; i < headers.Length; i++)
        {
            var header = headers[i];
            var blockStart = header.Index + header.Length;
            var blockEnd = i + 1 < headers.Length ? headers[i + 1].Index : markdown.Length;
            var block = markdown[blockStart..blockEnd];
            var number = int.Parse(header.Groups["number"].Value);

            var sections = ParseSections(block);
            var firstSection = SectionRegex.Match(block);
            var preamble = firstSection.Success ? block[..firstSection.Index] : block;
            var inline = ParseInlineFields(preamble);

            cases.Add(new EvalCase(
                number,
                header.Groups["title"].Value.Trim(),
                RequireInline(inline, "id", number),
                RequireInline(inline, "category", number),
                RequireInline(inline, "source_type", number),
                RequireInline(inline, "tone_preset", number),
                RequireInline(inline, "input_word_count_band", number),
                RequireText(sections, "input_draft", number),
                RequireText(sections, "what_actually_happened", number),
                RequireList(sections, "must_keep", number),
                RequireList(sections, "must_not_claim", number),
                RequireText(sections, "rewrite_quality_targets", number),
                RequireText(sections, "expected_rewrite_challenges", number)));
        }

        return cases;
    }

    private static Dictionary<string, string> ParseSections(string block)
    {
        var matches = SectionRegex.Matches(block).ToArray();
        var sections = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < matches.Length; i++)
        {
            var name = matches[i].Groups["name"].Value;
            var start = matches[i].Index + matches[i].Length;
            var end = i + 1 < matches.Length ? matches[i + 1].Index : block.Length;
            sections[name] = block[start..end].Trim();
        }

        return sections;
    }

    private static Dictionary<string, string> ParseInlineFields(string preamble)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rawLine in preamble.Split('\n'))
        {
            var match = InlineFieldRegex.Match(rawLine.TrimEnd('\r'));
            if (match.Success)
            {
                fields[match.Groups["name"].Value] = match.Groups["value"].Value.Trim();
            }
        }

        return fields;
    }

    private static string RequireInline(IReadOnlyDictionary<string, string> fields, string name, int number) =>
        fields.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Case {number:000} is missing inline field {name}.");

    private static string RequireText(IReadOnlyDictionary<string, string> sections, string name, int number) =>
        sections.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Case {number:000} is missing section #### {name}.");

    private static IReadOnlyList<string> RequireList(
        IReadOnlyDictionary<string, string> sections,
        string name,
        int number)
    {
        if (!sections.TryGetValue(name, out var body) || string.IsNullOrWhiteSpace(body))
        {
            throw new InvalidOperationException($"Case {number:000} is missing section #### {name}.");
        }

        var items = body
            .Split('\n')
            .Select(line => line.TrimEnd('\r').Trim())
            .Where(line => line.StartsWith("- ", StringComparison.Ordinal))
            .Select(line => line[2..].Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        if (items.Length == 0)
        {
            throw new InvalidOperationException($"Case {number:000} has an empty #### {name} list.");
        }

        return items;
    }
}

/// <summary>
/// Eval-side expectations. must_keep / must_not_claim are reference checks against the
/// output — they are NEVER fed to the engine (see <see cref="EvalCase.ToRewriteRequest"/>).
/// </summary>
public sealed record EvalExpectations(IReadOnlyList<string> MustKeep, IReadOnlyList<string> MustNotClaim)
{
    public static EvalExpectations FromCase(EvalCase sample) => new(sample.MustKeep, sample.MustNotClaim);
}

/// <summary>
/// Deterministic, intentionally HIGH-PRECISION screen for must_not_claim violations.
/// It only flags clear, un-negated lexical assertions (refund / guarantee / discount or
/// waiver / confirmation or approval). Constraints it cannot judge lexically (e.g. "do
/// not merge the two issues", numeric bounds) are ABSTAINED and reported separately —
/// they are NOT counted as violations. Bias is toward precision (avoid false fails); an
/// LLM judge can re-score the saved rewrite text offline for full recall.
/// </summary>
public static class ForbiddenClaimScreen
{
    public sealed record Result(IReadOnlyList<string> Violations, IReadOnlyList<string> Abstained);

    private static readonly (string ClaimKeyword, string[] AssertionMarkers)[] Categories =
    {
        ("refund", new[] { "refund" }),
        ("guarantee", new[] { "guarantee", "guaranteed", "warranty", "we promise", "i promise" }),
        ("discount", new[]
        {
            "discount", "% off", "percent off", "waive", "waiver", "for free",
            "no charge", "no cost", "free of charge",
        }),
        // Status-confirmation phrases only. Short verbs like "we approve"/"i confirm" are
        // dropped — they cross-match unrelated text ("we approved the scope") and the
        // forbidden screen cannot tell which subject is being confirmed.
        ("confirm", new[]
        {
            "is confirmed", "are confirmed", "has been confirmed", "have been confirmed",
        }),
    };

    private static readonly char[] SentenceEnders = { '.', '!', '?', '\n', ';' };

    // A forbidden assertion only counts as a violation when stated unconditionally. These
    // markers hedge it — negation ("cannot refund"), conditionality ("if confirmed",
    // "whether ... or", "once we receive", "until review"), or partiality ("partial/prorated
    // refund") — so a sentence containing one is NOT a forbidden promise. "may" is
    // deliberately excluded because it collides with the month name "May".
    private static readonly HashSet<string> NonCommitmentMarkers = new(StringComparer.Ordinal)
    {
        "cannot", "can't", "cant", "not", "no", "never", "unable", "won't", "wont",
        "don't", "dont", "doesn't", "doesnt", "didn't", "didnt", "isn't", "isnt",
        "aren't", "arent", "wasn't", "hasn't", "hasnt", "haven't", "havent",
        "wouldn't", "couldn't", "shouldn't", "without", "instead", "decline",
        "declined", "unfortunately", "rather", "unless", "before", "until", "once",
        // Conditional / hypothetical / partial hedges (clear conditional-refund false
        // positives like "if confirmed we will refund", "whether ... or a full refund").
        "if", "whether", "would", "might", "could", "pending", "depending", "provided",
        "partial", "prorated", "goodwill", "potentially", "possibly",
    };

    // A completed past action ("the $22.50 refund was issued on June 6") reports a fact, not
    // a forward promise, so it is hedged too.
    private static readonly Regex CompletedActionRegex = new(
        @"\b(was|were|already|has been|have been|had been)\b[^.;!?]{0,40}\b(issued|processed|refunded|credited|applied|sent|completed|posted)\b",
        RegexOptions.Compiled);

    public static Result Check(string outputText, IReadOnlyList<string> mustNotClaim)
    {
        var text = Normalize(outputText);
        var violations = new List<string>();
        var abstained = new List<string>();

        foreach (var claim in mustNotClaim)
        {
            var normalizedClaim = Normalize(claim);
            var category = Categories.FirstOrDefault(c =>
                normalizedClaim.Contains(c.ClaimKeyword, StringComparison.Ordinal));
            if (category.AssertionMarkers is null)
            {
                abstained.Add(claim);
                continue;
            }

            if (category.AssertionMarkers.Any(marker => ContainsUnhedgedAssertion(text, marker)))
            {
                violations.Add(claim);
            }
        }

        return new Result(violations, abstained);
    }

    private static bool ContainsUnhedgedAssertion(string text, string marker)
    {
        var index = 0;
        while ((index = text.IndexOf(marker, index, StringComparison.Ordinal)) >= 0)
        {
            // Examine the whole sentence containing the marker (not a fixed window) so
            // policy-careful hedges like "I cannot issue a cash refund ... not a direct
            // refund" or "if confirmed we will refund" are recognized.
            if (!IsHedged(SentenceAround(text, index)))
            {
                return true;
            }

            index += marker.Length;
        }

        return false;
    }

    // Sentence containing the index, with a decimal point treated as part of a number rather
    // than a sentence ender. Without this, "a full $74.00 refund" splits at the "." in 74.00,
    // stranding "00 refund" away from its "cannot promise" hedge -> false positive.
    private static string SentenceAround(string text, int index)
    {
        index = Math.Min(index, text.Length - 1);
        var start = index;
        while (start > 0 && !IsSentenceBoundary(text, start - 1))
        {
            start--;
        }

        var end = index;
        while (end < text.Length && !IsSentenceBoundary(text, end))
        {
            end++;
        }

        return text[start..end];
    }

    private static bool IsSentenceBoundary(string text, int i)
    {
        var c = text[i];
        if (c == '.' && i > 0 && i + 1 < text.Length
            && char.IsDigit(text[i - 1]) && char.IsDigit(text[i + 1]))
        {
            return false; // decimal point inside a number, e.g. 74.00
        }

        return Array.IndexOf(SentenceEnders, c) >= 0;
    }

    private static bool IsHedged(string sentence) =>
        Regex.Split(sentence, @"[^a-z']+").Any(token => NonCommitmentMarkers.Contains(token))
        || CompletedActionRegex.IsMatch(sentence);

    private static string Normalize(string value) =>
        Regex.Replace(value.ToLowerInvariant().Replace('’', '\''), @"\s+", " ").Trim();
}

/// <summary>Composes the customer-usable verdict from the independent gate signals.</summary>
public static class CustomerUsableEvaluator
{
    /// <summary>
    /// customer_usable = engine returned output AND all must_keep preserved AND the engine
    /// succeeded (its own gates, incl. its naturalness rule) AND no forbidden violation
    /// detected by the deterministic screen.
    /// </summary>
    public static bool IsCustomerUsable(
        bool engineSuccess,
        bool hasOutput,
        bool factsPreserved,
        int forbiddenViolationCount) =>
        engineSuccess && hasOutput && factsPreserved && forbiddenViolationCount == 0;
}

/// <summary>
/// Quantifies the C# naturalness gate-rule divergence (it still enforces the old
/// rewrite&lt;=draft punishment that TS removed). For a case the engine failed on
/// naturalness, returns true if any attempt produced a candidate that WOULD pass the TS
/// relaxed rule (rewrite median &lt;= threshold) while preserving facts and staying
/// forbidden-clean — i.e. the failure is the gate rule, not engine writing quality.
/// </summary>
public static class RelaxedNaturalnessProbe
{
    public static bool WouldPassUnderRelaxedGate(
        IReadOnlyList<RewriteAttemptHistoryItem> attemptHistory,
        IReadOnlyList<string> mustKeep,
        IReadOnlyList<string> mustNotClaim,
        int naturalnessThreshold)
    {
        foreach (var attempt in attemptHistory)
        {
            if (attempt.RewriteAiLikePercent is not { } percent || percent > naturalnessThreshold)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(attempt.CandidateText))
            {
                continue;
            }

            if (!FactExpectationChecker.Check(attempt.CandidateText, mustKeep).Passed)
            {
                continue;
            }

            if (ForbiddenClaimScreen.Check(attempt.CandidateText, mustNotClaim).Violations.Count == 0)
            {
                return true;
            }
        }

        return false;
    }
}
