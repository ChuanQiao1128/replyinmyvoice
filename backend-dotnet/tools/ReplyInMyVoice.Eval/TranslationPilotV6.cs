using System.Text.Json;
using System.Text.Json.Serialization;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.RewriteEngine;
using ReplyInMyVoice.Infrastructure.Providers;

// Round-7 Chinese-intermediate-polish pilot (R7_CN_INTERMEDIATE_PILOT=1). EVAL-ONLY.
// See plans/translation-roundtrip-pilot.md + the round-7 design.
//
// New variable vs rounds 1-6: do the LLM polish in the CHINESE middle state, so Youdao (not DeepSeek)
// stays the final English author and the MT perturbation survives, while the Chinese polish stabilizes
// facts/semantics under the ledgers.
//   T0   = production baseline
//   R7C  = T0 -> mask -> Youdao EN->ZH->EN -> unmask -> small English entity patch  (plain-round-trip control)
//   R7A  = T0 -> mask -> Youdao EN->ZH -> DeepSeek Chinese polish -> Youdao ZH->EN -> unmask -> entity patch
// English end NEVER gets a full DeepSeek rewrite (that re-adds the LLM fingerprint = T3). Entity patch
// budget is tight (<=8% chars, 0 full-sentence). Stricter than R5: AgentAction drift ("I am unable to
// get a full refund") is a HARD fail via the sendability judge. Pangram once per hard-gate survivor.

internal sealed class ChinesePolishClient(DeepSeekChatClient chat)
{
    private const string SystemPrompt =
        "你正在处理一段由英文邮件机器翻译成中文的中间稿。你的任务不是创作新内容，也不是添加解释，而是在不改变事实的前提下，"
        + "让中文语义更清楚、更稳定，方便之后再翻译回英文。\n\n"
        + "最高优先级规则：\n"
        + "1. 所有 sentinel（例如 [[A0]] 这类占位符）必须原样保留，不能删除、不能翻译、不能改动。\n"
        + "2. 所有英文姓名、称呼、订单号、SKU、金额、日期、时间、电话、邮箱、地址必须原样保留。\n"
        + "3. 不得新增事实、承诺、道歉、折扣、退款、医学建议、法律建议、录用决定、期限延长或政策例外。\n"
        + "4. 必须保留 cannot / may / not yet / no decision / not a clinician / no refund / no guarantee 等边界含义。\n"
        + "5. 可以修顺中文语序，但不要改变信息顺序和责任归属（谁对谁做什么）。\n"
        + "6. 中文里轻微机器翻译痕迹只要不影响事实，不需要修得很文学化。\n\n"
        + "只输出 JSON：{\"zh_polished\":\"润色后的中文中间稿\",\"risk_notes\":\"...\",\"changed_summary\":\"...\"}";

