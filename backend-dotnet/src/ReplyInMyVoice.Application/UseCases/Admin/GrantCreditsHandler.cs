using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed class GrantCreditsHandler(
    IAdminUserRepository adminUsers,
    IUnitOfWork unitOfWork)
{
    private const int AdminCreditExpiryDays = 90;
    private const string AdminCreditSource = "ADMIN";

    public async Task<AdminCreditGrantResultDto> HandleAsync(
        GrantCreditsCommand command,
        CancellationToken ct = default)
    {
        if (command.Amount is null)
        {
            return AdminCreditGrantResultDto.InvalidRequest("Credit amount is required.");
        }

        if (command.Amount <= 0)
        {
            return AdminCreditGrantResultDto.InvalidRequest("Credit amount must be greater than zero.");
        }

        if (!await adminUsers.UserExistsAsync(command.TargetUserId, ct))
        {
            return AdminCreditGrantResultDto.UserNotFound("No user exists for the requested id.");
        }

        var expiresAt = command.Now.AddDays(AdminCreditExpiryDays);
        var credit = new RewriteCredit
        {
            UserId = command.TargetUserId,
            Source = AdminCreditSource,
            AmountGranted = command.Amount.Value,
            OriginalAmountGranted = command.Amount.Value,
            AmountConsumed = 0,
            GrantedAt = command.Now,
            ExpiresAt = expiresAt,
        };

        await adminUsers.AddCreditAsync(credit, ct);
        await adminUsers.AddAuditLogAsync(new AdminAuditLog
        {
            AdminExternalAuthUserId = command.AdminExternalAuthUserId.Trim(),
            AdminEmail = command.AdminEmail?.Trim() ?? string.Empty,
            Action = "grant_credits",
            TargetUserId = command.TargetUserId,
            DetailsJson = AdminUseCaseSupport.SerializeAuditDetails(
                new AdminCreditGrantAuditDetailsDto(
                    credit.Id,
                    AdminCreditSource,
                    command.Amount.Value,
                    command.Now,
                    expiresAt,
                    NormalizeReason(command.Reason))),
            CreatedAt = command.Now,
        }, ct);

        await unitOfWork.SaveChangesAsync(ct);

        return AdminCreditGrantResultDto.Success(new AdminCreditGrantResponseDto(
            command.TargetUserId,
            credit.Id,
            AdminCreditSource,
            command.Amount.Value,
            credit.AmountConsumed,
            command.Amount.Value,
            command.Now,
            expiresAt));
    }

    private static string? NormalizeReason(string? reason)
    {
        var normalized = reason?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized;
    }
}
