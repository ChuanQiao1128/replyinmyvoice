using System.Text.RegularExpressions;
using ReplyInMyVoice.Domain.RewriteEngine;

namespace ReplyInMyVoice.Domain.Quality;

// Proposes business-object / status / action spans that must survive a rewrite verbatim — the terms
// FactLedgerExtractor can't find because they are ordinary nouns (saucer, planter, seat credit, dish
// rack), which is the substitution class the eval judge kept passing. Implemented by an LLM span
// proposer (eval/Infrastructure); the interface lives in Domain so the ledger builder depends on the
// abstraction, not the LLM client. Implementations MUST return only exact substrings of the draft; the
// builder re-validates regardless, so the proposer can never inject an invented term.
public interface IProtectedTermProposer
{
    Task<IReadOnlyList<string>> ProposeAsync(string draft, CancellationToken cancellationToken);
}

// Builds the ProtectedTermLedger that ProtectedTermGate enforces:
//   deterministic anchors (FactLedgerExtractor: names, amounts, dates, identifiers, digit-counts)
//   ∪ validated LLM-proposed business-object spans (each an exact substring of the draft).
// Pure and synchronous at its core (Build) so it is fully unit-testable without an LLM; BuildAsync is a
// thin convenience that fetches spans from an IProtectedTermProposer first.
public static class ProtectedTermLedgerExtractor
{
    // Deterministic protected terms from the fact ledger. Boundary categories (Policy/Condition/
    // NegativeConstraint/NextStep/SupportAvailability/Other) are NOT exact-required terms — they are the
    // BoundaryGate's job (polarity, not verbatim presence) — so they are excluded here.
    public static IReadOnlyList<ProtectedTerm> FromFactLedger(RewriteFactLedger factLedger)
    {
        var terms = new List<ProtectedTerm>();
        foreach (var fact in factLedger.Facts)
        {
            if (MapKind(fact.Category) is not { } kind)
            {
                continue;
            }

            var text = fact.Text.Trim();
            if (text.Length < 2)
            {
                continue;
            }

            // Counts: keep only digit-bearing ones. The gate does not normalize number words, so
            // requiring "two" verbatim would false-positive against a faithful "2".
            if (fact.Category == RewriteFactCategory.Count && !text.Any(char.IsDigit))
            {
                continue;
            }

            terms.Add(new ProtectedTerm(text, kind));
        }

        return Dedup(terms);
    }

    // Full ledger = deterministic fact-ledger terms ∪ validated proposed spans. Each proposed span must
    // be an EXACT substring of the draft (so the proposer cannot invent terms); invalid spans are
    // dropped. A proposed span equal (case-insensitively) to a term already present is not duplicated.
    public static ProtectedTermLedger Build(
        string draft,
        RewriteFactLedger factLedger,
        IReadOnlyList<string> proposedSpans,
        IReadOnlyList<string>? loadBearingSpans = null)
    {
        var result = new List<ProtectedTerm>(FromFactLedger(factLedger));
        var seen = new HashSet<string>(result.Select(t => t.Text), StringComparer.OrdinalIgnoreCase);

        // HARD load-bearing phrases (e.g. "expires June 7", "reply by June 7") — verbatim required, so the
        // deterministic gate catches translation drift like "expires June 7" -> "is very good through June 7".
        foreach (var raw in loadBearingSpans ?? Array.Empty<string>())
        {
            var span = (raw ?? string.Empty).Trim();
            if (span.Length < 3 || string.IsNullOrEmpty(draft))
            {
                continue;
            }

            if (!draft.Contains(span, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (seen.Add(span))
            {
                result.Add(new ProtectedTerm(span, ProtectedTermKind.StatusPhrase));
            }
        }

        foreach (var raw in proposedSpans ?? Array.Empty<string>())
        {
            var span = (raw ?? string.Empty).Trim();
            if (span.Length < 2)
            {
                continue;
            }

            // Must be a real span of the draft — the proposer points at text, it does not author it.
            if (string.IsNullOrEmpty(draft) || !draft.Contains(span, StringComparison.Ordinal))
            {
                continue;
            }

            if (seen.Add(span))
            {
                // Soft (ExactRequired: false): the deterministic gate does NOT verbatim-enforce a
                // fuzzy LLM-proposed multi-word span (T0 legitimately reorders/drops a possessive/rephrases
                // — that caused false positives in the T0 audit). Object substitution on these is the
                // hardened FidelityJudge's job (rule 4). The deterministic gate verbatim-enforces only the
                // high-precision fact-ledger anchors (IDs / amounts / dates / names).
                result.Add(new ProtectedTerm(span, ProtectedTermKind.BusinessObject, ExactRequired: false));
            }
        }

        // Acronyms (SSO, API, SLA, EST, ...) are specific terms a rewrite must NOT paraphrase or drop —
        // ExactRequired. This catches the object/term drift the LLM judge kept missing, e.g. the loop's
        // "advanced SSO setup" -> "advanced Settings" (SSO dropped).
        foreach (Match match in AcronymRegex.Matches(draft ?? string.Empty))
        {
            var acronym = match.Value;
            if (!NonAcronyms.Contains(acronym) && seen.Add(acronym))
            {
                result.Add(new ProtectedTerm(acronym, ProtectedTermKind.ProperName));
            }
        }

        return new ProtectedTermLedger(result);
    }

    // All-caps tokens of 2–6 letters: acronyms/initialisms (SSO, API, SLA, NDA, CEO, EST). Common
    // non-fact all-caps words are excluded to avoid false positives.
    private static readonly Regex AcronymRegex = new(@"\b[A-Z]{2,6}\b", RegexOptions.Compiled);

    private static readonly HashSet<string> NonAcronyms = new(StringComparer.Ordinal)
    {
        "OK", "AM", "PM", "OKAY", "FYI", "ASAP", "FAQ", "RE", "FW", "PS",
    };

    public static async Task<ProtectedTermLedger> BuildAsync(
        string draft,
        RewriteFactLedger factLedger,
        IProtectedTermProposer proposer,
        CancellationToken cancellationToken)
    {
        var spans = await proposer.ProposeAsync(draft, cancellationToken);
        return Build(draft, factLedger, spans);
    }

    private static ProtectedTermKind? MapKind(RewriteFactCategory category) => category switch
    {
        RewriteFactCategory.Person => ProtectedTermKind.ProperName,
        RewriteFactCategory.DateOrDeadline => ProtectedTermKind.DateTime,
        RewriteFactCategory.Amount => ProtectedTermKind.Amount,
        RewriteFactCategory.Identifier => ProtectedTermKind.Identifier,
        RewriteFactCategory.Count => ProtectedTermKind.Amount,
        _ => null,
    };

    private static IReadOnlyList<ProtectedTerm> Dedup(IEnumerable<ProtectedTerm> terms)
    {
        var seen = new HashSet<(ProtectedTermKind, string)>();
        var result = new List<ProtectedTerm>();
        foreach (var term in terms)
        {
            if (seen.Add((term.Kind, term.Text.ToLowerInvariant())))
            {
                result.Add(term);
            }
        }

        return result;
    }
}
