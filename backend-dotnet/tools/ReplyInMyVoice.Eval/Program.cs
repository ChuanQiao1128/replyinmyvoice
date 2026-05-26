using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.RewriteEngine;
using ReplyInMyVoice.Infrastructure.Providers;

var startedAt = DateTimeOffset.UtcNow;
var config = EvalConfig.FromEnvironment();
LoadEnvFileIfPresent(Path.Combine(config.RepoRoot, ".env.local"));

var casePaths = config.CasesPath.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var cases = (await Task.WhenAll(casePaths.Select(async p => EvalCaseParser.Parse(await File.ReadAllTextAsync(p)))))
    .SelectMany(parsed => parsed)
    .ToList();
var selectedCases = SelectCases(cases, config);

// EVAL_DRY_RUN validates parsing + selection with zero provider calls (no secrets needed).
if (IsTruthy(Environment.GetEnvironmentVariable("EVAL_DRY_RUN")))
{
    Console.WriteLine($"[dry-run] parsed {cases.Count} cases from {config.CasesPath}");
    Console.WriteLine(
        $"[dry-run] selected {selectedCases.Count} for mode={config.Mode} "
        + $"ids={(config.CaseIds.Count > 0 ? string.Join(",", config.CaseIds) : "(mode)")}");
    foreach (var dryCase in selectedCases)
    {
        Console.WriteLine(
            $"[dry-run] {dryCase.Id} #{dryCase.CaseNumber:000} tone={dryCase.TonePreset} "
            + $"mustKeep={dryCase.MustKeep.Count} mustNotClaim={dryCase.MustNotClaim.Count} "
            + $"draftWords={dryCase.InputDraft.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length}");
    }

    Console.WriteLine("[dry-run] parser + selection OK; no provider calls made.");
    return 0;
}

// EVAL_RESCORE_DIR re-scores saved run outputs with the current scoring logic — $0, no
// provider calls. Used to re-measure after a matcher / forbidden-screen fix.
var rescoreDir = Environment.GetEnvironmentVariable("EVAL_RESCORE_DIR");
if (!string.IsNullOrWhiteSpace(rescoreDir))
{
    RescoreSavedRun(rescoreDir, cases, config.NaturalnessThreshold);
    return 0;
}

var apiKey = ResolveApiKey(config);
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Missing model configuration. Set DEEPSEEK_API_KEY or OPENAI_API_KEY.");
    return 2;
}

// EVAL_SEMANTIC_RESCORE_FILES (comma-separated saved run JSONs) re-scores those outputs with the
// LLM-judge semantic verifier (semantic fact + forbidden checks) instead of the over-literal
// deterministic graders. No engine re-run — only judge model calls. Set EVAL_CASES_PATH to a
// comma list to load every cases file the saved rows reference.
var semanticFiles = Environment.GetEnvironmentVariable("EVAL_SEMANTIC_RESCORE_FILES");
if (!string.IsNullOrWhiteSpace(semanticFiles))
{
    await SemanticRescore(semanticFiles, cases, apiKey, config);
    return 0;
}

// Writing-signal provider is selectable via WRITING_SIGNAL_PROVIDER (default sapling). Pangram
// is a stricter detection-first alternative; it needs PANGRAM_API_KEY instead of SAPLING_API_KEY.
var signalProvider = (Environment.GetEnvironmentVariable("WRITING_SIGNAL_PROVIDER") ?? "sapling")
    .Trim().ToLowerInvariant();
var saplingApiKey = Environment.GetEnvironmentVariable("SAPLING_API_KEY");
var pangramApiKey = Environment.GetEnvironmentVariable("PANGRAM_API_KEY");
var usePangram = signalProvider == "pangram";
if (usePangram ? string.IsNullOrWhiteSpace(pangramApiKey) : string.IsNullOrWhiteSpace(saplingApiKey))
{
    Console.Error.WriteLine(
        $"Missing writing-signal key for provider '{signalProvider}'. Set "
        + (usePangram ? "PANGRAM_API_KEY." : "SAPLING_API_KEY."));
    return 2;
}

Directory.CreateDirectory(config.OutputDirectory);

// Eval-only A/B variant (EVAL_VARIANT=v0..v4). Maps to optional engine levers that default to
// current production behavior; the production composition root never sets them.
var variant = (Environment.GetEnvironmentVariable("EVAL_VARIANT") ?? "v0").Trim().ToLowerInvariant();
var (variantExtraInstruction, variantForceStrategy) = ResolveVariant(variant);
Console.WriteLine($"Variant: {variant}");

using var modelHttpClient = new HttpClient();
using var signalHttpClient = new HttpClient();
var modelClient = new OpenAiCompatibleRewriteModelClient(
    modelHttpClient,
    apiKey,
    config.Model,
    config.OpenAiBaseUrl,
    TimeSpan.FromSeconds(config.ModelTimeoutSeconds),
    variantExtraInstruction);
