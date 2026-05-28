using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Quality;
using ReplyInMyVoice.Domain.RewriteEngine;
using ReplyInMyVoice.Infrastructure.Providers;

// GPTZero feedback-loop pilot (GPTZERO_LOOP_PILOT=1). EVAL-ONLY. Owner's refined design: NO Sapling —
// start from the raw draft, then LOOP. Each iteration: DeepSeek naturalizes the text (history + the prior
// version's drift feedback + GPTZero per-sentence offenders; strict: keep every fact + meaning, no
// placeholders); then the discrete facts (amounts/dates/numbers/IDs/names) are masked and a Youdao
// EN->zh->EN round-trip translates the PROSE while the facts ride through as sentinels (so translation
// can't drift them); a broken sentinel skips translation that round. Score on real GPTZero + verify facts
// with the FidelityJudge each round; the judge's drift findings are fed into the NEXT loop so it repairs
// the specific problems. Stop at GPTZero <= target with facts intact, or the iter/budget cap.
//
// The open question this tests: does protecting the facts let translation lower GPTZero WITHOUT breaking
// the email — or does protecting enough to stay coherent keep it at ~100% (the R10 partial-perturbation
// wall), while only over-translation (which mangles the connective prose) drops the score?
//
// Knobs: GPTZERO_LOOP_FROM_DRAFT (default 1 = no Sapling/T0), GPTZERO_LOOP_ITERS (3 => 4 GPTZero scores),
// GPTZERO_LOOP_TARGET (30), GPTZERO_MAX_CALLS (5), GPTZERO_LOOP_YOUDAO (1), GPTZERO_LOOP_CASES (1).
internal static class GptzeroLoopRunner
{
    public static async Task<int> RunAsync(
        FactReconstructRewriteProvider provider,
        CountingRewriteModelClient modelCounter,
        CountingWritingSignalClient signalCounter,
        IReadOnlyList<EvalCase> cases,
        EvalConfig config,
        string apiKey,
        DateTimeOffset startedAt)
    {
        var gptzeroKey = Environment.GetEnvironmentVariable("GPTZero_API_KEY") ?? Environment.GetEnvironmentVariable("GPTZERO_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(gptzeroKey))
        {
            Console.Error.WriteLine("GPTZERO_LOOP_PILOT: need model api key + GPTZero_API_KEY.");
            return 2;
        }

        var maxIters = IntEnv("GPTZERO_LOOP_ITERS", 3);   // 3 loops => iter 0..3 => 4 GPTZero scores
        var target = IntEnv("GPTZERO_LOOP_TARGET", 30);
        var maxGpt = IntEnv("GPTZERO_MAX_CALLS", 5);
        var useYoudao = (Environment.GetEnvironmentVariable("GPTZERO_LOOP_YOUDAO") ?? "1").Trim() is "1" or "true";
        var caseLimit = IntEnv("GPTZERO_LOOP_CASES", 1);
        // Owner's refinement: drop Sapling/T0, start from the raw draft, and protect facts (DON'T translate
        // them). Default on.
        var fromDraft = (Environment.GetEnvironmentVariable("GPTZERO_LOOP_FROM_DRAFT") ?? "1").Trim() is "1" or "true";

        var youdaoKey = FirstEnv("YOUDAO_APP_KEY", "AppID", "YouDao_API_KEY");
        var youdaoSecret = FirstEnv("YOUDAO_APP_SECRET", "AppSecret");
        var youdaoUrl = Environment.GetEnvironmentVariable("YOUDAO_API_URL") ?? "https://openapi.youdao.com/api";
        useYoudao = useYoudao && !string.IsNullOrWhiteSpace(youdaoKey) && !string.IsNullOrWhiteSpace(youdaoSecret);

        using var http = new HttpClient();
        using var youdaoHttp = new HttpClient();
        using var judgeHttp = new HttpClient();
        using var gptHttp = new HttpClient();
        var deepseek = new DeepSeekChatClient(http, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(60));
        var judge = new SemanticEvalJudge(judgeHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));
        var proposer = new ProtectedTermProposer(deepseek); // builds the ProtectedTermLedger for the deterministic gate
        var youdao = useYoudao ? new YoudaoTranslationClient(youdaoHttp, youdaoKey!, youdaoSecret!, youdaoUrl, TimeSpan.FromSeconds(30)) : null;

        var gptCalls = 0;
        var report = new StringBuilder();
        report.AppendLine("# GPTZero feedback-loop pilot (owner's design: loop + history + Youdao + GPTZero feedback + fact-verify)");
        report.AppendLine();
        report.AppendLine($"Started {startedAt:O} · iters={maxIters} target={target} youdao={(useYoudao ? "on" : "off")} model={config.Model}");
        report.AppendLine();

        foreach (var sample in cases.Take(Math.Max(1, caseLimit)))
        {
            var request = sample.ToRewriteRequest();
            var ledger = FactLedgerExtractor.Extract(request);
            // Trustworthy acceptance: the deterministic gate chain (ProtectedTerm incl. acronyms / Boundary /
            // Sendability) runs alongside the LLM FidelityJudge, so the loop can't "win" on broken text.
            // Load-bearing phrases ("expires June 7", "reply by June 7") are extracted from the draft and
            // protected end-to-end: masked as units during translation AND verbatim-required by the gate, so
            // the verb+date relationship survives Youdao (was the main residual drift class).
            var loadBearing = LoadBearingPhraseExtractor.Extract(sample.InputDraft, ledger);
            var qctx = QualityContext.Build(
                sample.InputDraft, ledger,
                protectedSpans: await proposer.ProposeAsync(sample.InputDraft, CancellationToken.None),
                loadBearingSpans: loadBearing);
            string current;
            if (fromDraft)
            {
                current = sample.InputDraft; // no Sapling, no T0 — start from the raw draft
            }
            else
            {
                var t0 = await provider.RewriteAsync(Guid.NewGuid(), request, CancellationToken.None);
                current = RewritePayload.TryParse(t0.ResultJson)?.RewrittenText ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(current))
            {
                Console.WriteLine($"{sample.Id}: no starting text; skipped.");
                continue;
            }

            Console.WriteLine($"\n=== {sample.Id} ({sample.Category}) — fact-protected loop from {(fromDraft ? "draft" : "T0")} ===");
            report.AppendLine($"## {sample.Id} ({sample.Category})");
            report.AppendLine();
            report.AppendLine("| iter | source | GPTZero | facts | note |");
            report.AppendLine("| ---: | --- | ---: | :-: | --- |");

            var history = new List<(int Ai, string Text)>();
            var bestFaithfulAi = (int?)null;

            for (var iter = 0; iter <= maxIters; iter++)
            {
                if (gptCalls >= maxGpt)
                {
                    Console.WriteLine("  GPTZero budget cap reached; stopping.");
                    break;
                }

                var (ai, _, sentences, err) = await ScoreWithSentencesAsync(gptHttp, gptzeroKey!, current, CancellationToken.None);
                gptCalls++;
                if (err is not null)
                {
                    Console.WriteLine($"  iter {iter}: GPTZero error {err}; stopping.");
                    report.AppendLine($"| {iter} | {(iter == 0 ? "T0" : "loop")} | error:{err} | - | - |");
                    break;
                }

                var sem = await judge.VerifyAsync(current, sample.MustKeep, sample.MustNotClaim, CancellationToken.None);
                var det = QualityGateChain.Evaluate(current, qctx); // deterministic gates (no LLM, no cost)
                var semPass = sem.Error is null && sem.FactsReallyPass && sem.RealForbidden == 0 && !sem.MeaningChanged;
                var factsPass = semPass && det.Passed; // BOTH must pass — this is the trustworthy judge
                var driftFeedback = CombineFeedback(BuildDriftFeedback(sem), det); // LLM + deterministic, fed forward
                if (factsPass)
                {
                    bestFaithfulAi = bestFaithfulAi is null ? ai!.Value : Math.Min(bestFaithfulAi.Value, ai!.Value);
                }

                history.Add((ai!.Value, current));
                var detNote = det.Passed ? string.Empty : "det: " + string.Join("; ", det.Reasons.Take(2));
                Console.WriteLine($"  iter {iter}: GPTZero={ai}% facts={(factsPass ? "ok" : "DRIFT")} (sem={(semPass ? "ok" : "x")} det={(det.Passed ? "ok" : "x")}) ({(iter == 0 ? "draft" : "loop")})"
                    + (detNote.Length > 0 ? "\n      " + detNote : string.Empty));
                report.AppendLine($"| {iter} | {(iter == 0 ? "draft" : "loop")} | {ai}% | sem={(semPass ? "ok" : "x")} det={(det.Passed ? "ok" : "x")} | {(ai <= target ? (factsPass ? "**hit target + faithful**" : "below target but FACTS BROKEN: " + detNote) : detNote)} |");

                if (ai <= target && factsPass)
                {
                    Console.WriteLine("  -> reached target with facts intact. SUCCESS.");
                    break;
                }

                if (iter == maxIters || gptCalls >= maxGpt)
                {
                    break;
                }

                // DeepSeek naturalizes the current text (with history + the prior version's drift feedback),
                // keeping every fact verbatim and the meaning intact (no placeholders / sign-offs / new info).
                var ds = await deepseek.CompleteAsync(
                    LoopSystemPrompt,
                    BuildLoopUserPrompt(sample.InputDraft, current, history, sentences, sample.MustKeep, target, driftFeedback),
                    1400, 0.8, CancellationToken.None);
                var candidate = ExtractField(ds, "rewrittenText") ?? current;

                // Then protect ONLY the discrete facts (amounts/dates/numbers/IDs/names) as sentinels and run a
                // Youdao round-trip, so the TRANSLATION cannot drift them — prose is translated, facts ride
                // through. A broken sentinel -> skip translation this round (keep the DeepSeek text), so the
                // loop never no-ops on over-masking.
                if (youdao is not null)
                {
                    var masked = AnchorMasker.Mask(candidate, ledger, sample.MustKeep, sample.MustNotClaim, loadBearing, bracketFree: true);
                    var toZh = await youdao.TranslateAsync(masked.MaskedText, "en", "zh-CHS", CancellationToken.None);
                    if (toZh.Success)
                    {
                        var backEn = await youdao.TranslateAsync(toZh.Text, "zh-CHS", "en", CancellationToken.None);
                        if (backEn.Success)
                        {
                            var unmask = AnchorMasker.Unmask(backEn.Text, masked.Map);
                            if (unmask.IntegrityOk)
                            {
                                candidate = unmask.Restored;
                            }
                            else
                            {
                                Console.WriteLine("    [protect] translation broke a fact sentinel — kept the pre-translation text this round.");
                            }
                        }
                    }
                }

                current = candidate.Trim();
            }

            var verdict = bestFaithfulAi is null
                ? "no faithful candidate scored"
                : bestFaithfulAi <= target
                    ? $"**LOOP WORKED**: reached {bestFaithfulAi}% AI while faithful"
                    : $"best FAITHFUL score across the loop = **{bestFaithfulAi}% AI** (never < {target} while keeping facts)";
            Console.WriteLine($"  VERDICT: {verdict}");
            report.AppendLine();
            report.AppendLine($"**Verdict ({sample.Id}):** {verdict}");
            report.AppendLine();
            report.AppendLine("**Final text:**");
            report.AppendLine();
            report.AppendLine("> " + current.Replace("\n", "\n> ", StringComparison.Ordinal));
            report.AppendLine();
        }

        report.AppendLine($"\nGPTZero calls used: {gptCalls}/{maxGpt} · DeepSeek: {deepseek.CallCount} · Youdao: {youdao?.CallCount ?? 0}");
        Directory.CreateDirectory(config.OutputDirectory);
        var path = Path.Combine(config.OutputDirectory, $"{startedAt:yyyyMMdd-HHmmss}-gptzero-loop-pilot.md");
        await File.WriteAllTextAsync(path, report.ToString());
        Console.WriteLine($"\nWrote {path} (GPTZero calls: {gptCalls})");
        return 0;
    }

