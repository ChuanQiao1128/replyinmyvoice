using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class AdminUserRepository(AppDbContext db) : IAdminUserRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AdminUsersListDto> ListUsersAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken ct = default)
    {
        var skip = (page - 1) * pageSize;
        var searchTerm = NormalizeSearch(search);
        var query = ApplyUserSearch(db.AppUsers.AsNoTracking(), searchTerm);

        int totalCount;
        List<AdminUserListRow> users;
        if (db.Database.IsSqlite())
        {
            var userRows = await query
                .Select(x => new AdminUserListRow(
                    x.Id,
                    x.ExternalAuthUserId,
                    x.Email,
                    x.SubscriptionStatus,
                    x.CreatedAt,
                    x.UpdatedAt))
                .ToListAsync(ct);
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
            totalCount = await query.CountAsync(ct);
            users = await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .Skip(skip)
                .Take(pageSize)
                .Select(x => new AdminUserListRow(
                    x.Id,
                    x.ExternalAuthUserId,
                    x.Email,
                    x.SubscriptionStatus,
                    x.CreatedAt,
                    x.UpdatedAt))
                .ToListAsync(ct);
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
            .ToListAsync(ct);
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
            .ToListAsync(ct);
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
            .ToListAsync(ct);
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

                return new AdminUserListItemDto(
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
        return new AdminUsersListDto(page, pageSize, totalCount, totalPages, items);
    }

    public async Task<AdminUserDetailDto?> GetUserDetailAsync(
        Guid userId,
        CancellationToken ct = default)
    {
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
            .SingleOrDefaultAsync(ct);

        if (user is null)
        {
            return null;
        }

        var usageRows = await db.UsagePeriods
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new AdminUsagePeriodDto(
                x.Id,
                x.PeriodKey,
                x.QuotaLimit,
                x.UsedCount,
                x.ReservedCount,
                x.PeriodStart,
                x.PeriodEnd,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(ct);
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
                x.StripeReceiptUrl,
            })
            .ToListAsync(ct);
        creditRows = creditRows
            .OrderByDescending(x => x.GrantedAt)
            .ToList();

        var credits = creditRows
            .Select(x => new AdminCreditDto(
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
                x.StripeCurrency,
                x.StripeReceiptUrl))
            .ToList();

        var payments = creditRows
            .Where(x => IsPaymentCredit(
                x.Source,
                x.StripeEventId,
                x.StripePaymentIntentId,
                x.StripeSku,
                x.StripeAmountTotal,
                x.StripeCurrency))
            .Select(x => new AdminPaymentDto(
                x.Id,
                x.Source,
                x.StripeEventId,
                x.StripePaymentIntentId,
                x.StripeSku,
                x.StripeAmountTotal,
                x.StripeCurrency,
                x.StripeReceiptUrl,
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
            .ToListAsync(ct);

        return new AdminUserDetailDto(
            user.Id,
            user.ExternalAuthUserId,
            user.Email,
            user.CreatedAt,
            user.UpdatedAt,
            new AdminSubscriptionSummaryDto(
                user.SubscriptionStatus.ToString(),
                user.StripeCustomerId,
                user.StripeSubscriptionId,
                user.CurrentPeriodEnd),
            usage,
            credits,
            payments,
            costRows.Sum());
    }

    public async Task<bool> UserExistsAsync(
        Guid userId,
        CancellationToken ct = default) =>
        await db.AppUsers
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId, ct);

    public async Task AddCreditAsync(
        RewriteCredit credit,
        CancellationToken ct = default)
    {
        await db.RewriteCredits.AddAsync(credit, ct);
    }

    public async Task AddAuditLogAsync(
        AdminAuditLog auditLog,
        CancellationToken ct = default)
    {
        await db.AdminAuditLogs.AddAsync(auditLog, ct);
    }

    public async Task<AdminSuspensionMutationDto?> SetUserSuspensionAsync(
        Guid userId,
        bool suspended,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var user = await db.AppUsers
            .AsTracking()
            .SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null)
        {
            return null;
        }

        var suspendedAt = suspended
            ? user.SuspendedAt ?? now
            : (DateTimeOffset?)null;

        user.SuspendedAt = suspendedAt;
        user.UpdatedAt = now;
        user.RowVersion = Guid.NewGuid();

        return new AdminSuspensionMutationDto(user.Id, suspended, suspendedAt);
    }

    public async Task<AdminRefundAuditDetailsDto?> FindRefundAuditDetailsAsync(
        Guid targetUserId,
        string paymentIntentId,
        long amount,
        CancellationToken ct = default)
    {
        var auditRows = await db.AdminAuditLogs
            .AsNoTracking()
            .Where(x => x.TargetUserId == targetUserId && x.Action == "refund")
            .Select(x => x.DetailsJson)
            .ToListAsync(ct);

        foreach (var detailsJson in auditRows)
        {
            var details = TryParseRefundDetails(detailsJson);
            if (details is not null &&
                string.Equals(details.PaymentIntentId, paymentIntentId, StringComparison.Ordinal) &&
                details.Amount == amount)
            {
                return details;
            }
        }

        return null;
    }

    public async Task<AdminDeleteUserLookupDto?> GetDeleteUserLookupAsync(
        Guid userId,
        CancellationToken ct = default) =>
        await db.AppUsers
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new AdminDeleteUserLookupDto(
                x.Id,
                x.ExternalAuthUserId,
                x.StripeSubscriptionId))
            .SingleOrDefaultAsync(ct);

    public async Task<bool> EraseUserAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var user = await db.AppUsers
            .AsTracking()
            .SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null || IsErasedExternalAuthUserId(user.ExternalAuthUserId))
        {
            return false;
        }

        var attempts = await db.RewriteAttempts
            .AsTracking()
            .Where(x => x.UserId == user.Id)
            .ToListAsync(ct);
        var usagePeriods = await db.UsagePeriods
            .AsTracking()
            .Where(x => x.UserId == user.Id)
            .ToListAsync(ct);
        var reservations = await db.UsageReservations
            .AsTracking()
            .Where(x => x.UserId == user.Id)
            .ToListAsync(ct);
        var credits = await db.RewriteCredits
            .AsTracking()
            .Where(x => x.UserId == user.Id)
            .ToListAsync(ct);
        var promoRedemptions = await db.PromoCodeRedemptions
            .AsTracking()
            .Where(x => x.UserId == user.Id)
            .ToListAsync(ct);
        var billingSupportRequests = await db.BillingSupportRequests
            .AsTracking()
            .Where(x => x.UserId == user.Id)
            .ToListAsync(ct);

        user.ExternalAuthUserId = CreateErasedExternalAuthUserId(user.Id);
        user.Email = null;
        user.SubscriptionStatus = SubscriptionStatus.Canceled;
        user.CurrentPeriodEnd = null;
        user.UpdatedAt = now;
        user.RowVersion = Guid.NewGuid();

        foreach (var attempt in attempts)
        {
            attempt.IdempotencyKey = CreateErasedChildToken(attempt.Id);
            attempt.RequestHash = "erased";
            attempt.RequestJson = "{}";
            attempt.ResultJson = null;
            attempt.ErrorMessage = null;
            attempt.ExpiresAt = now;
            if (attempt.Status is RewriteAttemptStatus.Pending or RewriteAttemptStatus.Processing)
            {
                attempt.Status = RewriteAttemptStatus.Failed;
                attempt.ErrorCode = "account_erased";
                attempt.CompletedAt = now;
            }

            attempt.RowVersion = Guid.NewGuid();
        }

        foreach (var period in usagePeriods)
        {
            period.PeriodKey = CreateErasedChildToken(period.Id);
            period.QuotaLimit = 0;
            period.UsedCount = 0;
            period.ReservedCount = 0;
            period.PeriodStart = null;
            period.PeriodEnd = null;
            period.UpdatedAt = now;
            period.RowVersion = Guid.NewGuid();
        }

        foreach (var reservation in reservations)
        {
            reservation.Status = UsageReservationStatus.Released;
            reservation.FinalizedAt = null;
            reservation.ReleasedAt = now;
            reservation.ExpiresAt = now;
            reservation.RowVersion = Guid.NewGuid();
        }

        foreach (var credit in credits)
        {
            credit.Source = "ERASED";
            credit.AmountGranted = 0;
            credit.OriginalAmountGranted = null;
            credit.AmountConsumed = 0;
            credit.ExpiresAt = now;
            credit.RowVersion = Guid.NewGuid();
        }

        foreach (var redemption in promoRedemptions)
        {
            redemption.RedeemIpHash = null;
            redemption.RowVersion = Guid.NewGuid();
        }

        foreach (var billingSupportRequest in billingSupportRequests)
        {
            billingSupportRequest.RelatedPaymentIntentId = null;
            billingSupportRequest.Message = "erased";
            billingSupportRequest.Status = BillingSupportRequestStatus.Resolved;
            billingSupportRequest.ResolvedAt ??= now;
            billingSupportRequest.UpdatedAt = now;
            billingSupportRequest.RowVersion = Guid.NewGuid();
        }

        return true;
    }

    private static IQueryable<AppUser> ApplyUserSearch(
        IQueryable<AppUser> query,
        string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return query;
        }

        return query.Where(x =>
            x.ExternalAuthUserId.Contains(searchTerm) ||
            (x.Email != null && x.Email.Contains(searchTerm)));
    }

    private static string? NormalizeSearch(string? search)
    {
        var normalized = search?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
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

    private static AdminRefundAuditDetailsDto? TryParseRefundDetails(string? detailsJson)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AdminRefundAuditDetailsDto>(detailsJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsErasedExternalAuthUserId(string externalAuthUserId) =>
        externalAuthUserId.StartsWith("erased:", StringComparison.Ordinal);

    private static string CreateErasedExternalAuthUserId(Guid userId) =>
        $"erased:{userId:N}";

    private static string CreateErasedChildToken(Guid id) =>
        $"erased:{id:N}";

    private sealed record AdminUserListRow(
        Guid Id,
        string ExternalAuthUserId,
        string? Email,
        SubscriptionStatus SubscriptionStatus,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
