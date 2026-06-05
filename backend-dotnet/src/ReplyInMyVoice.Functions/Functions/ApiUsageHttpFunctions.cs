using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Http;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class ApiUsageHttpFunctions(
    IConfiguration configuration,
    AccountService accountService,
    ApiKeyUsageQueryService apiKeyUsageQueryService)
{
    [Function("GetApiUsageSummary")]
    public async Task<IActionResult> GetApiUsageSummary(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me/api-usage/summary")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var authUser = await FunctionAuthResolver.ResolveUserAsync(request, configuration, cancellationToken);
        if (authUser is null)
        {
            return AuthenticationRequired();
        }

        var summary = await apiKeyUsageQueryService.GetSummaryAsync(
            authUser.ExternalAuthUserId,
            authUser.Email,
            cancellationToken);
        return new OkObjectResult(summary);
    }

    [Function("GetApiUsageSeries")]
    public async Task<IActionResult> GetApiUsageSeries(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me/api-usage/series")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var authUser = await FunctionAuthResolver.ResolveUserAsync(request, configuration, cancellationToken);
        if (authUser is null)
        {
            return AuthenticationRequired();
        }

        var account = await accountService.GetOrCreateAccountSummaryAsync(
            authUser.ExternalAuthUserId,
            authUser.Email,
            cancellationToken);
        var days = ParsePositiveQueryInt(request, "days", 30);
        var series = await apiKeyUsageQueryService.GetSeriesAsync(
            account.Id,
            days,
            cancellationToken);
        return new OkObjectResult(series);
    }

    [Function("GetApiUsageRecent")]
    public async Task<IActionResult> GetApiUsageRecent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me/api-usage/recent")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var authUser = await FunctionAuthResolver.ResolveUserAsync(request, configuration, cancellationToken);
        if (authUser is null)
        {
            return AuthenticationRequired();
        }

        var account = await accountService.GetOrCreateAccountSummaryAsync(
            authUser.ExternalAuthUserId,
            authUser.Email,
            cancellationToken);
        var limit = ParsePositiveQueryInt(request, "limit", 50);
        var recent = await apiKeyUsageQueryService.GetRecentAsync(
            account.Id,
            limit,
            cancellationToken);
        return new OkObjectResult(recent);
    }

    private static int ParsePositiveQueryInt(
        HttpRequest request,
        string name,
        int defaultValue)
    {
        var rawValue = request.Query[name].ToString();
        return int.TryParse(rawValue, out var parsed) && parsed > 0
            ? parsed
            : defaultValue;
    }

    private static IActionResult AuthenticationRequired() =>
        FunctionHttpResults.Problem(
            "Authentication required",
            "A valid authenticated user is required.",
            StatusCodes.Status401Unauthorized);
}