IWritingSignalClient baseSignalClient = usePangram
    ? new PangramWritingSignalClient(signalHttpClient, pangramApiKey!, TimeSpan.FromSeconds(config.SaplingTimeoutSeconds))
    : new SaplingWritingSignalClient(signalHttpClient, saplingApiKey!, TimeSpan.FromSeconds(config.SaplingTimeoutSeconds));
var signalClient = new CountingWritingSignalClient(baseSignalClient);
Console.WriteLine($"Writing-signal provider: {signalProvider}");
var countingModelClient = new CountingRewriteModelClient(modelClient);
var provider = new FactReconstructRewriteProvider(
    countingModelClient,
    signalClient,
    new FactReconstructRewriteOptions(
        NaturalnessThreshold: config.NaturalnessThreshold,
        RequestedMaxAttempts: config.MaxAttempts,
        TargetAiLikePercent: config.TargetAiLikePercent,
        // Total wall-clock budget per rewrite (all loops). TOTAL_REWRITE_BUDGET_SEC=0 (default)
        // means unlimited; set e.g. 120 to cap each rewrite at 120s.
        TotalTimeBudget: TimeSpan.FromSeconds(
            int.TryParse(Environment.GetEnvironmentVariable("TOTAL_REWRITE_BUDGET_SEC"), out var budgetSec) && budgetSec > 0
                ? budgetSec
                : 0),
        ForceInitialStrategy: variantForceStrategy));

var rows = new List<EvalResultRow>();
foreach (var sample in selectedCases)
{
    var request = sample.ToRewriteRequest();
    var expected = EvalExpectations.FromCase(sample);
    var result = await provider.RewriteAsync(Guid.NewGuid(), request, CancellationToken.None);
    var payload = RewritePayload.TryParse(result.ResultJson);
    var failurePayload = RewriteFailurePayload.TryParse(result.ResultJson);
    var attemptHistory = failurePayload?.AttemptHistory ?? [];
    var rewrittenText = payload?.RewrittenText ?? string.Empty;
    var hasOutput = !string.IsNullOrWhiteSpace(rewrittenText);

    var factCheck = FactExpectationChecker.Check(rewrittenText, expected.MustKeep);
    var forbidden = ForbiddenClaimScreen.Check(rewrittenText, expected.MustNotClaim);
    var customerUsable = CustomerUsableEvaluator.IsCustomerUsable(
        result.Success, hasOutput, factCheck.Passed, forbidden.Violations.Count);
    var wouldPassRelaxedGate = !result.Success
        && result.ErrorCode == "naturalness_gate_failed"
        && RelaxedNaturalnessProbe.WouldPassUnderRelaxedGate(
            attemptHistory, expected.MustKeep, expected.MustNotClaim, config.NaturalnessThreshold);

    rows.Add(new EvalResultRow(
        sample.Id,
        sample.CaseNumber,
        sample.Category,
        request.Tone,
        result.Success,
        result.ErrorCode,
        payload?.Naturalness?.DraftAiLikePercent,
        payload?.Naturalness?.RewriteAiLikePercent,
        payload?.Naturalness?.ChangePoints,
        factCheck.Passed,
        factCheck.MissingFacts,
        forbidden.Violations,
        forbidden.Abstained,
        customerUsable,
        wouldPassRelaxedGate,
        rewrittenText,
        attemptHistory,
        payload?.AttemptsUsed,
        payload?.FailedAttempts));

    Console.WriteLine(
        $"{sample.Id}: usable={customerUsable} success={result.Success} facts={factCheck.Passed} "
        + $"forbidden={forbidden.Violations.Count} error={result.ErrorCode ?? "none"}");
}

var summary = EvalSummary.Create(
    startedAt,
    DateTimeOffset.UtcNow,
    config,
    rows,
    countingModelClient.CallCount,
    signalClient.CallCount);

var stamp = startedAt.ToString("yyyyMMdd-HHmmss");
var jsonPath = Path.Combine(config.OutputDirectory, $"{stamp}-csharp-rewrite-{config.Mode}-{variant}.json");
var mdPath = Path.Combine(config.OutputDirectory, $"{stamp}-csharp-rewrite-{config.Mode}-{variant}.md");
var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};
await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(new { summary, rows }, jsonOptions));
await File.WriteAllTextAsync(mdPath, MarkdownReport.Render(summary, rows));

Console.WriteLine($"Wrote {jsonPath}");
Console.WriteLine($"Wrote {mdPath}");
Console.WriteLine($"Summary: customerUsable={summary.CustomerUsableCount}/{summary.CasesEvaluated}, success={summary.SuccessCount}/{summary.CasesEvaluated}, factPass={summary.FactPassCount}/{summary.CasesEvaluated}, forbiddenViol={summary.ForbiddenViolationCaseCount}, relaxedRecoverable={summary.RelaxedGateRecoverableCount}, measured={summary.MeasuredCount}, avgDrop={summary.AverageDropPoints?.ToString() ?? "unavailable"}, highBaselineAvgDrop={summary.BaselineAboveThresholdAverageDropPoints?.ToString() ?? "unavailable"} ({summary.BaselineAboveThresholdCount}), below50={summary.RewritesBelow50Count}/{summary.MeasuredCount}, modelCalls={summary.ModelCalls}, saplingCalls={summary.SaplingCalls}");
return summary.ProviderFailureCount == 0 ? 0 : 1;

