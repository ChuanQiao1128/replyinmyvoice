using System.Text.RegularExpressions;

namespace ReplyInMyVoice.Domain.Quality;

public enum SendabilityTier
{
    Sendable,
    Minor,       // slightly awkward but a busy professional could send it as-is
    Unsendable,
}

public sealed record SendabilityIssue(string Kind, string Detail);

public sealed record SendabilityGateResult(SendabilityTier Tier, IReadOnlyList<SendabilityIssue> Issues)
{
    // Hard gate: only Unsendable blocks. Minor is allowed through (sendable-with-slight-awkwardness).
    public bool Passed => Tier != SendabilityTier.Unsendable;

    public static SendabilityGateResult Sendable { get; } =
        new(SendabilityTier.Sendable, Array.Empty<SendabilityIssue>());
}

// LLM sendability judge (tier sendable/minor/unsendable). Owns the SEMANTIC calls the deterministic gate
// can't make — agent-action errors ("I am unable to get a full refund" when the sender is the one who
// processes it), subtle awkwardness, a nonsensical-but-well-formed sign-off. Implemented in
// eval/Infrastructure; the interface lives in Domain so the gate chain depends on the abstraction.
public interface ISendabilityJudge
{
    Task<SendabilityGateResult> JudgeAsync(string text, CancellationToken cancellationToken);
}

// Deterministic structural sendability gate. Catches the mechanical defects that MT round-trips and
// template fills reliably produce and that should never need an LLM to spot: leftover placeholders /
// masking sentinels, garble (replacement-char mojibake, leftover CJK in an English reply, a word
// repeated 3+ times in a row), and an essentially empty body. Any of these = Unsendable.
//
// Intentionally conservative to stay FP-free: a bare "Best," sign-off with no name is fine (the engine
// is told to close simply without inventing a name), citation-style "[1]" is fine, and ordinary text is
// Sendable. Subtle awkwardness, agent-action errors, and the Minor tier are the ISendabilityJudge's job.
public static class SendabilityGate
{
    // [[A0]] / [A0] / (A0) masking sentinels (Youdao mangles [[A0]] -> (A0)) and bracket-free QZAN000QZ
    // sentinels (tolerant of inserted spaces).
    private static readonly Regex SentinelResidueRegex = new(
        @"\[\[\s*[A-Za-z]?\s*\d+\s*\]\]|\[\s*[A-Za-z]\s*\d+\s*\]|\(\s*[A-Za-z]\s*\d+\s*\)|Q\s*Z\s*A\s*N\s*\d+\s*Q\s*Z",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Unfilled template slots: {date1}, {invoice_id}, {name}.
    private static readonly Regex TemplateSlotRegex = new(
        @"\{[A-Za-z][A-Za-z0-9_]*\}",
        RegexOptions.Compiled);

    // Bracketed placeholder labels: [Name], [Your Name], [insert date], [COMPANY], [xxx].
    private static readonly Regex PlaceholderLabelRegex = new(
        @"\[[^\]\n]*\b(?:name|date|amount|company|title|role|recipient|sender|address|phone|email|insert|placeholder|your\s+\w+|x{2,})\b[^\]\n]*\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Leftover CJK/Japanese/Korean script in an English reply — a classic MT-leak garble signal.
    private static readonly Regex CjkRegex = new(
        @"[\p{IsCJKUnifiedIdeographs}\p{IsHiragana}\p{IsKatakana}\p{IsHangulSyllables}]",
        RegexOptions.Compiled);

    // The same word repeated 3+ times in a row ("the the the"): garble, not a real doubling like "had had".
    private static readonly Regex RepeatedTokenRegex = new(
        @"\b(\p{L}{2,})(?:\s+\1){2,}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex WordRegex = new(@"\b[\p{L}\p{N}'-]+\b", RegexOptions.Compiled);

    public static SendabilityGateResult Check(string candidateText)
    {
        var issues = new List<SendabilityIssue>();

        if (string.IsNullOrWhiteSpace(candidateText) || WordRegex.Matches(candidateText).Count < 3)
        {
            issues.Add(new SendabilityIssue("empty", "Output is empty or too short to send."));
            return new SendabilityGateResult(SendabilityTier.Unsendable, issues);
        }

        AddIfMatch(issues, SentinelResidueRegex, candidateText, "sentinel_residue", "Leftover masking sentinel (e.g. [[A0]] / QZAN000QZ).");
        AddIfMatch(issues, TemplateSlotRegex, candidateText, "unfilled_slot", "Unfilled template slot (e.g. {date}).");
        AddIfMatch(issues, PlaceholderLabelRegex, candidateText, "placeholder_label", "Unfilled bracketed placeholder (e.g. [Name]).");

        if (candidateText.Contains('�'))
        {
            issues.Add(new SendabilityIssue("mojibake", "Contains replacement-character mojibake (�)."));
        }

        AddIfMatch(issues, CjkRegex, candidateText, "cjk_leak", "Contains leftover CJK/JP/KR characters in an English reply.");
        AddIfMatch(issues, RepeatedTokenRegex, candidateText, "repeated_token", "A word is repeated 3+ times in a row (garble).");

        return issues.Count == 0
            ? SendabilityGateResult.Sendable
            : new SendabilityGateResult(SendabilityTier.Unsendable, issues);
    }

    // Maps the LLM judge's string tier to the enum (anything unrecognized = Unsendable, fail-closed).
    public static SendabilityTier ParseTier(string? tier) =>
        (tier ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "sendable" => SendabilityTier.Sendable,
            "minor" => SendabilityTier.Minor,
            _ => SendabilityTier.Unsendable,
        };

    private static void AddIfMatch(List<SendabilityIssue> issues, Regex regex, string text, string kind, string detail)
    {
        if (regex.IsMatch(text))
        {
            issues.Add(new SendabilityIssue(kind, detail));
        }
    }
}
