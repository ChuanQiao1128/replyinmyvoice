using System.Text.RegularExpressions;

namespace ReplyInMyVoice.Domain.Quality;

// Kind of a protected term. Drives how the gate matches it: Identifier matches on its >=3-digit core
// (so a faithful reformat keeps it), everything else matches as a word-bounded phrase.
public enum ProtectedTermKind
{
    BusinessObject,
    Identifier,
    Amount,
    DateTime,
    Contact,
    ProperName,
    StatusPhrase,
    ActionPhrase,
}

// A term from the source draft that a faithful rewrite must not drop or swap for a different thing.
// ExactRequired terms are enforced deterministically by ProtectedTermGate; non-exact terms are left to
// the (LLM) FidelityJudge, which can accept a genuine paraphrase.
public sealed record ProtectedTerm(string Text, ProtectedTermKind Kind, bool ExactRequired = true);

public sealed record ProtectedTermLedger(IReadOnlyList<ProtectedTerm> Terms)
{
    public static ProtectedTermLedger Empty { get; } = new(Array.Empty<ProtectedTerm>());
}

public sealed record ProtectedTermGateResult(
    bool Passed,
    IReadOnlyList<string> DriftedTerms,
    IReadOnlyList<string> Reasons)
{
    public static ProtectedTermGateResult Pass { get; } = new(true, Array.Empty<string>(), Array.Empty<string>());
}

// Deterministic protected-term fidelity gate. Each ExactRequired protected term must survive in the
// candidate — verbatim (whitespace/comma/case-normalized) for objects/names/phrases, or as its
// >=3-digit core for identifiers. An absent exact-required term is a drift: it was dropped or swapped
// for a DIFFERENT object/name. This is exactly the error class the LLM judge kept passing in the eval —
// seat credit -> letter of credit, planter -> flowerpot, saucer -> tea tray, Celestine -> Celeste,
// dish rack -> draining rack — so the gate makes those fail without an API call.
//
// FP-free on faithful reformatting: an identifier keeps its digit run ("INV-8842" -> "invoice 8842"
// passes), an amount survives comma normalization ("$1,250.00" -> "$1,250.00"/"$1250.00"), and an
// object/name preserved verbatim passes. The gate intentionally does NOT try to catch role/subject
// swaps (both names present, e.g. Mark<->Dana) or action-authority drift (process->apply for) — those
// are not term substitutions and are the FidelityJudge's job.
public static class ProtectedTermGate
{
    private static readonly Regex DigitRunRegex = new(@"\d{3,}", RegexOptions.Compiled);

    public static ProtectedTermGateResult Check(string candidateText, ProtectedTermLedger ledger)
    {
        if (string.IsNullOrWhiteSpace(candidateText) || ledger.Terms.Count == 0)
        {
            return ProtectedTermGateResult.Pass;
        }

        var normalizedCandidate = Normalize(candidateText);
        var drifted = new List<string>();
        var reasons = new List<string>();

        foreach (var term in ledger.Terms)
        {
            if (!term.ExactRequired || string.IsNullOrWhiteSpace(term.Text))
            {
                continue;
            }

            if (!IsPreserved(candidateText, normalizedCandidate, term))
            {
                drifted.Add(term.Text);
                reasons.Add($"Protected {term.Kind} term not preserved (dropped or substituted): {term.Text}.");
            }
        }

        return drifted.Count == 0
            ? ProtectedTermGateResult.Pass
            : new ProtectedTermGateResult(false, drifted, reasons);
    }

    private static bool IsPreserved(string candidateRaw, string normalizedCandidate, ProtectedTerm term)
    {
        if (term.Kind == ProtectedTermKind.Identifier)
        {
            var cores = DigitRunRegex.Matches(term.Text).Select(m => m.Value).ToArray();
            if (cores.Length > 0)
            {
                // Each >=3-digit core must appear digit-bounded, so "8842" is not satisfied by "88420".
                return cores.All(core => Regex.IsMatch(candidateRaw, $@"(?<!\d){Regex.Escape(core)}(?!\d)"));
            }

            // No long digit core (e.g. a short code): fall through to phrase matching.
        }

        var normalizedTerm = Normalize(term.Text);
        if (normalizedTerm.Length == 0)
        {
            return true;
        }

        // Word-bounded, case-insensitive, whitespace/comma-normalized phrase match. Boundaries use
        // letter/number so punctuation-led terms ("$1250.00") and multi-word phrases ("seat credit")
        // match cleanly without firing inside a larger word.
        return Regex.IsMatch(
            normalizedCandidate,
            $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(normalizedTerm)}(?![\p{{L}}\p{{N}}])");
    }

    private static string Normalize(string value)
    {
        var lower = value.ToLowerInvariant();
        lower = Regex.Replace(lower, @"(?<=\d),(?=\d)", string.Empty); // 1,250 -> 1250
        return Regex.Replace(lower, @"\s+", " ").Trim();
    }
}
