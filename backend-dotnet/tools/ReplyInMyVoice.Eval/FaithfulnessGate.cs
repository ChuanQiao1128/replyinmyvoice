using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Reliable faithfulness gate ("B", spec: plans/faithfulness-gate-spec.md). EVAL-ONLY.
// Finds the exact DRIFTED SPANS between a source email and a (rough back-translated) candidate, so an
// automated surgical-repair step can fix only those and keep the low detection score.
//   Layer 1 — deterministic anchor check (no LLM): proper names, money+currency, dates, times, IDs, %.
//             Catches what the single-pass LLM judge "reads past" (Dev->Dave, $12->12 yuan).
//   Layer 2 — constrained LLM check, DRIFTS-ONLY output (tiny response → no truncation/json-crash),
//             repair-parse + fail-closed: polarity flips, subject/role swaps, object substitution, additions.
// Fail-closed: any Layer-2 parse/timeout failure ⇒ Error set ⇒ Passed=false (never silently passes).
public enum DriftKind
{
    HardAnchorMissing,
    HardAnchorChanged,
    CurrencyChanged,
    PolarityFlipped,
    SubjectRoleSwapped,
    ObjectSubstituted,
    UnsupportedAddition,
}

public sealed record DriftSpan(DriftKind Kind, string SourceValue, string? CandidateSpan, string ExpectedFix, string Why);

public sealed record FaithfulnessReport(bool Passed, IReadOnlyList<DriftSpan> Drifts, string? SendAdvisory, string? Error);

// complete(systemPrompt, userPrompt, ct) → model JSON content. Injected so Layer 2 is unit-testable with a
// fake; the CLI wires it to DeepSeekChatClient.CompleteAsync(.., maxTokens:2000, temperature:0, ..).
public sealed class FaithfulnessGate(Func<string, string, CancellationToken, Task<string?>> complete)
{
    public async Task<FaithfulnessReport> EvaluateAsync(string sourceEn, string candidateEn, CancellationToken ct)
    {
        var drifts = new List<DriftSpan>(Layer1HardAnchors(sourceEn, candidateEn));
        var (semDrifts, error) = await Layer2SemanticAsync(sourceEn, candidateEn, ct);
        drifts.AddRange(semDrifts);

        // De-dup: same kind + same source value.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deduped = drifts.Where(d => seen.Add(d.Kind + "|" + d.SourceValue)).ToList();
        var passed = error is null && deduped.Count == 0;
        return new FaithfulnessReport(passed, deduped, null, error);
    }

