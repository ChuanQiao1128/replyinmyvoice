using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.BillingSupport;

public sealed class GetBillingSupportRequestsHandler(IBillingSupportRepository billingSupportRequests)
{
    public async Task<IReadOnlyList<BillingSupportRequestResponseDto>> HandleAsync(
        GetBillingSupportRequestsQuery query,
        CancellationToken ct = default)
    {
        var requests = await billingSupportRequests.ListByUserIdAsync(query.UserId, ct);
        return requests
            .Select(BillingSupportRequestResponseDto.FromRequest)
            .ToList();
    }
}
