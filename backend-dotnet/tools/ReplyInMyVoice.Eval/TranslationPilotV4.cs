using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.RewriteEngine;
using ReplyInMyVoice.Infrastructure.Providers;

// Round-5 fact-only-repair pilot (R5). EVAL-ONLY; triggered by R5_FACT_REPAIR_PILOT=1 in Program.cs.
// See plans/translation-roundtrip-pilot.md + the round-5 design.
//
// The one untested quadrant: keep raw-TA's low-Pangram machine-translation surface and patch ONLY
// fact/boundary drift — explicitly ACCEPTING translationese. The relaxed bar (vs round-3 T4) is the
// whole point: Q1 fact-safe + Q2 understandable are required; Q3 native-send-ready is recorded only.
//   T0     = production baseline
//   TA_raw = T0 -> mask hard facts -> Youdao en->zh-CHS->en (xN rounds) -> unmask
//   R5     = TA_raw -> locate fact/boundary drift -> apply fact-only patches (no style patches)
// DeepSeek may locate drift and propose patches but never emits a whole email; the program applies
// budgeted find/replace patches whose replacements are copied from Original/T0/ledger. Pangram once
// per Q1+Q2 survivor. NOTE: producing fact-safe-but-MT-flavored output is detection-chasing and
// off-brand for "Reply In My Voice" — this run is for empirical closure, not a product path.

internal sealed record R5Patch(string AssertionId, string Find, string Replace, string Source, string PatchType, string Reason);

internal sealed record FactAssertion(string Id, string Type, string SourceText, string Status, string EvidenceSpan, string Severity, string Reason);

internal sealed record FactDriftResult(bool Fallback, IReadOnlyList<FactAssertion> Assertions, IReadOnlyList<R5Patch> Patches, string? Error = null);

// Merged DriftLocator + FactPatchProposer (one DeepSeek call): locate which required facts/boundaries
// went missing/drifted in TA_raw, and propose fact-only patches whose replacements are copied from the
// authoritative sources. Explicitly forbidden from fixing style/translationese.
internal sealed class FactDriftRepairer(DeepSeekChatClient chat)
{
    private const string SystemPrompt =
        "You are NOT rewriting or polishing an email. You only (a) locate factual drift in a "
        + "machine-translated English draft (TA) and (b) propose fact-only patches. Authoritative fact "
        + "sources: ORIGINAL DRAFT, T0 BASELINE, MUST_KEEP, MUST_NOT_CLAIM. TA may contain translationese "
        + "— DO NOT fix style, fluency, prepositions, or awkwardness. ONLY fix: a required fact that is "
        + "missing; a fact whose value/object/status changed (e.g. 'saucer'->'tea tray', an amount/date/ID "
        + "altered, 'marked delivered'->'delivered to you'); or a boundary whose polarity flipped "
        + "(cannot->can, may->will, not yet->done). For each MUST_KEEP and MUST_NOT_CLAIM item, mark "
        + "status ok|missing|drifted with the TA evidence span. For each missing/drifted item, propose a "
        + "patch: find = an EXACT substring of TA (the wrong/garbled span), replace = the correct text "
        + "copied EXACTLY from ORIGINAL/T0/MUST_KEEP. Rules: JSON only, never the full email; every patch "
        + "has an assertion_id; prefer copied spans, avoid new writing; never replace whole paragraphs; "
        + "avoid replacing a whole sentence unless a severe factual reversal; if fact safety would require "
        + "broad rewriting, set fallback=true. Max 12 patches. Return JSON: {\"fallback\":false,"
        + "\"assertions\":[{\"id\":\"A1\",\"type\":\"ExactAnchor|ProtectedTerm|Boundary|Action|Status|NextStep\","
        + "\"source_text\":\"...\",\"status\":\"ok|missing|drifted\",\"evidence_span\":\"...\","
        + "\"severity\":\"low|medium|high\",\"reason\":\"...\"}],\"patches\":[{\"assertion_id\":\"A1\","
        + "\"find\":\"...\",\"replace\":\"...\",\"source\":\"original|t0|ledger\",\"patch_type\":\"...\","
        + "\"reason\":\"...\"}]}.";