    // ----------------------------- Layer 1 (deterministic, unit-testable) -----------------------------
    public static IReadOnlyList<DriftSpan> Layer1HardAnchors(string source, string candidate)
    {
        var drifts = new List<DriftSpan>();

        // Money — currency-aware ($12 must not become "12 yuan" / "12 元" / "¥12").
        foreach (Match m in Regex.Matches(source, @"\$\s?\d[\d,]*(?:\.\d+)?"))
        {
            var num = Regex.Match(m.Value, @"\d[\d,]*(?:\.\d+)?").Value;
            var dollar = "$" + num;
            var present = candidate.Contains(dollar, StringComparison.OrdinalIgnoreCase)
                || Regex.IsMatch(candidate, @"\b" + Regex.Escape(num) + @"\s*(?:dollars|usd|u\.s\. dollars|us dollars)\b", RegexOptions.IgnoreCase);
            if (present)
            {
                continue;
            }

            var yuan = Regex.Match(candidate, @"(?:¥\s?)?" + Regex.Escape(num) + @"\s*(?:yuan|元|rmb|cny)\b|¥\s?" + Regex.Escape(num), RegexOptions.IgnoreCase);
            drifts.Add(yuan.Success
                ? new DriftSpan(DriftKind.CurrencyChanged, dollar, yuan.Value.Trim(), dollar, "amount currency changed from USD")
                : new DriftSpan(DriftKind.HardAnchorMissing, dollar, null, dollar, "money amount missing or changed"));
        }

        // Identifiers: R-8801, INV-2290, Q-7719, A-913, FieldTrip-4A-09.
        foreach (Match m in Regex.Matches(source, @"\b[A-Z][A-Za-z]*-[A-Z0-9][A-Z0-9-]*\b"))
        {
            if (!candidate.Contains(m.Value, StringComparison.OrdinalIgnoreCase))
            {
                drifts.Add(new DriftSpan(DriftKind.HardAnchorMissing, m.Value, null, m.Value, "identifier missing or changed"));
            }
        }

        // Dates (Month Day).
        foreach (Match m in Regex.Matches(source, @"\b(January|February|March|April|May|June|July|August|September|October|November|December)\s+(\d{1,2})(?:st|nd|rd|th)?\b", RegexOptions.IgnoreCase))
        {
            var month = m.Groups[1].Value;
            var day = m.Groups[2].Value;
            if (!Regex.IsMatch(candidate, @"\b" + month + @"\s+" + day + @"(?:st|nd|rd|th)?\b", RegexOptions.IgnoreCase))
            {
                drifts.Add(new DriftSpan(DriftKind.HardAnchorMissing, month + " " + day, null, month + " " + day, "date missing or changed"));
            }
        }

        // Clock times.
        foreach (Match m in Regex.Matches(source, @"\b\d{1,2}(?::\d{2})?\s?(?:a\.m\.|p\.m\.|am|pm)\b", RegexOptions.IgnoreCase))
        {
            var norm = Regex.Replace(m.Value, @"\s+", string.Empty);
            var present = candidate.Contains(m.Value, StringComparison.OrdinalIgnoreCase)
                || Regex.Replace(candidate, @"\s+", string.Empty).Contains(norm, StringComparison.OrdinalIgnoreCase);
            if (!present)
            {
                drifts.Add(new DriftSpan(DriftKind.HardAnchorMissing, m.Value, null, m.Value, "time missing or changed"));
            }
        }

        // Phone numbers (e.g. 555-0148).
        foreach (Match m in Regex.Matches(source, @"\b\d{3}-\d{4}\b"))
        {
            if (!candidate.Contains(m.Value, StringComparison.Ordinal))
            {
                drifts.Add(new DriftSpan(DriftKind.HardAnchorMissing, m.Value, null, m.Value, "phone number missing or changed"));
            }
        }

        // Proper names (capitalized tokens minus stopwords/days/months; case-insensitive whole-word presence).
        var names = ExtractNames(source);
        var candNames = Regex.Matches(candidate, @"\b[A-Z][a-z]{2,}\b").Select(x => x.Value).ToHashSet(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (Regex.IsMatch(candidate, @"\b" + Regex.Escape(name) + @"\b", RegexOptions.IgnoreCase))
            {
                continue;
            }

            var near = candNames.FirstOrDefault(t => !names.Contains(t) && t.Length >= 3 && Lev(t.ToLowerInvariant(), name.ToLowerInvariant()) <= 2);
            drifts.Add(near is not null
                ? new DriftSpan(DriftKind.HardAnchorChanged, name, near, name, "proper name changed")
                : new DriftSpan(DriftKind.HardAnchorMissing, name, null, name, "proper name missing"));
        }

        return drifts;
    }

    private static readonly HashSet<string> Stop = new(StringComparer.Ordinal)
    {
        "I", "A", "An", "The", "Hi", "Hello", "Dear", "This", "That", "These", "Those", "Your", "You", "We", "Our",
        "My", "Me", "Please", "Thank", "Thanks", "If", "Once", "But", "So", "And", "Or", "For", "To", "On", "In",
        "At", "As", "Because", "When", "While", "After", "Before", "Since", "Right", "Now", "Both", "It", "Its",
        "Order", "Then", "Also", "There", "Here", "What", "Why", "How", "No", "Not", "Yes", "Without", "With",
        "From", "Next", "Fortunately", "Suddenly", "Honestly", "Seriously", "Well", "Alright", "Okay", "OK",
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday",
        "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December",
    };

