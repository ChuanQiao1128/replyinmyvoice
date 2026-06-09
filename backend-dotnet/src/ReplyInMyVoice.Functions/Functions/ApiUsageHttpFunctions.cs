using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Account;
using ReplyInMyVoice.Application.UseCases.ApiKey;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Http;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class ApiUsageHttpFunctions(
    IConfiguration configuration,
    GetApiUsageSummaryHandler getApiUsageSummaryHandler,
    GetApiUsageSeriesHandler getApiUsageSeriesHandler,
    GetApiUsageRecentHandler getApiUsageRecentHandler,
    GetAccountSummaryHandler getAccountSummaryHandler)
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

        await getAccountSummaryHandler.HandleAsync(
            new GetAccountSummaryQuery(authUser.ExternalAuthUserId, authUser.Email),
            cancellationToken);
        var summary = await getApiUsageSummaryHandler.HandleAsync(
            new GetApiUsageSummaryQuery(authUser.ExternalAuthUserId, authUser.Email, DateTimeOffset.UtcNow),
            cancellationToken);
        return new OkObjectResult(ToResponse(summary!));
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

        if (!TryParseBoundedQueryInt(
            request,
            "days",
            30,
            1,
            ApiKeyUsageWindow.MaxUsageWindowDays,
            out var days,
            out var errorMessage))
        {
            return InvalidRequest(errorMessage);
        }

        var account = await getAccountSummaryHandler.HandleAsync(
            new GetAccountSummaryQuery(authUser.ExternalAuthUserId, authUser.Email),
            cancellationToken);
        var series = await getApiUsageSeriesHandler.HandleAsync(
            new GetApiUsageSeriesQuery(account.Id, DateTimeOffset.UtcNow, days),
            cancellationToken);
        return new OkObjectResult(series.Select(ToResponse).ToArray());
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

        var account = await getAccountSummaryHandler.HandleAsync(
            new GetAccountSummaryQuery(authUser.ExternalAuthUserId, authUser.Email),
            cancellationToken);
        var limit = ParsePositiveQueryInt(request, "limit", 50);
        var recent = await getApiUsageRecentHandler.HandleAsync(
            new GetApiUsageRecentQuery(account.Id, DateTimeOffset.UtcNow, limit),
            cancellationToken);
        return new OkObjectResult(recent.Select(ToResponse).ToArray());
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

    private static bool TryParseBoundedQueryInt(
        HttpRequest request,
        string name,
        int defaultValue,
        int minValue,
        int maxValue,
        out int value,
        out string errorMessage)
    {
        if (!request.Query.ContainsKey(name))
        {
            value = defaultValue;
            errorMessage = string.Empty;
            return true;
        }

        var rawValue = request.Query[name].ToString();
        if (!int.TryParse(rawValue, out var parsed))
        {
            value = defaultValue;
            errorMessage = $"{name} must be a number between {minValue} and {maxValue}.";
            return false;
        }

        if (parsed < minValue || parsed > maxValue)
        {
            value = defaultValue;
            errorMessage = $"{name} must be between {minValue} and {maxValue}.";
            return false;
        }

        value = parsed;
        errorMessage = string.Empty;
        return true;
    }

    private static IActionResult AuthenticationRequired() =>
        FunctionHttpResults.Problem(
            "Authentication required",
            "A valid authenticated user is required.",
            StatusCodes.Status401Unauthorized);

    private static IActionResult InvalidRequest(string message) =>
        FunctionHttpResults.Problem(
            "Invalid request",
            message,
            StatusCodes.Status400BadRequest,
            "invalid_request");

    private static ApiUsageSummaryResponse ToResponse(ApiUsageSummaryDto summary) =>
        new(
            ToResponse(summary.Today),
            ToResponse(summary.Yesterday),
            ToResponse(summary.MonthToDate),
            summary.Last30dCalls,
            summary.Quota,
            summary.Used,
            summary.Remaining,
            summary.PeriodEnd);

    private static ApiUsageCount ToResponse(ApiUsageCountDto count) =>
        new(count.Calls, count.Succeeded, count.Failed);

    private static ApiUsageSeriesPoint ToResponse(ApiUsageSeriesPointDto point) =>
        new(point.Date, point.Calls, point.Succeeded, point.Failed);

    private static ApiUsageRecentItem ToResponse(ApiUsageRecentItemDto item) =>
        new(item.CreatedAt, item.Endpoint, item.StatusCode, item.LatencyMs, item.KeyLast4);
}
