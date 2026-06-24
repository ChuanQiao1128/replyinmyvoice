using System.Text.RegularExpressions;

namespace ReplyInMyVoice.Domain.Quality;

// What kind of boundary the source imposes. Orthogonal to polarity (a RefundLimit can be Negative or
// Conditional). Drives messaging only; the gate enforces Polarity.
public enum BoundaryKind
{
    NegativeConstraint,
    Modality,
    NoPromise,
    PolicyLimit,
    NoAdvice,
    RefundLimit,
    Status,
}

// How the source constrains the claim — the property the rewrite must not flatten.
//   Negative    : a refusal/limit ("cannot add SSO", "no refund is available")
//   Uncertain   : hedged/modal ("the refund may take 5 days", "you might be eligible")
//   Conditional : gated on a condition ("if the box is unopened, we can refund")
public enum BoundaryPolarity
{
    Negative,
    Uncertain,
    Conditional,
}

public sealed record Boundary(string Text, BoundaryKind Kind, BoundaryPolarity Polarity);

public sealed record BoundaryLedger(IReadOnlyList<Boundary> Items)
{
    public static BoundaryLedger Empty { get; } = new(Array.Empty<Boundary>());
}

public sealed record BoundaryGateResult(
    bool Passed,
    IReadOnlyList<string> FlippedBoundaries,
    IReadOnlyList<string> Reasons)
{
    public static BoundaryGateResult Pass { get; } = new(true, Array.Empty<string>(), Array.Empty<string>());
}

// Deterministic boundary-polarity gate. For each source boundary it finds the strongest-overlapping
// candidate sentence (by substantive content tokens, markers excluded) and checks that sentence still
// carries the boundary's polarity marker. If the best-matching sentence dropped the marker, the
// constraint was flattened — cannot -> can, may -> will, "no refund" -> "a refund is available",
// "if unopened, we can refund" -> "we can refund" — and the gate FAILS.
//
// Conservative by design (mirrors the engine's proven certainty-drift check): it only flags a FLIP on a
// strongly-matched sentence, never a bare omission (a boundary with no overlapping sentence is left to
// the fact/LLM gates), and it treats any same-class marker surviving in the matched sentence as
// preserved — so a faithful rephrase ("cannot" -> "unable to", "may" -> "might") never trips it.
public static class BoundaryGate
{
    // Core verbal negations only. "without" is intentionally excluded: it commonly survives a rephrase
    // ("can add it without delay") and would mask a real cannot->can flip in the same sentence.
    private static readonly Regex NegativeMarker = new(
        @"(?:\b(?:not|no|never|none|cannot|can't|cant|won't|wont|will\s+not|do\s+not|does\s+not|did\s+not|don't|dont|doesn't|isn't|aren't|wasn't|weren't|unable|ineligible|unavailable|decline[ds]?|refus(?:e|es|ed|al))\b|n['’]t\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UncertainMarker = new(
        @"(?:\b(?:may|might|could|possibly|perhaps|likely|probably|potentially|tentative(?:ly)?|seems?|appears?|subject\s+to)\b|\blooks?\s+like\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ConditionalMarker = new(
        @"\b(?:if|unless|provided(?:\s+that)?|as\s+long\s+as|only\s+if|contingent\s+on|depending\s+on|in\s+case)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SentenceRegex = new(@"[^.!?\n]+[.!?]?", RegexOptions.Compiled);
    private static readonly Regex TokenRegex = new(@"\b[\p{L}\p{N}'-]+\b", RegexOptions.Compiled);

    // Stopwords + polarity-marker words, excluded from content tokens so overlap is measured on
    // substance (refund/sso/approval/items), not on the markers themselves.
    private static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "but", "of", "to", "for", "in", "on", "at", "by", "with", "from",
        "as", "that", "this", "these", "those", "it", "its", "we", "our", "you", "your", "i", "he", "she",
        "they", "them", "his", "her", "their", "is", "are", "was", "were", "be", "been", "being", "am",
        "have", "has", "had", "will", "would", "shall", "should", "can", "cannot", "could", "may", "might",
        "must", "do", "does", "did", "not", "no", "never", "none", "if", "unless", "once", "provided",
        "without", "unable", "possibly", "perhaps", "likely", "probably", "potentially", "seems", "seem",
        "appear", "appears", "able", "any", "some", "all", "so", "than", "then", "there", "here", "out",
        "up", "down", "into", "about", "over", "under", "again", "new",
    };