    public async Task<string?> PolishAsync(string zhRaw, CancellationToken ct)
    {
        var content = await chat.CompleteAsync(SystemPrompt, "中文中间稿：\n" + zhRaw, 3000, 0.2, ct);
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            return doc.RootElement.TryGetProperty("zh_polished", out var z) && z.ValueKind == JsonValueKind.String
                ? z.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

// One translation branch's outcome (R7C or R7A) after unmask + entity patch + gates.
internal sealed record R7Branch(
    bool Produced, string Text, bool SentinelPass, int PatchCount, int EnglishPatchPct, int FullSentence,
    bool FactPass, int Forbid, bool MeaningChanged, IReadOnlyList<string> ProtectedMissing,
    bool Understandable, string Tier, bool HardPass, IReadOnlyList<string> MissingFacts, string FailReason)
{
    public static R7Branch Failed(string reason) =>
        new(false, string.Empty, false, 0, 0, 0, false, 0, false, Array.Empty<string>(), false, "unsendable", false, Array.Empty<string>(), reason);
}

internal sealed record PilotV6Row(
    string CaseId, int CaseNumber, string Category, string Tone,
    string T0Text, string ZhRaw, string ZhPolished, string R7CText, string R7AText,
    bool R7CHardPass, bool R7AHardPass, string R7CFail, string R7AFail,
    int R7AEnglishPatchPct, bool R7ASentinelPass, bool R7AAgentActionPass,
    int? PangramT0, int? PangramR7C, int? PangramR7A, int? DeltaR7AMinusT0, int? DeltaR7CMinusT0,
    string Notes);

internal static class TranslationPilotV6Runner
{
    private const double MaxEnglishPatchRatio = 0.08;

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
        if (string.IsNullOrWhiteSpace(youdaoKey) || string.IsNullOrWhiteSpace(youdaoSecret))
        {
            Console.Error.WriteLine("R7_CN_INTERMEDIATE_PILOT: missing Youdao credentials.");
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

        var youdao = new YoudaoTranslationClient(youdaoHttp, youdaoKey!, youdaoSecret!,
            Environment.GetEnvironmentVariable("YOUDAO_API_URL") ?? "https://openapi.youdao.com/api", TimeSpan.FromSeconds(30));
        var deepseek = new DeepSeekChatClient(deepseekHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));
        var polisher = new ChinesePolishClient(deepseek);
        var driftRepairer = new FactDriftRepairer(deepseek);
        var termProposer = new ProtectedTermProposer(deepseek);
        var understandability = new UnderstandabilityJudge(deepseek);
        var tierJudge = new SendabilityTierJudge(deepseek);
        var judge = new SemanticEvalJudge(judgeHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));
        var pangram = pangramEnabled ? new PangramWritingSignalClient(pangramHttp, pangramKey!, TimeSpan.FromSeconds(45)) : null;

        Console.WriteLine(
            $"R7 pilot: cases={cases.Count} youdaoMax={youdaoMaxCalls} pangram={(pangramEnabled ? $"on(max {pangramMaxCalls})" : "off")} model={config.Model}");

        var rows = new List<PilotV6Row>();
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
                Console.WriteLine($"{sample.Id}: T0 no output; skipped.");
                continue;
            }

            var ledger = FactLedgerExtractor.Extract(request);
            var protectedTerms = (await termProposer.ProposeAsync(sample.InputDraft, CancellationToken.None)).ToList();
            // Bracket-free sentinels (QZAN000QZ) survive the DeepSeek Chinese-polish pass; "[[A0]]" did not.
            var masked = AnchorMasker.Mask(t0Text, ledger, mustKeep, mustNotClaim, bracketFree: true);

            var zhRaw = string.Empty;
            var zhPolished = string.Empty;
            var r7c = R7Branch.Failed("not_run");
            var r7a = R7Branch.Failed("not_run");

            if (youdao.CallCount + 3 > youdaoMaxCalls)
            {
                r7c = R7Branch.Failed("youdao_budget_exhausted");
                r7a = R7Branch.Failed("youdao_budget_exhausted");
            }
            else
            {
                var toZh = await youdao.TranslateAsync(masked.MaskedText, "en", "zh-CHS", CancellationToken.None);
                if (!toZh.Success)
                {
                    r7c = R7Branch.Failed(toZh.ErrorCode ?? "youdao_en_zh_failed");
                    r7a = R7Branch.Failed(toZh.ErrorCode ?? "youdao_en_zh_failed");
                }
                else
                {
                    zhRaw = toZh.Text;

                    // R7C: plain round-trip (no Chinese polish).
                    var backC = await youdao.TranslateAsync(zhRaw, "zh-CHS", "en", CancellationToken.None);
                    r7c = backC.Success
                        ? await ProcessEnglishAsync(backC.Text, masked.Map, sample.InputDraft, t0Text, mustKeep, mustNotClaim, protectedTerms, driftRepairer, judge, understandability, tierJudge)
                        : R7Branch.Failed(backC.ErrorCode ?? "youdao_zh_en_failed");

                    // R7A: DeepSeek Chinese polish, then back-translate.
                    var polished = await polisher.PolishAsync(zhRaw, CancellationToken.None);
                    if (string.IsNullOrWhiteSpace(polished))
                    {
                        r7a = R7Branch.Failed("zh_polish_failed");
                    }
                    else
                    {
                        zhPolished = polished;
                        // Sentinels must survive the Chinese polish too (whitespace-tolerant: an LLM may
                        // insert spaces inside the token).
                        var zhNoWs = new string(zhPolished.Where(c => !char.IsWhiteSpace(c)).ToArray());
                        var sentinelsInZh = masked.Map.Keys.All(k =>
                            zhNoWs.Contains(new string(k.Where(c => !char.IsWhiteSpace(c)).ToArray()), StringComparison.Ordinal));
                        if (!sentinelsInZh && masked.Map.Count > 0)
                        {
                            r7a = R7Branch.Failed("zh_sentinel_break");
                        }
                        else
                        {
                            var backA = await youdao.TranslateAsync(zhPolished, "zh-CHS", "en", CancellationToken.None);
                            r7a = backA.Success
                                ? await ProcessEnglishAsync(backA.Text, masked.Map, sample.InputDraft, t0Text, mustKeep, mustNotClaim, protectedTerms, driftRepairer, judge, understandability, tierJudge)
                                : R7Branch.Failed(backA.ErrorCode ?? "youdao_zh_en_failed");
                        }
                    }
                }
            }

