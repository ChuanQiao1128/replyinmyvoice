using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Http;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class AccountHttpFunctions(
    IConfiguration configuration,
    AccountService accountService)
{
    [Function("GetAccountSummary")]
    public async Task<IActionResult> GetAccountSummary(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var authUser = await FunctionAuthResolver.ResolveUserAsync(request, configuration, cancellationToken);
        if (authUser is null)
        {
            return FunctionHttpResults.Problem(
                "Authentication required",
                "A valid authenticated user is required.",
                StatusCodes.Status401Unauthorized);
        }

        var account = await accountService.GetOrCreateAccountSummaryAsync(
            authUser.ExternalAuthUserId,
            authUser.Email,
            cancellationToken);

        return new OkObjectResult(account);
    }

    [Function("DeleteAccount")]
    public async Task<IActionResult> DeleteAccount(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "me")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var authUser = await FunctionAuthResolver.ResolveUserAsync(request, configuration, cancellationToken);
        if (authUser is null)
        {
            return FunctionHttpResults.Problem(
                "Authentication required",
                "A valid authenticated user is required.",
                StatusCodes.Status401Unauthorized);
        }

        await accountService.DeleteAccountAsync(authUser.ExternalAuthUserId, cancellationToken);
        return new NoContentResult();
    }
}
