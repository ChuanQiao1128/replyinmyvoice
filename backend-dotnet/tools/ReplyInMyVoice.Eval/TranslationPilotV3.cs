using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.RewriteEngine;
using ReplyInMyVoice.Infrastructure.Providers;

// Round-3 translation pilot (T4 selective-patch). EVAL-ONLY; triggered by T4_PILOT=1 in Program.cs.
// See plans/translation-roundtrip-pilot.md + the round-3 design.
//
// Round-2 (T3) re-authored the whole email in native English -> reverted to ~T0 -> Pangram climbed
// back. Round-3 hypothesis: the Pangram drop lives in the MT surface, so KEEP TA_raw and only patch
// the broken spans. DeepSeek may DIAGNOSE and PROPOSE patches but never emits a whole email — the
// PROGRAM applies small, budgeted find/replace patches onto TA_raw.
//   T0  = production baseline (engine v0, internal Sapling gate, no translation)
//   TA_raw = T0 -> extended mask (protection-forward) -> Youdao en->zh-CHS->en -> unmask (draft only)
//   T4  = TA_raw -> deterministic cleanup -> apply budgeted span patches (<=8, <=12% chars,
//         <=30% new-writing, no 2-sentence patch); final IF a 3-tier gate says sendable/minor + safe
// Pangram is read once per gate survivor. The report separates "Pangram lowered" from "send-ready"
// and shows where T4 lands between raw-TA (~0-14) and T0 (~99).

internal sealed record Patch(string Find, string Replace, string Reason, string GateType, string Source);

internal sealed record PatchApplyResult(
    string Text, int Proposed, int Applied, int ReplacedChars, double ReplacedRatio,
    double NewWritingRatio, bool BudgetExceeded, string Reason);

// Applies budgeted span patches onto TA_raw. The budget is what preserves the translation
// perturbation: too many / too large / too much new-writing => TA_raw was too broken => fall back
// to T0 instead of letting patches become a full rewrite.
internal static class PatchApplier
{
    private const int MaxPatches = 8;
    private const double MaxReplacedRatio = 0.12;
    private const int MaxFindLen = 120;
    private const int MaxReplaceLen = 140;
    private const double MaxNewWritingRatio = 0.30;

    private static readonly Regex SentenceBoundary = new(@"[.!?][""')\]]?\s+[A-Z0-9]", RegexOptions.Compiled);

    public static PatchApplyResult Apply(string taRaw, IReadOnlyList<Patch> patches, string t0, string originalDraft)
    {
        var valid = patches
            .Where(p => !string.IsNullOrEmpty(p.Find)
                && p.Find.Length <= MaxFindLen
                && (p.Replace?.Length ?? 0) <= MaxReplaceLen
                && !SpansMultipleSentences(p.Find)) // a single patch may not eat a whole extra sentence
            .ToList();

        // "Needs >8 surgical patches" means TA_raw is too broken to patch -> fall back.
        if (valid.Count > MaxPatches)
        {
            return new PatchApplyResult(taRaw, patches.Count, 0, 0, 0, 0, true, "too_many_patches");
        }

        var working = taRaw;
        var replacedChars = 0;
        var newChars = 0;
        var applied = 0;
        foreach (var p in valid)
        {
            var idx = working.IndexOf(p.Find, StringComparison.Ordinal);
            if (idx < 0)
            {
                continue; // find no longer present (earlier patch changed it, or model hallucinated it)
            }

            working = string.Concat(working.AsSpan(0, idx), p.Replace, working.AsSpan(idx + p.Find.Length));
            replacedChars += p.Find.Length;
            if (IsNewWriting(p.Replace, t0, originalDraft))
            {
                newChars += p.Replace?.Length ?? 0;
            }

            applied++;
        }

        var replacedRatio = (double)replacedChars / Math.Max(1, taRaw.Length);
        var newRatio = replacedChars == 0 ? 0 : (double)newChars / replacedChars;
        if (replacedRatio > MaxReplacedRatio)
        {
            return new PatchApplyResult(working, patches.Count, applied, replacedChars, replacedRatio, newRatio, true, "replaced_ratio_exceeded");
        }

        if (newRatio > MaxNewWritingRatio)
        {
            return new PatchApplyResult(working, patches.Count, applied, replacedChars, replacedRatio, newRatio, true, "new_writing_ratio_exceeded");
        }

        return new PatchApplyResult(working, patches.Count, applied, replacedChars, replacedRatio, newRatio, false, string.Empty);
    }