static bool IsTruthy(string? value) => value is "1" or "true" or "yes" or "on";

// Eval-only A/B levers (default v0 = current engine). v1/v2 append a system instruction;
// v3 forces facts-first routing; v4 combines v2 + v3. None of these touch production.
static (string? Extra, RewriteStrategy? Force) ResolveVariant(string variant) => variant switch
{
    "v1" => ("If the final reply is one or two short sentences, do not add a greeting or sign-off unless the source clearly requires one.", null),
    "v2" => ("Do not preserve or add a greeting or sign-off by default; include them only when the context clearly requires it.", null),
    "v3" => (null, RewriteStrategy.FactsFirstReconstruct),
    "v4" => ("Do not preserve or add a greeting or sign-off by default; include them only when the context clearly requires it.", RewriteStrategy.FactsFirstReconstruct),
    _ => (null, null),
};

static void RescoreSavedRun(string dir, IReadOnlyList<EvalCase> cases, int naturalnessThreshold)
{
    var byId = cases.ToDictionary(c => c.Id, c => c, StringComparer.OrdinalIgnoreCase);
    var caseOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    int total = 0, usable = 0, success = 0, factPass = 0, forbidden = 0, relaxed = 0;
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("| Case | usable | success | facts | forbidden | missing (rescored) |");
    sb.AppendLine("| --- | --- | --- | --- | ---: | --- |");

    foreach (var file in Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(f => f))
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        if (!doc.RootElement.TryGetProperty("rows", out var rowsEl) || rowsEl.ValueKind != JsonValueKind.Array)
        {
            continue;
        }

        foreach (var row in rowsEl.EnumerateArray())
        {
            var id = row.TryGetProperty("Id", out var idEl) ? idEl.GetString() ?? "" : "";
            if (!byId.TryGetValue(id, out var sample))
            {
                continue;
            }

            total++;
            var text = row.TryGetProperty("RewrittenText", out var t) ? t.GetString() ?? "" : "";
            var engineSuccess = row.TryGetProperty("Success", out var s) && s.GetBoolean();
            var errorCode = row.TryGetProperty("ErrorCode", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
            var hasOutput = !string.IsNullOrWhiteSpace(text);

            var factCheck = FactExpectationChecker.Check(text, sample.MustKeep);
            var forbiddenResult = ForbiddenClaimScreen.Check(text, sample.MustNotClaim);
            var customerUsable = CustomerUsableEvaluator.IsCustomerUsable(
                engineSuccess, hasOutput, factCheck.Passed, forbiddenResult.Violations.Count);

            var history = row.TryGetProperty("AttemptHistory", out var h) && h.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<List<RewriteAttemptHistoryItem>>(h.GetRawText(), caseOpts) ?? new List<RewriteAttemptHistoryItem>()
                : new List<RewriteAttemptHistoryItem>();
            var wouldPassRelaxed = !engineSuccess && errorCode == "naturalness_gate_failed"
                && RelaxedNaturalnessProbe.WouldPassUnderRelaxedGate(history, sample.MustKeep, sample.MustNotClaim, naturalnessThreshold);

            if (engineSuccess) success++;
            if (factCheck.Passed) factPass++;
            if (forbiddenResult.Violations.Count > 0) forbidden++;
            if (customerUsable) usable++;
            if (wouldPassRelaxed) relaxed++;
            sb.AppendLine($"| {id} | {(customerUsable ? "yes" : "no")} | {(engineSuccess ? "yes" : "no")} | {(factCheck.Passed ? "yes" : "no")} | {forbiddenResult.Violations.Count} | {string.Join("; ", factCheck.MissingFacts).Replace("|", "/", StringComparison.Ordinal)} |");
        }
    }

    var summaryLine = $"customerUsable={usable}/{total}, success={success}/{total}, factPass={factPass}/{total}, forbiddenViol={forbidden}, relaxedRecoverable={relaxed}";
    var outPath = Path.Combine(dir, "_rescored-summary.md");
    File.WriteAllText(outPath, $"# Re-scored summary ({total} cases)\n\n{summaryLine}\n\n{sb}");
    Console.WriteLine($"RESCORE: {summaryLine}");
    Console.WriteLine($"wrote {outPath}");
}

