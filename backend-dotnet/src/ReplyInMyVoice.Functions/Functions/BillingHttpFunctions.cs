using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Http;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class BillingHttpFunctions(
    IConfiguration configuration,
    IStripeBillingService billingService)
{
    [Function("CreateCheckoutSession")]
    public async Task<IActionResult> CreateCheckoutSession(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "stripe/checkout")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var authUser = await FunctionAuthResolver.ResolveUserAsync(request, configuration, cancellationToken);
        if (authUser is null)
        {
            return FunctionHttpResults.Problem(
                "Authentication required",
                null,
                StatusCodes.Status401Unauthorized);
        }

        try
        {
            var url = await billingService.CreateCheckoutSessionUrlAsync(
                authUser.ExternalAuthUserId,
                authUser.Email,
                cancellationToken);
            return new OkObjectResult(new BillingUrlResponse(url));
        }
        catch (InvalidOperationException ex) when (ex.Message.EndsWith("_missing", StringComparison.Ordinal))
        {
            return FunctionHttpResults.Problem(
                "Billing is not configured",
                null,
                StatusCodes.Status500InternalServerError);
        }
    }

    [Function("CreateBillingPortalSession")]
    public async Task<IActionResult> CreateBillingPortalSession(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "stripe/portal")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var authUser = await FunctionAuthResolver.ResolveUserAsync(request, configuration, cancellationToken);
        if (authUser is null)
        {
            return FunctionHttpResults.Problem(
                "Authentication required",
                null,
                StatusCodes.Status401Unauthorized);
        }

        try
        {
            var url = await billingService.CreatePortalSessionUrlAsync(authUser.ExternalAuthUserId, cancellationToken);
            return new OkObjectResult(new BillingUrlResponse(url));
        }
        catch (InvalidOperationException ex) when (ex.Message == "stripe_customer_missing")
        {
            return FunctionHttpResults.Problem(
                "Billing customer not found",
                null,
                StatusCodes.Status400BadRequest);
        }
    }
}

public sealed record BillingUrlResponse(string Url);
