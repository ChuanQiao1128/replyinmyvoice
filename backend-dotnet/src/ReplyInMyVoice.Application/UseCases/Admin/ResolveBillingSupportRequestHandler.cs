using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed class ResolveBillingSupportRequestHandler(
    IBillingSupportRequestRepository billingSupportRequests,
    IAdminUserRepository adminUsers,
    IUnitOfWork unitOfWork)
{
    public async Task<AdminBillingSupportRequestDto?> HandleAsync(
        ResolveBillingSupportRequestCommand command,
        CancellationToken ct = default)
    {
        var request = await billingSupportRequests.GetByIdForAdminResolveAsync(command.RequestId, ct);
        if (request is null)
        {
            return null;
        }

        if (request.Status == BillingSupportRequestStatus.Open)
        {
            await billingSupportRequests.MarkResolvedAsync(request, command.Now, ct);
            await adminUsers.AddAuditLogAsync(new AdminAuditLog
            {
                AdminExternalAuthUserId = command.AdminExternalAuthUserId.Trim(),
                AdminEmail = command.AdminEmail?.Trim() ?? string.Empty,
                Action = "resolve_billing_support_request",
                TargetUserId = request.UserId,
                DetailsJson = AdminUseCaseSupport.SerializeAuditDetails(
                    new AdminBillingSupportResolveAuditDetailsDto(
                        request.Id,
                        GetBillingSupportQueueHandler.FormatType(request.Type),
                        request.RelatedPaymentIntentId,
                        command.Now)),
                CreatedAt = command.Now,
            }, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return GetBillingSupportQueueHandler.ToAdminBillingSupportRequest(request);
    }
}
