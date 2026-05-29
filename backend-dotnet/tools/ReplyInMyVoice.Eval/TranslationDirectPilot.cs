using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Owner's EXACT chain (TRANSLATION_DIRECT=1, TD_FILE="path"). EVAL-ONLY.
//   original EN -> Youdao EN->ZH -> DeepSeek polish ZH (natural) -> DeepSeek MINIMAL fact-repair ZH (vs EN)
//   -> Youdao ZH->EN -> score on GPTZero.
// NO masking, NO English-side repair. The point is to see the raw detection result of THIS specific chain:
// does keeping the back-translation untouched (more translationese) lower the score — and at what cost to facts.
internal static class TranslationDirectPilot
{
    public static async Task<int> RunAsync(string apiKey, string model, string baseUrl)
    {
        var file = Environment.GetEnvironmentVariable("TD_FILE");
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
        {
            Console.Error.WriteLine("TRANSLATION_DIRECT: set TD_FILE=path (the English original).");
            return 2;
        }

        var gptKey = Environment.GetEnvironmentVariable("GPTZero_API_KEY") ?? Environment.GetEnvironmentVariable("GPTZERO_API_KEY");
        var youdaoKey = Environment.GetEnvironmentVariable("YOUDAO_APP_KEY") ?? Environment.GetEnvironmentVariable("AppID") ?? Environment.GetEnvironmentVariable("YouDao_API_KEY");
        var youdaoSecret = Environment.GetEnvironmentVariable("YOUDAO_APP_SECRET") ?? Environment.GetEnvironmentVariable("AppSecret");
        var youdaoUrl = Environment.GetEnvironmentVariable("YOUDAO_API_URL") ?? "https://openapi.youdao.com/api";
        if (string.IsNullOrWhiteSpace(gptKey) || string.IsNullOrWhiteSpace(youdaoKey) || string.IsNullOrWhiteSpace(youdaoSecret))
        {
            Console.Error.WriteLine("TRANSLATION_DIRECT: need GPTZero + Youdao keys in .env.local.");
            return 2;
        }

        var original = (await File.ReadAllTextAsync(file)).Trim();
        using var http = new HttpClient();
        using var youdaoHttp = new HttpClient();
        using var gptHttp = new HttpClient();
        var deepseek = new DeepSeekChatClient(http, apiKey, model, baseUrl, TimeSpan.FromSeconds(60));
        var youdao = new YoudaoTranslationClient(youdaoHttp, youdaoKey!, youdaoSecret!, youdaoUrl, TimeSpan.FromSeconds(30));

        Console.WriteLine("=== Direct chain: EN -> Youdao ZH -> polish ZH -> minimal fact-repair ZH -> Youdao EN -> score ===\n");
        Console.WriteLine("ORIGINAL EN:\n" + original + "\n");

        var zh0 = await youdao.TranslateAsync(original, "en", "zh-CHS", CancellationToken.None);
        if (!zh0.Success)
        {
            Console.Error.WriteLine($"Youdao EN->ZH failed: {zh0.ErrorCode}");
            return 1;
        }

        Console.WriteLine("[1] Youdao EN->ZH:\n" + zh0.Text + "\n");

        var zh1 = await PolishAsync(deepseek, original, zh0.Text) ?? zh0.Text;
        Console.WriteLine("[2] DeepSeek polished ZH (natural):\n" + zh1 + "\n");

        var zh2 = await RepairAsync(deepseek, original, zh1) ?? zh1;
        Console.WriteLine("[3] DeepSeek minimal fact-repair ZH:\n" + zh2 + "\n");

        var enFinal = await youdao.TranslateAsync(zh2, "zh-CHS", "en", CancellationToken.None);
        if (!enFinal.Success)
        {
            Console.Error.WriteLine($"Youdao ZH->EN failed: {enFinal.ErrorCode}");
            return 1;
        }

        var final = enFinal.Text.Trim();
        Console.WriteLine("[4] Youdao ZH->EN (FINAL — no English repair):\n" + final + "\n");

        var (ai, cls, burst, sents, err) = await ScoreAsync(gptHttp, gptKey!, final, CancellationToken.None);
        if (err is not null)
        {
            Console.WriteLine($"GPTZero error: {err}");
            return 1;
        }

        Console.WriteLine($"GPTZero: document = {ai}% AI  [{cls}]  burstiness={(burst?.ToString("F2") ?? "-")}  ({sents.Count} sentences)");
        Console.WriteLine("    # | ai% | hl | sentence");
        Console.WriteLine("   -- | --- | -- | --------");
        var n = 0;
        foreach (var s in sents)
        {
            n++;
            var t = s.Sentence.Replace("\n", " ", StringComparison.Ordinal).Trim();
            if (t.Length > 70)
            {
                t = t[..69] + "…";
            }

            Console.WriteLine($"   {n,2} | {s.Ai,3} | {(s.Hl ? "* " : "  ")} | {t}");
        }

        Console.WriteLine($"\nCALLS: Youdao={youdao.CallCount} · DeepSeek={deepseek.CallCount} · GPTZero=1");
        Console.WriteLine("\n(Compare FINAL EN to ORIGINAL EN above to judge fact drift — this chain does NO English repair.)");
        return 0;
    }

    // Generality test (TRANSLATION_DIRECT_BATCH=1). Runs the SAME chain over a batch of corpus cases and
    // reports, per case: GPTZero before -> after, and a semantic facts verdict (facts / meaning / forbidden).
    // The question: is "stable GPTZero drop + facts preserved" a PATTERN on billing/renewal-type emails, or
    // was the single Orbit case lucky? Writes final texts to /tmp for a manual sendability/translationese read.
    public static async Task<int> RunBatchAsync(IReadOnlyList<EvalCase> cases, string apiKey, EvalConfig config, DateTimeOffset startedAt)
    {
        var gptKey = Environment.GetEnvironmentVariable("GPTZero_API_KEY") ?? Environment.GetEnvironmentVariable("GPTZERO_API_KEY");
        var youdaoKey = Environment.GetEnvironmentVariable("YOUDAO_APP_KEY") ?? Environment.GetEnvironmentVariable("AppID") ?? Environment.GetEnvironmentVariable("YouDao_API_KEY");
        var youdaoSecret = Environment.GetEnvironmentVariable("YOUDAO_APP_SECRET") ?? Environment.GetEnvironmentVariable("AppSecret");
        var youdaoUrl = Environment.GetEnvironmentVariable("YOUDAO_API_URL") ?? "https://openapi.youdao.com/api";
        if (string.IsNullOrWhiteSpace(gptKey) || string.IsNullOrWhiteSpace(youdaoKey) || string.IsNullOrWhiteSpace(youdaoSecret))
        {
            Console.Error.WriteLine("TRANSLATION_DIRECT_BATCH: need GPTZero + Youdao keys in .env.local.");
            return 2;
        }

        using var http = new HttpClient();
        using var youdaoHttp = new HttpClient();
        using var gptHttp = new HttpClient();
        using var judgeHttp = new HttpClient();
        var deepseek = new DeepSeekChatClient(http, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(60));
        var youdao = new YoudaoTranslationClient(youdaoHttp, youdaoKey!, youdaoSecret!, youdaoUrl, TimeSpan.FromSeconds(30));
        var judge = new SemanticEvalJudge(judgeHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));

        var texts = new StringBuilder();
        int dropped = 0, factSafe = 0, both = 0, gptCalls = 0;
        Console.WriteLine($"=== Generality batch: {cases.Count} cases through EN->ZH->polish->repair->EN, scored before/after ===\n");
        Console.WriteLine("case            | category          | before -> after | facts | mean | forb | verdict");

        foreach (var c in cases)
        {
            var original = c.InputDraft.Trim();
            var (beforeAi, _, _, _, beforeErr) = await ScoreAsync(gptHttp, gptKey!, original, CancellationToken.None);
            gptCalls++;

            var zh0 = await youdao.TranslateAsync(original, "en", "zh-CHS", CancellationToken.None);
            if (!zh0.Success)
            {
                Console.WriteLine($"{c.Id,-15} | {c.Category,-17} | youdao EN->ZH fail ({zh0.ErrorCode})");
                continue;
            }

            var zh1 = await PolishAsync(deepseek, original, zh0.Text) ?? zh0.Text;
            var zh2 = await RepairAsync(deepseek, original, zh1) ?? zh1;
            var back = await youdao.TranslateAsync(zh2, "zh-CHS", "en", CancellationToken.None);
            if (!back.Success)
            {
                Console.WriteLine($"{c.Id,-15} | {c.Category,-17} | youdao ZH->EN fail ({back.ErrorCode})");
                continue;
            }

            var final = back.Text.Trim();
            var (afterAi, afterCls, _, _, afterErr) = await ScoreAsync(gptHttp, gptKey!, final, CancellationToken.None);
            gptCalls++;

            var sem = await judge.VerifyAsync(final, c.MustKeep, c.MustNotClaim, CancellationToken.None);
            var factsPass = sem.Error is null && sem.FactsReallyPass && sem.RealForbidden == 0 && !sem.MeaningChanged;
            var isDropped = afterErr is null && afterAi is not null && afterAi <= 30;
            var bothOk = isDropped && factsPass;
            if (isDropped)
            {
                dropped++;
            }

            if (factsPass)
            {
                factSafe++;
            }

            if (bothOk)
            {
                both++;
            }

            var verdict = afterErr is not null ? "score_err"
                : bothOk ? "WIN (low+facts)"
                : isDropped ? "low but FACTS BROKEN"
                : factsPass ? "facts ok, still AI"
                : "neither";
            Console.WriteLine(
                $"{c.Id,-15} | {c.Category,-17} | {(beforeAi?.ToString() ?? "-"),3} -> {(afterAi?.ToString() ?? "-"),-4} | "
                + $"{(sem.FactsReallyPass ? "ok" : "x"),-5} | {(sem.MeaningChanged ? "chg" : "ok"),-4} | {sem.RealForbidden,3}  | {verdict}");

            texts.AppendLine($"## {c.Id} ({c.Category})  before={beforeAi}% after={afterAi}% [{afterCls}]  facts={(factsPass ? "ok" : "x")}");
            texts.AppendLine($"must_keep: {string.Join(" | ", c.MustKeep)}");
            texts.AppendLine($"FINAL: {final}");
            texts.AppendLine();
        }

