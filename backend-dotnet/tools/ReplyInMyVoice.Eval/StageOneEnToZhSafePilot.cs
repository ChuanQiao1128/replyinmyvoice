using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ReplyInMyVoice.Domain.Quality;
using ReplyInMyVoice.Domain.RewriteEngine;

namespace ReplyInMyVoice.Eval;

public enum ZhClaimSurvivalStatus
{
    Yes,
    Partial,
    No,
}

public sealed record ZhClaimSurvivalVerdict(
    string ClaimId,
    ZhClaimSurvivalStatus Status,
    string Reason);

public enum ZhFactCheckStatus
{
    Present,
    Missing,
    Changed,
    Ambiguous,
}

public enum ZhFactMatchKind
{
    None,
    Exact,
    Normalized,
    Alias,
    TranslatedEquivalent,
    Semantic,
}

public enum ZhFactFailureKind
{
    None,
    Missing,
    ValueChanged,
    ExactRequiredButTranslated,
    EntityGeneralized,
    RoleOrObjectChanged,
    AliasNotApproved,
    NormalizerGap,
    EvidenceExtractionGap,
    OverExtractedNonHardFact,
    ShouldBeTermLedger,
    ShouldBeClaimLedger,
    Ambiguous,
}

public sealed record ZhFactCheckItem(
    string Id,
    string SourceText,
    RewriteFactCategory Type,
    RewriteFactPreserveMode PreserveMode,
    ZhFactCheckStatus Status,
    ZhFactMatchKind MatchKind,
    string? ZhEvidence,
    string? Issue,
    ZhFactFailureKind FailureKind = ZhFactFailureKind.None,
    string? RecommendedNextAction = null);

public interface IZhClaimSurvivalJudge
{
    Task<IReadOnlyDictionary<string, ZhClaimSurvivalVerdict>> JudgeAsync(
        string originalEn,
        string translatedZh,
        IReadOnlyList<RewriteClaim> claims,
        CancellationToken cancellationToken);
}

public sealed record ZhPostCheckReport(
    string CaseId,
    string OriginalEn,
    string TranslatedZh,
    IReadOnlyList<RewriteFact> FactsSurvived,
    IReadOnlyList<RewriteFact> FactsDrifted,
    IReadOnlyList<RewriteClaim> ClaimsSurvived,
    IReadOnlyList<RewriteClaim> ClaimsDrifted,
    double FactSurvivalPct,
    double ClaimSurvivalPct)
{
    public IReadOnlyDictionary<string, string> FactDriftReasons { get; init; } =
        new Dictionary<string, string>();

    public IReadOnlyList<ZhFactCheckItem> FactChecks { get; init; } = Array.Empty<ZhFactCheckItem>();

    public IReadOnlyDictionary<string, ZhClaimSurvivalVerdict> ClaimVerdicts { get; init; } =
        new Dictionary<string, ZhClaimSurvivalVerdict>();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public bool HasErrors => FactsDrifted.Count > 0 || ClaimsDrifted.Count > 0 || Warnings.Count > 0;
}

public sealed record ZhMinimalRepairRequest(
    string CaseId,
    string OriginalEn,
    string CurrentZh,
    RewriteFactLedger FactLedger,
    RewriteClaimLedger ClaimLedger,
    ZhPostCheckReport Report);

public interface IZhMinimalRepairer
{
    Task<string?> RepairAsync(ZhMinimalRepairRequest request, CancellationToken cancellationToken);
}

public sealed record ZhSafeIntermediateResult(
    string CaseId,
    string OriginalEn,
    string RawTranslatedZh,
    string FinalZh,
    ZhPostCheckReport InitialReport,
    ZhPostCheckReport FinalReport,
    int RepairAttempts)
{
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public static class StageOneZhPostChecker
{
    public static async Task<ZhPostCheckReport> CreateReportAsync(
        string caseId,
        string originalEn,
        string translatedZh,
        RewriteFactLedger factLedger,
        RewriteClaimLedger claimLedger,
        IZhClaimSurvivalJudge claimJudge,
        CancellationToken cancellationToken)
    {
        var factResults = factLedger.Facts
            .Select(f => (Fact: f, Result: StageOneHardFactChecker.Check(f, translatedZh)))
            .ToArray();
        var factsSurvived = factResults.Where(r => r.Result.Status == ZhFactCheckStatus.Present).Select(r => r.Fact).ToArray();
        var factsDrifted = factResults.Where(r => r.Result.Status != ZhFactCheckStatus.Present).Select(r => r.Fact).ToArray();
        var factDriftReasons = factResults
            .Where(r => r.Result.Status != ZhFactCheckStatus.Present)
            .ToDictionary(r => r.Fact.Id, r => r.Result.Issue ?? r.Result.Status.ToString(), StringComparer.Ordinal);

        var verdicts = claimLedger.Claims.Count == 0
            ? new Dictionary<string, ZhClaimSurvivalVerdict>(StringComparer.Ordinal)
            : await claimJudge.JudgeAsync(originalEn, translatedZh, claimLedger.Claims, cancellationToken);
        var normalizedVerdicts = NormalizeVerdicts(claimLedger.Claims, verdicts);
        var claimsSurvived = claimLedger.Claims
            .Where(c => normalizedVerdicts[c.Id].Status == ZhClaimSurvivalStatus.Yes)
            .ToArray();
        var claimsDrifted = claimLedger.Claims
            .Where(c => normalizedVerdicts[c.Id].Status != ZhClaimSurvivalStatus.Yes)
            .ToArray();
        var warnings = new List<string>();
        if (!string.IsNullOrWhiteSpace(originalEn) && claimLedger.Claims.Count == 0)
        {
            warnings.Add("claim_ledger_empty");
        }

        return new ZhPostCheckReport(
            caseId,
            originalEn,
            translatedZh,
            factsSurvived,
            factsDrifted,
            claimsSurvived,
            claimsDrifted,
            Percent(factsSurvived.Length, factLedger.Facts.Count),
            warnings.Contains("claim_ledger_empty", StringComparer.Ordinal)
                ? 0.0
                : Percent(claimsSurvived.Length, claimLedger.Claims.Count))
        {
            FactChecks = factResults.Select(r => r.Result).ToArray(),
            FactDriftReasons = factDriftReasons,
            ClaimVerdicts = normalizedVerdicts,
            Warnings = warnings,
        };
    }

    private static IReadOnlyDictionary<string, ZhClaimSurvivalVerdict> NormalizeVerdicts(
        IReadOnlyList<RewriteClaim> claims,
        IReadOnlyDictionary<string, ZhClaimSurvivalVerdict> verdicts)
    {
        var normalized = new Dictionary<string, ZhClaimSurvivalVerdict>(StringComparer.Ordinal);
        foreach (var claim in claims)
        {
            normalized[claim.Id] = verdicts.TryGetValue(claim.Id, out var verdict)
                ? verdict
                : new ZhClaimSurvivalVerdict(claim.Id, ZhClaimSurvivalStatus.No, "judge_missing");
        }

        return normalized;
    }

    private static double Percent(int survived, int total) =>
        total == 0 ? 100.0 : survived * 100.0 / total;
}

public static class ZhClaimSurvivalVerdictParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static IReadOnlyDictionary<string, ZhClaimSurvivalVerdict> Parse(
        string? json,
        IReadOnlyList<RewriteClaim> claims)
    {
        var byId = claims.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var result = byId.Keys.ToDictionary(
            id => id,
            id => new ZhClaimSurvivalVerdict(id, ZhClaimSurvivalStatus.No, "judge_missing"),
            StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        RawResponse? raw;
        try
        {
            raw = JsonSerializer.Deserialize<RawResponse>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            foreach (var id in result.Keys.ToArray())
            {
                result[id] = new ZhClaimSurvivalVerdict(id, ZhClaimSurvivalStatus.No, "judge_parse_failed");
            }

            return result;
        }

        if (raw?.Claims is null)
        {
            return result;
        }

        foreach (var rawVerdict in raw.Claims)
        {
            var id = FirstNonBlank(rawVerdict.Id, rawVerdict.ClaimId);
            if (id is null || !byId.ContainsKey(id))
            {
                continue;
            }

            result[id] = new ZhClaimSurvivalVerdict(
                id,
                ParseStatus(rawVerdict.Status, rawVerdict.Survived),
                string.IsNullOrWhiteSpace(rawVerdict.Reason) ? "no_reason" : rawVerdict.Reason.Trim());
        }

        return result;
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static ZhClaimSurvivalStatus ParseStatus(string? raw, bool? survived)
    {
        var normalized = raw?.Trim().ToLowerInvariant();
        if (normalized is "yes" or "true" or "survived" or "present")
        {
            return ZhClaimSurvivalStatus.Yes;
        }

        if (normalized is "partial" or "partly" or "unclear")
        {
            return ZhClaimSurvivalStatus.Partial;
        }

        return survived == true ? ZhClaimSurvivalStatus.Yes : ZhClaimSurvivalStatus.No;
    }

    private sealed class RawResponse
    {
        [JsonPropertyName("claims")]
        public List<RawVerdict>? Claims { get; set; }
    }

    private sealed class RawVerdict
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("claim_id")] public string? ClaimId { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("survived")] public bool? Survived { get; set; }
        [JsonPropertyName("reason")] public string? Reason { get; set; }
    }
}

