using ReplyInMyVoice.Application.Common;
using AppStripeReconciliationAlerter = ReplyInMyVoice.Application.Abstractions.IStripeReconciliationAlerter;
using LegacyStripeReconciliationAlerter = ReplyInMyVoice.Infrastructure.Services.IStripeReconciliationAlerter;
using LegacyStripeReconciliationDiscrepancy = ReplyInMyVoice.Infrastructure.Services.StripeReconciliationDiscrepancy;
using LegacyStripeReconciliationDiscrepancyKind = ReplyInMyVoice.Infrastructure.Services.StripeReconciliationDiscrepancyKind;
using LegacyStripeReconciliationReport = ReplyInMyVoice.Infrastructure.Services.StripeReconciliationReport;

namespace ReplyInMyVoice.Infrastructure.Providers;

public sealed class StripeReconciliationAlerterAdapter(
    LegacyStripeReconciliationAlerter legacyAlerter) : AppStripeReconciliationAlerter
{
    public async Task AlertAsync(
        StripeReconciliationReportDto report,
        CancellationToken ct = default)
    {
        await legacyAlerter.AlertAsync(ToLegacyReport(report), ct);
    }

    private static LegacyStripeReconciliationReport ToLegacyReport(
        StripeReconciliationReportDto report) =>
        new(
            report.WindowStart,
            report.WindowEnd,
            report.CompletedAt,
            report.StripePaymentCount,
            report.PurchaseGrantCount,
            report.PaidButNoGrantCount,
            report.GrantButNoPaymentCount,
            report.AmountMismatchCount,
            report.Discrepancies.Select(ToLegacyDiscrepancy).ToList());

    private static LegacyStripeReconciliationDiscrepancy ToLegacyDiscrepancy(
        StripeReconciliationDiscrepancyDto discrepancy) =>
        new(
            ToLegacyKind(discrepancy.Kind),
            discrepancy.PaymentIntentId,
            discrepancy.CreditId,
            discrepancy.StripeAmount,
            discrepancy.LedgerAmount,
            discrepancy.StripeCurrency,
            discrepancy.LedgerCurrency,
            discrepancy.StripePaidAt,
            discrepancy.LedgerGrantedAt);

    private static LegacyStripeReconciliationDiscrepancyKind ToLegacyKind(
        StripeReconciliationDiscrepancyKindDto kind) =>
        kind switch
        {
            StripeReconciliationDiscrepancyKindDto.PaidButNoGrant =>
                LegacyStripeReconciliationDiscrepancyKind.PaidButNoGrant,
            StripeReconciliationDiscrepancyKindDto.GrantButNoPayment =>
                LegacyStripeReconciliationDiscrepancyKind.GrantButNoPayment,
            StripeReconciliationDiscrepancyKindDto.AmountMismatch =>
                LegacyStripeReconciliationDiscrepancyKind.AmountMismatch,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
}
