using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.UseCases.Admin;
using ReplyInMyVoice.Application.UseCases.PromoAdmin;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Http;
using ReplyInMyVoice.Infrastructure.Services;
using AppCommon = ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class AdminHttpFunctions
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AdminAccess _adminAccess;
    private readonly AdminService _adminService;
    private readonly GetAdminUsersHandler _getAdminUsersHandler;
    private readonly GetAdminUserDetailHandler _getAdminUserDetailHandler;
    private readonly GetAdminStatsHandler _getAdminStatsHandler;
    private readonly DeleteAdminUserHandler _deleteAdminUserHandler;
    private readonly GrantCreditsHandler _grantCreditsHandler;
    private readonly CreatePromoCodeHandler _createPromoCodeHandler;
    private readonly ListPromoCodesHandler _listPromoCodesHandler;
    private readonly GetPromoCodeDetailHandler _getPromoCodeDetailHandler;
    private readonly UpdatePromoCodeHandler _updatePromoCodeHandler;
    private readonly SetPromoCodeActiveHandler _setPromoCodeActiveHandler;
    private readonly ArchivePromoCodeHandler _archivePromoCodeHandler;
    private readonly RestorePromoCodeHandler _restorePromoCodeHandler;

    public AdminHttpFunctions(
        IConfiguration configuration,
        AdminService adminService,
        GetAdminUsersHandler getAdminUsersHandler,
        GetAdminUserDetailHandler getAdminUserDetailHandler,
        GetAdminStatsHandler getAdminStatsHandler,
        DeleteAdminUserHandler deleteAdminUserHandler,
        GrantCreditsHandler grantCreditsHandler,
        CreatePromoCodeHandler createPromoCodeHandler,
        ListPromoCodesHandler listPromoCodesHandler,
        GetPromoCodeDetailHandler getPromoCodeDetailHandler,
        UpdatePromoCodeHandler updatePromoCodeHandler,
        SetPromoCodeActiveHandler setPromoCodeActiveHandler,
        ArchivePromoCodeHandler archivePromoCodeHandler,
        RestorePromoCodeHandler restorePromoCodeHandler)
    {
        _adminAccess = new AdminAccess(configuration);
        _adminService = adminService;
        _getAdminUsersHandler = getAdminUsersHandler;
        _getAdminUserDetailHandler = getAdminUserDetailHandler;
        _getAdminStatsHandler = getAdminStatsHandler;
        _deleteAdminUserHandler = deleteAdminUserHandler;
        _grantCreditsHandler = grantCreditsHandler;
        _createPromoCodeHandler = createPromoCodeHandler;
        _listPromoCodesHandler = listPromoCodesHandler;
        _getPromoCodeDetailHandler = getPromoCodeDetailHandler;
        _updatePromoCodeHandler = updatePromoCodeHandler;
        _setPromoCodeActiveHandler = setPromoCodeActiveHandler;
        _archivePromoCodeHandler = archivePromoCodeHandler;
        _restorePromoCodeHandler = restorePromoCodeHandler;
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

        var page = ParsePositiveInt(request.Query, "page", DefaultPage, int.MaxValue);
        var pageSize = ParsePositiveInt(request.Query, "pageSize", DefaultPageSize, MaxPageSize);
        var users = await _getAdminUsersHandler.HandleAsync(
            new GetAdminUsersQuery(page, pageSize),
            cancellationToken);
        return new OkObjectResult(ToAdminUsersListResponse(users));
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

        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return FunctionHttpResults.Problem(
                "Invalid user id",
                "The admin user detail route requires a valid user id.",
                StatusCodes.Status400BadRequest);
        }

        var detail = await _getAdminUserDetailHandler.HandleAsync(
            new GetAdminUserDetailQuery(parsedUserId),
            cancellationToken);
        if (detail is null)
        {
            return FunctionHttpResults.Problem(
                "User not found",
                "No user exists for the requested id.",
                StatusCodes.Status404NotFound);
        }

        return new OkObjectResult(ToAdminUserDetailResponse(detail));
    }

    [Function("AdminDeleteUser")]
    public async Task<IActionResult> DeleteUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "console/users/{userId}")]
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

        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return FunctionHttpResults.Problem(
                "Invalid user id",
                "The admin delete route requires a valid user id.",
                StatusCodes.Status400BadRequest);
        }

        var result = await _deleteAdminUserHandler.HandleAsync(
            new DeleteAdminUserCommand(
                admin.ExternalAuthUserId,
                admin.Email,
                parsedUserId,
                DateTimeOffset.UtcNow),
            cancellationToken);

        return result.Kind switch
        {
            AppCommon.AdminDeleteUserResultKind.Success => new OkObjectResult(ToAdminDeleteUserResponse(result.Response!)),
            AppCommon.AdminDeleteUserResultKind.UserNotFound => FunctionHttpResults.Problem(
                "User not found",
                result.Detail,
                StatusCodes.Status404NotFound),
            AppCommon.AdminDeleteUserResultKind.Forbidden => FunctionHttpResults.Problem(
                "Delete user forbidden",
                result.Detail,
                StatusCodes.Status403Forbidden),
            _ => FunctionHttpResults.Problem(
                "Invalid delete request",
                result.Detail,
                StatusCodes.Status400BadRequest),
        };
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

        var stats = await _getAdminStatsHandler.HandleAsync(
            new GetAdminStatsQuery(DateTimeOffset.UtcNow),
            cancellationToken);
        return new OkObjectResult(ToAdminStatsResponse(stats));
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

        // TODO(DDD): remaining AdminService use-case — DDD-63
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

        if (!Guid.TryParse(requestId, out var parsedRequestId))
        {
            return FunctionHttpResults.Problem(
                "Invalid billing support request id",
                "The admin billing support route requires a valid request id.",
                StatusCodes.Status400BadRequest);
        }

        // TODO(DDD): remaining AdminService use-case — DDD-63
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

        // TODO(DDD): remaining AdminService use-case — DDD-63
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

        var result = await _createPromoCodeHandler.HandleAsync(
            new CreatePromoCodeCommand(
                admin.ExternalAuthUserId,
                admin.Email,
                createRequest.Code,
                createRequest.Description,
                createRequest.CreditsGranted,
                createRequest.GrantTtlDays,
                createRequest.ValidFrom,
                createRequest.ValidUntil,
                createRequest.MaxRedemptionsGlobal,
                createRequest.MaxRedemptionsPerUser,
                DateTimeOffset.UtcNow),
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

        var promoCodes = await _listPromoCodesHandler.HandleAsync(
            new ListPromoCodesQuery(DateTimeOffset.UtcNow),
            cancellationToken);
        return new OkObjectResult(ToAdminPromoCodesListResponse(promoCodes));
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

        if (!Guid.TryParse(promoCodeId, out var parsedPromoCodeId))
        {
            return FunctionHttpResults.Problem(
                "Invalid promo code id",
                "The promo code detail route requires a valid promo code id.",
                StatusCodes.Status400BadRequest);
        }

        var detail = await _getPromoCodeDetailHandler.HandleAsync(
            new GetPromoCodeDetailQuery(parsedPromoCodeId, DateTimeOffset.UtcNow),
            cancellationToken);
        if (detail is null)
        {
            return FunctionHttpResults.Problem(
                "Promo code not found",
                "No promo code exists for the requested id.",
                StatusCodes.Status404NotFound);
        }

        return new OkObjectResult(ToAdminPromoCodeDetailResponse(detail));
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

        var result = await _updatePromoCodeHandler.HandleAsync(
            new UpdatePromoCodeCommand(
                admin.ExternalAuthUserId,
                admin.Email,
                parsedPromoCodeId,
                updateRequest.Description,
                updateRequest.CreditsGranted,
                updateRequest.GrantTtlDays,
                updateRequest.ValidFrom,
                updateRequest.ValidUntil,
                updateRequest.MaxRedemptionsGlobal,
                updateRequest.MaxRedemptionsPerUser,
                DateTimeOffset.UtcNow),
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

    [Function("AdminPromoCodeArchive")]
    public async Task<IActionResult> ArchivePromoCode(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "console/promo-codes/{promoCodeId}/archive")]
        HttpRequest request,
        string promoCodeId,
        CancellationToken cancellationToken) =>
        await ArchiveOrRestorePromoCodeAsync(request, promoCodeId, archive: true, cancellationToken);

    [Function("AdminPromoCodeRestore")]
    public async Task<IActionResult> RestorePromoCode(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "console/promo-codes/{promoCodeId}/restore")]
        HttpRequest request,
        string promoCodeId,
        CancellationToken cancellationToken) =>
        await ArchiveOrRestorePromoCodeAsync(request, promoCodeId, archive: false, cancellationToken);

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

        var result = await _grantCreditsHandler.HandleAsync(
            new GrantCreditsCommand(
                admin.ExternalAuthUserId,
                admin.Email,
                parsedUserId,
                grantRequest.Amount,
                grantRequest.Reason,
                DateTimeOffset.UtcNow),
            cancellationToken);

        return result.Kind switch
        {
            AppCommon.AdminCreditGrantResultKind.Success => new OkObjectResult(ToAdminCreditGrantResponse(result.Response!)),
            AppCommon.AdminCreditGrantResultKind.UserNotFound => FunctionHttpResults.Problem(
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

        // TODO(DDD): remaining AdminService use-case — DDD-63
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

        // TODO(DDD): remaining AdminService use-case — DDD-63
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

        if (!Guid.TryParse(promoCodeId, out var parsedPromoCodeId))
        {
            return FunctionHttpResults.Problem(
                "Invalid promo code id",
                "The promo code active-state route requires a valid promo code id.",
                StatusCodes.Status400BadRequest);
        }

        var result = await _setPromoCodeActiveHandler.HandleAsync(
            new SetPromoCodeActiveCommand(
                admin.ExternalAuthUserId,
                admin.Email,
                parsedPromoCodeId,
                isActive,
                DateTimeOffset.UtcNow),
            cancellationToken);

        return MapPromoMutationResult(result);
    }

    private async Task<IActionResult> ArchiveOrRestorePromoCodeAsync(
        HttpRequest request,
        string promoCodeId,
        bool archive,
        CancellationToken cancellationToken)
    {
        var admin = await _adminAccess.RequireAdminAsync(request, cancellationToken);
        if (admin is null)
        {
            return AdminForbidden();
        }

        if (!Guid.TryParse(promoCodeId, out var parsedPromoCodeId))
        {
            return FunctionHttpResults.Problem(
                "Invalid promo code id",
                "The promo code archive route requires a valid promo code id.",
                StatusCodes.Status400BadRequest);
        }

        var now = DateTimeOffset.UtcNow;
        var result = archive
            ? await _archivePromoCodeHandler.HandleAsync(
                new ArchivePromoCodeCommand(
                    admin.ExternalAuthUserId,
                    admin.Email,
                    parsedPromoCodeId,
                    now),
                cancellationToken)
            : await _restorePromoCodeHandler.HandleAsync(
                new RestorePromoCodeCommand(
                    admin.ExternalAuthUserId,
                    admin.Email,
                    parsedPromoCodeId,
                    now),
                cancellationToken);

        return MapPromoMutationResult(result);
    }

    private static IActionResult MapPromoMutationResult(AppCommon.AdminPromoMutationResultDto result) =>
        result.Kind switch
        {
            AppCommon.AdminPromoResultKind.Success => new OkObjectResult(ToAdminPromoCodeResponse(result.Response!)),
            AppCommon.AdminPromoResultKind.NotFound => FunctionHttpResults.Problem(
                "Promo code not found",
                result.Detail,
                StatusCodes.Status404NotFound),
            AppCommon.AdminPromoResultKind.DuplicateCode => FunctionHttpResults.Problem(
                "Duplicate promo code",
                result.Detail,
                StatusCodes.Status400BadRequest),
            _ => FunctionHttpResults.Problem(
                "Invalid promo code request",
                result.Detail,
                StatusCodes.Status400BadRequest),
        };

    private static AdminUsersListResponse ToAdminUsersListResponse(AppCommon.AdminUsersListDto dto) =>
        new(
            dto.Page,
            dto.PageSize,
            dto.TotalCount,
            dto.TotalPages,
            dto.Users.Select(ToAdminUserListItem).ToList());

    private static AdminUserListItem ToAdminUserListItem(AppCommon.AdminUserListItemDto dto) =>
        new(
            dto.Id,
            dto.ExternalAuthUserId,
            dto.Email,
            dto.SubscriptionStatus,
            dto.CreatedAt,
            dto.UpdatedAt,
            dto.UsedRewrites,
            dto.ReservedRewrites,
            dto.CreditRemaining,
            dto.CostToDateUsd);

    private static AdminUserDetailResponse ToAdminUserDetailResponse(AppCommon.AdminUserDetailDto dto) =>
        new(
            dto.Id,
            dto.ExternalAuthUserId,
            dto.Email,
            dto.CreatedAt,
            dto.UpdatedAt,
            ToAdminSubscriptionSummary(dto.Subscription),
            dto.Usage.Select(ToAdminUsagePeriod).ToList(),
            dto.Credits.Select(ToAdminCredit).ToList(),
            dto.Payments.Select(ToAdminPayment).ToList(),
            dto.CostToDateUsd);

    private static AdminSubscriptionSummary ToAdminSubscriptionSummary(AppCommon.AdminSubscriptionSummaryDto dto) =>
        new(
            dto.Status,
            dto.StripeCustomerId,
            dto.StripeSubscriptionId,
            dto.CurrentPeriodEnd);

    private static AdminUsagePeriod ToAdminUsagePeriod(AppCommon.AdminUsagePeriodDto dto) =>
        new(
            dto.Id,
            dto.PeriodKey,
            dto.Quota,
            dto.Used,
            dto.Reserved,
            dto.PeriodStart,
            dto.PeriodEnd,
            dto.CreatedAt,
            dto.UpdatedAt);

    private static AdminCredit ToAdminCredit(AppCommon.AdminCreditDto dto) =>
        new(
            dto.Id,
            dto.Source,
            dto.AmountGranted,
            dto.AmountConsumed,
            dto.Remaining,
            dto.GrantedAt,
            dto.ExpiresAt,
            dto.StripeEventId,
            dto.PaymentIntentId,
            dto.Sku,
            dto.AmountTotal,
            dto.Currency,
            dto.ReceiptUrl);

    private static AdminPayment ToAdminPayment(AppCommon.AdminPaymentDto dto) =>
        new(
            dto.CreditId,
            dto.Source,
            dto.EventId,
            dto.PaymentIntentId,
            dto.Sku,
            dto.AmountTotal,
            dto.Currency,
            dto.ReceiptUrl,
            dto.GrantedAt,
            dto.ExpiresAt,
            dto.CreditsGranted,
            dto.CreditsConsumed,
            dto.CreditsRemaining);

    private static AdminStatsResponse ToAdminStatsResponse(AppCommon.AdminStatsDto dto) =>
        new(
            dto.TotalUsers,
            dto.PaidUsers,
            dto.FreeUsers,
            dto.UsageUsed,
            dto.UsageReserved,
            dto.CreditRemaining,
            dto.PaymentCount,
            dto.PaymentAmountTotal,
            dto.CostToDateUsd,
            ToTaxTurnoverReport(dto.GstTurnover),
            ToAdminPaymentReconciliationSummary(dto.PaymentReconciliation),
            ToAdminRefundReviewStats(dto.RefundReview));

    private static TaxTurnoverReport ToTaxTurnoverReport(AppCommon.TaxTurnoverReportDto dto) =>
        new(
            dto.WindowStart,
            dto.WindowEnd,
            dto.Currency,
            dto.GrossAmountTotal,
            dto.RegistrationThresholdAmountTotal,
            dto.WarningFraction,
            dto.WarningAmountTotal,
            dto.FractionOfThreshold,
            dto.IgnoredNonNzdPaymentCount,
            dto.Warning is null
                ? null
                : new TaxTurnoverWarning(
                    dto.Warning.Code,
                    dto.Warning.Severity,
                    dto.Warning.Message),
            dto.Notification is null
                ? null
                : new TaxTurnoverNotificationResult(
                    dto.Notification.Attempted,
                    dto.Notification.Sent,
                    dto.Notification.Provider,
                    dto.Notification.Reason));

    private static AdminPaymentReconciliationSummary? ToAdminPaymentReconciliationSummary(
        AppCommon.AdminPaymentReconciliationSummaryDto? dto) =>
        dto is null
            ? null
            : new AdminPaymentReconciliationSummary(
                dto.LastCompletedAt,
                dto.WindowStart,
                dto.WindowEnd,
                dto.DiscrepancyCount,
                dto.PaidButNoGrantCount,
                dto.GrantButNoPaymentCount,
                dto.AmountMismatchCount,
                dto.StripePaymentCount,
                dto.PurchaseGrantCount);

    private static AdminRefundReviewStats ToAdminRefundReviewStats(AppCommon.AdminRefundReviewStatsDto dto) =>
        new(
            dto.FlaggedUserCount,
            dto.RefundCountThreshold,
            dto.RefundAmountThreshold,
            dto.TotalRefundCount,
            dto.TotalRefundAmount);

    private static AdminDeleteUserResponse ToAdminDeleteUserResponse(AppCommon.AdminDeleteUserResponseDto dto) =>
        new(dto.UserId, dto.Status);

    private static AdminCreditGrantResponse ToAdminCreditGrantResponse(AppCommon.AdminCreditGrantResponseDto dto) =>
        new(
            dto.TargetUserId,
            dto.CreditId,
            dto.Source,
            dto.AmountGranted,
            dto.AmountConsumed,
            dto.Remaining,
            dto.GrantedAt,
            dto.ExpiresAt);

    private static AdminPromoCodesListResponse ToAdminPromoCodesListResponse(AppCommon.AdminPromoCodesListDto dto) =>
        new(dto.PromoCodes.Select(ToAdminPromoCodeResponse).ToList());

    private static AdminPromoCodeDetailResponse ToAdminPromoCodeDetailResponse(
        AppCommon.AdminPromoCodeDetailDto dto) =>
        new(
            ToAdminPromoCodeResponse(dto.PromoCode),
            ToAdminPromoStats(dto.Stats));

    private static AdminPromoCodeResponse ToAdminPromoCodeResponse(AppCommon.AdminPromoCodeDto dto) =>
        new(
            dto.Id,
            dto.Code,
            dto.DisplayCode,
            dto.Description,
            dto.Kind,
            dto.CreditsGranted,
            dto.GrantTtlDays,
            dto.ValidFrom,
            dto.ValidUntil,
            dto.MaxRedemptionsGlobal,
            dto.MaxRedemptionsPerUser,
            dto.RedemptionCount,
            dto.IsActive,
            dto.ArchivedAt,
            dto.Status,
            dto.CreatedAt,
            dto.UpdatedAt);

    private static AdminPromoStats ToAdminPromoStats(AppCommon.AdminPromoStatsDto dto) =>
        new(
            dto.TotalRedemptions,
            dto.DistinctUsers,
            dto.ActivationRate,
            dto.DailyCurve
                .Select(x => new AdminPromoDailyRedemptions(x.Date, x.Redemptions))
                .ToList(),
            dto.IpHashClusters
                .Select(x => new AdminPromoIpHashCluster(
                    x.IpHash,
                    x.Redemptions,
                    x.DistinctUsers,
                    x.FirstRedeemedAt,
                    x.LastRedeemedAt))
                .ToList());

    private static IActionResult AdminForbidden() =>
        FunctionHttpResults.Problem(
            "Admin access required",
            "The authenticated user is not allowed to access admin endpoints.",
            StatusCodes.Status403Forbidden);

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
