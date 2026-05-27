using System.Text.Json;
using System.Text.Json.Serialization;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.RewriteEngine;
using ReplyInMyVoice.Infrastructure.Providers;

// Round-8 layered-routing pilot (R8_LAYERED_PILOT=1). EVAL-ONLY.
// See plans/translation-roundtrip-pilot.md + the round-8 design.
//
// R8's reframe: don't force "native + low Pangram" into one path. The detection-mode target is
// redefined from native-send-ready to CONTROLLED INTERNATIONAL ENGLISH (professional, understandable,
// sendable, slight non-native rhythm allowed — but no garble / agent-error / object drift / severe
// grammar). This pass implements the decisive, cheapest branch:
//   T0  = production baseline
//   R8A = DeepSeek generates controlled international English directly (NO translation, NO native polish)
// It tests the core new hypothesis on its own: can changing only the English *register* lower Pangram
// while passing a professional (not native) quality bar? B/C (translation / segment routes) are built
// only if R8A shows signal — they re-add the translation tradeoff already mapped in rounds 1-7.

internal sealed record R8aResult(string Text, string StyleNotes, string RiskNotes, string? Error);

// Generates "controlled international English": professional + sendable but deliberately NOT polished
// native corporate prose. The hypothesis is that this register is less AI-template-like.
internal sealed class ControlledInternationalGenerator(DeepSeekChatClient chat)
{
    private const string SystemPrompt =
        "Rewrite the draft into clear PROFESSIONAL INTERNATIONAL ENGLISH. Important: do NOT make it sound "
        + "like highly polished native corporate writing. Use simple, direct wording; short concrete "
        + "sentences; avoid generic AI-email phrasing and overly smooth transitions; avoid idioms and "
        + "corporate templates. Keep it professional, warm, and sendable. Hard requirements: "
        + "(1) preserve every fact, amount, date, time, name, identifier, SKU, contact detail, and next "
        + "step; (2) preserve every boundary (cannot / may / not yet / no decision / no advice / no refund "
        + "/ no guarantee); (3) add no promises, discounts, refunds, apologies, medical/legal advice, "
        + "hiring decisions, deadline extensions, or policy exceptions; (4) do NOT add intentional mistakes "
        + "— it must stay professional and understandable; (5) prefer short concrete sentences, do not "
        + "over-polish. Return JSON: {\"text\":\"...\",\"style_notes\":\"...\",\"risk_notes\":\"...\"}.";

    public async Task<R8aResult> GenerateAsync(
        string draft, string tone, IReadOnlyList<string> mustKeep, IReadOnlyList<string> mustNotClaim,
        IReadOnlyList<string> protectedTerms, CancellationToken ct)
    {
        var user =
            $"Tone: {tone}\n\nDraft:\n{draft}\n\n"
            + "Facts to preserve:\n" + string.Join("\n", mustKeep.Select(f => "- " + f))
            + "\n\nBoundaries (must not change):\n" + string.Join("\n", mustNotClaim.Select(f => "- " + f))
            + "\n\nProtected business terms (keep exact):\n"
            + (protectedTerms.Count == 0 ? "(none)" : string.Join("\n", protectedTerms.Select(p => "- " + p)));

        var content = await chat.CompleteAsync(SystemPrompt, user, 1400, 0.4, ct);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new R8aResult(string.Empty, string.Empty, string.Empty, "gen_failed");
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var text = root.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
            return string.IsNullOrWhiteSpace(text)
                ? new R8aResult("", "", "", "gen_no_text")
                : new R8aResult(text, Str(root, "style_notes"), Str(root, "risk_notes"), null);
        }
        catch (JsonException)
        {
            return new R8aResult(string.Empty, string.Empty, string.Empty, "gen_parse_failed");
        }
    }

    private static string Str(JsonElement e, string n) =>
        e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}

internal sealed record ProfIntlVerdict(bool ProfessionalIntl, bool NativeLike, IReadOnlyList<string> Issues, string? Error = null);

