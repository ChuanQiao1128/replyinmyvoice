using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ReplyInMyVoice.Functions.Auth;

public static class FunctionAuthResolver
{
    public static string? ResolveExternalUserId(HttpRequest request, IConfiguration configuration)
    {
        var allowHeaderAuth = string.Equals(
            configuration["ALLOW_HEADER_AUTH"],
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (allowHeaderAuth)
        {
            var testUserId = request.Headers["X-Test-User-Id"].ToString();
            if (!string.IsNullOrWhiteSpace(testUserId))
            {
                return testUserId;
            }

            var externalUserId = request.Headers["X-External-User-Id"].ToString();
            if (!string.IsNullOrWhiteSpace(externalUserId))
            {
                return externalUserId;
            }
        }

        if (request.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            return request.HttpContext.User.FindFirstValue("sub") ??
                request.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        return null;
    }

    public static string? ResolveEmail(HttpRequest request)
    {
        var headerEmail = request.Headers["X-User-Email"].ToString();
        if (!string.IsNullOrWhiteSpace(headerEmail))
        {
            return headerEmail;
        }

        return request.HttpContext.User.FindFirstValue(ClaimTypes.Email) ??
            request.HttpContext.User.FindFirstValue("email");
    }
}
