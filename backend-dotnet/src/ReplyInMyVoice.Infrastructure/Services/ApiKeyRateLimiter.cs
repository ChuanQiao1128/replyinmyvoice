using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public interface IApiKeyRateLimiter
{
    Task<ApiKeyRateLimitResult> CheckAndIncrementAsync(
        Guid apiKeyId,
        int rateLimitPerMinute,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public sealed class ApiKeyRateLimiter(Func<AppDbContext> dbContextFactory) : IApiKeyRateLimiter
{
    private const int MaxAttempts = 5;

    public async Task<ApiKeyRateLimitResult> CheckAndIncrementAsync(
        Guid apiKeyId,
        int rateLimitPerMinute,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var windowStart = ToMinuteWindowStart(now);
        var resetAt = windowStart.AddMinutes(1);
        if (rateLimitPerMinute <= 0)
        {
            return ApiKeyRateLimitResult.Limited(0, 0, resetAt);
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt += 1)
        {
            try
            {
                return await CheckAndIncrementCoreAsync(
                    apiKeyId,
                    rateLimitPerMinute,
                    windowStart,
                    resetAt,
                    now,
                    cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                await DelayRetryAsync(attempt, cancellationToken);
            }
            catch (DbUpdateException ex) when (attempt < MaxAttempts && IsRateLimitRaceException(ex))
            {
                await DelayRetryAsync(attempt, cancellationToken);
            }
            catch (SqliteException ex) when (attempt < MaxAttempts && IsSqliteBusy(ex))
            {
                await DelayRetryAsync(attempt, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return ApiKeyRateLimitResult.Unavailable(rateLimitPerMinute, now);
            }
        }

        return ApiKeyRateLimitResult.Unavailable(rateLimitPerMinute, now);
    }

    private async Task<ApiKeyRateLimitResult> CheckAndIncrementCoreAsync(
        Guid apiKeyId,
        int rateLimitPerMinute,
        DateTimeOffset windowStart,
        DateTimeOffset resetAt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var strategyDb = dbContextFactory();
        var strategy = strategyDb.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var db = dbContextFactory();
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var counter = await db.ApiKeyRateLimitWindows
                .AsTracking()
                .SingleOrDefaultAsync(
                    x => x.ApiKeyId == apiKeyId && x.WindowStart == windowStart,
                    cancellationToken);

            if (counter is null)
            {
                counter = new ApiKeyRateLimitWindow
                {
                    ApiKeyId = apiKeyId,
                    WindowStart = windowStart,
                    Count = 1,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.ApiKeyRateLimitWindows.Add(counter);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ApiKeyRateLimitResult.Allowed(rateLimitPerMinute, counter.Count, resetAt);
            }

            if (counter.Count >= rateLimitPerMinute)
            {
                await transaction.CommitAsync(cancellationToken);
                return ApiKeyRateLimitResult.Limited(rateLimitPerMinute, counter.Count, resetAt);
            }

            counter.Count += 1;
            counter.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ApiKeyRateLimitResult.Allowed(rateLimitPerMinute, counter.Count, resetAt);
        });
    }

    private static DateTimeOffset ToMinuteWindowStart(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        return new DateTimeOffset(
            utc.Year,
            utc.Month,
            utc.Day,
            utc.Hour,
            utc.Minute,
            0,
            TimeSpan.Zero);
    }

    private static Task DelayRetryAsync(int attempt, CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromMilliseconds(10 * attempt), cancellationToken);

    private static bool IsRateLimitRaceException(DbUpdateException exception)
    {
        var message = exception.ToString();
        return IsSqliteBusy(exception) ||
            message.Contains("IX_ApiKeyRateLimitWindows_ApiKeyId_WindowStart", StringComparison.OrdinalIgnoreCase) ||
            (message.Contains("ApiKeyRateLimitWindows.ApiKeyId", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("ApiKeyRateLimitWindows.WindowStart", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSqliteBusy(Exception exception) =>
        exception is SqliteException { SqliteErrorCode: 5 or 6 } ||
        exception.ToString().Contains("database is locked", StringComparison.OrdinalIgnoreCase) ||
        exception.ToString().Contains("database table is locked", StringComparison.OrdinalIgnoreCase);
}

public sealed record ApiKeyRateLimitResult(
    int Limit,
    int Calls,
    DateTimeOffset ResetAt,
    bool IsLimited,
    bool IsUnavailable)
{
    public static ApiKeyRateLimitResult Allowed(int limit, int calls, DateTimeOffset resetAt) =>
        new(limit, calls, resetAt, IsLimited: false, IsUnavailable: false);

    public static ApiKeyRateLimitResult Limited(int limit, int calls, DateTimeOffset resetAt) =>
        new(limit, calls, resetAt, IsLimited: true, IsUnavailable: false);

    public static ApiKeyRateLimitResult Unavailable(int limit, DateTimeOffset now) =>
        new(limit, 0, now.AddMinutes(1), IsLimited: false, IsUnavailable: true);

    public int Remaining => Math.Max(0, Limit - Calls);

    public int RetryAfterSeconds(DateTimeOffset now) =>
        Math.Max(1, (int)Math.Ceiling((ResetAt - now).TotalSeconds));
}