public static class StageOneZhReportRenderer
{
    public static string Render(
        DateTimeOffset startedAt,
        IReadOnlyList<ZhPostCheckReport> reports,
        int youdaoCalls,
        int deepSeekCalls,
        string model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Stage 1 EN->ZH Post-Check Pilot");
        sb.AppendLine();
        sb.AppendLine($"Started: `{startedAt:O}`");
        sb.AppendLine($"Model: `{model}`");
        sb.AppendLine($"Youdao calls: `{youdaoCalls}`");
        sb.AppendLine($"DeepSeek calls: `{deepSeekCalls}`");
        sb.AppendLine();
        sb.AppendLine("| Case | Fact Survival | Claim Survival | Facts | Claims |");
        sb.AppendLine("|---|---:|---:|---:|---:|");
        foreach (var report in reports)
        {
            var totalFacts = report.FactsSurvived.Count + report.FactsDrifted.Count;
            var totalClaims = report.ClaimsSurvived.Count + report.ClaimsDrifted.Count;
            sb.AppendLine(
                $"| {EscapeCell(report.CaseId)} | {report.FactSurvivalPct:F1}% | {report.ClaimSurvivalPct:F1}% | "
                + $"{report.FactsSurvived.Count}/{totalFacts} | {report.ClaimsSurvived.Count}/{totalClaims} |");
        }

        foreach (var report in reports)
        {
            sb.AppendLine();
            sb.AppendLine($"## {report.CaseId}");
            sb.AppendLine();
            sb.AppendLine($"- Fact survival: `{report.FactSurvivalPct:F1}%`");
            sb.AppendLine($"- Claim survival: `{report.ClaimSurvivalPct:F1}%`");
            if (report.Warnings.Count > 0)
            {
                sb.AppendLine($"- Warnings: `{string.Join(", ", report.Warnings)}`");
            }

            sb.AppendLine("- Fact drifts:");
            if (report.FactsDrifted.Count == 0)
            {
                sb.AppendLine("  - none");
            }
            else
            {
                foreach (var fact in report.FactsDrifted)
                {
                    var reason = report.FactDriftReasons.TryGetValue(fact.Id, out var r) ? r : "missing";
                    sb.AppendLine($"  - {fact.Id}: `{fact.Text}` - {reason}");
                }
            }

            sb.AppendLine("- Claim drifts:");
            if (report.ClaimsDrifted.Count == 0)
            {
                sb.AppendLine("  - none");
            }
            else
            {
                foreach (var claim in report.ClaimsDrifted)
                {
                    var verdict = report.ClaimVerdicts.TryGetValue(claim.Id, out var v)
                        ? v
                        : new ZhClaimSurvivalVerdict(claim.Id, ZhClaimSurvivalStatus.No, "judge_missing");
                    sb.AppendLine(
                        $"  - {claim.Id}: {verdict.Status.ToString().ToLowerInvariant()} - "
                        + $"{verdict.Reason} - `{claim.SourceSpan}`");
                }
            }

            sb.AppendLine();
            sb.AppendLine("<details><summary>Translated ZH</summary>");
            sb.AppendLine();
            sb.AppendLine("```text");
            sb.AppendLine(report.TranslatedZh);
            sb.AppendLine("```");
            sb.AppendLine("</details>");
        }

        return sb.ToString();
    }

    private static string EscapeCell(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);

    public static string RenderSafeIntermediates(
        DateTimeOffset startedAt,
        IReadOnlyList<ZhSafeIntermediateResult> results,
        int youdaoCalls,
        int deepSeekCalls,
        string model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Stage 1 EN->ZH Safe Intermediate Pilot");
        sb.AppendLine();
        sb.AppendLine($"Started: `{startedAt:O}`");
        sb.AppendLine($"Model: `{model}`");
        sb.AppendLine($"Youdao calls: `{youdaoCalls}`");
        sb.AppendLine($"DeepSeek calls: `{deepSeekCalls}`");
        sb.AppendLine();
        sb.AppendLine("| Case | Initial Fact | Initial Claim | Final Fact | Final Claim | Repairs | Final |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---|");
        foreach (var result in results)
        {
            sb.AppendLine(
                $"| {EscapeCell(result.CaseId)} | {result.InitialReport.FactSurvivalPct:F1}% | "
                + $"{result.InitialReport.ClaimSurvivalPct:F1}% | {result.FinalReport.FactSurvivalPct:F1}% | "
                + $"{result.FinalReport.ClaimSurvivalPct:F1}% | {result.RepairAttempts} | "
                + $"{(result.FinalReport.HasErrors ? "fail" : "pass")} |");
        }

        AppendFactSummary(sb, "Initial fact summary", results.SelectMany(r => r.InitialReport.FactChecks).ToArray());
        AppendFactSummary(sb, "Final fact summary", results.SelectMany(r => r.FinalReport.FactChecks).ToArray());
        AppendFailureBreakdown(sb, "Initial failure breakdown", results.SelectMany(r => r.InitialReport.FactChecks).ToArray());
        AppendFailureBreakdown(sb, "Final failure breakdown", results.SelectMany(r => r.FinalReport.FactChecks).ToArray());

        foreach (var result in results)
        {
            sb.AppendLine();
            sb.AppendLine($"## {result.CaseId}");
            sb.AppendLine();
            sb.AppendLine($"- Repair attempts: `{result.RepairAttempts}`");
            sb.AppendLine($"- Initial fact survival: `{result.InitialReport.FactSurvivalPct:F1}%`");
            sb.AppendLine($"- Initial claim survival: `{result.InitialReport.ClaimSurvivalPct:F1}%`");
            sb.AppendLine($"- Final fact survival: `{result.FinalReport.FactSurvivalPct:F1}%`");
            sb.AppendLine($"- Final claim survival: `{result.FinalReport.ClaimSurvivalPct:F1}%`");
            sb.AppendLine($"- Final status: `{(result.FinalReport.HasErrors ? "fail" : "pass")}`");
            if (result.Warnings.Count > 0)
            {
                sb.AppendLine($"- Warnings: `{string.Join(", ", result.Warnings)}`");
            }

            AppendDrifts(sb, "Initial", result.InitialReport);
            if (result.RepairAttempts > 0 || result.FinalReport.HasErrors)
            {
                AppendDrifts(sb, "Final", result.FinalReport);
            }

            sb.AppendLine();
            sb.AppendLine("<details><summary>Raw Youdao ZH</summary>");
            sb.AppendLine();
            sb.AppendLine("```text");
            sb.AppendLine(result.RawTranslatedZh);
            sb.AppendLine("```");
            sb.AppendLine("</details>");
            sb.AppendLine();
            sb.AppendLine("<details><summary>Final Safe ZH</summary>");
            sb.AppendLine();
            sb.AppendLine("```text");
            sb.AppendLine(result.FinalZh);
            sb.AppendLine("```");
            sb.AppendLine("</details>");
        }

        return sb.ToString();
    }

    private static void AppendFactSummary(StringBuilder sb, string title, IReadOnlyList<ZhFactCheckItem> checks)
    {
        sb.AppendLine();
        sb.AppendLine($"## {title}");
        sb.AppendLine();
        sb.AppendLine($"- total: `{checks.Count}`");
        foreach (var group in checks.GroupBy(c => (c.Status, c.MatchKind)).OrderBy(g => g.Key.Status).ThenBy(g => g.Key.MatchKind))
        {
            sb.AppendLine($"- {group.Key.Status.ToString().ToLowerInvariant()} / {group.Key.MatchKind.ToString().ToLowerInvariant()}: `{group.Count()}`");
        }
    }

