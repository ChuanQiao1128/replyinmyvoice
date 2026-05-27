using System.Text;
using System.Text.Json;

// U2 Chinese-fact-note pilot (U2_FACT_NOTE_PILOT=1). EVAL-ONLY. Tests the doc's U2 idea: instead of
// polishing an MT'd email (the friend's method, already 100% AI on GPTZero), DeepSeek EXTRACTS the
// load-bearing facts and writes a deliberately terse, un-polished CHINESE fact-note (no pleasantries,
// no business polish, no new commitments); Youdao then translates that note ZH->EN, so the final
// English author is Youdao (an MT surface) but the Chinese source is intentionally un-AI-like.
// Question: does a terse-Chinese-note -> MT surface score below the AI draft on GPTZero?
//
// Same 2 simple cases as the U3 pilot (SimplePilotCases.All), so U2/U3/draft compare on identical
// inputs. Semantic judge gates each output; GPTZero only on survivors. Hard budget GPTZERO_MAX_CALLS.
internal static class U2FactNotePilot
{
    public static async Task<int> RunAsync(EvalConfig config, string apiKey, DateTimeOffset startedAt)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine("U2_FACT_NOTE_PILOT: missing model api key (DEEPSEEK_API_KEY/OPENAI_API_KEY).");
            return 2;
        }

        var gptzeroKey = Environment.GetEnvironmentVariable("GPTZero_API_KEY")
            ?? Environment.GetEnvironmentVariable("GPTZERO_API_KEY");
        if (string.IsNullOrWhiteSpace(gptzeroKey))
        {
            Console.Error.WriteLine("U2_FACT_NOTE_PILOT: missing GPTZero_API_KEY.");
            return 2;
        }

        var youdaoKey = FirstEnv("YOUDAO_APP_KEY", "AppID", "YouDao_API_KEY");
        var youdaoSecret = FirstEnv("YOUDAO_APP_SECRET", "AppSecret");
        var youdaoUrl = Environment.GetEnvironmentVariable("YOUDAO_API_URL") ?? "https://openapi.youdao.com/api";
        if (string.IsNullOrWhiteSpace(youdaoKey) || string.IsNullOrWhiteSpace(youdaoSecret))
        {
            Console.Error.WriteLine("U2_FACT_NOTE_PILOT: missing Youdao credentials (YOUDAO_APP_KEY/AppID + YOUDAO_APP_SECRET/AppSecret).");
            return 2;
        }

        var maxGpt = IntEnv("GPTZERO_MAX_CALLS", 6);
        var gptCalls = 0;
        // U2_FACT_REPAIR=1 adds the user's proposed stage: after the MT English, an LLM fixes ONLY the
        // spans that diverge from the extracted facts/roles (not a full rewrite), then we re-score the
        // repaired (now-faithful) text to test whether the low GPTZero reading survives fact-fixing.
        var doRepair = (Environment.GetEnvironmentVariable("U2_FACT_REPAIR") ?? string.Empty)
            .Trim().ToLowerInvariant() is "1" or "true" or "yes";

        using var http = new HttpClient();
        using var youdaoHttp = new HttpClient();
        using var judgeHttp = new HttpClient();
        using var gptHttp = new HttpClient();
        var deepseek = new DeepSeekChatClient(http, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(60));
        var youdao = new YoudaoTranslationClient(youdaoHttp, youdaoKey!, youdaoSecret!, youdaoUrl, TimeSpan.FromSeconds(30));
        var judge = new SemanticEvalJudge(judgeHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));

        Console.WriteLine($"U2 fact-note pilot: cases={SimplePilotCases.All.Count} gptzeroMax={maxGpt} model={config.Model}");
        var report = new StringBuilder();
        report.AppendLine("# U2 Chinese-fact-note pilot (terse Chinese note -> Youdao EN)");
        report.AppendLine();
        report.AppendLine("**Eval-only.** DeepSeek extracts facts and writes a terse, un-polished Chinese fact-note; "
            + "Youdao translates it ZH→EN (Youdao is the final English author). GPTZero only on semantic-gate survivors. "
            + "Lower GPTZero than the draft = a real detection drop. Same inputs as the U3 pilot.");
        report.AppendLine();
        report.AppendLine($"Started: {startedAt:O}  ·  model: {config.Model}  ·  GPTZero budget: {maxGpt}");
        report.AppendLine();

        foreach (var c in SimplePilotCases.All)
        {
            Console.WriteLine($"\n=== {c.Id} ({c.EmailType}) ===");
            report.AppendLine($"## {c.Id} ({c.EmailType})");
            report.AppendLine();

            // 1. Draft baseline on GPTZero.
            int? draftScore = null;
            string? draftCls = null;
            if (gptCalls < maxGpt)
            {
                var (ai, cls, err) = await GptzeroScorer.ScoreAsync(gptHttp, gptzeroKey!, c.Draft, CancellationToken.None);
                gptCalls++;
                draftScore = ai;
                draftCls = cls;
                Console.WriteLine($"  draft GPTZero: {(err is null ? $"{ai}% AI [{cls}]" : "error: " + err)}");
            }

            // 2. DeepSeek extracts load-bearing facts (verbatim identifiers/amounts/dates/names).
            var factsJson = await deepseek.CompleteAsync(ExtractSystem, c.Draft, 700, 0.0, CancellationToken.None);
            if (string.IsNullOrWhiteSpace(factsJson))
            {
                Console.WriteLine("  U2: SKIP (fact extraction failed).");
                report.AppendLine($"- draft GPTZero: **{Fmt(draftScore)}** {Cls(draftCls)}");
                report.AppendLine("- U2: **skipped** (fact extraction failed)");
                report.AppendLine();
                continue;
            }

            // 3. DeepSeek writes a terse Chinese fact-note FROM the facts (not from the flowery draft),
            //    so the AI-draft register is not carried through. Returns {"note_zh":"..."}.
            var noteJson = await deepseek.CompleteAsync(ChineseNoteSystem, factsJson, 700, 0.2, CancellationToken.None);
            var noteZh = ExtractField(noteJson, "note_zh");
            if (string.IsNullOrWhiteSpace(noteZh))
            {
                Console.WriteLine("  U2: SKIP (Chinese note generation failed).");
                report.AppendLine($"- draft GPTZero: **{Fmt(draftScore)}** {Cls(draftCls)}");
                report.AppendLine("- U2: **skipped** (Chinese note generation failed)");
                report.AppendLine();
                continue;
            }

            Console.WriteLine($"  中文事实稿: {noteZh.Replace("\n", " ", StringComparison.Ordinal)}");

            // 4. Youdao ZH->EN — Youdao is the final English author (MT surface).
            var toEn = await youdao.TranslateAsync(noteZh, "zh-CHS", "en", CancellationToken.None);
            if (!toEn.Success || string.IsNullOrWhiteSpace(toEn.Text))
            {
                Console.WriteLine($"  U2: SKIP (Youdao zh->en failed: {toEn.ErrorCode}).");
                report.AppendLine($"- draft GPTZero: **{Fmt(draftScore)}** {Cls(draftCls)}");
                report.AppendLine($"- U2: **skipped** (Youdao zh→en failed: {toEn.ErrorCode})");
                report.AppendLine();
                continue;
            }

            var english = toEn.Text.Trim();
            Console.WriteLine($"  U2 English ({english.Length} chars):\n    {english.Replace("\n", "\n    ", StringComparison.Ordinal)}");

            // 5. Gate the RAW MT English (facts + forbidden + meaning), then score it on GPTZero. We
            //    score the raw output regardless of the gate here — the experiment compares the raw MT
            //    surface against the fact-repaired version, so we need both numbers.
            var rawGate = await judge.VerifyAsync(english, c.MustKeep, c.MustNotClaim, CancellationToken.None);
            var rawPass = rawGate.Error is null && rawGate.FactsReallyPass && rawGate.RealForbidden == 0 && !rawGate.MeaningChanged;
            Console.WriteLine($"  raw MT: semantic {(rawPass ? "PASS" : "FAIL")}{DriftStr(rawGate)}");
            int? rawScore = null;
            string? rawCls = null;
            if (gptCalls < maxGpt)
            {
                (rawScore, rawCls, _) = await GptzeroScorer.ScoreAsync(gptHttp, gptzeroKey!, english, CancellationToken.None);
                gptCalls++;
                Console.WriteLine($"  raw MT GPTZero: {Fmt(rawScore)} {Cls(rawCls)}");
            }

            // 6. Minimal fact-repair (the user's proposal): an LLM fixes ONLY the spans that diverge from
            //    the required facts/roles, keeping the plain MT style elsewhere — NOT a full rewrite. Then
            //    re-gate and re-score, to see whether the low reading survives becoming faithful + sendable.
            string? repaired = null;
            int? repScore = null;
            string? repCls = null;
            var repPass = false;
            SemVerdict? repGate = null;
            IReadOnlyList<string> edits = Array.Empty<string>();
            if (doRepair)
            {
                (repaired, edits) = await RepairAsync(deepseek, english, c.Draft, c.MustKeep, c.MustNotClaim, CancellationToken.None);
                if (!string.IsNullOrWhiteSpace(repaired))
                {
                    Console.WriteLine($"  repaired ({repaired!.Length} chars, edits={edits.Count}):\n    {repaired.Replace("\n", "\n    ", StringComparison.Ordinal)}");
                    repGate = await judge.VerifyAsync(repaired, c.MustKeep, c.MustNotClaim, CancellationToken.None);
                    repPass = repGate.Error is null && repGate.FactsReallyPass && repGate.RealForbidden == 0 && !repGate.MeaningChanged;
                    Console.WriteLine($"  repaired: semantic {(repPass ? "PASS" : "FAIL")}{DriftStr(repGate)}");
                    if (gptCalls < maxGpt)
                    {
                        (repScore, repCls, _) = await GptzeroScorer.ScoreAsync(gptHttp, gptzeroKey!, repaired, CancellationToken.None);
                        gptCalls++;
                        Console.WriteLine($"  repaired GPTZero: {Fmt(repScore)} {Cls(repCls)}");
                    }
                }
                else
                {
                    Console.WriteLine("  repair: failed to produce output.");
                }
            }

            report.AppendLine($"- draft GPTZero: **{Fmt(draftScore)}** {Cls(draftCls)}");
            report.AppendLine($"- raw MT: semantic **{(rawPass ? "PASS" : "FAIL")}**{DriftMd(rawGate)} · GPTZero **{Fmt(rawScore)}** {Cls(rawCls)}");
            if (doRepair && repGate is not null)
            {
                report.AppendLine($"- fact-repaired: semantic **{(repPass ? "PASS" : "FAIL")}**{DriftMd(repGate)} · GPTZero **{Fmt(repScore)}** {Cls(repCls)}"
                    + (draftScore.HasValue && repScore.HasValue ? $"  ·  Δ vs draft: **{Signed(repScore - draftScore)}**" : string.Empty));
            }

            report.AppendLine();
            report.AppendLine($"**中文事实稿:** {noteZh.Replace("\n", " ", StringComparison.Ordinal)}");
            report.AppendLine();
            report.AppendLine("**U2 raw English (Youdao zh→en):**");
            report.AppendLine();
            report.AppendLine("> " + english.Replace("\n", "\n> ", StringComparison.Ordinal));
            if (doRepair && !string.IsNullOrWhiteSpace(repaired))
            {
                report.AppendLine();
                report.AppendLine($"**Fact-repaired** (edits: {(edits.Count > 0 ? string.Join("; ", edits) : "none reported")}):");
                report.AppendLine();
                report.AppendLine("> " + repaired!.Replace("\n", "\n> ", StringComparison.Ordinal));
            }

            report.AppendLine();
        }

        var verdictLine = $"Budget used: {gptCalls}/{maxGpt} GPTZero calls; Youdao calls: {youdao.CallCount}.";
        Console.WriteLine($"\n=== {verdictLine} ===");
        report.AppendLine("## Budget");
        report.AppendLine();
        report.AppendLine(verdictLine);

        Directory.CreateDirectory(config.OutputDirectory);
        var stamp = startedAt.ToString("yyyyMMdd-HHmmss");
        var mdPath = Path.Combine(config.OutputDirectory, $"{stamp}-u2-fact-note-pilot.md");
        await File.WriteAllTextAsync(mdPath, report.ToString());
        Console.WriteLine($"Wrote {mdPath}");
        return 0;
    }

    private const string ExtractSystem =
        "You extract the load-bearing facts from an email draft so a terse reply can be rebuilt without "
        + "losing anything. Return JSON only: {\"email_type\":\"\",\"identifiers\":[],\"amounts\":[],"
        + "\"dates\":[],\"names\":[],\"intent\":\"\",\"constraints\":[],\"modality\":[]}. Copy identifiers, "
        + "amounts (with currency symbol), dates, and names VERBATIM — do not translate, normalize, or invent. "
        + "intent = what the sender is trying to do, one short phrase. constraints = hard limits/conditions. "
        + "modality = commitment/certainty words present (can, cannot, will, may, not yet, etc.). Empty array if none.";

    private const string ChineseNoteSystem =
        "你要根据给定的事实 JSON，写一版**简短、直接、低润色**的中文事实稿，供之后机器翻译回英文。"
        + "硬规则：(1) 不要扩写，不要加寒暄/感谢/礼貌话，不要任何商务润色或情绪铺垫。"
        + "(2) 不得新增任何事实、承诺、道歉、折扣、期限或政策。"
        + "(3) 所有英文姓名、订单号、发票号(如 INV-204)、SKU、金额(如 $1,250.00)、日期(如 June 10)必须**原样保留英文/原格式**，不要翻译成中文、不要改写。"
        + "(4) 必须保留承诺与确定性强度：can→能/可以，cannot→不能，will→会，may→可能，not yet→还没。不要加强也不要削弱。"
        + "(5) 只写必须的事实和下一步动作，能多短就多短。"
        + "只返回 JSON：{\"note_zh\":\"<中文事实稿>\"}";

    private const string RepairSystem =
        "You are a MINIMAL fact corrector for a draft email reply. You are given a ground-truth source, "
        + "the required facts, and an EMAIL TO CORRECT. Fix ONLY what misstates, omits, inverts, or "
        + "weakens/strengthens a required fact — most importantly WHO does WHAT (sender vs recipient roles), "
        + "any missing request, and any changed number/date/name/identifier/modality. Rules: (1) Change as "
        + "little as possible — keep the existing wording, sentence order, length, and plain unembellished "
        + "style everywhere that is already fact-correct; do NOT rewrite or polish fact-correct sentences. "
        + "(2) Do NOT add greetings, pleasantries, sign-offs, or any content beyond restoring a required fact. "
        + "(3) The result must be a correct, sendable FIRST-PERSON reply from the sender to the recipient. "
        + "Return JSON only: {\"repaired\":\"<corrected email>\",\"edits\":[\"<short note per edit>\"]}.";

    private static async Task<(string? Text, IReadOnlyList<string> Edits)> RepairAsync(
        DeepSeekChatClient deepseek,
        string english,
        string sourceDraft,
        IReadOnlyList<string> mustKeep,
        IReadOnlyList<string> mustNotClaim,
        CancellationToken cancellationToken)
    {
        var user =
            "GROUND-TRUTH SOURCE (original draft — use ONLY to check facts/roles, never as a style target):\n"
            + sourceDraft
            + "\n\nREQUIRED FACTS (must be conveyed correctly):\n- " + string.Join("\n- ", mustKeep)
            + "\n\nMUST NOT CLAIM:\n- " + string.Join("\n- ", mustNotClaim)
            + "\n\nEMAIL TO CORRECT (keep its plain style; fix only fact/role errors):\n" + english;

        var json = await deepseek.CompleteAsync(RepairSystem, user, 900, 0.1, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return (null, Array.Empty<string>());
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var text = root.TryGetProperty("repaired", out var r) && r.ValueKind == JsonValueKind.String
                ? r.GetString()?.Trim()
                : null;
            var edits = root.TryGetProperty("edits", out var e) && e.ValueKind == JsonValueKind.Array
                ? e.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
                : new List<string>();
            return (text, edits);
        }
        catch (JsonException)
        {
            return (null, Array.Empty<string>());
        }
    }

    // Compact drift description for console (leading space) and markdown (leading " —").
    private static string DriftStr(SemVerdict v)
    {
        var parts = new List<string>();
        if (v.Error is not null)
        {
            parts.Add($"judge_error:{v.Error}");
        }

        var drifts = v.Facts.Where(f => f.Status is "missing" or "contradicted").Select(f => $"{f.Status}:{f.Fact}").ToList();
        if (drifts.Count > 0)
        {
            parts.Add("drifts=[" + string.Join("; ", drifts) + "]");
        }

        var forbidden = v.Forbidden.Where(f => f.Violated).Select(f => f.Rule).ToList();
        if (forbidden.Count > 0)
        {
            parts.Add("forbidden=[" + string.Join("; ", forbidden) + "]");
        }

        if (v.MeaningChanged)
        {
            parts.Add("meaning_changed");
        }

        return parts.Count > 0 ? " " + string.Join(" ", parts) : string.Empty;
    }

    private static string DriftMd(SemVerdict v)
    {
        var s = DriftStr(v);
        return string.IsNullOrEmpty(s) ? string.Empty : " —" + s;
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
            return doc.RootElement.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()?.Trim()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? FirstEnv(params string[] names) =>
        names.Select(Environment.GetEnvironmentVariable).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static int IntEnv(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var v) && v > 0 ? v : fallback;

    private static string Fmt(int? v) => v is null ? "-" : $"{v}% AI";

    private static string Cls(string? cls) => string.IsNullOrEmpty(cls) ? string.Empty : $"[{cls}]";

    private static string Signed(int? v) => v is null ? "-" : v.Value > 0 ? $"+{v.Value}" : v.Value.ToString();
}
