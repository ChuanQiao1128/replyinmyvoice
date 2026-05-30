using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Providers;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class RewriteAttemptNotFoundException : InvalidOperationException
{
    public RewriteAttemptNotFoundException(Guid attemptId)
        : base($"Rewrite attempt {attemptId} was not found.")
    {
        AttemptId = attemptId;
    }

    public RewriteAttemptNotFoundException(Guid attemptId, Exception innerException)
        : base($"Rewrite attempt {attemptId} was not found.", innerException)
    {
        AttemptId = attemptId;
    }

    public Guid AttemptId { get; }
}

public sealed class RewriteJobProcessor(
    Func<AppDbContext> dbContextFactory,
    IRewriteProvider rewriteProvider,
    ILogger<RewriteJobProcessor>? logger = null)
{
    public async Task ProcessAsync(RewriteJob job, CancellationToken cancellationToken)
    {
        var quotaService = new QuotaService(dbContextFactory);
        var now = DateTimeOffset.UtcNow;
        RewriteRequest request;

        await using (var db = dbContextFactory())
        {
            var attempt = await db.RewriteAttempts
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == job.AttemptId, cancellationToken);

            if (attempt is null)
            {
                throw new RewriteAttemptNotFoundException(job.AttemptId);
            }

            if (attempt.Status is RewriteAttemptStatus.Succeeded or RewriteAttemptStatus.Failed or RewriteAttemptStatus.Expired)
            {
                return;
            }

            if (attempt.ExpiresAt <= now)
            {
                await ExecuteAttemptMutationAsync(
                    job.AttemptId,
                    () => quotaService.ReleaseAsync(job.AttemptId, "reservation_expired", now, cancellationToken),
                    cancellationToken);
                return;
            }

            try
            {
                request = JsonSerializer.Deserialize<RewriteRequest>(
                    attempt.RequestJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new JsonException();
            }
            catch (JsonException)
            {
                await ExecuteAttemptMutationAsync(
                    job.AttemptId,
                    () => quotaService.ReleaseAsync(job.AttemptId, "request_json_parse_failed", now, cancellationToken),
                    cancellationToken);
                return;
            }
        }

        var claimed = await ExecuteAttemptMutationAsync(
            job.AttemptId,
            () => quotaService.MarkProcessingAsync(job.AttemptId, now, cancellationToken),
            cancellationToken);
        if (!claimed)
        {
            return;
        }

        var rewriteStartedAt = DateTimeOffset.UtcNow;
        using var providerCallCapture = RewriteProviderCallCapture.Begin();
        var result = await rewriteProvider.RewriteAsync(job.AttemptId, request, cancellationToken);
        var rewriteFinishedAt = DateTimeOffset.UtcNow;
        var providerCalls = providerCallCapture.Calls;

        if (result.Success && IsValidResultJson(result.ResultJson))
        {
            await ExecuteAttemptMutationAsync(
                job.AttemptId,
                () => quotaService.FinalizeSuccessAsync(job.AttemptId, result.ResultJson!, rewriteFinishedAt, cancellationToken),
                cancellationToken);
            await WriteCostLogAsync(
                job.AttemptId,
                request,
                result.ResultJson,
                providerCalls,
                "succeeded",
                null,
                rewriteStartedAt,
                rewriteFinishedAt,
                cancellationToken);
            return;
        }

        var errorCode = result.Success ? "provider_json_parse_failed" : result.ErrorCode ?? "provider_failed";
        await WriteCostLogAsync(
            job.AttemptId,
            request,
            result.ResultJson,
            providerCalls,
            "failed",
            errorCode,
            rewriteStartedAt,
            rewriteFinishedAt,
            cancellationToken);
        await ExecuteAttemptMutationAsync(
            job.AttemptId,
            () => quotaService.ReleaseAsync(
                job.AttemptId,
                errorCode,
                rewriteFinishedAt,
                cancellationToken),
            cancellationToken);
    }

    private async Task ExecuteAttemptMutationAsync(
        Guid attemptId,
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await action();
        }
        catch (InvalidOperationException ex)
        {
            await ThrowIfAttemptIsMissingAsync(attemptId, ex, cancellationToken);
            throw;
        }
    }

    private async Task<T> ExecuteAttemptMutationAsync<T>(
        Guid attemptId,
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            return await action();
        }
        catch (InvalidOperationException ex)
        {
            await ThrowIfAttemptIsMissingAsync(attemptId, ex, cancellationToken);
            throw;
        }
    }

    private async Task ThrowIfAttemptIsMissingAsync(
        Guid attemptId,
        InvalidOperationException innerException,
        CancellationToken cancellationToken)
    {
        await using var db = dbContextFactory();
        var attemptExists = await db.RewriteAttempts
            .AsNoTracking()
            .AnyAsync(x => x.Id == attemptId, cancellationToken);
        if (!attemptExists)
        {
            throw new RewriteAttemptNotFoundException(attemptId, innerException);
        }
    }

    private async Task WriteCostLogAsync(
        Guid attemptId,
        RewriteRequest request,
        string? resultJson,
        IReadOnlyList<RewriteProviderCallMetric> providerCalls,
        string status,
        string? errorCode,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteCostLogCoreAsync(
                attemptId,
                request,
                resultJson,
                providerCalls,
                status,
                errorCode,
                startedAt,
                finishedAt,
                cancellationToken);
        }
        catch (DbUpdateException ex) when (IsRequestIdUniqueConstraintViolation(ex))
        {
            logger?.LogInformation(
                ex,
                "Rewrite cost log already exists for request {RequestId}; skipping duplicate cost log.",
                attemptId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not RewriteAttemptNotFoundException)
        {
            logger?.LogWarning(
                ex,
                "Rewrite cost log write failed for request {RequestId}; continuing rewrite processing.",
                attemptId);
        }
    }

    private async Task WriteCostLogCoreAsync(
        Guid attemptId,
        RewriteRequest request,
        string? resultJson,
        IReadOnlyList<RewriteProviderCallMetric> providerCalls,
        string status,
        string? errorCode,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        CancellationToken cancellationToken)
    {
        if (providerCalls.Count == 0)
        {
            return;
        }

        await using var db = dbContextFactory();
        var requestId = attemptId.ToString();
        var alreadyLogged = await db.RewriteCostLogs
            .AsNoTracking()
            .AnyAsync(x => x.RequestId == requestId, cancellationToken);
        if (alreadyLogged)
        {
            return;
        }

        var attempt = await db.RewriteAttempts
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == attemptId, cancellationToken);
        if (attempt is null)
        {
            throw new RewriteAttemptNotFoundException(attemptId);
        }

        var rates = RewriteCostRates.FromEnvironment();
        var costedCalls = providerCalls
            .Select(call => new CostedProviderCall(call, CalculateCost(call, rates)))
            .ToArray();
        var openAiInputTokens = costedCalls.Sum(x => x.Metric.InputTokens ?? 0);
        var openAiOutputTokens = costedCalls.Sum(x => x.Metric.OutputTokens ?? 0);
        var openAiCost = costedCalls.Sum(x => x.EstimatedCostUsd);
        var metadata = ReadResultMetadata(resultJson);

        var log = new RewriteCostLog
        {
            UserId = attempt.UserId,
            RequestId = requestId,
            StrategyVersion = metadata.Strategy ?? "unknown",
            Scenario = metadata.Scenario ?? "unknown",
            TonePreset = request.Tone,
            Status = status,
            ErrorCode = errorCode,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            DurationMs = ToDurationMs(finishedAt - startedAt),
            InputCharCount = CountInputCharacters(request),
            DraftWordCount = CountWords(request.RoughDraftReply),
            RewriteWordCount = CountWords(metadata.RewrittenText),
            DraftAiLikePercent = metadata.DraftAiLikePercent,
            RewriteAiLikePercent = metadata.RewriteAiLikePercent,
            ChangePoints = metadata.ChangePoints,
            InternalStrategies = metadata.AttemptsUsed ?? 0,
            RepairCandidates = 0,
            RejectedCandidates = metadata.FailedAttempts ?? 0,
            UsedEscalation = costedCalls.Any(x =>
                string.Equals(x.Metric.Model, "gpt-4o", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Metric.Model, "gpt-4.1", StringComparison.OrdinalIgnoreCase)),
            OpenAiInputTokens = openAiInputTokens,
            OpenAiOutputTokens = openAiOutputTokens,
            OpenAiCostUsd = openAiCost,
            SaplingCallCount = 0,
            SaplingCharacters = 0,
            SaplingCostUsd = 0,
            TotalEstimatedCostUsd = openAiCost,
            ModelsUsedJson = JsonSerializer.Serialize(costedCalls
                .Select(x => x.Metric.Model)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToArray()),
            ProviderCallsJson = JsonSerializer.Serialize(costedCalls.Select(x => new
            {
                x.Metric.Provider,
                x.Metric.Role,
                x.Metric.Model,
                x.Metric.InputTokens,
                x.Metric.OutputTokens,
                x.Metric.Characters,
                x.Metric.LatencyMs,
                x.Metric.Success,
                x.Metric.ErrorCode,
                x.EstimatedCostUsd,
            })),
            CreatedAt = finishedAt,
            UpdatedAt = finishedAt,
        };

        foreach (var costedCall in costedCalls)
        {
            log.ProviderCalls.Add(new RewriteProviderCall
            {
                Provider = costedCall.Metric.Provider,
                Role = costedCall.Metric.Role,
                Model = costedCall.Metric.Model,
                InputTokens = costedCall.Metric.InputTokens,
                OutputTokens = costedCall.Metric.OutputTokens,
                Characters = costedCall.Metric.Characters,
                EstimatedCostUsd = costedCall.EstimatedCostUsd,
                LatencyMs = costedCall.Metric.LatencyMs,
                Success = costedCall.Metric.Success,
                ErrorCode = costedCall.Metric.ErrorCode,
                CreatedAt = finishedAt,
            });
        }

        db.RewriteCostLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsRequestIdUniqueConstraintViolation(DbUpdateException exception)
    {
        var message = exception.ToString();
        return message.Contains("IX_RewriteCostLogs_RequestId", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("RewriteCostLogs.RequestId", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal CalculateCost(RewriteProviderCallMetric call, RewriteCostRates rates)
    {
        var inputCost = (call.InputTokens ?? 0) / 1000m * rates.InputPer1K;
        var outputCost = (call.OutputTokens ?? 0) / 1000m * rates.OutputPer1K;
        return decimal.Round(inputCost + outputCost, 6, MidpointRounding.AwayFromZero);
    }

    private static int ToDurationMs(TimeSpan duration) =>
        duration.TotalMilliseconds >= int.MaxValue
            ? int.MaxValue
            : Math.Max(0, (int)duration.TotalMilliseconds);

    private static int CountInputCharacters(RewriteRequest request) =>
        Length(request.MessageToReplyTo) +
        Length(request.RoughDraftReply) +
        Length(request.Audience) +
        Length(request.Purpose) +
        Length(request.WhatHappened) +
        Length(request.FactsToPreserve) +
        Length(request.Tone);

    private static int Length(string? value) => value?.Length ?? 0;

    private static int CountWords(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        return value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static ResultMetadata ReadResultMetadata(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
        {
            return new ResultMetadata(null, null, null, null, null, null, null, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;
            var rewrittenText = ReadString(root, "rewrittenText");
            var naturalness = root.TryGetProperty("naturalness", out var naturalnessValue)
                ? naturalnessValue
                : default;
            var optimization = root.TryGetProperty("optimization", out var optimizationValue)
                ? optimizationValue
                : default;

            return new ResultMetadata(
                rewrittenText,
                ReadString(optimization, "strategy"),
                ReadString(optimization, "scenario"),
                ReadInt(naturalness, "draftAiLikePercent"),
                ReadInt(naturalness, "rewriteAiLikePercent"),
                ReadInt(naturalness, "changePoints"),
                ReadInt(optimization, "attemptsUsed"),
                ReadInt(optimization, "failedAttempts"));
        }
        catch (JsonException)
        {
            return new ResultMetadata(null, null, null, null, null, null, null, null);
        }
    }

    private static string? ReadString(JsonElement value, string propertyName) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int? ReadInt(JsonElement value, string propertyName) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.Number &&
        property.TryGetInt32(out var intValue)
            ? intValue
            : null;

    private sealed record CostedProviderCall(
        RewriteProviderCallMetric Metric,
        decimal EstimatedCostUsd);

    private sealed record ResultMetadata(
        string? RewrittenText,
        string? Strategy,
        string? Scenario,
        int? DraftAiLikePercent,
        int? RewriteAiLikePercent,
        int? ChangePoints,
        int? AttemptsUsed,
        int? FailedAttempts);

    private sealed record RewriteCostRates(decimal InputPer1K, decimal OutputPer1K)
    {
        public static RewriteCostRates FromEnvironment() =>
            new(
                ReadRate(
                    "REWRITE_COST_INPUT_PER_1K",
                    "REWRITE_COST_INPUT_USD_PER_1K",
                    "RewriteCost__InputPer1K",
                    "RewriteCost:InputPer1K"),
                ReadRate(
                    "REWRITE_COST_OUTPUT_PER_1K",
                    "REWRITE_COST_OUTPUT_USD_PER_1K",
                    "RewriteCost__OutputPer1K",
                    "RewriteCost:OutputPer1K"));

        private static decimal ReadRate(params string[] names)
        {
            foreach (var name in names)
            {
                var raw = Environment.GetEnvironmentVariable(name);
                if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) &&
                    value >= 0)
                {
                    return value;
                }
            }

            return 0;
        }
    }

    private static bool IsValidResultJson(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;
            return root.TryGetProperty("rewrittenText", out var rewrittenText) &&
                rewrittenText.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(rewrittenText.GetString()) &&
                root.TryGetProperty("changeSummary", out var changeSummary) &&
                changeSummary.ValueKind == JsonValueKind.Array &&
                root.TryGetProperty("riskNotes", out var riskNotes) &&
                riskNotes.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
