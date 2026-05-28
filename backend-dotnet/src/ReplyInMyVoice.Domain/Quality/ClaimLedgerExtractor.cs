using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ReplyInMyVoice.Domain.RewriteEngine;

namespace ReplyInMyVoice.Domain.Quality;

// Extracts structured atomic claims (RewriteClaim) from an English email draft via an LLM, for the
// Phase-1 EN→ZH safe-intermediate pipeline. The ledger lets the post-translation check verify that
// each subject/modality/polarity/time_scope/condition tuple survived the round-trip — invariants
// that the regex-based FactLedger cannot express on its own.
//
// PROMPT VERSION: claim-ledger-v1 (frozen 2026-05-28). Validated across 10 corpus cases (95 claims):
//   • 0 hallucinated facts, 0 source_span violations (whitespace-normalized), 0 missed critical anchors
//   • 3 known noise behaviors handled in the parser below, not the prompt:
//       1. duplicate source_spans (compound `or` sometimes emits twice) → dedupe
//       2. occasional empathy-shape statements ("I know this is frustrating because…")
//          slipping past Rule 1 → soft-skip
//       3. source_span occasionally wraps a corpus-file soft line-break → whitespace-normalized check
//   Re-validation harness: plans/claim-ledger-validate-v2.py (cheap, ~$0.01 DeepSeek to re-run).
//
// Domain declares the interface only; the DeepSeek call lives in the eval tool (and later the
// Infrastructure project) following the same pattern as IProtectedTermProposer.
public interface IClaimLedgerExtractor
{
    Task<RewriteClaimLedger> ExtractAsync(string draft, CancellationToken cancellationToken);
}

public static class ClaimLedgerJsonParser
{
    // Frozen prompt text. Treat as immutable — re-validate against the 10-case corpus before
    // any change. See plans/claim-ledger-validate-v2.py for the validation harness.
    public const string SystemPromptVersion = "claim-ledger-v1";

    public const string SystemPrompt =
        "You extract STRUCTURED ATOMIC CLAIMS from an email so a translator can be verified for fidelity. Return JSON only.\n\n" +
        "For each meaningful statement, output:\n" +
        "{\n" +
        "  \"id\": \"C001\",\n" +
        "  \"source_span\": \"exact substring of the source email\",\n" +
        "  \"subject\": \"who/what performs the action or holds the state\",\n" +
        "  \"action\": \"verb or state predicate\",\n" +
        "  \"object\": \"what is acted on or stated about (null if intransitive)\",\n" +
        "  \"modality\": \"one of: requirement | permission | capability | uncertainty | prohibition | certainty | offer\",\n" +
        "  \"polarity\": \"positive | negative\",\n" +
        "  \"time_scope\": \"specific date / duration / deadline / temporal qualifier (null if none)\",\n" +
        "  \"condition\": \"any IF/UNLESS condition this claim depends on (null if none)\",\n" +
        "  \"must_preserve\": [\"short list of phrases/properties that must survive any rewrite\"]\n" +
        "}\n\n" +
        "Rules:\n" +
        "1. Skip greetings, sign-offs, polite filler (\"thanks for\", \"hope this finds you well\", etc.)\n" +
        "2. Each claim must be ATOMIC: one subject, one action, one object. Split compound sentences.\n" +
        "3. source_span MUST be an EXACT verbatim substring of the input — do NOT normalize tense, person, word order, or rephrase. If you reconstruct the subject (e.g. resolving a pronoun in `subject`), the source_span must still be the original wording. A naive `draft.contains(source_span)` check must pass.\n" +
        "4. NEVER invent claims not in the source.\n" +
        "5. Capture ALL meaningful claims — a missed claim is a missed drift detection.\n" +
        "6. SKIP meta-communication statements — sentences where the writer comments on how they are phrasing the message rather than asserting a fact about the world. Examples to skip: \"I do not want to make this sound more final than it is\", \"let me explain\", \"to be clear\", \"I'll keep this short\".\n" +
        "7. Modality calibration: verbs that hedge confidence (expect / hope / think / believe / suppose / guess / probably / likely) are modality=`uncertainty`, even if the surrounding tense is declarative. Reserve `certainty` for unhedged assertions of fact or definite future action (\"will\", \"is\", \"was\", \"have done\").\n\n" +
        "Return JSON: {\"claims\": [...]}";