    public async Task<FactDriftResult> LocateAndProposeAsync(
        string taRaw, string originalDraft, string t0, IReadOnlyList<string> mustKeep, IReadOnlyList<string> mustNotClaim, CancellationToken ct)
    {
        var user =
            "ORIGINAL DRAFT (fact source of truth):\n" + originalDraft
            + "\n\nT0 BASELINE (clean reference for copyable spans):\n" + t0
            + "\n\nMUST_KEEP:\n" + string.Join("\n", mustKeep.Select(f => "- " + f))
            + "\n\nMUST_NOT_CLAIM:\n" + string.Join("\n", mustNotClaim.Select(f => "- " + f))
            + "\n\nTA (machine-translated text to check and fact-patch):\n" + taRaw;

        // Larger token budget: the combined assertions+patches JSON for a fact-dense case (e.g. 12
        // must_keep items) easily exceeds 2000 tokens and truncates into invalid JSON.
        var content = await chat.CompleteAsync(SystemPrompt, user, 4000, 0, ct);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new FactDriftResult(false, Array.Empty<FactAssertion>(), Array.Empty<R5Patch>(), "drift_call_failed");
        }

        try
        {
            using var doc = JsonDocument.Parse(ExtractJson(content));
            var root = doc.RootElement;
            var fallback = root.TryGetProperty("fallback", out var fb) && fb.ValueKind == JsonValueKind.True;

            var assertions = new List<FactAssertion>();
            if (root.TryGetProperty("assertions", out var aArr) && aArr.ValueKind == JsonValueKind.Array)
            {
                assertions.AddRange(aArr.EnumerateArray().Select(a => new FactAssertion(
                    Str(a, "id"), Str(a, "type"), Str(a, "source_text"), Str(a, "status"),
                    Str(a, "evidence_span"), Str(a, "severity"), Str(a, "reason"))));
            }

            var patches = new List<R5Patch>();
            if (root.TryGetProperty("patches", out var pArr) && pArr.ValueKind == JsonValueKind.Array)
            {
                patches.AddRange(pArr.EnumerateArray().Select(p => new R5Patch(
                    Str(p, "assertion_id"), Str(p, "find"), Str(p, "replace"), Str(p, "source"),
                    Str(p, "patch_type"), Str(p, "reason"))).Where(p => p.Find.Length > 0));
            }

            return new FactDriftResult(fallback, assertions, patches);
        }
        catch (JsonException)
        {
            return new FactDriftResult(false, Array.Empty<FactAssertion>(), Array.Empty<R5Patch>(), "drift_parse_failed");
        }
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;

    // Defensive: strip ```json fences and trim to the outermost {...} so stray prose/fences don't
    // break the parse. (Truncation is handled by the larger token budget above.)
    private static string ExtractJson(string s)
    {
        var t = s.Trim();
        if (t.StartsWith("```", StringComparison.Ordinal))
        {
            var nl = t.IndexOf('\n');
            if (nl >= 0)
            {
                t = t[(nl + 1)..];
            }

            if (t.EndsWith("```", StringComparison.Ordinal))
            {
                t = t[..^3];
            }

            t = t.Trim();
        }

        int a = t.IndexOf('{'), b = t.LastIndexOf('}');
        return a >= 0 && b > a ? t.Substring(a, b - a + 1) : t;
    }
}

internal sealed record R5ApplyResult(
    string Text, int PatchCount, double FactCharRatio, double StyleCharRatio, int FullSentenceReplacements, bool Fallback, string Reason);

// Applies fact-only patches under the round-5 budget (§7): ≤12 patches, fact-char ≤25%, style/new ≤5%,
// ≤1 full-sentence replacement. Over budget => fallback to T0 (do not become a rewrite).
internal static class R5PatchApplier
{
    private const int MaxPatches = 12;
    private const double MaxFactRatio = 0.25;
    private const double MaxStyleRatio = 0.05;
    private const int MaxFullSentence = 1;
    private const int MaxFind = 160;
    private const int MaxReplace = 180;
    private static readonly Regex EndsSentence = new(@"[.!?][""')\]]?\s*$", RegexOptions.Compiled);

