using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Notifications;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class TaxTurnoverService(
    Func<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    INotificationService? notificationService = null)
{
    private const string PurchaseSource = "PURCHASE";
    private const string NzdCurrency = "nzd";
    private const decimal DefaultRegistrationThresholdNzd = 60_000m;
    private const decimal DefaultWarningFraction = 0.80m;

    public async Task<TaxTurnoverReport> GetRollingTwelveMonthReportAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var windowStart = now.AddMonths(-12);
        await using var db = dbContextFactory();
        var rows = await db.RewriteCredits
            .AsNoTracking()
            .Where(x =>
                x.Source == PurchaseSource &&
                x.StripeAmountTotal.HasValue &&
                x.StripeAmountTotal.Value > 0)
            .Select(x => new
            {
                x.GrantedAt,
                x.StripeAmountTotal,
                x.StripeCurrency,
            })
            .ToListAsync(cancellationToken);

        var windowRows = rows
            .Where(x =>
                x.GrantedAt >= windowStart &&
                x.GrantedAt <= now)
            .ToList();
        var nzdRows = windowRows
            .Where(x =>
                string.Equals(x.StripeCurrency, NzdCurrency, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var grossAmountTotal = nzdRows.Sum(x => x.StripeAmountTotal!.Value);
        var registrationThresholdAmountTotal = ParseNzdMinorAmount(
            configuration["GST_TURNOVER_THRESHOLD_NZD"],
            DefaultRegistrationThresholdNzd);
        var warningFraction = ParseWarningFraction(configuration["GST_TURNOVER_WARNING_FRACTION"]);
        var warningAmountTotal = (long)Math.Ceiling(registrationThresholdAmountTotal * warningFraction);
        var warning = grossAmountTotal >= warningAmountTotal
            ? new TaxTurnoverWarning(
                "nz_gst_turnover_threshold_approaching",
                "warning",
                "Rolling 12-month gross NZD revenue is approaching the GST registration threshold.")
            : null;
        var notification = warning is null
            ? null
            : await TrySendWarningNotificationAsync(
                now,
                grossAmountTotal,
                registrationThresholdAmountTotal,
                warningFraction,
                cancellationToken);

        return new TaxTurnoverReport(
            windowStart,
            now,
            NzdCurrency,
            grossAmountTotal,
            registrationThresholdAmountTotal,
            warningFraction,
            warningAmountTotal,
            registrationThresholdAmountTotal == 0
                ? 0m
                : grossAmountTotal / (decimal)registrationThresholdAmountTotal,
            windowRows.Count - nzdRows.Count,
            warning,
            notification);
    }

    private async Task<TaxTurnoverNotificationResult> TrySendWarningNotificationAsync(
        DateTimeOffset now,
        long grossAmountTotal,
        long registrationThresholdAmountTotal,
        decimal warningFraction,
        CancellationToken cancellationToken)
    {
        var recipientEmail = configuration["GST_TURNOVER_NOTIFICATION_EMAIL"]?.Trim();
        if (string.IsNullOrWhiteSpace(recipientEmail) || notificationService is null)
        {
            return new TaxTurnoverNotificationResult(
                Attempted: false,
                Sent: false,
                Provider: null,
                Reason: "notification_not_configured");
        }

        var result = await notificationService.SendAsync(
            NotificationTemplates.GstTurnoverThreshold,
            new NotificationRecipient(
                recipientEmail,
                configuration["GST_TURNOVER_NOTIFICATION_NAME"]?.Trim()),
            new GstTurnoverThresholdNotificationModel(
                GrossAmountTotal: grossAmountTotal,
                RegistrationThresholdAmountTotal: registrationThresholdAmountTotal,
                WarningFraction: warningFraction,
                WindowEndUtc: now,
                SupportEmail: configuration["NOTIFICATIONS_REPLY_TO_EMAIL"]),
            cancellationToken);

        return new TaxTurnoverNotificationResult(
            Attempted: true,
            Sent: result.Sent,
            Provider: result.Provider,
            Reason: result.Reason);
    }

    private static long ParseNzdMinorAmount(string? configuredValue, decimal fallbackNzd)
    {
        var value = decimal.TryParse(
            configuredValue,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : fallbackNzd;

        if (value < 0)
        {
            value = fallbackNzd;
        }

        return (long)Math.Round(value * 100m, MidpointRounding.AwayFromZero);
    }

    private static decimal ParseWarningFraction(string? configuredValue)
    {
        if (!decimal.TryParse(
                configuredValue,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed) ||
            parsed <= 0m ||
            parsed > 1m)
        {
            return DefaultWarningFraction;
        }

        return parsed;
    }
}

public sealed record TaxTurnoverReport(
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    string Currency,
    long GrossAmountTotal,
    long RegistrationThresholdAmountTotal,
    decimal WarningFraction,
    long WarningAmountTotal,
    decimal FractionOfThreshold,
    int IgnoredNonNzdPaymentCount,
    TaxTurnoverWarning? Warning,
    TaxTurnoverNotificationResult? Notification);

public sealed record TaxTurnoverWarning(
    string Code,
    string Severity,
    string Message);

public sealed record TaxTurnoverNotificationResult(
    bool Attempted,
    bool Sent,
    string? Provider,
    string? Reason);