        Console.WriteLine(
            $"\nSUMMARY over {cases.Count}: dropped(<=30%)={dropped} · fact-safe={factSafe} · BOTH(low+facts)={both}");
        Console.WriteLine($"CALLS: Youdao={youdao.CallCount} · DeepSeek={deepseek.CallCount} · GPTZero={gptCalls}");
        await File.WriteAllTextAsync("/tmp/td-batch-report.md", texts.ToString());
        Console.WriteLine("Final texts written to /tmp/td-batch-report.md (for sendability/translationese review).");
        return 0;
    }

    private static async Task<string?> PolishAsync(DeepSeekChatClient ds, string en, string zh)
    {
        // TD_NONNATIVE=1 switches to the owner's hypothesis: do NOT polish toward native/fluent Chinese
        // (that smooths out the translationese that lowers the detection score). Instead keep it non-native and
        // literal, only lock facts/meaning. Goal: more reliably retain the low-scoring MT surface.
        var nonNative = (Environment.GetEnvironmentVariable("TD_NONNATIVE") ?? string.Empty).Trim() is "1" or "true";
        const string nativeSys =
            "你是资深中文商务邮件编辑。下面给你【英文原文】和它的【机器翻译中文初稿】。把中文初稿改写成自然、地道、专业的"
            + "中文商务邮件——像中文母语者亲笔写的,不带翻译腔。\n"
            + "硬规则(不可违反):\n"
            + "1. 事实忠于英文原文,不增不减:数字、金额、日期、截止日、百分比、人名、发票/账号(如 INV-8842、A-913)、"
            + "套餐/产品名,必须与英文原文完全一致——不改写、不换算、不遗漏、不杜撰。\n"
            + "2. 业务术语按原义,不得偷换概念:credit→信用额度/可抵扣金额(不是\"积分/座位点数\");keep/retain→维持/保留现状"
            + "(不是\"预订/reserve\");套餐名(Starter/Basic/Business/Growth Plus/Orbit Pro 等)保留英文原名,不要意译成"
            + "\"入门版/商业版/基础版\";downgrade/upgrade/proration/grace period/late fee 用准确的中文商务术语。\n"
            + "3. 保边界与语气强度,极性不反转不软化:否定(不能/无法/不会)、不确定(可能/似乎/看起来)、政策限制"
            + "(不予全额退款/须在X日前书面确认/逾期收取滞纳金)一律原样保留;不得把\"不能\"写成\"可以\",不把\"可能\"写成\"一定\"。\n"
            + "4. 不新增原文没有的信息、承诺、问候或签名。\n"
            + "5. 责任主体不错位:谁对谁做什么(我/我们/你/系统)保持一致。\n"
            + "风格:自然中文商务语气,句子长短有起伏;抹掉机翻痕迹但不得为通顺牺牲任何硬规则;该简洁就简洁,该解释清楚就解释清楚。\n"
            + "只返回 JSON:{\"polished\":\"<改写后的中文>\"}";
        const string nonNativeSys =
            "你是一个把英文邮件翻译成中文的【非母语译者】。你的中文【不需要地道、不需要书面化、不需要像中文母语者写的】——"
            + "保持直白、贴着英文字面,允许翻译腔和不自然的语序。你唯一的任务是:不丢失、不改变英文原文的【事实与语义】。\n"
            + "硬规则:\n"
            + "1. 数字/金额/日期/截止日/百分比/人名/发票号(如 INV-8842)/套餐名,与英文原文完全一致。\n"
            + "2. 否定/不确定/政策限制的极性和强度不变(不能/无法/可能/不予全额退款/须在X日前书面确认)。\n"
            + "3. 不新增信息,不加问候或签名。\n"
            + "4. 【不要】为了通顺或地道而改写语序;能贴着英文直译就直译,宁可生硬也别改成地道中文。\n"
            + "只返回 JSON:{\"polished\":\"<贴近字面、可带翻译腔、但语义忠实的中文>\"}";
        var json = await ds.CompleteAsync(
            nonNative ? nonNativeSys : nativeSys,
            "英文原文:\n" + en + "\n\n机器翻译中文初稿:\n" + zh,
            1800, nonNative ? 0.4 : 0.7, CancellationToken.None);
        return ExtractField(json, "polished");
    }

    private static async Task<string?> RepairAsync(DeepSeekChatClient ds, string en, string zh)
    {
        const string sys =
            "你在做【最小事实修复】。对比英文原文,检查中文是否漏掉或改变了事实(数字/日期/人名/产品名/金额/否定/承诺/责任主体)。"
            + "只在确有漂移处做最小改动修回事实,其余一字不动,保持原中文的风格(原文若直白/带翻译腔就维持,不要润色成地道中文)。"
            + "不得新增信息。只返回 JSON:{\"repaired\":\"...\"}";
        var json = await ds.CompleteAsync(sys, "英文原文(事实真相):\n" + en + "\n\n当前中文(对照原文最小修复):\n" + zh, 1600, 0.2, CancellationToken.None);
        return ExtractField(json, "repaired");
    }

    // Owner's iterative loop (TRANSLATION_DIRECT_LOOP=1, EVAL_CASE_IDS=<one case>, TD_LOOP_ITERS, TD_LOOP_TARGET).
    // Each round after the first feeds DeepSeek the FULL prior state (original + every round's polished ZH,
    // repaired ZH, back-translated EN, and its GPTZero score) and asks for a fresh Chinese rewrite, then
    // repair -> back-translate -> re-score. Stops at score <= target with facts intact, or the iter cap.
    public static async Task<int> RunLoopAsync(IReadOnlyList<EvalCase> cases, string apiKey, EvalConfig config, DateTimeOffset startedAt)
    {
        var gptKey = Environment.GetEnvironmentVariable("GPTZero_API_KEY") ?? Environment.GetEnvironmentVariable("GPTZERO_API_KEY");
        var youdaoKey = Environment.GetEnvironmentVariable("YOUDAO_APP_KEY") ?? Environment.GetEnvironmentVariable("AppID") ?? Environment.GetEnvironmentVariable("YouDao_API_KEY");
        var youdaoSecret = Environment.GetEnvironmentVariable("YOUDAO_APP_SECRET") ?? Environment.GetEnvironmentVariable("AppSecret");
        var youdaoUrl = Environment.GetEnvironmentVariable("YOUDAO_API_URL") ?? "https://openapi.youdao.com/api";
        if (string.IsNullOrWhiteSpace(gptKey) || string.IsNullOrWhiteSpace(youdaoKey) || string.IsNullOrWhiteSpace(youdaoSecret))
        {
            Console.Error.WriteLine("TRANSLATION_DIRECT_LOOP: need GPTZero + Youdao keys in .env.local.");
            return 2;
        }

        if (cases.Count == 0)
        {
            Console.Error.WriteLine("TRANSLATION_DIRECT_LOOP: need one case (set EVAL_CASE_IDS).");
            return 2;
        }

        var c = cases[0];
        var iters = int.TryParse(Environment.GetEnvironmentVariable("TD_LOOP_ITERS"), out var it) && it > 0 ? it : 3;
        var target = int.TryParse(Environment.GetEnvironmentVariable("TD_LOOP_TARGET"), out var tg) && tg > 0 ? tg : 30;

        using var http = new HttpClient();
        using var youdaoHttp = new HttpClient();
        using var gptHttp = new HttpClient();
        using var judgeHttp = new HttpClient();
        var deepseek = new DeepSeekChatClient(http, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(60));
        var youdao = new YoudaoTranslationClient(youdaoHttp, youdaoKey!, youdaoSecret!, youdaoUrl, TimeSpan.FromSeconds(30));
        var judge = new SemanticEvalJudge(judgeHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));

        var original = c.InputDraft.Trim();
        Console.WriteLine($"=== Re-polish loop on {c.Id} ({c.Category}) · iters={iters} · target<={target}% ===\n");
        Console.WriteLine("ORIGINAL EN:\n" + original + "\n");

        var history = new List<(string ZhPol, string ZhRep, string BackEn, int? Score)>();
        int? bestFaithful = null;
        var report = new StringBuilder();
        report.AppendLine($"# Re-polish loop · {c.Id} ({c.Category}) · {startedAt:O}");
        report.AppendLine("\nORIGINAL EN:\n" + original + "\n");

        for (var round = 0; round <= iters; round++)
        {
            string zhPol;
            if (round == 0)
            {
                var zh0 = await youdao.TranslateAsync(original, "en", "zh-CHS", CancellationToken.None);
                if (!zh0.Success)
                {
                    Console.WriteLine($"round {round}: Youdao EN->ZH fail ({zh0.ErrorCode})");
                    break;
                }

                zhPol = await PolishAsync(deepseek, original, zh0.Text) ?? zh0.Text;
            }
            else
            {
                zhPol = await RePolishAsync(deepseek, original, history) ?? history[^1].ZhRep;
            }

            var zhRep = await RepairAsync(deepseek, original, zhPol) ?? zhPol;
            var back = await youdao.TranslateAsync(zhRep, "zh-CHS", "en", CancellationToken.None);
            if (!back.Success)
            {
                Console.WriteLine($"round {round}: Youdao ZH->EN fail ({back.ErrorCode})");
                break;
            }

            var final = back.Text.Trim();
            var (ai, cls, _, _, err) = await ScoreAsync(gptHttp, gptKey!, final, CancellationToken.None);
            var sem = await judge.VerifyAsync(final, c.MustKeep, c.MustNotClaim, CancellationToken.None);
            var factsPass = sem.Error is null && sem.FactsReallyPass && sem.RealForbidden == 0 && !sem.MeaningChanged;
            history.Add((zhPol, zhRep, final, ai));
            if (factsPass && ai is not null)
            {
                bestFaithful = bestFaithful is null ? ai : Math.Min(bestFaithful.Value, ai.Value);
            }

            Console.WriteLine(
                $"--- round {round}: GPTZero={(ai?.ToString() ?? "err")}% [{cls}]  "
                + $"facts={(factsPass ? "ok" : "x")} (sem={(sem.FactsReallyPass ? "ok" : "x")} mean={(sem.MeaningChanged ? "chg" : "ok")} forb={sem.RealForbidden}) ---");
            Console.WriteLine("  ZH : " + Oneline(zhRep, 150));
            Console.WriteLine("  EN : " + Oneline(final, 200));
            report.AppendLine($"## round {round}: GPTZero={ai}% [{cls}] facts={(factsPass ? "ok" : "x")}");
            report.AppendLine("ZH(repaired): " + zhRep);
            report.AppendLine("EN(back): " + final + "\n");

            if (err is null && ai is not null && ai <= target && factsPass)
            {
                Console.WriteLine("  -> hit target with facts intact. STOP.");
                break;
            }
        }

        Console.WriteLine($"\nVERDICT: best FAITHFUL GPTZero across loop = {(bestFaithful?.ToString() ?? "n/a")}%  (target <= {target}%)");
        Console.WriteLine($"CALLS: Youdao={youdao.CallCount} · DeepSeek={deepseek.CallCount} · GPTZero={history.Count}");
        await File.WriteAllTextAsync("/tmp/td-loop-report.md", report.ToString());
        Console.WriteLine("Full per-round texts: /tmp/td-loop-report.md");
        return 0;
    }

    // Dynamic re-polish: hands DeepSeek the original + the FULL history (each round's polished ZH, repaired
    // ZH, back-translated EN, and GPTZero score) and asks for a DIFFERENT Chinese rewrite. This is the
    // owner's "feed everything back" loop step. (Honest note: the action is still 'rewrite cleaner Chinese',
    // which back-translates cleaner — it does not point downhill on the detection score.)
    private static async Task<string?> RePolishAsync(
        DeepSeekChatClient ds, string en, IReadOnlyList<(string ZhPol, string ZhRep, string BackEn, int? Score)> history)
    {
        const string sys =
            "你在做一个降低 AI 文本检测分的中文改写循环。下面给你英文原文,以及之前每一轮的:中文润色版、事实修复版、"
            + "回译英文、以及该英文的 AI 检测分(0-100,越高越像 AI)。之前这些版本回译成英文后检测分仍然偏高。\n"
            + "请用一种【明显不同】的中文表达方式重新改写这段中文:换措辞、换句子节奏、换信息顺序,让它回译成英文后更像真人随手写的。\n"
            + "硬约束:数字/金额/日期/截止日/人名/发票号/套餐名与原文一致;否定与不确定语气不反转不软化;不新增信息;不加问候签名。\n"
            + "只返回 JSON:{\"polished\":\"<与历史都不同的新中文版>\"}";
        var sb = new StringBuilder();
        sb.Append("英文原文:\n").Append(en).Append("\n\n历史尝试:\n");
        for (var i = 0; i < history.Count; i++)
        {
            sb.Append($"--- 第{i + 1}轮 (回译英文 AI 检测分 {history[i].Score?.ToString() ?? "?"}%) ---\n");
            sb.Append("中文润色版: ").Append(history[i].ZhPol).Append('\n');
            sb.Append("事实修复版: ").Append(history[i].ZhRep).Append('\n');
            sb.Append("回译英文: ").Append(history[i].BackEn).Append("\n\n");
        }

        sb.Append("请给出与以上都不同的新中文版,目标是回译英文后检测分更低,同时事实完全不变。");
        var json = await ds.CompleteAsync(sys, sb.ToString(), 1800, 0.85, CancellationToken.None);
        return ExtractField(json, "polished");
    }

    // Owner's hypothesis test (ESSAY_LOOP=1): use the perplexity-increasing ESSAY prompt as the per-round
    // polish inside a fact-gated loop. Each round: essay-rough-rewrite the Youdao Chinese (with feedback to
    // diverge from prior rounds) -> minimal fact-repair (style-preserving) -> Youdao back -> GPTZero + fact
    // gate (SemanticEvalJudge). Accept only low-score AND facts-intact; otherwise reroll. EL_FILE/EL_KEEP/
    // EL_FORBID/EL_ITERS/EL_TARGET.
    public static async Task<int> RunEssayLoopAsync(string apiKey, EvalConfig config)
    {
        var gptKey = Environment.GetEnvironmentVariable("GPTZero_API_KEY") ?? Environment.GetEnvironmentVariable("GPTZERO_API_KEY");
        var youdaoKey = Environment.GetEnvironmentVariable("YOUDAO_APP_KEY") ?? Environment.GetEnvironmentVariable("AppID") ?? Environment.GetEnvironmentVariable("YouDao_API_KEY");
        var youdaoSecret = Environment.GetEnvironmentVariable("YOUDAO_APP_SECRET") ?? Environment.GetEnvironmentVariable("AppSecret");
        var youdaoUrl = Environment.GetEnvironmentVariable("YOUDAO_API_URL") ?? "https://openapi.youdao.com/api";
        var enFile = Environment.GetEnvironmentVariable("EL_FILE");
        if (string.IsNullOrWhiteSpace(gptKey) || string.IsNullOrWhiteSpace(youdaoKey) || string.IsNullOrWhiteSpace(youdaoSecret))
        {
            Console.Error.WriteLine("ESSAY_LOOP: need GPTZero + Youdao keys in .env.local.");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(enFile) || !File.Exists(enFile))
        {
            Console.Error.WriteLine("ESSAY_LOOP: set EL_FILE=path (the English email).");
            return 2;
        }

        var en = (await File.ReadAllTextAsync(enFile)).Trim();
        var keepFile = Environment.GetEnvironmentVariable("EL_KEEP");
        var forbidFile = Environment.GetEnvironmentVariable("EL_FORBID");
        var mustKeep = !string.IsNullOrWhiteSpace(keepFile) && File.Exists(keepFile)
            ? (await File.ReadAllLinesAsync(keepFile)).Where(l => !string.IsNullOrWhiteSpace(l)).ToList()
            : new List<string>();
        var mustNotClaim = !string.IsNullOrWhiteSpace(forbidFile) && File.Exists(forbidFile)
            ? (await File.ReadAllLinesAsync(forbidFile)).Where(l => !string.IsNullOrWhiteSpace(l)).ToList()
            : new List<string>();
        var iters = int.TryParse(Environment.GetEnvironmentVariable("EL_ITERS"), out var it) && it > 0 ? it : 5;
        var target = int.TryParse(Environment.GetEnvironmentVariable("EL_TARGET"), out var tg) && tg > 0 ? tg : 30;

        using var http = new HttpClient();
        using var youdaoHttp = new HttpClient();
        using var gptHttp = new HttpClient();
        using var judgeHttp = new HttpClient();
        var deepseek = new DeepSeekChatClient(http, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(120));
        var youdao = new YoudaoTranslationClient(youdaoHttp, youdaoKey!, youdaoSecret!, youdaoUrl, TimeSpan.FromSeconds(30));
        var judge = new SemanticEvalJudge(judgeHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));

        var zh0r = await youdao.TranslateAsync(en, "en", "zh-CHS", CancellationToken.None);
        if (!zh0r.Success)
        {
            Console.Error.WriteLine($"Youdao EN->ZH fail: {zh0r.ErrorCode}");
            return 1;
        }

        var zh0 = zh0r.Text;
        Console.WriteLine($"=== Essay-prompt fact-gated loop · iters={iters} · target<={target}% · mustKeep={mustKeep.Count} forbid={mustNotClaim.Count} ===\n");
        var history = new List<(string Zh, string En, int? Score)>();
        int? bestFaithful = null;
        var report = new StringBuilder();

        for (var round = 0; round <= iters; round++)
        {
            var zhRough = await EssayRoundAsync(deepseek, zh0, history) ?? zh0;
            var zhRep = await RepairAsync(deepseek, en, zhRough) ?? zhRough;
            var backR = await youdao.TranslateAsync(zhRep, "zh-CHS", "en", CancellationToken.None);
            if (!backR.Success)
            {
                Console.WriteLine($"round {round}: Youdao ZH->EN fail ({backR.ErrorCode})");
                break;
            }

            var final = backR.Text.Trim();
            var (ai, cls, _, _, err) = await ScoreAsync(gptHttp, gptKey!, final, CancellationToken.None);
            var sem = await judge.VerifyAsync(final, mustKeep, mustNotClaim, CancellationToken.None);
            var factsPass = sem.Error is null && sem.FactsReallyPass && sem.RealForbidden == 0 && !sem.MeaningChanged;
            history.Add((zhRough, final, ai));
            if (factsPass && ai is not null)
            {
                bestFaithful = bestFaithful is null ? ai : Math.Min(bestFaithful.Value, ai.Value);
            }

            Console.WriteLine(
                $"--- round {round}: GPTZero={(ai?.ToString() ?? "err")}% [{cls}] "
                + $"facts={(factsPass ? "ok" : "x")} (sem={(sem.FactsReallyPass ? "ok" : "x")} mean={(sem.MeaningChanged ? "chg" : "ok")} forb={sem.RealForbidden}) ---");
            Console.WriteLine("  EN: " + Oneline(final, 220));
            report.AppendLine($"## round {round}: GPTZero={ai}% [{cls}] facts={(factsPass ? "ok" : "x")} (mean={(sem.MeaningChanged ? "chg" : "ok")} forb={sem.RealForbidden})");
            report.AppendLine(final + "\n");

            if (err is null && ai is not null && ai <= target && factsPass)
            {
                Console.WriteLine("  -> HIT: low score + facts intact. STOP.");
                break;
            }
        }

        Console.WriteLine($"\nVERDICT: best FAITHFUL GPTZero across loop = {(bestFaithful?.ToString() ?? "n/a")}% (target <= {target}%)");
        Console.WriteLine($"CALLS: Youdao={youdao.CallCount} · DeepSeek={deepseek.CallCount} · GPTZero={history.Count}");
        await File.WriteAllTextAsync("/tmp/essay-loop-report.md", report.ToString());
        Console.WriteLine("Per-round texts: /tmp/essay-loop-report.md");
        return 0;
    }

    private static async Task<string?> EssayRoundAsync(DeepSeekChatClient ds, string zh0, IReadOnlyList<(string Zh, string En, int? Score)> history)
    {
        var sys = EssayPolish.OwnerPrompt + "\n\n注意：把第二遍完成后的终稿全文放进 JSON 返回，不要任何额外文字：{\"final\":\"<终稿全文>\"}";
        if (history.Count > 0)
        {
            var sb = new StringBuilder();
            sb.Append("\n\n之前这几版回译成英文后，AI 检测分仍然偏高：\n");
            for (var i = 0; i < history.Count; i++)
            {
                sb.Append($"--- 第{i + 1}版 (回译英文检测 {history[i].Score?.ToString() ?? "?"}% AI) ---\n");
                sb.Append(history[i].En).Append('\n');
            }

            sb.Append("\n请这次用【更不一样、更口语、更出人意料】的写法重写，和上面每一版都明显不同，让回译英文的检测分更低；事实绝不能变。");
            sys += sb.ToString();
        }

        var raw = await ds.CompleteAsync(sys, "原文如下：\n" + zh0, 3000, 0.95, CancellationToken.None);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.TryGetProperty("final", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()?.Trim() : raw;
        }
        catch (JsonException)
        {
            return raw;
        }
    }

    // FULL version (MASK_ESSAY_LOOP=1): hard facts masked as protected tokens BEFORE the rough rewrite
    // (currency/dates/ids can't drift); semantic facts held via an explicit relationship checklist in the
    // prompt + per-relationship judge + drift-feedback that tightens constraints each round; accept only
    // low-score AND facts-intact, else reroll. EL_FILE/EL_KEEP/EL_FORBID/EL_SEM/EL_ITERS/EL_TARGET.
    public static async Task<int> RunMaskedEssayLoopAsync(string apiKey, EvalConfig config)
    {
        var gptKey = Environment.GetEnvironmentVariable("GPTZero_API_KEY") ?? Environment.GetEnvironmentVariable("GPTZERO_API_KEY");
        var youdaoKey = Environment.GetEnvironmentVariable("YOUDAO_APP_KEY") ?? Environment.GetEnvironmentVariable("AppID") ?? Environment.GetEnvironmentVariable("YouDao_API_KEY");
        var youdaoSecret = Environment.GetEnvironmentVariable("YOUDAO_APP_SECRET") ?? Environment.GetEnvironmentVariable("AppSecret");
        var youdaoUrl = Environment.GetEnvironmentVariable("YOUDAO_API_URL") ?? "https://openapi.youdao.com/api";
        var enFile = Environment.GetEnvironmentVariable("EL_FILE");
        if (string.IsNullOrWhiteSpace(gptKey) || string.IsNullOrWhiteSpace(youdaoKey) || string.IsNullOrWhiteSpace(youdaoSecret))
        {
            Console.Error.WriteLine("MASK_ESSAY_LOOP: need GPTZero + Youdao keys in .env.local.");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(enFile) || !File.Exists(enFile))
        {
            Console.Error.WriteLine("MASK_ESSAY_LOOP: set EL_FILE=path (English email).");
            return 2;
        }

        var en = (await File.ReadAllTextAsync(enFile)).Trim();
        var keepFile = Environment.GetEnvironmentVariable("EL_KEEP");
        var forbidFile = Environment.GetEnvironmentVariable("EL_FORBID");
        var semFile = Environment.GetEnvironmentVariable("EL_SEM");
        var mustKeep = !string.IsNullOrWhiteSpace(keepFile) && File.Exists(keepFile) ? (await File.ReadAllLinesAsync(keepFile)).Where(l => !string.IsNullOrWhiteSpace(l)).ToList() : new List<string>();
        var mustNotClaim = !string.IsNullOrWhiteSpace(forbidFile) && File.Exists(forbidFile) ? (await File.ReadAllLinesAsync(forbidFile)).Where(l => !string.IsNullOrWhiteSpace(l)).ToList() : new List<string>();
        var semChecklist = !string.IsNullOrWhiteSpace(semFile) && File.Exists(semFile) ? (await File.ReadAllTextAsync(semFile)).Trim() : string.Empty;
        var promptFile = Environment.GetEnvironmentVariable("EL_PROMPT_FILE");
        var basePrompt = !string.IsNullOrWhiteSpace(promptFile) && File.Exists(promptFile) ? (await File.ReadAllTextAsync(promptFile)).Trim() : EssayPolish.OwnerPrompt;
        var iters = int.TryParse(Environment.GetEnvironmentVariable("EL_ITERS"), out var it) && it > 0 ? it : 6;
        var target = int.TryParse(Environment.GetEnvironmentVariable("EL_TARGET"), out var tg) && tg > 0 ? tg : 30;

        using var http = new HttpClient();
        using var youdaoHttp = new HttpClient();
        using var gptHttp = new HttpClient();
        using var judgeHttp = new HttpClient();
        var deepseek = new DeepSeekChatClient(http, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(120));
        var youdao = new YoudaoTranslationClient(youdaoHttp, youdaoKey!, youdaoSecret!, youdaoUrl, TimeSpan.FromSeconds(30));
        var judge = new SemanticEvalJudge(judgeHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));

        var zh0r = await youdao.TranslateAsync(en, "en", "zh-CHS", CancellationToken.None);
        if (!zh0r.Success)
        {
            Console.Error.WriteLine($"Youdao EN->ZH fail: {zh0r.ErrorCode}");
            return 1;
        }

        var (maskedZh, map) = MaskHardFacts(zh0r.Text);
        Console.WriteLine($"=== Masked essay loop · iters={iters} target<={target}% · masked {map.Count} hard tokens · sem={(semChecklist.Length > 0 ? "on" : "off")} mustKeep={mustKeep.Count} ===\n");

        var history = new List<(string Zh, string En, int? Score)>();
        IReadOnlyList<string> lastDrift = new List<string>();
        int? bestFaithful = null;
        var report = new StringBuilder();

        for (var round = 0; round <= iters; round++)
        {
            var zhRough = await MaskedEssayRound(deepseek, basePrompt, maskedZh, semChecklist, lastDrift) ?? maskedZh;
            var backR = await youdao.TranslateAsync(zhRough, "zh-CHS", "en", CancellationToken.None);
            if (!backR.Success)
            {
                Console.WriteLine($"round {round}: Youdao ZH->EN fail ({backR.ErrorCode})");
                break;
            }

            var survived = map.Count(kv => backR.Text.Contains(kv.Key, StringComparison.Ordinal));
            var finalEn = Unmask(backR.Text.Trim(), map);
            var (ai, cls, _, _, err) = await ScoreAsync(gptHttp, gptKey!, finalEn, CancellationToken.None);
            var sem = await judge.VerifyAsync(finalEn, mustKeep, mustNotClaim, CancellationToken.None);
            var factsPass = sem.Error is null && sem.FactsReallyPass && sem.RealForbidden == 0 && !sem.MeaningChanged;
            lastDrift = sem.Error is null
                ? sem.Facts.Where(f => f.Status is "missing" or "contradicted").Select(f => f.Fact).ToList()
                : new List<string>();
            history.Add((zhRough, finalEn, ai));
            if (factsPass && ai is not null)
            {
                bestFaithful = bestFaithful is null ? ai : Math.Min(bestFaithful.Value, ai.Value);
            }

            Console.WriteLine(
                $"--- round {round}: GPTZero={(ai?.ToString() ?? "err")}% [{cls}] facts={(factsPass ? "ok" : "x")} "
                + $"(sem={(sem.FactsReallyPass ? "ok" : "x")} mean={(sem.MeaningChanged ? "chg" : "ok")} forb={sem.RealForbidden}) · tokens {survived}/{map.Count} ---");
            Console.WriteLine("  EN: " + Oneline(finalEn, 220));
            if (lastDrift.Count > 0)
            {
                Console.WriteLine("  drift: " + string.Join(" | ", lastDrift.Take(4)));
            }

            report.AppendLine($"## round {round}: GPTZero={ai}% [{cls}] facts={(factsPass ? "ok" : "x")} tokens {survived}/{map.Count}");
            report.AppendLine(finalEn + "\n");

            if (err is null && ai is not null && ai <= target && factsPass)
            {
                Console.WriteLine("  -> HIT: low score + facts intact. STOP.");
                break;
            }
        }

        Console.WriteLine($"\nVERDICT: best FAITHFUL GPTZero = {(bestFaithful?.ToString() ?? "n/a")}% (target <= {target}%)");
        Console.WriteLine($"CALLS: Youdao={youdao.CallCount} · DeepSeek={deepseek.CallCount} · GPTZero={history.Count}");
        await File.WriteAllTextAsync("/tmp/masked-essay-loop-report.md", report.ToString());
        Console.WriteLine("Per-round texts: /tmp/masked-essay-loop-report.md");
        return 0;
    }

    private static async Task<string?> MaskedEssayRound(DeepSeekChatClient ds, string basePrompt, string maskedZh, string semChecklist, IReadOnlyList<string> lastDrift)
    {
        var sys = basePrompt
            + "\n\n【保护令牌】文中形如 QZAN000QZ 的标记代表不可改动的硬事实(金额/日期/单号)。请【原样保留】,绝不翻译、改动、删除、拆开或在其中加空格或换行。";
        if (semChecklist.Length > 0)
        {
            sys += "\n\n【必须保住的语义关系】(改写时,下面每条的方向、极性、主体都不能变):\n" + semChecklist;
        }

        if (lastDrift.Count > 0)
        {
            sys += "\n\n【上一轮把下面这些弄丢或弄反了,这一轮务必原意保住】:\n- " + string.Join("\n- ", lastDrift.Take(8));
        }

        sys += "\n\n注意:把第二遍终稿全文放进 JSON 返回,不要任何额外文字:{\"final\":\"<终稿全文>\"}";
        var raw = await ds.CompleteAsync(sys, "原文如下(含保护令牌)：\n" + maskedZh, 3000, 0.9, CancellationToken.None);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.TryGetProperty("final", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()?.Trim() : raw;
        }
        catch (JsonException)
        {
            return raw;
        }
    }

    // Mask regex-findable HARD facts in the Chinese (order IDs, $ amounts, dates) as bracket-free QZAN
    // sentinels that ride through the rewrite + Youdao back-translation; the map restores the EN canonical
    // value (so "$29.40" can't drift to "29.4 yuan", "April 18" can't drift, etc.).
    private static (string Masked, List<(string Key, string En)> Map) MaskHardFacts(string zh)
    {
        var map = new List<(string Key, string En)>();
        var masked = zh;
        masked = MaskPattern(masked, @"(?:R|INV|A)-\d+", v => v, map);
        masked = MaskPattern(masked, @"\d+(?:\.\d+)?\s*美元", v => "$" + Regex.Match(v, @"\d+(?:\.\d+)?").Value, map);
        masked = MaskPattern(masked, @"\$\s?\d+(?:\.\d+)?", v => v.Replace(" ", string.Empty, StringComparison.Ordinal), map);
        masked = MaskPattern(masked, @"(\d+)月(\d+)日", MonthDay, map);
        return (masked, map);
    }

    private static string MaskPattern(string text, string pattern, Func<string, string> toEn, List<(string Key, string En)> map)
    {
        var local = map;
        return Regex.Replace(text, pattern, m =>
        {
            var key = $"QZAN{local.Count:000}QZ";
            local.Add((key, toEn(m.Value)));
            return key;
        });
    }

    private static string MonthDay(string zhDate)
    {
        var m = Regex.Match(zhDate, @"(\d+)月(\d+)日");
        if (!m.Success)
        {
            return zhDate;
        }

        var mo = int.Parse(m.Groups[1].Value);
        string[] months = { string.Empty, "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
        var name = mo is >= 1 and <= 12 ? months[mo] : m.Groups[1].Value + "月";
        return name + " " + m.Groups[2].Value;
    }

    private static string Unmask(string text, List<(string Key, string En)> map)
    {
        foreach (var (key, enVal) in map)
        {
            text = text.Replace(key, enVal, StringComparison.Ordinal);
        }

        return text;
    }

    // PHASE 2 (SURGICAL_REPAIR_LOOP=1): full auto pipeline. Rough essay translation → if GPTZero is low,
    // run FaithfulnessGate to FIND drifts → AUTO surgical-repair (replace each CandidateSpan with ExpectedFix)
    // → re-score + re-gate → ACCEPT on low + faithful (0 drifts, 0 unrepairable), else reroll. EL_FILE (source
    // English), EL_ITERS, EL_TARGET. This automates what was done by hand (Jamie 1->1%, Celestine 32->29%).
    public static async Task<int> RunSurgicalRepairLoopAsync(string apiKey, EvalConfig config)
    {
        var gptKey = Environment.GetEnvironmentVariable("GPTZero_API_KEY") ?? Environment.GetEnvironmentVariable("GPTZERO_API_KEY");
        var youdaoKey = Environment.GetEnvironmentVariable("YOUDAO_APP_KEY") ?? Environment.GetEnvironmentVariable("AppID") ?? Environment.GetEnvironmentVariable("YouDao_API_KEY");
        var youdaoSecret = Environment.GetEnvironmentVariable("YOUDAO_APP_SECRET") ?? Environment.GetEnvironmentVariable("AppSecret");
        var youdaoUrl = Environment.GetEnvironmentVariable("YOUDAO_API_URL") ?? "https://openapi.youdao.com/api";
        var enFile = Environment.GetEnvironmentVariable("EL_FILE");
        if (string.IsNullOrWhiteSpace(gptKey) || string.IsNullOrWhiteSpace(youdaoKey) || string.IsNullOrWhiteSpace(youdaoSecret) || string.IsNullOrWhiteSpace(enFile) || !File.Exists(enFile))
        {
            Console.Error.WriteLine("SURGICAL_REPAIR_LOOP: need GPTZero + Youdao keys and EL_FILE.");
            return 2;
        }

        var source = (await File.ReadAllTextAsync(enFile)).Trim();
        var iters = int.TryParse(Environment.GetEnvironmentVariable("EL_ITERS"), out var it) && it > 0 ? it : 4;
        var target = int.TryParse(Environment.GetEnvironmentVariable("EL_TARGET"), out var tg) && tg > 0 ? tg : 30;
        // EL_ZH_REPAIR=1 (owner's idea): repair drifts in the CHINESE and re-back-translate, never edit the
        // English (so the back-translation surface — the low-score property — is preserved).
        var zhRepairMode = (Environment.GetEnvironmentVariable("EL_ZH_REPAIR") ?? string.Empty).Trim() is "1" or "true";

        using var http = new HttpClient();
        using var youdaoHttp = new HttpClient();
        using var gptHttp = new HttpClient();
        var deepseek = new DeepSeekChatClient(http, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(120));
        var youdao = new YoudaoTranslationClient(youdaoHttp, youdaoKey!, youdaoSecret!, youdaoUrl, TimeSpan.FromSeconds(30));
        var gate = new FaithfulnessGate((s, u, gct) => deepseek.CompleteAsync(s, u, 2000, 0, gct));

        var zh0r = await youdao.TranslateAsync(source, "en", "zh-CHS", CancellationToken.None);
        if (!zh0r.Success)
        {
            Console.Error.WriteLine($"Youdao EN->ZH fail: {zh0r.ErrorCode}");
            return 1;
        }

        var zh0 = zh0r.Text;
        var empty = new List<(string Zh, string En, int? Score)>();
        Console.WriteLine($"=== Surgical-repair loop · iters={iters} target<={target}% ===\n");
        string? accepted = null;
        int? acceptedScore = null;
        var report = new StringBuilder();

        for (var round = 0; round <= iters; round++)
        {
            var zhRough = await EssayRoundAsync(deepseek, zh0, empty) ?? zh0;
            var back = await youdao.TranslateAsync(zhRough, "zh-CHS", "en", CancellationToken.None);
            if (!back.Success)
            {
                Console.WriteLine($"--- round {round}: Youdao ZH->EN fail ({back.ErrorCode}) ---");
                continue;
            }

            var cand = back.Text.Trim();
            var (ai, _, _, _, err) = await ScoreAsync(gptHttp, gptKey!, cand, CancellationToken.None);
            if (err is not null || ai is null)
            {
                Console.WriteLine($"--- round {round}: GPTZero error ({err}) ---");
                continue;
            }

            if (ai > target)
            {
                Console.WriteLine($"--- round {round}: GPTZero={ai}% (> {target}, reroll) ---");
                continue;
            }

            var fr = await gate.EvaluateAsync(source, cand, CancellationToken.None);
            if (fr.Passed)
            {
                Console.WriteLine($"--- round {round}: GPTZero={ai}% + faithful (no repair needed) -> ACCEPT ---");
                accepted = cand;
                acceptedScore = ai;
                report.AppendLine($"## round {round}: {ai}% + faithful (no repair)\n{cand}\n");
                break;
            }

            string repaired;
            var unrepCount = 0;
            if (zhRepairMode)
            {
                // Fix the drifts in the Chinese, then re-back-translate — English is never span-edited.
                var zhFixed = await RepairChineseForDrifts(deepseek, source, zhRough, fr.Drifts) ?? zhRough;
                var back2 = await youdao.TranslateAsync(zhFixed, "zh-CHS", "en", CancellationToken.None);
                repaired = back2.Success ? back2.Text.Trim() : cand;
            }
            else
            {
                var (rep, unrep) = ApplySurgicalRepair(cand, fr.Drifts);
                repaired = rep;
                unrepCount = unrep.Count;
            }

            var (ai2, _, _, _, err2) = await ScoreAsync(gptHttp, gptKey!, repaired, CancellationToken.None);
            var fr2 = await gate.EvaluateAsync(source, repaired, CancellationToken.None);
            var ok = err2 is null && ai2 is not null && ai2 <= target && fr2.Passed && unrepCount == 0;
            Console.WriteLine(
                $"--- round {round}: GPTZero {ai}%->{(ai2?.ToString() ?? "err")}% · drifts {fr.Drifts.Count}->{fr2.Drifts.Count} · "
                + $"{(zhRepairMode ? "zh-repair" : $"unrepairable={unrepCount}")} · {(ok ? "ACCEPT (low + faithful)" : "reroll")} ---");
            report.AppendLine($"## round {round}: {ai}%->{ai2}% drifts {fr.Drifts.Count}->{fr2.Drifts.Count} {(zhRepairMode ? "zh-repair" : $"unrep {unrepCount}")} {(ok ? "ACCEPT" : "reroll")}");
            report.AppendLine("repaired: " + repaired + "\n");
            if (ok)
            {
                accepted = repaired;
                acceptedScore = ai2;
                break;
            }
        }

        Console.WriteLine(accepted is null
            ? $"\nVERDICT: no low+faithful candidate within {iters + 1} rounds"
            : $"\nVERDICT: ACCEPTED at {acceptedScore}% AI + faithful (auto surgical-repair)");
        if (accepted is not null)
        {
            Console.WriteLine("\nFINAL:\n" + accepted);
        }

        Console.WriteLine($"\nCALLS: Youdao={youdao.CallCount} · DeepSeek={deepseek.CallCount}");
        await File.WriteAllTextAsync("/tmp/surgical-loop-report.md", report.ToString());
        return 0;
    }

    // Apply each drift's CandidateSpan -> ExpectedFix (minimal string replace; empty fix = deletion for
    // unsupported additions). Drifts with no CandidateSpan (pure omissions) can't be replaced -> unrepairable.
    private static (string Repaired, List<DriftSpan> Unrepairable) ApplySurgicalRepair(string candidate, IReadOnlyList<DriftSpan> drifts)
    {
        var text = candidate;
        var unrep = new List<DriftSpan>();
        foreach (var d in drifts)
        {
            if (string.IsNullOrEmpty(d.CandidateSpan))
            {
                unrep.Add(d);
                continue;
            }

            if (text.Contains(d.CandidateSpan, StringComparison.Ordinal))
            {
                text = text.Replace(d.CandidateSpan, d.ExpectedFix, StringComparison.Ordinal);
            }
            else
            {
                var idx = text.IndexOf(d.CandidateSpan, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    text = text[..idx] + d.ExpectedFix + text[(idx + d.CandidateSpan.Length)..];
                }
                else
                {
                    unrep.Add(d);
                }
            }
        }

        // Tidy whitespace/punctuation left by deletions.
        text = Regex.Replace(text, @"[ \t]{2,}", " ");
        text = Regex.Replace(text, @"\s+([.,;!?])", "$1");
        return (text.Trim(), unrep);
    }

    // One-shot (ZH_SURGICAL_ONCE=1): run the cross-lingual gate + surgical repair on a GIVEN Chinese candidate
    // (ZS_ZH file) against EL_FILE source, then back-translate + score. Shows whether the gate now finds a
    // specific drift (e.g. an added '正好可以往前赶') and whether surgical replace excises it without rewriting.
    public static async Task<int> RunZhSurgicalOnceAsync(string apiKey, EvalConfig config)
    {
        var gptKey = Environment.GetEnvironmentVariable("GPTZero_API_KEY") ?? Environment.GetEnvironmentVariable("GPTZERO_API_KEY");
        var youdaoKey = Environment.GetEnvironmentVariable("YOUDAO_APP_KEY") ?? Environment.GetEnvironmentVariable("AppID") ?? Environment.GetEnvironmentVariable("YouDao_API_KEY");
        var youdaoSecret = Environment.GetEnvironmentVariable("YOUDAO_APP_SECRET") ?? Environment.GetEnvironmentVariable("AppSecret");
        var youdaoUrl = Environment.GetEnvironmentVariable("YOUDAO_API_URL") ?? "https://openapi.youdao.com/api";
        var enFile = Environment.GetEnvironmentVariable("EL_FILE");
        var zhFile = Environment.GetEnvironmentVariable("ZS_ZH");
        if (string.IsNullOrWhiteSpace(gptKey) || string.IsNullOrWhiteSpace(youdaoKey) || string.IsNullOrWhiteSpace(youdaoSecret)
            || string.IsNullOrWhiteSpace(enFile) || !File.Exists(enFile) || string.IsNullOrWhiteSpace(zhFile) || !File.Exists(zhFile))
        {
            Console.Error.WriteLine("ZH_SURGICAL_ONCE: need GPTZero + Youdao keys, EL_FILE (English source), ZS_ZH (Chinese candidate).");
            return 2;
        }

        var source = (await File.ReadAllTextAsync(enFile)).Trim();
        var zh = (await File.ReadAllTextAsync(zhFile)).Trim();
        using var http = new HttpClient();
        using var youdaoHttp = new HttpClient();
        using var gptHttp = new HttpClient();
        var deepseek = new DeepSeekChatClient(http, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(120));
        var youdao = new YoudaoTranslationClient(youdaoHttp, youdaoKey!, youdaoSecret!, youdaoUrl, TimeSpan.FromSeconds(30));
        var gate = new FaithfulnessGate((s, u, gct) => deepseek.CompleteAsync(s, u, 2000, 0, gct));

        Console.WriteLine("================ ENGLISH SOURCE ================\n" + source + "\n");
        Console.WriteLine("================ ZH (input, pre-surgery) ================\n" + zh + "\n");

        var (drifts, xerr) = await gate.EvaluateCrossLingualAsync(source, zh, CancellationToken.None);
        Console.WriteLine($"================ cross-lingual gate: {drifts.Count} Chinese drift(s){(xerr is null ? string.Empty : $" (err {xerr})")} ================");
        foreach (var d in drifts)
        {
            Console.WriteLine($"  [{d.Kind}] \"{d.CandidateSpan}\" -> \"{d.ExpectedFix}\"  ({d.Why})");
        }

        var (zhFixed, _) = ApplySurgicalRepair(zh, drifts);
        Console.WriteLine("\n================ ZH (after surgical repair) ================\n" + zhFixed + "\n");

        var back = await youdao.TranslateAsync(zhFixed, "zh-CHS", "en", CancellationToken.None);
        var final = back.Success ? back.Text.Trim() : $"(youdao fail: {back.ErrorCode})";
        Console.WriteLine("================ EN (back-translated, NOT edited) ================\n" + final + "\n");

        var (ai, _, _, _, serr) = await ScoreAsync(gptHttp, gptKey!, final, CancellationToken.None);
        var frEn = await gate.EvaluateAsync(source, final, CancellationToken.None);
        Console.WriteLine($"GPTZero = {(ai?.ToString() ?? "err")}%{(serr is null ? string.Empty : $" ({serr})")}  ·  final-EN residual drifts = {frEn.Drifts.Count}");
        foreach (var d in frEn.Drifts.Take(8))
        {
            Console.WriteLine($"  [EN {d.Kind}] \"{d.SourceValue}\" -> \"{d.CandidateSpan ?? "(missing)"}\"");
        }

        return 0;
    }

    // EN_SURGICAL_ONCE: take a rough back-translated English candidate (ES_EN) for EL_FILE source, score it, run
    // the EN faithfulness gate, surgically replace ONLY the drifted spans (span -> expected_fix, no rewrite), then
    // re-score + re-check residual drifts. Deterministic apply (reproducible). Answers: if we STOP at a
    // low-but-drifted round and precisely fix its drifts in place, what does the score become?
    public static async Task<int> RunEnSurgicalOnceAsync(string apiKey, EvalConfig config)
    {
        var gptKey = Environment.GetEnvironmentVariable("GPTZero_API_KEY") ?? Environment.GetEnvironmentVariable("GPTZERO_API_KEY");
        var enFile = Environment.GetEnvironmentVariable("EL_FILE");
        var candFile = Environment.GetEnvironmentVariable("ES_EN");
        if (string.IsNullOrWhiteSpace(gptKey) || string.IsNullOrWhiteSpace(enFile) || !File.Exists(enFile)
            || string.IsNullOrWhiteSpace(candFile) || !File.Exists(candFile))
        {
            Console.Error.WriteLine("EN_SURGICAL_ONCE: need GPTZero key, EL_FILE (English source), ES_EN (rough English candidate).");
            return 2;
        }

        var source = (await File.ReadAllTextAsync(enFile)).Trim();
        var cand = (await File.ReadAllTextAsync(candFile)).Trim();
        using var http = new HttpClient();
        using var gptHttp = new HttpClient();
        var deepseek = new DeepSeekChatClient(http, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(120));
        var gate = new FaithfulnessGate((s, u, gct) => deepseek.CompleteAsync(s, u, 2000, 0, gct));

        Console.WriteLine("================ ENGLISH SOURCE ================\n" + source + "\n");
        Console.WriteLine("================ EN (candidate, pre-surgery) ================\n" + cand + "\n");

        var (aiBefore, _, _, _, errBefore) = await ScoreAsync(gptHttp, gptKey!, cand, CancellationToken.None);
        Console.WriteLine($"GPTZero (before) = {(aiBefore?.ToString() ?? "err")}%{(errBefore is null ? string.Empty : $" ({errBefore})")}\n");

        var report = await gate.EvaluateAsync(source, cand, CancellationToken.None);
        Console.WriteLine($"================ EN gate: {report.Drifts.Count} drift(s){(report.Error is null ? string.Empty : $" (err {report.Error})")} ================");
        foreach (var d in report.Drifts)
        {
            Console.WriteLine($"  [{d.Kind}] \"{d.CandidateSpan ?? "(missing)"}\" -> \"{d.ExpectedFix}\"  ({d.Why})");
        }

        var (repaired, unrep) = ApplySurgicalRepair(cand, report.Drifts);
        Console.WriteLine("\n================ EN (after surgical span->fix) ================\n" + repaired + "\n");
        if (unrep.Count > 0)
        {
            Console.WriteLine($"({unrep.Count} drift(s) had no exact span to replace — left as-is)\n");
        }

        var (aiAfter, _, _, _, errAfter) = await ScoreAsync(gptHttp, gptKey!, repaired, CancellationToken.None);
        var residual = await gate.EvaluateAsync(source, repaired, CancellationToken.None);
        Console.WriteLine($"GPTZero (after) = {(aiAfter?.ToString() ?? "err")}%{(errAfter is null ? string.Empty : $" ({errAfter})")}  ·  residual drifts = {residual.Drifts.Count}");
        foreach (var d in residual.Drifts.Take(8))
        {
            Console.WriteLine($"  [residual {d.Kind}] \"{d.SourceValue}\" -> \"{d.CandidateSpan ?? "(missing)"}\"");
        }

        Console.WriteLine($"\nCALLS: DeepSeek={deepseek.CallCount}");
        return 0;
    }

    // Owner's CORRECTED idea (verbose): SURGICAL repair in the CHINESE — cross-lingual gate finds Chinese drift
    // spans, ApplySurgicalRepair replaces ONLY those (not an LLM rewrite) → one back-translation → measure the
    // English with the EN gate. Prints English source + each round's polished ZH, surgically-repaired ZH,
    // back-translated EN, and score. ZH_SURGICAL_LOOP=1, EL_FILE, EL_ITERS, EL_TARGET.
    public static async Task<int> RunZhSurgicalLoopAsync(string apiKey, EvalConfig config)
    {
        var gptKey = Environment.GetEnvironmentVariable("GPTZero_API_KEY") ?? Environment.GetEnvironmentVariable("GPTZERO_API_KEY");
        var youdaoKey = Environment.GetEnvironmentVariable("YOUDAO_APP_KEY") ?? Environment.GetEnvironmentVariable("AppID") ?? Environment.GetEnvironmentVariable("YouDao_API_KEY");
        var youdaoSecret = Environment.GetEnvironmentVariable("YOUDAO_APP_SECRET") ?? Environment.GetEnvironmentVariable("AppSecret");
        var youdaoUrl = Environment.GetEnvironmentVariable("YOUDAO_API_URL") ?? "https://openapi.youdao.com/api";
        var enFile = Environment.GetEnvironmentVariable("EL_FILE");
        if (string.IsNullOrWhiteSpace(gptKey) || string.IsNullOrWhiteSpace(youdaoKey) || string.IsNullOrWhiteSpace(youdaoSecret) || string.IsNullOrWhiteSpace(enFile) || !File.Exists(enFile))
        {
            Console.Error.WriteLine("ZH_SURGICAL_LOOP: need GPTZero + Youdao keys and EL_FILE.");
            return 2;
        }

        var source = (await File.ReadAllTextAsync(enFile)).Trim();
        var iters = int.TryParse(Environment.GetEnvironmentVariable("EL_ITERS"), out var it) && it > 0 ? it : 3;
        var target = int.TryParse(Environment.GetEnvironmentVariable("EL_TARGET"), out var tg) && tg > 0 ? tg : 30;

        using var http = new HttpClient();
        using var youdaoHttp = new HttpClient();
        using var gptHttp = new HttpClient();
        var deepseek = new DeepSeekChatClient(http, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(120));
        var youdao = new YoudaoTranslationClient(youdaoHttp, youdaoKey!, youdaoSecret!, youdaoUrl, TimeSpan.FromSeconds(30));
        var gate = new FaithfulnessGate((s, u, gct) => deepseek.CompleteAsync(s, u, 2000, 0, gct));

        var zh0r = await youdao.TranslateAsync(source, "en", "zh-CHS", CancellationToken.None);
        if (!zh0r.Success)
        {
            Console.Error.WriteLine($"Youdao EN->ZH fail: {zh0r.ErrorCode}");
            return 1;
        }

        var zh0 = zh0r.Text;
        var priorDrifts = new List<DriftSpan>();
        Console.WriteLine("================ ENGLISH SOURCE ================\n" + source + "\n");
        string? accepted = null;
        int? acceptedScore = null;

        for (var round = 0; round <= iters; round++)
        {
            Console.WriteLine($"================ round {round} ================");
            // (7) feed prior rounds' drifts back so the polish avoids known FACT drifts but keeps the rough style.
            var feedback = BuildPolishFeedback(priorDrifts);
            var zhRough = await EssayPolishFeedbackAsync(deepseek, zh0, feedback) ?? zh0;
            Console.WriteLine("ZH (essay polish" + (feedback.Length > 0 ? ", fed prior-round drifts" : string.Empty) + "):\n" + zhRough + "\n");

            var (drifts, _) = await gate.EvaluateCrossLingualAsync(source, zhRough, CancellationToken.None);
            // (3) LLM-precise surgical edit, guarded so it can't rewrite non-flagged sentences (else deterministic).
            var (zhFixed, usedLlm) = await LlmSurgicalEditWithGuard(deepseek, zhRough, drifts);
            Console.WriteLine($"ZH (surgical repair — {drifts.Count} drift(s), {(usedLlm ? "LLM-precise + guard" : "deterministic")}):\n" + zhFixed + "\n");
            foreach (var d in drifts.Take(8))
            {
                Console.WriteLine($"    [ZH {d.Kind}] \"{d.CandidateSpan}\" -> \"{d.ExpectedFix}\"");
            }

            var back = await youdao.TranslateAsync(zhFixed, "zh-CHS", "en", CancellationToken.None);
            if (!back.Success)
            {
                Console.WriteLine($"Youdao ZH->EN fail ({back.ErrorCode})\n");
                continue;
            }

            var final = back.Text.Trim();
            Console.WriteLine("EN (back-translated, NOT edited):\n" + final + "\n");

            var (ai, _, _, _, serr) = await ScoreAsync(gptHttp, gptKey!, final, CancellationToken.None);
            var frEn = await gate.EvaluateAsync(source, final, CancellationToken.None);
            var faithful = frEn.Error is null && frEn.Passed;
            var ok = serr is null && ai is not null && ai <= target && faithful;
            Console.WriteLine($"GPTZero = {(ai?.ToString() ?? "err")}%  ·  final-EN: {(faithful ? "FAITHFUL" : $"{frEn.Drifts.Count} drift(s)")}  ->  {(ok ? "ACCEPT" : "reroll")}");
            foreach (var d in frEn.Drifts.Take(8))
            {
                Console.WriteLine($"    [EN {d.Kind}] \"{d.SourceValue}\" -> \"{d.CandidateSpan ?? "(missing)"}\"");
            }

            Console.WriteLine();
            priorDrifts.AddRange(drifts);
            priorDrifts.AddRange(frEn.Drifts);
            if (ok)
            {
                accepted = final;
                acceptedScore = ai;
                break;
            }
        }

        Console.WriteLine(accepted is null
            ? $"VERDICT: no low+faithful candidate within {iters + 1} rounds"
            : $"VERDICT: ACCEPTED at {acceptedScore}% AI + faithful");
        Console.WriteLine($"CALLS: Youdao={youdao.CallCount} · DeepSeek={deepseek.CallCount}");
        return 0;
    }

    // Owner's idea: fix the gate's drifts IN THE CHINESE (against the English source), preserving the rough
    // translationese style, so a fresh back-translation keeps the low-score surface while the facts come right.
    private static async Task<string?> RepairChineseForDrifts(DeepSeekChatClient ds, string sourceEn, string zh, IReadOnlyList<DriftSpan> drifts)
    {
        if (drifts.Count == 0)
        {
            return zh;
        }

        var list = string.Join("\n", drifts.Select(d =>
            $"- 原文「{d.SourceValue}」" + (d.CandidateSpan is null ? "(中文里漏了)" : $",回译成了「{d.CandidateSpan}」") + $",应为「{d.ExpectedFix}」"));
        const string sys =
            "你在修一段【英译中的粗糙中文稿】。下面给你英文原文、当前中文、以及把中文回译成英文后发现的漂移清单。"
            + "请【只在中文里】按英文原文修正这些漂移:数字/币种/日期/人名/单号与原文一致;否定、主体、对象、关系、时态不得变;"
            + "删掉原文没有的杜撰内容、情绪、叙事。其余部分【保持原中文的口语、粗糙、翻译腔,一字不要润色】。"
            + "只返回 JSON:{\"zh\":\"<修好的中文>\"}";
        var user = "英文原文(事实真相):\n" + sourceEn + "\n\n当前中文:\n" + zh + "\n\n回译后发现的漂移:\n" + list;
        var raw = await ds.CompleteAsync(sys, user, 2200, 0.2, CancellationToken.None);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.TryGetProperty("zh", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()?.Trim() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // (7) Build polish feedback from prior rounds' drifts: tell the NEXT polish to avoid these FACT drifts at the
    // source, while explicitly KEEPING the rough/colloquial style (else avoiding drifts = faithful = clean = score up).
    private static string BuildPolishFeedback(IReadOnlyList<DriftSpan> priorDrifts)
    {
        if (priorDrifts.Count == 0)
        {
            return string.Empty;
        }

        var facts = priorDrifts
            .Select(d => d.SourceValue)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();
        if (facts.Count == 0)
        {
            return string.Empty;
        }

        return "【上一轮(们)出现过下面这些事实漂移,这一轮务必从源头照英文原文保住,但继续保持口语、粗糙、不工整的风格】:\n- "
            + string.Join("\n- ", facts)
            + "\n硬要求:人名/产品名/ID/功能套餐术语在中文里保留英文原样(如 Dev、Northstar、onboarding、admin workspace、SSO、email support);数字/金额/日期/时间照原文(如 this week 别写成\"马上\");"
            + "否定与边界——尤其 \"I cannot add X without a new approval cycle\" 这类——必须照原文的【主体】(是\"我不能\",不是\"你不能\")和【否定】保留,别改写成\"我没权限/做不到/你不能改\";"
            + "不要凭空加理由、情绪或方向。其余照旧口语粗糙——不要为了忠实把全文写规整。";
    }

    private static async Task<string?> EssayPolishFeedbackAsync(DeepSeekChatClient ds, string zh0, string feedback)
    {
        var sys = EssayPolish.OwnerPrompt
            + (feedback.Length > 0 ? "\n\n" + feedback : string.Empty)
            + "\n\n注意:把第二遍完成后的终稿全文放进 JSON 返回,不要任何额外文字:{\"final\":\"<终稿全文>\"}";
        var raw = await ds.CompleteAsync(sys, "原文如下：\n" + zh0, 3000, 0.9, CancellationToken.None);
        return ParseJsonStringField(raw, "final");
    }

    // Robustly pull a JSON string field even when the model emitted UNESCAPED newlines inside the value
    // (which breaks strict JsonDocument.Parse). Never returns the raw {"field":...} wrapper (the old bug).
    private static string? ParseJsonStringField(string? raw, string field)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.String)
            {
                return v.GetString()?.Trim();
            }
        }
        catch (JsonException)
        {
            // fall through to lenient extraction
        }

        var m = Regex.Match(raw, "\"" + Regex.Escape(field) + "\"\\s*:\\s*\"(.*)\"\\s*\\}?\\s*$", RegexOptions.Singleline);
        if (!m.Success)
        {
            return null;
        }

        var val = m.Groups[1].Value
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal);
        return val.Trim();
    }

    // (3) LLM applies ONLY the flagged span->fix edits; a deterministic guard verifies it didn't touch any
    // NON-flagged sentence (else it over-edited / cleaned -> fall back to deterministic ApplySurgicalRepair).
    private static async Task<(string Result, bool UsedLlm)> LlmSurgicalEditWithGuard(DeepSeekChatClient ds, string zh, IReadOnlyList<DriftSpan> drifts)
    {
        var (det, _) = ApplySurgicalRepair(zh, drifts);
        if (drifts.Count == 0)
        {
            return (det, false);
        }

        var edits = string.Join("\n", drifts.Select(d => string.IsNullOrEmpty(d.ExpectedFix)
            ? $"- 删除「{d.CandidateSpan}」"
            : $"- 把「{d.CandidateSpan}」替换为「{d.ExpectedFix}」"));
        const string sys =
            "你是精确文本编辑器。下面给你一段中文和一份【改动清单】。请输出这段中文,【只】做清单里列出的替换/删除,"
            + "其余每一个字都原样保留——绝不改写、润色、调整语序、增删任何其它内容。只返回 JSON:{\"text\":\"<只做了指定改动的全文>\"}";
        var raw = await ds.CompleteAsync(sys, "中文:\n" + zh + "\n\n改动清单:\n" + edits, 2400, 0, CancellationToken.None);
        string? llm = null;
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                llm = doc.RootElement.TryGetProperty("text", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()?.Trim() : null;
            }
            catch (JsonException)
            {
                llm = null;
            }
        }

        if (string.IsNullOrWhiteSpace(llm))
        {
            return (det, false);
        }

        // Guard: every NON-flagged sentence of the original must survive verbatim in the LLM output.
        var spans = drifts.Where(d => !string.IsNullOrEmpty(d.CandidateSpan)).Select(d => d.CandidateSpan!).ToList();
        foreach (var sentence in Regex.Split(zh, @"(?<=[。！？\n])").Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var flagged = spans.Any(sp => sentence.Contains(sp, StringComparison.Ordinal));
            if (!flagged && !llm.Contains(sentence, StringComparison.Ordinal))
            {
                return (det, false); // LLM changed a non-flagged sentence -> over-edited -> deterministic fallback
            }
        }

        return (llm, true);
    }

    private static string Oneline(string s, int max)
    {
        var t = s.Replace("\n", " ", StringComparison.Ordinal).Trim();
        return t.Length > max ? t[..(max - 1)] + "…" : t;
    }

    // GPTZero score + per-sentence. Never echoes the response body (it can reflect the api key).
    private static async Task<(int? Ai, string? Cls, double? Burst, IReadOnlyList<(string Sentence, int Ai, bool Hl)> Sentences, string? Err)>
        ScoreAsync(HttpClient http, string key, string text, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.gptzero.me/v2/predict/text");
            req.Headers.Add("x-api-key", key);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = new StringContent(JsonSerializer.Serialize(new { document = text }), Encoding.UTF8, "application/json");
            using var resp = await http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                return (null, null, null, Array.Empty<(string, int, bool)>(), $"http_{(int)resp.StatusCode}");
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("documents", out var docs) || docs.ValueKind != JsonValueKind.Array || docs.GetArrayLength() == 0)
            {
                return (null, null, null, Array.Empty<(string, int, bool)>(), "no_documents");
            }

            var d = docs[0];
            double? ai = null;
            if (d.TryGetProperty("class_probabilities", out var cp) && cp.ValueKind == JsonValueKind.Object && cp.TryGetProperty("ai", out var aiEl) && aiEl.ValueKind == JsonValueKind.Number)
            {
                ai = aiEl.GetDouble();
            }
            else if (d.TryGetProperty("completely_generated_prob", out var cg) && cg.ValueKind == JsonValueKind.Number)
            {
                ai = cg.GetDouble();
            }

            var cls = d.TryGetProperty("document_classification", out var dc) && dc.ValueKind == JsonValueKind.String ? dc.GetString() : null;
            if (ai is null && cls is not null)
            {
                ai = cls == "AI_ONLY" ? 0.99 : cls == "HUMAN_ONLY" ? 0.01 : 0.5;
            }

            double? burst = d.TryGetProperty("overall_burstiness", out var bv) && bv.ValueKind == JsonValueKind.Number ? bv.GetDouble() : null;

            var sents = new List<(string, int, bool)>();
            if (d.TryGetProperty("sentences", out var ss) && ss.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in ss.EnumerateArray())
                {
                    var sen = s.TryGetProperty("sentence", out var se) && se.ValueKind == JsonValueKind.String ? se.GetString() : null;
                    if (sen is null)
                    {
                        continue;
                    }

                    var gp = s.TryGetProperty("generated_prob", out var ge) && ge.ValueKind == JsonValueKind.Number ? ge.GetDouble() : (double?)null;
                    var hl = s.TryGetProperty("highlight_sentence_for_ai", out var he) && (he.ValueKind == JsonValueKind.True || he.ValueKind == JsonValueKind.False) && he.GetBoolean();
                    sents.Add((sen, gp is null ? -1 : (int)Math.Round(gp.Value * 100), hl));
                }
            }

            return (ai is null ? null : (int)Math.Round(ai.Value * 100), cls, burst, sents, null);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or OperationCanceledException)
        {
            return (null, null, null, Array.Empty<(string, int, bool)>(), e.GetType().Name);
        }
    }

    private static string? ExtractField(string? json, string field)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()?.Trim() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
