using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed class GetDeadLetterDetailHandler(
    IDeadLetterMessageRepository deadLetters,
    IAdminUserRepository adminUsers,
    IUnitOfWork unitOfWork)
{
    public async Task<AdminDeadLetterDetailDto?> HandleAsync(
        GetDeadLetterDetailQuery query,
        CancellationToken ct = default)
    {
        var message = await deadLetters.GetByIdAsync(query.DeadLetterId, track: false, ct);
        if (message is null)
        {
            return null;
        }

        await adminUsers.AddAuditLogAsync(new AdminAuditLog
        {
            AdminExternalAuthUserId = query.AdminExternalAuthUserId.Trim(),
            AdminEmail = query.AdminEmail?.Trim() ?? string.Empty,
            Action = "get_dead_letter",
            TargetUserId = null,
            DetailsJson = AdminUseCaseSupport.SerializeAuditDetails(
                DeadLetterMessageSupport.ToAuditDetails(message)),
            CreatedAt = query.Now,
        }, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return DeadLetterMessageSupport.ToDetail(message);
    }
}
