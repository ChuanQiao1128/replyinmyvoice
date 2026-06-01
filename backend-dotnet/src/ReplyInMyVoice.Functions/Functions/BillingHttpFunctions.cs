using System.Globalization;
using System.Text.Json;
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
    IStripeBillingService billingService,
    ICheckoutVelocityLimiter? checkoutVelocityLimiter = null)
{
    private readonly ICheckoutVelocityLimiter checkoutLimiter = checkoutVelocityLimiter ?? new CheckoutVelocityLimiter();

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

        CheckoutSessionRequest? checkoutRequest;
        try
        {
            checkoutRequest = await ReadCheckoutSessionRequestAsync(request, cancellationToken);
        }
        catch (JsonException)
        {
            return FunctionHttpResults.Problem(
                "Invalid checkout request",
                null,
                StatusCodes.Status400BadRequest);
        }

        var sku = checkoutRequest?.Sku?.Trim();
        if (!string.IsNullOrWhiteSpace(sku) && !StripeBillingService.IsKnownSku(sku))
        {
            return FunctionHttpResults.Problem(
                "Unknown checkout SKU",
                null,
                StatusCodes.Status400BadRequest);
        }

        var velocity = checkoutLimiter.Check(authUser.ExternalAuthUserId);
        if (!velocity.Allowed)
        {
            SetRetryAfter(request, velocity);
            return FunctionHttpResults.Problem(
                "Checkout rate limit reached",
                "Too many checkout sessions were requested. Please wait before trying again.",
                StatusCodes.Status429TooManyRequests);
        }

        try
        {
            var url = await billingService.CreateCheckoutSessionUrlAsync(
                authUser.ExternalAuthUserId,
                authUser.Email,
                string.IsNullOrWhiteSpace(sku) ? null : sku,
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

    private static async Task<CheckoutSessionRequest?> ReadCheckoutSessionRequestAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        using var document = JsonDocument.Parse(body);
        if (document.RootElement.ValueKind == JsonValueKind.Undefined ||
            document.RootElement.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Checkout request must be a JSON object.");
        }

        if (!document.RootElement.TryGetProperty("sku", out var sku) ||
            sku.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return new CheckoutSessionRequest(sku.ValueKind == JsonValueKind.String ? sku.GetString() : sku.ToString());
    }

    private static void SetRetryAfter(HttpRequest request, CheckoutVelocityLimitResult velocity)
    {
        if (velocity.RetryAfter is not { } retryAfter)
        {
            return;
        }

        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        request.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
    }
}

public sealed record BillingUrlResponse(string Url);

public sealed record CheckoutSessionRequest(string? Sku);