    private const string LoopSystemPrompt =
        "You rewrite an email so it reads as if a real person wrote it, to LOWER an AI-text classifier's score. "
        + "ABSOLUTE rules: (1) keep EVERY fact verbatim — names, numbers, dates, amounts, IDs/references; (2) keep "
        + "the exact MEANING and relationships — e.g. if the quote 'expires' on a date it still EXPIRES (never "
        + "soften/flip it); keep commitments and uncertainty (cannot stays cannot, may stays may); (3) do NOT add "
        + "greetings, sign-offs, names, placeholders like '(Your name)', or any new information; (4) it must stay a "
        + "sendable, professional reply. Vary sentence rhythm for a natural human feel. You are given the ORIGINAL "
        + "(truth), the CURRENT version, the PROBLEMS to fix, and prior attempts with their scores. Return JSON: "
        + "{\"rewrittenText\":\"...\"}.";

    private static string BuildLoopUserPrompt(
        string draft, string current, IReadOnlyList<(int Ai, string Text)> history,
        IReadOnlyList<(string Sentence, int Ai)> sentences, IReadOnlyList<string> mustKeep, int target, string? prevProblems)
    {
        var topSentences = sentences.OrderByDescending(s => s.Ai).Take(4)
            .Select(s => $"- ({s.Ai}% AI) \"{s.Sentence.Trim()}\"").ToList();
        var hist = history.Select((h, i) => $"attempt {i}: scored {h.Ai}% AI").ToList();
        return "ORIGINAL (source of truth — preserve every fact, name, number, date):\n" + draft
            + "\n\nMUST KEEP:\n- " + string.Join("\n- ", mustKeep)
            + "\n\nCURRENT VERSION (rewrite this; keep every fact and the meaning):\n" + current
            + (prevProblems is not null
                ? "\n\nPROBLEMS IN THE CURRENT VERSION TO FIX NOW (repair the MEANING; the source above is the truth):\n" + prevProblems
                : string.Empty)
            + (topSentences.Count > 0 ? "\n\nMOST AI-LIKE SENTENCES to make more human:\n" + string.Join("\n", topSentences) : string.Empty)
            + "\n\nPRIOR ATTEMPTS:\n" + string.Join("\n", hist)
            + $"\n\nProduce a new version a classifier would rate below {target}% AI, keeping ALL facts exactly.";
    }

