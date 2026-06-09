using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed class SetUserSuspensionHandler(
    IAdminUserRepository adminUsers,
    IUnitOfWork unitOfWork)
{
    public async Task<AdminSuspensionResultDto> HandleAsync(
        SetUserSuspensionCommand command,
        CancellationToken ct = default)
    {
        var suspension = await adminUsers.SetUserSuspensionAsync(
            command.TargetUserId,
            command.Suspended,
            command.Now,
            ct);
        if (suspension is null)
        {
            return AdminSuspensionResultDto.UserNotFound("No user exists for the requested id.");
        }

        await adminUsers.AddAuditLogAsync(new AdminAuditLog
        {
            AdminExternalAuthUserId = command.AdminExternalAuthUserId.Trim(),
            AdminEmail = command.AdminEmail?.Trim() ?? string.Empty,
            Action = command.Suspended ? "suspend_user" : "unsuspend_user",
            TargetUserId = command.TargetUserId,
            DetailsJson = AdminUseCaseSupport.SerializeAuditDetails(
                new AdminSuspensionAuditDetailsDto(command.Suspended, suspension.SuspendedAt)),
            CreatedAt = command.Now,
        }, ct);

        await unitOfWork.SaveChangesAsync(ct);

        return AdminSuspensionResultDto.Success(new AdminSuspensionResponseDto(
            command.TargetUserId,
            command.Suspended,
            suspension.SuspendedAt));
    }
}