            int? pT0 = null, pC = null, pA = null;
            if (pangram is not null)
            {
                if (pangramCalls < pangramMaxCalls)
                {
                    pT0 = await Measure(pangram, t0Text);
                    pangramCalls++;
                }

                // Measure the back-translated English's Pangram for EVERY produced case (not only
                // gate-passers) — the question is whether the MT surface is low, separate from quality.
                if (!string.IsNullOrEmpty(r7c.Text) && pangramCalls < pangramMaxCalls)
                {
                    pC = await Measure(pangram, r7c.Text);
                    pangramCalls++;
                }

                if (!string.IsNullOrEmpty(r7a.Text) && pangramCalls < pangramMaxCalls)
                {
                    pA = await Measure(pangram, r7a.Text);
                    pangramCalls++;
                }
            }

            int? dA = pT0.HasValue && pA.HasValue ? pA.Value - pT0.Value : null;
            int? dC = pT0.HasValue && pC.HasValue ? pC.Value - pT0.Value : null;

            rows.Add(new PilotV6Row(
                sample.Id, sample.CaseNumber, sample.Category, tone,
                t0Text, zhRaw, zhPolished, r7c.Text, r7a.Text,
                r7c.HardPass, r7a.HardPass, r7c.FailReason, r7a.FailReason,
                r7a.EnglishPatchPct, r7a.SentinelPass, r7a.Tier != "unsendable",
                pT0, pC, pA, dA, dC,
                Notes(r7a, dA)));