    // New-writing = a replacement not copied (as a span) from T0 or the original draft. Whitespace /
    // very short replacements (punctuation, casing) are treated as non-new (they carry no LLM signal).
    private static bool IsNewWriting(string? replace, string t0, string originalDraft)
    {
        var trimmed = (replace ?? string.Empty).Trim();
        if (trimmed.Length <= 2)
        {
            return false;
        }

        return !t0.Contains(trimmed, StringComparison.Ordinal) && !originalDraft.Contains(trimmed, StringComparison.Ordinal);
    }

    private static bool SpansMultipleSentences(string find) => SentenceBoundary.IsMatch(find);
}

// Proposes surgical patches (NOT a rewrite). DeepSeek diagnoses; the program applies.
internal sealed class PatchProposer(DeepSeekChatClient chat)
{
    private const string SystemPrompt =
        "You are diagnosing a MACHINE-TRANSLATED email so a human can send it. You do NOT rewrite it. "
        + "Propose a SMALL set of surgical find/replace patches that fix ONLY: (a) factual errors or "
        + "mistranslated business nouns (e.g. 'tea tray' -> 'saucer'), (b) boundary/meaning errors "
        + "(uncertainty turned into a promise, a changed amount/date, wrong liability), (c) clearly "
        + "unsendable fragments (garbled clauses, wrong subject/agent, scrambled phone/time, broken "
        + "sign-off). LEAVE slightly-awkward-but-understandable phrasing ALONE — that natural variation "
        + "is wanted and must survive. Each patch: \"find\" = an EXACT substring of the MT text (<=120 "
        + "chars); \"replace\" (<=140 chars) = PREFERABLY an exact span copied from the ORIGINAL MESSAGE "
        + "or the T0 BASELINE, only writing new words for a collocation or sign-off fix; \"reason\"; "
        + "\"gate_type\" one of fact|business_noun|boundary|fluency|signature; \"source\" one of "
        + "t0|original|new. At most 8 patches — do not try to fix everything. Return JSON: "
        + "{\"patches\":[{\"find\":\"...\",\"replace\":\"...\",\"reason\":\"...\",\"gate_type\":\"...\",\"source\":\"...\"}]}.";

    public async Task<IReadOnlyList<Patch>> ProposeAsync(
        string taRaw, string t0, string originalDraft, IReadOnlyList<string> protectedTerms, CancellationToken cancellationToken)
    {
        var user =
            "ORIGINAL MESSAGE (fact source of truth):\n" + originalDraft
            + "\n\nT0 BASELINE (clean reference — copy replacement spans from here or the original):\n" + t0
            + "\n\nPROTECTED TERMS (must appear exactly in the final text):\n"
            + (protectedTerms.Count == 0 ? "(none)" : string.Join("\n", protectedTerms.Select(t => "- " + t)))
            + "\n\nMACHINE-TRANSLATED TEXT TO PATCH:\n" + taRaw;

        var content = await chat.CompleteAsync(SystemPrompt, user, 1600, 0, cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<Patch>();
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("patches", out var arr) || arr.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<Patch>();
            }

            return arr.EnumerateArray()
                .Select(p => new Patch(
                    Str(p, "find"), Str(p, "replace"), Str(p, "reason"), Str(p, "gate_type"), Str(p, "source")))
                .Where(p => p.Find.Length > 0)
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<Patch>();
        }
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
}

internal sealed record TierVerdict(string Tier, IReadOnlyList<string> Issues, string? Error = null)
{
    public bool Acceptable => Tier is "sendable" or "minor";
}

// 3-tier sendability: pass (sendable), minor_awkward_but_sendable (minor), fail (unsendable). T4
// intentionally tolerates "minor" so it does not get patched all the way back to T0.
internal sealed class SendabilityTierJudge(DeepSeekChatClient chat) : ReplyInMyVoice.Domain.Quality.ISendabilityJudge
{
    private const string SystemPrompt =
        "You rate whether an email is send-ready, ALLOWING slight awkwardness. Return tier: 'sendable' "
        + "(clean, natural English), 'minor' (slightly awkward phrasing or a non-idiomatic preposition, "
        + "but a busy professional could send it as-is without embarrassment), or 'unsendable' (garbled "
        + "or scrambled clauses, wrong subject/agent, broken or nonsensical sign-off, scrambled "
        + "phone/time/dates, or it obviously reads like raw machine translation). Judge fluency only, "
        + "not facts. Return JSON: {\"tier\":\"sendable|minor|unsendable\",\"issues\":[\"...\"]}.";