    private static void AppendFailureBreakdown(StringBuilder sb, string title, IReadOnlyList<ZhFactCheckItem> checks)
    {
        sb.AppendLine();
        sb.AppendLine($"## {title}");
        sb.AppendLine();
        var failures = checks.Where(c => c.FailureKind != ZhFactFailureKind.None).ToArray();
        if (failures.Length == 0)
        {
            sb.AppendLine("- none");
            return;
        }

        foreach (var group in failures.GroupBy(c => c.FailureKind).OrderBy(g => g.Key))
        {
            sb.AppendLine($"- {SnakeCase(group.Key.ToString())}: `{group.Count()}`");
        }
    }

    private static void AppendDrifts(StringBuilder sb, string label, ZhPostCheckReport report)
    {
        sb.AppendLine($"- {label} fact drifts:");
        if (report.FactsDrifted.Count == 0)
        {
            sb.AppendLine("  - none");
        }
        else
        {
            foreach (var fact in report.FactsDrifted)
            {
                var reason = report.FactDriftReasons.TryGetValue(fact.Id, out var r) ? r : "missing";
                var check = report.FactChecks.FirstOrDefault(c => string.Equals(c.Id, fact.Id, StringComparison.Ordinal));
                if (check is null)
                {
                    sb.AppendLine($"  - {fact.Id}: `{fact.Text}` - {reason}");
                }
                else
                {
                    var evidence = string.IsNullOrWhiteSpace(check.ZhEvidence)
                        ? string.Empty
                        : $"; zh_evidence: `{check.ZhEvidence}`";
                    var nextAction = string.IsNullOrWhiteSpace(check.RecommendedNextAction)
                        ? string.Empty
                        : $"; recommended_next_action: `{check.RecommendedNextAction}`";
                    sb.AppendLine(
                        $"  - {fact.Id}: `{fact.Text}` - {reason}; "
                        + $"failure_kind: `{SnakeCase(check.FailureKind.ToString())}`; "
                        + $"status: `{check.Status.ToString().ToLowerInvariant()}`; "
                        + $"match_kind: `{check.MatchKind.ToString().ToLowerInvariant()}`"
                        + evidence
                        + nextAction);
                }
            }
        }

        sb.AppendLine($"- {label} claim drifts:");
        if (report.ClaimsDrifted.Count == 0)
        {
            sb.AppendLine("  - none");
        }
        else
        {
            foreach (var claim in report.ClaimsDrifted)
            {
                var verdict = report.ClaimVerdicts.TryGetValue(claim.Id, out var v)
                    ? v
                    : new ZhClaimSurvivalVerdict(claim.Id, ZhClaimSurvivalStatus.No, "judge_missing");
                sb.AppendLine(
                    $"  - {claim.Id}: {verdict.Status.ToString().ToLowerInvariant()} - "
                    + $"{verdict.Reason} - `{claim.SourceSpan}`");
            }
        }
    }

    private static string SnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(value.Length + 4);
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (char.IsUpper(ch) && i > 0)
            {
                sb.Append('_');
            }

            sb.Append(char.ToLowerInvariant(ch));
        }

        return sb.ToString();
    }
}

public static class StageOneModelSelection
{
    public static string ResolveClaimModel(string? overrideModel) =>
        string.IsNullOrWhiteSpace(overrideModel) ? "deepseek-chat" : overrideModel.Trim();
}

