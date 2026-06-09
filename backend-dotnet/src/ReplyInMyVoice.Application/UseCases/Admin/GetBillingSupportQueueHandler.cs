using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed class GetBillingSupportQueueHandler(IBillingSupportRequestRepository billingSupportRequests)
{
    public async Task<IReadOnlyList<AdminBillingSupportRequestDto>> HandleAsync(
        GetBillingSupportQueueQuery query,
        CancellationToken ct = default)
    {
        var rows = await billingSupportRequests.ListOpenForAdminQueueAsync(ct);
        return rows
            .Select(ToAdminBillingSupportRequest)
            .ToList();
    }

    internal static AdminBillingSupportRequestDto ToAdminBillingSupportRequest(
        BillingSupportRequest request) =>
        new(
            request.Id,
            request.UserId,
            request.User?.Email,
            request.User?.ExternalAuthUserId,
            FormatType(request.Type),
            request.RelatedPaymentIntentId,
            request.Message,
            FormatStatus(request.Status),
            request.CreatedAt,
            request.UpdatedAt,
            request.ResolvedAt);

    internal static string FormatType(BillingSupportRequestType type) =>
        type switch
        {
            BillingSupportRequestType.BillingQuestion => "billing-question",
            _ => "refund",
        };

    private static string FormatStatus(BillingSupportRequestStatus status) =>
        status switch
        {
            BillingSupportRequestStatus.Resolved => "resolved",
            _ => "open",
        };
}