    public static R5ApplyResult Apply(string taRaw, IReadOnlyList<R5Patch> patches, string original, string t0)
    {
        var valid = patches
            .Where(p => !string.IsNullOrEmpty(p.AssertionId) && !string.IsNullOrEmpty(p.Find)
                && p.Find.Length <= MaxFind && (p.Replace?.Length ?? 0) <= MaxReplace)
            .ToList();
        if (valid.Count > MaxPatches)
        {
            return new R5ApplyResult(taRaw, 0, 0, 0, 0, true, "too_many_patches");
        }

        var working = taRaw;
        int factChars = 0, styleChars = 0, fullSent = 0, applied = 0;
        foreach (var p in valid)
        {
            var idx = working.IndexOf(p.Find, StringComparison.Ordinal);
            if (idx < 0)
            {
                continue;
            }

            working = string.Concat(working.AsSpan(0, idx), p.Replace, working.AsSpan(idx + p.Find.Length));
            factChars += p.Find.Length;
            if (IsNewWriting(p.Replace, original, t0))
            {
                styleChars += p.Replace?.Length ?? 0;
            }

            if (IsFullSentence(p.Find))
            {
                fullSent++;
            }

            applied++;
        }

        var factRatio = (double)factChars / Math.Max(1, taRaw.Length);
        var styleRatio = (double)styleChars / Math.Max(1, taRaw.Length);
        if (fullSent > MaxFullSentence)
        {
            return new R5ApplyResult(working, applied, factRatio, styleRatio, fullSent, true, "too_many_full_sentence_replacements");
        }

        if (factRatio > MaxFactRatio)
        {
            return new R5ApplyResult(working, applied, factRatio, styleRatio, fullSent, true, "fact_ratio_exceeded");
        }

        if (styleRatio > MaxStyleRatio)
        {
            return new R5ApplyResult(working, applied, factRatio, styleRatio, fullSent, true, "style_ratio_exceeded");
        }

        return new R5ApplyResult(working, applied, factRatio, styleRatio, fullSent, false, string.Empty);
    }

    private static bool IsNewWriting(string? replace, string original, string t0)
    {
        var t = (replace ?? string.Empty).Trim();
        if (t.Length <= 2)
        {
            return false;
        }

        return !original.Contains(t, StringComparison.Ordinal) && !t0.Contains(t, StringComparison.Ordinal);
    }

    private static bool IsFullSentence(string find)
    {
        var f = find.Trim();
        return f.Length > 40 && EndsSentence.IsMatch(f);
    }
}

internal sealed record UnderstandabilityVerdict(bool Understandable, IReadOnlyList<string> Issues, string? Error = null);

// Q2: can the recipient understand the actionable content, IGNORING translationese? (Round-5 accepts
// awkward phrasing; it only fails when clunkiness makes a fact/next-step/limit impossible to follow.)
internal sealed class UnderstandabilityJudge(DeepSeekChatClient chat)
{
    private const string SystemPrompt =
        "You judge ONLY whether the recipient can UNDERSTAND the actionable content of this email, "
        + "IGNORING awkward phrasing, non-idiomatic prepositions, or machine-translation style. Can they "
        + "understand: the next step, any deadline/time, who is responsible, the limits/constraints, and "
        + "what is being asked of them? If the core actionable meaning is clear despite clunky English, it "
        + "is understandable. Only mark it not understandable if clunkiness makes a fact, next step, time, "
        + "contact, or limit genuinely impossible to follow. Return JSON: {\"understandable\":true,"
        + "\"issues\":[\"what is unclear\"]}.";

    public async Task<UnderstandabilityVerdict> JudgeAsync(string text, CancellationToken ct)
    {
        var content = await chat.CompleteAsync(SystemPrompt, text, 500, 0, ct);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new UnderstandabilityVerdict(false, Array.Empty<string>(), "judge_failed");
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("understandable", out var u) && u.ValueKind == JsonValueKind.True;
            var issues = root.TryGetProperty("issues", out var iss) && iss.ValueKind == JsonValueKind.Array
                ? iss.EnumerateArray().Select(i => i.GetString() ?? string.Empty).Where(i => i.Length > 0).ToList()
                : new List<string>();
            return new UnderstandabilityVerdict(ok, issues);
        }
        catch (JsonException)
        {
            return new UnderstandabilityVerdict(false, Array.Empty<string>(), "judge_parse_failed");
        }
    }
}

internal sealed record PilotV4Row(
    string CaseId,
    int CaseNumber,
    string Category,
    string Tone,
    bool T0HasOutput,
    string T0Text,
    bool SentinelPass,
    int AnchorCount,
    string TaRawText,
    string R5Text,
    bool R5Generated,
    int DriftAssertionsCount,
    int PatchCount,
    int FactPatchCharRatioPct,
    int StylePatchCharRatioPct,
    int FullSentenceReplacements,
    bool Q1FactSafe,
    bool Q2Understandable,
    bool Q3NativeSendReady,
    IReadOnlyList<string> Q1Issues,
    IReadOnlyList<string> Q2Issues,
    int? PangramT0,
    int? PangramR5,
    int? DeltaR5MinusT0,
    string ChosenVariant,
    string FallbackReason,
    string Notes);