    private static HashSet<string> ExtractNames(string source)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(source, @"\b[A-Z][a-zA-Z]{1,}\b"))
        {
            var w = m.Value;
            if (!Stop.Contains(w) && w.Length >= 2)
            {
                names.Add(w);
            }
        }

        return names;
    }

    private static int Lev(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++)
        {
            d[i, 0] = i;
        }

        for (var j = 0; j <= b.Length; j++)
        {
            d[0, j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[a.Length, b.Length];
    }

    // ----------------------------- Layer 2 (constrained LLM, drifts-only, robust) -----------------------------
    private async Task<(List<DriftSpan> Drifts, string? Error)> Layer2SemanticAsync(string source, string candidate, CancellationToken ct)
    {
        const string sys =
            "You are a STRICT faithfulness checker for an email rewrite. Compare SOURCE (truth) to CANDIDATE. "
            + "Output ONLY the places where CANDIDATE changed the meaning — do NOT list things that are fine, and do "
            + "NOT echo every fact. Drift kinds to find: "
            + "polarity_flipped (a negation/modality/condition reversed or softened — 'cannot'→'can', 'not on file'→'on file', "
            + "'will add (future, conditional)'→'was added (done)'); "
            + "subject_role_swapped (who does/receives the action changed — 'you reported'→'I reported', 'I will refund'→'you refund'); "
            + "object_substituted (a concrete thing replaced by a DIFFERENT thing — 'permission slip'→'receipt', 'delivered'→'shipped', "
            + "'admin workspace'→'back-end'); "
            + "unsupported_addition (CANDIDATE asserts a fact/obligation/opinion about the recipient or deal that is NOT in SOURCE — "
            + "e.g. an added jab like 'why are you so obsessed with this?'; IGNORE harmless pleasantries like 'thanks for your patience'). "
            + "These specific pairs are the SAME thing and must NOT be flagged: 'set'≈'confirmed', 'wrapped up'≈'finished', "
            + "'quote'≈'quotation sheet'≈'quotation', 'onboarding'≈'help getting started'≈'someone to guide you at the start', "
            + "'email support'≈'standard email support'. object_substituted means the candidate names a genuinely DIFFERENT "
            + "real-world thing: permission slip→receipt, delivered→shipped, admin workspace→billing dashboard, dish rack→colander. "
            + "RECALL-FIRST: apart from the safe pairs just listed, when in doubt FLAG it — over-reporting a borderline drift is "
            + "fine, but MISSING a real one (a changed amount/currency, a flipped negation or modality, a swapped subject/role, a "
            + "tense change from future-conditional to done, or a different object) is NOT acceptable. "
            + "For each drift return {kind, source_value, candidate_span, expected_fix, why} where candidate_span is the exact text in "
            + "CANDIDATE to replace and expected_fix is what it should say. If fully faithful return an empty list. "
            + "Return JSON ONLY: {\"drifts\":[...]}.";
        var user = "SOURCE:\n" + source + "\n\nCANDIDATE:\n" + candidate;

        string? raw;
        try
        {
            raw = await complete(sys, user, ct);
        }
        catch (Exception e) when (e is OperationCanceledException or HttpRequestException)
        {
            return (new List<DriftSpan>(), "layer2_call_failed");
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return (new List<DriftSpan>(), "layer2_empty");
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

        // Fail-closed: unparseable ⇒ error (caller marks Passed=false), never silently "no drift".
        return parsed is null ? (new List<DriftSpan>(), "layer2_json_parse_failed") : (parsed, null);
    }

    private static List<DriftSpan>? TryParseDrifts(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("drifts", out var arr) || arr.ValueKind != JsonValueKind.Array)
            {
                return new List<DriftSpan>();
            }

            var list = new List<DriftSpan>();
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

                list.Add(new DriftSpan(kind.Value, srcVal, NullStr(d, "candidate_span"), Str(d, "expected_fix"), Str(d, "why")));
            }

            return list;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DriftKind? MapKind(string k) => k.Trim().ToLowerInvariant() switch
    {
        "polarity_flipped" => DriftKind.PolarityFlipped,
        "subject_role_swapped" => DriftKind.SubjectRoleSwapped,
        "object_substituted" => DriftKind.ObjectSubstituted,
        "unsupported_addition" => DriftKind.UnsupportedAddition,
        _ => null,
    };

    private static string Str(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;

    private static string? NullStr(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
