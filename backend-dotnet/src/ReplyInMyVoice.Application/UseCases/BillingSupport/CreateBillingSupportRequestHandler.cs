using System.Data;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.BillingSupport;

public sealed class CreateBillingSupportRequestHandler(
    IBillingSupportRepository billingSupportRequests,
    IUnitOfWork unitOfWork)
{
    private const int MaxMessageLength = 2000;
    private const int MaxPaymentIntentLength = 160;
    private const string InvalidPaymentIntentMarker = "\u0000";

    public async Task<BillingSupportRequestResultDto> HandleAsync(
        CreateBillingSupportRequestCommand command,
        CancellationToken ct = default)
    {
        if (!TryParseType(command.Type, out var requestType))
        {
            return BillingSupportRequestResultDto.InvalidRequest(
                "Request type must be refund or billing-question.");
        }

        var message = NormalizeMessage(command.Message);
        if (message is null)
        {
            return BillingSupportRequestResultDto.InvalidRequest(
                "Message must be between 10 and 2000 characters.");
        }

        var paymentIntentId = NormalizePaymentIntentId(command.RelatedPaymentIntentId);
        if (paymentIntentId == InvalidPaymentIntentMarker)
        {
            return BillingSupportRequestResultDto.InvalidRequest(
                "Payment intent id must be 160 characters or less.");
        }

        return await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                if (!string.IsNullOrWhiteSpace(paymentIntentId))
                {
                    var ownsPayment = await billingSupportRequests.HasPaymentIntentForUserAsync(
                        command.UserId,
                        paymentIntentId,
                        transactionCt);
                    if (!ownsPayment)
                    {
                        return BillingSupportRequestResultDto.InvalidRequest(
                            "Selected payment was not found for this account.");
                    }
                }

                var hasOpenRequest = await billingSupportRequests.HasOpenRequestForUserAsync(
                    command.UserId,
                    transactionCt);
                if (hasOpenRequest)
                {
                    return BillingSupportRequestResultDto.InvalidRequest(
                        "An open billing support request already exists for this account.");
                }

                var request = new BillingSupportRequest
                {
                    UserId = command.UserId,
                    Type = requestType,
                    RelatedPaymentIntentId = paymentIntentId,
                    Message = message,
                    Status = BillingSupportRequestStatus.Open,
                    CreatedAt = command.Now,
                    UpdatedAt = command.Now,
                };
                await billingSupportRequests.AddAsync(request, transactionCt);
                await unitOfWork.SaveChangesAsync(transactionCt);

                return BillingSupportRequestResultDto.Success(
                    BillingSupportRequestResponseDto.FromRequest(request));
            },
            IsolationLevel.Serializable,
            ct);
    }

    private static string? NormalizeMessage(string? message)
    {
        var normalized = message?.Trim();
        return normalized is { Length: >= 10 and <= MaxMessageLength }
            ? normalized
            : null;
    }

    private static string? NormalizePaymentIntentId(string? paymentIntentId)
    {
        var normalized = paymentIntentId?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= MaxPaymentIntentLength
            ? normalized
            : InvalidPaymentIntentMarker;
    }

    private static bool TryParseType(
        string? value,
        out BillingSupportRequestType requestType)
    {
        var normalized = value?.Trim().Replace("_", "-", StringComparison.Ordinal).ToLowerInvariant();
        requestType = normalized switch
        {
            "refund" => BillingSupportRequestType.Refund,
            "billing-question" => BillingSupportRequestType.BillingQuestion,
            _ => default,
        };
        return normalized is "refund" or "billing-question";
    }
}