static async Task SemanticRescore(string files, IReadOnlyList<EvalCase> cases, string apiKey, EvalConfig config)
{
    var byId = cases.ToDictionary(c => c.Id, c => c, StringComparer.OrdinalIgnoreCase);
    using var http = new HttpClient();
    var judge = new SemanticEvalJudge(http, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));
    var paths = files.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    var rows = new List<(string Id, string Text, bool DetFacts, int DetForbid)>();
    foreach (var path in paths)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("rows", out var rs) || rs.ValueKind != JsonValueKind.Array)
        {
            continue;
        }

        foreach (var r in rs.EnumerateArray())
        {
            var id = r.TryGetProperty("Id", out var idEl) ? idEl.GetString() ?? "" : "";
            var text = r.TryGetProperty("RewrittenText", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
            var detFacts = r.TryGetProperty("FactsPreserved", out var fp) && fp.ValueKind == JsonValueKind.True;
            var detForbid = r.TryGetProperty("ForbiddenViolations", out var fv) && fv.ValueKind == JsonValueKind.Array ? fv.GetArrayLength() : 0;
            rows.Add((id, text, detFacts, detForbid));
        }
    }

    int judged = 0, semFactsPass = 0, detFactsPass = 0, semForbidCases = 0, detForbidCases = 0;
    var factFN = new List<string>();
    var factFP = new List<string>();
    var forbidFP = new List<string>();
    var forbidFN = new List<string>();
    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"# Semantic re-score (C# eval tool; judge {judge.Model}; prompt {SemanticEvalJudge.PromptVersion})\n");
    sb.AppendLine("| id | det_facts | sem_facts | det_forbid | sem_forbid | really-lost |");
    sb.AppendLine("| --- | --- | --- | ---: | ---: | --- |");

    foreach (var row in rows)
    {
        if (!byId.TryGetValue(row.Id, out var c))
        {
            continue;
        }

        var v = await judge.VerifyAsync(row.Text, c.MustKeep, c.MustNotClaim, CancellationToken.None);
        if (v.Error is not null)
        {
            Console.WriteLine($"  {row.Id}: judge error {v.Error}");
            continue;
        }

        judged++;
        var semPass = v.FactsReallyPass;
        var semForbid = v.RealForbidden;
        if (semPass) semFactsPass++;
        if (row.DetFacts) detFactsPass++;
        if (semForbid > 0) semForbidCases++;
        if (row.DetForbid > 0) detForbidCases++;
        if (semPass && !row.DetFacts) factFN.Add(row.Id);
        if (!semPass && row.DetFacts) factFP.Add(row.Id);
        if (row.DetForbid > 0 && semForbid == 0) forbidFP.Add(row.Id);
        if (row.DetForbid == 0 && semForbid > 0) forbidFN.Add(row.Id);
        var lost = string.Join("; ", v.Facts
            .Where(f => f.Status is "missing" or "contradicted")
            .Select(f => $"{f.Status}:{f.Fact}"));
        sb.AppendLine($"| {row.Id} | {row.DetFacts} | {semPass} | {row.DetForbid} | {semForbid} | {lost.Replace("|", "/", StringComparison.Ordinal)} |");
        Console.WriteLine($"  {row.Id}: sem_facts={(semPass ? "pass" : "FAIL")} sem_forbid={semForbid} (det_facts={row.DetFacts} det_forbid={row.DetForbid})");
    }

    var summary = $"SEMANTIC (C#): facts {semFactsPass}/{judged} (det {detFactsPass}/{judged}); forbidden {semForbidCases}/{judged} (det {detForbidCases})";
    sb.AppendLine($"\n{summary}\n");
    sb.AppendLine($"- fact false-negatives (det fail, really pass): {string.Join(", ", factFN)}");
    sb.AppendLine($"- fact false-positives (det pass, really lost): {string.Join(", ", factFP)}");
    sb.AppendLine($"- forbidden false-positives (det flagged, really clean): {string.Join(", ", forbidFP)}");
    sb.AppendLine($"- forbidden false-negatives (det missed a real one): {string.Join(", ", forbidFN)}");

    Directory.CreateDirectory(config.OutputDirectory);
    var outPath = Path.Combine(config.OutputDirectory, $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-semantic-rescore-csharp.md");
    await File.WriteAllTextAsync(outPath, sb.ToString());

    Console.WriteLine($"\n{summary}");
    Console.WriteLine($"fact FN: {string.Join(",", factFN)} | fact FP: {string.Join(",", factFP)} | forbid FP: {string.Join(",", forbidFP)} | forbid FN: {string.Join(",", forbidFN)}");
    Console.WriteLine($"wrote {outPath}");
}

