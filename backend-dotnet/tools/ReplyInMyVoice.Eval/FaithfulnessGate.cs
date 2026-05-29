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
    RelationshipChanged,
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

    // Cross-lingual: compare the English SOURCE to a Chinese CANDIDATE; return drifts whose CandidateSpan /
    // ExpectedFix are CHINESE, so they can be SURGICALLY string-replaced in the Chinese before one back-
    // translation (owner's "repair in Chinese, surgically" idea). Drifts-only, repair-parse, fail-closed.
    public async Task<(IReadOnlyList<DriftSpan> Drifts, string? Error)> EvaluateCrossLingualAsync(string sourceEn, string zhCandidate, CancellationToken ct)
    {
        const string sys =
            "You are a strict faithfulness checker. SOURCE is the TRUTH (English). CANDIDATE is a rough Chinese translation. "
            + "CRITICAL — PROPER NAMES, PRODUCT/BRAND NAMES, IDs, and FEATURE/PACKAGE terms from SOURCE must stay VERBATIM IN ENGLISH inside the Chinese. "
            + "If the Chinese TRANSLITERATED or TRANSLATED one (e.g. 'Dev'→'戴夫', 'Northstar'→'北极星', 'admin workspace'→'管理工作区'/'工作区管理', 'onboarding'→'上线服务'/'入职'/'引导', 'SSO'→'单点登录', 'email support'→'邮件支持'), "
            + "that IS a drift: flag it with candidate_span = the Chinese rendering and expected_fix = the exact English token, so it "
            + "survives the back-translation as the original English. "
            + "But if a name/product/ID/feature term ALREADY appears VERBATIM IN ENGLISH in the CANDIDATE, it is CORRECT — do NOT flag it, and never emit a no-op fix (candidate_span equal to expected_fix). "
            + "Find ONLY the spans in the CHINESE CANDIDATE that CHANGE, DROP, CONTRADICT, or INVENT a verifiable fact vs SOURCE: a "
            + "changed number/amount/currency ($ vs 元/yuan)/date/name/id/count; a negation or modality whose TRUTH VALUE flipped "
            + "(能↔不能, 可能↔一定, 截止X前↔X后); a swapped subject/role (谁对谁做什么真的变了); a substituted object (一个东西换成了"
            + "另一个真正不同的东西); two facts conflated or attached to the wrong thing; or invented content not in SOURCE (凭空的事实/"
            + "情绪/叙事/结论). "
            + "TWO TESTS before flagging — PASS whatever survives BOTH: (1) FACT 测试:每个 数字/金额/日期/单号/人名/数量、谁对谁、"
            + "是否、能/不能、条件、方向 都保住(即使措辞不同/更口语/更啰嗦)→ 放行; (2) 真值等价:主动↔被动、有主语↔无主语,只要"
            + "真值一样就等价(如「我不能加」≡「没你书面确认这边动不了」)→ 放行。只有真值真翻了才标 polarity/role。 "
            + "INVENTED ADDITIONS still matter: flag a Chinese phrase that adds a NEW fact, emotion, narrative, or a WRONG DIRECTION the "
            + "SOURCE lacks — e.g. an added '正好可以往前赶'/'这下好了'/'正好提前' when SOURCE only states a plain fact, or implying "
            + "'move it earlier' when SOURCE's offered options are actually later. For such an addition: if the span is PURELY the "
            + "unsupported addition, set expected_fix to an empty string to delete it; but if the span MIXES a real fact with an addition "
            + "(e.g. '那个房间空出来了，正好可以往前赶' mixes the room reason with an added 'move earlier'), set expected_fix to the "
            + "CORRECTED minimal Chinese that KEEPS the real fact and drops ONLY the added part (here: '那个房间用不了。') — never "
            + "delete the real fact. Do NOT flag faithful Chinese paraphrase, reordering, casual/verbose wording, or active↔passive "
            + "rephrasing that keeps the facts (that is the #1 error to avoid); but do NOT miss a real fact/truth change or an invented "
            + "fact/emotion/wrong-direction. For each drift output "
            + "{\"kind\":\"hard_anchor|currency_changed|polarity_flipped|subject_role_swapped|object_substituted|relationship_changed|unsupported_addition\","
            + "\"source_value\":\"<the English truth>\",\"candidate_span\":\"<EXACT Chinese substring to replace, copied verbatim from CANDIDATE>\","
            + "\"expected_fix\":\"<corrected Chinese; use empty string to DELETE an invented addition>\",\"why\":\"...\"}. "
            + "candidate_span MUST be an exact substring of the CHINESE so it can be string-replaced. Return JSON ONLY: {\"drifts\":[...]}.";
        var user = "SOURCE (English, truth):\n" + sourceEn + "\n\nCANDIDATE (Chinese):\n" + zhCandidate;

        string? raw;
        try
        {
            raw = await complete(sys, user, ct);
        }
        catch (Exception e) when (e is OperationCanceledException or HttpRequestException)
        {
            return (new List<DriftSpan>(), "xl_call_failed");
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return (new List<DriftSpan>(), "xl_empty");
        }

        var parsed = TryParseDrifts(raw);
        if (parsed is null)
        {
            var first = raw.IndexOf('{');
            var last = raw.LastIndexOf('}');
            if (first >= 0 && last > first)
            {
                parsed = TryParseDrifts(raw.Substring(first, last - first + 1));
            }
        }

        return parsed is null ? (new List<DriftSpan>(), "xl_json_parse_failed") : (PruneNoOpDrifts(parsed, zhCandidate), null);
    }

    // Precision guard for the cross-lingual gate: the heavy "keep terms in English" rule makes the LLM
    // sometimes emit drifts that are already satisfied — candidate_span == expected_fix (a literal no-op), or
    // it claims a Chinese rendering that isn't actually in the candidate while the corrected token already is
    // (a phantom). Applying either is a no-op, so they only inflate the drift count. Drop them. Recall-neutral:
    // a genuinely-needed fix never has its corrected value already present verbatim with the bad span absent.
    public static IReadOnlyList<DriftSpan> PruneNoOpDrifts(IReadOnlyList<DriftSpan> drifts, string candidate)
    {
        var kept = new List<DriftSpan>();
        foreach (var d in drifts)
        {
            if (d.CandidateSpan is not null && string.Equals(d.CandidateSpan, d.ExpectedFix, StringComparison.Ordinal))
            {
                continue; // literal no-op: replace X with the same X
            }

            var spanAbsent = d.CandidateSpan is null || !candidate.Contains(d.CandidateSpan, StringComparison.Ordinal);
            if (spanAbsent && !string.IsNullOrEmpty(d.ExpectedFix) && candidate.Contains(d.ExpectedFix, StringComparison.Ordinal))
            {
                continue; // phantom: nothing to replace and the corrected value is already present
            }

            kept.Add(d);
        }

        return kept;
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
            "You are a faithfulness checker for an email rewrite. Treat SOURCE as a LEDGER OF VERIFIABLE FACTS: "
            + "amounts/currency, dates, times, IDs, proper names, counts/quantities, who-does-what-to-whom, every yes/no and "
            + "can/cannot/must/unless condition, and the DIRECTION of each action. CANDIDATE is a paraphrase of SOURCE. Find ONLY the "
            + "places where CANDIDATE CHANGES, DROPS, CONTRADICTS, or INVENTS one of those facts. Rewording that preserves the facts "
            + "is NOT a drift — do not list it, and do not echo facts that are fine. "
            + "BEFORE flagging anything, apply two tests and PASS whatever survives BOTH: "
            + "(1) FACT test — would a reader be misled about a verifiable fact (a number/amount/currency/date/id/name/count, who is "
            + "responsible, a yes/no, a can/cannot, a condition, or a direction)? If every such fact is preserved — even if worded "
            + "differently, reordered, more verbose, or more casual — it PASSES. "
            + "(2) TRUTH-CONDITION test for negation/modality/subject — compare TRUTH CONDITIONS, not surface form. Active vs passive "
            + "and personal vs impersonal phrasings are EQUIVALENT: 'I cannot apply any change without your written confirmation' ≡ "
            + "'without your written confirmation, nothing can be changed on my side' (both: no change unless you confirm) → PASS. "
            + "'is up for renewal on June 15' ≡ 'will be renewed on June 15' when SOURCE states it auto-renews that day → PASS. Flag "
            + "polarity/role ONLY when the truth value actually flips (cannot→can, must→optional, by-June-8→after-June-8, you-pay→I-pay, "
            + "future-conditional 'will add'→done 'was added'). "
            + "Drift kinds (flag only GENUINE fact/truth changes): "
            + "hard_anchor — a changed or dropped amount/currency ($12→12 yuan), date, time, id, name, or count; "
            + "polarity_flipped — a negation/modality/condition whose TRUTH VALUE reversed ('cannot'→'can', 'not on file'→'on file', "
            + "'will add (future, conditional)'→'was added (done)'); "
            + "subject_role_swapped — who does/receives the action genuinely changed ('you reported'→'I reported', 'I will refund'→'you refund'); "
            + "object_substituted — a concrete thing replaced by a genuinely DIFFERENT real-world thing ('permission slip'→'receipt', "
            + "'delivered'→'shipped', 'admin workspace'→'billing dashboard', 'dish rack'→'colander'); "
            + "relationship_changed — two distinct facts merged/conflated or a fact attached to the WRONG thing (a check/review date and "
            + "an event date merged into one; a date attached to the wrong event); "
            + "unsupported_addition — CANDIDATE asserts a NEW verifiable fact/number/obligation/commitment SOURCE never states, or injects "
            + "an opinion, jab, dramatized feeling, or narrative ('why are you so obsessed with this?', 'this has been weighing on my "
            + "heart', 'I searched over and over'). Do NOT flag a brief courtesy ('thanks for your patience'), nor a minor manner/framing "
            + "rewording that adds no new verifiable fact ('I noticed'→'I looked and noticed'). "
            + "These pairs are the SAME and must NOT be flagged: 'set'≈'confirmed', 'wrapped up'≈'finished', 'quote'≈'quotation sheet'≈'quotation', "
            + "'onboarding'≈'help getting started'≈'someone to guide you at the start', 'email support'≈'standard email support', and ANY "
            + "active↔passive, personal↔impersonal, or reordered/verbose/casual rephrasing that keeps the facts. "
            + "PRECISION AND RECALL BOTH MATTER: do NOT flag faithful paraphrase, reordering, added verbosity, casual tone, or "
            + "truth-equivalent rephrasing (that is the #1 error to avoid); but do NOT miss a real change — a changed amount/currency, a "
            + "truth-value flip, a swapped subject/role, a future-conditional→done tense change, a different object, or an invented fact. "
            + "For each drift return {kind, source_value, candidate_span, expected_fix, why} where candidate_span is the exact text in "
            + "CANDIDATE to replace and expected_fix is what it should say. If every fact is preserved return an empty list. "
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
        "hard_anchor" => DriftKind.HardAnchorChanged,
        "currency_changed" => DriftKind.CurrencyChanged,
        "polarity_flipped" => DriftKind.PolarityFlipped,
        "subject_role_swapped" => DriftKind.SubjectRoleSwapped,
        "object_substituted" => DriftKind.ObjectSubstituted,
        "relationship_changed" => DriftKind.RelationshipChanged,
        "unsupported_addition" => DriftKind.UnsupportedAddition,
        _ => null,
    };

    private static string Str(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;

    private static string? NullStr(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
