using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class ApiKeyUsageRepository(AppDbContext db) : IApiKeyUsageRepository
{
    public async Task AddAsync(ApiKeyUsage usage, CancellationToken ct = default)
    {
        await db.ApiKeyUsages.AddAsync(usage, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, ApiUsageCountDto>> CountByApiKeyAsync(
        IReadOnlyCollection<Guid> apiKeyIds,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default)
    {
        var keyIds = apiKeyIds.Distinct().ToArray();
        if (keyIds.Length == 0)
        {
            return new Dictionary<Guid, ApiUsageCountDto>();
        }

        var usageQuery = db.ApiKeyUsages
            .AsNoTracking()
            .Where(x => keyIds.Contains(x.ApiKeyId));
        var filtersDatesInMemory = UsesSqlite();
        if (!filtersDatesInMemory)
        {
            usageQuery = usageQuery.Where(x => x.CreatedAt >= windowStart && x.CreatedAt <= windowEnd);
        }

        var usageRows = await usageQuery
            .Select(x => new
            {
                x.ApiKeyId,
                x.CreatedAt,
                x.StatusCode,
            })
            .ToListAsync(ct);
        var countedRows = filtersDatesInMemory
            ? usageRows.Where(x => x.CreatedAt >= windowStart && x.CreatedAt <= windowEnd)
            : usageRows;

        return countedRows
            .GroupBy(x => x.ApiKeyId)
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var calls = x.Count();
                    var succeeded = x.Count(row => row.StatusCode is 200 or 202);
                    return new ApiUsageCountDto(calls, succeeded, calls - succeeded);
                });
    }

    public async Task<IReadOnlyList<ApiUsageRowDto>> ListUsageRowsAsync(
        Guid userId,
        DateTimeOffset windowStart,
        CancellationToken ct = default) =>
        await ApiUsageWindowQuery(userId, windowStart)
            .Select(x => new ApiUsageRowDto(x.CreatedAt, x.StatusCode))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ApiUsageRecentItemDto>> ListRecentAsync(
        Guid userId,
        DateTimeOffset windowStart,
        int limit,
        CancellationToken ct = default)
    {
        var query = ApiUsageWindowQuery(userId, windowStart)
            .Join(
                db.ApiKeys.AsNoTracking(),
                usage => usage.ApiKeyId,
                key => key.Id,
                (usage, key) => new RecentUsageRow(
                    usage.Id,
                    usage.CreatedAt,
                    usage.Endpoint,
                    usage.StatusCode,
                    usage.LatencyMs,
                    key.Last4));

        if (UsesSqlite())
        {
            var rows = await query.ToListAsync(ct);
            return rows
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Take(limit)
                .Select(ToRecentItem)
                .ToList();
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .Select(x => new ApiUsageRecentItemDto(
                x.CreatedAt,
                x.Endpoint,
                x.StatusCode,
                x.LatencyMs,
                x.KeyLast4))
            .ToListAsync(ct);
    }

    private IQueryable<ApiKeyUsage> ApiUsageWindowQuery(
        Guid userId,
        DateTimeOffset windowStart) =>
        db.ApiKeyUsages
            .FromSqlInterpolated($"""
                SELECT u.*
                FROM ApiKeyUsages AS u
                INNER JOIN ApiKeys AS k ON u.ApiKeyId = k.Id
                WHERE k.UserId = {userId}
                  AND u.CreatedAt >= {windowStart}
                """)
            .AsNoTracking();

    private bool UsesSqlite() =>
        string.Equals(
            db.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.Sqlite",
            StringComparison.OrdinalIgnoreCase);

    private static ApiUsageRecentItemDto ToRecentItem(RecentUsageRow row) =>
        new(
            row.CreatedAt,
            row.Endpoint,
            row.StatusCode,
            row.LatencyMs,
            row.KeyLast4);

    private sealed record RecentUsageRow(
        Guid Id,
        DateTimeOffset CreatedAt,
        string Endpoint,
        int StatusCode,
        int? LatencyMs,
        string? KeyLast4);
}