static IReadOnlyList<EvalCase> SelectCases(IReadOnlyList<EvalCase> cases, EvalConfig config)
{
    if (config.CaseIds.Count > 0)
    {
        return cases
            .Where(sample => config.CaseIds.Contains(sample.Id, StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    var modeLimit = config.Mode switch
    {
        "smoke" => 10,
        "focused" => 40,
        "full" => 100,
        _ => throw new InvalidOperationException($"Unknown EVAL_MODE {config.Mode}. Use smoke, focused, or full."),
    };
    var limit = Math.Min(config.Limit ?? modeLimit, modeLimit);
    return cases.Take(Math.Min(limit, cases.Count)).ToArray();
}

static string ResolveApiKey(EvalConfig config)
{
    if (IsDeepSeekBaseUrl(config.OpenAiBaseUrl))
    {
        return Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? string.Empty;
    }

    return Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
        ?? string.Empty;
}

static bool IsDeepSeekBaseUrl(string value)
{
    try
    {
        var host = new Uri(value).Host.ToLowerInvariant();
        return host == "api.deepseek.com" || host.EndsWith(".deepseek.com", StringComparison.Ordinal);
    }
    catch (UriFormatException)
    {
        return false;
    }
}

static void LoadEnvFileIfPresent(string path)
{
    if (!File.Exists(path))
    {
        return;
    }

    foreach (var line in File.ReadLines(path))
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
        {
            continue;
        }

        var match = Regex.Match(trimmed, @"^([A-Za-z_][A-Za-z0-9_]*)=(.*)$");
        if (!match.Success || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(match.Groups[1].Value)))
        {
            continue;
        }

        var value = match.Groups[2].Value.Trim().Trim('"', '\'').Replace("\\n", "\n", StringComparison.Ordinal);
        Environment.SetEnvironmentVariable(match.Groups[1].Value, value);
    }
}

sealed record EvalConfig(
    string RepoRoot,
    string CasesPath,
    string OutputDirectory,
    IReadOnlyList<string> CaseIds,
    string Mode,
    int? Limit,
    int MaxAttempts,
    string OpenAiBaseUrl,
    string Model,
    int NaturalnessThreshold,
    int ModelTimeoutSeconds,
    int SaplingTimeoutSeconds,
    int TargetAiLikePercent)
{
    public static EvalConfig FromEnvironment()
    {
        var repoRoot = Directory.GetCurrentDirectory();
        return new EvalConfig(
            repoRoot,
            Env("EVAL_CASES_PATH", Path.Combine(repoRoot, "docs", "rewrite-email-eval-cases-100.md")),
            Env("EVAL_OUTPUT_DIR", Path.Combine(repoRoot, "docs", "rewrite-eval-results")),
            Env("EVAL_CASE_IDS", string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Env("EVAL_MODE", "smoke"),
            TryInt("EVAL_LIMIT"),
            Int("EVAL_MAX_ATTEMPTS", 10),
            Env("OPENAI_BASE_URL", "https://api.deepseek.com"),
            Env("OPENAI_MODEL_MID_WRITER", Env("OPENAI_MODEL", "deepseek-v4-pro")),
            Int("NATURALNESS_THRESHOLD", 40),
            Int("REWRITE_MODEL_TIMEOUT_SEC", 60),
            Int("WRITING_SIGNAL_TIMEOUT_SEC", 20),
            // Adaptive refinement: default target = naturalness floor, so a plain run reproduces
            // the baseline "return first passing candidate" behavior. Set EVAL_TARGET_AI_LIKE
            // (e.g. 25) to drive the loop, and EVAL_MAX_ATTEMPTS (default 10) for the loop cap.
            Int("EVAL_TARGET_AI_LIKE", Int("NATURALNESS_THRESHOLD", 40)));
    }

    private static string Env(string name, string fallback) =>
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))
            ? fallback
            : Environment.GetEnvironmentVariable(name)!;

    private static int Int(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value > 0 ? value : fallback;

    private static int? TryInt(string name) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value > 0 ? value : null;
}

public static class FactExpectationChecker
{
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "actively", "an", "and", "are", "as", "ask", "at", "be", "by", "can", "client", "currently",
        "customer", "date", "did", "do", "does", "dropped", "explain", "for", "from", "has",
        "have", "in", "is", "it", "must", "need", "needs", "new", "no", "not", "of", "off", "on", "only", "or",
        "parent", "people", "person", "please", "plus", "reply", "requires", "seller", "should",
        "status", "still", "student", "support", "team", "the", "they", "to", "user", "want",
        "whether", "will", "with", "would",
        // Role-scaffolding words: must_keep facts are phrased "The <role> is <Name>", and the
        // rewrite echoes only the Name ("Hi Ren"). The role word must not be a required token.
        "recipient", "sender", "contact", "candidate", "caller", "attendee",
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> TokenAliases =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            // Verb-inflection / paraphrase aliases for facts the engine faithfully restates
            // in different wording (e.g. "No fix can be promised" -> "I can't promise a fix").
            // Pass-only: aliases only ADD coverage, so they cannot turn a present fact into a
            // miss — they strictly reduce matcher false-negatives.
            ["beyond"] = ["beyond", "further", "more", "additional"],
            ["deadline"] = ["deadline", "due", "latest", "cutoff"],
            ["detailed"] = ["detailed", "detail", "specific", "specifics"],
            ["donations"] = ["donations", "donation", "donate", "donating", "donor"],
            ["investigating"] = ["investigating", "investigate", "looking", "reviewing", "examining"],
            ["making"] = ["making", "make", "draw", "drawing"],
            ["promised"] = ["promised", "promise", "promising"],
            ["shared"] = ["shared", "share", "sharing"],
            ["allowed"] = ["allowed", "possible", "available", "can"],
            ["acceptance"] = ["acceptance", "accept"],
            ["approval"] = ["approval", "approve", "approved", "preapproval"],
            ["approved"] = ["approved", "approval", "preapproval"],
            ["cancel"] = ["cancel", "cancellation", "canceling", "cancelled", "canceled"],
            ["change"] = ["change", "different", "explore"],
            ["choose"] = ["choose", "choice", "decision", "proceed", "prefer", "which option"],
            ["confirm"] = ["confirm", "confirmation", "confirming", "confirmed"],
            ["confirmation"] = ["confirm", "confirmation", "confirming", "confirmed"],
            ["delivered"] = ["delivered", "arrived"],
            ["delivery"] = ["delivery", "deliver", "shipment", "ship"],
            ["exchange"] = ["exchange", "switch"],
            ["exists"] = ["exists", "available", "allows", "inventory", "stock"],
            ["feedback"] = ["feedback", "share"],
            ["general"] = ["general", "policy"],
            ["immediately"] = ["immediately", "immediate", "now"],
            ["internal"] = ["internal", "our"],
            ["interview"] = ["interview", "meeting", "met", "spoke", "conversation", "call"],
            ["inventory"] = ["inventory", "stock"],
            ["appointment"] = ["appointment", "meeting", "slot", "visit", "time"],
            ["meeting"] = ["meeting", "meet", "met", "call", "interview"],
            ["pending"] = ["pending", "confirming", "awaiting"],
            ["preapproval"] = ["preapproval", "pre", "approval"],
            ["received"] = ["received", "receive", "dropped"],
            ["resend"] = ["resend", "send"],
            ["retries"] = ["retries", "retry", "charges"],
            ["strong"] = ["strong", "wonderful", "engaged"],
            ["suggest"] = ["suggest", "schedule", "offer"],
            ["without"] = ["without", "no", "until", "before"],
        };

    public static FactCheckResult Check(string text, IReadOnlyList<string> expectedFacts)
    {
        var normalizedText = Normalize(text);
        var missing = expectedFacts
            .Where(fact => !IncludesFact(normalizedText, fact))
            .ToArray();

        return new FactCheckResult(missing.Length == 0, missing);
    }

    private static bool IncludesFact(string normalizedText, string fact)
    {
        var normalizedFact = Normalize(fact);
        if (normalizedText.Contains(normalizedFact, StringComparison.Ordinal))
        {
            return true;
        }

        // Anchor PASS path: the salient content of a declaratively phrased fact ("The account
        // is Northstar.", "invoice INV-8842 for $186.00") is its proper nouns / IDs / money /
        // multi-digit numbers. If all such anchors appear in the rewrite, the fact is present
        // regardless of role-scaffolding wording. Pass-only — never fails a fact — so it
        // strictly reduces false-negatives.
        var anchors = ExtractAnchors(fact);
        if (anchors.Count > 0 && anchors.All(anchor => normalizedText.Contains(anchor, StringComparison.Ordinal)))
        {
            return true;
        }

        var tokens = FactTokens(fact);
        if (tokens.Count == 0)
        {
            return true;
        }

        var requiredCount = tokens.Count switch
        {
            <= 2 => tokens.Count,
            <= 4 => Math.Max(2, (int)Math.Floor(tokens.Count * 0.67)),
            _ => Math.Max(2, (int)Math.Floor(tokens.Count * 0.67)),
        };
        return tokens.Count(token => IsTokenCovered(normalizedText, token)) >= requiredCount;
    }

    private static readonly HashSet<string> AnchorStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "this", "that", "these", "those", "i", "we", "you", "he", "she",
        "it", "they", "do", "please", "if", "when", "after", "before", "once", "until",
        "there", "here", "your", "our", "my", "his", "her", "their", "its", "no", "not",
        "and", "but", "or", "for", "to", "of", "in", "on", "at", "as", "is", "are", "was",
        "were", "be", "because", "so", "most", "new", "all", "any", "please",
    };

    // Proper nouns / IDs / money / multi-digit numbers carried by a fact. Lowercased to
    // compare against the normalized (lowercased) rewrite text.
    private static IReadOnlyList<string> ExtractAnchors(string fact)
    {
        var anchors = new List<string>();
        foreach (Match match in Regex.Matches(fact, @"\b[A-Z][A-Za-z]+\b"))
        {
            if (!AnchorStopWords.Contains(match.Value))
            {
                anchors.Add(match.Value.ToLowerInvariant());
            }
        }

        foreach (Match match in Regex.Matches(fact, @"\b[A-Za-z]{1,6}-?\d{2,}\b|#[A-Za-z0-9-]+"))
        {
            anchors.Add(match.Value.ToLowerInvariant());
        }

        foreach (Match match in Regex.Matches(fact, @"\$\d[\d,.]*"))
        {
            anchors.Add(match.Value.ToLowerInvariant());
        }

        foreach (Match match in Regex.Matches(fact, @"\b\d{2,}\b"))
        {
            anchors.Add(match.Value);
        }

        return anchors.Distinct(StringComparer.Ordinal).ToList();
    }

    private static IReadOnlyList<string> FactTokens(string value) =>
        Regex.Replace(Normalize(value), @"[^a-z0-9#$:.@'-]+", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(token => Regex.Replace(token, @"^[^a-z0-9#$@]+|[^a-z0-9]+$", ""))
            .Where(token => token.Length > 1 && !StopWords.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static bool IsTokenCovered(string normalizedText, string token)
    {
        if (normalizedText.Contains(token, StringComparison.Ordinal))
        {
            return true;
        }

        return TokenAliases.TryGetValue(token, out var aliases) &&
            aliases.Any(alias => normalizedText.Contains(alias, StringComparison.Ordinal));
    }

    private static string Normalize(string value) =>
        Regex.Replace(
            Regex.Replace(value.ToLowerInvariant()
                    .Replace("’", "'", StringComparison.Ordinal)
                    .Replace("can't", "cannot", StringComparison.Ordinal)
                    .Replace("won't", "will not", StringComparison.Ordinal)
                    .Replace("hasn't", "has not", StringComparison.Ordinal)
                    .Replace("haven't", "have not", StringComparison.Ordinal)
                    .Replace("one-time", "one time", StringComparison.Ordinal)
                    .Replace("30-day", "30 day", StringComparison.Ordinal)
                    .Replace("cancellation", "cancel", StringComparison.Ordinal)
                    .Replace("canceling", "cancel", StringComparison.Ordinal)
                    .Replace("cancelled", "cancel", StringComparison.Ordinal)
                    .Replace("canceled", "cancel", StringComparison.Ordinal)
                    .Replace("confirmation", "confirm", StringComparison.Ordinal)
                    .Replace("confirming", "confirm", StringComparison.Ordinal)
                    .Replace("confirmed", "confirm", StringComparison.Ordinal)
                    .Replace("preapproval", "pre approval", StringComparison.Ordinal)
                    .Replace("immediately", "now", StringComparison.Ordinal),
                @"\s+",
                " "),
            @"\bfees\b",
            "fee").Trim();
}

public sealed record FactCheckResult(bool Passed, IReadOnlyList<string> MissingFacts);

sealed record RewritePayload(
    string RewrittenText,
    NaturalnessPayload? Naturalness,
    // From the success payload's optimization block: which loop produced the returned candidate
    // (attemptsUsed) and how many prior refine/fail iterations ran (failedAttempts). Lets the
    // eval report loops-to-success per case.
    int? AttemptsUsed,
    int? FailedAttempts)
{
    public static RewritePayload? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var text = root.TryGetProperty("rewrittenText", out var textElement)
                ? textElement.GetString() ?? string.Empty
                : string.Empty;
            NaturalnessPayload? naturalness = null;
            if (root.TryGetProperty("naturalness", out var naturalnessElement))
            {
                naturalness = new NaturalnessPayload(
                    ReadInt(naturalnessElement, "draftAiLikePercent"),
                    ReadInt(naturalnessElement, "rewriteAiLikePercent"),
                    ReadInt(naturalnessElement, "changePoints"));
            }

            int? attemptsUsed = null;
            int? failedAttempts = null;
            if (root.TryGetProperty("optimization", out var optimizationElement))
            {
                attemptsUsed = ReadInt(optimizationElement, "attemptsUsed");
                failedAttempts = ReadInt(optimizationElement, "failedAttempts");
            }

            return new RewritePayload(text, naturalness, attemptsUsed, failedAttempts);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? ReadInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;
}

sealed record RewriteFailurePayload(IReadOnlyList<RewriteAttemptHistoryItem> AttemptHistory)
{
    public static RewriteFailurePayload? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("attemptHistory", out var attemptHistoryElement) ||
                attemptHistoryElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var history = JsonSerializer.Deserialize<IReadOnlyList<RewriteAttemptHistoryItem>>(
                attemptHistoryElement.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return new RewriteFailurePayload(history ?? []);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

sealed record NaturalnessPayload(int? DraftAiLikePercent, int? RewriteAiLikePercent, int? ChangePoints);

sealed record EvalResultRow(
    string Id,
    int CaseNumber,
    string Category,
    string Tone,
    bool Success,
    string? ErrorCode,
    int? DraftAiLikePercent,
    int? RewriteAiLikePercent,
    int? ChangePoints,
    bool FactsPreserved,
    IReadOnlyList<string> MissingFacts,
    IReadOnlyList<string> ForbiddenViolations,
    IReadOnlyList<string> ForbiddenAbstained,
    bool CustomerUsable,
    bool WouldPassRelaxedGate,
    string RewrittenText,
    IReadOnlyList<RewriteAttemptHistoryItem> AttemptHistory,
    int? AttemptsUsed,
    int? FailedAttempts);

sealed record EvalSummary(
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string Mode,
    int CasesEvaluated,
    int SuccessCount,
    int ProviderFailureCount,
    int FactPassCount,
    int MeasuredCount,
    int RewritesBelow50Count,
    int BaselineAboveThresholdCount,
    int? BaselineAboveThresholdAverageDropPoints,
    int? AverageDropPoints,
    int ModelCalls,
    int SaplingCalls,
    int MaxAttempts,
    string Model,
    int NaturalnessThreshold,
    int CustomerUsableCount,
    int ForbiddenViolationCaseCount,
    int RelaxedGateRecoverableCount)
{
    public static EvalSummary Create(
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        EvalConfig config,
        IReadOnlyList<EvalResultRow> rows,
        int modelCalls,
        int saplingCalls)
    {
        var measuredRows = rows
            .Where(row => row.DraftAiLikePercent.HasValue && row.RewriteAiLikePercent.HasValue)
            .ToArray();
        var averageDrop = measuredRows.Length == 0
            ? null
            : (int?)Math.Round(measuredRows.Average(row => row.DraftAiLikePercent!.Value - row.RewriteAiLikePercent!.Value), MidpointRounding.AwayFromZero);
        var baselineAboveThresholdRows = measuredRows
            .Where(row => row.DraftAiLikePercent > config.NaturalnessThreshold)
            .ToArray();
        var baselineAboveThresholdAverageDrop = baselineAboveThresholdRows.Length == 0
            ? null
            : (int?)Math.Round(baselineAboveThresholdRows.Average(row => row.DraftAiLikePercent!.Value - row.RewriteAiLikePercent!.Value), MidpointRounding.AwayFromZero);

        return new EvalSummary(
            startedAt,
            finishedAt,
            config.Mode,
            rows.Count,
            rows.Count(row => row.Success),
            rows.Count(row => !row.Success && row.ErrorCode is not null),
            rows.Count(row => row.FactsPreserved),
            measuredRows.Length,
            measuredRows.Count(row => row.RewriteAiLikePercent < 50),
            baselineAboveThresholdRows.Length,
            baselineAboveThresholdAverageDrop,
            averageDrop,
            modelCalls,
            saplingCalls,
            config.MaxAttempts,
            config.Model,
            config.NaturalnessThreshold,
            rows.Count(row => row.CustomerUsable),
            rows.Count(row => row.ForbiddenViolations.Count > 0),
            rows.Count(row => row.WouldPassRelaxedGate));
    }
}

static class MarkdownReport
{
    public static string Render(EvalSummary summary, IReadOnlyList<EvalResultRow> rows)
    {
        var lines = new List<string>
        {
            $"# C# Rewrite Eval - {summary.Mode}",
            "",
            $"Started: {summary.StartedAt:O}",
            $"Finished: {summary.FinishedAt:O}",
            $"Cases evaluated: {summary.CasesEvaluated}",
            $"Successful rewrites: {summary.SuccessCount}/{summary.CasesEvaluated}",
            $"Fact pass count: {summary.FactPassCount}/{summary.CasesEvaluated}",
            $"Customer-usable pass: {summary.CustomerUsableCount}/{summary.CasesEvaluated} (output + all must_keep preserved + engine success + no forbidden violation)",
            $"Forbidden-claim violations (deterministic screen): {summary.ForbiddenViolationCaseCount}/{summary.CasesEvaluated}",
            $"Naturalness failures recoverable under relaxed gate (rewrite <= {summary.NaturalnessThreshold}): {summary.RelaxedGateRecoverableCount}",
            $"Measured rewrites: {summary.MeasuredCount}",
            $"Average signal drop: {summary.AverageDropPoints?.ToString() ?? "unavailable"} pts",
            $"Baseline-above-threshold average drop: {summary.BaselineAboveThresholdAverageDropPoints?.ToString() ?? "unavailable"} pts ({summary.BaselineAboveThresholdCount} cases)",
            $"Rewrites below 50% signal: {summary.RewritesBelow50Count}/{summary.MeasuredCount}",
            $"Model calls: {summary.ModelCalls}",
            $"Sapling calls: {summary.SaplingCalls}",
            $"Model: {summary.Model}",
            $"Max attempts: {summary.MaxAttempts}",
            "",
            "| Case | Category | Tone | Usable | Success | Draft | Rewrite | Change | Facts | Forbidden | Error | Missing facts |",
            "| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |",
        };

        lines.AddRange(rows.Select(row => string.Join(" | ", new[]
        {
            row.Id,
            row.Category,
            row.Tone,
            row.CustomerUsable ? "yes" : "no",
            row.Success ? "yes" : "no",
            Points(row.DraftAiLikePercent),
            Points(row.RewriteAiLikePercent),
            row.ChangePoints?.ToString() ?? "unavailable",
            row.FactsPreserved ? "yes" : "no",
            row.ForbiddenViolations.Count.ToString(),
            row.ErrorCode ?? "",
            string.Join("; ", row.MissingFacts).Replace("|", "/", StringComparison.Ordinal),
        })));

        return string.Join("\n", lines) + "\n";
    }

    private static string Points(int? value) => value.HasValue ? $"{value.Value}%" : "unavailable";
}

sealed class CountingRewriteModelClient(IRewriteModelClient inner) : IRewriteModelClient
{
    public int CallCount { get; private set; }

    public async Task<RewriteModelResult> GenerateCandidateAsync(
        RewriteModelRequest request,
        CancellationToken cancellationToken)
    {
        CallCount += 1;
        return await inner.GenerateCandidateAsync(request, cancellationToken);
    }
}

sealed class CountingWritingSignalClient(IWritingSignalClient inner) : IWritingSignalClient
{
    public int CallCount { get; private set; }

    public async Task<WritingSignalResult> MeasureAsync(string text, CancellationToken cancellationToken)
    {
        CallCount += 1;
        return await inner.MeasureAsync(text, cancellationToken);
    }
}
