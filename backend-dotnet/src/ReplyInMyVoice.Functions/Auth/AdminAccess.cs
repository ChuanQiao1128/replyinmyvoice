using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ReplyInMyVoice.Functions.Auth;

public sealed class AdminAccess(IConfiguration configuration)
{
    public async Task<FunctionAuthUser?> RequireAdminAsync(
        HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await FunctionAuthResolver.ResolveUserAsync(request, configuration, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var allowedAdminIds = ResolveAllowedAdminIds(configuration);
        return IsAllowedAdmin(user, allowedAdminIds) ? user : null;
    }

    private static bool IsAllowedAdmin(
        FunctionAuthUser user,
        ISet<string> allowedAdminIds) =>
        allowedAdminIds.Contains(user.ExternalAuthUserId) ||
        (!string.IsNullOrWhiteSpace(user.Email) && allowedAdminIds.Contains(user.Email));

    private static ISet<string> ResolveAllowedAdminIds(IConfiguration configuration) =>
        (configuration["ADMIN_EMAILS"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
