using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.UseCases.RewriteJob;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class RewriteCostLogger(
    Func<AppDbContext> dbContextFactory,
    ILogger<RewriteCostLogger>? logger = null) : IRewriteCostLogger
{
    public async Task WriteAsync(
        RewriteCostLogEntry entry,
        CancellationToken ct = default)
    {
        try
        {
            await WriteCoreAsync(entry, ct);
        }
        catch (DbUpdateException ex) when (IsRequestIdUniqueConstraintViolation(ex))
        {
            logger?.LogInformation(
                ex,
                "Rewrite cost log already exists for request {RequestId}; skipping duplicate cost log.",
                entry.AttemptId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not RewriteJobAttemptNotFoundException)
        {
            logger?.LogWarning(
                ex,
                "Rewrite cost log write failed for request {RequestId}; continuing rewrite processing.",
                entry.AttemptId);
        }
    }

    private async Task WriteCoreAsync(
        RewriteCostLogEntry entry,
        CancellationToken ct)
    {
        if (entry.ProviderCalls.Count == 0)
        {
            return;
        }

        await using var db = dbContextFactory();
        var requestId = entry.AttemptId.ToString();
        var alreadyLogged = await db.RewriteCostLogs
            .AsNoTracking()
            .AnyAsync(x => x.RequestId == requestId, ct);
        if (alreadyLogged)
        {
            return;
        }

        var attempt = await db.RewriteAttempts
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == entry.AttemptId, ct);
        if (attempt is null)
        {
            throw new RewriteJobAttemptNotFoundException(entry.AttemptId);
        }

        var rates = RewriteCostRates.FromEnvironment();
        var costedCalls = entry.ProviderCalls
            .Select(call => new CostedProviderCall(call, CalculateCost(call, rates)))
            .ToArray();
        var openAiInputTokens = costedCalls.Sum(x => x.Metric.InputTokens ?? 0);
        var openAiOutputTokens = costedCalls.Sum(x => x.Metric.OutputTokens ?? 0);
        var openAiCost = costedCalls.Sum(x => x.EstimatedCostUsd);
        var metadata = ReadResultMetadata(entry.ResultJson);

        var log = new RewriteCostLog
        {
            UserId = attempt.UserId,
            RequestId = requestId,
            StrategyVersion = metadata.Strategy ?? "unknown",
            Scenario = metadata.Scenario ?? "unknown",
            TonePreset = entry.Request.Tone,
            Status = entry.Status,
            ErrorCode = entry.ErrorCode,
            StartedAt = entry.StartedAt,
            FinishedAt = entry.FinishedAt,
            DurationMs = ToDurationMs(entry.FinishedAt - entry.StartedAt),
            InputCharCount = CountInputCharacters(entry.Request),
            DraftWordCount = CountWords(entry.Request.RoughDraftReply),
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
            CreatedAt = entry.FinishedAt,
            UpdatedAt = entry.FinishedAt,
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
                CreatedAt = entry.FinishedAt,
            });
        }

        db.RewriteCostLogs.Add(log);
        await db.SaveChangesAsync(ct);
    }

    private static bool IsRequestIdUniqueConstraintViolation(DbUpdateException exception)
    {
        var message = exception.ToString();
        return message.Contains("IX_RewriteCostLogs_RequestId", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("RewriteCostLogs.RequestId", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal CalculateCost(RewriteEngineCallMetric call, RewriteCostRates rates)
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
        RewriteEngineCallMetric Metric,
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
}
