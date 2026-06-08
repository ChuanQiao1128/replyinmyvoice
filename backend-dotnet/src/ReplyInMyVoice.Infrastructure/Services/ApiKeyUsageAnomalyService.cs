using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class ApiKeyUsageAnomalyService(
    Func<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    ILogger<ApiKeyUsageAnomalyService>? logger = null)
{
    public const string AlertEventName = "api_key_usage_spike_flagged";

    private const int DefaultWindowMinutes = 60;
    private const double DefaultSpikeMultiplier = 3;
    private const int DefaultAbsoluteCeiling = 500;

    public Task<ApiKeyUsageAnomalyResult> EvaluateAsync(
        Guid apiKeyId,
        CancellationToken cancellationToken) =>
        EvaluateAsync(apiKeyId, DateTimeOffset.UtcNow, cancellationToken);

    public async Task<ApiKeyUsageAnomalyResult> EvaluateAsync(
        Guid apiKeyId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var options = ResolveOptions(configuration);
        var window = TimeSpan.FromMinutes(options.WindowMinutes);
        var currentWindowStart = now.Subtract(window);
        var baselineWindowStart = currentWindowStart.Subtract(window);
        var rows = await LoadUsageCreatedAtAsync(
            apiKeyId,
            baselineWindowStart,
            now,
            cancellationToken);
        var baselineCount = rows.Count(x => x >= baselineWindowStart && x < currentWindowStart);
        var observedCount = rows.Count(x => x >= currentWindowStart && x < now);
        var thresholdCount = baselineCount > 0
            ? (int)Math.Ceiling(baselineCount * options.SpikeMultiplier)
            : options.AbsoluteCeiling;

        var result = Classify(
            apiKeyId,
            observedCount,
            baselineCount,
            thresholdCount,
            options,
            currentWindowStart,
            now,
            baselineWindowStart);

        if (result.IsFlagged)
        {
            logger?.LogWarning(
                "{EventName} api_key_id={ApiKeyId} observed_count={ObservedCount} expected_count={ExpectedCount} threshold_count={ThresholdCount} window_minutes={WindowMinutes} reason={Reason}",
                AlertEventName,
                result.ApiKeyId,
                result.ObservedCount,
                result.ExpectedCount,
                result.ThresholdCount,
                options.WindowMinutes,
                result.Reason);
        }

        return result;
    }

    private async Task<IReadOnlyList<DateTimeOffset>> LoadUsageCreatedAtAsync(
        Guid apiKeyId,
        DateTimeOffset baselineWindowStart,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var db = dbContextFactory();
        var query = db.ApiKeyUsages
            .AsNoTracking()
            .Where(x => x.ApiKeyId == apiKeyId);

        if (!string.Equals(
                db.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.Sqlite",
                StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.CreatedAt >= baselineWindowStart && x.CreatedAt < now);
        }

        var rows = await query
            .Select(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return rows
            .Where(x => x >= baselineWindowStart && x < now)
            .ToList();
    }

    private static ApiKeyUsageAnomalyResult Classify(
        Guid apiKeyId,
        int observedCount,
        int baselineCount,
        int thresholdCount,
        ApiKeyUsageAnomalyOptions options,
        DateTimeOffset currentWindowStart,
        DateTimeOffset currentWindowEnd,
        DateTimeOffset baselineWindowStart)
    {
        if (observedCount == 0)
        {
            return Result("no_usage", false, baselineCount);
        }

        if (observedCount > options.AbsoluteCeiling)
        {
            return Result("absolute_ceiling", true, options.AbsoluteCeiling);
        }

        if (baselineCount > 0 && observedCount >= thresholdCount)
        {
            return Result("baseline_multiplier", true, baselineCount);
        }

        return Result("normal", false, baselineCount);

        ApiKeyUsageAnomalyResult Result(
            string reason,
            bool isFlagged,
            int expectedCount) =>
            new(
                apiKeyId,
                isFlagged,
                observedCount,
                expectedCount,
                thresholdCount,
                reason,
                currentWindowStart,
                currentWindowEnd,
                baselineWindowStart,
                currentWindowStart);
    }

    private static ApiKeyUsageAnomalyOptions ResolveOptions(IConfiguration configuration) =>
        new(
            Clamp(
                ReadInt(
                    configuration,
                    "ApiKeyUsageAnomaly:WindowMinutes",
                    "API_KEY_USAGE_ANOMALY_WINDOW_MINUTES",
                    DefaultWindowMinutes),
                5,
                24 * 60),
            ClampDouble(
                ReadDouble(
                    configuration,
                    "ApiKeyUsageAnomaly:SpikeMultiplier",
                    "API_KEY_USAGE_ANOMALY_SPIKE_MULTIPLIER",
                    DefaultSpikeMultiplier),
                1.1,
                100),
            Clamp(
                ReadInt(
                    configuration,
                    "ApiKeyUsageAnomaly:AbsoluteCeiling",
                    "API_KEY_USAGE_ANOMALY_ABSOLUTE_CEILING",
                    DefaultAbsoluteCeiling),
                1,
                1_000_000));

    private static int ReadInt(
        IConfiguration configuration,
        string sectionKey,
        string envKey,
        int defaultValue) =>
        int.TryParse(configuration[sectionKey] ?? configuration[envKey], out var parsed)
            ? parsed
            : defaultValue;

    private static double ReadDouble(
        IConfiguration configuration,
        string sectionKey,
        string envKey,
        double defaultValue) =>
        double.TryParse(
            configuration[sectionKey] ?? configuration[envKey],
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : defaultValue;

    private static int Clamp(int value, int min, int max) =>
        Math.Min(Math.Max(value, min), max);

    private static double ClampDouble(double value, double min, double max) =>
        Math.Min(Math.Max(value, min), max);

    private sealed record ApiKeyUsageAnomalyOptions(
        int WindowMinutes,
        double SpikeMultiplier,
        int AbsoluteCeiling);
}

public sealed record ApiKeyUsageAnomalyResult(
    Guid ApiKeyId,
    bool IsFlagged,
    int ObservedCount,
    int ExpectedCount,
    int ThresholdCount,
    string Reason,
    DateTimeOffset CurrentWindowStart,
    DateTimeOffset CurrentWindowEnd,
    DateTimeOffset BaselineWindowStart,
    DateTimeOffset BaselineWindowEnd);
