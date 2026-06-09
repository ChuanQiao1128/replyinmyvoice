using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Account;
using ReplyInMyVoice.Application.UseCases.BillingSupport;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Http;
using ReplyInMyVoice.Infrastructure.Notifications;
using ReplyInMyVoice.Infrastructure.Services;
using AppBillingSupportRequestResultKind = ReplyInMyVoice.Application.Common.BillingSupportRequestResultKind;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class AccountHttpFunctions(
    IConfiguration configuration,
    GetAccountSummaryHandler getAccountSummaryHandler,
    GetPurchaseHistoryHandler getPurchaseHistoryHandler,
    GetBillingHistoryHandler getBillingHistoryHandler,
    GetBillingSupportRequestsHandler getBillingSupportRequestsHandler,
    GetOrCreateUserHandler getOrCreateUserHandler,
    CreateBillingSupportRequestHandler createBillingSupportRequestHandler,
    DeleteAccountHandler deleteAccountHandler,
    INotificationService notificationService)
{
    private const string SupportEmail = "info@timeawake.co.nz";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

        var account = await getAccountSummaryHandler.HandleAsync(
            new GetAccountSummaryQuery(authUser.ExternalAuthUserId, authUser.Email),
            cancellationToken);

        return new OkObjectResult(ToAccountSummary(account));
    }

    [Function("GetAccountPayments")]
    public async Task<IActionResult> GetAccountPayments(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me/payments")]
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

        var payments = await getPurchaseHistoryHandler.HandleAsync(
            new GetPurchaseHistoryQuery(authUser.ExternalAuthUserId, authUser.Email),
            cancellationToken);

        return new OkObjectResult(payments.Select(ToAccountPayment).ToList());
    }

    [Function("GetBillingHistory")]
    public async Task<IActionResult> GetBillingHistory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me/billing/history")]
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

        var history = await getBillingHistoryHandler.HandleAsync(
            new GetBillingHistoryQuery(authUser.ExternalAuthUserId, authUser.Email),
            cancellationToken);

        return new OkObjectResult(history.Select(ToAccountBillingHistoryItem).ToList());
    }

    [Function("GetBillingSupportRequests")]
    public async Task<IActionResult> GetBillingSupportRequests(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "billing-support-requests")]
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

        var user = await getOrCreateUserHandler.HandleAsync(
            new GetOrCreateUserCommand(authUser.ExternalAuthUserId, authUser.Email),
            cancellationToken);
        var requests = await getBillingSupportRequestsHandler.HandleAsync(
            new GetBillingSupportRequestsQuery(user.Id),
            cancellationToken);

        return new OkObjectResult(requests.Select(ToBillingSupportRequestResponse).ToList());
    }

    [Function("CreateBillingSupportRequest")]
    public async Task<IActionResult> CreateBillingSupportRequest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "billing-support-requests")]
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

        BillingSupportCreateRequest? createRequest;
        try
        {
            createRequest = await ReadBillingSupportCreateRequestAsync(request, cancellationToken);
        }
        catch (JsonException)
        {
            return FunctionHttpResults.Problem(
                "Invalid billing support request",
                "The billing support request body must be a JSON object.",
                StatusCodes.Status400BadRequest);
        }

        if (createRequest is null)
        {
            return FunctionHttpResults.Problem(
                "Invalid billing support request",
                "The billing support request body is required.",
                StatusCodes.Status400BadRequest);
        }

        var user = await getOrCreateUserHandler.HandleAsync(
            new GetOrCreateUserCommand(authUser.ExternalAuthUserId, authUser.Email),
            cancellationToken);
        var result = await createBillingSupportRequestHandler.HandleAsync(
            new CreateBillingSupportRequestCommand(
                user.Id,
                createRequest.Type,
                createRequest.RelatedPaymentIntentId,
                createRequest.Message,
                DateTimeOffset.UtcNow),
            cancellationToken);

        if (result.Kind == AppBillingSupportRequestResultKind.Success && result.Response is not null)
        {
            var response = ToBillingSupportRequestResponse(result.Response);
            await SendBillingSupportReceivedNotificationAsync(user.Email, response.Id, cancellationToken);
            return new CreatedResult(
                $"/api/billing-support-requests/{response.Id}",
                response);
        }

        return FunctionHttpResults.Problem(
            "Invalid billing support request",
            result.Detail ?? "The billing support request is invalid.",
            StatusCodes.Status400BadRequest);
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

        await deleteAccountHandler.HandleAsync(
            new DeleteAccountCommand(authUser.ExternalAuthUserId),
            cancellationToken);
        return new NoContentResult();
    }

    private async Task SendBillingSupportReceivedNotificationAsync(
        string? email,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await notificationService.SendAsync(
            NotificationTemplates.BillingSupportRequestReceived,
            new NotificationRecipient(email ?? string.Empty, email),
            new BillingSupportRequestReceivedNotificationModel(
                CustomerName: email ?? "there",
                SupportEmail: SupportEmail,
                RequestReference: $"request {requestId:N}"[..20]),
            cancellationToken);
    }

    private static async Task<BillingSupportCreateRequest?> ReadBillingSupportCreateRequestAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        return JsonSerializer.Deserialize<BillingSupportCreateRequest>(body, JsonOptions);
    }

    private static AccountSummary ToAccountSummary(AccountSummaryDto account) =>
        new(
            account.Id,
            account.ExternalAuthUserId,
            account.Email,
            account.SubscriptionStatus,
            account.PaymentGraceEndsAt,
            ToAccountUsageSummary(account.Usage),
            new AccountPromoSummary(
                account.Promo.HasRedeemed,
                account.Promo.Eligible,
                account.Promo.TrialRemaining,
                account.Promo.TrialExpiresAt));

    private static AccountUsageSummary ToAccountUsageSummary(AccountUsageSummaryDto usage) =>
        new(
            usage.Scope,
            usage.PeriodKey,
            usage.Quota,
            usage.Used,
            usage.Reserved,
            usage.Remaining,
            usage.Exhausted)
        {
            Sources = usage.Sources.Select(ToAccountUsageSource).ToList(),
        };

    private static AccountUsageSource ToAccountUsageSource(AccountUsageSourceDto source) =>
        new(
            source.Source,
            source.Label,
            source.Used,
            source.Limit,
            source.Reserved,
            source.Remaining,
            source.ExpiresAt,
            source.ExpiresInDays);

    private static AccountPayment ToAccountPayment(AccountPaymentDto payment) =>
        new(
            payment.Sku,
            payment.PaymentIntentId,
            payment.Amount,
            payment.Currency,
            payment.ReceiptUrl,
            payment.Date,
            payment.Expiry,
            payment.Remaining);

    private static AccountBillingHistoryItem ToAccountBillingHistoryItem(AccountBillingHistoryItemDto item) =>
        new(
            item.Type,
            item.Date,
            item.Description,
            item.Amount,
            item.Currency,
            item.Status,
            item.ReceiptUrl,
            item.HostedInvoiceUrl);

    private static BillingSupportRequestResponse ToBillingSupportRequestResponse(
        BillingSupportRequestResponseDto response) =>
        new(
            response.Id,
            response.UserId,
            response.Type,
            response.RelatedPaymentIntentId,
            response.Message,
            response.Status,
            response.CreatedAt,
            response.UpdatedAt,
            response.ResolvedAt);
}
