using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed class GetAdminUsersHandler(IAdminUserRepository adminUsers)
{
    private const int MaxPageSize = 100;

    public async Task<AdminUsersListDto> HandleAsync(
        GetAdminUsersQuery query,
        CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        return await adminUsers.ListUsersAsync(page, pageSize, query.Search, ct);
    }
}
