using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed class ListDeadLettersHandler(
    IDeadLetterMessageRepository deadLetters,
    IAdminUserRepository adminUsers,
    IUnitOfWork unitOfWork)
{
    private const int MaxPageSize = 100;

    public async Task<AdminDeadLettersListDto> HandleAsync(
        ListDeadLettersQuery query,
        CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        if (!DeadLetterSourceTypes.TryNormalize(query.SourceType, out var sourceType))
        {
            sourceType = query.SourceType;
        }

        var result = await deadLetters.GetPagedAsync(page, pageSize, sourceType, ct);
        await adminUsers.AddAuditLogAsync(new AdminAuditLog
        {
            AdminExternalAuthUserId = query.AdminExternalAuthUserId.Trim(),
            AdminEmail = query.AdminEmail?.Trim() ?? string.Empty,
            Action = "list_dead_letters",
            TargetUserId = null,
            DetailsJson = AdminUseCaseSupport.SerializeAuditDetails(
                new AdminDeadLetterAuditDetailsDto(sourceType, null, null, null)),
            CreatedAt = query.Now,
        }, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var totalPages = result.TotalCount == 0
            ? 0
            : (int)Math.Ceiling(result.TotalCount / (double)pageSize);

        return new AdminDeadLettersListDto(
            page,
            pageSize,
            result.TotalCount,
            totalPages,
            result.Items.Select(DeadLetterMessageSupport.ToListItem).ToList());
    }
}
