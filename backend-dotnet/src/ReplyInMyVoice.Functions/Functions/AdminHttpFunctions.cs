using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.UseCases.Admin;
using ReplyInMyVoice.Application.UseCases.PromoAdmin;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Http;
using AppCommon = ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class AdminHttpFunctions
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string AccountingRevenueCsvHeader =
        "date,userRef,sku,amount,currency,paymentIntent,receiptUrl,creditsGranted,creditsConsumed,creditsRemaining";

    private readonly AdminAccess _adminAccess;
    private readonly GetAdminUsersHandler _getAdminUsersHandler;
    private readonly GetAdminUserDetailHandler _getAdminUserDetailHandler;
    private readonly GetAdminStatsHandler _getAdminStatsHandler;
    private readonly DeleteAdminUserHandler _deleteAdminUserHandler;
    private readonly GrantCreditsHandler _grantCreditsHandler;
    private readonly GetBillingSupportQueueHandler _getBillingSupportQueueHandler;
    private readonly ResolveBillingSupportRequestHandler _resolveBillingSupportRequestHandler;
    private readonly ExportAccountingRevenueHandler _exportAccountingRevenueHandler;
    private readonly SetUserSuspensionHandler _setUserSuspensionHandler;
    private readonly IssueRefundHandler _issueRefundHandler;
    private readonly RetryWebhookDeliveryHandler _retryWebhookDeliveryHandler;
    private readonly CreatePromoCodeHandler _createPromoCodeHandler;
    private readonly ListPromoCodesHandler _listPromoCodesHandler;
    private readonly GetPromoCodeDetailHandler _getPromoCodeDetailHandler;
    private readonly UpdatePromoCodeHandler _updatePromoCodeHandler;
    private readonly SetPromoCodeActiveHandler _setPromoCodeActiveHandler;
    private readonly ArchivePromoCodeHandler _archivePromoCodeHandler;
    private readonly RestorePromoCodeHandler _restorePromoCodeHandler;

    public AdminHttpFunctions(
        IConfiguration configuration,
        GetAdminUsersHandler getAdminUsersHandler,
        GetAdminUserDetailHandler getAdminUserDetailHandler,
        GetAdminStatsHandler getAdminStatsHandler,
        DeleteAdminUserHandler deleteAdminUserHandler,
        GrantCreditsHandler grantCreditsHandler,
        GetBillingSupportQueueHandler getBillingSupportQueueHandler,
        ResolveBillingSupportRequestHandler resolveBillingSupportRequestHandler,
        ExportAccountingRevenueHandler exportAccountingRevenueHandler,
        SetUserSuspensionHandler setUserSuspensionHandler,
        IssueRefundHandler issueRefundHandler,
        RetryWebhookDeliveryHandler retryWebhookDeliveryHandler,
        CreatePromoCodeHandler createPromoCodeHandler,
        ListPromoCodesHandler listPromoCodesHandler,
        GetPromoCodeDetailHandler getPromoCodeDetailHandler,
        UpdatePromoCodeHandler updatePromoCodeHandler,
        SetPromoCodeActiveHandler setPromoCodeActiveHandler,
        ArchivePromoCodeHandler archivePromoCodeHandler,
        RestorePromoCodeHandler restorePromoCodeHandler)
    {
        _adminAccess = new AdminAccess(configuration);
        _getAdminUsersHandler = getAdminUsersHandler;
        _getAdminUserDetailHandler = getAdminUserDetailHandler;
        _getAdminStatsHandler = getAdminStatsHandler;
        _deleteAdminUserHandler = deleteAdminUserHandler;
        _grantCreditsHandler = grantCreditsHandler;
        _getBillingSupportQueueHandler = getBillingSupportQueueHandler;
        _resolveBillingSupportRequestHandler = resolveBillingSupportRequestHandler;
        _exportAccountingRevenueHandler = exportAccountingRevenueHandler;
        _setUserSuspensionHandler = setUserSuspensionHandler;
        _issueRefundHandler = issueRefundHandler;
        _retryWebhookDeliveryHandler = retryWebhookDeliveryHandler;
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

        var queue = await _getBillingSupportQueueHandler.HandleAsync(
            new GetBillingSupportQueueQuery(),
            cancellationToken);
        return new OkObjectResult(queue.Select(ToAdminBillingSupportRequest).ToList());
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

        var resolved = await _resolveBillingSupportRequestHandler.HandleAsync(
            new ResolveBillingSupportRequestCommand(
                admin.ExternalAuthUserId,
                admin.Email,
                parsedRequestId,
                DateTimeOffset.UtcNow),
            cancellationToken);
        if (resolved is null)
        {
            return FunctionHttpResults.Problem(
                "Billing support request not found",
                "No billing support request exists for the requested id.",
                StatusCodes.Status404NotFound);
        }

        return new OkObjectResult(ToAdminBillingSupportRequest(resolved));
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

        var export = await _exportAccountingRevenueHandler.HandleAsync(
            new ExportAccountingRevenueQuery(fromInclusive, toExclusive),
            cancellationToken);
        await WriteAccountingRevenueCsvAsync(
            response.Body,
            export,
            cancellationToken);

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

        PromoCodeCreateRequest? createRequest;
        try
        {
            createRequest = await ReadJsonRequestAsync<PromoCodeCreateRequest>(request, cancellationToken);
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

        if (!Guid.TryParse(promoCodeId, out var parsedPromoCodeId))
        {
            return FunctionHttpResults.Problem(
                "Invalid promo code id",
                "The promo code update route requires a valid promo code id.",
                StatusCodes.Status400BadRequest);
        }

        PromoCodeUpdateRequest? updateRequest;
        try
        {
            updateRequest = await ReadJsonRequestAsync<PromoCodeUpdateRequest>(request, cancellationToken);
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

        var result = await _setUserSuspensionHandler.HandleAsync(
            new SetUserSuspensionCommand(
                admin.ExternalAuthUserId,
                admin.Email,
                parsedUserId,
                suspensionRequest.Suspended.Value,
                DateTimeOffset.UtcNow),
            cancellationToken);

        return result.Kind switch
        {
            AppCommon.AdminSuspensionResultKind.Success => new OkObjectResult(
                ToAdminSuspensionResponse(result.Response!)),
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

        var result = await _issueRefundHandler.HandleAsync(
            new IssueRefundCommand(
                admin.ExternalAuthUserId,
                admin.Email,
                parsedUserId,
                refundRequest.PaymentIntentId,
                refundRequest.Amount,
                refundRequest.Currency),
            cancellationToken);

        return result.Kind switch
        {
            AppCommon.AdminRefundResultKind.Success => new OkObjectResult(ToAdminRefundResponse(result.Response!)),
            AppCommon.AdminRefundResultKind.UserNotFound => FunctionHttpResults.Problem(
                "User not found",
                result.Detail,
                StatusCodes.Status404NotFound),
            AppCommon.AdminRefundResultKind.PaymentNotFound => FunctionHttpResults.Problem(
                "Payment not found",
                result.Detail,
                StatusCodes.Status404NotFound),
            AppCommon.AdminRefundResultKind.RefundUnavailable => FunctionHttpResults.Problem(
                "Refund service unavailable",
                result.Detail,
                StatusCodes.Status500InternalServerError),
            _ => FunctionHttpResults.Problem(
                "Invalid refund request",
                result.Detail,
                StatusCodes.Status400BadRequest),
        };
    }

    [Function("AdminRetryWebhookDelivery")]
    public async Task<IActionResult> RetryWebhookDelivery(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "console/webhook-deliveries/{id}/retry")]
        HttpRequest request,
        string id,
        CancellationToken cancellationToken)
    {
        var forbidden = await RequireAdminResultAsync(request, cancellationToken);
        if (forbidden is not null)
        {
            return forbidden;
        }

        if (!Guid.TryParse(id, out var deliveryId))
        {
            return FunctionHttpResults.Problem(
                "Invalid webhook delivery id",
                "The admin webhook delivery retry route requires a valid delivery id.",
                StatusCodes.Status400BadRequest);
        }

        var result = await _retryWebhookDeliveryHandler.HandleAsync(
            new RetryWebhookDeliveryCommand(deliveryId, DateTimeOffset.UtcNow),
            cancellationToken);

        return result.Kind switch
        {
            AdminWebhookDeliveryRetryResultKind.Success => new OkObjectResult(
                new AdminWebhookDeliveryRetryResponse(
                    result.Id!.Value,
                    result.Status!.Value.ToString(),
                    result.AttemptCount!.Value,
                    result.NextAttemptAt!.Value)),
            AdminWebhookDeliveryRetryResultKind.NotFailed => FunctionHttpResults.Problem(
                "Webhook delivery is not failed",
                "Only failed webhook deliveries can be retried.",
                StatusCodes.Status409Conflict),
            _ => FunctionHttpResults.Problem(
                "Webhook delivery not found",
                "No webhook delivery exists for the requested id.",
                StatusCodes.Status404NotFound),
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
            AppCommon.AdminPromoResultKind.Success => new OkObjectResult(result.Response!),
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
            dto.GstTurnover,
            ToAdminPaymentReconciliationSummary(dto.PaymentReconciliation),
            ToAdminRefundReviewStats(dto.RefundReview));

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

    private static AdminBillingSupportRequest ToAdminBillingSupportRequest(
        AppCommon.AdminBillingSupportRequestDto dto) =>
        new(
            dto.Id,
            dto.UserId,
            dto.UserEmail,
            dto.ExternalAuthUserId,
            dto.Type,
            dto.RelatedPaymentIntentId,
            dto.Message,
            dto.Status,
            dto.CreatedAt,
            dto.UpdatedAt,
            dto.ResolvedAt);

    private static AdminSuspensionResponse ToAdminSuspensionResponse(
        AppCommon.AdminSuspensionResponseDto dto) =>
        new(dto.TargetUserId, dto.Suspended, dto.SuspendedAt);

    private static AdminRefundResponse ToAdminRefundResponse(AppCommon.AdminRefundResponseDto dto) =>
        new(
            dto.TargetUserId,
            dto.PaymentIntentId,
            dto.Amount,
            dto.Currency,
            dto.RefundId,
            dto.AlreadyRefunded);

    private static async Task WriteAccountingRevenueCsvAsync(
        Stream output,
        AppCommon.AdminAccountingRevenueExportDto export,
        CancellationToken cancellationToken)
    {
        using var writer = new StreamWriter(
            output,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 16 * 1024,
            leaveOpen: true)
        {
            NewLine = "\r\n",
        };

        await writer.WriteLineAsync(AccountingRevenueCsvHeader.AsMemory(), cancellationToken);
        foreach (var row in export.Rows)
        {
            await writer.WriteLineAsync(FormatAccountingRevenueCsvRow(row).AsMemory(), cancellationToken);
        }

        await writer.FlushAsync(cancellationToken);
    }

    private static string FormatAccountingRevenueCsvRow(AppCommon.AdminAccountingRevenueRowDto row) =>
        string.Join(",", new[]
        {
            CsvField(row.GrantedAt.ToString("O", CultureInfo.InvariantCulture)),
            CsvField(row.UserId.ToString("D")),
            CsvField(row.Sku),
            CsvField(row.AmountTotal?.ToString(CultureInfo.InvariantCulture)),
            CsvField(row.Currency),
            CsvField(row.PaymentIntentId),
            CsvField(null),
            CsvField(row.AmountGranted.ToString(CultureInfo.InvariantCulture)),
            CsvField(row.AmountConsumed.ToString(CultureInfo.InvariantCulture)),
            CsvField(row.CreditsRemaining.ToString(CultureInfo.InvariantCulture)),
        });

    private static string CsvField(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

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

    private sealed record PromoCodeCreateRequest(
        string? Code,
        string? Description,
        int? CreditsGranted,
        int? GrantTtlDays,
        DateTimeOffset? ValidFrom,
        DateTimeOffset? ValidUntil,
        int? MaxRedemptionsGlobal,
        int? MaxRedemptionsPerUser);

    private sealed record PromoCodeUpdateRequest(
        string? Description,
        int? CreditsGranted,
        int? GrantTtlDays,
        DateTimeOffset? ValidFrom,
        DateTimeOffset? ValidUntil,
        int? MaxRedemptionsGlobal,
        int? MaxRedemptionsPerUser);
}

public sealed record AdminUsersListResponse(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyList<AdminUserListItem> Users);

public sealed record AdminUserListItem(
    Guid Id,
    string ExternalAuthUserId,
    string? Email,
    string SubscriptionStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int UsedRewrites,
    int ReservedRewrites,
    int CreditRemaining,
    decimal CostToDateUsd);

public sealed record AdminUserDetailResponse(
    Guid Id,
    string ExternalAuthUserId,
    string? Email,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    AdminSubscriptionSummary Subscription,
    IReadOnlyList<AdminUsagePeriod> Usage,
    IReadOnlyList<AdminCredit> Credits,
    IReadOnlyList<AdminPayment> Payments,
    decimal CostToDateUsd);

public sealed record AdminSubscriptionSummary(
    string Status,
    string? StripeCustomerId,
    string? StripeSubscriptionId,
    DateTimeOffset? CurrentPeriodEnd);

public sealed record AdminUsagePeriod(
    Guid Id,
    string PeriodKey,
    int Quota,
    int Used,
    int Reserved,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminCredit(
    Guid Id,
    string Source,
    int AmountGranted,
    int AmountConsumed,
    int Remaining,
    DateTimeOffset GrantedAt,
    DateTimeOffset? ExpiresAt,
    string? StripeEventId,
    string? PaymentIntentId,
    string? Sku,
    long? AmountTotal,
    string? Currency,
    string? ReceiptUrl);

public sealed record AdminPayment(
    Guid CreditId,
    string Source,
    string? EventId,
    string? PaymentIntentId,
    string? Sku,
    long? AmountTotal,
    string? Currency,
    string? ReceiptUrl,
    DateTimeOffset GrantedAt,
    DateTimeOffset? ExpiresAt,
    int CreditsGranted,
    int CreditsConsumed,
    int CreditsRemaining);

public sealed record AdminStatsResponse(
    int TotalUsers,
    int PaidUsers,
    int FreeUsers,
    int UsageUsed,
    int UsageReserved,
    int CreditRemaining,
    int PaymentCount,
    long PaymentAmountTotal,
    decimal CostToDateUsd,
    AppCommon.TaxTurnoverReportDto GstTurnover,
    AdminPaymentReconciliationSummary? PaymentReconciliation,
    AdminRefundReviewStats RefundReview);

public sealed record AdminBillingSupportRequest(
    Guid Id,
    Guid UserId,
    string? UserEmail,
    string? ExternalAuthUserId,
    string Type,
    string? RelatedPaymentIntentId,
    string Message,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt);

public sealed record AdminPaymentReconciliationSummary(
    DateTimeOffset LastCompletedAt,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    int DiscrepancyCount,
    int PaidButNoGrantCount,
    int GrantButNoPaymentCount,
    int AmountMismatchCount,
    int StripePaymentCount,
    int PurchaseGrantCount);

public sealed record AdminRefundReviewStats(
    int FlaggedUserCount,
    int RefundCountThreshold,
    long RefundAmountThreshold,
    int TotalRefundCount,
    long TotalRefundAmount);

public sealed record AdminCreditGrantRequest(
    int? Amount,
    string? Reason);

public sealed record AdminCreditGrantResponse(
    Guid TargetUserId,
    Guid CreditId,
    string Source,
    int AmountGranted,
    int AmountConsumed,
    int Remaining,
    DateTimeOffset GrantedAt,
    DateTimeOffset? ExpiresAt);

public sealed record AdminDeleteUserResponse(
    Guid UserId,
    string Status);

public sealed record AdminSuspensionRequest(bool? Suspended);

public sealed record AdminSuspensionResponse(
    Guid TargetUserId,
    bool Suspended,
    DateTimeOffset? SuspendedAt);

public sealed record AdminRefundRequest(
    string? PaymentIntentId,
    long? Amount,
    string? Currency);

public sealed record AdminRefundResponse(
    Guid TargetUserId,
    string PaymentIntentId,
    long Amount,
    string? Currency,
    string? RefundId,
    bool AlreadyRefunded);

public sealed record AdminWebhookDeliveryRetryResponse(
    Guid Id,
    string Status,
    int AttemptCount,
    DateTimeOffset NextAttemptAt);
