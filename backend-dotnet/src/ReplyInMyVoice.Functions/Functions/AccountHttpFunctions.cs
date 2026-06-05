using System.Text.Json;
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
    AccountService accountService,
    BillingSupportService billingSupportService)
{
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

        var account = await accountService.GetOrCreateAccountSummaryAsync(
            authUser.ExternalAuthUserId,
            authUser.Email,
            cancellationToken);

        return new OkObjectResult(account);
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

        var payments = await accountService.GetPurchaseHistoryAsync(
            authUser.ExternalAuthUserId,
            authUser.Email,
            cancellationToken);

        return new OkObjectResult(payments);
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

        var history = await accountService.GetBillingHistoryAsync(
            authUser.ExternalAuthUserId,
            authUser.Email,
            cancellationToken);

        return new OkObjectResult(history);
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

        var user = await accountService.GetOrCreateUserAsync(
            authUser.ExternalAuthUserId,
            authUser.Email,
            cancellationToken);
        var requests = await billingSupportService.GetForUserAsync(user.Id, cancellationToken);
        return new OkObjectResult(requests);
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

        var user = await accountService.GetOrCreateUserAsync(
            authUser.ExternalAuthUserId,
            authUser.Email,
            cancellationToken);
        var result = await billingSupportService.CreateForUserAsync(
            user,
            createRequest,
            DateTimeOffset.UtcNow,
            cancellationToken);

        if (result.Kind == BillingSupportRequestResultKind.Success && result.Response is not null)
        {
            return new CreatedResult(
                $"/api/billing-support-requests/{result.Response.Id}",
                result.Response);
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

        await accountService.DeleteAccountAsync(authUser.ExternalAuthUserId, cancellationToken);
        return new NoContentResult();
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
}