// ProfessionalInternationalEnglishGate: passes "professional, understandable, acceptable to send" even
// with slight non-native rhythm; fails only garble / wrong-agent / object drift / severe grammar / raw
// MT. Also records native_like separately (the NativeSendReady soft signal).
internal sealed class ProfessionalInternationalEnglishJudge(DeepSeekChatClient chat)
{
    private const string SystemPrompt =
        "You judge whether an email is PROFESSIONAL, UNDERSTANDABLE, and ACCEPTABLE TO SEND, written in "
        + "clear international English. It does NOT need to read like a native speaker — slight non-native "
        + "rhythm, simple/direct phrasing, or mild awkwardness is acceptable. Set professional_international "
        + "= false ONLY if it has: garbled/scrambled clauses, wrong subject/agent (e.g. the sender appears "
        + "to request their OWN refund), object/identity drift, severe grammar errors that look "
        + "unprofessional, gibberish, or it reads like raw machine translation. Separately set native_like "
        + "= true only if it reads like polished native English. Judge style/fluency, not facts. Return "
        + "JSON: {\"professional_international\":true,\"native_like\":false,\"issues\":[\"...\"]}.";

    public async Task<ProfIntlVerdict> JudgeAsync(string text, CancellationToken ct)
    {
        var content = await chat.CompleteAsync(SystemPrompt, text, 500, 0, ct);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new ProfIntlVerdict(false, false, Array.Empty<string>(), "judge_failed");
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var prof = root.TryGetProperty("professional_international", out var p) && p.ValueKind == JsonValueKind.True;
            var native = root.TryGetProperty("native_like", out var n) && n.ValueKind == JsonValueKind.True;
            var issues = root.TryGetProperty("issues", out var iss) && iss.ValueKind == JsonValueKind.Array
                ? iss.EnumerateArray().Select(i => i.GetString() ?? "").Where(i => i.Length > 0).ToList()
                : new List<string>();
            return new ProfIntlVerdict(prof, native, issues);
        }
        catch (JsonException)
        {
            return new ProfIntlVerdict(false, false, Array.Empty<string>(), "judge_parse_failed");
        }
    }
}

internal sealed record PilotV7Row(
    string CaseId, int CaseNumber, string Category, string Tone, string RiskTier,
    string T0Text, string R8aText, string R8aStyleNotes,
    bool T0HardPass, bool T0NativeLike,
    bool R8aFactPass, int R8aForbid, bool R8aMeaningChanged, bool R8aUnderstandable,
    bool R8aProfessionalIntl, bool R8aNativeLike, bool R8aHardPass,
    IReadOnlyList<string> R8aMissingFacts, IReadOnlyList<string> R8aProfIssues,
    int? PangramT0, int? PangramR8a, int? DeltaR8aMinusT0,
    string FallbackReason, string Notes);

