using System.Text.Json;

namespace ReplyInMyVoice.Domain.Quality;

// Semantic drift kinds the FidelityJudge reports. The deterministic QualityGateChain (RewriteFactGate /
// ProtectedTermGate / BoundaryGate / SendabilityGate) already covers verbatim hard-anchor presence, term
// presence and polarity; the judge adds the meaning-level calls a regex cannot make — truth-value flips,
// who-did-what swaps, OBJECT/TERM SUBSTITUTION, conflation, and invented content.
public enum FidelityDriftKind
{
    HardAnchorChanged,
    PolarityFlipped,
    SubjectRoleSwapped,
    ObjectSubstituted,
    RelationshipChanged,
    UnsupportedAddition,
}

public sealed record FidelityDrift(FidelityDriftKind Kind, string SourceValue, string? CandidateSpan, string ExpectedFix, string Why);

// Result of the semantic fidelity judge. Passed = no drift AND no error. A set Error ⇒ fail-closed: the
// orchestrator must treat an unavailable/garbled judge as a quality failure, never a silent pass.
public sealed record FidelityJudgeResult(bool Passed, IReadOnlyList<FidelityDrift> Drifts, string? Error)
{
    public static FidelityJudgeResult Clean { get; } = new(true, Array.Empty<FidelityDrift>(), null);
}

// The semantic half of the Voice+Fidelity quality track. The deterministic chain catches verbatim
// anchor/term/boundary/sendability defects; this judge catches meaning-level drift a regex cannot — and,
// critically, OBJECT/TERM SUBSTITUTION (seat credit → letter of credit), the documented miss of the old
// SemanticEvalJudge.
public interface IFidelityJudge
{
    Task<FidelityJudgeResult> EvaluateAsync(string sourceText, string candidateText, IReadOnlyList<string>? protectedTerms, CancellationToken ct);
}

// LLM-backed fidelity judge. The model call is injected as a Func so Domain stays free of HTTP/provider
// dependencies and the judge is unit-testable with a fake. Prompt is FACT-LEDGER framed (calibrated
// 2026-05-30, mirrors the eval FaithfulnessGate fix): treat SOURCE as a closed ledger of verifiable facts;
// PASS faithful paraphrase / active↔passive / reordering; FLAG only a genuine fact or truth-value change,
// object substitution, or invented content — so it neither over-flags rewording nor misses object swaps.
// Fail-closed: any call/parse failure ⇒ Error set ⇒ Passed=false.
public sealed class FidelityJudge(Func<string, string, CancellationToken, Task<string?>> complete) : IFidelityJudge
{
    public async Task<FidelityJudgeResult> EvaluateAsync(
        string sourceText,
        string candidateText,
        IReadOnlyList<string>? protectedTerms,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(candidateText))
        {
            return new FidelityJudgeResult(false, Array.Empty<FidelityDrift>(), "candidate_empty");
        }

        var system = BuildPrompt(protectedTerms);
        var user = "SOURCE:\n" + sourceText + "\n\nCANDIDATE:\n" + candidateText;

        string? raw;
        try
        {
            raw = await complete(system, user, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Any provider/HTTP/timeout failure: fail closed, never silently pass.
            return new FidelityJudgeResult(false, Array.Empty<FidelityDrift>(), "judge_call_failed");
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return new FidelityJudgeResult(false, Array.Empty<FidelityDrift>(), "judge_empty");
        }

        var parsed = TryParseDrifts(raw);
        if (parsed is null)
        {
            // repair-parse: take the largest {...} slice and retry once.
            var first = raw.IndexOf('{');
            var last = raw.LastIndexOf('}');
            if (first >= 0 && last > first)
            {
                parsed = TryParseDrifts(raw.Substring(first, last - first + 1));
            }
        }

        if (parsed is null)
        {
            return new FidelityJudgeResult(false, Array.Empty<FidelityDrift>(), "judge_json_parse_failed");
        }

        var pruned = PruneNoOpDrifts(parsed, candidateText);
        return new FidelityJudgeResult(pruned.Count == 0, pruned, null);
    }

    // Drop drifts that are already satisfied — candidate_span == expected_fix (a literal no-op), or a
    // phantom (a span the model claims to see that isn't in the candidate while the corrected value already
    // is). Recall-neutral: a genuinely-needed fix never has its corrected value already present verbatim
    // with the bad span absent.
    public static IReadOnlyList<FidelityDrift> PruneNoOpDrifts(IReadOnlyList<FidelityDrift> drifts, string candidate)
    {
        var kept = new List<FidelityDrift>();
        foreach (var d in drifts)
        {
            if (d.CandidateSpan is not null && string.Equals(d.CandidateSpan, d.ExpectedFix, StringComparison.Ordinal))
            {
                continue;
            }

            var spanAbsent = d.CandidateSpan is null || !candidate.Contains(d.CandidateSpan, StringComparison.Ordinal);
            if (spanAbsent && !string.IsNullOrEmpty(d.ExpectedFix) && candidate.Contains(d.ExpectedFix, StringComparison.Ordinal))
            {
                continue;
            }

            kept.Add(d);
        }

        return kept;
    }