    public async Task<TierVerdict> JudgeAsync(string text, CancellationToken cancellationToken)
    {
        var content = await chat.CompleteAsync(SystemPrompt, text, 500, 0, cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new TierVerdict("unsendable", Array.Empty<string>(), "judge_failed");
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var tier = (root.TryGetProperty("tier", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null)
                ?.Trim().ToLowerInvariant() ?? "unsendable";
            if (tier is not ("sendable" or "minor" or "unsendable"))
            {
                tier = "unsendable";
            }

            var issues = root.TryGetProperty("issues", out var iss) && iss.ValueKind == JsonValueKind.Array
                ? iss.EnumerateArray().Select(i => i.GetString() ?? string.Empty).Where(i => i.Length > 0).ToList()
                : new List<string>();
            return new TierVerdict(tier, issues);
        }
        catch (JsonException)
        {
            return new TierVerdict("unsendable", Array.Empty<string>(), "judge_parse_failed");
        }
    }

    // Adapts the string-tier verdict to the Domain ISendabilityJudge contract so the LLM sendability
    // tier can plug into the quality gate chain alongside the deterministic SendabilityGate.
    async Task<ReplyInMyVoice.Domain.Quality.SendabilityGateResult>
        ReplyInMyVoice.Domain.Quality.ISendabilityJudge.JudgeAsync(string text, CancellationToken cancellationToken)
    {
        var verdict = await JudgeAsync(text, cancellationToken);
        return new ReplyInMyVoice.Domain.Quality.SendabilityGateResult(
            ReplyInMyVoice.Domain.Quality.SendabilityGate.ParseTier(verdict.Tier),
            verdict.Issues.Select(i => new ReplyInMyVoice.Domain.Quality.SendabilityIssue("llm", i)).ToList());
    }
}

// Conservative, deterministic fixes applied to TA_raw before LLM patching: collapse extra spaces,
// remove spaces before punctuation, and normalize the common MT sign-off artifact "The best".
internal static class DeterministicCleanup
{
    private static readonly Regex MultiSpace = new(@"[ \t]{2,}", RegexOptions.Compiled);
    private static readonly Regex SpaceBeforePunct = new(@" +([.,;:!?])", RegexOptions.Compiled);
    private static readonly Regex TrailingTheBest = new(@"(?im)^\s*the best\.?\s*$", RegexOptions.Compiled);