internal static class R8LayeredRunner
{
    public static async Task<int> RunAsync(
        FactReconstructRewriteProvider t0Provider,
        CountingRewriteModelClient modelCounter,
        CountingWritingSignalClient saplingCounter,
        IReadOnlyList<EvalCase> cases,
        EvalConfig config,
        string apiKey,
        DateTimeOffset startedAt)
    {
        var pangramMaxCalls = IntEnv("PANGRAM_MAX_CALLS", 0);
        var pangramKey = Environment.GetEnvironmentVariable("PANGRAM_API_KEY");
        var pangramEnabled = pangramMaxCalls > 0 && !string.IsNullOrWhiteSpace(pangramKey);

        using var deepseekHttp = new HttpClient();
        using var judgeHttp = new HttpClient();
        using var pangramHttp = new HttpClient();

        var deepseek = new DeepSeekChatClient(deepseekHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));
        var generator = new ControlledInternationalGenerator(deepseek);
        var profJudge = new ProfessionalInternationalEnglishJudge(deepseek);
        var understandability = new UnderstandabilityJudge(deepseek);
        var termProposer = new ProtectedTermProposer(deepseek);
        var judge = new SemanticEvalJudge(judgeHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));
        var pangram = pangramEnabled ? new PangramWritingSignalClient(pangramHttp, pangramKey!, TimeSpan.FromSeconds(45)) : null;

        Console.WriteLine(
            $"R8 pilot (R8A controlled-international, no translation): cases={cases.Count} "
            + $"pangram={(pangramEnabled ? $"on(max {pangramMaxCalls})" : "off")} model={config.Model}");

        var rows = new List<PilotV7Row>();
        var pangramCalls = 0;

        foreach (var sample in cases)
        {
            var request = sample.ToRewriteRequest();
            var tone = request.Tone;
            var mustKeep = sample.MustKeep;
            var mustNotClaim = sample.MustNotClaim;

            var t0Result = await t0Provider.RewriteAsync(Guid.NewGuid(), request, CancellationToken.None);
            var t0Text = RewritePayload.TryParse(t0Result.ResultJson)?.RewrittenText ?? string.Empty;

            var protectedTerms = (await termProposer.ProposeAsync(sample.InputDraft, CancellationToken.None)).ToList();
            var r8a = await generator.GenerateAsync(sample.InputDraft, tone, mustKeep, mustNotClaim, protectedTerms, CancellationToken.None);

            // Gate T0 (for symmetric comparison) and R8A.
            var t0Gate = string.IsNullOrWhiteSpace(t0Text) ? null
                : await EvaluateAsync(t0Text, mustKeep, mustNotClaim, judge, understandability, profJudge);
            var r8aGate = r8a.Error is null && !string.IsNullOrWhiteSpace(r8a.Text)
                ? await EvaluateAsync(r8a.Text, mustKeep, mustNotClaim, judge, understandability, profJudge)
                : null;

            int? pT0 = null, pR8a = null;
            if (pangram is not null)
            {
                if (t0Gate is { HardPass: true } && pangramCalls < pangramMaxCalls)
                {
                    pT0 = await Measure(pangram, t0Text);
                    pangramCalls++;
                }

                if (r8aGate is { HardPass: true } && pangramCalls < pangramMaxCalls)
                {
                    pR8a = await Measure(pangram, r8a.Text);
                    pangramCalls++;
                }
            }

            int? delta = pT0.HasValue && pR8a.HasValue ? pR8a.Value - pT0.Value : null;
            var fail = r8a.Error is not null ? r8a.Error
                : r8aGate is null ? "no_output"
                : r8aGate.HardPass ? ""
                : !r8aGate.ProfessionalIntl ? "not_professional_international"
                : !r8aGate.FactPass ? "fact_drift"
                : r8aGate.Forbid > 0 ? "forbidden"
                : r8aGate.MeaningChanged ? "meaning_changed"
                : !r8aGate.Understandable ? "not_understandable"
                : "fail";

            rows.Add(new PilotV7Row(
                sample.Id, sample.CaseNumber, sample.Category, tone, RiskTier(sample.Category),
                t0Text, r8a.Text, r8a.StyleNotes,
                t0Gate?.HardPass ?? false, t0Gate?.NativeLike ?? false,
                r8aGate?.FactPass ?? false, r8aGate?.Forbid ?? 0, r8aGate?.MeaningChanged ?? false, r8aGate?.Understandable ?? false,
                r8aGate?.ProfessionalIntl ?? false, r8aGate?.NativeLike ?? false, r8aGate?.HardPass ?? false,
                r8aGate?.MissingFacts ?? Array.Empty<string>(), r8aGate?.ProfIssues ?? Array.Empty<string>(),
                pT0, pR8a, delta, fail, Notes(r8aGate, delta)));

            Console.WriteLine(
                $"{sample.Id}: r8aHard={r8aGate?.HardPass ?? false} profIntl={r8aGate?.ProfessionalIntl ?? false} "
                + $"native={r8aGate?.NativeLike ?? false} pangram T0={Fmt(pT0)} R8A={Fmt(pR8a)} delta={Fmt(delta)}"
                + $"{(r8aGate?.HardPass ?? false ? "" : " fail:" + fail)}");
        }

        var summary = PilotV7Summary.Create(startedAt, DateTimeOffset.UtcNow, rows,
            pangramCalls, deepseek.CallCount, modelCounter.CallCount, saplingCounter.CallCount, pangramEnabled);

        var stamp = startedAt.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(config.OutputDirectory);
        var jsonPath = Path.Combine(config.OutputDirectory, $"{stamp}-r8a-controlled-intl-pilot.json");
        var mdPath = Path.Combine(config.OutputDirectory, $"{stamp}-r8a-controlled-intl-pilot.md");
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(new { summary, rows }, jsonOptions));
        await File.WriteAllTextAsync(mdPath, PilotV7Report.Render(summary, rows));

        Console.WriteLine($"Wrote {jsonPath}");
        Console.WriteLine($"Wrote {mdPath}");
        Console.WriteLine(summary.OneLine());
        return 0;
    }

    private sealed record GateResult(
        bool FactPass, int Forbid, bool MeaningChanged, bool Understandable, bool ProfessionalIntl, bool NativeLike,
        IReadOnlyList<string> MissingFacts, IReadOnlyList<string> ProfIssues)
    {
        public bool HardPass => FactPass && Forbid == 0 && !MeaningChanged && Understandable && ProfessionalIntl;
    }

    private static async Task<GateResult> EvaluateAsync(
        string text, IReadOnlyList<string> mustKeep, IReadOnlyList<string> mustNotClaim,
        SemanticEvalJudge judge, UnderstandabilityJudge understandability, ProfessionalInternationalEnglishJudge profJudge)
    {
        var sem = await judge.VerifyAsync(text, mustKeep, mustNotClaim, CancellationToken.None);
        var u = await understandability.JudgeAsync(text, CancellationToken.None);
        var prof = await profJudge.JudgeAsync(text, CancellationToken.None);
        var missing = sem.Error is null
            ? sem.Facts.Where(f => f.Status is "missing" or "contradicted").Select(f => $"{f.Status}:{f.Fact}").ToList()
            : new List<string> { $"judge_error:{sem.Error}" };
        return new GateResult(
            sem.Error is null && sem.FactsReallyPass, sem.RealForbidden, sem.MeaningChanged,
            u.Understandable, prof.ProfessionalIntl, prof.NativeLike, missing, prof.Issues);
    }

    private static string RiskTier(string category) => category.ToLowerInvariant() switch
    {
        var c when c.Contains("medical") => "R3",
        var c when c.Contains("billing") || c.Contains("recruiting") || c.Contains("hr_") => "R3",
        var c when c.Contains("support") => "R2",
        var c when c.Contains("workplace") => "R1",
        _ => "R2",
    };

    private static async Task<int?> Measure(IWritingSignalClient client, string text)
    {
        var r = await client.MeasureAsync(text, CancellationToken.None);
        return r.Available ? r.AiLikePercent : null;
    }

    private static string Notes(object? gate, int? delta)
    {
        if (gate is null)
        {
            return "R8A no output.";
        }

        var g = (GateResult)gate;
        if (!g.HardPass)
        {
            return "R8A failed hard gates.";
        }

        var d = delta is null ? "Pangram not measured" : delta < 0 ? $"Pangram {Math.Abs(delta.Value)} lower" : delta > 0 ? $"Pangram {delta.Value} higher" : "Pangram equal";
        return $"R8A passed (professional-international, native_like={g.NativeLike}); {d} vs T0.";
    }

    private static int IntEnv(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var v) && v > 0 ? v : fallback;

    private static string Fmt(int? v) => v?.ToString() ?? "-";
}

