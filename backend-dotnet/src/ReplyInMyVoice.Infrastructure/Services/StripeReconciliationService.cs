using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Notifications;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class StripeReconciliationService(
    Func<AppDbContext> dbContextFactory,
    IStripePaymentReconciliationClient stripeClient,
    IStripeReconciliationAlerter? alerter = null,
    ILogger<StripeReconciliationService>? logger = null)
{
    private const string PurchaseSource = "PURCHASE";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<StripeReconciliationReport> ReconcileAsync(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        if (windowEnd <= windowStart)
        {
            throw new ArgumentException("reconciliation_window_invalid", nameof(windowEnd));
        }

        var startedAt = DateTimeOffset.UtcNow;
        var stripePayments = NormalizePayments(await stripeClient.ListPaidPaymentIntentsAsync(
            windowStart,
            windowEnd,
            cancellationToken));
        var paymentIntentIds = stripePayments
            .Select(x => x.PaymentIntentId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        await using var db = dbContextFactory();
        var grants = await LoadPurchaseGrantsAsync(
            db,
            windowStart,
            windowEnd,
            paymentIntentIds,
            cancellationToken);

        var report = BuildReport(
            windowStart,
            windowEnd,
            completedAt,
            stripePayments,
            grants);
        var reportJson = JsonSerializer.Serialize(report, JsonOptions);

        db.StripeReconciliationRuns.Add(new StripeReconciliationRun
        {
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            StripePaymentCount = report.StripePaymentCount,
            PurchaseGrantCount = report.PurchaseGrantCount,
            PaidButNoGrantCount = report.PaidButNoGrantCount,
            GrantButNoPaymentCount = report.GrantButNoPaymentCount,
            AmountMismatchCount = report.AmountMismatchCount,
            ReportJson = reportJson,
        });
        await db.SaveChangesAsync(cancellationToken);

        if (report.DiscrepancyCount > 0)
        {
            logger?.LogWarning(
                "Stripe reconciliation found {DiscrepancyCount} discrepancy rows for {WindowStart:o} to {WindowEnd:o}: {ReportJson}",
                report.DiscrepancyCount,
                windowStart,
                windowEnd,
                reportJson);
            await AlertAsync(report, cancellationToken);
        }
        else
        {
            logger?.LogInformation(
                "Stripe reconciliation completed with no discrepancy rows for {WindowStart:o} to {WindowEnd:o}.",
                windowStart,
                windowEnd);
        }

        return report;
    }

    private async Task AlertAsync(
        StripeReconciliationReport report,
        CancellationToken cancellationToken)
    {
        if (alerter is null)
        {
            return;
        }

        try
        {
            await alerter.AlertAsync(report, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(
                ex,
                "Stripe reconciliation alert failed for {WindowStart:o} to {WindowEnd:o}.",
                report.WindowStart,
                report.WindowEnd);
        }
    }

    private static async Task<IReadOnlyList<PurchaseGrantSnapshot>> LoadPurchaseGrantsAsync(
        AppDbContext db,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        IReadOnlyCollection<string> paymentIntentIds,
        CancellationToken cancellationToken)
    {
        if (db.Database.IsSqlite())
        {
            var rows = await db.RewriteCredits
                .AsNoTracking()
                .Where(x => x.Source == PurchaseSource)
                .Select(x => new PurchaseGrantSnapshot(
                    x.Id,
                    x.StripePaymentIntentId,
                    x.StripeAmountTotal,
                    x.StripeCurrency,
                    x.GrantedAt))
                .ToListAsync(cancellationToken);

            return rows
                .Where(x =>
                    (x.GrantedAt >= windowStart && x.GrantedAt < windowEnd) ||
                    (!string.IsNullOrWhiteSpace(x.PaymentIntentId) && paymentIntentIds.Contains(x.PaymentIntentId)))
                .ToList();
        }

        IQueryable<RewriteCredit> query = db.RewriteCredits
            .AsNoTracking()
            .Where(x => x.Source == PurchaseSource);

        query = paymentIntentIds.Count == 0
            ? query.Where(x => x.GrantedAt >= windowStart && x.GrantedAt < windowEnd)
            : query.Where(x =>
                (x.GrantedAt >= windowStart && x.GrantedAt < windowEnd) ||
                (x.StripePaymentIntentId != null && paymentIntentIds.Contains(x.StripePaymentIntentId)));

        return await query
            .Select(x => new PurchaseGrantSnapshot(
                x.Id,
                x.StripePaymentIntentId,
                x.StripeAmountTotal,
                x.StripeCurrency,
                x.GrantedAt))
            .ToListAsync(cancellationToken);
    }

    private static IReadOnlyList<StripePaidPayment> NormalizePayments(
        IReadOnlyList<StripePaidPayment> stripePayments) =>
        stripePayments
            .Where(x => !string.IsNullOrWhiteSpace(x.PaymentIntentId))
            .Select(x => x with
            {
                PaymentIntentId = x.PaymentIntentId.Trim(),
                Currency = NormalizeCurrency(x.Currency) ?? string.Empty,
            })
            .GroupBy(x => x.PaymentIntentId, StringComparer.Ordinal)
            .Select(x => x.OrderByDescending(row => row.PaidAt).First())
            .ToList();

    private static StripeReconciliationReport BuildReport(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        DateTimeOffset completedAt,
        IReadOnlyList<StripePaidPayment> stripePayments,
        IReadOnlyList<PurchaseGrantSnapshot> grants)
    {
        var paymentByIntent = stripePayments.ToDictionary(x => x.PaymentIntentId, StringComparer.Ordinal);
        var grantsByIntent = grants
            .Where(x => !string.IsNullOrWhiteSpace(x.PaymentIntentId))
            .GroupBy(x => x.PaymentIntentId!.Trim(), StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);
        var discrepancies = new List<StripeReconciliationDiscrepancy>();

        foreach (var payment in stripePayments.OrderBy(x => x.PaidAt).ThenBy(x => x.PaymentIntentId))
        {
            if (!grantsByIntent.TryGetValue(payment.PaymentIntentId, out var matchingGrants) ||
                matchingGrants.Count == 0)
            {
                discrepancies.Add(new StripeReconciliationDiscrepancy(
                    StripeReconciliationDiscrepancyKind.PaidButNoGrant,
                    payment.PaymentIntentId,
                    CreditId: null,
                    StripeAmount: payment.AmountReceived,
                    LedgerAmount: null,
                    StripeCurrency: payment.Currency,
                    LedgerCurrency: null,
                    StripePaidAt: payment.PaidAt,
                    LedgerGrantedAt: null));
                continue;
            }

            var grant = matchingGrants.OrderBy(x => x.GrantedAt).First();
            if (grant.AmountTotal != payment.AmountReceived ||
                !string.Equals(
                    NormalizeCurrency(grant.Currency),
                    NormalizeCurrency(payment.Currency),
                    StringComparison.Ordinal))
            {
                discrepancies.Add(new StripeReconciliationDiscrepancy(
                    StripeReconciliationDiscrepancyKind.AmountMismatch,
                    payment.PaymentIntentId,
                    grant.CreditId,
                    payment.AmountReceived,
                    grant.AmountTotal,
                    payment.Currency,
                    grant.Currency,
                    payment.PaidAt,
                    grant.GrantedAt));
            }
        }

        foreach (var grant in grants
            .Where(x => x.GrantedAt >= windowStart && x.GrantedAt < windowEnd)
            .OrderBy(x => x.GrantedAt)
            .ThenBy(x => x.CreditId))
        {
            var paymentIntentId = grant.PaymentIntentId?.Trim();
            if (!string.IsNullOrWhiteSpace(paymentIntentId) &&
                paymentByIntent.ContainsKey(paymentIntentId))
            {
                continue;
            }

            discrepancies.Add(new StripeReconciliationDiscrepancy(
                StripeReconciliationDiscrepancyKind.GrantButNoPayment,
                string.IsNullOrWhiteSpace(paymentIntentId) ? null : paymentIntentId,
                grant.CreditId,
                StripeAmount: null,
                LedgerAmount: grant.AmountTotal,
                StripeCurrency: null,
                LedgerCurrency: grant.Currency,
                StripePaidAt: null,
                LedgerGrantedAt: grant.GrantedAt));
        }

        return StripeReconciliationReport.Create(
            windowStart,
            windowEnd,
            completedAt,
            stripePayments.Count,
            grants.Count,
            discrepancies);
    }

    private static string? NormalizeCurrency(string? currency)
    {
        var normalized = currency?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized.ToLowerInvariant();
    }

    private sealed record PurchaseGrantSnapshot(
        Guid CreditId,
        string? PaymentIntentId,
        long? AmountTotal,
        string? Currency,
        DateTimeOffset GrantedAt);
}

public interface IStripePaymentReconciliationClient
{
    Task<IReadOnlyList<StripePaidPayment>> ListPaidPaymentIntentsAsync(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken cancellationToken);
}

public interface IStripeReconciliationAlerter
{
    Task AlertAsync(
        StripeReconciliationReport report,
        CancellationToken cancellationToken);
}

public sealed class StripeReconciliationNotificationAlerter(
    IConfiguration configuration,
    INotificationService notificationService,
    ILogger<StripeReconciliationNotificationAlerter> logger) : IStripeReconciliationAlerter
{
    public async Task AlertAsync(
        StripeReconciliationReport report,
        CancellationToken cancellationToken)
    {
        var recipientEmail = ResolveRecipientEmail(configuration);
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            logger.LogWarning("Skipping Stripe reconciliation alert because no admin alert email is configured.");
            return;
        }

        await notificationService.SendAsync(
            NotificationTemplates.PaymentReconciliationDiscrepancy,
            new NotificationRecipient(recipientEmail),
            new PaymentReconciliationNotificationModel(
                report.WindowStart,
                report.WindowEnd,
                report.DiscrepancyCount,
                report.PaidButNoGrantCount,
                report.GrantButNoPaymentCount,
                report.AmountMismatchCount),
            cancellationToken);
    }

    private static string? ResolveRecipientEmail(IConfiguration configuration)
    {
        var explicitEmail = configuration["PAYMENT_ALERT_EMAIL"] ??
            configuration["RECONCILIATION_ALERT_EMAIL"];
        if (!string.IsNullOrWhiteSpace(explicitEmail))
        {
            return explicitEmail.Trim();
        }

        return (configuration["ADMIN_EMAILS"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(x => x.Contains('@', StringComparison.Ordinal));
    }
}

public sealed record StripePaidPayment(
    string PaymentIntentId,
    long AmountReceived,
    string Currency,
    DateTimeOffset PaidAt);

public sealed record StripeReconciliationReport(
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    DateTimeOffset CompletedAt,
    int StripePaymentCount,
    int PurchaseGrantCount,
    int PaidButNoGrantCount,
    int GrantButNoPaymentCount,
    int AmountMismatchCount,
    IReadOnlyList<StripeReconciliationDiscrepancy> Discrepancies)
{
    public int DiscrepancyCount => PaidButNoGrantCount + GrantButNoPaymentCount + AmountMismatchCount;

    public static StripeReconciliationReport Create(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        DateTimeOffset completedAt,
        int stripePaymentCount,
        int purchaseGrantCount,
        IReadOnlyList<StripeReconciliationDiscrepancy> discrepancies)
    {
        return new StripeReconciliationReport(
            windowStart,
            windowEnd,
            completedAt,
            stripePaymentCount,
            purchaseGrantCount,
            discrepancies.Count(x => x.Kind == StripeReconciliationDiscrepancyKind.PaidButNoGrant),
            discrepancies.Count(x => x.Kind == StripeReconciliationDiscrepancyKind.GrantButNoPayment),
            discrepancies.Count(x => x.Kind == StripeReconciliationDiscrepancyKind.AmountMismatch),
            discrepancies);
    }
}

public sealed record StripeReconciliationDiscrepancy(
    StripeReconciliationDiscrepancyKind Kind,
    string? PaymentIntentId,
    Guid? CreditId,
    long? StripeAmount,
    long? LedgerAmount,
    string? StripeCurrency,
    string? LedgerCurrency,
    DateTimeOffset? StripePaidAt,
    DateTimeOffset? LedgerGrantedAt);

public enum StripeReconciliationDiscrepancyKind
{
    PaidButNoGrant,
    GrantButNoPayment,
    AmountMismatch,
}
