using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Infrastructure.Providers;

var startedAt = DateTimeOffset.UtcNow;
var config = EvalConfig.FromEnvironment();
LoadEnvFileIfPresent(Path.Combine(config.RepoRoot, ".env.local"));

var cases = EvalCaseParser.Parse(await File.ReadAllTextAsync(config.CasesPath));
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

var apiKey = ResolveApiKey(config);
var saplingApiKey = Environment.GetEnvironmentVariable("SAPLING_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(saplingApiKey))
{
    Console.Error.WriteLine("Missing real provider configuration. Set DEEPSEEK_API_KEY or OPENAI_API_KEY, plus SAPLING_API_KEY.");
    return 2;
}

Directory.CreateDirectory(config.OutputDirectory);

using var modelHttpClient = new HttpClient();
using var signalHttpClient = new HttpClient();
var modelClient = new OpenAiCompatibleRewriteModelClient(
    modelHttpClient,
    apiKey,
    config.Model,
    config.OpenAiBaseUrl,
    TimeSpan.FromSeconds(config.ModelTimeoutSeconds));
var signalClient = new CountingWritingSignalClient(
    new SaplingWritingSignalClient(
        signalHttpClient,
        saplingApiKey,
        TimeSpan.FromSeconds(config.SaplingTimeoutSeconds)));
var countingModelClient = new CountingRewriteModelClient(modelClient);
var provider = new FactReconstructRewriteProvider(
    countingModelClient,
    signalClient,
    new FactReconstructRewriteOptions(
        NaturalnessThreshold: config.NaturalnessThreshold,
        RequestedMaxAttempts: config.MaxAttempts));

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
        attemptHistory));

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
var jsonPath = Path.Combine(config.OutputDirectory, $"{stamp}-csharp-rewrite-{config.Mode}.json");
var mdPath = Path.Combine(config.OutputDirectory, $"{stamp}-csharp-rewrite-{config.Mode}.md");
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
    int SaplingTimeoutSeconds)
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
            Int("WRITING_SIGNAL_TIMEOUT_SEC", 20));
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
        "a", "an", "and", "are", "as", "ask", "at", "be", "by", "can", "client", "currently",
        "customer", "date", "did", "do", "does", "dropped", "explain", "for", "from", "has",
        "have", "in", "is", "it", "must", "need", "needs", "new", "no", "not", "of", "off", "on", "only", "or",
        "parent", "people", "person", "please", "plus", "reply", "requires", "seller", "should",
        "status", "still", "student", "support", "team", "the", "they", "to", "user", "want",
        "whether", "will", "with", "would",
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> TokenAliases =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
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
            ["inventory"] = ["inventory", "stock"],
            ["meeting"] = ["meeting", "meet"],
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

        var ids = Regex.Matches(normalizedFact, @"\b[a-z]{1,8}-\d+\b|#[a-z0-9-]+")
            .Select(match => match.Value)
            .ToArray();
        if (ids.Length > 0 && ids.All(id => normalizedText.Contains(id, StringComparison.Ordinal)))
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
    NaturalnessPayload? Naturalness)
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

            return new RewritePayload(text, naturalness);
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
    IReadOnlyList<RewriteAttemptHistoryItem> AttemptHistory);

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