internal static class TranslationPilotV4Runner
{
    public static async Task<int> RunAsync(
        FactReconstructRewriteProvider provider,
        CountingRewriteModelClient modelCounter,
        CountingWritingSignalClient saplingCounter,
        IReadOnlyList<EvalCase> cases,
        EvalConfig config,
        string apiKey,
        DateTimeOffset startedAt)
    {
        var youdaoKey = FirstEnv("YOUDAO_APP_KEY", "AppID", "YouDao_API_KEY");
        var youdaoSecret = FirstEnv("YOUDAO_APP_SECRET", "AppSecret");
        var youdaoUrl = Environment.GetEnvironmentVariable("YOUDAO_API_URL") ?? "https://openapi.youdao.com/api";
        if (string.IsNullOrWhiteSpace(youdaoKey) || string.IsNullOrWhiteSpace(youdaoSecret))
        {
            Console.Error.WriteLine("R5_FACT_REPAIR_PILOT: missing Youdao credentials.");
            return 2;
        }

        var youdaoMaxCalls = IntEnv("YOUDAO_MAX_CALLS", 40);
        var pangramMaxCalls = IntEnv("PANGRAM_MAX_CALLS", 0);
        var pangramKey = Environment.GetEnvironmentVariable("PANGRAM_API_KEY");
        var pangramEnabled = pangramMaxCalls > 0 && !string.IsNullOrWhiteSpace(pangramKey);
        // R5B: number of full en->zh->en round-trips (default 1 = R5A). 2 = double translation.
        var translationRounds = Math.Clamp(IntEnv("R5_TRANSLATION_ROUNDS", 1), 1, 3);

        using var youdaoHttp = new HttpClient();
        using var deepseekHttp = new HttpClient();
        using var judgeHttp = new HttpClient();
        using var pangramHttp = new HttpClient();

        var youdao = new YoudaoTranslationClient(youdaoHttp, youdaoKey!, youdaoSecret!, youdaoUrl, TimeSpan.FromSeconds(30));
        var deepseek = new DeepSeekChatClient(deepseekHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));
        var driftRepairer = new FactDriftRepairer(deepseek);
        var understandability = new UnderstandabilityJudge(deepseek);
        var tierJudge = new SendabilityTierJudge(deepseek); // Q3 (recorded only)
        var judge = new SemanticEvalJudge(judgeHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));
        var pangram = pangramEnabled
            ? new PangramWritingSignalClient(pangramHttp, pangramKey!, TimeSpan.FromSeconds(45))
            : null;

        Console.WriteLine(
            $"R5 pilot: cases={cases.Count} translationRounds={translationRounds} youdaoMax={youdaoMaxCalls} "
            + $"pangram={(pangramEnabled ? $"on(max {pangramMaxCalls})" : "off")} model={config.Model}");

        var rows = new List<PilotV4Row>();
        var pangramCalls = 0;

        foreach (var sample in cases)
        {
            var request = sample.ToRewriteRequest();
            var tone = request.Tone;
            var mustKeep = sample.MustKeep;
            var mustNotClaim = sample.MustNotClaim;

            var t0Result = await provider.RewriteAsync(Guid.NewGuid(), request, CancellationToken.None);
            var t0Text = RewritePayload.TryParse(t0Result.ResultJson)?.RewrittenText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(t0Text))
            {
                rows.Add(EmptyRow(sample, tone, t0Result.ErrorCode));
                Console.WriteLine($"{sample.Id}: T0 produced no output ({t0Result.ErrorCode ?? "unknown"}); skipped.");
                continue;
            }

            var ledger = FactLedgerExtractor.Extract(request);
            // §8: mask only high-confidence hard facts + must_keep/must_not_claim anchors (NOT all
            // business nouns) — drift in ordinary nouns is fixed by the fact-patch step, not masking.
            var masked = AnchorMasker.Mask(t0Text, ledger, mustKeep, mustNotClaim);

            var taRaw = string.Empty;
            var sentinelPass = false;
            var r5Text = string.Empty;
            var r5Generated = false;
            var driftCount = 0;
            var patchCount = 0;
            var factPct = 0;
            var stylePct = 0;
            var fullSent = 0;
            var q1 = false;
            var q2 = false;
            var q3 = false;
            IReadOnlyList<string> q1Issues = Array.Empty<string>();
            IReadOnlyList<string> q2Issues = Array.Empty<string>();
            string fallbackReason;

            if (youdao.CallCount + (2 * translationRounds) > youdaoMaxCalls)
            {
                fallbackReason = "youdao_budget_exhausted";
            }
            else
            {
                // N full round-trips on the masked text (R5A: 1, R5B: 2).
                var carried = masked.MaskedText;
                var translateOk = true;
                string? translateErr = null;
                for (var i = 0; i < translationRounds && translateOk; i++)
                {
                    var toZh = await youdao.TranslateAsync(carried, "en", "zh-CHS", CancellationToken.None);
                    var backEn = toZh.Success
                        ? await youdao.TranslateAsync(toZh.Text, "zh-CHS", "en", CancellationToken.None)
                        : new TranslateResult(false, string.Empty, toZh.ErrorCode);
                    if (!backEn.Success)
                    {
                        translateOk = false;
                        translateErr = backEn.ErrorCode;
                    }
                    else
                    {
                        carried = backEn.Text;
                    }
                }

                if (!translateOk)
                {
                    fallbackReason = translateErr ?? "youdao_failed";
                }
                else
                {
                    var unmask = AnchorMasker.Unmask(carried, masked.Map);
                    taRaw = unmask.Restored;
                    sentinelPass = unmask.IntegrityOk;
                    if (!sentinelPass)
                    {
                        fallbackReason = "sentinel_broken";
                    }
                    else
                    {
                        var drift = await driftRepairer.LocateAndProposeAsync(taRaw, sample.InputDraft, t0Text, mustKeep, mustNotClaim, CancellationToken.None);
                        driftCount = drift.Assertions.Count(a => a.Status is "missing" or "drifted");
                        if (drift.Error is not null)
                        {
                            fallbackReason = $"drift_{drift.Error}";
                        }
                        else if (drift.Fallback)
                        {
                            fallbackReason = "drift_locator_fallback";
                        }
                        else
                        {
                            var apply = R5PatchApplier.Apply(taRaw, drift.Patches, sample.InputDraft, t0Text);
                            patchCount = apply.PatchCount;
                            factPct = (int)Math.Round(apply.FactCharRatio * 100);
                            stylePct = (int)Math.Round(apply.StyleCharRatio * 100);
                            fullSent = apply.FullSentenceReplacements;
                            if (apply.Fallback)
                            {
                                fallbackReason = "patch_budget:" + apply.Reason;
                            }
                            else
                            {
                                r5Generated = true;
                                r5Text = apply.Text;

                                // Q1 fact-safe: semantic facts + boundary + no meaning flip.
                                var sem = await judge.VerifyAsync(r5Text, mustKeep, mustNotClaim, CancellationToken.None);
                                q1 = sem.Error is null && sem.FactsReallyPass && sem.RealForbidden == 0 && !sem.MeaningChanged;
                                q1Issues = sem.Error is null
                                    ? sem.Facts.Where(f => f.Status is "missing" or "contradicted").Select(f => $"{f.Status}:{f.Fact}").ToList()
                                    : new List<string> { $"judge_error:{sem.Error}" };

                                // Q2 understandable (translationese ignored).
                                var u = await understandability.JudgeAsync(r5Text, CancellationToken.None);
                                q2 = u.Understandable;
                                q2Issues = u.Issues;

                                // Q3 native-send-ready: recorded only, NOT a pass condition.
                                var tier = await tierJudge.JudgeAsync(r5Text, CancellationToken.None);
                                q3 = tier.Tier == "sendable";

                                fallbackReason = q1 && q2 ? string.Empty : (!q1 ? "Q1_fact_unsafe" : "Q2_not_understandable");
                            }
                        }
                    }
                }
            }

            var accepted = r5Generated && q1 && q2;
            var chosen = accepted ? "R5" : "T0";

            int? pangramT0 = null;
            int? pangramR5 = null;
            if (pangram is not null)
            {
                if (pangramCalls < pangramMaxCalls)
                {
                    pangramT0 = await MeasureAsync(pangram, t0Text);
                    pangramCalls++;
                }

                if (accepted && !string.Equals(r5Text, t0Text, StringComparison.Ordinal) && pangramCalls < pangramMaxCalls)
                {
                    pangramR5 = await MeasureAsync(pangram, r5Text);
                    pangramCalls++;
                }
                else
                {
                    pangramR5 = pangramT0;
                }
            }

            int? delta = pangramT0.HasValue && pangramR5.HasValue ? pangramR5.Value - pangramT0.Value : null;

            rows.Add(new PilotV4Row(
                sample.Id, sample.CaseNumber, sample.Category, tone,
                true, t0Text,
                sentinelPass, masked.AnchorCount, taRaw, r5Text, r5Generated,
                driftCount, patchCount, factPct, stylePct, fullSent,
                q1, q2, q3, q1Issues, q2Issues,
                pangramT0, pangramR5, delta,
                chosen, accepted ? string.Empty : fallbackReason,
                Notes(accepted, fallbackReason, delta, q3)));

            Console.WriteLine(
                $"{sample.Id}: sentinel={(sentinelPass ? "ok" : "BRK")} drift={driftCount} patches={patchCount} "
                + $"fact={factPct}% style={stylePct}% Q1={q1} Q2={q2} Q3={q3} accepted={accepted}"
                + $"{(accepted ? "" : ":" + fallbackReason)} pangram T0={Fmt(pangramT0)} R5={Fmt(pangramR5)} delta={Fmt(delta)}");
        }

        var summary = PilotV4Summary.Create(startedAt, DateTimeOffset.UtcNow, rows, translationRounds,
            youdao.CallCount, pangramCalls, deepseek.CallCount, modelCounter.CallCount, saplingCounter.CallCount, pangramEnabled);

        var stamp = startedAt.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(config.OutputDirectory);
        var suffix = translationRounds > 1 ? $"r5b-{translationRounds}round" : "r5a";
        var jsonPath = Path.Combine(config.OutputDirectory, $"{stamp}-{suffix}-fact-repair-pilot.json");
        var mdPath = Path.Combine(config.OutputDirectory, $"{stamp}-{suffix}-fact-repair-pilot.md");
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(new { summary, rows }, jsonOptions));
        await File.WriteAllTextAsync(mdPath, PilotV4Report.Render(summary, rows));

        Console.WriteLine($"Wrote {jsonPath}");
        Console.WriteLine($"Wrote {mdPath}");
        Console.WriteLine(summary.OneLine());
        return 0;
    }

    private static async Task<int?> MeasureAsync(IWritingSignalClient client, string text)
    {
        var r = await client.MeasureAsync(text, CancellationToken.None);
        return r.Available ? r.AiLikePercent : null;
    }

    private static PilotV4Row EmptyRow(EvalCase sample, string tone, string? errorCode) => new(
        sample.Id, sample.CaseNumber, sample.Category, tone,
        false, string.Empty,
        false, 0, string.Empty, string.Empty, false,
        0, 0, 0, 0, 0,
        false, false, false, Array.Empty<string>(), Array.Empty<string>(),
        null, null, null,
        "T0", $"t0_no_output:{errorCode ?? "unknown"}",
        "T0 produced no output; R5 not attempted.");

    private static string Notes(bool accepted, string fallbackReason, int? delta, bool q3)
    {
        if (!accepted)
        {
            return $"R5 fell back to T0 ({fallbackReason}).";
        }

        var pangramNote = delta is null ? "Pangram not measured"
            : delta < 0 ? $"Pangram dropped {Math.Abs(delta.Value)} pts"
            : delta > 0 ? $"Pangram rose {delta.Value} pts"
            : "Pangram unchanged";
        return $"R5 accepted (fact-safe + understandable; native-send-ready={q3}); {pangramNote} vs T0.";
    }

    private static string? FirstEnv(params string[] names) =>
        names.Select(Environment.GetEnvironmentVariable).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static int IntEnv(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var v) && v >= 0 ? v : fallback;

    private static string Fmt(int? v) => v?.ToString() ?? "-";
}

