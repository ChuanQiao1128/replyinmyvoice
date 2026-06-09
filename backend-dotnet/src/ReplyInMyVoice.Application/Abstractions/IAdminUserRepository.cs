using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IAdminUserRepository
{
    Task<AdminUsersListDto> ListUsersAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken ct = default);

    Task<AdminUserDetailDto?> GetUserDetailAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<bool> UserExistsAsync(
        Guid userId,
        CancellationToken ct = default);

    Task AddCreditAsync(
        RewriteCredit credit,
        CancellationToken ct = default);

    Task AddAuditLogAsync(
        AdminAuditLog auditLog,
        CancellationToken ct = default);

    Task<AdminDeleteUserLookupDto?> GetDeleteUserLookupAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<bool> EraseUserAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken ct = default);
}