            Console.WriteLine(
                $"{sample.Id}: r7c={(r7c.HardPass ? "pass" : "fail:" + r7c.FailReason)} "
                + $"r7a={(r7a.HardPass ? "pass" : "fail:" + r7a.FailReason)} r7aPatch={r7a.EnglishPatchPct}% "
                + $"pangram T0={Fmt(pT0)} R7C={Fmt(pC)} R7A={Fmt(pA)} dA={Fmt(dA)}");
        }

        var summary = PilotV6Summary.Create(startedAt, DateTimeOffset.UtcNow, rows,
            youdao.CallCount, pangramCalls, deepseek.CallCount, modelCounter.CallCount, saplingCounter.CallCount, pangramEnabled);

        var stamp = startedAt.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(config.OutputDirectory);
        var jsonPath = Path.Combine(config.OutputDirectory, $"{stamp}-r7-cn-intermediate-pilot.json");
        var mdPath = Path.Combine(config.OutputDirectory, $"{stamp}-r7-cn-intermediate-pilot.md");
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(new { summary, rows }, jsonOptions));
        await File.WriteAllTextAsync(mdPath, PilotV6Report.Render(summary, rows));

        Console.WriteLine($"Wrote {jsonPath}");
        Console.WriteLine($"Wrote {mdPath}");
        Console.WriteLine(summary.OneLine());
        return 0;
    }

    // Unmask -> fact-only English entity patch (tight <=8% budget, 0 full-sentence) -> hard gates.
    private static async Task<R7Branch> ProcessEnglishAsync(
        string enBack, IReadOnlyDictionary<string, string> map, string originalDraft, string t0Text,
        IReadOnlyList<string> mustKeep, IReadOnlyList<string> mustNotClaim, IReadOnlyList<string> protectedTerms,
        FactDriftRepairer driftRepairer, SemanticEvalJudge judge, UnderstandabilityJudge understandability, SendabilityTierJudge tierJudge)
    {
        var unmask = AnchorMasker.Unmask(enBack, map);
        // Carry the back-translated English even on failure, so its Pangram can still be observed
        // (answers "is the MT surface actually low?" independent of whether it passes the quality gate).
        if (!unmask.IntegrityOk)
        {
            return Carry(unmask.Restored, false, 0, 0, "sentinel_broken");
        }

        // Locate fact/boundary drift and propose entity-only patches (copied from original/T0/ledger).
        var drift = await driftRepairer.LocateAndProposeAsync(unmask.Restored, originalDraft, t0Text, mustKeep, mustNotClaim, CancellationToken.None);
        if (drift.Error is not null)
        {
            return Carry(unmask.Restored, true, 0, 0, $"drift_{drift.Error}");
        }

        var apply = R5PatchApplier.Apply(unmask.Restored, drift.Patches, originalDraft, t0Text);
        var englishPct = (int)Math.Round(apply.FactCharRatio * 100);
        // R7's English end must stay tiny: <=8% chars and zero full-sentence replacement, else it is a
        // rewrite in disguise -> fail (do not become T3). Carry the text so its Pangram is still observed.
        if (apply.Fallback || apply.FactCharRatio > MaxEnglishPatchRatio || apply.FullSentenceReplacements > 0)
        {
            return Carry(apply.Text, true, apply.PatchCount, englishPct, $"english_patch_over_budget:{englishPct}%");
        }

        var text = apply.Text;
        var protectedMissing = protectedTerms
            .Where(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 3 && !text.Contains(t, StringComparison.Ordinal))
            .ToList();
        var sem = await judge.VerifyAsync(text, mustKeep, mustNotClaim, CancellationToken.None);
        var u = await understandability.JudgeAsync(text, CancellationToken.None);
        var tier = await tierJudge.JudgeAsync(text, CancellationToken.None);

        var factPass = sem.Error is null && sem.FactsReallyPass;
        var forbid = sem.RealForbidden;
        var meaning = sem.MeaningChanged;
        var understandable = u.Understandable;
        // SendReady is HARD for email; tier "unsendable" (incl. agent-action errors) fails.
        var sendOk = tier.Tier is "sendable" or "minor";
        var missing = sem.Error is null
            ? sem.Facts.Where(f => f.Status is "missing" or "contradicted").Select(f => $"{f.Status}:{f.Fact}").ToList()
            : new List<string> { $"judge_error:{sem.Error}" };

        var hardPass = factPass && forbid == 0 && !meaning && understandable && sendOk;
        var fail = hardPass ? string.Empty
            : !factPass ? "fact_drift"
            : forbid > 0 ? "forbidden"
            : meaning ? "meaning_changed"
            : !understandable ? "not_understandable"
            : "not_sendable_or_agent_error";

        return new R7Branch(true, text, true, apply.PatchCount, englishPct, apply.FullSentenceReplacements,
            factPass, forbid, meaning, protectedMissing, understandable, tier.Tier, hardPass, missing, fail);
    }

    // Build an R7Branch that carries the back-translated text for a gate failure (so Pangram is still observed).
    private static R7Branch Carry(string text, bool sentinelPass, int patchCount, int englishPct, string fail) =>
        new(true, text, sentinelPass, patchCount, englishPct, 0, false, 0, false, Array.Empty<string>(), false, "unsendable", false, Array.Empty<string>(), fail);

    private static async Task<int?> Measure(IWritingSignalClient client, string text)
    {
        var r = await client.MeasureAsync(text, CancellationToken.None);
        return r.Available ? r.AiLikePercent : null;
    }

    private static PilotV6Row EmptyRow(EvalCase sample, string tone, string? errorCode) => new(
        sample.Id, sample.CaseNumber, sample.Category, tone,
        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
        false, false, "t0_no_output", "t0_no_output",
        0, false, false,
        null, null, null, null, null,
        $"T0 produced no output ({errorCode ?? "unknown"}).");

    private static string Notes(R7Branch r7a, int? dA)
    {
        if (!r7a.HardPass)
        {
            return $"R7A fell back to T0 ({r7a.FailReason}).";
        }

        var d = dA is null ? "Pangram not measured" : dA < 0 ? $"Pangram {Math.Abs(dA.Value)} lower" : dA > 0 ? $"Pangram {dA.Value} higher" : "Pangram equal";
        return $"R7A passed hard gates (tier={r7a.Tier}, eng-patch={r7a.EnglishPatchPct}%); {d} vs T0.";
    }

    private static string? FirstEnv(params string[] names) =>
        names.Select(Environment.GetEnvironmentVariable).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static int IntEnv(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var v) && v >= 0 ? v : fallback;

    private static string Fmt(int? v) => v?.ToString() ?? "-";
}

