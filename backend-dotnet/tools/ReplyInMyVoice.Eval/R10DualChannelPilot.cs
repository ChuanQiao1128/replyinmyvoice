using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.RewriteEngine;
using ReplyInMyVoice.Infrastructure.Providers;

// Round-10 dual-channel rewrite pilot (DUAL_CHANNEL_TRANSLATION_PILOT=1). EVAL-ONLY.
// See plans/translation-roundtrip-pilot.md + the round-10 design.
//
// Idea (exploits that Pangram is a per-window MEAN): split T0 into sentences by risk; keep
// fact/boundary/next-step sentences as native T0 (safe), and route only LOW-RISK sentences
// (context/warmth/closing) through a Youdao round-trip (texture channel). Splice back in order.
// The MT-flavored texture windows pull the mean down a little while the fact windows (and all facts)
// stay intact -> a modest, fact-safe drop instead of fighting the drop/fact-damage coupling.
//   T0    = production baseline
//   DCR-A = T0 with context/warmth/closing sentences round-tripped (most conservative)
//   DCR-B = DCR-A + next_step sentences also round-tripped (more perturbation)
// DeepSeek only classifies sentences; it never writes the final English. Per-sentence local gates
// are deterministic (no new fact tokens, sentinels intact, sane length); full-text gates are the LLM
// judges. Pangram once per full-text gate survivor.

internal sealed class SentenceSplitter
{
    // Split into (text, trailing-separator) units, splitting on sentence-end punctuation OR newlines
    // so greetings/sign-offs and paragraph breaks become their own units and reassembly is exact.
    private static readonly Regex Boundary = new(@"((?<=[.!?])\s+|\n+)", RegexOptions.Compiled);

    public static List<(string Text, string Sep)> Split(string text)
    {
        var parts = Boundary.Split(text);
        var units = new List<(string Text, string Sep)>();
        for (var i = 0; i < parts.Length; i += 2)
        {
            var sentence = parts[i];
            var sep = i + 1 < parts.Length ? parts[i + 1] : string.Empty;
            if (sentence.Length == 0 && sep.Length == 0)
            {
                continue;
            }

            if (sentence.Length == 0 && units.Count > 0)
            {
                // stray separator -> attach to previous unit
                units[^1] = (units[^1].Text, units[^1].Sep + sep);
                continue;
            }

            units.Add((sentence, sep));
        }

        return units;
    }
}

internal sealed class SentenceRiskClassifier(DeepSeekChatClient chat)
{
    private const string SystemPrompt =
        "Classify each numbered sentence of an email into exactly one class: hard_fact (contains amount, "
        + "date, time, ID, SKU, phone, email, address, person name, or order status), boundary (cannot / "
        + "may / not yet / no decision / not a clinician / refund / policy / guarantee), next_step (asks the "
        + "reader to reply/send/confirm/call, a deadline, a channel, an action), context (background "
        + "explanation with no hard fact), warmth (thanks / acknowledgement / sorry-without-liability), or "
        + "closing (sign-off / final courtesy). When unsure between hard_fact/boundary and a softer class, "
        + "choose the safer (hard_fact or boundary). Return JSON: {\"sentences\":[{\"id\":\"S01\",\"class\":\"...\"}]}.";

    public async Task<Dictionary<string, string>> ClassifyAsync(IReadOnlyList<(string Id, string Text)> sentences, CancellationToken ct)
    {
        var user = string.Join("\n", sentences.Select(s => $"{s.Id}: {s.Text}"));
        var content = await chat.CompleteAsync(SystemPrompt, user, 1500, 0, ct);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(content))
        {
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("sentences", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in arr.EnumerateArray())
                {
                    var id = s.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() : null;
                    var cls = s.TryGetProperty("class", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                    if (id is not null && cls is not null)
                    {
                        result[id] = cls.Trim().ToLowerInvariant();
                    }
                }
            }
        }
        catch (JsonException)
        {
            // empty dict -> caller treats all as fact (no perturbation; DCR == T0, safe)
        }

        return result;
    }
}

internal sealed record DcrSentence(string Id, string Text, string Sep, string Class, string Route, string TextureReplacement);

