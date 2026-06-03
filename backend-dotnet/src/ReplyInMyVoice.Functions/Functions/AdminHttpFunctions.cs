using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Http;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Notifications;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class AdminHttpFunctions
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AdminAccess _adminAccess;
    private readonly AdminService? _adminService;
    private readonly PromoAdminService? _promoAdminService;

    public AdminHttpFunctions(
        IConfiguration configuration,
        Func<AppDbContext>? dbContextFactory = null,
        IStripeRefundClient? refundClient = null,
        INotificationService? notificationService = null)
    {
        _adminAccess = new AdminAccess(configuration);
        _adminService = dbContextFactory is null
            ? null
            : new AdminService(
                dbContextFactory,
                refundClient ?? new StripeBillingService(dbContextFactory, configuration),
                new TaxTurnoverService(dbContextFactory, configuration, notificationService));
        _promoAdminService = dbContextFactory is null
            ? null
            : new PromoAdminService(dbContextFactory);
    }

    [Function("AdminPing")]
    public async Task<IActionResult> Ping(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "console/ping")]
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "console/users")]
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "console/users/{userId}")]
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "console/stats")]
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

    [Function("AdminBillingSupportRequests")]
    public async Task<IActionResult> ListBillingSupportRequests(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "console/billing-support-requests")]
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

        var queue = await _adminService.GetBillingSupportQueueAsync(cancellationToken);
        return new OkObjectResult(queue);
    }

    [Function("AdminResolveBillingSupportRequest")]
    public async Task<IActionResult> ResolveBillingSupportRequest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "console/billing-support-requests/{requestId}/resolve")]
        HttpRequest request,
        string requestId,
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

        if (_adminService is null)
        {
            return AdminServiceUnavailable();
        }

        if (!Guid.TryParse(requestId, out var parsedRequestId))
        {
            return FunctionHttpResults.Problem(
                "Invalid billing support request id",
                "The admin billing support route requires a valid request id.",
                StatusCodes.Status400BadRequest);
        }

        var resolved = await _adminService.ResolveBillingSupportRequestAsync(
            admin.ExternalAuthUserId,
            admin.Email,
            parsedRequestId,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (resolved is null)
        {
            return FunctionHttpResults.Problem(
                "Billing support request not found",
                "No billing support request exists for the requested id.",
                StatusCodes.Status404NotFound);
        }

        return new OkObjectResult(resolved);
    }

    [Function("AdminAccountingRevenueCsv")]
    public async Task<IActionResult> ExportAccountingRevenueCsv(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "console/accounting/revenue.csv")]
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

        if (!TryParseDateQuery(request.Query, "from", out var fromInclusive) ||
            !TryParseDateQuery(request.Query, "to", out var toExclusive))
        {
            return FunctionHttpResults.Problem(
                "Invalid accounting export date range",
                "The accounting export requires valid from and to query parameters.",
                StatusCodes.Status400BadRequest);
        }

        if (toExclusive <= fromInclusive)
        {
            return FunctionHttpResults.Problem(
                "Invalid accounting export date range",
                "The accounting export to date must be after the from date.",
                StatusCodes.Status400BadRequest);
        }

        var response = request.HttpContext.Response;
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/csv; charset=utf-8";
        response.Headers.CacheControl = "no-store";
        response.Headers.ContentDisposition =
            $"attachment; filename=\"accounting-revenue-{fromInclusive.UtcDateTime:yyyyMMdd}-{toExclusive.UtcDateTime:yyyyMMdd}.csv\"";

        await _adminService.WriteAccountingRevenueCsvAsync(
            response.Body,
            fromInclusive,
            toExclusive,
            cancellationToken: cancellationToken);

        return new EmptyResult();
    }

    [Function("AdminPromoCodesCreate")]
    public async Task<IActionResult> CreatePromoCode(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "console/promo-codes")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminAccess.RequireAdminAsync(request, cancellationToken);
        if (admin is null)
        {
            return AdminForbidden();
        }

        if (_promoAdminService is null)
        {
            return AdminServiceUnavailable();
        }

        AdminPromoCodeCreateRequest? createRequest;
        try
        {
            createRequest = await ReadJsonRequestAsync<AdminPromoCodeCreateRequest>(request, cancellationToken);
        }
        catch (JsonException)
        {
            return FunctionHttpResults.Problem(
                "Invalid promo code request",
                "The promo code request body must be a JSON object.",
                StatusCodes.Status400BadRequest);
        }

        if (createRequest is null)
        {
            return FunctionHttpResults.Problem(
                "Invalid promo code request",
                "The promo code request body is required.",
                StatusCodes.Status400BadRequest);
        }

        var result = await _promoAdminService.CreatePromoCodeAsync(
            admin.ExternalAuthUserId,
            admin.Email,
            createRequest,
            DateTimeOffset.UtcNow,
            cancellationToken);

        return MapPromoMutationResult(result);
    }

    [Function("AdminPromoCodesList")]
    public async Task<IActionResult> ListPromoCodes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "console/promo-codes")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var forbidden = await RequireAdminResultAsync(request, cancellationToken);
        if (forbidden is not null)
        {
            return forbidden;
        }

        if (_promoAdminService is null)
        {
            return AdminServiceUnavailable();
        }

        var promoCodes = await _promoAdminService.ListPromoCodesAsync(DateTimeOffset.UtcNow, cancellationToken);
        return new OkObjectResult(promoCodes);
    }

    [Function("AdminPromoCodeDetail")]
    public async Task<IActionResult> GetPromoCodeDetail(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "console/promo-codes/{promoCodeId}")]
        HttpRequest request,
        string promoCodeId,
        CancellationToken cancellationToken)
    {
        var forbidden = await RequireAdminResultAsync(request, cancellationToken);
        if (forbidden is not null)
        {
            return forbidden;
        }

        if (_promoAdminService is null)
        {
            return AdminServiceUnavailable();
        }

        if (!Guid.TryParse(promoCodeId, out var parsedPromoCodeId))
        {
            return FunctionHttpResults.Problem(
                "Invalid promo code id",
                "The promo code detail route requires a valid promo code id.",
                StatusCodes.Status400BadRequest);
        }

        var detail = await _promoAdminService.GetPromoCodeDetailAsync(
            parsedPromoCodeId,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (detail is null)
        {
            return FunctionHttpResults.Problem(
                "Promo code not found",
                "No promo code exists for the requested id.",
                StatusCodes.Status404NotFound);
        }

        return new OkObjectResult(detail);
    }

    [Function("AdminPromoCodeUpdate")]
    public async Task<IActionResult> UpdatePromoCode(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "console/promo-codes/{promoCodeId}")]
        HttpRequest request,
        string promoCodeId,
        CancellationToken cancellationToken)
    {
        var admin = await _adminAccess.RequireAdminAsync(request, cancellationToken);
        if (admin is null)
        {
            return AdminForbidden();
        }

        if (_promoAdminService is null)
        {
            return AdminServiceUnavailable();
        }

        if (!Guid.TryParse(promoCodeId, out var parsedPromoCodeId))
        {
            return FunctionHttpResults.Problem(
                "Invalid promo code id",
                "The promo code update route requires a valid promo code id.",
                StatusCodes.Status400BadRequest);
        }

        AdminPromoCodeUpdateRequest? updateRequest;
        try
        {
            updateRequest = await ReadJsonRequestAsync<AdminPromoCodeUpdateRequest>(request, cancellationToken);
        }
        catch (JsonException)
        {
            return FunctionHttpResults.Problem(
                "Invalid promo code request",
                "The promo code request body must be a JSON object.",
                StatusCodes.Status400BadRequest);
        }

        if (updateRequest is null)
        {
            return FunctionHttpResults.Problem(
                "Invalid promo code request",
                "The promo code request body is required.",
                StatusCodes.Status400BadRequest);
        }

        var result = await _promoAdminService.UpdatePromoCodeAsync(
            admin.ExternalAuthUserId,
            admin.Email,
            parsedPromoCodeId,
            updateRequest,
            DateTimeOffset.UtcNow,
            cancellationToken);

        return MapPromoMutationResult(result);
    }

    [Function("AdminPromoCodeDisable")]
    public async Task<IActionResult> DisablePromoCode(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "console/promo-codes/{promoCodeId}/disable")]
        HttpRequest request,
        string promoCodeId,
        CancellationToken cancellationToken) =>
        await SetPromoCodeActiveAsync(request, promoCodeId, isActive: false, cancellationToken);

    [Function("AdminPromoCodeEnable")]
    public async Task<IActionResult> EnablePromoCode(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "console/promo-codes/{promoCodeId}/enable")]
        HttpRequest request,
        string promoCodeId,
        CancellationToken cancellationToken) =>
        await SetPromoCodeActiveAsync(request, promoCodeId, isActive: true, cancellationToken);

    [Function("AdminGrantCredits")]
    public async Task<IActionResult> GrantCredits(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "console/users/{userId}/credits")]
        HttpRequest request,
        string userId,
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

        if (_adminService is null)
        {
            return AdminServiceUnavailable();
        }

        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return FunctionHttpResults.Problem(
                "Invalid user id",
                "The admin credit route requires a valid user id.",
                StatusCodes.Status400BadRequest);
        }

        AdminCreditGrantRequest? grantRequest;
        try
        {
            grantRequest = await ReadCreditGrantRequestAsync(request, cancellationToken);
        }
        catch (JsonException)
        {
            return FunctionHttpResults.Problem(
                "Invalid credit request",
                "The credit request body must be a JSON object.",
                StatusCodes.Status400BadRequest);
        }

        if (grantRequest is null)
        {
            return FunctionHttpResults.Problem(
                "Invalid credit request",
                "The credit request body is required.",
                StatusCodes.Status400BadRequest);
        }

        var result = await _adminService.GrantCreditsAsync(
            admin.ExternalAuthUserId,
            admin.Email,
            parsedUserId,
            grantRequest,
            DateTimeOffset.UtcNow,
            cancellationToken);

        return result.Kind switch
        {
            AdminCreditGrantResultKind.Success => new OkObjectResult(result.Response),
            AdminCreditGrantResultKind.UserNotFound => FunctionHttpResults.Problem(
                "User not found",
                result.Detail,
                StatusCodes.Status404NotFound),
            _ => FunctionHttpResults.Problem(
                "Invalid credit request",
                result.Detail,
                StatusCodes.Status400BadRequest),
        };
    }

    [Function("AdminSetUserSuspension")]
    public async Task<IActionResult> SetUserSuspension(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "console/users/{userId}/suspension")]
        HttpRequest request,
        string userId,
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

        if (_adminService is null)
        {
            return AdminServiceUnavailable();
        }

        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return FunctionHttpResults.Problem(
                "Invalid user id",
                "The admin suspension route requires a valid user id.",
                StatusCodes.Status400BadRequest);
        }

        AdminSuspensionRequest? suspensionRequest;
        try
        {
            suspensionRequest = await ReadSuspensionRequestAsync(request, cancellationToken);
        }
        catch (JsonException)
        {
            return FunctionHttpResults.Problem(
                "Invalid suspension request",
                "The suspension request body must be a JSON object.",
                StatusCodes.Status400BadRequest);
        }

        if (suspensionRequest?.Suspended is null)
        {
            return FunctionHttpResults.Problem(
                "Invalid suspension request",
                "The suspension request body must include suspended.",
                StatusCodes.Status400BadRequest);
        }

        var result = await _adminService.SetUserSuspensionAsync(
            admin.ExternalAuthUserId,
            admin.Email,
            parsedUserId,
            suspensionRequest.Suspended.Value,
            DateTimeOffset.UtcNow,
            cancellationToken);

        return result.Kind switch
        {
            AdminSuspensionResultKind.Success => new OkObjectResult(result.Response),
            _ => FunctionHttpResults.Problem(
                "User not found",
                result.Detail,
                StatusCodes.Status404NotFound),
        };
    }

    [Function("AdminIssueRefund")]
    public async Task<IActionResult> IssueRefund(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "console/users/{userId}/refund")]
        HttpRequest request,
        string userId,
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

        if (_adminService is null)
        {
            return AdminServiceUnavailable();
        }

        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return FunctionHttpResults.Problem(
                "Invalid user id",
                "The admin refund route requires a valid user id.",
                StatusCodes.Status400BadRequest);
        }

        AdminRefundRequest? refundRequest;
        try
        {
            refundRequest = await ReadRefundRequestAsync(request, cancellationToken);
        }
        catch (JsonException)
        {
            return FunctionHttpResults.Problem(
                "Invalid refund request",
                "The refund request body must be a JSON object.",
                StatusCodes.Status400BadRequest);
        }

        if (refundRequest is null)
        {
            return FunctionHttpResults.Problem(
                "Invalid refund request",
                "The refund request body is required.",
                StatusCodes.Status400BadRequest);
        }

        var result = await _adminService.IssueRefundAsync(
            admin.ExternalAuthUserId,
            admin.Email,
            parsedUserId,
            refundRequest,
            cancellationToken);

        return result.Kind switch
        {
            AdminRefundResultKind.Success => new OkObjectResult(result.Response),
            AdminRefundResultKind.UserNotFound => FunctionHttpResults.Problem(
                "User not found",
                result.Detail,
                StatusCodes.Status404NotFound),
            AdminRefundResultKind.PaymentNotFound => FunctionHttpResults.Problem(
                "Payment not found",
                result.Detail,
                StatusCodes.Status404NotFound),
            AdminRefundResultKind.RefundUnavailable => FunctionHttpResults.Problem(
                "Refund service unavailable",
                result.Detail,
                StatusCodes.Status500InternalServerError),
            _ => FunctionHttpResults.Problem(
                "Invalid refund request",
                result.Detail,
                StatusCodes.Status400BadRequest),
        };
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

        return AdminForbidden();
    }

    private async Task<IActionResult> SetPromoCodeActiveAsync(
        HttpRequest request,
        string promoCodeId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var admin = await _adminAccess.RequireAdminAsync(request, cancellationToken);
        if (admin is null)
        {
            return AdminForbidden();
        }

        if (_promoAdminService is null)
        {
            return AdminServiceUnavailable();
        }

        if (!Guid.TryParse(promoCodeId, out var parsedPromoCodeId))
        {
            return FunctionHttpResults.Problem(
                "Invalid promo code id",
                "The promo code active-state route requires a valid promo code id.",
                StatusCodes.Status400BadRequest);
        }

        var result = await _promoAdminService.SetPromoCodeActiveAsync(
            admin.ExternalAuthUserId,
            admin.Email,
            parsedPromoCodeId,
            isActive,
            DateTimeOffset.UtcNow,
            cancellationToken);

        return MapPromoMutationResult(result);
    }

    private static IActionResult MapPromoMutationResult(AdminPromoMutationResult result) =>
        result.Kind switch
        {
            AdminPromoResultKind.Success => new OkObjectResult(result.Response),
            AdminPromoResultKind.NotFound => FunctionHttpResults.Problem(
                "Promo code not found",
                result.Detail,
                StatusCodes.Status404NotFound),
            AdminPromoResultKind.DuplicateCode => FunctionHttpResults.Problem(
                "Duplicate promo code",
                result.Detail,
                StatusCodes.Status400BadRequest),
            _ => FunctionHttpResults.Problem(
                "Invalid promo code request",
                result.Detail,
                StatusCodes.Status400BadRequest),
        };

    private static IActionResult AdminForbidden() =>
        FunctionHttpResults.Problem(
            "Admin access required",
            "The authenticated user is not allowed to access admin endpoints.",
            StatusCodes.Status403Forbidden);

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

    private static bool TryParseDateQuery(
        IQueryCollection query,
        string key,
        out DateTimeOffset value)
    {
        value = default;
        return query.TryGetValue(key, out var values) &&
            DateTimeOffset.TryParse(
                values.ToString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces |
                DateTimeStyles.AssumeUniversal |
                DateTimeStyles.AdjustToUniversal,
                out value);
    }

    private static async Task<AdminRefundRequest?> ReadRefundRequestAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        return JsonSerializer.Deserialize<AdminRefundRequest>(body, JsonOptions);
    }

    private static async Task<AdminCreditGrantRequest?> ReadCreditGrantRequestAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        return JsonSerializer.Deserialize<AdminCreditGrantRequest>(body, JsonOptions);
    }

    private static async Task<AdminSuspensionRequest?> ReadSuspensionRequestAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        return JsonSerializer.Deserialize<AdminSuspensionRequest>(body, JsonOptions);
    }

    private static async Task<T?> ReadJsonRequestAsync<T>(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(body, JsonOptions);
    }
}