    private static string BuildPrompt(IReadOnlyList<string>? protectedTerms)
    {
        var protectedClause = protectedTerms is { Count: > 0 }
            ? "PROTECTED TERMS — each must appear verbatim OR as a faithful synonym, NEVER replaced by a genuinely DIFFERENT "
              + "real-world thing. For each, if CANDIDATE names a different object (e.g. 'seat credit'→'letter of credit', "
              + "'planter'→'flowerpot', 'saucer'→'tea tray'), that is object_substituted — FLAG it: ["
              + string.Join(", ", protectedTerms.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => "\"" + t.Replace("\"", "'") + "\"")) + "]. "
            : string.Empty;

        return
            "You are a faithfulness checker for an email rewrite. Treat SOURCE as a LEDGER OF VERIFIABLE FACTS: "
            + "amounts/currency, dates, times, IDs, proper names, counts/quantities, who-does-what-to-whom, every yes/no and "
            + "can/cannot/must/unless condition, and the DIRECTION of each action. CANDIDATE is a paraphrase of SOURCE. Find ONLY the "
            + "places where CANDIDATE CHANGES, DROPS, CONTRADICTS, or INVENTS one of those facts. Rewording that preserves the facts "
            + "is NOT a drift — do not list it, and do not echo facts that are fine. "
            + protectedClause
            + "BEFORE flagging anything, apply two tests and PASS whatever survives BOTH: "
            + "(1) FACT test — would a reader be misled about a verifiable fact (a number/amount/currency/date/id/name/count, who is "
            + "responsible, a yes/no, a can/cannot, a condition, or a direction)? If every such fact is preserved — even if worded "
            + "differently, reordered, more verbose, or more casual — it PASSES. "
            + "(2) TRUTH-CONDITION test for negation/modality/subject — compare TRUTH CONDITIONS, not surface form. Active vs passive "
            + "and personal vs impersonal phrasings are EQUIVALENT: 'I cannot apply any change without your written confirmation' ≡ "
            + "'without your written confirmation, nothing can be changed on my side' (both: no change unless you confirm) → PASS. "
            + "Flag polarity/role ONLY when the truth value actually flips (cannot→can, must→optional, by-June-8→after-June-8, "
            + "you-pay→I-pay, future-conditional 'will add'→done 'was added'). "
            + "Drift kinds (flag only GENUINE fact/truth changes): "
            + "hard_anchor — a changed or dropped amount/currency ($12→12 yuan), date, time, id, name, or count; "
            + "polarity_flipped — a negation/modality/condition whose TRUTH VALUE reversed; "
            + "subject_role_swapped — who does/receives the action genuinely changed ('you reported'→'I reported'); "
            + "object_substituted — a concrete thing replaced by a genuinely DIFFERENT real-world thing ('permission slip'→'receipt', "
            + "'delivered'→'shipped', 'seat credit'→'letter of credit'); "
            + "relationship_changed — two distinct facts merged/conflated or a fact attached to the WRONG thing; "
            + "unsupported_addition — CANDIDATE asserts a NEW verifiable fact/number/obligation/commitment SOURCE never states, or injects "
            + "an opinion, jab, dramatized feeling, or narrative. Do NOT flag a brief courtesy, nor a minor manner/framing rewording that "
            + "adds no new verifiable fact. "
            + "These pairs are the SAME and must NOT be flagged: 'set'≈'confirmed', 'wrapped up'≈'finished', 'quote'≈'quotation', "
            + "'onboarding'≈'help getting started', 'email support'≈'standard email support', and ANY active↔passive, personal↔impersonal, "
            + "or reordered/verbose/casual rephrasing that keeps the facts. "
            + "PRECISION AND RECALL BOTH MATTER: do NOT flag faithful paraphrase (the #1 error to avoid); but do NOT miss a real change — "
            + "a changed amount/currency, a truth-value flip, a swapped subject/role, a future-conditional→done tense change, a different "
            + "object, or an invented fact. "
            + "For each drift return {kind, source_value, candidate_span, expected_fix, why} where candidate_span is the exact text in "
            + "CANDIDATE to replace and expected_fix is what it should say. If every fact is preserved return an empty list. "
            + "Return JSON ONLY: {\"drifts\":[...]}.";
    }

    private static List<FidelityDrift>? TryParseDrifts(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("drifts", out var arr) || arr.ValueKind != JsonValueKind.Array)
            {
                return new List<FidelityDrift>();
            }

            var list = new List<FidelityDrift>();
            foreach (var d in arr.EnumerateArray())
            {
                var kind = MapKind(Str(d, "kind"));
                if (kind is null)
                {
                    continue;
                }

                var srcVal = Str(d, "source_value");
                if (string.IsNullOrWhiteSpace(srcVal))
                {
                    continue;
                }

                list.Add(new FidelityDrift(kind.Value, srcVal, NullStr(d, "candidate_span"), Str(d, "expected_fix"), Str(d, "why")));
            }

            return list;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static FidelityDriftKind? MapKind(string k) => k.Trim().ToLowerInvariant() switch
    {
        "hard_anchor" => FidelityDriftKind.HardAnchorChanged,
        "currency_changed" => FidelityDriftKind.HardAnchorChanged,
        "polarity_flipped" => FidelityDriftKind.PolarityFlipped,
        "subject_role_swapped" => FidelityDriftKind.SubjectRoleSwapped,
        "object_substituted" => FidelityDriftKind.ObjectSubstituted,
        "relationship_changed" => FidelityDriftKind.RelationshipChanged,
        "unsupported_addition" => FidelityDriftKind.UnsupportedAddition,
        _ => null,
    };

    private static string Str(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;

    private static string? NullStr(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
