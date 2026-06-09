using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed class GetAdminUserDetailHandler(IAdminUserRepository adminUsers)
{
    public async Task<AdminUserDetailDto?> HandleAsync(
        GetAdminUserDetailQuery query,
        CancellationToken ct = default) =>
        await adminUsers.GetUserDetailAsync(query.UserId, ct);
}
