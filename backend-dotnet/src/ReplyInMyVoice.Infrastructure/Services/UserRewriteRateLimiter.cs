using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public interface IUserRewriteRateLimiter
{
    bool Enabled { get; }

    int LimitPerMinute { get; }

    Task<ApiKeyRateLimitResult> CheckAndIncrementAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public sealed class UserRewriteRateLimiter(
    Func<AppDbContext> dbContextFactory,
    int limitPerMinute) : IUserRewriteRateLimiter
{
    private const int MaxAttempts = 5;

    public bool Enabled => limitPerMinute > 0;

    public int LimitPerMinute => limitPerMinute;

    public async Task<ApiKeyRateLimitResult> CheckAndIncrementAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var windowStart = ToMinuteWindowStart(now);
        var resetAt = windowStart.AddMinutes(1);
        if (!Enabled)
        {
            return ApiKeyRateLimitResult.Allowed(0, 0, resetAt);
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt += 1)
        {
            try
            {
                return await CheckAndIncrementCoreAsync(
                    userId,
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
                return ApiKeyRateLimitResult.Unavailable(limitPerMinute, now);
            }
        }

        return ApiKeyRateLimitResult.Unavailable(limitPerMinute, now);
    }

    private async Task<ApiKeyRateLimitResult> CheckAndIncrementCoreAsync(
        Guid userId,
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

            var counter = await db.UserRewriteRateLimitWindows
                .AsTracking()
                .SingleOrDefaultAsync(
                    x => x.UserId == userId && x.WindowStart == windowStart,
                    cancellationToken);

            if (counter is null)
            {
                counter = new UserRewriteRateLimitWindow
                {
                    UserId = userId,
                    WindowStart = windowStart,
                    Count = 1,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.UserRewriteRateLimitWindows.Add(counter);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ApiKeyRateLimitResult.Allowed(limitPerMinute, counter.Count, resetAt);
            }

            if (counter.Count >= limitPerMinute)
            {
                await transaction.CommitAsync(cancellationToken);
                return ApiKeyRateLimitResult.Limited(limitPerMinute, counter.Count, resetAt);
            }

            counter.Count += 1;
            counter.UpdatedAt = now;
            counter.RowVersion = Guid.NewGuid();
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ApiKeyRateLimitResult.Allowed(limitPerMinute, counter.Count, resetAt);
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
            message.Contains("IX_UserRewriteRateLimitWindows_UserId_WindowStart", StringComparison.OrdinalIgnoreCase) ||
            (message.Contains("UserRewriteRateLimitWindows.UserId", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("UserRewriteRateLimitWindows.WindowStart", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSqliteBusy(Exception exception) =>
        exception is SqliteException { SqliteErrorCode: 5 or 6 } ||
        exception.ToString().Contains("database is locked", StringComparison.OrdinalIgnoreCase) ||
        exception.ToString().Contains("database table is locked", StringComparison.OrdinalIgnoreCase);
}
