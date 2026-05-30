using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Http;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class AdminHttpFunctions
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    private readonly AdminAccess _adminAccess;
    private readonly AdminService? _adminService;

    public AdminHttpFunctions(
        IConfiguration configuration,
        Func<AppDbContext>? dbContextFactory = null)
    {
        _adminAccess = new AdminAccess(configuration);
        _adminService = dbContextFactory is null ? null : new AdminService(dbContextFactory);
    }

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

    [Function("AdminUsersList")]
    public async Task<IActionResult> ListUsers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "admin/users")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var forbidden = await RequireAdminResultAsync(request, cancellationToken);
        if (forbidden is not null)
        {
            return forbidden;
        }

        if (_adminService is null)
        {
            return AdminServiceUnavailable();
        }

        var page = ParsePositiveInt(request.Query, "page", DefaultPage, int.MaxValue);
        var pageSize = ParsePositiveInt(request.Query, "pageSize", DefaultPageSize, MaxPageSize);
        var users = await _adminService.GetUsersAsync(page, pageSize, cancellationToken);
        return new OkObjectResult(users);
    }

    [Function("AdminUserDetail")]
    public async Task<IActionResult> GetUserDetail(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "admin/users/{userId}")]
        HttpRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        var forbidden = await RequireAdminResultAsync(request, cancellationToken);
        if (forbidden is not null)
        {
            return forbidden;
        }

        if (_adminService is null)
        {
            return AdminServiceUnavailable();
        }

        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return FunctionHttpResults.Problem(
                "Invalid user id",
                "The admin user detail route requires a valid user id.",
                StatusCodes.Status400BadRequest);
        }

        var detail = await _adminService.GetUserDetailAsync(parsedUserId, cancellationToken);
        if (detail is null)
        {
            return FunctionHttpResults.Problem(
                "User not found",
                "No user exists for the requested id.",
                StatusCodes.Status404NotFound);
        }

        return new OkObjectResult(detail);
    }

    [Function("AdminStats")]
    public async Task<IActionResult> GetStats(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "admin/stats")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var forbidden = await RequireAdminResultAsync(request, cancellationToken);
        if (forbidden is not null)
        {
            return forbidden;
        }

        if (_adminService is null)
        {
            return AdminServiceUnavailable();
        }

        var stats = await _adminService.GetStatsAsync(cancellationToken);
        return new OkObjectResult(stats);
    }

    private async Task<IActionResult?> RequireAdminResultAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminAccess.RequireAdminAsync(request, cancellationToken);
        if (admin is not null)
        {
            return null;
        }

        return FunctionHttpResults.Problem(
            "Admin access required",
            "The authenticated user is not allowed to access admin endpoints.",
            StatusCodes.Status403Forbidden);
    }

    private static IActionResult AdminServiceUnavailable() =>
        FunctionHttpResults.Problem(
            "Admin service unavailable",
            "Admin read services are not configured.",
            StatusCodes.Status500InternalServerError);

    private static int ParsePositiveInt(
        IQueryCollection query,
        string key,
        int defaultValue,
        int maxValue)
    {
        if (!query.TryGetValue(key, out var values) ||
            !int.TryParse(values.ToString(), out var parsed) ||
            parsed < 1)
        {
            return defaultValue;
        }

        return Math.Min(parsed, maxValue);
    }
}