internal sealed record PilotV6Summary(
    DateTimeOffset StartedAt, DateTimeOffset FinishedAt, int Cases, int T0WithOutput,
    int R7CHardPass, int R7AHardPass, int R7ASentinelPass, int R7AAgentActionPass,
    IReadOnlyDictionary<string, int> R7AFailReasons,
    int R7APangramPairs, int R7ALower, int R7AHigher, int R7AEqual, int? R7AMeanDelta, int? R7AMedianDelta,
    int? R7CMeanDelta, int? MedianEnglishPatchPct,
    bool PangramEnabled, int YoudaoCalls, int PangramCalls, int DeepSeekCalls, int ModelCalls, int SaplingCalls)
{
    public static PilotV6Summary Create(
        DateTimeOffset startedAt, DateTimeOffset finishedAt, IReadOnlyList<PilotV6Row> rows,
        int youdaoCalls, int pangramCalls, int deepseekCalls, int modelCalls, int saplingCalls, bool pangramEnabled)
    {
        var withOutput = rows.Where(r => !string.IsNullOrEmpty(r.T0Text)).ToList();
        var aPass = withOutput.Where(r => r.R7AHardPass).ToList();
        var aPairs = aPass.Where(r => r.DeltaR7AMinusT0.HasValue).Select(r => r.DeltaR7AMinusT0!.Value).ToList();
        var cPairs = withOutput.Where(r => r.R7CHardPass && r.DeltaR7CMinusT0.HasValue).Select(r => r.DeltaR7CMinusT0!.Value).ToList();
        var aFails = withOutput.Where(r => !r.R7AHardPass).GroupBy(r => r.R7AFail).ToDictionary(g => g.Key, g => g.Count());
        var patchPcts = aPass.Select(r => r.R7AEnglishPatchPct).OrderBy(x => x).ToList();

        return new PilotV6Summary(
            startedAt, finishedAt, rows.Count, withOutput.Count,
            rows.Count(r => r.R7CHardPass), rows.Count(r => r.R7AHardPass),
            withOutput.Count(r => r.R7ASentinelPass), aPass.Count(r => r.R7AAgentActionPass),
            aFails,
            aPairs.Count, aPairs.Count(d => d < 0), aPairs.Count(d => d > 0), aPairs.Count(d => d == 0),
            aPairs.Count == 0 ? null : (int?)Math.Round(aPairs.Average(), MidpointRounding.AwayFromZero), Median(aPairs),
            cPairs.Count == 0 ? null : (int?)Math.Round(cPairs.Average(), MidpointRounding.AwayFromZero),
            patchPcts.Count == 0 ? null : patchPcts[patchPcts.Count / 2],
            pangramEnabled, youdaoCalls, pangramCalls, deepseekCalls, modelCalls, saplingCalls);
    }

    private static int? Median(IReadOnlyList<int> v)
    {
        if (v.Count == 0)
        {
            return null;
        }

        var s = v.OrderBy(x => x).ToList();
        var m = s.Count / 2;
        return s.Count % 2 == 1 ? s[m] : (int)Math.Round((s[m - 1] + s[m]) / 2.0, MidpointRounding.AwayFromZero);
    }

    public string OneLine() =>
        $"R7 pilot: T0out={T0WithOutput}/{Cases}, R7C hardPass={R7CHardPass}/{Cases}, R7A hardPass={R7AHardPass}/{Cases}, "
        + $"R7A sentinel={R7ASentinelPass}, agentActionOk={R7AAgentActionPass}, "
        + $"R7A pangramPairs={R7APangramPairs} (lower={R7ALower}/higher={R7AHigher}/eq={R7AEqual}), "
        + $"R7A meanDelta={(R7AMeanDelta?.ToString() ?? "n/a")}, medianDelta={(R7AMedianDelta?.ToString() ?? "n/a")}, "
        + $"R7C meanDelta={(R7CMeanDelta?.ToString() ?? "n/a")}, medEngPatch={(MedianEnglishPatchPct?.ToString() ?? "n/a")}%, "
        + $"youdao={YoudaoCalls}, pangram={PangramCalls}, deepseek={DeepSeekCalls}";
}

