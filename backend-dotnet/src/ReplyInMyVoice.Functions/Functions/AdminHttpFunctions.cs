using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Http;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class AdminHttpFunctions(IConfiguration configuration)
{
    private readonly AdminAccess _adminAccess = new(configuration);

    [Function("AdminPing")]
    public async Task<IActionResult> Ping(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "admin/ping")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminAccess.RequireAdminAsync(request, cancellationToken);
        if (admin is null)
        {
            return FunctionHttpResults.Problem(
                "Admin access required",
                "The authenticated user is not allowed to access admin endpoints.",
                StatusCodes.Status403Forbidden);
        }

        return new OkObjectResult(new { ok = true });
    }
}