    public static BoundaryGateResult Check(string candidateText, BoundaryLedger ledger)
    {
        if (string.IsNullOrWhiteSpace(candidateText) || ledger.Items.Count == 0)
        {
            return BoundaryGateResult.Pass;
        }

        var sentences = SentenceRegex.Matches(candidateText)
            .Select(m => m.Value.Trim())
            .Where(s => s.Length > 0)
            .Select(s => new CandidateSentence(s, ContentTokens(s)))
            .ToArray();

        var flipped = new List<string>();
        var reasons = new List<string>();

        foreach (var boundary in ledger.Items)
        {
            // Conditional flattening ("if the box is unopened, we can refund" -> "we can refund") is
            // deferred to the LLM FidelityJudge. A faithful rewrite routinely re-expresses a condition
            // WITHOUT the literal if/unless marker ("if you want to adjust to 9 seats..." -> "to adjust to
            // 9 seats..."), which the marker-based check cannot tell from a real drop — it false-failed
            // faithful T0 output in the quality audit. The deterministic gate enforces only the robust
            // Negative/Uncertain polarity flips (cannot->can, may->will, "no refund"->refund); those have
            // reliable marker synonym sets and stay false-positive-free.
            if (boundary.Polarity == BoundaryPolarity.Conditional)
            {
                continue;
            }

            var sourceTokens = ContentTokens(boundary.Text);
            if (sourceTokens.Count < 2)
            {
                continue; // too little substance to match a sentence reliably
            }

            CandidateSentence? best = null;
            var bestOverlap = 0;
            foreach (var sentence in sentences)
            {
                var overlap = sourceTokens.Count(sentence.Tokens.Contains);
                if (overlap > bestOverlap)
                {
                    bestOverlap = overlap;
                    best = sentence;
                }
            }

            if (best is null || !IsStrongOverlap(bestOverlap, sourceTokens.Count))
            {
                continue; // topic not clearly present in the rewrite — omission is the fact/LLM gate's job
            }

            if (!MarkerFor(boundary.Polarity).IsMatch(best.Text))
            {
                flipped.Add(boundary.Text);
                reasons.Add(
                    $"Boundary polarity ({boundary.Polarity}) flattened — the matching sentence dropped the "
                    + $"{boundary.Polarity.ToString().ToLowerInvariant()} marker: \"{Truncate(best.Text)}\" "
                    + $"(source: \"{Truncate(boundary.Text)}\").");
            }
        }

        return flipped.Count == 0
            ? BoundaryGateResult.Pass
            : new BoundaryGateResult(false, flipped, reasons);
    }

    // Classifies a sentence/span by its strongest polarity marker (negative beats uncertain beats
    // conditional — a "we cannot refund unless..." line is primarily a refusal). Returns null when no
    // marker is present, i.e. it is not a boundary. Used by BoundaryLedgerExtractor to type conditionals
    // and LLM-augmented spans without duplicating the marker regexes.
    public static BoundaryPolarity? InferPolarity(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (NegativeMarker.IsMatch(text))
        {
            return BoundaryPolarity.Negative;
        }

        if (UncertainMarker.IsMatch(text))
        {
            return BoundaryPolarity.Uncertain;
        }

        return ConditionalMarker.IsMatch(text) ? BoundaryPolarity.Conditional : null;
    }

    private static Regex MarkerFor(BoundaryPolarity polarity) => polarity switch
    {
        BoundaryPolarity.Negative => NegativeMarker,
        BoundaryPolarity.Uncertain => UncertainMarker,
        BoundaryPolarity.Conditional => ConditionalMarker,
        _ => NegativeMarker,
    };

    private static bool IsStrongOverlap(int overlap, int sourceTokenCount) =>
        overlap >= Math.Min(3, sourceTokenCount) &&
        overlap >= Math.Ceiling(sourceTokenCount * 0.5);

    private static HashSet<string> ContentTokens(string value) =>
        TokenRegex.Matches(value.ToLowerInvariant())
            .Select(m => m.Value.Trim('\'', '-'))
            .Where(t => t.Length >= 3 && !Stop.Contains(t))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string Truncate(string text) =>
        text.Length <= 80 ? text : text[..77] + "...";

    private sealed record CandidateSentence(string Text, HashSet<string> Tokens);
}
