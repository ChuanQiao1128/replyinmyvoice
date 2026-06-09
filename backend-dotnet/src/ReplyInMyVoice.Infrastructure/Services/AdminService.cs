using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class AdminService(
    Func<AppDbContext> dbContextFactory,
    IStripeRefundClient? stripeRefundClient = null,
    AccountService? accountService = null)
{
    public const int RefundReviewCountThreshold = 3;
    public const long RefundReviewAmountThreshold = 2_500;

    private const string PurchaseSource = "PURCHASE";
    private const string NzdCurrency = "nzd";
    private const long DefaultRegistrationThresholdAmountTotal = 6_000_000;
    private const decimal DefaultWarningFraction = 0.80m;
    private const int MaxPageSize = 100;
    private const int AccountingExportPageSize = 500;
    private const int MaxAccountingExportPageSize = 1000;
    private const int AdminCreditExpiryDays = 90;
    private const string AdminCreditSource = "ADMIN";
    private const string AccountingRevenueCsvHeader =
        "date,userRef,sku,amount,currency,paymentIntent,receiptUrl,creditsGranted,creditsConsumed,creditsRemaining";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AccountService _accountService = accountService ?? new AccountService(dbContextFactory);

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
            // Order + page on the AppUser ENTITY (real columns) BEFORE projecting.
            // Projecting to AdminUserListRow first and then ordering by a projected
            // member (x.CreatedAt) cannot be translated by EF Core on SQL Server and
            // throws InvalidOperationException at runtime (the SQLite test path orders
            // in-memory after ToList, so it never reproduces this).
            totalCount = await db.AppUsers.AsNoTracking().CountAsync(cancellationToken);
            users = await db.AppUsers
                .AsNoTracking()
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
                x.StripeReceiptUrl,
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
            .Select(x => new AdminPayment(
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
                     x.SubscriptionStatus == SubscriptionStatus.Testing ||
                     x.SubscriptionStatus == SubscriptionStatus.PastDue,
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
            .Select(x => new AdminStatsCreditRow(
                x.Source,
                x.AmountGranted,
                x.AmountConsumed,
                x.StripeEventId,
                x.StripePaymentIntentId,
                x.StripeSku,
                x.StripeAmountTotal,
                x.StripeCurrency,
                x.GrantedAt))
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
        var gstTurnover = BuildTaxTurnoverReport(DateTimeOffset.UtcNow, creditRows);
        var paymentReconciliation = await GetLatestPaymentReconciliationSummaryAsync(
            db,
            cancellationToken);
        var refundReview = await GetRefundReviewStatsAsync(db, cancellationToken);

        return new AdminStatsResponse(
            totalUsers,
            paidUsers,
            totalUsers - paidUsers,
            usageRows.Sum(x => x.UsedCount),
            usageRows.Sum(x => x.ReservedCount),
            creditRows.Sum(x => Math.Max(x.AmountGranted - x.AmountConsumed, 0)),
            paymentRows.Count,
            paymentRows.Sum(x => x.StripeAmountTotal ?? 0),
            costRows.Sum(),
            gstTurnover,
            paymentReconciliation,
            refundReview);
    }

    public async Task<IReadOnlyList<AdminBillingSupportRequest>> GetBillingSupportQueueAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory();
        var rows = await db.BillingSupportRequests
            .AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.Status == BillingSupportRequestStatus.Open)
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Select(ToAdminBillingSupportRequest)
            .ToList();
    }

    public async Task<AdminBillingSupportRequest?> ResolveBillingSupportRequestAsync(
        string adminExternalAuthUserId,
        string? adminEmail,
        Guid requestId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory();
        var request = await db.BillingSupportRequests
            .Include(x => x.User)
            .AsTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (request is null)
        {
            return null;
        }

        if (request.Status == BillingSupportRequestStatus.Open)
        {
            request.Status = BillingSupportRequestStatus.Resolved;
            request.ResolvedAt = now;
            request.UpdatedAt = now;
            request.RowVersion = Guid.NewGuid();

            db.AdminAuditLogs.Add(new AdminAuditLog
            {
                AdminExternalAuthUserId = adminExternalAuthUserId.Trim(),
                AdminEmail = adminEmail?.Trim() ?? string.Empty,
                Action = "resolve_billing_support_request",
                TargetUserId = request.UserId,
                DetailsJson = JsonSerializer.Serialize(
                    new AdminBillingSupportResolveAuditDetails(
                        request.Id,
                        BillingSupportService.FormatType(request.Type),
                        request.RelatedPaymentIntentId,
                        now),
                    JsonOptions),
                CreatedAt = now,
            });

            await db.SaveChangesAsync(cancellationToken);
        }

        return ToAdminBillingSupportRequest(request);
    }

    public async Task WriteAccountingRevenueCsvAsync(
        Stream output,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        int pageSize = AccountingExportPageSize,
        CancellationToken cancellationToken = default)
    {
        if (toExclusive <= fromInclusive)
        {
            throw new ArgumentException("The export end date must be after the start date.", nameof(toExclusive));
        }

        pageSize = Math.Clamp(pageSize, 1, MaxAccountingExportPageSize);

        await using var db = dbContextFactory();
        await using var writer = new StreamWriter(
            output,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 16 * 1024,
            leaveOpen: true)
        {
            NewLine = "\r\n",
        };

        await writer.WriteLineAsync(AccountingRevenueCsvHeader.AsMemory(), cancellationToken);

        if (db.Database.IsSqlite())
        {
            await WriteAccountingRevenueCsvSqliteAsync(
                db,
                writer,
                fromInclusive,
                toExclusive,
                pageSize,
                cancellationToken);
        }
        else
        {
            await WriteAccountingRevenueCsvRelationalAsync(
                db,
                writer,
                fromInclusive,
                toExclusive,
                pageSize,
                cancellationToken);
        }

        await writer.FlushAsync(cancellationToken);
    }

    public async Task<AdminCreditGrantServiceResult> GrantCreditsAsync(
        string adminExternalAuthUserId,
        string? adminEmail,
        Guid targetUserId,
        AdminCreditGrantRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (request.Amount is null)
        {
            return AdminCreditGrantServiceResult.InvalidRequest("Credit amount is required.");
        }

        if (request.Amount <= 0)
        {
            return AdminCreditGrantServiceResult.InvalidRequest("Credit amount must be greater than zero.");
        }

        await using var db = dbContextFactory();
        var userExists = await db.AppUsers
            .AsNoTracking()
            .AnyAsync(x => x.Id == targetUserId, cancellationToken);
        if (!userExists)
        {
            return AdminCreditGrantServiceResult.UserNotFound("No user exists for the requested id.");
        }

        var expiresAt = now.AddDays(AdminCreditExpiryDays);
        var credit = new RewriteCredit
        {
            UserId = targetUserId,
            Source = AdminCreditSource,
            AmountGranted = request.Amount.Value,
            OriginalAmountGranted = request.Amount.Value,
            AmountConsumed = 0,
            GrantedAt = now,
            ExpiresAt = expiresAt,
        };
        db.RewriteCredits.Add(credit);

        var reason = NormalizeReason(request.Reason);
        var details = new AdminCreditGrantAuditDetails(
            credit.Id,
            AdminCreditSource,
            request.Amount.Value,
            now,
            expiresAt,
            reason);
        db.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminExternalAuthUserId = adminExternalAuthUserId.Trim(),
            AdminEmail = adminEmail?.Trim() ?? string.Empty,
            Action = "grant_credits",
            TargetUserId = targetUserId,
            DetailsJson = JsonSerializer.Serialize(details, JsonOptions),
            CreatedAt = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        return AdminCreditGrantServiceResult.Success(new AdminCreditGrantResponse(
            targetUserId,
            credit.Id,
            AdminCreditSource,
            request.Amount.Value,
            credit.AmountConsumed,
            request.Amount.Value,
            now,
            expiresAt));
    }

    public async Task<AdminDeleteUserServiceResult> DeleteUserAsync(
        string adminExternalAuthUserId,
        string? adminEmail,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var normalizedAdminExternalAuthUserId = adminExternalAuthUserId.Trim();

        string targetExternalAuthUserId;
        await using (var lookupDb = dbContextFactory())
        {
            var user = await lookupDb.AppUsers
                .AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => new
                {
                    x.ExternalAuthUserId,
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                return AdminDeleteUserServiceResult.UserNotFound("No user exists for the requested id.");
            }

            if (ExternalAuthUserId.IsErasedExternalAuthUserId(user.ExternalAuthUserId))
            {
                return AdminDeleteUserServiceResult.Forbidden("account already erased");
            }

            if (string.Equals(
                    user.ExternalAuthUserId,
                    normalizedAdminExternalAuthUserId,
                    StringComparison.Ordinal))
            {
                return AdminDeleteUserServiceResult.Forbidden(
                    "an admin cannot delete their own account from the console");
            }

            targetExternalAuthUserId = user.ExternalAuthUserId;
        }

        await _accountService.DeleteAccountAsync(targetExternalAuthUserId, cancellationToken);

        await using var auditDb = dbContextFactory();
        auditDb.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminExternalAuthUserId = normalizedAdminExternalAuthUserId,
            AdminEmail = adminEmail?.Trim() ?? string.Empty,
            Action = "user.delete",
            TargetUserId = userId,
            DetailsJson = JsonSerializer.Serialize(
                new AdminDeleteUserAuditDetails("erased", now),
                JsonOptions),
            CreatedAt = now,
        });
        await auditDb.SaveChangesAsync(cancellationToken);

        return AdminDeleteUserServiceResult.Success(new AdminDeleteUserResponse(userId, "erased"));
    }

    public async Task<AdminSuspensionServiceResult> SetUserSuspensionAsync(
        string adminExternalAuthUserId,
        string? adminEmail,
        Guid targetUserId,
        bool suspended,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory();
        var user = await db.AppUsers
            .AsTracking()
            .SingleOrDefaultAsync(x => x.Id == targetUserId, cancellationToken);
        if (user is null)
        {
            return AdminSuspensionServiceResult.UserNotFound("No user exists for the requested id.");
        }

        DateTimeOffset? suspendedAt = suspended
            ? user.SuspendedAt ?? now
            : null;

        user.SuspendedAt = suspendedAt;
        user.UpdatedAt = now;
        user.RowVersion = Guid.NewGuid();

        var details = new AdminSuspensionAuditDetails(suspended, suspendedAt);
        db.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminExternalAuthUserId = adminExternalAuthUserId.Trim(),
            AdminEmail = adminEmail?.Trim() ?? string.Empty,
            Action = suspended ? "suspend_user" : "unsuspend_user",
            TargetUserId = targetUserId,
            DetailsJson = JsonSerializer.Serialize(details, JsonOptions),
            CreatedAt = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        return AdminSuspensionServiceResult.Success(new AdminSuspensionResponse(
            targetUserId,
            suspended,
            suspendedAt));
    }

    public async Task<AdminRefundServiceResult> IssueRefundAsync(
        string adminExternalAuthUserId,
        string? adminEmail,
        Guid targetUserId,
        AdminRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        var paymentIntentId = request.PaymentIntentId?.Trim();
        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            return AdminRefundServiceResult.InvalidRequest("A payment intent id is required.");
        }

        if (request.Amount is <= 0)
        {
            return AdminRefundServiceResult.InvalidRequest("Refund amount must be greater than zero.");
        }

        var requestedCurrency = NormalizeCurrency(request.Currency);

        await using var db = dbContextFactory();
        var userExists = await db.AppUsers
            .AsNoTracking()
            .AnyAsync(x => x.Id == targetUserId, cancellationToken);
        if (!userExists)
        {
            return AdminRefundServiceResult.UserNotFound("No user exists for the requested id.");
        }

        var payment = await db.RewriteCredits
            .AsNoTracking()
            .Where(x => x.UserId == targetUserId && x.StripePaymentIntentId == paymentIntentId)
            .Select(x => new AdminRefundPaymentLookup(
                x.StripePaymentIntentId,
                x.StripeAmountTotal,
                x.StripeCurrency))
            .FirstOrDefaultAsync(cancellationToken);
        if (payment is null)
        {
            return AdminRefundServiceResult.PaymentNotFound("No payment exists for the requested user and payment intent.");
        }

        var amount = request.Amount ?? payment.AmountTotal;
        if (amount is not > 0)
        {
            return AdminRefundServiceResult.InvalidRequest("Refund amount must be provided for payments without a stored amount.");
        }

        if (payment.AmountTotal is > 0 && amount > payment.AmountTotal.Value)
        {
            return AdminRefundServiceResult.InvalidRequest("Refund amount cannot exceed the stored payment amount.");
        }

        var currency = requestedCurrency ?? NormalizeCurrency(payment.Currency);
        if (!string.IsNullOrWhiteSpace(requestedCurrency) &&
            !string.IsNullOrWhiteSpace(payment.Currency) &&
            !string.Equals(requestedCurrency, NormalizeCurrency(payment.Currency), StringComparison.OrdinalIgnoreCase))
        {
            return AdminRefundServiceResult.InvalidRequest("Refund currency must match the stored payment currency.");
        }

        var existingRefund = await FindExistingRefundAsync(
            db,
            targetUserId,
            paymentIntentId,
            amount.Value,
            cancellationToken);
        if (existingRefund is not null)
        {
            return AdminRefundServiceResult.Success(new AdminRefundResponse(
                targetUserId,
                paymentIntentId,
                amount.Value,
                existingRefund.Currency ?? currency,
                existingRefund.RefundId,
                AlreadyRefunded: true));
        }

        if (stripeRefundClient is null)
        {
            return AdminRefundServiceResult.RefundUnavailable("Stripe refund services are not configured.");
        }

        var refund = await stripeRefundClient.RefundPaymentAsync(
            new StripeRefundRequest(
                paymentIntentId,
                amount.Value,
                currency,
                CreateRefundIdempotencyKey(targetUserId, paymentIntentId, amount.Value),
                targetUserId),
            cancellationToken);

        var details = new AdminRefundAuditDetails(
            paymentIntentId,
            refund.RefundId,
            amount.Value,
            refund.Currency ?? currency,
            refund.Status);
        db.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminExternalAuthUserId = adminExternalAuthUserId.Trim(),
            AdminEmail = adminEmail?.Trim() ?? string.Empty,
            Action = "refund",
            TargetUserId = targetUserId,
            DetailsJson = JsonSerializer.Serialize(details, JsonOptions),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);

        return AdminRefundServiceResult.Success(new AdminRefundResponse(
            targetUserId,
            paymentIntentId,
            amount.Value,
            refund.Currency ?? currency,
            refund.RefundId,
            AlreadyRefunded: false));
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

    private static TaxTurnoverReportDto BuildTaxTurnoverReport(
        DateTimeOffset now,
        IReadOnlyList<AdminStatsCreditRow> creditRows)
    {
        var windowStart = now.AddMonths(-12);
        var windowRows = creditRows
            .Where(x =>
                x.Source == PurchaseSource &&
                x.StripeAmountTotal is > 0 &&
                x.GrantedAt >= windowStart &&
                x.GrantedAt <= now)
            .ToList();
        var nzdRows = windowRows
            .Where(x => string.Equals(x.StripeCurrency, NzdCurrency, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var grossAmountTotal = nzdRows.Sum(x => x.StripeAmountTotal!.Value);
        var warningAmountTotal = (long)Math.Ceiling(
            DefaultRegistrationThresholdAmountTotal * DefaultWarningFraction);
        var warning = grossAmountTotal >= warningAmountTotal
            ? new TaxTurnoverWarningDto(
                "nz_gst_turnover_threshold_approaching",
                "warning",
                "Rolling 12-month gross NZD revenue is approaching the GST registration threshold.")
            : null;

        return new TaxTurnoverReportDto(
            windowStart,
            now,
            NzdCurrency,
            grossAmountTotal,
            DefaultRegistrationThresholdAmountTotal,
            DefaultWarningFraction,
            warningAmountTotal,
            grossAmountTotal / (decimal)DefaultRegistrationThresholdAmountTotal,
            windowRows.Count - nzdRows.Count,
            warning,
            warning is null
                ? null
                : new TaxTurnoverNotificationResultDto(
                    Attempted: false,
                    Sent: false,
                    Provider: null,
                    Reason: "notification_not_configured"));
    }

    private static async Task<AdminPaymentReconciliationSummary?> GetLatestPaymentReconciliationSummaryAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var rows = await db.StripeReconciliationRuns
            .AsNoTracking()
            .Select(x => new AdminPaymentReconciliationSummary(
                x.CompletedAt,
                x.WindowStart,
                x.WindowEnd,
                x.PaidButNoGrantCount + x.GrantButNoPaymentCount + x.AmountMismatchCount,
                x.PaidButNoGrantCount,
                x.GrantButNoPaymentCount,
                x.AmountMismatchCount,
                x.StripePaymentCount,
                x.PurchaseGrantCount))
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(x => x.LastCompletedAt)
            .ThenByDescending(x => x.WindowEnd)
            .FirstOrDefault();
    }

    private static async Task<AdminRefundReviewStats> GetRefundReviewStatsAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var auditRows = await db.AdminAuditLogs
            .AsNoTracking()
            .Where(x => x.TargetUserId.HasValue && x.Action == "refund")
            .Select(x => new
            {
                x.TargetUserId,
                x.DetailsJson,
            })
            .ToListAsync(cancellationToken);

        var refunds = new List<AdminRefundReviewRow>();
        foreach (var auditRow in auditRows)
        {
            var details = TryParseRefundDetails(auditRow.DetailsJson);
            if (details is null || auditRow.TargetUserId is null)
            {
                continue;
            }

            refunds.Add(new AdminRefundReviewRow(auditRow.TargetUserId.Value, details.Amount));
        }

        var refundRowsByUser = refunds
            .GroupBy(x => x.TargetUserId)
            .Select(x => new
            {
                RefundCount = x.Count(),
                RefundAmount = x.Sum(row => row.Amount),
            })
            .ToList();

        var flaggedUserCount = refundRowsByUser.Count(x =>
            x.RefundCount >= RefundReviewCountThreshold ||
            x.RefundAmount >= RefundReviewAmountThreshold);

        return new AdminRefundReviewStats(
            flaggedUserCount,
            RefundReviewCountThreshold,
            RefundReviewAmountThreshold,
            refunds.Count,
            refunds.Sum(x => x.Amount));
    }

    private static async Task<AdminRefundAuditDetails?> FindExistingRefundAsync(
        AppDbContext db,
        Guid targetUserId,
        string paymentIntentId,
        long amount,
        CancellationToken cancellationToken)
    {
        var auditRows = await db.AdminAuditLogs
            .AsNoTracking()
            .Where(x => x.TargetUserId == targetUserId && x.Action == "refund")
            .Select(x => x.DetailsJson)
            .ToListAsync(cancellationToken);

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

    private static async Task WriteAccountingRevenueCsvRelationalAsync(
        AppDbContext db,
        StreamWriter writer,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var skip = 0;
        while (true)
        {
            var rows = await PaymentCredits(db.RewriteCredits)
                .Where(x => x.GrantedAt >= fromInclusive && x.GrantedAt < toExclusive)
                .OrderBy(x => x.GrantedAt)
                .ThenBy(x => x.Id)
                .Skip(skip)
                .Take(pageSize)
                .Select(x => new AccountingRevenueCreditRow(
                    x.Id,
                    x.UserId,
                    x.GrantedAt,
                    x.StripeSku,
                    x.StripeAmountTotal,
                    x.StripeCurrency,
                    x.StripePaymentIntentId,
                    x.AmountGranted,
                    x.AmountConsumed))
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                return;
            }

            await WriteAccountingRevenueCsvRowsAsync(writer, rows, cancellationToken);
            await writer.FlushAsync(cancellationToken);

            if (rows.Count < pageSize)
            {
                return;
            }

            skip += rows.Count;
        }
    }

    private static async Task WriteAccountingRevenueCsvSqliteAsync(
        AppDbContext db,
        StreamWriter writer,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var skip = 0;
        while (true)
        {
            // SQLite tests cannot translate DateTimeOffset range/order; keep the same bounded
            // page size and filter each fetched page in memory.
            var page = await PaymentCredits(db.RewriteCredits)
                .OrderBy(x => x.Id)
                .Skip(skip)
                .Take(pageSize)
                .Select(x => new AccountingRevenueCreditRow(
                    x.Id,
                    x.UserId,
                    x.GrantedAt,
                    x.StripeSku,
                    x.StripeAmountTotal,
                    x.StripeCurrency,
                    x.StripePaymentIntentId,
                    x.AmountGranted,
                    x.AmountConsumed))
                .ToListAsync(cancellationToken);

            if (page.Count == 0)
            {
                return;
            }

            var rows = page
                .Where(x => x.GrantedAt >= fromInclusive && x.GrantedAt < toExclusive)
                .OrderBy(x => x.GrantedAt)
                .ThenBy(x => x.CreditId)
                .ToList();
            await WriteAccountingRevenueCsvRowsAsync(writer, rows, cancellationToken);
            await writer.FlushAsync(cancellationToken);

            if (page.Count < pageSize)
            {
                return;
            }

            skip += page.Count;
        }
    }

    private static IQueryable<RewriteCredit> PaymentCredits(IQueryable<RewriteCredit> credits) =>
        credits
            .AsNoTracking()
            .Where(x =>
                (x.StripePaymentIntentId != null && x.StripePaymentIntentId != string.Empty) ||
                (x.StripeEventId != null && x.StripeEventId != string.Empty) ||
                (x.StripeSku != null && x.StripeSku != string.Empty) ||
                (x.StripeCurrency != null && x.StripeCurrency != string.Empty) ||
                x.StripeAmountTotal != null ||
                x.Source == "PURCHASE" ||
                x.Source == "Purchase" ||
                x.Source == "purchase");

    private static async Task WriteAccountingRevenueCsvRowsAsync(
        StreamWriter writer,
        IEnumerable<AccountingRevenueCreditRow> rows,
        CancellationToken cancellationToken)
    {
        foreach (var row in rows)
        {
            await writer.WriteLineAsync(FormatAccountingRevenueCsvRow(row).AsMemory(), cancellationToken);
        }
    }

    private static string FormatAccountingRevenueCsvRow(AccountingRevenueCreditRow row)
    {
        var remaining = Math.Max(row.AmountGranted - row.AmountConsumed, 0);
        return string.Join(",", new[]
        {
            CsvField(row.GrantedAt.ToString("O", CultureInfo.InvariantCulture)),
            CsvField(row.UserId.ToString("D")),
            CsvField(row.Sku),
            CsvField(row.AmountTotal?.ToString(CultureInfo.InvariantCulture)),
            CsvField(row.Currency),
            CsvField(row.PaymentIntentId),
            CsvField(null),
            CsvField(row.AmountGranted.ToString(CultureInfo.InvariantCulture)),
            CsvField(row.AmountConsumed.ToString(CultureInfo.InvariantCulture)),
            CsvField(remaining.ToString(CultureInfo.InvariantCulture)),
        });
    }

    private static string CsvField(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static AdminRefundAuditDetails? TryParseRefundDetails(string? detailsJson)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AdminRefundAuditDetails>(detailsJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string CreateRefundIdempotencyKey(
        Guid targetUserId,
        string paymentIntentId,
        long amount) =>
        $"admin-refund:{targetUserId:N}:{paymentIntentId}:{amount}";

    private static string? NormalizeCurrency(string? currency)
    {
        var normalized = currency?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized.ToLowerInvariant();
    }

    private static string? NormalizeReason(string? reason)
    {
        var normalized = reason?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized;
    }

    private static AdminBillingSupportRequest ToAdminBillingSupportRequest(
        BillingSupportRequest request) =>
        new(
            request.Id,
            request.UserId,
            request.User?.Email,
            request.User?.ExternalAuthUserId,
            BillingSupportService.FormatType(request.Type),
            request.RelatedPaymentIntentId,
            request.Message,
            BillingSupportService.FormatStatus(request.Status),
            request.CreatedAt,
            request.UpdatedAt,
            request.ResolvedAt);

    private sealed record AdminUserListRow(
        Guid Id,
        string ExternalAuthUserId,
        string? Email,
        SubscriptionStatus SubscriptionStatus,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record AdminRefundPaymentLookup(
        string? PaymentIntentId,
        long? AmountTotal,
        string? Currency);

    private sealed record AccountingRevenueCreditRow(
        Guid CreditId,
        Guid UserId,
        DateTimeOffset GrantedAt,
        string? Sku,
        long? AmountTotal,
        string? Currency,
        string? PaymentIntentId,
        int AmountGranted,
        int AmountConsumed);

    private sealed record AdminStatsCreditRow(
        string Source,
        int AmountGranted,
        int AmountConsumed,
        string? StripeEventId,
        string? StripePaymentIntentId,
        string? StripeSku,
        long? StripeAmountTotal,
        string? StripeCurrency,
        DateTimeOffset GrantedAt);

    private sealed record AdminRefundReviewRow(Guid TargetUserId, long Amount);
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
    string? Currency,
    string? ReceiptUrl);

public sealed record AdminPayment(
    Guid CreditId,
    string Source,
    string? EventId,
    string? PaymentIntentId,
    string? Sku,
    long? AmountTotal,
    string? Currency,
    string? ReceiptUrl,
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
    decimal CostToDateUsd,
    TaxTurnoverReportDto GstTurnover,
    AdminPaymentReconciliationSummary? PaymentReconciliation,
    AdminRefundReviewStats RefundReview);

public sealed record AdminBillingSupportRequest(
    Guid Id,
    Guid UserId,
    string? UserEmail,
    string? ExternalAuthUserId,
    string Type,
    string? RelatedPaymentIntentId,
    string Message,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt);

public sealed record AdminBillingSupportResolveAuditDetails(
    Guid RequestId,
    string Type,
    string? RelatedPaymentIntentId,
    DateTimeOffset ResolvedAt);

public sealed record AdminPaymentReconciliationSummary(
    DateTimeOffset LastCompletedAt,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    int DiscrepancyCount,
    int PaidButNoGrantCount,
    int GrantButNoPaymentCount,
    int AmountMismatchCount,
    int StripePaymentCount,
    int PurchaseGrantCount);

public sealed record AdminRefundReviewStats(
    int FlaggedUserCount,
    int RefundCountThreshold,
    long RefundAmountThreshold,
    int TotalRefundCount,
    long TotalRefundAmount);

public sealed record AdminCreditGrantRequest(
    int? Amount,
    string? Reason);

public sealed record AdminCreditGrantResponse(
    Guid TargetUserId,
    Guid CreditId,
    string Source,
    int AmountGranted,
    int AmountConsumed,
    int Remaining,
    DateTimeOffset GrantedAt,
    DateTimeOffset? ExpiresAt);

public sealed record AdminCreditGrantServiceResult(
    AdminCreditGrantResultKind Kind,
    AdminCreditGrantResponse? Response,
    string? Detail)
{
    public static AdminCreditGrantServiceResult Success(AdminCreditGrantResponse response) =>
        new(AdminCreditGrantResultKind.Success, response, null);

    public static AdminCreditGrantServiceResult InvalidRequest(string detail) =>
        new(AdminCreditGrantResultKind.InvalidRequest, null, detail);

    public static AdminCreditGrantServiceResult UserNotFound(string detail) =>
        new(AdminCreditGrantResultKind.UserNotFound, null, detail);
}

public enum AdminCreditGrantResultKind
{
    Success,
    InvalidRequest,
    UserNotFound,
}

public sealed record AdminCreditGrantAuditDetails(
    Guid CreditId,
    string Source,
    int AmountGranted,
    DateTimeOffset GrantedAt,
    DateTimeOffset? ExpiresAt,
    string? Reason);

public sealed record AdminDeleteUserResponse(
    Guid UserId,
    string Status);

public sealed record AdminDeleteUserServiceResult(
    AdminDeleteUserResultKind Kind,
    AdminDeleteUserResponse? Response,
    string? Detail)
{
    public static AdminDeleteUserServiceResult Success(AdminDeleteUserResponse response) =>
        new(AdminDeleteUserResultKind.Success, response, null);

    public static AdminDeleteUserServiceResult UserNotFound(string detail) =>
        new(AdminDeleteUserResultKind.UserNotFound, null, detail);

    public static AdminDeleteUserServiceResult Forbidden(string detail) =>
        new(AdminDeleteUserResultKind.Forbidden, null, detail);
}

public enum AdminDeleteUserResultKind
{
    Success,
    UserNotFound,
    Forbidden,
}

public sealed record AdminDeleteUserAuditDetails(
    string Status,
    DateTimeOffset DeletedAt);

public sealed record AdminSuspensionRequest(bool? Suspended);

public sealed record AdminSuspensionResponse(
    Guid TargetUserId,
    bool Suspended,
    DateTimeOffset? SuspendedAt);

public sealed record AdminSuspensionServiceResult(
    AdminSuspensionResultKind Kind,
    AdminSuspensionResponse? Response,
    string? Detail)
{
    public static AdminSuspensionServiceResult Success(AdminSuspensionResponse response) =>
        new(AdminSuspensionResultKind.Success, response, null);

    public static AdminSuspensionServiceResult UserNotFound(string detail) =>
        new(AdminSuspensionResultKind.UserNotFound, null, detail);
}

public enum AdminSuspensionResultKind
{
    Success,
    UserNotFound,
}

public sealed record AdminSuspensionAuditDetails(
    bool Suspended,
    DateTimeOffset? SuspendedAt);

public sealed record AdminRefundRequest(
    string? PaymentIntentId,
    long? Amount,
    string? Currency);

public sealed record AdminRefundResponse(
    Guid TargetUserId,
    string PaymentIntentId,
    long Amount,
    string? Currency,
    string? RefundId,
    bool AlreadyRefunded);

public sealed record AdminRefundServiceResult(
    AdminRefundResultKind Kind,
    AdminRefundResponse? Response,
    string? Detail)
{
    public static AdminRefundServiceResult Success(AdminRefundResponse response) =>
        new(AdminRefundResultKind.Success, response, null);

    public static AdminRefundServiceResult InvalidRequest(string detail) =>
        new(AdminRefundResultKind.InvalidRequest, null, detail);

    public static AdminRefundServiceResult UserNotFound(string detail) =>
        new(AdminRefundResultKind.UserNotFound, null, detail);

    public static AdminRefundServiceResult PaymentNotFound(string detail) =>
        new(AdminRefundResultKind.PaymentNotFound, null, detail);

    public static AdminRefundServiceResult RefundUnavailable(string detail) =>
        new(AdminRefundResultKind.RefundUnavailable, null, detail);
}

public enum AdminRefundResultKind
{
    Success,
    InvalidRequest,
    UserNotFound,
    PaymentNotFound,
    RefundUnavailable,
}

public sealed record AdminRefundAuditDetails(
    string PaymentIntentId,
    string? RefundId,
    long Amount,
    string? Currency,
    string? Status);
