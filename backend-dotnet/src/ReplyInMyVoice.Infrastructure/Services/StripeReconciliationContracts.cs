using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Infrastructure.Notifications;

namespace ReplyInMyVoice.Infrastructure.Services;

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