    // Empathy / acknowledgment openings that Rule 1 should have skipped but the model occasionally
    // captures anyway. Soft-skipped at parse time so the post-check doesn't over-bind to them.
    // Conservative pattern — only matches sentences that OPEN with the acknowledgment cue, so we
    // don't strip real factual claims that happen to contain "I know".
    private static readonly Regex EmpathyShapeRegex = new(
        @"^\s*I\s+(know|understand|appreciate|realize|hear|see)\s+(this|that|how|you|your)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Parses the raw LLM JSON response and applies the 3 downstream-cleanup rules:
    //   1. Dedupe claims with identical source_span (compound-or split bug).
    //   2. Drop claims whose source_span is NOT a whitespace-normalized substring of the draft
    //      (catches the rare paraphrase + soft-line-wrap mismatch).
    //   3. Soft-skip empathy-shape openings ("I know this is frustrating because…").
    // Returns an empty ledger on parse failure — never throws on bad JSON.
    public static RewriteClaimLedger Parse(string json, string draft)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new RewriteClaimLedger(Array.Empty<RewriteClaim>());
        }

        RawResponse? raw;
        try
        {
            raw = JsonSerializer.Deserialize<RawResponse>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return new RewriteClaimLedger(Array.Empty<RewriteClaim>());
        }

        if (raw?.Claims is null)
        {
            return new RewriteClaimLedger(Array.Empty<RewriteClaim>());
        }

        var normalizedDraft = NormalizeWhitespace(draft);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var kept = new List<RewriteClaim>();

        foreach (var raw_claim in raw.Claims)
        {
            if (raw_claim is null || string.IsNullOrWhiteSpace(raw_claim.SourceSpan))
            {
                continue;
            }

            var normalizedSpan = NormalizeWhitespace(raw_claim.SourceSpan);
            // Cleanup rule 2: source_span must survive a whitespace-normalized substring check.
            if (!normalizedDraft.Contains(normalizedSpan, StringComparison.Ordinal))
            {
                continue;
            }

            // Cleanup rule 1: dedupe by normalized source_span.
            if (!seen.Add(normalizedSpan))
            {
                continue;
            }

            // Cleanup rule 3: skip empathy-shape openings.
            if (EmpathyShapeRegex.IsMatch(raw_claim.SourceSpan))
            {
                continue;
            }

            kept.Add(new RewriteClaim(
                Id: raw_claim.Id ?? $"C{kept.Count + 1:000}",
                SourceSpan: raw_claim.SourceSpan,
                Subject: raw_claim.Subject ?? string.Empty,
                Action: raw_claim.Action ?? string.Empty,
                Object: string.IsNullOrWhiteSpace(raw_claim.Object) ? null : raw_claim.Object,
                Modality: ParseModality(raw_claim.Modality),
                Polarity: ParsePolarity(raw_claim.Polarity),
                TimeScope: string.IsNullOrWhiteSpace(raw_claim.TimeScope) ? null : raw_claim.TimeScope,
                Condition: string.IsNullOrWhiteSpace(raw_claim.Condition) ? null : raw_claim.Condition,
                MustPreserve: raw_claim.MustPreserve?.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray()
                    ?? Array.Empty<string>()));
        }

        return new RewriteClaimLedger(kept);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static string NormalizeWhitespace(string value) =>
        Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();

    private static RewriteClaimModality ParseModality(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "uncertainty" => RewriteClaimModality.Uncertainty,
        "requirement" => RewriteClaimModality.Requirement,
        "permission" => RewriteClaimModality.Permission,
        "capability" => RewriteClaimModality.Capability,
        "prohibition" => RewriteClaimModality.Prohibition,
        "offer" => RewriteClaimModality.Offer,
        _ => RewriteClaimModality.Certainty,
    };

    private static RewriteClaimPolarity ParsePolarity(string? raw) =>
        string.Equals(raw?.Trim(), "negative", StringComparison.OrdinalIgnoreCase)
            ? RewriteClaimPolarity.Negative
            : RewriteClaimPolarity.Positive;

    // Wire shape matching the prompt's JSON contract. Snake_case property names mapped via attrs.
    private sealed class RawResponse
    {
        [JsonPropertyName("claims")]
        public List<RawClaim>? Claims { get; set; }
    }

    private sealed class RawClaim
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("source_span")] public string? SourceSpan { get; set; }
        [JsonPropertyName("subject")] public string? Subject { get; set; }
        [JsonPropertyName("action")] public string? Action { get; set; }
        [JsonPropertyName("object")] public string? Object { get; set; }
        [JsonPropertyName("modality")] public string? Modality { get; set; }
        [JsonPropertyName("polarity")] public string? Polarity { get; set; }
        [JsonPropertyName("time_scope")] public string? TimeScope { get; set; }
        [JsonPropertyName("condition")] public string? Condition { get; set; }
        [JsonPropertyName("must_preserve")] public List<string>? MustPreserve { get; set; }
    }
}
