using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class AdminService(Func<AppDbContext> dbContextFactory)
{
    private const int MaxPageSize = 100;

    public async Task<AdminUsersListResponse> GetUsersAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var skip = (page - 1) * pageSize;

        await using var db = dbContextFactory();
        var userQuery = db.AppUsers
            .AsNoTracking()
            .Select(x => new AdminUserListRow(
                x.Id,
                x.ExternalAuthUserId,
                x.Email,
                x.SubscriptionStatus,
                x.CreatedAt,
                x.UpdatedAt));

        int totalCount;
        List<AdminUserListRow> users;
        if (db.Database.IsSqlite())
        {
            var userRows = await userQuery.ToListAsync(cancellationToken);
            totalCount = userRows.Count;
            users = userRows
                .OrderByDescending(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .Skip(skip)
                .Take(pageSize)
                .ToList();
        }
        else
        {
            totalCount = await userQuery.CountAsync(cancellationToken);
            users = await userQuery
                .OrderByDescending(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        var pageUserIds = users.Select(x => x.Id).ToHashSet();
        var usageRows = await db.UsagePeriods
            .AsNoTracking()
            .Where(x => pageUserIds.Contains(x.UserId))
            .Select(x => new
            {
                x.UserId,
                x.UsedCount,
                x.ReservedCount,
            })
            .ToListAsync(cancellationToken);
        var usageByUser = usageRows
            .GroupBy(x => x.UserId)
            .ToDictionary(
                x => x.Key,
                x => new
                {
                    Used = x.Sum(row => row.UsedCount),
                    Reserved = x.Sum(row => row.ReservedCount),
                });

        var creditRows = await db.RewriteCredits
            .AsNoTracking()
            .Where(x => pageUserIds.Contains(x.UserId))
            .Select(x => new
            {
                x.UserId,
                x.AmountGranted,
                x.AmountConsumed,
            })
            .ToListAsync(cancellationToken);
        var creditRemainingByUser = creditRows
            .GroupBy(x => x.UserId)
            .ToDictionary(
                x => x.Key,
                x => x.Sum(row => Math.Max(row.AmountGranted - row.AmountConsumed, 0)));

        var costRows = await db.RewriteCostLogs
            .AsNoTracking()
            .Where(x => x.UserId.HasValue && pageUserIds.Contains(x.UserId.Value))
            .Select(x => new
            {
                x.UserId,
                x.TotalEstimatedCostUsd,
            })
            .ToListAsync(cancellationToken);
        var costByUser = costRows
            .GroupBy(x => x.UserId!.Value)
            .ToDictionary(
                x => x.Key,
                x => x.Sum(row => row.TotalEstimatedCostUsd));

        var items = users
            .Select(x =>
            {
                usageByUser.TryGetValue(x.Id, out var usage);
                creditRemainingByUser.TryGetValue(x.Id, out var creditRemaining);
                costByUser.TryGetValue(x.Id, out var costToDate);

                return new AdminUserListItem(
                    x.Id,
                    x.ExternalAuthUserId,
                    x.Email,
                    x.SubscriptionStatus.ToString(),
                    x.CreatedAt,
                    x.UpdatedAt,
                    usage?.Used ?? 0,
                    usage?.Reserved ?? 0,
                    creditRemaining,
                    costToDate);
            })
            .ToList();

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new AdminUsersListResponse(page, pageSize, totalCount, totalPages, items);
    }

    public async Task<AdminUserDetailResponse?> GetUserDetailAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory();
        var user = await db.AppUsers
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.Id,
                x.ExternalAuthUserId,
                x.Email,
                x.StripeCustomerId,
                x.StripeSubscriptionId,
                x.SubscriptionStatus,
                x.CurrentPeriodEnd,
                x.CreatedAt,
                x.UpdatedAt,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        var usageRows = await db.UsagePeriods
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new AdminUsagePeriod(
                x.Id,
                x.PeriodKey,
                x.QuotaLimit,
                x.UsedCount,
                x.ReservedCount,
                x.PeriodStart,
                x.PeriodEnd,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);
        var usage = usageRows
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

        var creditRows = await db.RewriteCredits
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new
            {
                x.Id,
                x.Source,
                x.AmountGranted,
                x.AmountConsumed,
                x.GrantedAt,
                x.ExpiresAt,
                x.StripeEventId,
                x.StripePaymentIntentId,
                x.StripeSku,
                x.StripeAmountTotal,
                x.StripeCurrency,
            })
            .ToListAsync(cancellationToken);
        creditRows = creditRows
            .OrderByDescending(x => x.GrantedAt)
            .ToList();

        var credits = creditRows
            .Select(x => new AdminCredit(
                x.Id,
                x.Source,
                x.AmountGranted,
                x.AmountConsumed,
                Math.Max(x.AmountGranted - x.AmountConsumed, 0),
                x.GrantedAt,
                x.ExpiresAt,
                x.StripeEventId,
                x.StripePaymentIntentId,
                x.StripeSku,
                x.StripeAmountTotal,
                x.StripeCurrency))
            .ToList();

        var payments = creditRows
            .Where(x => IsPaymentCredit(
                x.Source,
                x.StripeEventId,
                x.StripePaymentIntentId,
                x.StripeSku,
                x.StripeAmountTotal,
                x.StripeCurrency))
            .Select(x => new AdminPayment(
                x.Id,
                x.Source,
                x.StripeEventId,
                x.StripePaymentIntentId,
                x.StripeSku,
                x.StripeAmountTotal,
                x.StripeCurrency,
                x.GrantedAt,
                x.ExpiresAt,
                x.AmountGranted,
                x.AmountConsumed,
                Math.Max(x.AmountGranted - x.AmountConsumed, 0)))
            .ToList();

        var costRows = await db.RewriteCostLogs
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.TotalEstimatedCostUsd)
            .ToListAsync(cancellationToken);
        var costToDate = costRows.Sum();

        return new AdminUserDetailResponse(
            user.Id,
            user.ExternalAuthUserId,
            user.Email,
            user.CreatedAt,
            user.UpdatedAt,
            new AdminSubscriptionSummary(
                user.SubscriptionStatus.ToString(),
                user.StripeCustomerId,
                user.StripeSubscriptionId,
                user.CurrentPeriodEnd),
            usage,
            credits,
            payments,
            costToDate);
    }

    public async Task<AdminStatsResponse> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory();
        var totalUsers = await db.AppUsers.AsNoTracking().CountAsync(cancellationToken);
        var paidUsers = await db.AppUsers
            .AsNoTracking()
            .CountAsync(
                x => x.SubscriptionStatus == SubscriptionStatus.Active ||
                     x.SubscriptionStatus == SubscriptionStatus.Trialing ||
                     x.SubscriptionStatus == SubscriptionStatus.Testing,
                cancellationToken);

        var usageRows = await db.UsagePeriods
            .AsNoTracking()
            .Select(x => new
            {
                x.UsedCount,
                x.ReservedCount,
            })
            .ToListAsync(cancellationToken);

        var creditRows = await db.RewriteCredits
            .AsNoTracking()
            .Select(x => new
            {
                x.Source,
                x.AmountGranted,
                x.AmountConsumed,
                x.StripeEventId,
                x.StripePaymentIntentId,
                x.StripeSku,
                x.StripeAmountTotal,
                x.StripeCurrency,
            })
            .ToListAsync(cancellationToken);

        var costRows = await db.RewriteCostLogs
            .AsNoTracking()
            .Select(x => x.TotalEstimatedCostUsd)
            .ToListAsync(cancellationToken);

        var paymentRows = creditRows
            .Where(x => IsPaymentCredit(
                x.Source,
                x.StripeEventId,
                x.StripePaymentIntentId,
                x.StripeSku,
                x.StripeAmountTotal,
                x.StripeCurrency))
            .ToList();
        return new AdminStatsResponse(
            totalUsers,
            paidUsers,
            totalUsers - paidUsers,
            usageRows.Sum(x => x.UsedCount),
            usageRows.Sum(x => x.ReservedCount),
            creditRows.Sum(x => Math.Max(x.AmountGranted - x.AmountConsumed, 0)),
            paymentRows.Count,
            paymentRows.Sum(x => x.StripeAmountTotal ?? 0),
            costRows.Sum());
    }

    private static bool IsPaymentCredit(
        string source,
        string? stripeEventId,
        string? paymentIntentId,
        string? sku,
        long? amountTotal,
        string? currency) =>
        !string.IsNullOrWhiteSpace(paymentIntentId) ||
        !string.IsNullOrWhiteSpace(stripeEventId) ||
        !string.IsNullOrWhiteSpace(sku) ||
        !string.IsNullOrWhiteSpace(currency) ||
        amountTotal is not null ||
        string.Equals(source, "PURCHASE", StringComparison.OrdinalIgnoreCase);

    private sealed record AdminUserListRow(
        Guid Id,
        string ExternalAuthUserId,
        string? Email,
        SubscriptionStatus SubscriptionStatus,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}