internal sealed record PilotV4Summary(
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    int TranslationRounds,
    int Cases,
    int T0WithOutput,
    int SentinelPass,
    int R5Generated,
    int Q1Pass,
    int Q2Pass,
    int Q1AndQ2Pass,
    int Q3NativeSendReady,
    int Fallback,
    IReadOnlyDictionary<string, int> FallbackReasons,
    int PangramPairs,
    int PangramLower,
    int PangramHigher,
    int PangramEqual,
    int? MeanDelta,
    int? MedianDelta,
    int? MedianFactPatchPct,
    bool PangramEnabled,
    int YoudaoCalls,
    int PangramCalls,
    int DeepSeekCalls,
    int ModelCalls,
    int SaplingCalls)
{
    public static PilotV4Summary Create(
        DateTimeOffset startedAt, DateTimeOffset finishedAt, IReadOnlyList<PilotV4Row> rows, int translationRounds,
        int youdaoCalls, int pangramCalls, int deepseekCalls, int modelCalls, int saplingCalls, bool pangramEnabled)
    {
        var withOutput = rows.Where(r => r.T0HasOutput).ToList();
        var accepted = withOutput.Where(r => r.Q1FactSafe && r.Q2Understandable).ToList();
        var pairs = accepted.Where(r => r.DeltaR5MinusT0.HasValue).Select(r => r.DeltaR5MinusT0!.Value).ToList();
        var fallbackReasons = withOutput.Where(r => !(r.Q1FactSafe && r.Q2Understandable))
            .GroupBy(r => r.FallbackReason).ToDictionary(g => g.Key, g => g.Count());
        var factPcts = accepted.Select(r => r.FactPatchCharRatioPct).OrderBy(x => x).ToList();

        return new PilotV4Summary(
            startedAt, finishedAt, translationRounds,
            rows.Count,
            withOutput.Count,
            rows.Count(r => r.SentinelPass),
            rows.Count(r => r.R5Generated),
            rows.Count(r => r.Q1FactSafe),
            rows.Count(r => r.Q2Understandable),
            accepted.Count,
            rows.Count(r => (r.Q1FactSafe && r.Q2Understandable) && r.Q3NativeSendReady),
            withOutput.Count(r => !(r.Q1FactSafe && r.Q2Understandable)),
            fallbackReasons,
            pairs.Count,
            pairs.Count(d => d < 0),
            pairs.Count(d => d > 0),
            pairs.Count(d => d == 0),
            pairs.Count == 0 ? null : (int?)Math.Round(pairs.Average(), MidpointRounding.AwayFromZero),
            Median(pairs),
            factPcts.Count == 0 ? null : factPcts[factPcts.Count / 2],
            pangramEnabled,
            youdaoCalls, pangramCalls, deepseekCalls, modelCalls, saplingCalls);
    }

    private static int? Median(IReadOnlyList<int> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (int)Math.Round((sorted[mid - 1] + sorted[mid]) / 2.0, MidpointRounding.AwayFromZero);
    }

    public string OneLine() =>
        $"R5 pilot (rounds={TranslationRounds}): T0out={T0WithOutput}/{Cases}, sentinel={SentinelPass}/{Cases}, r5gen={R5Generated}, "
        + $"Q1={Q1Pass}/{Cases}, Q2={Q2Pass}/{Cases}, Q1+Q2={Q1AndQ2Pass}/{Cases}, Q3(native)={Q3NativeSendReady}, "
        + $"pangramPairs={PangramPairs} (lower={PangramLower}/higher={PangramHigher}/equal={PangramEqual}), "
        + $"meanDelta={(MeanDelta?.ToString() ?? "n/a")}, medianDelta={(MedianDelta?.ToString() ?? "n/a")}, "
        + $"medFactPatch%={(MedianFactPatchPct?.ToString() ?? "n/a")}, youdao={YoudaoCalls}, pangram={PangramCalls}, deepseek={DeepSeekCalls}";
}