    // Merges the LLM judge's drift findings with the deterministic gate's failures (e.g. "SSO dropped",
    // "boundary flipped", "sentinel residue"), so the next loop sees BOTH and can repair them.
    private static string? CombineFeedback(string? semFeedback, QualityGateReport det)
    {
        var parts = new List<string>();
        if (semFeedback is not null)
        {
            parts.Add(semFeedback);
        }

        if (!det.Passed)
        {
            parts.AddRange(det.Reasons.Select(r => "- " + r));
        }

        return parts.Count > 0 ? string.Join("\n", parts) : null;
    }

    // What the FidelityJudge still flags on the current candidate — fed into the next loop so it can
    // repair those specific problems (the owner's "carry the errors into the next loop" point).
    private static string? BuildDriftFeedback(SemVerdict sem)
    {
        if (sem.Error is not null)
        {
            return null;
        }

        var problems = new List<string>();
        problems.AddRange(sem.Facts
            .Where(f => f.Status is "missing" or "contradicted")
            .Select(f => $"{f.Status}: {f.Fact}" + (string.IsNullOrWhiteSpace(f.Reason) ? string.Empty : $" — {f.Reason}")));
        problems.AddRange(sem.Forbidden.Where(f => f.Violated).Select(f => $"forbidden claim made: {f.Rule}"));
        if (sem.MeaningChanged)
        {
            problems.Add("the overall meaning drifted from the source");
        }

        return problems.Count > 0 ? string.Join("\n", problems.Select(p => "- " + p)) : null;
    }