internal sealed record PilotV7Summary(
    DateTimeOffset StartedAt, DateTimeOffset FinishedAt, int Cases, int T0WithOutput,
    int R8aHardPass, int R8aProfessionalIntl, int R8aNativeLike, int T0HardPass, int T0NativeLike,
    IReadOnlyDictionary<string, int> FailReasons,
    int PangramPairs, int R8aLower, int R8aHigher, int R8aEqual, int? MeanDelta, int? MedianDelta,
    int? MeanT0, int? MeanR8a, bool PangramEnabled, int PangramCalls, int DeepSeekCalls, int ModelCalls, int SaplingCalls)
{
    public static PilotV7Summary Create(
        DateTimeOffset startedAt, DateTimeOffset finishedAt, IReadOnlyList<PilotV7Row> rows,
        int pangramCalls, int deepseekCalls, int modelCalls, int saplingCalls, bool pangramEnabled)
    {
        var withOutput = rows.Where(r => !string.IsNullOrEmpty(r.T0Text)).ToList();
        var pass = rows.Where(r => r.R8aHardPass).ToList();
        var pairs = pass.Where(r => r.DeltaR8aMinusT0.HasValue).Select(r => r.DeltaR8aMinusT0!.Value).ToList();
        var fails = rows.Where(r => !r.R8aHardPass).GroupBy(r => r.FallbackReason).ToDictionary(g => g.Key, g => g.Count());
        var t0s = pass.Where(r => r.PangramT0.HasValue).Select(r => r.PangramT0!.Value).ToList();
        var r8as = pass.Where(r => r.PangramR8a.HasValue).Select(r => r.PangramR8a!.Value).ToList();

        return new PilotV7Summary(
            startedAt, finishedAt, rows.Count, withOutput.Count,
            rows.Count(r => r.R8aHardPass), rows.Count(r => r.R8aProfessionalIntl), rows.Count(r => r.R8aNativeLike),
            rows.Count(r => r.T0HardPass), rows.Count(r => r.T0NativeLike),
            fails,
            pairs.Count, pairs.Count(d => d < 0), pairs.Count(d => d > 0), pairs.Count(d => d == 0),
            pairs.Count == 0 ? null : (int?)Math.Round(pairs.Average(), MidpointRounding.AwayFromZero), Median(pairs),
            t0s.Count == 0 ? null : (int?)Math.Round(t0s.Average(), MidpointRounding.AwayFromZero),
            r8as.Count == 0 ? null : (int?)Math.Round(r8as.Average(), MidpointRounding.AwayFromZero),
            pangramEnabled, pangramCalls, deepseekCalls, modelCalls, saplingCalls);
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
        $"R8A pilot: T0out={T0WithOutput}/{Cases}, R8A hardPass={R8aHardPass}/{Cases}, profIntl={R8aProfessionalIntl}, "
        + $"native(R8A)={R8aNativeLike}, native(T0)={T0NativeLike}, pangramPairs={PangramPairs} "
        + $"(lower={R8aLower}/higher={R8aHigher}/eq={R8aEqual}), meanT0={MeanT0}, meanR8A={MeanR8a}, "
        + $"meanDelta={(MeanDelta?.ToString() ?? "n/a")}, medianDelta={(MedianDelta?.ToString() ?? "n/a")}, "
        + $"pangram={PangramCalls}, deepseek={DeepSeekCalls}";
}

internal static class PilotV7Report
{
    public static string Render(PilotV7Summary s, IReadOnlyList<PilotV7Row> rows)
    {
        var lines = new List<string>
        {
            "# R8A controlled-international-English pilot (round 8, branch A)",
            "",
            "**Eval-only research pilot.** Not wired into production. R8A = DeepSeek generates *controlled international English* directly (no translation, no native polish). Tests R8's core hypothesis: can changing only the English register lower Pangram while passing a PROFESSIONAL (not native) bar? Hard gates: Fact + Boundary + Understandability + ProfessionalInternationalEnglish; NativeSendReady recorded. (R8B/R8C built only if R8A shows signal.)",
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
            lines.Add("No R8A passed hard gates as a distinct output → no pair.");
        }
        else
        {
            lines.Add($"- R8A hard-gate pass: **{s.R8aHardPass}/{s.Cases}** (professional-international {s.R8aProfessionalIntl}/{s.Cases}). native_like: R8A {s.R8aNativeLike} vs T0 {s.T0NativeLike}.");
            lines.Add($"- Pangram of {s.PangramPairs} pairs: lower {s.R8aLower}, higher {s.R8aHigher}, equal {s.R8aEqual} (mean T0 {s.MeanT0} → R8A {s.MeanR8a}, mean Δ **{Signed(s.MeanDelta)}**, median **{Signed(s.MedianDelta)}**).");
            lines.Add($"- §10 bar: hard-gate ≥8/10 {(s.R8aHardPass >= 8 ? "✓" : "✗")}; Pangram-win ≥5 {(s.R8aLower >= 5 ? "✓" : "✗")}; median Δ ≤−15 {(s.MedianDelta is int md && md <= -15 ? "✓" : "✗")}.");
        }

        lines.Add("");
        lines.Add("## Fail reasons");
        lines.Add(s.FailReasons.Count == 0 ? "(none)" : string.Join(", ", s.FailReasons.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}×{kv.Value}")));
        lines.Add("");
        lines.Add($"Cost: Pangram **{s.PangramCalls}** · DeepSeek (gen+gates) **{s.DeepSeekCalls}** · DeepSeek model (T0) **{s.ModelCalls}** · Sapling **{s.SaplingCalls}**. No translation calls.");
        lines.Add("");
        lines.Add("## Per-case");
        lines.Add("");
        lines.Add("| Case | Cat | R8A fact | forbid | meaning | underst | prof-intl | native(R8A) | hard | Pangram T0 | Pangram R8A | Δ | fail |");
        lines.Add("| --- | --- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | ---: | ---: | ---: | --- |");
        foreach (var r in rows)
        {
            lines.Add(string.Join(" | ", new[]
            {
                r.CaseId, r.Category,
                YN(r.R8aFactPass), r.R8aForbid.ToString(), YN(r.R8aMeaningChanged), YN(r.R8aUnderstandable),
                YN(r.R8aProfessionalIntl), YN(r.R8aNativeLike), YN(r.R8aHardPass),
                r.PangramT0?.ToString() ?? "-", r.PangramR8a?.ToString() ?? "-", Signed(r.DeltaR8aMinusT0),
                (r.R8aHardPass ? "" : r.FallbackReason).Replace("|", "/", StringComparison.Ordinal),
            }));
        }

        lines.Add("");
        lines.Add("## Text comparison (T0 vs R8A)");
        foreach (var r in rows)
        {
            lines.Add("");
            lines.Add($"### {r.CaseId} — {r.Category} (R8A hard={r.R8aHardPass}, native_like={r.R8aNativeLike}, Δ={Signed(r.DeltaR8aMinusT0)})");
            if (r.R8aProfIssues.Count > 0)
            {
                lines.Add($"_prof-intl issues: {string.Join("; ", r.R8aProfIssues).Replace("|", "/", StringComparison.Ordinal)}_");
            }

            lines.Add("");
            lines.Add("**T0:**");
            lines.Add("> " + (string.IsNullOrWhiteSpace(r.T0Text) ? "(no output)" : r.T0Text.Replace("\n", "\n> ", StringComparison.Ordinal)));
            lines.Add("");
            lines.Add("**R8A (controlled international):**");
            lines.Add("> " + (string.IsNullOrWhiteSpace(r.R8aText) ? "(no output)" : r.R8aText.Replace("\n", "\n> ", StringComparison.Ordinal)));
        }

        lines.Add("");
        return string.Join("\n", lines) + "\n";
    }

    private static string YN(bool b) => b ? "y" : "n";

    private static string Signed(int? v) => v is null ? "-" : v.Value > 0 ? $"+{v.Value}" : v.Value.ToString();
}
