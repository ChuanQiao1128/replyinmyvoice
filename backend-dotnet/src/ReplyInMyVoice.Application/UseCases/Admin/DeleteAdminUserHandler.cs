using System.Data;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Account;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed class DeleteAdminUserHandler(
    IAdminUserRepository adminUsers,
    IUnitOfWork unitOfWork,
    IStripeSubscriptionCancellationService? subscriptionCancellationService = null)
{
    public async Task<AdminDeleteUserResultDto> HandleAsync(
        DeleteAdminUserCommand command,
        CancellationToken ct = default)
    {
        var normalizedAdminExternalAuthUserId = command.AdminExternalAuthUserId.Trim();
        var target = await adminUsers.GetDeleteUserLookupAsync(command.UserId, ct);
        if (target is null)
        {
            return AdminDeleteUserResultDto.UserNotFound("No user exists for the requested id.");
        }

        if (AccountUseCaseSupport.IsErasedExternalAuthUserId(target.ExternalAuthUserId))
        {
            return AdminDeleteUserResultDto.Forbidden("account already erased");
        }

        if (string.Equals(
                target.ExternalAuthUserId,
                normalizedAdminExternalAuthUserId,
                StringComparison.Ordinal))
        {
            return AdminDeleteUserResultDto.Forbidden(
                "an admin cannot delete their own account from the console");
        }

        if (!string.IsNullOrWhiteSpace(target.StripeSubscriptionId) &&
            subscriptionCancellationService is not null)
        {
            await subscriptionCancellationService.CancelSubscriptionAsync(target.StripeSubscriptionId, ct);
        }

        var erased = await unitOfWork.ExecuteInTransactionAsync(async transactionCt =>
        {
            if (!await adminUsers.EraseUserAsync(command.UserId, command.Now, transactionCt))
            {
                return false;
            }

            await adminUsers.AddAuditLogAsync(new AdminAuditLog
            {
                AdminExternalAuthUserId = normalizedAdminExternalAuthUserId,
                AdminEmail = command.AdminEmail?.Trim() ?? string.Empty,
                Action = "user.delete",
                TargetUserId = command.UserId,
                DetailsJson = AdminUseCaseSupport.SerializeAuditDetails(
                    new AdminDeleteUserAuditDetailsDto("erased", command.Now)),
                CreatedAt = command.Now,
            }, transactionCt);
            await unitOfWork.SaveChangesAsync(transactionCt);
            return true;
        }, IsolationLevel.Serializable, ct);

        return erased
            ? AdminDeleteUserResultDto.Success(new AdminDeleteUserResponseDto(command.UserId, "erased"))
            : AdminDeleteUserResultDto.UserNotFound("No user exists for the requested id.");
    }
}