    public static string Clean(string text)
    {
        var t = MultiSpace.Replace(text, " ");
        t = SpaceBeforePunct.Replace(t, "$1");
        t = TrailingTheBest.Replace(t, "Best,");
        return t;
    }
}

internal sealed record PilotV3Row(
    string CaseId,
    int CaseNumber,
    string Category,
    string Tone,
    bool T0HasOutput,
    string T0Text,
    bool SentinelPass,
    int AnchorCount,
    int ProtectedTermCount,
    string TaRawText,
    int PatchesProposed,
    int PatchesApplied,
    int ReplacedRatioPct,
    int NewWritingRatioPct,
    bool BudgetExceeded,
    bool T4Generated,
    string T4Text,
    bool FactPass,
    int Forbid,
    bool MeaningChanged,
    IReadOnlyList<string> ProtectedMissing,
    string Tier,
    IReadOnlyList<string> SendIssues,
    bool T4Accepted,
    int? PangramT0,
    int? PangramT4,
    int? DeltaT4MinusT0,
    string ChosenVariant,
    string FallbackReason,
    IReadOnlyList<string> MissingFacts,
    string Notes);

internal static class TranslationPilotV3Runner
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
            Console.Error.WriteLine("T4_PILOT: missing Youdao credentials (YOUDAO_APP_KEY/AppID + YOUDAO_APP_SECRET/AppSecret).");
            return 2;
        }

        var youdaoMaxCalls = IntEnv("YOUDAO_MAX_CALLS", 40);
        var pangramMaxCalls = IntEnv("PANGRAM_MAX_CALLS", 0);
        var pangramKey = Environment.GetEnvironmentVariable("PANGRAM_API_KEY");
        var pangramEnabled = pangramMaxCalls > 0 && !string.IsNullOrWhiteSpace(pangramKey);

        using var youdaoHttp = new HttpClient();
        using var deepseekHttp = new HttpClient();
        using var judgeHttp = new HttpClient();
        using var pangramHttp = new HttpClient();

        var youdao = new YoudaoTranslationClient(youdaoHttp, youdaoKey!, youdaoSecret!, youdaoUrl, TimeSpan.FromSeconds(30));
        var deepseek = new DeepSeekChatClient(deepseekHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));
        var termProposer = new ProtectedTermProposer(deepseek);
        var patchProposer = new PatchProposer(deepseek);
        var tierJudge = new SendabilityTierJudge(deepseek);
        var judge = new SemanticEvalJudge(judgeHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));
        var pangram = pangramEnabled
            ? new PangramWritingSignalClient(pangramHttp, pangramKey!, TimeSpan.FromSeconds(45))
            : null;

        Console.WriteLine(
            $"T4 pilot: cases={cases.Count} youdaoMax={youdaoMaxCalls} pangram={(pangramEnabled ? $"on(max {pangramMaxCalls})" : "off")} model={config.Model}");

        var rows = new List<PilotV3Row>();
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
            var proposedTerms = await termProposer.ProposeAsync(sample.InputDraft, CancellationToken.None);
            var protectedTerms = BuildProtectedTerms(t0Text, ledger, proposedTerms);

            // Protection-forward, but conservative: each extra mask adds sentinel-break risk (masking
            // 15 extra spans broke every sentinel in validation). Cap at a few short single-word
            // business nouns; the patch step fixes any remaining noun drift, which is its whole job.
            var maskExtra = protectedTerms
                .Where(t => t.Length is >= 3 and <= 20 && !t.Contains(' '))
                .OrderBy(t => t.Length).Take(3).ToList();
            var masked = AnchorMasker.Mask(t0Text, ledger, mustKeep, mustNotClaim, maskExtra);

            var taRaw = string.Empty;
            var sentinelPass = false;
            var patchesProposed = 0;
            var patchesApplied = 0;
            var replacedPct = 0;
            var newPct = 0;
            var budgetExceeded = false;
            var t4Generated = false;
            var t4Text = string.Empty;
            var factPass = false;
            var forbid = 0;
            var meaning = false;
            IReadOnlyList<string> protectedMissing = Array.Empty<string>();
            var tier = "unsendable";
            IReadOnlyList<string> sendIssues = Array.Empty<string>();
            IReadOnlyList<string> missingFacts = Array.Empty<string>();
            string fallbackReason;

            if (youdao.CallCount + 2 > youdaoMaxCalls)
            {
                fallbackReason = "youdao_budget_exhausted";
            }
            else
            {
                var toZh = await youdao.TranslateAsync(masked.MaskedText, "en", "zh-CHS", CancellationToken.None);
                var backEn = toZh.Success
                    ? await youdao.TranslateAsync(toZh.Text, "zh-CHS", "en", CancellationToken.None)
                    : new TranslateResult(false, string.Empty, toZh.ErrorCode);
                if (!backEn.Success)
                {
                    fallbackReason = backEn.ErrorCode ?? "youdao_failed";
                }
                else
                {
                    var unmask = AnchorMasker.Unmask(backEn.Text, masked.Map);
                    taRaw = unmask.Restored;
                    sentinelPass = unmask.IntegrityOk;
                    if (!sentinelPass)
                    {
                        fallbackReason = "sentinel_broken";
                    }
                    else
                    {
                        var cleaned = DeterministicCleanup.Clean(taRaw);
                        var patches = await patchProposer.ProposeAsync(cleaned, t0Text, sample.InputDraft, protectedTerms, CancellationToken.None);
                        var apply = PatchApplier.Apply(cleaned, patches, t0Text, sample.InputDraft);
                        patchesProposed = apply.Proposed;
                        patchesApplied = apply.Applied;
                        replacedPct = (int)Math.Round(apply.ReplacedRatio * 100);
                        newPct = (int)Math.Round(apply.NewWritingRatio * 100);
                        budgetExceeded = apply.BudgetExceeded;

                        if (apply.BudgetExceeded)
                        {
                            // Per design: over budget => TA_raw too broken => fall back, do NOT full-repair.
                            fallbackReason = "patch_budget:" + apply.Reason;
                        }
                        else
                        {
                            t4Generated = true;
                            t4Text = apply.Text;

                            protectedMissing = protectedTerms
                                .Where(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 3
                                    && !t4Text.Contains(t, StringComparison.Ordinal))
                                .ToList();
                            var sem = await judge.VerifyAsync(t4Text, mustKeep, mustNotClaim, CancellationToken.None);
                            var tierVerdict = await tierJudge.JudgeAsync(t4Text, CancellationToken.None);
                            factPass = sem.Error is null && sem.FactsReallyPass;
                            forbid = sem.RealForbidden;
                            meaning = sem.MeaningChanged;
                            tier = tierVerdict.Tier;
                            sendIssues = tierVerdict.Issues;
                            missingFacts = sem.Error is null
                                ? sem.Facts.Where(f => f.Status is "missing" or "contradicted").Select(f => $"{f.Status}:{f.Fact}").ToList()
                                : new List<string> { $"judge_error:{sem.Error}" };

                            fallbackReason =
                                !tierVerdict.Acceptable ? "t4_unsendable"
                                : protectedMissing.Count > 0 ? "t4_protected_lost"
                                : !factPass ? "t4_facts_drifted"
                                : forbid > 0 ? "t4_forbidden_violation"
                                : meaning ? "t4_meaning_changed"
                                : string.Empty;
                        }
                    }
                }
            }

            var accepted = t4Generated && string.IsNullOrEmpty(fallbackReason);
            var chosen = accepted ? "T4" : "T0";

            int? pangramT0 = null;
            int? pangramT4 = null;
            if (pangram is not null)
            {
                if (pangramCalls < pangramMaxCalls)
                {
                    pangramT0 = await MeasureAsync(pangram, t0Text);
                    pangramCalls++;
                }

                if (accepted && !string.Equals(t4Text, t0Text, StringComparison.Ordinal) && pangramCalls < pangramMaxCalls)
                {
                    pangramT4 = await MeasureAsync(pangram, t4Text);
                    pangramCalls++;
                }
                else
                {
                    pangramT4 = pangramT0;
                }
            }

            int? delta = pangramT0.HasValue && pangramT4.HasValue ? pangramT4.Value - pangramT0.Value : null;

            rows.Add(new PilotV3Row(
                sample.Id, sample.CaseNumber, sample.Category, tone,
                true, t0Text,
                sentinelPass, masked.AnchorCount, protectedTerms.Count, taRaw,
                patchesProposed, patchesApplied, replacedPct, newPct, budgetExceeded,
                t4Generated, t4Text,
                factPass, forbid, meaning, protectedMissing, tier, sendIssues,
                accepted,
                pangramT0, pangramT4, delta,
                chosen, accepted ? string.Empty : fallbackReason,
                missingFacts,
                Notes(accepted, fallbackReason, delta, tier)));

            Console.WriteLine(
                $"{sample.Id}: anchors={masked.AnchorCount} prot={protectedTerms.Count} sentinel={(sentinelPass ? "ok" : "BROKEN")} "
                + $"patches={patchesApplied}/{patchesProposed} repl={replacedPct}% new={newPct}% tier={tier} "
                + $"accepted={accepted}{(accepted ? "" : ":" + fallbackReason)} pangram T0={Fmt(pangramT0)} T4={Fmt(pangramT4)} delta={Fmt(delta)}");
        }

        var summary = PilotV3Summary.Create(startedAt, DateTimeOffset.UtcNow, rows,
            youdao.CallCount, pangramCalls, deepseek.CallCount, modelCounter.CallCount, saplingCounter.CallCount, pangramEnabled);

        var stamp = startedAt.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(config.OutputDirectory);
        var jsonPath = Path.Combine(config.OutputDirectory, $"{stamp}-t4-translation-pilot.json");
        var mdPath = Path.Combine(config.OutputDirectory, $"{stamp}-t4-translation-pilot.md");
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(new { summary, rows }, jsonOptions));
        await File.WriteAllTextAsync(mdPath, PilotV3Report.Render(summary, rows));

        Console.WriteLine($"Wrote {jsonPath}");
        Console.WriteLine($"Wrote {mdPath}");
        Console.WriteLine(summary.OneLine());
        return 0;
    }

    private static List<string> BuildProtectedTerms(string t0Text, RewriteFactLedger ledger, IReadOnlyList<string> proposed)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fact in ledger.Facts)
        {
            if ((fact.Category is RewriteFactCategory.Amount or RewriteFactCategory.Identifier
                    or RewriteFactCategory.DateOrDeadline or RewriteFactCategory.Person or RewriteFactCategory.Count)
                && t0Text.Contains(fact.Text, StringComparison.Ordinal))
            {
                set.Add(fact.Text.Trim());
            }
        }

        foreach (var term in proposed)
        {
            set.Add(term);
        }

        return set.Where(t => t.Length >= 2).Take(30).ToList();
    }

    private static async Task<int?> MeasureAsync(IWritingSignalClient client, string text)
    {
        var result = await client.MeasureAsync(text, CancellationToken.None);
        return result.Available ? result.AiLikePercent : null;
    }

    private static PilotV3Row EmptyRow(EvalCase sample, string tone, string? errorCode) => new(
        sample.Id, sample.CaseNumber, sample.Category, tone,
        false, string.Empty,
        false, 0, 0, string.Empty,
        0, 0, 0, 0, false,
        false, string.Empty,
        false, 0, false, Array.Empty<string>(), "unsendable", Array.Empty<string>(),
        false,
        null, null, null,
        "T0", $"t0_no_output:{errorCode ?? "unknown"}",
        Array.Empty<string>(),
        "T0 produced no output; T4 not attempted.");

    private static string Notes(bool accepted, string fallbackReason, int? delta, string tier)
    {
        if (!accepted)
        {
            return $"T4 fell back to T0 ({fallbackReason}).";
        }

        var pangramNote = delta is null ? "Pangram not measured"
            : delta < 0 ? $"Pangram dropped {Math.Abs(delta.Value)} pts"
            : delta > 0 ? $"Pangram rose {delta.Value} pts"
            : "Pangram unchanged";
        return $"T4 chosen (tier={tier}); {pangramNote} vs T0.";
    }

    private static string? FirstEnv(params string[] names) =>
        names.Select(Environment.GetEnvironmentVariable).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static int IntEnv(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var v) && v >= 0 ? v : fallback;

    private static string Fmt(int? v) => v?.ToString() ?? "-";
}

