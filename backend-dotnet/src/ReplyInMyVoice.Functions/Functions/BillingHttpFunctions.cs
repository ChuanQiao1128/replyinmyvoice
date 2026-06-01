using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Http;
using ReplyInMyVoice.Infrastructure.Services;
using Stripe;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class BillingHttpFunctions(
    IConfiguration configuration,
    IStripeBillingService billingService,
    ILogger<BillingHttpFunctions> logger,
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

        var correlationId = ResolveCorrelationId(request);
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
            logger.LogError(
                ex,
                "{PaymentObservabilityEvent} checkout error for correlation {CorrelationId}, user {ExternalAuthUserId}, reason {PaymentFailureReason}.",
                "payment_failed",
                correlationId,
                authUser.ExternalAuthUserId,
                "billing_not_configured");
            return FunctionHttpResults.Problem(
                "Billing is not configured",
                null,
                StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex) when (IsBillingProviderFailure(ex, cancellationToken))
        {
            logger.LogError(
                ex,
                "{PaymentObservabilityEvent} checkout error for correlation {CorrelationId}, user {ExternalAuthUserId}, reason {PaymentFailureReason}.",
                "payment_failed",
                correlationId,
                authUser.ExternalAuthUserId,
                "billing_provider_failure");
            return FunctionHttpResults.Problem(
                "Billing provider request failed",
                null,
                StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "{PaymentObservabilityEvent} checkout error for correlation {CorrelationId}, user {ExternalAuthUserId}.",
                "payment_failed",
                correlationId,
                authUser.ExternalAuthUserId);
            throw;
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

        var correlationId = ResolveCorrelationId(request);
        try
        {
            var url = await billingService.CreatePortalSessionUrlAsync(authUser.ExternalAuthUserId, cancellationToken);
            return new OkObjectResult(new BillingUrlResponse(url));
        }
        catch (InvalidOperationException ex) when (ex.Message == "stripe_customer_missing")
        {
            logger.LogError(
                ex,
                "{PaymentObservabilityEvent} billing portal error for correlation {CorrelationId}, user {ExternalAuthUserId}, reason {PaymentFailureReason}.",
                "payment_failed",
                correlationId,
                authUser.ExternalAuthUserId,
                "stripe_customer_missing");
            return FunctionHttpResults.Problem(
                "Billing customer not found",
                null,
                StatusCodes.Status400BadRequest);
        }
        catch (Exception ex) when (IsBillingProviderFailure(ex, cancellationToken))
        {
            logger.LogError(
                ex,
                "{PaymentObservabilityEvent} billing portal error for correlation {CorrelationId}, user {ExternalAuthUserId}, reason {PaymentFailureReason}.",
                "payment_failed",
                correlationId,
                authUser.ExternalAuthUserId,
                "billing_provider_failure");
            return FunctionHttpResults.Problem(
                "Billing provider request failed",
                null,
                StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "{PaymentObservabilityEvent} billing portal error for correlation {CorrelationId}, user {ExternalAuthUserId}.",
                "payment_failed",
                correlationId,
                authUser.ExternalAuthUserId);
            throw;
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

    private static string ResolveCorrelationId(HttpRequest request)
    {
        var header = request.Headers["X-Correlation-Id"].ToString();
        return string.IsNullOrWhiteSpace(header)
            ? request.HttpContext.TraceIdentifier
            : header;
    }

    private static bool IsBillingProviderFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is StripeException ||
        exception is TaskCanceledException && !cancellationToken.IsCancellationRequested;

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