    // GPTZero score with per-sentence breakdown. Never echoes the response body (it can reflect the key).
    private static async Task<(int? Ai, string? Cls, IReadOnlyList<(string Sentence, int Ai)> Sentences, string? Err)>
        ScoreWithSentencesAsync(HttpClient http, string key, string text, CancellationToken ct)
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
                return (null, null, Array.Empty<(string, int)>(), $"http_{(int)resp.StatusCode}");
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("documents", out var docs) || docs.ValueKind != JsonValueKind.Array || docs.GetArrayLength() == 0)
            {
                return (null, null, Array.Empty<(string, int)>(), "no_documents");
            }

            var d = docs[0];
            double? aiProb = null;
            if (d.TryGetProperty("class_probabilities", out var cp) && cp.TryGetProperty("ai", out var aiEl) && aiEl.ValueKind == JsonValueKind.Number)
            {
                aiProb = aiEl.GetDouble();
            }
            else if (d.TryGetProperty("completely_generated_prob", out var cg) && cg.ValueKind == JsonValueKind.Number)
            {
                aiProb = cg.GetDouble();
            }

            var cls = d.TryGetProperty("document_classification", out var dc) && dc.ValueKind == JsonValueKind.String ? dc.GetString() : null;
            if (aiProb is null && cls is not null)
            {
                aiProb = cls == "AI_ONLY" ? 0.99 : cls == "HUMAN_ONLY" ? 0.01 : 0.5;
            }

            var sentences = new List<(string, int)>();
            if (d.TryGetProperty("sentences", out var sents) && sents.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in sents.EnumerateArray())
                {
                    var sentence = s.TryGetProperty("sentence", out var se) && se.ValueKind == JsonValueKind.String ? se.GetString() : null;
                    double? p = s.TryGetProperty("generated_prob", out var ge) && ge.ValueKind == JsonValueKind.Number ? ge.GetDouble() : null;
                    if (sentence is not null && p is not null)
                    {
                        sentences.Add((sentence, (int)Math.Round(p.Value * 100)));
                    }
                }
            }

            return (aiProb is null ? null : (int)Math.Round(aiProb.Value * 100), cls, sentences, null);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or OperationCanceledException)
        {
            return (null, null, Array.Empty<(string, int)>(), e.GetType().Name);
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

    private static string? FirstEnv(params string[] names) =>
        names.Select(Environment.GetEnvironmentVariable).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static int IntEnv(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var v) && v > 0 ? v : fallback;
}
