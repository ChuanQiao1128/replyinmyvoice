using System.Text.RegularExpressions;
using ReplyInMyVoice.Domain.RewriteEngine;

namespace ReplyInMyVoice.Domain.Quality;

// Adds boundary sentences the deterministic rules miss (e.g. an implicit policy limit phrased without a
// hard marker). Rules-first: the augmenter may only ADD candidate spans, never override; each span must
// be an exact substring of the draft and must itself carry a polarity marker, or it is dropped. Mirrors
// IProtectedTermProposer. Implemented by an LLM augmenter (eval/Infrastructure).
public interface IBoundaryAugmenter
{
    Task<IReadOnlyList<string>> ProposeBoundariesAsync(string draft, CancellationToken cancellationToken);
}

// Builds the BoundaryLedger that BoundaryGate enforces:
//   FactLedgerExtractor NegativeConstraint -> Negative, Condition -> Uncertain (deterministic)
//   ∪ conditional sentences in the draft (if/unless/provided/as long as...) -> Conditional
//   ∪ validated LLM-augmented spans (exact substring + carries a marker), polarity inferred.
// Pure core (Build) is fully unit-testable without an LLM; BuildAsync is a thin convenience.
public static class BoundaryLedgerExtractor
{
    private static readonly Regex SentenceRegex = new(@"[^.!?\n]+[.!?]?", RegexOptions.Compiled);

    // Soft first-person volition ("I do not want any of them to get lost") is a conversational
    // preference, not a policy/capability constraint. A faithful rewrite can state it affirmatively
    // ("I want to make sure each is handled") without the negation, so enforcing the negative marker
    // there false-fails faithful output. Hard refusals ("I cannot waive...", "we will not ship...")
    // do not match this pattern and stay enforced.
    private static readonly Regex SoftVolitionalNegative = new(
        @"\b(?:i|we)\s+(?:do\s+not|don['’]?t|would\s+not|wouldn['’]?t|did\s+not|didn['’]?t)\s+(?:want|wish|like|think|feel|intend|mean|expect)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<Boundary> FromFactLedger(RewriteFactLedger factLedger)
    {
        var items = new List<Boundary>();
        foreach (var fact in factLedger.Facts)
        {
            if (fact.Category is not (RewriteFactCategory.NegativeConstraint or RewriteFactCategory.Condition))
            {
                continue;
            }

            var text = fact.Text.Trim();
            if (text.Length < 3)
            {
                continue;
            }

            // Re-validate (and re-derive polarity) with WORD-BOUNDED markers. FactLedgerExtractor's
            // NegativeConstraint regex matches "no"/"not" as SUBSTRINGS — it pulls in "Northstar", "notes",
            // "noon", "nothing", greetings — which are not boundaries and caused an 8/10 false-positive
            // rate in the T0 audit. InferPolarity uses \bno\b / \bnot\b etc., so those are dropped here.
            if (BoundaryGate.InferPolarity(text) is { } polarity)
            {
                // Drop soft volitional negatives — not a hard boundary; a faithful affirmative rephrase
                // would otherwise false-fail as a polarity flip.
                if (polarity == BoundaryPolarity.Negative && SoftVolitionalNegative.IsMatch(text))
                {
                    continue;
                }

                items.Add(new Boundary(text, KindFor(polarity), polarity));
            }
        }

        return Dedup(items);
    }

    public static BoundaryLedger Build(
        string draft,
        RewriteFactLedger factLedger,
        IReadOnlyList<string>? augmentedSpans = null)
    {
        var items = new List<Boundary>(FromFactLedger(factLedger));

        // Conditional sentences (if/unless/...) are not a FactLedger category — pick them up from the draft.
        if (!string.IsNullOrWhiteSpace(draft))
        {
            foreach (Match match in SentenceRegex.Matches(draft))
            {
                var sentence = match.Value.Trim();
                if (sentence.Length >= 3 && BoundaryGate.InferPolarity(sentence) == BoundaryPolarity.Conditional)
                {
                    items.Add(new Boundary(sentence, BoundaryKind.PolicyLimit, BoundaryPolarity.Conditional));
                }
            }
        }

        // LLM-augmented spans: exact substring of the draft AND carries a polarity marker, else dropped.
        foreach (var raw in augmentedSpans ?? Array.Empty<string>())
        {
            var span = (raw ?? string.Empty).Trim();
            if (span.Length < 3 || string.IsNullOrEmpty(draft) || !draft.Contains(span, StringComparison.Ordinal))
            {
                continue;
            }

            if (BoundaryGate.InferPolarity(span) is { } polarity)
            {
                items.Add(new Boundary(span, KindFor(polarity), polarity));
            }
        }

        return new BoundaryLedger(Dedup(items));
    }

    public static async Task<BoundaryLedger> BuildAsync(
        string draft,
        RewriteFactLedger factLedger,
        IBoundaryAugmenter augmenter,
        CancellationToken cancellationToken)
    {
        var spans = await augmenter.ProposeBoundariesAsync(draft, cancellationToken);
        return Build(draft, factLedger, spans);
    }

    private static BoundaryKind KindFor(BoundaryPolarity polarity) => polarity switch
    {
        BoundaryPolarity.Negative => BoundaryKind.NegativeConstraint,
        BoundaryPolarity.Uncertain => BoundaryKind.Modality,
        BoundaryPolarity.Conditional => BoundaryKind.PolicyLimit,
        _ => BoundaryKind.Status,
    };

    private static IReadOnlyList<Boundary> Dedup(IEnumerable<Boundary> items)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<Boundary>();
        foreach (var item in items)
        {
            if (seen.Add(Regex.Replace(item.Text.ToLowerInvariant(), @"\s+", " ").Trim()))
            {
                result.Add(item);
            }
        }

        return result;
    }
}