internal sealed record PilotV3Summary(
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    int Cases,
    int T0WithOutput,
    int SentinelPass,
    int T4Generated,
    int T4Accepted,
    int TierSendable,
    int TierMinor,
    int Fallback,
    IReadOnlyDictionary<string, int> FallbackReasons,
    int AvgReplacedPct,
    int AvgPatchesApplied,
    int PangramPairs,
    int PangramLower,
    int PangramHigher,
    int PangramEqual,
    int PangramLowerAndAccepted,
    int? MeanDelta,
    int? MedianDelta,
    int? MeanT0,
    int? MeanT4,
    bool PangramEnabled,
    int YoudaoCalls,
    int PangramCalls,
    int DeepSeekCalls,
    int ModelCalls,
    int SaplingCalls)
{
    public static PilotV3Summary Create(
        DateTimeOffset startedAt, DateTimeOffset finishedAt, IReadOnlyList<PilotV3Row> rows,
        int youdaoCalls, int pangramCalls, int deepseekCalls, int modelCalls, int saplingCalls, bool pangramEnabled)
    {
        var withOutput = rows.Where(r => r.T0HasOutput).ToList();
        var accepted = withOutput.Where(r => r.T4Accepted).ToList();
        var pairs = accepted.Where(r => r.DeltaT4MinusT0.HasValue).Select(r => r.DeltaT4MinusT0!.Value).ToList();
        var fallbackReasons = withOutput.Where(r => !r.T4Accepted)
            .GroupBy(r => r.FallbackReason).ToDictionary(g => g.Key, g => g.Count());
        var t0Scores = accepted.Where(r => r.PangramT0.HasValue).Select(r => r.PangramT0!.Value).ToList();
        var t4Scores = accepted.Where(r => r.PangramT4.HasValue).Select(r => r.PangramT4!.Value).ToList();

        return new PilotV3Summary(
            startedAt, finishedAt,
            rows.Count,
            withOutput.Count,
            rows.Count(r => r.SentinelPass),
            rows.Count(r => r.T4Generated),
            accepted.Count,
            rows.Count(r => r.T4Accepted && r.Tier == "sendable"),
            rows.Count(r => r.T4Accepted && r.Tier == "minor"),
            withOutput.Count(r => !r.T4Accepted),
            fallbackReasons,
            accepted.Count == 0 ? 0 : (int)Math.Round(accepted.Average(r => r.ReplacedRatioPct)),
            accepted.Count == 0 ? 0 : (int)Math.Round(accepted.Average(r => (double)r.PatchesApplied)),
            pairs.Count,
            pairs.Count(d => d < 0),
            pairs.Count(d => d > 0),
            pairs.Count(d => d == 0),
            accepted.Count(r => r.DeltaT4MinusT0 < 0),
            pairs.Count == 0 ? null : (int?)Math.Round(pairs.Average(), MidpointRounding.AwayFromZero),
            Median(pairs),
            t0Scores.Count == 0 ? null : (int?)Math.Round(t0Scores.Average(), MidpointRounding.AwayFromZero),
            t4Scores.Count == 0 ? null : (int?)Math.Round(t4Scores.Average(), MidpointRounding.AwayFromZero),
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
        $"T4 pilot: T0out={T0WithOutput}/{Cases}, sentinel={SentinelPass}/{Cases}, t4gen={T4Generated}, "
        + $"t4Accepted={T4Accepted} (sendable={TierSendable}/minor={TierMinor}), avgRepl={AvgReplacedPct}%, "
        + $"pangramPairs={PangramPairs} (lower={PangramLower}/higher={PangramHigher}/equal={PangramEqual}), "
        + $"**lower+accepted={PangramLowerAndAccepted}**, meanT0={MeanT0}, meanT4={MeanT4}, meanDelta={(MeanDelta?.ToString() ?? "n/a")}, "
        + $"youdao={YoudaoCalls}, pangram={PangramCalls}, deepseek={DeepSeekCalls}";
}

internal static class PilotV3Report
{
    public static string Render(PilotV3Summary s, IReadOnlyList<PilotV3Row> rows)
    {
        var lines = new List<string>
        {
            "# T4 selective-patch translation pilot (round 3)",
            "",
            "**Eval-only research pilot.** Not wired into the production engine. T0 = production baseline.",
            "TA_raw = Youdao en→zh-CHS→en perturbation draft. T4 = TA_raw with a SMALL set of budgeted span patches applied by the program (DeepSeek only proposes find/replace; budget ≤8 patches, ≤12% chars, ≤30% new-writing, no 2-sentence patch). 3-tier gate (sendable / minor-awkward-but-sendable / unsendable); only 'unsendable' or fact/boundary failure falls back to T0.",
            "",
            $"Started: {s.StartedAt:O}  Finished: {s.FinishedAt:O}",
            "",
            "## Headline — does selective-patch keep the Pangram drop AND stay sendable?",
            "",
        };

        if (!s.PangramEnabled)
        {
            lines.Add("Pangram disabled — only the gate ran.");
        }
        else if (s.PangramPairs == 0)
        {
            lines.Add("No T4 was accepted as a distinct output, so there is no T0-vs-T4 Pangram pair.");
        }
        else
        {
            lines.Add($"- T4 accepted (sendable or minor + fact/boundary safe): **{s.T4Accepted}/{s.Cases}** (sendable {s.TierSendable}, minor {s.TierMinor}). Avg patches {s.AvgPatchesApplied}, avg {s.AvgReplacedPct}% of text replaced.");
            lines.Add($"- Pangram of the {s.PangramPairs} accepted: **lower {s.PangramLower}**, higher {s.PangramHigher}, equal {s.PangramEqual} (mean Δ **{Signed(s.MeanDelta)}**, median **{Signed(s.MedianDelta)}**).");
            lines.Add($"- Where T4 lands: mean Pangram **T0 {s.MeanT0} → T4 {s.MeanT4}** (round-1 raw-TA was ~0–14; round-2 T3 was ~T0).");
            lines.Add($"- **Lower AND accepted (the only real win): {s.PangramLowerAndAccepted}/{s.Cases}.**");
        }

        lines.Add("");
        lines.Add("## Gate / safety");
        lines.Add("");
        lines.Add($"- T0 with output: **{s.T0WithOutput}/{s.Cases}** · sentinel held: **{s.SentinelPass}/{s.Cases}** · T4 generated: **{s.T4Generated}**");
        lines.Add($"- T4 accepted: **{s.T4Accepted}** (tier sendable {s.TierSendable}, minor {s.TierMinor}); fell back to T0: **{s.Fallback}/{s.T0WithOutput}**"
            + (s.FallbackReasons.Count > 0 ? " — " + string.Join(", ", s.FallbackReasons.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}×{kv.Value}")) : string.Empty));
        lines.Add("");
        lines.Add("## Cost / calls");
        lines.Add("");
        lines.Add($"Youdao: **{s.YoudaoCalls}** · Pangram: **{s.PangramCalls}** · DeepSeek (terms+patch+tier+judge): **{s.DeepSeekCalls}** · DeepSeek model (T0): **{s.ModelCalls}** · Sapling (engine gate): **{s.SaplingCalls}**");
        lines.Add("");
        lines.Add("## Per-case");
        lines.Add("");
        lines.Add("| Case | Category | Sentinel | Patches | Repl% | New% | Tier | Accepted | Pangram T0 | Pangram T4 | Δ | Chosen | Fallback / issues |");
        lines.Add("| --- | --- | :---: | :---: | ---: | ---: | :---: | :---: | ---: | ---: | ---: | :---: | --- |");
        foreach (var r in rows)
        {
            var detail = r.T4Accepted
                ? string.Empty
                : (r.FallbackReason
                   + (r.ProtectedMissing.Count > 0 ? " | prot:" + string.Join(";", r.ProtectedMissing) : string.Empty)
                   + (r.SendIssues.Count > 0 ? " | send:" + string.Join(";", r.SendIssues) : string.Empty)
                   + (r.MissingFacts.Count > 0 ? " | facts:" + string.Join(";", r.MissingFacts) : string.Empty));
            lines.Add(string.Join(" | ", new[]
            {
                r.CaseId, r.Category,
                r.SentinelPass ? "ok" : "broken",
                $"{r.PatchesApplied}/{r.PatchesProposed}",
                r.ReplacedRatioPct.ToString() + "%",
                r.NewWritingRatioPct.ToString() + "%",
                r.Tier,
                r.T4Accepted ? "yes" : "no",
                r.PangramT0?.ToString() ?? "-",
                r.PangramT4?.ToString() ?? "-",
                Signed(r.DeltaT4MinusT0),
                r.ChosenVariant,
                detail.Replace("|", "/", StringComparison.Ordinal),
            }));
        }

        lines.Add("");
        lines.Add("## Text comparison (T0 → TA_raw → T4)");
        foreach (var r in rows.Where(r => r.T0HasOutput))
        {
            lines.Add("");
            lines.Add($"### {r.CaseId} — {r.Category} (chosen: {r.ChosenVariant})");
            lines.Add("");
            lines.Add("**T0 (baseline):**");
            lines.Add("> " + r.T0Text.Replace("\n", "\n> ", StringComparison.Ordinal));
            if (r.SentinelPass)
            {
                lines.Add("");
                lines.Add("**TA_raw (Youdao round-trip):**");
                lines.Add("> " + r.TaRawText.Replace("\n", "\n> ", StringComparison.Ordinal));
            }

            if (r.T4Generated)
            {
                lines.Add("");
                lines.Add($"**T4 ({r.PatchesApplied} patches, {r.ReplacedRatioPct}% replaced, tier={r.Tier}, accepted={r.T4Accepted}):**");
                lines.Add("> " + r.T4Text.Replace("\n", "\n> ", StringComparison.Ordinal));
            }
        }

        lines.Add("");
        return string.Join("\n", lines) + "\n";
    }

    private static string Signed(int? v) => v is null ? "-" : v.Value > 0 ? $"+{v.Value}" : v.Value.ToString();
}
