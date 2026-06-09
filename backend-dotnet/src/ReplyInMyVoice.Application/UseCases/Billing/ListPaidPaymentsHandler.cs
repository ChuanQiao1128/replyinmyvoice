using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.Billing;

public sealed class ListPaidPaymentsHandler(IStripeBillingClient stripeBillingClient)
{
    public async Task<IReadOnlyList<StripePaidPaymentDto>> HandleAsync(
        ListPaidPaymentsQuery query,
        CancellationToken ct = default)
    {
        if (query.WindowEnd <= query.WindowStart)
        {
            throw new ArgumentException("reconciliation_window_invalid", nameof(query));
        }

        return await stripeBillingClient.ListPaidPaymentIntentsAsync(
            query.WindowStart,
            query.WindowEnd,
            ct);
    }
}