internal sealed record PilotV8Row(
    string CaseId, int CaseNumber, string Category, string Tone,
    string T0Text, string DcrAText, string DcrBText,
    int TotalSentences, int TextureEligible, int TextureReplacedA, int TextureReplacedB,
    int TextureSentenceRatioA, int TextureCharRatioA,
    bool DcrAFactPass, bool DcrAHardPass, bool DcrANative,
    bool DcrBFactPass, bool DcrBHardPass, bool DcrBNative,
    int? PangramT0, int? PangramDcrA, int? PangramDcrB,
    string SafeBest, int? DeltaSafeBestMinusT0, string FailReason, string Notes);

internal static class R10DualChannelRunner
{
    private static readonly Regex FactToken = new(@"\$\d[\d,.]*|\b\d[\d,.:/-]*\b", RegexOptions.Compiled);

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
            Console.Error.WriteLine("DUAL_CHANNEL_TRANSLATION_PILOT: missing Youdao credentials.");
            return 2;
        }

        var youdaoMaxCalls = IntEnv("YOUDAO_MAX_CALLS", 80);
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
        var classifier = new SentenceRiskClassifier(deepseek);
        var understandability = new UnderstandabilityJudge(deepseek);
        var profJudge = new ProfessionalInternationalEnglishJudge(deepseek);
        var judge = new SemanticEvalJudge(judgeHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));
        var pangram = pangramEnabled ? new PangramWritingSignalClient(pangramHttp, pangramKey!, TimeSpan.FromSeconds(45)) : null;

        // DCR-A perturbs these classes; DCR-B additionally perturbs next_step.
        var classesA = new HashSet<string>(StringComparer.Ordinal) { "context", "warmth", "closing" };
        var classesB = new HashSet<string>(classesA, StringComparer.Ordinal) { "next_step" };

        Console.WriteLine(
            $"DCR pilot: cases={cases.Count} youdaoMax={youdaoMaxCalls} pangram={(pangramEnabled ? $"on(max {pangramMaxCalls})" : "off")} model={config.Model}");

        var rows = new List<PilotV8Row>();
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
            var units = SentenceSplitter.Split(t0Text);
            var ids = units.Select((u, i) => ($"S{i:D2}", u.Text)).ToList();
            var classMap = await classifier.ClassifyAsync(ids, CancellationToken.None);

            // Round-trip each texture-eligible sentence ONCE (cache); deterministic local gate.
            var sentences = new List<DcrSentence>();
            for (var i = 0; i < units.Count; i++)
            {
                var id = $"S{i:D2}";
                var text = units[i].Text;
                var cls = classMap.GetValueOrDefault(id, "hard_fact"); // unknown -> safest
                var replacement = string.Empty;
                if (classesB.Contains(cls) && !string.IsNullOrWhiteSpace(text) && youdao.CallCount + 2 <= youdaoMaxCalls)
                {
                    replacement = await TextureRoundTripAsync(text, ledger, mustKeep, mustNotClaim, youdao);
                }

                sentences.Add(new DcrSentence(id, text, units[i].Sep, cls,
                    classesB.Contains(cls) ? "texture" : "fact", replacement));
            }

            var dcrA = Assemble(sentences, classesA);
            var dcrB = Assemble(sentences, classesB);

            var (replacedA, charA) = ReplacementStats(sentences, classesA, t0Text.Length);
            var (replacedB, _) = ReplacementStats(sentences, classesB, t0Text.Length);
            var textureEligible = sentences.Count(s => classesB.Contains(s.Class));

            var aGate = await EvaluateAsync(dcrA, mustKeep, mustNotClaim, judge, understandability, profJudge);
            var bGate = string.Equals(dcrB, dcrA, StringComparison.Ordinal)
                ? aGate
                : await EvaluateAsync(dcrB, mustKeep, mustNotClaim, judge, understandability, profJudge);

            int? pT0 = null, pA = null, pB = null;
            if (pangram is not null)
            {
                if (pangramCalls < pangramMaxCalls)
                {
                    pT0 = await Measure(pangram, t0Text);
                    pangramCalls++;
                }

                if (aGate.HardPass && pangramCalls < pangramMaxCalls)
                {
                    pA = await Measure(pangram, dcrA);
                    pangramCalls++;
                }

                if (bGate.HardPass && !string.Equals(dcrB, dcrA, StringComparison.Ordinal) && pangramCalls < pangramMaxCalls)
                {
                    pB = await Measure(pangram, dcrB);
                    pangramCalls++;
                }
                else if (bGate.HardPass)
                {
                    pB = pA; // identical to A
                }
            }

            // SAFE-BEST: lowest Pangram among hard-gate passers.
            var (safeBest, bestPangram) = PickSafeBest(aGate.HardPass, pA, bGate.HardPass, pB);
            int? delta = bestPangram.HasValue && pT0.HasValue ? bestPangram.Value - pT0.Value : null;

            rows.Add(new PilotV8Row(
                sample.Id, sample.CaseNumber, sample.Category, tone,
                t0Text, dcrA, dcrB,
                sentences.Count, textureEligible, replacedA, replacedB,
                sentences.Count == 0 ? 0 : (int)Math.Round(100.0 * replacedA / sentences.Count), charA,
                aGate.FactPass, aGate.HardPass, aGate.NativeLike,
                bGate.FactPass, bGate.HardPass, bGate.NativeLike,
                pT0, pA, pB,
                safeBest, delta,
                aGate.HardPass ? string.Empty : aGate.FailReason,
                Notes(safeBest, delta, replacedA, sentences.Count)));

            Console.WriteLine(
                $"{sample.Id}: sentences={sentences.Count} texture={textureEligible} replacedA={replacedA} "
                + $"dcrA hard={aGate.HardPass} dcrB hard={bGate.HardPass} pangram T0={Fmt(pT0)} A={Fmt(pA)} B={Fmt(pB)} "
                + $"safeBest={safeBest} delta={Fmt(delta)}");
        }

        var summary = PilotV8Summary.Create(startedAt, DateTimeOffset.UtcNow, rows,
            youdao.CallCount, pangramCalls, deepseek.CallCount, modelCounter.CallCount, saplingCounter.CallCount, pangramEnabled);

        var stamp = startedAt.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(config.OutputDirectory);
        var jsonPath = Path.Combine(config.OutputDirectory, $"{stamp}-dcr-dual-channel-pilot.json");
        var mdPath = Path.Combine(config.OutputDirectory, $"{stamp}-dcr-dual-channel-pilot.md");
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(new { summary, rows }, jsonOptions));
        await File.WriteAllTextAsync(mdPath, PilotV8Report.Render(summary, rows));

        Console.WriteLine($"Wrote {jsonPath}");
        Console.WriteLine($"Wrote {mdPath}");
        Console.WriteLine(summary.OneLine());
        return 0;
    }

    // Round-trip one low-risk sentence; deterministic local gate. Returns replacement or "" (keep T0).
    private static async Task<string> TextureRoundTripAsync(
        string sentence, RewriteFactLedger ledger, IReadOnlyList<string> mustKeep, IReadOnlyList<string> mustNotClaim,
        YoudaoTranslationClient youdao)
    {
        var masked = AnchorMasker.Mask(sentence, ledger, mustKeep, mustNotClaim, bracketFree: true);
        var toZh = await youdao.TranslateAsync(masked.MaskedText, "en", "zh-CHS", CancellationToken.None);
        if (!toZh.Success)
        {
            return string.Empty;
        }

        var back = await youdao.TranslateAsync(toZh.Text, "zh-CHS", "en", CancellationToken.None);
        if (!back.Success)
        {
            return string.Empty;
        }

        var unmask = AnchorMasker.Unmask(back.Text, masked.Map);
        var candidate = unmask.Restored.Trim();

        // Local gates (deterministic): sentinels intact, non-empty, sane length, and NO new fact token
        // (digits/$) that was not in the original sentence.
        if (!unmask.IntegrityOk || candidate.Length == 0)
        {
            return string.Empty;
        }

        var ratio = (double)candidate.Length / Math.Max(1, sentence.Length);
        if (ratio is < 0.4 or > 1.9)
        {
            return string.Empty;
        }

        var origFacts = FactToken.Matches(sentence).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
        foreach (Match m in FactToken.Matches(candidate))
        {
            if (!origFacts.Contains(m.Value))
            {
                return string.Empty; // introduced a number/amount not in the original -> reject
            }
        }

        return candidate;
    }

    private static string Assemble(IReadOnlyList<DcrSentence> sentences, HashSet<string> textureClasses)
    {
        var sb = new StringBuilder();
        foreach (var s in sentences)
        {
            var text = textureClasses.Contains(s.Class) && s.TextureReplacement.Length > 0 ? s.TextureReplacement : s.Text;
            sb.Append(text).Append(s.Sep);
        }

        // Punctuation/space cleanup only (no English polishing). Fix common MT sign-off.
        var result = Regex.Replace(sb.ToString(), @"[ \t]{2,}", " ");
        result = Regex.Replace(result, @"(?im)^\s*the best\.?\s*$", "Best,");
        return result.TrimEnd();
    }

    private static (int Replaced, int CharPct) ReplacementStats(IReadOnlyList<DcrSentence> sentences, HashSet<string> textureClasses, int totalChars)
    {
        var replaced = sentences.Count(s => textureClasses.Contains(s.Class) && s.TextureReplacement.Length > 0);
        var changedChars = sentences.Where(s => textureClasses.Contains(s.Class) && s.TextureReplacement.Length > 0).Sum(s => s.Text.Length);
        return (replaced, totalChars == 0 ? 0 : (int)Math.Round(100.0 * changedChars / totalChars));
    }

    private sealed record GateResult(bool FactPass, bool HardPass, bool NativeLike, string FailReason);

    private static async Task<GateResult> EvaluateAsync(
        string text, IReadOnlyList<string> mustKeep, IReadOnlyList<string> mustNotClaim,
        SemanticEvalJudge judge, UnderstandabilityJudge understandability, ProfessionalInternationalEnglishJudge profJudge)
    {
        var sem = await judge.VerifyAsync(text, mustKeep, mustNotClaim, CancellationToken.None);
        var u = await understandability.JudgeAsync(text, CancellationToken.None);
        var prof = await profJudge.JudgeAsync(text, CancellationToken.None);
        var factPass = sem.Error is null && sem.FactsReallyPass;
        var hardPass = factPass && sem.RealForbidden == 0 && !sem.MeaningChanged && u.Understandable && prof.ProfessionalIntl;
        var fail = hardPass ? string.Empty
            : !factPass ? "fact_drift"
            : sem.RealForbidden > 0 ? "forbidden"
            : sem.MeaningChanged ? "meaning_changed"
            : !u.Understandable ? "not_understandable"
            : "not_professional";
        return new GateResult(factPass, hardPass, prof.NativeLike, fail);
    }

    private static (string SafeBest, int? Pangram) PickSafeBest(bool aPass, int? pA, bool bPass, int? pB)
    {
        var options = new List<(string Name, int? P)>();
        if (aPass)
        {
            options.Add(("DCR-A", pA));
        }

        if (bPass)
        {
            options.Add(("DCR-B", pB));
        }

        if (options.Count == 0)
        {
            return ("T0", null);
        }

        var measured = options.Where(o => o.P.HasValue).ToList();
        if (measured.Count == 0)
        {
            return (options[0].Name, null);
        }

        var best = measured.OrderBy(o => o.P!.Value).First();
        return (best.Name, best.P);
    }

    private static async Task<int?> Measure(IWritingSignalClient client, string text)
    {
        var r = await client.MeasureAsync(text, CancellationToken.None);
        return r.Available ? r.AiLikePercent : null;
    }

    private static PilotV8Row EmptyRow(EvalCase sample, string tone, string? errorCode) => new(
        sample.Id, sample.CaseNumber, sample.Category, tone,
        string.Empty, string.Empty, string.Empty,
        0, 0, 0, 0, 0, 0,
        false, false, false, false, false, false,
        null, null, null, "T0", null, $"t0_no_output:{errorCode ?? "unknown"}",
        "T0 produced no output.");

    private static string Notes(string safeBest, int? delta, int replaced, int total)
    {
        if (safeBest == "T0")
        {
            return "No DCR branch passed hard gates; fell back to T0.";
        }

        var d = delta is null ? "Pangram not measured" : delta < 0 ? $"Pangram {Math.Abs(delta.Value)} lower" : delta > 0 ? $"Pangram {delta.Value} higher" : "Pangram unchanged";
        return $"{safeBest} chosen ({replaced}/{total} sentences perturbed); {d} vs T0.";
    }

    private static string? FirstEnv(params string[] names) =>
        names.Select(Environment.GetEnvironmentVariable).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static int IntEnv(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var v) && v >= 0 ? v : fallback;

    private static string Fmt(int? v) => v?.ToString() ?? "-";
}

