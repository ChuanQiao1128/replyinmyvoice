using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        await db.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken ct = default) =>
        await ExecuteInTransactionAsync(operation, IsolationLevel.Serializable, ct);

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        IsolationLevel isolationLevel,
        CancellationToken ct = default)
    {
        await ExecuteInTransactionAsync(
            async transactionCt =>
            {
                await operation(transactionCt);
                return true;
            },
            isolationLevel,
            ct);
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        IsolationLevel isolationLevel,
        CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            await using var transaction = await db.Database.BeginTransactionAsync(isolationLevel, ct);
            try
            {
                var result = await operation(ct);
                await transaction.CommitAsync(ct);
                return result;
            }
            catch
            {
                db.ChangeTracker.Clear();
                throw;
            }
        });
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        IsolationLevel isolationLevel,
        int maxAttempts,
        CancellationToken ct = default)
    {
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Max attempts must be greater than zero.");
        }

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await ExecuteInTransactionAsync(operation, isolationLevel, ct);
            }
            catch (Exception ex) when (attempt < maxAttempts && IsRetryableTransactionRace(ex))
            {
                db.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(10 * attempt), ct);
            }
        }

        return await ExecuteInTransactionAsync(operation, isolationLevel, ct);
    }

    private static bool IsRetryableTransactionRace(Exception exception) =>
        exception is DbUpdateConcurrencyException ||
        exception is SqliteException { SqliteErrorCode: 5 or 6 } ||
        exception.ToString().Contains("database is locked", StringComparison.OrdinalIgnoreCase) ||
        exception.ToString().Contains("database table is locked", StringComparison.OrdinalIgnoreCase) ||
        (exception is DbUpdateException dbUpdateException && IsRetryableDbUpdateRaceException(dbUpdateException));

    private static bool IsRetryableDbUpdateRaceException(DbUpdateException exception)
    {
        var message = exception.ToString();
        return message.Contains("IX_UsagePeriods_UserId_PeriodKey", StringComparison.OrdinalIgnoreCase) ||
            (message.Contains("UsagePeriods.UserId", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("UsagePeriods.PeriodKey", StringComparison.OrdinalIgnoreCase)) ||
            message.Contains("IX_RewriteAttempts_UserId_IdempotencyKey", StringComparison.OrdinalIgnoreCase) ||
            (message.Contains("RewriteAttempts.UserId", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("RewriteAttempts.IdempotencyKey", StringComparison.OrdinalIgnoreCase)) ||
            message.Contains("serialization", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("deadlock", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("3960", StringComparison.OrdinalIgnoreCase);
    }
}
