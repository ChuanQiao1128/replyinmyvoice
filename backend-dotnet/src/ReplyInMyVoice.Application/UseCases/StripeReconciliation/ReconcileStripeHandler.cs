using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.StripeReconciliation;

public sealed class ReconcileStripeHandler(
    IPaymentGrantRepository paymentGrants,
    IStripePaymentReconciliationClient stripeClient,
    IStripeReconciliationAlerter alerter,
    IUnitOfWork unitOfWork)
{
    public async Task<StripeReconciliationReportDto> HandleAsync(
        ReconcileStripeCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);

        if (command.WindowEnd <= command.WindowStart)
        {
            throw new ArgumentException("reconciliation_window_invalid", nameof(command));
        }

        var stripePayments = NormalizePayments(await stripeClient.ListPaidPaymentIntentsAsync(
            command.WindowStart,
            command.WindowEnd,
            ct));
        var paymentIntentIds = stripePayments
            .Select(x => x.PaymentIntentId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var grants = await paymentGrants.ListPurchaseGrantsForReconciliationAsync(
            command.WindowStart,
            command.WindowEnd,
            paymentIntentIds,
            ct);
        var report = BuildReport(
            command.WindowStart,
            command.WindowEnd,
            command.CompletedAt,
            stripePayments,
            grants);

        if (report.DiscrepancyCount > 0)
        {
            await AlertAsync(report, ct);
        }

        return report;
    }

    private async Task AlertAsync(
        StripeReconciliationReportDto report,
        CancellationToken ct)
    {
        try
        {
            await alerter.AlertAsync(report, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
        }
    }

    private static IReadOnlyList<StripePaidPaymentDto> NormalizePayments(
        IReadOnlyList<StripePaidPaymentDto> stripePayments) =>
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

    private static StripeReconciliationReportDto BuildReport(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        DateTimeOffset completedAt,
        IReadOnlyList<StripePaidPaymentDto> stripePayments,
        IReadOnlyList<PaymentGrantSnapshot> grants)
    {
        var paymentByIntent = stripePayments.ToDictionary(x => x.PaymentIntentId, StringComparer.Ordinal);
        var grantsByIntent = grants
            .Where(x => !string.IsNullOrWhiteSpace(x.PaymentIntentId))
            .GroupBy(x => x.PaymentIntentId!.Trim(), StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);
        var discrepancies = new List<StripeReconciliationDiscrepancyDto>();

        foreach (var payment in stripePayments.OrderBy(x => x.PaidAt).ThenBy(x => x.PaymentIntentId))
        {
            if (!grantsByIntent.TryGetValue(payment.PaymentIntentId, out var matchingGrants) ||
                matchingGrants.Count == 0)
            {
                discrepancies.Add(new StripeReconciliationDiscrepancyDto(
                    StripeReconciliationDiscrepancyKindDto.PaidButNoGrant,
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
                discrepancies.Add(new StripeReconciliationDiscrepancyDto(
                    StripeReconciliationDiscrepancyKindDto.AmountMismatch,
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

            discrepancies.Add(new StripeReconciliationDiscrepancyDto(
                StripeReconciliationDiscrepancyKindDto.GrantButNoPayment,
                string.IsNullOrWhiteSpace(paymentIntentId) ? null : paymentIntentId,
                grant.CreditId,
                StripeAmount: null,
                LedgerAmount: grant.AmountTotal,
                StripeCurrency: null,
                LedgerCurrency: grant.Currency,
                StripePaidAt: null,
                LedgerGrantedAt: grant.GrantedAt));
        }

        return StripeReconciliationReportDto.Create(
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
}