internal static class PilotV4Report
{
    public static string Render(PilotV4Summary s, IReadOnlyList<PilotV4Row> rows)
    {
        var lines = new List<string>
        {
            $"# R5 fact-only-repair translation pilot (round 5{(s.TranslationRounds > 1 ? $", {s.TranslationRounds}-round / R5B" : " / R5A")})",
            "",
            "**Eval-only research pilot.** Not wired into production. T0 = production baseline. TA_raw = Youdao en→zh-CHS→en perturbation. R5 = TA_raw with FACT-ONLY patches (drift located + patched from Original/T0/ledger; translationese deliberately left in).",
            "**Relaxed bar:** Q1 fact-safe + Q2 understandable are required; Q3 native-send-ready is RECORDED ONLY. So R5 may keep MT-flavored prose. Producing fact-safe-but-MT-flavored text is detection-chasing and off-brand for \"Reply In My Voice\" — this run is for empirical closure, not a product path.",
            "",
            $"Started: {s.StartedAt:O}  Finished: {s.FinishedAt:O}",
            "",
            "## Headline — does fact-only repair keep the Pangram drop while staying fact-safe + understandable?",
            "",
        };

        if (!s.PangramEnabled)
        {
            lines.Add("Pangram disabled — only the Q1/Q2 gate ran.");
        }
        else if (s.PangramPairs == 0)
        {
            lines.Add("No R5 candidate passed Q1+Q2 as a distinct output → no T0-vs-R5 Pangram pair.");
        }
        else
        {
            lines.Add($"- R5 passed Q1 (fact-safe) + Q2 (understandable): **{s.Q1AndQ2Pass}/{s.Cases}** (Q1 {s.Q1Pass}, Q2 {s.Q2Pass}). Of these, native-send-ready (Q3): **{s.Q3NativeSendReady}**.");
            lines.Add($"- Pangram of the {s.PangramPairs} accepted: **lower {s.PangramLower}**, higher {s.PangramHigher}, equal {s.PangramEqual} (mean Δ **{Signed(s.MeanDelta)}**, median **{Signed(s.MedianDelta)}**). Median fact-patch char ratio: **{s.MedianFactPatchPct?.ToString() ?? "n/a"}%**.");
            lines.Add($"- Reference: round-1 raw-TA mean Δ −76 (unreadable); round-2 T3 +4 (≈T0). R5 sits where it sits BY accepting translationese (Q3 native-send-ready = {s.Q3NativeSendReady}/{s.Q1AndQ2Pass} of accepted).");
        }

        lines.Add("");
        lines.Add("## Round-5 success criteria (§11)");
        lines.Add("");
        lines.Add($"- Q1 fact-safe ≥8/10: **{s.Q1Pass}/10** {(s.Q1Pass >= 8 ? "✓" : "✗")}");
        lines.Add($"- Q2 understandable ≥8/10: **{s.Q2Pass}/10** {(s.Q2Pass >= 8 ? "✓" : "✗")}");
        lines.Add($"- Pangram win ≥6 among Q1+Q2 pass: **{s.PangramLower}/{s.PangramPairs}** {(s.PangramLower >= 6 ? "✓" : "✗")}");
        lines.Add($"- Median delta ≤ −40: **{Signed(s.MedianDelta)}** {(s.MedianDelta is int md && md <= -40 ? "✓" : "✗")}");
        lines.Add($"- Median fact-patch ratio ≤20%: **{s.MedianFactPatchPct?.ToString() ?? "n/a"}%** {(s.MedianFactPatchPct is int fp && fp <= 20 ? "✓" : "✗")}");
        lines.Add("");
        lines.Add("## Gate / safety");
        lines.Add("");
        lines.Add($"- T0 with output: **{s.T0WithOutput}/{s.Cases}** · sentinel held: **{s.SentinelPass}/{s.Cases}** · R5 generated: **{s.R5Generated}**");
        lines.Add($"- Fell back to T0: **{s.Fallback}/{s.T0WithOutput}**"
            + (s.FallbackReasons.Count > 0 ? " — " + string.Join(", ", s.FallbackReasons.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}×{kv.Value}")) : string.Empty));
        lines.Add("");
        lines.Add("## Cost / calls");
        lines.Add("");
        lines.Add($"Youdao: **{s.YoudaoCalls}** · Pangram: **{s.PangramCalls}** · DeepSeek (drift+Q2+Q3+judge): **{s.DeepSeekCalls}** · DeepSeek model (T0): **{s.ModelCalls}** · Sapling (engine gate): **{s.SaplingCalls}**");
        lines.Add("");
        lines.Add("## Per-case");
        lines.Add("");
        lines.Add("| Case | Category | Sentinel | Drift | Patches | Fact% | Style% | Q1 | Q2 | Q3 | Pangram T0 | Pangram R5 | Δ | Chosen | Fallback |");
        lines.Add("| --- | --- | :---: | ---: | ---: | ---: | ---: | :---: | :---: | :---: | ---: | ---: | ---: | :---: | --- |");
        foreach (var r in rows)
        {
            lines.Add(string.Join(" | ", new[]
            {
                r.CaseId, r.Category,
                r.SentinelPass ? "ok" : "broken",
                r.DriftAssertionsCount.ToString(),
                r.PatchCount.ToString(),
                r.FactPatchCharRatioPct + "%",
                r.StylePatchCharRatioPct + "%",
                r.Q1FactSafe ? "yes" : "no",
                r.Q2Understandable ? "yes" : "no",
                r.Q3NativeSendReady ? "yes" : "no",
                r.PangramT0?.ToString() ?? "-",
                r.PangramR5?.ToString() ?? "-",
                Signed(r.DeltaR5MinusT0),
                r.ChosenVariant,
                (r.ChosenVariant == "R5" ? "" : r.FallbackReason).Replace("|", "/", StringComparison.Ordinal),
            }));
        }

        lines.Add("");
        lines.Add("## Text comparison (T0 → TA_raw → R5)");
        foreach (var r in rows.Where(r => r.T0HasOutput))
        {
            lines.Add("");
            lines.Add($"### {r.CaseId} — {r.Category} (chosen: {r.ChosenVariant}; Q1={r.Q1FactSafe} Q2={r.Q2Understandable} Q3={r.Q3NativeSendReady})");
            lines.Add("");
            lines.Add("**T0 (baseline):**");
            lines.Add("> " + r.T0Text.Replace("\n", "\n> ", StringComparison.Ordinal));
            if (r.SentinelPass)
            {
                lines.Add("");
                lines.Add("**TA_raw (Youdao round-trip):**");
                lines.Add("> " + r.TaRawText.Replace("\n", "\n> ", StringComparison.Ordinal));
            }

            if (r.R5Generated)
            {
                lines.Add("");
                lines.Add($"**R5 ({r.PatchCount} fact patches, {r.FactPatchCharRatioPct}% replaced):**");
                lines.Add("> " + r.R5Text.Replace("\n", "\n> ", StringComparison.Ordinal));
            }
        }

        lines.Add("");
        return string.Join("\n", lines) + "\n";
    }

    private static string Signed(int? v) => v is null ? "-" : v.Value > 0 ? $"+{v.Value}" : v.Value.ToString();
}