public sealed record AdminUsersListResponse(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyList<AdminUserListItem> Users);

public sealed record AdminUserListItem(
    Guid Id,
    string ExternalAuthUserId,
    string? Email,
    string SubscriptionStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int UsedRewrites,
    int ReservedRewrites,
    int CreditRemaining,
    decimal CostToDateUsd);

public sealed record AdminUserDetailResponse(
    Guid Id,
    string ExternalAuthUserId,
    string? Email,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    AdminSubscriptionSummary Subscription,
    IReadOnlyList<AdminUsagePeriod> Usage,
    IReadOnlyList<AdminCredit> Credits,
    IReadOnlyList<AdminPayment> Payments,
    decimal CostToDateUsd);

public sealed record AdminSubscriptionSummary(
    string Status,
    string? StripeCustomerId,
    string? StripeSubscriptionId,
    DateTimeOffset? CurrentPeriodEnd);

public sealed record AdminUsagePeriod(
    Guid Id,
    string PeriodKey,
    int Quota,
    int Used,
    int Reserved,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminCredit(
    Guid Id,
    string Source,
    int AmountGranted,
    int AmountConsumed,
    int Remaining,
    DateTimeOffset GrantedAt,
    DateTimeOffset? ExpiresAt,
    string? StripeEventId,
    string? PaymentIntentId,
    string? Sku,
    long? AmountTotal,
    string? Currency);

public sealed record AdminPayment(
    Guid CreditId,
    string Source,
    string? EventId,
    string? PaymentIntentId,
    string? Sku,
    long? AmountTotal,
    string? Currency,
    DateTimeOffset GrantedAt,
    DateTimeOffset? ExpiresAt,
    int CreditsGranted,
    int CreditsConsumed,
    int CreditsRemaining);

public sealed record AdminStatsResponse(
    int TotalUsers,
    int PaidUsers,
    int FreeUsers,
    int UsageUsed,
    int UsageReserved,
    int CreditRemaining,
    int PaymentCount,
    long PaymentAmountTotal,
    decimal CostToDateUsd);