internal sealed record PilotV8Summary(
    DateTimeOffset StartedAt, DateTimeOffset FinishedAt, int Cases, int T0WithOutput,
    int DcrAHardPass, int DcrBHardPass, int SafeBestNonT0,
    int AvgTextureSentenceRatio, int AvgTextureCharRatio,
    int PangramPairs, int Lower, int Higher, int Equal, int? MeanDelta, int? MedianDelta, int? MeanT0, int? MeanBest,
    IReadOnlyDictionary<string, int> FailReasons,
    bool PangramEnabled, int YoudaoCalls, int PangramCalls, int DeepSeekCalls, int ModelCalls, int SaplingCalls)
{
    public static PilotV8Summary Create(
        DateTimeOffset startedAt, DateTimeOffset finishedAt, IReadOnlyList<PilotV8Row> rows,
        int youdaoCalls, int pangramCalls, int deepseekCalls, int modelCalls, int saplingCalls, bool pangramEnabled)
    {
        var withOutput = rows.Where(r => !string.IsNullOrEmpty(r.T0Text)).ToList();
        var chosen = withOutput.Where(r => r.SafeBest != "T0" && r.DeltaSafeBestMinusT0.HasValue).ToList();
        var deltas = chosen.Select(r => r.DeltaSafeBestMinusT0!.Value).ToList();
        var t0s = chosen.Where(r => r.PangramT0.HasValue).Select(r => r.PangramT0!.Value).ToList();
        var bests = chosen.Select(r => (r.PangramT0 ?? 0) + r.DeltaSafeBestMinusT0!.Value).ToList();
        var fails = withOutput.Where(r => r.SafeBest == "T0").GroupBy(r => r.FailReason).ToDictionary(g => g.Key, g => g.Count());

        return new PilotV8Summary(
            startedAt, finishedAt, rows.Count, withOutput.Count,
            rows.Count(r => r.DcrAHardPass), rows.Count(r => r.DcrBHardPass), rows.Count(r => r.SafeBest != "T0"),
            withOutput.Count == 0 ? 0 : (int)Math.Round(withOutput.Average(r => r.TextureSentenceRatioA)),
            withOutput.Count == 0 ? 0 : (int)Math.Round(withOutput.Average(r => r.TextureCharRatioA)),
            deltas.Count, deltas.Count(d => d < 0), deltas.Count(d => d > 0), deltas.Count(d => d == 0),
            deltas.Count == 0 ? null : (int?)Math.Round(deltas.Average(), MidpointRounding.AwayFromZero), Median(deltas),
            t0s.Count == 0 ? null : (int?)Math.Round(t0s.Average(), MidpointRounding.AwayFromZero),
            bests.Count == 0 ? null : (int?)Math.Round(bests.Average(), MidpointRounding.AwayFromZero),
            fails, pangramEnabled, youdaoCalls, pangramCalls, deepseekCalls, modelCalls, saplingCalls);
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
        $"DCR pilot: T0out={T0WithOutput}/{Cases}, DCR-A hardPass={DcrAHardPass}, DCR-B hardPass={DcrBHardPass}, "
        + $"safeBest≠T0={SafeBestNonT0}, avgTextureSentence={AvgTextureSentenceRatio}%, avgTextureChar={AvgTextureCharRatio}%, "
        + $"pangramPairs={PangramPairs} (lower={Lower}/higher={Higher}/eq={Equal}), meanT0={MeanT0}, meanBest={MeanBest}, "
        + $"meanDelta={(MeanDelta?.ToString() ?? "n/a")}, medianDelta={(MedianDelta?.ToString() ?? "n/a")}, "
        + $"youdao={YoudaoCalls}, pangram={PangramCalls}, deepseek={DeepSeekCalls}";
}

internal static class PilotV8Report
{
    public static string Render(PilotV8Summary s, IReadOnlyList<PilotV8Row> rows)
    {
        var lines = new List<string>
        {
            "# DCR dual-channel pilot (round 10)",
            "",
            "**Eval-only research pilot.** Not wired into production. Fact/boundary/(next-step) sentences stay as native T0; only low-risk context/warmth/closing sentences are round-tripped through Youdao (texture channel) and spliced back. DCR-A = texture only; DCR-B = + next_step. DeepSeek only classifies sentences. Hard gates: Fact + Boundary + Forbidden + Understandability + ProfessionalInternational. Pangram once per gate survivor. The hypothesis: perturbing only safe sentences pulls the windowed Pangram mean down modestly while keeping facts intact.",
            "",
            $"Started: {s.StartedAt:O}  Finished: {s.FinishedAt:O}",
            "",
            "## Headline",
            "",
        };

        if (!s.PangramEnabled)
        {
            lines.Add("Pangram disabled — gates only.");
        }
        else if (s.PangramPairs == 0)
        {
            lines.Add("No DCR branch passed hard gates as a distinct output → no pair.");
        }
        else
        {
            lines.Add($"- DCR hard-gate pass: DCR-A {s.DcrAHardPass}/{s.Cases}, DCR-B {s.DcrBHardPass}/{s.Cases}; SAFE-BEST≠T0 in {s.SafeBestNonT0}/{s.Cases}.");
            lines.Add($"- Texture perturbation: avg {s.AvgTextureSentenceRatio}% of sentences, {s.AvgTextureCharRatio}% of chars.");
            lines.Add($"- Pangram of {s.PangramPairs} SAFE-BEST pairs: lower {s.Lower}, higher {s.Higher}, equal {s.Equal} (mean T0 {s.MeanT0} → best {s.MeanBest}, mean Δ **{Signed(s.MeanDelta)}**, median **{Signed(s.MedianDelta)}**).");
            lines.Add($"- §9 bar: hard-gate ≥9/10 {(s.DcrAHardPass >= 9 ? "✓" : "✗")}; Pangram-win ≥5 {(s.Lower >= 5 ? "✓" : "✗")}; median Δ ≤−10 {(s.MedianDelta is int md && md <= -10 ? "✓" : "✗")}.");
        }

        lines.Add("");
        lines.Add("## Fail reasons (SAFE-BEST=T0)");
        lines.Add(s.FailReasons.Count == 0 ? "(none)" : string.Join(", ", s.FailReasons.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}×{kv.Value}")));
        lines.Add("");
        lines.Add($"Cost: Youdao **{s.YoudaoCalls}** · Pangram **{s.PangramCalls}** · DeepSeek (classify+gates) **{s.DeepSeekCalls}** · DeepSeek model (T0) **{s.ModelCalls}** · Sapling **{s.SaplingCalls}**.");
        lines.Add("");
        lines.Add("## Per-case");
        lines.Add("");
        lines.Add("| Case | Cat | sents | texture-elig | replacedA | %sent | DCR-A hard | DCR-B hard | Pangram T0 | A | B | safe-best | Δ |");
        lines.Add("| --- | --- | ---: | ---: | ---: | ---: | :---: | :---: | ---: | ---: | ---: | :---: | ---: |");
        foreach (var r in rows)
        {
            lines.Add(string.Join(" | ", new[]
            {
                r.CaseId, r.Category,
                r.TotalSentences.ToString(), r.TextureEligible.ToString(), r.TextureReplacedA.ToString(), r.TextureSentenceRatioA + "%",
                r.DcrAHardPass ? "y" : "n", r.DcrBHardPass ? "y" : "n",
                r.PangramT0?.ToString() ?? "-", r.PangramDcrA?.ToString() ?? "-", r.PangramDcrB?.ToString() ?? "-",
                r.SafeBest, Signed(r.DeltaSafeBestMinusT0),
            }));
        }

        lines.Add("");
        lines.Add("## Text comparison (T0 vs DCR-A)");
        foreach (var r in rows.Where(r => !string.IsNullOrEmpty(r.T0Text)))
        {
            lines.Add("");
            lines.Add($"### {r.CaseId} — {r.Category} (safe-best {r.SafeBest}, Δ {Signed(r.DeltaSafeBestMinusT0)}, {r.TextureReplacedA}/{r.TotalSentences} perturbed)");
            lines.Add("");
            lines.Add("**T0:**");
            lines.Add("> " + r.T0Text.Replace("\n", "\n> ", StringComparison.Ordinal));
            lines.Add("");
            lines.Add("**DCR-A:**");
            lines.Add("> " + (string.IsNullOrWhiteSpace(r.DcrAText) ? "(none)" : r.DcrAText.Replace("\n", "\n> ", StringComparison.Ordinal)));
        }

        lines.Add("");
        return string.Join("\n", lines) + "\n";
    }

    private static string Signed(int? v) => v is null ? "-" : v.Value > 0 ? $"+{v.Value}" : v.Value.ToString();
}
