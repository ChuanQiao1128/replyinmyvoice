using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.Common;

public sealed record BillingSupportRequestResponseDto(
    Guid Id,
    Guid UserId,
    string Type,
    string? RelatedPaymentIntentId,
    string Message,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt)
{
    public static BillingSupportRequestResponseDto FromRequest(BillingSupportRequest request) =>
        new(
            request.Id,
            request.UserId,
            FormatType(request.Type),
            request.RelatedPaymentIntentId,
            request.Message,
            FormatStatus(request.Status),
            request.CreatedAt,
            request.UpdatedAt,
            request.ResolvedAt);

    private static string FormatType(BillingSupportRequestType type) =>
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
