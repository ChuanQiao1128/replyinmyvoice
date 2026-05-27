using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// U3 deterministic-template pilot (U3_TEMPLATE_PILOT=1). EVAL-ONLY. The "last shot" at the
// AI-detection question: build the FINAL English from hardcoded, human-written templates filled with
// DeepSeek-extracted fact slots — NO LLM English authoring and NO translation, so the output carries
// neither an LLM generation fingerprint nor an MT surface. Then ask GPTZero whether it scores BELOW
// the AI draft. This is the only U-variant with a genuinely new detection surface (U1/U2 both end on a
// Youdao MT surface already measured at 100% AI on GPTZero).
//
// Deliberately uses 2 SIMPLE cases (one scheduling, one billing) whose every fact fits a template;
// the production hard cases (005 sales-quote, 041 multi-item partial refund) are too fact-dense for a
// fixed template and would fail the semantic gate before ever reaching GPTZero (that mismatch is the
// whole reason this pilot does not reuse them).
//
// Flow per case: score draft on GPTZero (baseline) -> DeepSeek extracts flat slots -> code picks a
// template by email_type and fills it -> semantic judge gates the render (facts/forbidden/meaning) ->
// GPTZero scores the render ONLY when the gate passes. Hard budget: GPTZERO_MAX_CALLS (default 6).
// No best-of-N, no detection-driven loop — one render per case, scored once.
internal static class U3TemplatePilot
{
    // Test inputs live in SimplePilotCases.All (shared with the U2 pilot so both score identical drafts).

