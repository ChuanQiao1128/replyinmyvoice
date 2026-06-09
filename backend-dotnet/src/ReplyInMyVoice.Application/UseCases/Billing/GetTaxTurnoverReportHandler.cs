using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.Billing;

public sealed class GetTaxTurnoverReportHandler(
    IRewriteCreditRepository credits,
    ITaxTurnoverNotifier notifier,
    ITaxTurnoverSettingsProvider settingsProvider)
{
    private const string NzdCurrency = "nzd";

    public async Task<TaxTurnoverReportDto> HandleAsync(
        GetTaxTurnoverReportQuery query,
        CancellationToken ct = default)
    {
        var windowStart = query.Now.AddMonths(-12);
        var rows = await credits.ListPurchaseCreditsForTurnoverAsync(
            windowStart,
            query.Now,
            ct);
        var nzdRows = rows
            .Where(x => string.Equals(x.StripeCurrency, NzdCurrency, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var grossAmountTotal = nzdRows.Sum(x => x.StripeAmountTotal!.Value);
        var settings = settingsProvider.GetSettings();
        var warningAmountTotal = (long)Math.Ceiling(
            settings.RegistrationThresholdAmountTotal * settings.WarningFraction);
        var warning = grossAmountTotal >= warningAmountTotal
            ? new TaxTurnoverWarningDto(
                "nz_gst_turnover_threshold_approaching",
                "warning",
                "Rolling 12-month gross NZD revenue is approaching the GST registration threshold.")
            : null;
        var notification = warning is null
            ? null
            : await notifier.TrySendWarningNotificationAsync(
                new TaxTurnoverNotificationRequest(
                    query.Now,
                    grossAmountTotal,
                    settings.RegistrationThresholdAmountTotal,
                    settings.WarningFraction),
                ct);

        return new TaxTurnoverReportDto(
            windowStart,
            query.Now,
            NzdCurrency,
            grossAmountTotal,
            settings.RegistrationThresholdAmountTotal,
            settings.WarningFraction,
            warningAmountTotal,
            settings.RegistrationThresholdAmountTotal == 0
                ? 0m
                : grossAmountTotal / (decimal)settings.RegistrationThresholdAmountTotal,
            rows.Count - nzdRows.Count,
            warning,
            notification);
    }
}