internal static class StageOneEnToZhSafePilot
{
    public static async Task<int> RunAsync(IReadOnlyList<EvalCase> cases, EvalConfig config, DateTimeOffset startedAt)
    {
        var apiKey = FirstEnv("DEEPSEEK_API_KEY", "OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine("Missing model configuration. Set DEEPSEEK_API_KEY or OPENAI_API_KEY.");
            return 2;
        }

        var youdaoKey = FirstEnv("YOUDAO_APP_KEY", "AppID", "YouDao_API_KEY");
        var youdaoSecret = FirstEnv("YOUDAO_APP_SECRET", "AppSecret");
        if (string.IsNullOrWhiteSpace(youdaoKey) || string.IsNullOrWhiteSpace(youdaoSecret))
        {
            Console.Error.WriteLine("Missing Youdao configuration. Set YOUDAO_APP_KEY and YOUDAO_APP_SECRET.");
            return 2;
        }

        var youdaoMaxCalls = IntEnv("STAGE1_YOUDAO_MAX_CALLS", IntEnv("YOUDAO_MAX_CALLS", 40));
        var deepSeekMaxCalls = IntEnv("STAGE1_DEEPSEEK_MAX_CALLS", 80);
        var repairAttempts = IntEnv("STAGE1_REPAIR_MAX_ATTEMPTS", 1);
        var claimModel = StageOneModelSelection.ResolveClaimModel(Environment.GetEnvironmentVariable("STAGE1_CLAIM_MODEL"));
        var results = new List<ZhSafeIntermediateResult>();

        using var youdaoHttp = new HttpClient();
        using var deepSeekHttp = new HttpClient();
        var youdao = new YoudaoTranslationClient(
            youdaoHttp,
            youdaoKey!,
            youdaoSecret!,
            Environment.GetEnvironmentVariable("YOUDAO_API_URL") ?? "https://openapi.youdao.com/api",
            TimeSpan.FromSeconds(30));
        var chat = new DeepSeekChatClient(
            deepSeekHttp,
            apiKey!,
            claimModel,
            config.OpenAiBaseUrl,
            TimeSpan.FromSeconds(config.ModelTimeoutSeconds));
        var claimExtractor = new DeepSeekClaimLedgerExtractor(chat);
        var claimJudge = new DeepSeekZhClaimSurvivalJudge(chat);
        var repairer = new DeepSeekZhMinimalRepairer(chat);

        Console.WriteLine(
            $"Stage1 EN->ZH pilot: cases={cases.Count} youdaoMax={youdaoMaxCalls} "
            + $"deepSeekMax={deepSeekMaxCalls} repairMax={repairAttempts} claimModel={claimModel}");
        foreach (var sample in cases)
        {
            if (youdao.CallCount + 1 > youdaoMaxCalls)
            {
                Console.Error.WriteLine("Stopping before next case: Youdao budget exhausted.");
                break;
            }

            if (chat.CallCount + 2 + (repairAttempts * 2) > deepSeekMaxCalls)
            {
                Console.Error.WriteLine("Stopping before next case: DeepSeek budget exhausted.");
                break;
            }

            var factLedger = StageOneApprovedAliasCatalog.Apply(
                StageOneHardFactLedgerBuilder.Build(
                    FactLedgerExtractor.Extract(sample.ToRewriteRequest()),
                    sample.InputDraft));
            var claimLedger = await claimExtractor.ExtractAsync(sample.InputDraft, CancellationToken.None);
            var translated = await youdao.TranslateAsync(sample.InputDraft, "en", "zh-CHS", CancellationToken.None);
            if (!translated.Success)
            {
                Console.Error.WriteLine($"{sample.Id}: Youdao EN->ZH failed: {translated.ErrorCode}");
                return 2;
            }

            var result = await StageOneZhRepairLoop.RunAsync(
                sample.Id,
                sample.InputDraft,
                translated.Text,
                factLedger,
                claimLedger,
                claimJudge,
                repairer,
                repairAttempts,
                CancellationToken.None);
            results.Add(result);
            Console.WriteLine(
                $"{sample.Id}: initialFacts={result.InitialReport.FactsSurvived.Count}/{factLedger.Facts.Count} "
                + $"finalFacts={result.FinalReport.FactsSurvived.Count}/{factLedger.Facts.Count} "
                + $"initialClaims={result.InitialReport.ClaimsSurvived.Count}/{claimLedger.Claims.Count} "
                + $"finalClaims={result.FinalReport.ClaimsSurvived.Count}/{claimLedger.Claims.Count} "
                + $"repairs={result.RepairAttempts}");
        }

        Directory.CreateDirectory(config.OutputDirectory);
        var outputPath = Path.Combine(
            config.OutputDirectory,
            $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-stage1-en-zh-pilot.md");
        await File.WriteAllTextAsync(
            outputPath,
            StageOneZhReportRenderer.RenderSafeIntermediates(startedAt, results, youdao.CallCount, chat.CallCount, claimModel));
        Console.WriteLine($"Stage1 EN->ZH pilot wrote {outputPath}");
        return results.Count == 0 ? 2 : 0;
    }

    private static string? FirstEnv(params string[] names) =>
        names.Select(Environment.GetEnvironmentVariable).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static int IntEnv(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value > 0 ? value : fallback;
}

public static class StageOneZhRepairLoop
{
    public static async Task<ZhSafeIntermediateResult> RunAsync(
        string caseId,
        string originalEn,
        string rawTranslatedZh,
        RewriteFactLedger factLedger,
        RewriteClaimLedger claimLedger,
        IZhClaimSurvivalJudge claimJudge,
        IZhMinimalRepairer repairer,
        int maxRepairAttempts,
        CancellationToken cancellationToken)
    {
        var initialReport = await StageOneZhPostChecker.CreateReportAsync(
            caseId,
            originalEn,
            rawTranslatedZh,
            factLedger,
            claimLedger,
            claimJudge,
            cancellationToken);

        var currentZh = rawTranslatedZh;
        var currentReport = initialReport;
        var attempts = 0;
        var warnings = new List<string>();
        while (StageOneFactFailureRouting.HasRepairableWork(currentReport)
            && attempts < Math.Max(0, maxRepairAttempts))
        {
            var repairReport = StageOneFactFailureRouting.FilterForRepair(currentReport);
            var repaired = await repairer.RepairAsync(
                new ZhMinimalRepairRequest(caseId, originalEn, currentZh, factLedger, claimLedger, repairReport),
                cancellationToken);
            if (string.IsNullOrWhiteSpace(repaired))
            {
                break;
            }

            attempts += 1;
            var candidateZh = repaired.Trim();
            var candidateReport = await StageOneZhPostChecker.CreateReportAsync(
                caseId,
                originalEn,
                candidateZh,
                factLedger,
                claimLedger,
                claimJudge,
                cancellationToken);
            if (ClaimSurvivalRegressed(currentReport, candidateReport))
            {
                warnings.Add("repair_rejected_claim_regression");
                break;
            }

            currentZh = candidateZh;
            currentReport = candidateReport;
        }

        return new ZhSafeIntermediateResult(
            caseId,
            originalEn,
            rawTranslatedZh,
            currentZh,
            initialReport,
            currentReport,
            attempts)
        {
            Warnings = warnings,
        };
    }

    private static bool ClaimSurvivalRegressed(ZhPostCheckReport previous, ZhPostCheckReport candidate) =>
        candidate.ClaimsSurvived.Count < previous.ClaimsSurvived.Count
        || candidate.ClaimsDrifted.Count > previous.ClaimsDrifted.Count;
}

public static class StageOneFactFailureRouting
{
    public static bool HasRepairableWork(ZhPostCheckReport report) =>
        report.ClaimsDrifted.Count > 0 || report.FactChecks.Any(ShouldSendToRepair);

    public static bool ShouldSendToRepair(ZhFactCheckItem item) =>
        item.Status != ZhFactCheckStatus.Present
        && item.FailureKind is ZhFactFailureKind.Missing
            or ZhFactFailureKind.ValueChanged
            or ZhFactFailureKind.ExactRequiredButTranslated
            or ZhFactFailureKind.EntityGeneralized;

    public static ZhPostCheckReport FilterForRepair(ZhPostCheckReport report)
    {
        var actionableIds = report.FactChecks
            .Where(ShouldSendToRepair)
            .Select(c => c.Id)
            .ToHashSet(StringComparer.Ordinal);
        var filteredDrifts = report.FactsDrifted
            .Where(f => actionableIds.Contains(f.Id))
            .ToArray();

        return report with
        {
            FactsDrifted = filteredDrifts,
            FactDriftReasons = report.FactDriftReasons
                .Where(kvp => actionableIds.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal),
            FactChecks = report.FactChecks
                .Where(c => c.Status == ZhFactCheckStatus.Present || actionableIds.Contains(c.Id))
                .ToArray(),
        };
    }
}

public sealed record StageOneAliasEntry(
    string SourceText,
    string AliasText,
    string AliasLanguage,
    string Source,
    bool Approved,
    string DomainScope);

public static class StageOneApprovedAliasCatalog
{
    public const string BuiltInDomainScope = "stage1-eval-30";
    private const string BuiltInSource = "stage1-eval-reviewed";

    private static readonly IReadOnlyList<StageOneAliasEntry> BuiltInEntries = new[]
    {
        Approved("Jamie", "杰米"),
        Approved("Maya", "玛雅"),
        Approved("Dev", "戴夫"),
        Approved("Northstar", "北极星"),
        Approved("Nora", "诺拉"),
        Approved("Eli", "伊莱"),
        Approved("Chen", "陈"),
        Approved("Mina", "米娜"),
        Approved("Jordan", "乔丹"),
        Approved("Drew", "德鲁"),
        Approved("Maple", "枫树"),
        Approved("Kwame", "夸梅"),
        Approved("Lena", "莉娜"),
        Approved("Priya", "普丽娅"),
        Approved("Ren", "任"),
        Approved("Cam", "小卡"),
        Approved("Mateo", "马特奥"),
        Approved("Morgan", "摩根"),
        Approved("Theo", "西奥"),
        Approved("Elaine", "伊莱恩"),
        Approved("Priya Shah", "普丽娅·沙阿"),
        Approved("Priya Shah", "普丽娅沙阿"),
        Approved("Luis", "路易斯"),
        Approved("Rivera", "里韦拉"),
        Approved("Jamal", "贾马尔"),
        Approved("Felix", "菲利克斯"),
        Approved("Petra", "佩特拉"),
        Approved("Sunita", "苏尼塔"),
        Approved("Clearwater", "清水"),
        Approved("Claudia", "克劳迪娅"),
        Approved("Oliver", "奥利弗"),
        Approved("Rosalind", "罗莎琳德"),
        Approved("Mercer Terrace", "美世露台"),
        Approved("Riverside Park", "河滨公园"),
        Approved("Elm Street", "榆树街"),
        Approved("Park Pantry", "公园食品储藏室"),
        Approved("Hall B", "B大厅"),
        Approved("Hall B", "B 大厅"),
    };

    public static IReadOnlyList<StageOneAliasEntry> Entries => BuiltInEntries;

    public static RewriteFactLedger Apply(RewriteFactLedger ledger) =>
        Apply(ledger, BuiltInEntries, BuiltInDomainScope);

    public static RewriteFactLedger Apply(
        RewriteFactLedger ledger,
        IReadOnlyDictionary<string, string[]> approvedAliases) =>
        Apply(
            ledger,
            approvedAliases
                .SelectMany(kvp => kvp.Value.Select(alias => Approved(kvp.Key, alias, "legacy-test", "*")))
                .ToArray(),
            BuiltInDomainScope);

    public static RewriteFactLedger Apply(
        RewriteFactLedger ledger,
        IReadOnlyList<StageOneAliasEntry> entries,
        string domainScope)
    {
        var facts = ledger.Facts.Select(f => Apply(f, entries, domainScope)).ToArray();
        return new RewriteFactLedger(facts);
    }

    private static RewriteFact Apply(RewriteFact fact, IReadOnlyList<StageOneAliasEntry> entries, string domainScope)
    {
        if (fact.PreserveMode == RewriteFactPreserveMode.Normalized)
        {
            return fact;
        }

        var matchingEntries = entries
            .Where(e => e.SourceText.Equals(fact.Text, StringComparison.OrdinalIgnoreCase)
                && IsDomainMatch(e.DomainScope, domainScope)
                && !string.IsNullOrWhiteSpace(e.AliasText))
            .ToArray();
        if (matchingEntries.Length == 0)
        {
            return fact;
        }

        var approvedAliases = matchingEntries
            .Where(e => e.Approved)
            .Select(e => e.AliasText);
        var proposedAliases = matchingEntries
            .Where(e => !e.Approved)
            .Select(e => e.AliasText);
        var mergedAliases = (fact.AllowedAliases ?? Array.Empty<string>())
            .Concat(approvedAliases)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var mergedProposedAliases = (fact.ProposedAliases ?? Array.Empty<string>())
            .Concat(proposedAliases)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return fact with
        {
            PreserveMode = RewriteFactPreserveMode.ExactOrTranslatedAlias,
            AllowedAliases = mergedAliases,
            ProposedAliases = mergedProposedAliases,
        };
    }

    private static bool IsDomainMatch(string entryDomainScope, string requestedDomainScope) =>
        entryDomainScope == "*"
        || entryDomainScope.Equals(requestedDomainScope, StringComparison.OrdinalIgnoreCase);

    private static StageOneAliasEntry Approved(
        string sourceText,
        string aliasText,
        string source = BuiltInSource,
        string domainScope = BuiltInDomainScope) =>
        new(
            SourceText: sourceText,
            AliasText: aliasText,
            AliasLanguage: "zh-Hans",
            Source: source,
            Approved: true,
            DomainScope: domainScope);
}

public static class StageOneHardFactLedgerBuilder
{
    private static readonly Regex AcronymRegex = new(@"\b[A-Z][A-Z0-9]{1,}(?:\s+[0-9])?\b", RegexOptions.Compiled);
    private static readonly Regex MoneyRegex = new(
        @"(?:USD\s*)?\$?\s*(?<amount>\d{1,3}(?:,\d{3})+|\d+)(?:\.\d{2})?\s*(?:USD|dollars?)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FullDateRegex = new(
        @"\b(?<month>January|February|March|April|May|June|July|August|September|October|November|December)\s+(?<day>\d{1,2}),\s*(?<year>\d{4})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MonthDayRegex = new(
        @"\b(?<month>January|February|March|April|May|June|July|August|September|October|November|December)\s+(?<day>\d{1,2})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex WeekdayRegex = new(
        @"\b(?<weekday>Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ClockTimeRegex = new(
        @"\b(?<hour>\d{1,2}):(?<minute>\d{2})\s*(?:a\.?m\.?|p\.?m\.?)?\b|\b(?<hourOnly>\d{1,2})\s*(?:a\.?m\.?|p\.?m\.?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TitleCaseEntityPhraseRegex = new(
        @"\b(?<phrase>(?:[A-Z][a-z]+|[A-Z]{2,})(?:\s+(?:[A-Z][a-z]+|[A-Z]|\d+)){1,})\b",
        RegexOptions.Compiled);
    private static readonly Regex EntityTokenRegex = new(@"[A-Z][a-z]+|[A-Z]{2,}|[A-Z]|\d+", RegexOptions.Compiled);
    private static readonly Regex PercentRegex = new(@"\b(?<value>\d+(?:\.\d+)?)\s*%", RegexOptions.Compiled);
    private static readonly Regex DigitsOnlyRegex = new(@"^\d+(?:,\d{3})*$", RegexOptions.Compiled);
    private static readonly HashSet<string> IgnoredAcronyms = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "NZD", "AUD",
    };
    private static readonly HashSet<string> NonHardFactNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Right", "Please", "Thanks", "Thank", "Hi", "Hello",
        "After", "Both", "Coordinator", "Could", "Each", "Email", "Lunch", "Meals", "Our",
        "Package", "Parking", "Questions", "Quick", "Room", "They", "Volunteer",
        "Weekend", "Your",
        "Because", "Content", "For", "Growth", "Lead", "Manager", "Most", "Once",
        "Operations", "Order", "Let", "Plus", "Roles", "Senior", "Spanish", "Support",
        "Team", "That", "The", "There", "This", "Under", "Unit", "What", "You",
    };
    private static readonly HashSet<string> EntityPhraseSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dashboard", "Hall", "Pantry", "Park", "Portal", "Street", "Terrace", "Workspace",
    };

    public static RewriteFactLedger Build(RewriteFactLedger rawLedger, string sourceText)
    {
        var consumedFactIds = new HashSet<string>(StringComparer.Ordinal);
        var mergedEntityFacts = BuildMergedEntityFacts(rawLedger.Facts, sourceText, consumedFactIds);
        var facts = rawLedger.Facts
            .Where(f => !consumedFactIds.Contains(f.Id))
            .Select(f => NormalizeRawFact(f, sourceText))
            .Where(f => f is not null)
            .Select(f => f!)
            .ToList();
        facts.AddRange(mergedEntityFacts);

        foreach (var acronym in AcronymRegex.Matches(sourceText).Select(m => m.Value.Trim()).Distinct(StringComparer.Ordinal))
        {
            if (IgnoredAcronyms.Contains(acronym) || facts.Any(f => string.Equals(f.Text, acronym, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            facts.Add(new RewriteFact(
                Id: $"fact_acronym_{acronym.ToLowerInvariant().Replace(" ", "_", StringComparison.Ordinal)}",
                Text: acronym,
                Source: "roughDraftReply",
                Importance: RewriteFactImportance.Critical,
                Category: RewriteFactCategory.Identifier,
                CanBeRephrased: false,
                SourceSpan: acronym,
                PreserveMode: RewriteFactPreserveMode.Exact,
                Normalized: acronym));
        }

        return new RewriteFactLedger(facts);
    }

    private static IReadOnlyList<RewriteFact> BuildMergedEntityFacts(
        IReadOnlyList<RewriteFact> rawFacts,
        string sourceText,
        ISet<string> consumedFactIds)
    {
        var rawPersonFactsByText = rawFacts
            .Where(f => f.Category == RewriteFactCategory.Person && !NonHardFactNames.Contains(f.Text))
            .GroupBy(f => f.Text, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var mergedFacts = new List<RewriteFact>();
        var seenPhrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in TitleCaseEntityPhraseRegex.Matches(sourceText))
        {
            var tokens = TrimLeadingEntityStopwords(
                EntityTokenRegex.Matches(match.Groups["phrase"].Value).Select(m => m.Value).ToArray());
            if (tokens.Length < 2)
            {
                continue;
            }

            var phrase = string.Join(" ", tokens);
            if (!seenPhrases.Add(phrase))
            {
                continue;
            }

            var tokenFacts = tokens
                .Select(token => rawPersonFactsByText.TryGetValue(token, out var fact) ? fact : null)
                .Where(f => f is not null)
                .Select(f => f!)
                .ToArray();
            if (!ShouldMergeEntityPhrase(tokens, tokenFacts))
            {
                continue;
            }

            foreach (var fact in tokenFacts)
            {
                consumedFactIds.Add(fact.Id);
            }

            mergedFacts.Add(new RewriteFact(
                Id: $"fact_entity_{Slug(phrase)}",
                Text: phrase,
                Source: "roughDraftReply",
                Importance: RewriteFactImportance.Critical,
                Category: ClassifyMergedEntityPhrase(tokens),
                CanBeRephrased: false,
                SourceSpan: phrase,
                PreserveMode: RewriteFactPreserveMode.ExactOrTranslatedAlias));
        }

        return mergedFacts;
    }

    private static string[] TrimLeadingEntityStopwords(string[] tokens)
    {
        var start = 0;
        while (start < tokens.Length && NonHardFactNames.Contains(tokens[start]))
        {
            start++;
        }

        return tokens.Skip(start).ToArray();
    }

    private static bool ShouldMergeEntityPhrase(IReadOnlyList<string> tokens, IReadOnlyList<RewriteFact> tokenFacts)
    {
        if (tokenFacts.Count >= 2)
        {
            return true;
        }

        return tokenFacts.Count == 1 && IsLetterOrNumberSuffix(tokens[^1]);
    }

    private static bool IsLetterOrNumberSuffix(string token) =>
        token.Length == 1 && token.All(char.IsUpper) || token.All(char.IsDigit);

    private static RewriteFactCategory ClassifyMergedEntityPhrase(IReadOnlyList<string> tokens)
    {
        var last = tokens[^1];
        var previous = tokens.Count > 1 ? tokens[^2] : string.Empty;
        return EntityPhraseSuffixes.Contains(last) || EntityPhraseSuffixes.Contains(previous)
            ? RewriteFactCategory.Identifier
            : RewriteFactCategory.Person;
    }

    private static string Slug(string text) =>
        Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9]+", "_", RegexOptions.CultureInvariant)
            .Trim('_');

    private static RewriteFact? NormalizeRawFact(RewriteFact fact, string sourceText)
    {
        if (fact.Category is RewriteFactCategory.Condition or RewriteFactCategory.NegativeConstraint)
        {
            return null;
        }

        if (fact.Category == RewriteFactCategory.Person && NonHardFactNames.Contains(fact.Text))
        {
            return null;
        }

        if (fact.Category == RewriteFactCategory.Count && !ShouldKeepCount(fact.Text, sourceText))
        {
            return null;
        }

        return fact.Category switch
        {
            RewriteFactCategory.Amount => fact with
            {
                PreserveMode = RewriteFactPreserveMode.Normalized,
                Normalized = NormalizeMoney(fact.Text),
                CanBeRephrased = false,
            },
            RewriteFactCategory.DateOrDeadline => fact with
            {
                PreserveMode = RewriteFactPreserveMode.Normalized,
                Normalized = NormalizeDate(fact.Text),
                CanBeRephrased = false,
            },
            RewriteFactCategory.Count => fact with
            {
                PreserveMode = RewriteFactPreserveMode.Normalized,
                Normalized = NormalizeCount(fact.Text),
                CanBeRephrased = false,
            },
            _ => fact,
        };
    }

    private static bool ShouldKeepCount(string text, string sourceText)
    {
        if (PercentRegex.IsMatch(text))
        {
            return true;
        }

        var digits = text.Replace(",", string.Empty, StringComparison.Ordinal);
        if (!DigitsOnlyRegex.IsMatch(digits))
        {
            return false;
        }

        return AppearsOutsideClockTime(digits, sourceText);
    }

    private static bool AppearsOutsideClockTime(string digits, string sourceText)
    {
        var occurrenceRegex = new Regex($@"(?<!\d){Regex.Escape(digits)}(?!\d)", RegexOptions.Compiled);
        foreach (Match occurrence in occurrenceRegex.Matches(sourceText))
        {
            if (!IsClockTimeComponentOccurrence(sourceText, occurrence.Index, occurrence.Length))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsClockTimeComponentOccurrence(string sourceText, int index, int length) =>
        ClockTimeRegex.Matches(sourceText)
            .Any(m => IsSameCapture(m.Groups["hour"], index, length)
                || IsSameCapture(m.Groups["minute"], index, length)
                || IsSameCapture(m.Groups["hourOnly"], index, length));

    private static bool IsSameCapture(Group group, int index, int length) =>
        group.Success && group.Index == index && group.Length == length;

    private static string? NormalizeMoney(string text)
    {
        var match = MoneyRegex.Match(text);
        return match.Success ? $"USD:{DigitsOnly(match.Groups["amount"].Value)}" : null;
    }

    private static string? NormalizeDate(string text)
    {
        var full = FullDateRegex.Match(text);
        if (full.Success)
        {
            return $"{int.Parse(full.Groups["year"].Value):0000}-{MonthNumber(full.Groups["month"].Value):00}-{int.Parse(full.Groups["day"].Value):00}";
        }

        var monthDay = MonthDayRegex.Match(text);
        if (monthDay.Success)
        {
            return $"{MonthNumber(monthDay.Groups["month"].Value):00}-{int.Parse(monthDay.Groups["day"].Value):00}";
        }

        var weekday = WeekdayRegex.Match(text);
        return weekday.Success ? $"weekday:{weekday.Groups["weekday"].Value.ToLowerInvariant()}" : null;
    }

    private static string? NormalizeCount(string text)
    {
        var percent = PercentRegex.Match(text);
        if (percent.Success)
        {
            return $"{percent.Groups["value"].Value}%";
        }

        var digits = DigitsOnly(text);
        return digits.Length > 0 ? digits : null;
    }

    private static int MonthNumber(string month) => month.ToLowerInvariant() switch
    {
        "january" => 1,
        "february" => 2,
        "march" => 3,
        "april" => 4,
        "may" => 5,
        "june" => 6,
        "july" => 7,
        "august" => 8,
        "september" => 9,
        "october" => 10,
        "november" => 11,
        "december" => 12,
        _ => 0,
    };

    private static string DigitsOnly(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }
}

internal sealed class DeepSeekZhClaimSurvivalJudge(DeepSeekChatClient chat) : IZhClaimSurvivalJudge
{
    private const string SystemPrompt =
        "You verify whether a Chinese translation preserves structured English email claims. "
        + "For each claim, judge whether the Chinese text still asserts the same subject, action, "
        + "object, modality, polarity, time_scope, and condition, allowing faithful Chinese equivalents. "
        + "Use status=yes only when every tuple field survives. Use status=partial when the claim is "
        + "recognizable but weakened, merged, or missing one tuple field. Use status=no when the claim "
        + "is absent, contradicted, or has changed polarity/modality. Return JSON only: "
        + "{\"claims\":[{\"id\":\"C001\",\"status\":\"yes|partial|no\",\"reason\":\"short reason\"}]}";

    public async Task<IReadOnlyDictionary<string, ZhClaimSurvivalVerdict>> JudgeAsync(
        string originalEn,
        string translatedZh,
        IReadOnlyList<RewriteClaim> claims,
        CancellationToken cancellationToken)
    {
        if (claims.Count == 0)
        {
            return new Dictionary<string, ZhClaimSurvivalVerdict>();
        }

        var user = BuildUserPrompt(originalEn, translatedZh, claims);
        var maxTokens = Math.Min(4000, 400 + claims.Count * 160);
        var content = await chat.CompleteAsync(SystemPrompt, user, maxTokens, 0, cancellationToken);
        return ZhClaimSurvivalVerdictParser.Parse(content, claims);
    }

    private static string BuildUserPrompt(
        string originalEn,
        string translatedZh,
        IReadOnlyList<RewriteClaim> claims)
    {
        var claimPayload = claims.Select(c => new
        {
            id = c.Id,
            source_span = c.SourceSpan,
            subject = c.Subject,
            action = c.Action,
            @object = c.Object,
            modality = c.Modality.ToString().ToLowerInvariant(),
            polarity = c.Polarity.ToString().ToLowerInvariant(),
            time_scope = c.TimeScope,
            condition = c.Condition,
            must_preserve = c.MustPreserve,
        });

        return "ORIGINAL ENGLISH:\n" + originalEn
            + "\n\nCHINESE TRANSLATION:\n" + translatedZh
            + "\n\nCLAIMS TO VERIFY:\n" + JsonSerializer.Serialize(claimPayload);
    }
}

internal sealed class DeepSeekZhMinimalRepairer(DeepSeekChatClient chat) : IZhMinimalRepairer
{
    private const string SystemPrompt =
        "你是事实修补器，不是润色器。只修复检查报告列出的问题。"
        + "禁止润色。禁止改写没有问题的句子。禁止新增英文原文没有的信息。"
        + "禁止删除任何限定条件、否定、例外、时间范围、角色关系。"
        + "尽量少改字，只把缺失事实补回去，或把错译事实改回来。"
        + "返回 JSON：{\"repaired_zh\":\"修补后的中文\"}";

    public async Task<string?> RepairAsync(ZhMinimalRepairRequest request, CancellationToken cancellationToken)
    {
        var user = BuildUserPrompt(request);
        var content = await chat.CompleteAsync(SystemPrompt, user, 2600, 0, cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            return doc.RootElement.TryGetProperty("repaired_zh", out var repaired)
                && repaired.ValueKind == JsonValueKind.String
                ? repaired.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildUserPrompt(ZhMinimalRepairRequest request)
    {
        var factIssues = request.Report.FactsDrifted.Select(f => new
        {
            id = f.Id,
            source_text = f.Text,
            type = f.Category.ToString(),
            preserve_mode = f.PreserveMode.ToString(),
            normalized = f.Normalized,
            allowed_aliases = f.AllowedAliases ?? Array.Empty<string>(),
            proposed_aliases = f.ProposedAliases ?? Array.Empty<string>(),
            source_span = f.SourceSpan,
            issue = request.Report.FactDriftReasons.TryGetValue(f.Id, out var reason) ? reason : "missing",
            failure_kind = request.Report.FactChecks.FirstOrDefault(c => string.Equals(c.Id, f.Id, StringComparison.Ordinal))?.FailureKind.ToString(),
            recommended_next_action = request.Report.FactChecks.FirstOrDefault(c => string.Equals(c.Id, f.Id, StringComparison.Ordinal))?.RecommendedNextAction,
        });
        var claimIssues = request.Report.ClaimsDrifted.Select(c =>
        {
            var verdict = request.Report.ClaimVerdicts.TryGetValue(c.Id, out var v)
                ? v
                : new ZhClaimSurvivalVerdict(c.Id, ZhClaimSurvivalStatus.No, "judge_missing");
            return new
            {
                id = c.Id,
                source_span = c.SourceSpan,
                subject = c.Subject,
                action = c.Action,
                @object = c.Object,
                modality = c.Modality.ToString(),
                polarity = c.Polarity.ToString(),
                time_scope = c.TimeScope,
                condition = c.Condition,
                must_preserve = c.MustPreserve,
                status = verdict.Status.ToString(),
                issue = verdict.Reason,
            };
        });
        var payload = new
        {
            case_id = request.CaseId,
            original_en = request.OriginalEn,
            current_zh = request.CurrentZh,
            fact_issues = factIssues,
            claim_issues = claimIssues,
            warnings = request.Report.Warnings,
        };

        return JsonSerializer.Serialize(payload);
    }
}

public static class StageOneHardFactChecker
{
    private static readonly Regex AmountRegex = new(
        @"(?<integer>\d{1,3}(?:,\d{3})+|\d+)(?:\.(?<cents>\d{2}))?",
        RegexOptions.Compiled);

    private static readonly Regex DigitRunRegex = new(@"\d{2,}", RegexOptions.Compiled);
    private static readonly Regex ZhDateRegex = new(
        @"(?<evidence>(?<year>\d{4})年\s*(?<month>\d{1,2})月\s*(?<day>\d{1,2})[日号])",
        RegexOptions.Compiled);
    private static readonly Regex ZhMonthDayRegex = new(
        @"(?<evidence>(?<month>\d{1,2})月\s*(?<day>\d{1,2})[日号])",
        RegexOptions.Compiled);
    private static readonly Regex IsoDateRegex = new(
        @"(?<evidence>(?<year>\d{4})-(?<month>\d{1,2})-(?<day>\d{1,2}))",
        RegexOptions.Compiled);
    private static readonly Regex SlashDateRegex = new(
        @"(?<evidence>(?<month>\d{1,2})/(?<day>\d{1,2})/(?<year>\d{4}))",
        RegexOptions.Compiled);
    private static readonly Regex ZhWeekdayRegex = new(
        @"(?<evidence>(?:星期|周|礼拜)(?<weekday>[一二三四五六日天]))",
        RegexOptions.Compiled);
    private static readonly Regex MoneyCandidateRegex = new(
        @"(?<evidence>(?:USD\s*)?\$?\s*(?<amount>\d{1,3}(?:,\d{3})+|\d+)(?:\.\d{2})?\s*(?:美元|美金|USD)?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PercentCandidateRegex = new(
        @"(?<evidence>(?<value>\d+(?:\.\d+)?)\s*%|百分之\s*(?<value2>\d+(?:\.\d+)?))",
        RegexOptions.Compiled);
    private static readonly Regex DurationCandidateRegex = new(
        @"(?<evidence>(?<value>\d+)\s*(?<unit>天|日|days?|day))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ZhNumeralDurationRegex = new(
        @"(?<evidence>[一二三四五六七八九十两零〇百]+\s*(?:天|日))",
        RegexOptions.Compiled);
    private static readonly Regex NumericCandidateRegex = new(
        @"(?<evidence>(?<value>\d{1,3}(?:,\d{3})+|\d+))",
        RegexOptions.Compiled);
    private static readonly Regex OrdinaryBusinessPhraseRegex = new(
        @"^[a-z][a-z\s\-']{2,}$",
        RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, string[]> KnownExactTranslations = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["SSO"] = new[] { "单点登录" },
        ["API"] = new[] { "接口", "应用程序接口" },
        ["SLA"] = new[] { "服务水平协议" },
    };

    private static readonly string[] GenericEntityPhrases =
    {
        "账单系统",
        "计费系统",
        "账务系统",
        "门户",
        "平台",
        "系统",
    };

    public static ZhFactCheckItem Check(RewriteFact fact, string translatedZh)
    {
        return fact.PreserveMode switch
        {
            RewriteFactPreserveMode.Normalized => CheckNormalized(fact, translatedZh),
            RewriteFactPreserveMode.Translated => CheckTranslated(fact, translatedZh),
            RewriteFactPreserveMode.ExactOrTranslatedAlias => CheckExactOrAlias(fact, translatedZh),
            RewriteFactPreserveMode.Semantic => Ambiguous(
                fact,
                "semantic_check_deferred",
                ZhFactFailureKind.Ambiguous,
                null,
                "manual_review"),
            _ => CheckExact(fact, translatedZh),
        };
    }

    private static ZhFactCheckItem CheckExact(RewriteFact fact, string translatedZh)
    {
        if (ContainsTokenBoundaryAware(translatedZh, fact.Text))
        {
            return Present(fact, ZhFactMatchKind.Exact, fact.Text);
        }

        if (IsClaimLedgerMaterial(fact))
        {
            return Ambiguous(
                fact,
                "This item is a claim-bound condition or constraint, not a hard fact anchor.",
                ZhFactFailureKind.ShouldBeClaimLedger,
                null,
                "demote_to_claim");
        }

        if (IsOverExtractedNonHardFact(fact))
        {
            return Ambiguous(
                fact,
                "This item looks like an ordinary business phrase, not a hard fact anchor.",
                ZhFactFailureKind.OverExtractedNonHardFact,
                null,
                "demote_to_term");
        }

        var translatedEvidence = FindKnownTranslation(fact.Text, translatedZh);
        if (!string.IsNullOrWhiteSpace(translatedEvidence))
        {
            return Missing(
                fact,
                $"{fact.Text} requires exact preservation, but only a translated form was found.",
                ZhFactFailureKind.ExactRequiredButTranslated,
                translatedEvidence,
                "true_repair_needed");
        }

        if (fact.Category == RewriteFactCategory.Person)
        {
            return Ambiguous(
                fact,
                $"{fact.Text} may have been translated or transliterated, but no approved alias is configured.",
                ZhFactFailureKind.AliasNotApproved,
                null,
                "approve_alias");
        }

        return Missing(fact, $"{fact.Text} requires exact preservation.", ZhFactFailureKind.Missing, null, "true_repair_needed");
    }

    private static ZhFactCheckItem CheckExactOrAlias(RewriteFact fact, string translatedZh)
    {
        if (ContainsTokenBoundaryAware(translatedZh, fact.Text))
        {
            return Present(fact, ZhFactMatchKind.Exact, fact.Text);
        }

        foreach (var alias in fact.AllowedAliases ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(alias) && ContainsTokenBoundaryAware(translatedZh, alias))
            {
                return Present(fact, ZhFactMatchKind.Alias, alias);
            }
        }

        foreach (var alias in fact.ProposedAliases ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(alias) && ContainsTokenBoundaryAware(translatedZh, alias))
            {
                return Ambiguous(
                    fact,
                    $"{alias} may be a translated alias for {fact.Text}, but it is not approved.",
                    ZhFactFailureKind.AliasNotApproved,
                    alias,
                    "approve_alias");
            }
        }

        var genericEvidence = fact.Category == RewriteFactCategory.Person
            ? null
            : FindGenericEntityEvidence(translatedZh);
        if (!string.IsNullOrWhiteSpace(genericEvidence))
        {
            return Missing(
                fact,
                $"{fact.Text} was replaced by a generic entity phrase.",
                ZhFactFailureKind.EntityGeneralized,
                genericEvidence,
                "manual_review");
        }

        return Ambiguous(
            fact,
            $"{fact.Text} may have been translated or transliterated, but no approved alias is configured.",
            ZhFactFailureKind.AliasNotApproved,
            null,
            "approve_alias");
    }

    private static ZhFactCheckItem CheckTranslated(RewriteFact fact, string translatedZh)
    {
        var translated = fact.Normalized;
        if (!string.IsNullOrWhiteSpace(translated) && ContainsTokenBoundaryAware(translatedZh, translated))
        {
            return Present(fact, ZhFactMatchKind.TranslatedEquivalent, translated);
        }

        return Missing(
            fact,
            $"Expected translated equivalent {translated ?? fact.Text}.",
            ZhFactFailureKind.Missing,
            null,
            "true_repair_needed");
    }

    private static ZhFactCheckItem CheckNormalized(RewriteFact fact, string translatedZh)
    {
        if (string.IsNullOrWhiteSpace(fact.Normalized))
        {
            return CheckExact(fact, translatedZh);
        }

        var expected = fact.Normalized.Trim();
        var candidates = ExtractNormalizedCandidates(fact.Category, translatedZh).ToArray();
        var exactCandidate = candidates.FirstOrDefault(c => string.Equals(c.Normalized, expected, StringComparison.OrdinalIgnoreCase));
        if (exactCandidate is not null)
        {
            return Present(fact, ZhFactMatchKind.Normalized, exactCandidate.Evidence);
        }

        if (candidates.Length > 0)
        {
            return Changed(
                fact,
                $"Expected normalized value {expected}.",
                candidates[0].Evidence,
                ZhFactFailureKind.ValueChanged,
                "true_repair_needed");
        }

        var gapEvidence = FindNormalizerGapEvidence(fact, translatedZh);
        if (!string.IsNullOrWhiteSpace(gapEvidence))
        {
            return Ambiguous(
                fact,
                $"Expected normalized value {expected}, but the text contains a likely equivalent unsupported by the normalizer.",
                ZhFactFailureKind.NormalizerGap,
                gapEvidence,
                "add_normalizer_case");
        }

        return Missing(
            fact,
            $"Expected normalized value {expected}.",
            ZhFactFailureKind.Missing,
            null,
            "true_repair_needed");
    }

    private static IEnumerable<NormalizedCandidate> ExtractNormalizedCandidates(RewriteFactCategory category, string translatedZh)
    {
        foreach (var candidate in ExtractDateCandidates(translatedZh))
        {
            yield return candidate;
        }

        if (category == RewriteFactCategory.Amount)
        {
            foreach (var candidate in ExtractMoneyCandidates(translatedZh))
            {
                yield return candidate;
            }
        }

        foreach (var candidate in ExtractPercentCandidates(translatedZh))
        {
            yield return candidate;
        }

        foreach (var candidate in ExtractDurationCandidates(translatedZh))
        {
            yield return candidate;
        }

        if (category == RewriteFactCategory.Count)
        {
            foreach (var candidate in ExtractNumericCandidates(translatedZh))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<NormalizedCandidate> ExtractDateCandidates(string text)
    {
        foreach (Match match in ZhDateRegex.Matches(text).Concat(IsoDateRegex.Matches(text)))
        {
            yield return DateCandidate(match);
        }

        foreach (Match match in SlashDateRegex.Matches(text))
        {
            yield return new NormalizedCandidate(
                $"{int.Parse(match.Groups["year"].Value):0000}-{int.Parse(match.Groups["month"].Value):00}-{int.Parse(match.Groups["day"].Value):00}",
                match.Groups["evidence"].Value);
        }

        foreach (Match match in ZhMonthDayRegex.Matches(text))
        {
            yield return new NormalizedCandidate(
                $"{int.Parse(match.Groups["month"].Value):00}-{int.Parse(match.Groups["day"].Value):00}",
                match.Groups["evidence"].Value);
        }

        foreach (Match match in ZhWeekdayRegex.Matches(text))
        {
            yield return new NormalizedCandidate(
                $"weekday:{NormalizeZhWeekday(match.Groups["weekday"].Value)}",
                match.Groups["evidence"].Value);
        }
    }

    private static NormalizedCandidate DateCandidate(Match match) => new(
        $"{int.Parse(match.Groups["year"].Value):0000}-{int.Parse(match.Groups["month"].Value):00}-{int.Parse(match.Groups["day"].Value):00}",
        match.Groups["evidence"].Value);

    private static string NormalizeZhWeekday(string value) => value switch
    {
        "一" => "monday",
        "二" => "tuesday",
        "三" => "wednesday",
        "四" => "thursday",
        "五" => "friday",
        "六" => "saturday",
        "日" or "天" => "sunday",
        _ => value,
    };

    private static IEnumerable<NormalizedCandidate> ExtractMoneyCandidates(string text)
    {
        foreach (Match match in MoneyCandidateRegex.Matches(text))
        {
            var amount = DigitsOnly(match.Groups["amount"].Value);
            if (amount.Length > 0)
            {
                yield return new NormalizedCandidate($"USD:{amount}", match.Groups["evidence"].Value);
            }
        }
    }

    private static IEnumerable<NormalizedCandidate> ExtractPercentCandidates(string text)
    {
        foreach (Match match in PercentCandidateRegex.Matches(text))
        {
            var value = match.Groups["value"].Success ? match.Groups["value"].Value : match.Groups["value2"].Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return new NormalizedCandidate($"{value}%", match.Groups["evidence"].Value);
            }
        }
    }

    private static IEnumerable<NormalizedCandidate> ExtractDurationCandidates(string text)
    {
        foreach (Match match in DurationCandidateRegex.Matches(text))
        {
            yield return new NormalizedCandidate($"{match.Groups["value"].Value}:day", match.Groups["evidence"].Value);
        }
    }

    private static IEnumerable<NormalizedCandidate> ExtractNumericCandidates(string text)
    {
        foreach (Match match in NumericCandidateRegex.Matches(text))
        {
            var value = DigitsOnly(match.Groups["value"].Value);
            if (value.Length > 0)
            {
                yield return new NormalizedCandidate(value, match.Groups["evidence"].Value);
            }
        }
    }

    private static bool ContainsTokenBoundaryAware(string haystack, string needle)
    {
        if (string.IsNullOrWhiteSpace(needle))
        {
            return false;
        }

        if (needle.Any(IsAsciiLetterOrDigit))
        {
            return Regex.IsMatch(
                haystack,
                $@"(?<![A-Za-z0-9]){Regex.Escape(needle)}(?![A-Za-z0-9])",
                RegexOptions.IgnoreCase);
        }

        return haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindKnownTranslation(string sourceText, string translatedZh)
    {
        if (!KnownExactTranslations.TryGetValue(sourceText, out var translations))
        {
            return null;
        }

        return translations.FirstOrDefault(t => ContainsTokenBoundaryAware(translatedZh, t));
    }

    private static string? FindGenericEntityEvidence(string translatedZh) =>
        GenericEntityPhrases
            .Where(p => ContainsTokenBoundaryAware(translatedZh, p))
            .OrderByDescending(p => p.Length)
            .FirstOrDefault();

    private static string? FindNormalizerGapEvidence(RewriteFact fact, string translatedZh)
    {
        if (fact.Category == RewriteFactCategory.Count && (fact.Normalized?.EndsWith(":day", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            var zhNumeralDuration = ZhNumeralDurationRegex.Match(translatedZh);
            if (zhNumeralDuration.Success)
            {
                return zhNumeralDuration.Groups["evidence"].Value;
            }
        }

        return null;
    }

    private static bool IsClaimLedgerMaterial(RewriteFact fact) =>
        fact.Category is RewriteFactCategory.Condition or RewriteFactCategory.NegativeConstraint;

    private static bool IsOverExtractedNonHardFact(RewriteFact fact)
    {
        if (fact.Category is RewriteFactCategory.Policy or RewriteFactCategory.NextStep or RewriteFactCategory.SupportAvailability)
        {
            return true;
        }

        return fact.Category == RewriteFactCategory.Other && OrdinaryBusinessPhraseRegex.IsMatch(fact.Text.Trim());
    }

    private static ZhFactCheckItem Present(RewriteFact fact, ZhFactMatchKind matchKind, string evidence) =>
        new(
            fact.Id,
            fact.Text,
            fact.Category,
            fact.PreserveMode,
            ZhFactCheckStatus.Present,
            matchKind,
            evidence,
            null);

    private static ZhFactCheckItem Missing(
        RewriteFact fact,
        string issue,
        ZhFactFailureKind failureKind,
        string? evidence,
        string? recommendedNextAction) =>
        new(
            fact.Id,
            fact.Text,
            fact.Category,
            fact.PreserveMode,
            ZhFactCheckStatus.Missing,
            ZhFactMatchKind.None,
            evidence,
            issue,
            failureKind,
            recommendedNextAction);

    private static ZhFactCheckItem Changed(
        RewriteFact fact,
        string issue,
        string evidence,
        ZhFactFailureKind failureKind,
        string? recommendedNextAction) =>
        new(
            fact.Id,
            fact.Text,
            fact.Category,
            fact.PreserveMode,
            ZhFactCheckStatus.Changed,
            ZhFactMatchKind.None,
            evidence,
            issue,
            failureKind,
            recommendedNextAction);

    private static ZhFactCheckItem Ambiguous(
        RewriteFact fact,
        string issue,
        ZhFactFailureKind failureKind,
        string? evidence,
        string? recommendedNextAction) =>
        new(
            fact.Id,
            fact.Text,
            fact.Category,
            fact.PreserveMode,
            ZhFactCheckStatus.Ambiguous,
            ZhFactMatchKind.None,
            evidence,
            issue,
            failureKind,
            recommendedNextAction);

    private static bool IsAsciiLetterOrDigit(char ch) =>
        ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';

    private sealed record NormalizedCandidate(string Normalized, string Evidence);

    private static string DigitsOnly(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }
}