    // Hardcoded, human-written templates (NOT LLM-generated). Each holds exactly the draft's facts in
    // {slot} placeholders. Kept terse and plain on purpose — that plainness is what is being tested.
    private static readonly IReadOnlyDictionary<string, string[]> Templates =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["scheduling"] = new[]
            {
                "Hi {greeting_name},\n\n"
                + "Thanks for the invite. I can't make {unavailable_date} — I've got a conflict that day. "
                + "{proposed_date} works for me though, if that's an option on your end. Does that work for you?\n\n"
                + "Thanks,\n{signoff_name}",
            },
            ["billing"] = new[]
            {
                "Hi,\n\n"
                + "Just following up on invoice {invoice_id} for {amount}, which was due {due_date}. We "
                + "haven't received the payment yet on our end. Could you confirm whether it was paid under a "
                + "different reference number?\n\n"
                + "Thanks",
            },
        };

    // Slots a render needs; if any is missing/empty after extraction the case falls back (skip), it is
    // never rendered with a hole.
    private static readonly IReadOnlyDictionary<string, string[]> RequiredSlots =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["scheduling"] = new[] { "greeting_name", "unavailable_date", "proposed_date", "signoff_name" },
            ["billing"] = new[] { "invoice_id", "amount", "due_date" },
        };

    private static readonly Regex SlotRegex = new(@"\{([a-z_]+)\}", RegexOptions.Compiled);

    public static async Task<int> RunAsync(EvalConfig config, string apiKey, DateTimeOffset startedAt)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine("U3_TEMPLATE_PILOT: missing model api key (DEEPSEEK_API_KEY/OPENAI_API_KEY).");
            return 2;
        }

        var gptzeroKey = Environment.GetEnvironmentVariable("GPTZero_API_KEY")
            ?? Environment.GetEnvironmentVariable("GPTZERO_API_KEY");
        if (string.IsNullOrWhiteSpace(gptzeroKey))
        {
            Console.Error.WriteLine("U3_TEMPLATE_PILOT: missing GPTZero_API_KEY.");
            return 2;
        }

        var maxGpt = IntEnv("GPTZERO_MAX_CALLS", 6);
        var gptCalls = 0;

        using var http = new HttpClient();
        using var judgeHttp = new HttpClient();
        using var gptHttp = new HttpClient();
        var deepseek = new DeepSeekChatClient(http, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(60));
        var judge = new SemanticEvalJudge(judgeHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));

        Console.WriteLine($"U3 template pilot: cases={SimplePilotCases.All.Count} gptzeroMax={maxGpt} model={config.Model}");
        var report = new StringBuilder();
        report.AppendLine("# U3 deterministic-template pilot (last-shot AI-detection test)");
        report.AppendLine();
        report.AppendLine("**Eval-only.** Final English = hardcoded human-written template + DeepSeek-extracted fact slots. "
            + "No LLM English authoring, no translation. GPTZero only on semantic-gate survivors. "
            + "Lower GPTZero than the draft = a real detection drop.");
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

            // 2. DeepSeek slot extraction (verbatim copy of identifiers/amounts/dates/names).
            var slotsJson = await deepseek.CompleteAsync(
                SlotSystemPrompt(c.EmailType), c.Draft, 600, 0.1, CancellationToken.None);
            var slots = ParseSlots(slotsJson);
            Console.WriteLine($"  slots: {string.Join(", ", slots.Select(kv => $"{kv.Key}={kv.Value}"))}");

            // 3 + 4. Pick template, render, require all slots.
            var template = Templates.TryGetValue(c.EmailType, out var variants) && variants.Length > 0
                ? variants[0]
                : null;
            var required = RequiredSlots.TryGetValue(c.EmailType, out var req) ? req : Array.Empty<string>();
            var missing = required.Where(s => !slots.TryGetValue(s, out var v) || string.IsNullOrWhiteSpace(v)).ToArray();

            if (template is null || missing.Length > 0)
            {
                var reason = template is null ? "no_template_for_type" : $"missing_slots:{string.Join("|", missing)}";
                Console.WriteLine($"  U3: SKIP ({reason}) — fact gate not reached.");
                report.AppendLine($"- draft GPTZero: **{Fmt(draftScore)}** {Cls(draftCls)}");
                report.AppendLine($"- U3 render: **skipped** ({reason})");
                report.AppendLine();
                continue;
            }

            var rendered = SlotRegex.Replace(template, m =>
                slots.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value).Trim();
            Console.WriteLine($"  U3 rendered ({rendered.Length} chars):\n    {rendered.Replace("\n", "\n    ", StringComparison.Ordinal)}");

            // 5. Semantic gate (facts + forbidden + meaning). Facts must hold before detection counts.
            var verdict = await judge.VerifyAsync(rendered, c.MustKeep, c.MustNotClaim, CancellationToken.None);
            var drifts = verdict.Facts
                .Where(f => f.Status is "missing" or "contradicted")
                .Select(f => $"{f.Status}:{f.Fact}")
                .ToList();
            var forbidden = verdict.Forbidden.Where(f => f.Violated).Select(f => f.Rule).ToList();
            var semPass = verdict.Error is null && verdict.FactsReallyPass && verdict.RealForbidden == 0 && !verdict.MeaningChanged;
            Console.WriteLine($"  semantic: {(semPass ? "PASS" : "FAIL")}"
                + (verdict.Error is not null ? $" (judge_error:{verdict.Error})" : string.Empty)
                + (drifts.Count > 0 ? $" drifts=[{string.Join("; ", drifts)}]" : string.Empty)
                + (forbidden.Count > 0 ? $" forbidden=[{string.Join("; ", forbidden)}]" : string.Empty)
                + (verdict.MeaningChanged ? " meaning_changed" : string.Empty));

            // 6. GPTZero on the render only when the gate passes and budget remains.
            int? u3Score = null;
            string? u3Cls = null;
            string? u3Err = null;
            if (semPass && gptCalls < maxGpt)
            {
                (u3Score, u3Cls, u3Err) = await GptzeroScorer.ScoreAsync(gptHttp, gptzeroKey!, rendered, CancellationToken.None);
                gptCalls++;
                Console.WriteLine($"  U3 GPTZero: {(u3Err is null ? $"{u3Score}% AI [{u3Cls}]" : "error: " + u3Err)}");
            }
            else if (!semPass)
            {
                Console.WriteLine("  U3 GPTZero: skipped (semantic gate failed).");
            }

            var delta = draftScore.HasValue && u3Score.HasValue ? u3Score.Value - draftScore.Value : (int?)null;
            report.AppendLine($"- draft GPTZero: **{Fmt(draftScore)}** {Cls(draftCls)}");
            report.AppendLine($"- semantic gate: **{(semPass ? "PASS" : "FAIL")}**"
                + (drifts.Count > 0 ? $" — drifts: {string.Join("; ", drifts)}" : string.Empty)
                + (forbidden.Count > 0 ? $" — forbidden: {string.Join("; ", forbidden)}" : string.Empty)
                + (verdict.MeaningChanged ? " — meaning changed" : string.Empty));
            report.AppendLine($"- U3 GPTZero: **{(semPass ? Fmt(u3Score) : "n/a (gate failed)")}** {Cls(u3Cls)}"
                + (delta.HasValue ? $"  ·  Δ vs draft: **{Signed(delta)}**" : string.Empty));
            report.AppendLine();
            report.AppendLine("**U3 render:**");
            report.AppendLine();
            report.AppendLine("> " + rendered.Replace("\n", "\n> ", StringComparison.Ordinal));
            report.AppendLine();
        }

        var verdictLine = $"Budget used: {gptCalls}/{maxGpt} GPTZero calls.";
        Console.WriteLine($"\n=== {verdictLine} ===");
        report.AppendLine("## Budget");
        report.AppendLine();
        report.AppendLine(verdictLine);

        Directory.CreateDirectory(config.OutputDirectory);
        var stamp = startedAt.ToString("yyyyMMdd-HHmmss");
        var mdPath = Path.Combine(config.OutputDirectory, $"{stamp}-u3-template-pilot.md");
        await File.WriteAllTextAsync(mdPath, report.ToString());
        Console.WriteLine($"Wrote {mdPath}");
        return 0;
    }

    private static string SlotSystemPrompt(string emailType) =>
        emailType switch
        {
            "scheduling" =>
                "You extract slots from an email draft so a fixed template can rebuild it. Return JSON only: "
                + "{\"email_type\":\"scheduling\",\"slots\":{\"greeting_name\":\"\",\"unavailable_date\":\"\","
                + "\"proposed_date\":\"\",\"signoff_name\":\"\"}}. Copy each value VERBATIM from the draft — do not "
                + "paraphrase, normalize, or invent. greeting_name = who it is addressed to; unavailable_date = the "
                + "day/time the sender cannot attend; proposed_date = the day/time the sender proposes instead; "
                + "signoff_name = the sender's name in the sign-off. If a value is absent, use an empty string.",
            "billing" =>
                "You extract slots from an email draft so a fixed template can rebuild it. Return JSON only: "
                + "{\"email_type\":\"billing\",\"slots\":{\"invoice_id\":\"\",\"amount\":\"\",\"due_date\":\"\"}}. "
                + "Copy each value VERBATIM from the draft — do not paraphrase, normalize, or invent. invoice_id = the "
                + "invoice number/identifier; amount = the money amount exactly as written incl. symbol; due_date = the "
                + "due date exactly as written. If a value is absent, use an empty string.",
            _ =>
                "Extract slots from the email. Return JSON only as {\"email_type\":\"other\",\"slots\":{}}.",
        };

    private static Dictionary<string, string> ParseSlots(string? json)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("slots", out var slots) && slots.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in slots.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        result[prop.Name] = prop.Value.GetString()?.Trim() ?? string.Empty;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // leave empty -> required-slot check forces a skip
        }

        return result;
    }

    private static int IntEnv(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var v) && v > 0 ? v : fallback;

    private static string Fmt(int? v) => v is null ? "-" : $"{v}% AI";

    private static string Cls(string? cls) => string.IsNullOrEmpty(cls) ? string.Empty : $"[{cls}]";

    private static string Signed(int? v) => v is null ? "-" : v.Value > 0 ? $"+{v.Value}" : v.Value.ToString();
}
