using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class AdminStatsRepository(AppDbContext db) : IAdminStatsRepository
{
    private const string NzdCurrency = "nzd";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AdminStatsDto> GetStatsAsync(
        DateTimeOffset now,
        TaxTurnoverSettings taxTurnoverSettings,
        int refundReviewCountThreshold,
        long refundReviewAmountThreshold,
        CancellationToken ct = default)
    {
        var totalUsers = await db.AppUsers.AsNoTracking().CountAsync(ct);
        var paidUsers = await db.AppUsers
            .AsNoTracking()
            .CountAsync(
                x => x.SubscriptionStatus == SubscriptionStatus.Active ||
                    x.SubscriptionStatus == SubscriptionStatus.Trialing ||
                    x.SubscriptionStatus == SubscriptionStatus.Testing ||
                    x.SubscriptionStatus == SubscriptionStatus.PastDue,
                ct);

        var usageRows = await db.UsagePeriods
            .AsNoTracking()
            .Select(x => new
            {
                x.UsedCount,
                x.ReservedCount,
            })
            .ToListAsync(ct);

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
            .ToListAsync(ct);

        var costRows = await db.RewriteCostLogs
            .AsNoTracking()
            .Select(x => x.TotalEstimatedCostUsd)
            .ToListAsync(ct);

        var paymentRows = creditRows
            .Where(x => IsPaymentCredit(
                x.Source,
                x.StripeEventId,
                x.StripePaymentIntentId,
                x.StripeSku,
                x.StripeAmountTotal,
                x.StripeCurrency))
            .ToList();
        var gstTurnover = BuildTaxTurnoverReport(now, taxTurnoverSettings, creditRows);
        var paymentReconciliation = await GetLatestPaymentReconciliationSummaryAsync(ct);
        var refundReview = await GetRefundReviewStatsAsync(
            refundReviewCountThreshold,
            refundReviewAmountThreshold,
            ct);

        return new AdminStatsDto(
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

    private async Task<AdminPaymentReconciliationSummaryDto?> GetLatestPaymentReconciliationSummaryAsync(
        CancellationToken ct)
    {
        var rows = await db.StripeReconciliationRuns
            .AsNoTracking()
            .Select(x => new AdminPaymentReconciliationSummaryDto(
                x.CompletedAt,
                x.WindowStart,
                x.WindowEnd,
                x.PaidButNoGrantCount + x.GrantButNoPaymentCount + x.AmountMismatchCount,
                x.PaidButNoGrantCount,
                x.GrantButNoPaymentCount,
                x.AmountMismatchCount,
                x.StripePaymentCount,
                x.PurchaseGrantCount))
            .ToListAsync(ct);

        return rows
            .OrderByDescending(x => x.LastCompletedAt)
            .ThenByDescending(x => x.WindowEnd)
            .FirstOrDefault();
    }

    private async Task<AdminRefundReviewStatsDto> GetRefundReviewStatsAsync(
        int refundReviewCountThreshold,
        long refundReviewAmountThreshold,
        CancellationToken ct)
    {
        var auditRows = await db.AdminAuditLogs
            .AsNoTracking()
            .Where(x => x.TargetUserId.HasValue && x.Action == "refund")
            .Select(x => new
            {
                x.TargetUserId,
                x.DetailsJson,
            })
            .ToListAsync(ct);

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
            x.RefundCount >= refundReviewCountThreshold ||
            x.RefundAmount >= refundReviewAmountThreshold);

        return new AdminRefundReviewStatsDto(
            flaggedUserCount,
            refundReviewCountThreshold,
            refundReviewAmountThreshold,
            refunds.Count,
            refunds.Sum(x => x.Amount));
    }

    private static TaxTurnoverReportDto BuildTaxTurnoverReport(
        DateTimeOffset now,
        TaxTurnoverSettings settings,
        IReadOnlyList<AdminStatsCreditRow> creditRows)
    {
        var windowStart = now.AddMonths(-12);
        var windowRows = creditRows
            .Where(x =>
                x.Source == "PURCHASE" &&
                x.StripeAmountTotal is > 0 &&
                x.GrantedAt >= windowStart &&
                x.GrantedAt <= now)
            .ToList();
        var nzdRows = windowRows
            .Where(x => string.Equals(x.StripeCurrency, NzdCurrency, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var grossAmountTotal = nzdRows.Sum(x => x.StripeAmountTotal!.Value);
        var warningAmountTotal = (long)Math.Ceiling(
            settings.RegistrationThresholdAmountTotal * settings.WarningFraction);
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
            settings.RegistrationThresholdAmountTotal,
            settings.WarningFraction,
            warningAmountTotal,
            settings.RegistrationThresholdAmountTotal == 0
                ? 0m
                : grossAmountTotal / (decimal)settings.RegistrationThresholdAmountTotal,
            windowRows.Count - nzdRows.Count,
            warning,
            Notification: null);
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

    private sealed record AdminRefundReviewRow(Guid TargetUserId, long Amount);

    private sealed record AdminRefundAuditDetails(
        string PaymentIntentId,
        string? RefundId,
        long Amount,
        string? Currency,
        string? Status);

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
}