internal static class PilotV6Report
{
    public static string Render(PilotV6Summary s, IReadOnlyList<PilotV6Row> rows)
    {
        var lines = new List<string>
        {
            "# R7 Chinese-intermediate-polish pilot (round 7)",
            "",
            "**Eval-only research pilot.** Not wired into production. The LLM polish happens in the CHINESE middle state; Youdao stays the final English author. R7C = plain Youdao round-trip + entity patch (control); R7A = + DeepSeek Chinese polish. English end gets only a tight entity patch (≤8% chars, 0 full-sentence). Stricter than R5: agent-action drift is a hard fail. Pangram once per hard-gate survivor.",
            "",
            $"Started: {s.StartedAt:O}  Finished: {s.FinishedAt:O}",
            "",
            "## Headline — does Chinese-side polish keep the Pangram drop while staying fact-safe + send-ready?",
            "",
        };

        if (!s.PangramEnabled)
        {
            lines.Add("Pangram disabled — only the gates ran.");
        }
        else if (s.R7APangramPairs == 0)
        {
            lines.Add("No R7A candidate passed hard gates as a distinct output → no T0-vs-R7A Pangram pair.");
        }
        else
        {
            lines.Add($"- R7A hard-gate pass: **{s.R7AHardPass}/{s.Cases}** (R7C control {s.R7CHardPass}/{s.Cases}).");
            lines.Add($"- R7A Pangram of {s.R7APangramPairs} pairs: lower {s.R7ALower}, higher {s.R7AHigher}, equal {s.R7AEqual} (mean Δ **{Signed(s.R7AMeanDelta)}**, median **{Signed(s.R7AMedianDelta)}**); R7C mean Δ {Signed(s.R7CMeanDelta)}.");
            lines.Add($"- Median English-patch ratio (R7A): **{s.MedianEnglishPatchPct?.ToString() ?? "n/a"}%**.");
        }

        lines.Add("");
        lines.Add("## Round-7 success criteria (§10)");
        lines.Add($"- hard-gate ≥8/10: **{s.R7AHardPass}/10** {(s.R7AHardPass >= 8 ? "✓" : "✗")}");
        lines.Add($"- Pangram win ≥5/10 (of pairs): **{s.R7ALower}/{s.R7APangramPairs}** {(s.R7ALower >= 5 ? "✓" : "✗")}");
        lines.Add($"- median delta ≤ −20: **{Signed(s.R7AMedianDelta)}** {(s.R7AMedianDelta is int md && md <= -20 ? "✓" : "✗")}");
        lines.Add($"- English patch ratio median ≤8%: **{s.MedianEnglishPatchPct?.ToString() ?? "n/a"}%** {(s.MedianEnglishPatchPct is int ep && ep <= 8 ? "✓" : "✗")}");
        lines.Add($"- sentinel survival ≥9/10: **{s.R7ASentinelPass}/10** {(s.R7ASentinelPass >= 9 ? "✓" : "✗")}");
        lines.Add("");
        lines.Add("## Gate / fallback");
        lines.Add($"- T0 with output: **{s.T0WithOutput}/{s.Cases}** · R7A sentinel held: **{s.R7ASentinelPass}/{s.Cases}** · R7A agent-action ok: **{s.R7AAgentActionPass}**");
        lines.Add($"- R7A fell back: **{s.Cases - s.R7AHardPass}/{s.T0WithOutput}**"
            + (s.R7AFailReasons.Count > 0 ? " — " + string.Join(", ", s.R7AFailReasons.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}×{kv.Value}")) : string.Empty));
        lines.Add("");
        lines.Add("## Cost / calls");
        lines.Add($"Youdao: **{s.YoudaoCalls}** · Pangram: **{s.PangramCalls}** · DeepSeek (polish+drift+gates): **{s.DeepSeekCalls}** · DeepSeek model (T0): **{s.ModelCalls}** · Sapling: **{s.SaplingCalls}**");
        lines.Add("");
        lines.Add("## Per-case");
        lines.Add("");
        lines.Add("| Case | Category | R7C | R7A | R7A eng-patch% | R7A sentinel | agent-ok | Pangram T0 | R7C | R7A | Δ R7A | R7A fail |");
        lines.Add("| --- | --- | :---: | :---: | ---: | :---: | :---: | ---: | ---: | ---: | ---: | --- |");
        foreach (var r in rows)
        {
            lines.Add(string.Join(" | ", new[]
            {
                r.CaseId, r.Category,
                r.R7CHardPass ? "pass" : "fail",
                r.R7AHardPass ? "pass" : "fail",
                r.R7AEnglishPatchPct + "%",
                r.R7ASentinelPass ? "ok" : "broken",
                r.R7AAgentActionPass ? "y" : "n",
                r.PangramT0?.ToString() ?? "-", r.PangramR7C?.ToString() ?? "-", r.PangramR7A?.ToString() ?? "-",
                Signed(r.DeltaR7AMinusT0),
                (r.R7AHardPass ? "" : r.R7AFail).Replace("|", "/", StringComparison.Ordinal),
            }));
        }

        lines.Add("");
        lines.Add("## Text comparison (T0 → zh_raw → zh_polished → R7A)");
        foreach (var r in rows.Where(r => !string.IsNullOrEmpty(r.T0Text)))
        {
            lines.Add("");
            lines.Add($"### {r.CaseId} — {r.Category} (R7A hard={r.R7AHardPass})");
            lines.Add("");
            lines.Add("**T0:**");
            lines.Add("> " + r.T0Text.Replace("\n", "\n> ", StringComparison.Ordinal));
            if (!string.IsNullOrEmpty(r.ZhPolished))
            {
                lines.Add("");
                lines.Add("**zh_polished (DeepSeek Chinese middle):**");
                lines.Add("> " + r.ZhPolished.Replace("\n", "\n> ", StringComparison.Ordinal));
            }

            if (!string.IsNullOrEmpty(r.R7AText))
            {
                lines.Add("");
                lines.Add($"**R7A (back-translated + {r.R7AEnglishPatchPct}% entity patch):**");
                lines.Add("> " + r.R7AText.Replace("\n", "\n> ", StringComparison.Ordinal));
            }
        }

        lines.Add("");
        return string.Join("\n", lines) + "\n";
    }

    private static string Signed(int? v) => v is null ? "-" : v.Value > 0 ? $"+{v.Value}" : v.Value.ToString();
}
