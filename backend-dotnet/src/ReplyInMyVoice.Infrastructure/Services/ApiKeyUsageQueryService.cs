using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class ApiKeyUsageQueryService(
    Func<AppDbContext> dbContextFactory,
    AccountService accountService)
{
    // Business reporting days are bucketed in the product's Pacific/Auckland time zone.
    private static readonly TimeZoneInfo BusinessTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");

    public Task<ApiUsageSummaryResponse> GetSummaryAsync(
        string externalAuthUserId,
        string? email,
        CancellationToken cancellationToken) =>
        GetSummaryAsync(externalAuthUserId, email, DateTimeOffset.UtcNow, cancellationToken);

    public async Task<ApiUsageSummaryResponse> GetSummaryAsync(
        string externalAuthUserId,
        string? email,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var account = await accountService.GetOrCreateAccountSummaryAsync(
            externalAuthUserId,
            email,
            cancellationToken);
        var today = ToBusinessDate(now);
        var yesterday = today.AddDays(-1);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var last30Start = today.AddDays(-29);
        var rows = await LoadUsageRowsAsync(account.Id, cancellationToken);
        var eligibleRows = rows
            .Where(x => x.CreatedAt <= now)
            .Select(x => new LocalUsageRow(ToBusinessDate(x.CreatedAt), x.StatusCode))
            .ToList();

        var periodEnd = await GetCurrentPeriodEndAsync(account.Id, cancellationToken);

        return new ApiUsageSummaryResponse(
            CountForDay(eligibleRows, today),
            CountForDay(eligibleRows, yesterday),
            CountForRange(eligibleRows, monthStart, today),
            eligibleRows.Count(x => x.Date >= last30Start && x.Date <= today),
            account.Usage.Quota,
            account.Usage.Used,
            account.Usage.Remaining,
            periodEnd);
    }

    public Task<IReadOnlyList<ApiUsageSeriesPoint>> GetSeriesAsync(
        Guid userId,
        int days,
        CancellationToken cancellationToken) =>
        GetSeriesAsync(userId, DateTimeOffset.UtcNow, days, cancellationToken);

    public async Task<IReadOnlyList<ApiUsageSeriesPoint>> GetSeriesAsync(
        Guid userId,
        DateTimeOffset now,
        int days,
        CancellationToken cancellationToken)
    {
        var boundedDays = Math.Max(days, 1);
        var today = ToBusinessDate(now);
        var start = today.AddDays(-(boundedDays - 1));
        var rows = await LoadUsageRowsAsync(userId, cancellationToken);
        var eligibleRows = rows
            .Where(x => x.CreatedAt <= now)
            .Select(x => new LocalUsageRow(ToBusinessDate(x.CreatedAt), x.StatusCode))
            .Where(x => x.Date >= start && x.Date <= today)
            .ToList();

        return Enumerable.Range(0, boundedDays)
            .Select(offset =>
            {
                var date = start.AddDays(offset);
                var count = CountForDay(eligibleRows, date);
                return new ApiUsageSeriesPoint(
                    date.ToString("yyyy-MM-dd"),
                    count.Calls,
                    count.Succeeded,
                    count.Failed);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ApiUsageRecentItem>> GetRecentAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken)
    {
        var boundedLimit = limit <= 0 ? 50 : Math.Min(limit, 200);
        await using var db = dbContextFactory();
        var query = db.ApiKeyUsages
            .AsNoTracking()
            .Where(x => x.ApiKey != null && x.ApiKey.UserId == userId)
            .Select(x => new RecentUsageRow(
                x.Id,
                x.CreatedAt,
                x.Endpoint,
                x.StatusCode,
                x.LatencyMs,
                x.ApiKey!.Last4));

        if (string.Equals(
                db.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.Sqlite",
                StringComparison.OrdinalIgnoreCase))
        {
            var rows = await query.ToListAsync(cancellationToken);
            return rows
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Take(boundedLimit)
                .Select(ToRecentItem)
                .ToList();
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(boundedLimit)
            .Select(x => new ApiUsageRecentItem(
                x.CreatedAt,
                x.Endpoint,
                x.StatusCode,
                x.LatencyMs,
                x.KeyLast4))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<UsageRow>> LoadUsageRowsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var db = dbContextFactory();
        return await db.ApiKeyUsages
            .AsNoTracking()
            .Where(x => x.ApiKey != null && x.ApiKey.UserId == userId)
            .Select(x => new UsageRow(x.CreatedAt, x.StatusCode))
            .ToListAsync(cancellationToken);
    }

    private async Task<DateTimeOffset?> GetCurrentPeriodEndAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var db = dbContextFactory();
        return await db.AppUsers
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.CurrentPeriodEnd)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static DateOnly ToBusinessDate(DateTimeOffset value)
    {
        var local = TimeZoneInfo.ConvertTime(value, BusinessTimeZone);
        return DateOnly.FromDateTime(local.Date);
    }

    private static ApiUsageCount CountForDay(
        IReadOnlyList<LocalUsageRow> rows,
        DateOnly date) =>
        CountForRange(rows, date, date);

    private static ApiUsageCount CountForRange(
        IReadOnlyList<LocalUsageRow> rows,
        DateOnly start,
        DateOnly end)
    {
        var matched = rows
            .Where(x => x.Date >= start && x.Date <= end)
            .ToList();
        var succeeded = matched.Count(x => IsSucceeded(x.StatusCode));
        return new ApiUsageCount(
            matched.Count,
            succeeded,
            matched.Count - succeeded);
    }

    private static bool IsSucceeded(int statusCode) =>
        statusCode is 200 or 202;

    private static ApiUsageRecentItem ToRecentItem(RecentUsageRow row) =>
        new(
            row.CreatedAt,
            row.Endpoint,
            row.StatusCode,
            row.LatencyMs,
            row.KeyLast4);

    private sealed record UsageRow(DateTimeOffset CreatedAt, int StatusCode);

    private sealed record LocalUsageRow(DateOnly Date, int StatusCode);

    private sealed record RecentUsageRow(
        Guid Id,
        DateTimeOffset CreatedAt,
        string Endpoint,
        int StatusCode,
        int? LatencyMs,
        string? KeyLast4);
}

public sealed record ApiUsageSummaryResponse(
    ApiUsageCount Today,
    ApiUsageCount Yesterday,
    ApiUsageCount MonthToDate,
    int Last30dCalls,
    int Quota,
    int Used,
    int Remaining,
    DateTimeOffset? PeriodEnd);

public sealed record ApiUsageCount(int Calls, int Succeeded, int Failed);

public sealed record ApiUsageSeriesPoint(
    string Date,
    int Calls,
    int Succeeded,
    int Failed);

public sealed record ApiUsageRecentItem(
    DateTimeOffset CreatedAt,
    string Endpoint,
    int StatusCode,
    int? LatencyMs,
    string? KeyLast4);
