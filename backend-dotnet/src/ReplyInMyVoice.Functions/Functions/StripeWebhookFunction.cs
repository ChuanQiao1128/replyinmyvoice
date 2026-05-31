using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Functions.Http;
using ReplyInMyVoice.Infrastructure.Services;
using Stripe;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class StripeWebhookFunction(
    IConfiguration configuration,
    IHostEnvironment environment,
    StripeEventService stripeEventService,
    ILogger<StripeWebhookFunction> logger)
{
    [Function("StripeWebhook")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "stripe/webhook")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        var webhookSecret = configuration["STRIPE_WEBHOOK_SECRET"];
        var allowUnsignedWebhook = string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase);
        var requestCorrelationId = ResolveCorrelationId(request);

        string eventId;
        string eventType;

        if (!allowUnsignedWebhook && string.IsNullOrWhiteSpace(webhookSecret))
        {
            logger.LogError(
                "{PaymentObservabilityEvent} Stripe webhook configuration failed for correlation {CorrelationId}.",
                "webhook_failed",
                requestCorrelationId);
            return FunctionHttpResults.Problem(
                "Stripe webhook is not configured",
                null,
                StatusCodes.Status500InternalServerError);
        }

        if (!allowUnsignedWebhook)
        {
            var signature = request.Headers["Stripe-Signature"].ToString();
            if (string.IsNullOrWhiteSpace(signature))
            {
                logger.LogWarning(
                    "{PaymentObservabilityEvent} Stripe webhook rejected missing signature for correlation {CorrelationId}.",
                    "webhook_failed",
                    requestCorrelationId);
                return FunctionHttpResults.Problem(
                    "Invalid webhook event",
                    "Missing Stripe-Signature header.",
                    StatusCodes.Status400BadRequest);
            }

            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(rawBody, signature, webhookSecret);
            }
            catch (StripeException ex)
            {
                logger.LogWarning(
                    ex,
                    "{PaymentObservabilityEvent} Stripe webhook rejected invalid signature for correlation {CorrelationId}.",
                    "webhook_failed",
                    requestCorrelationId);
                return FunctionHttpResults.Problem(
                    "Invalid webhook event",
                    null,
                    StatusCodes.Status400BadRequest);
            }

            eventId = stripeEvent.Id;
            eventType = stripeEvent.Type;
        }
        else
        {
            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                eventId = doc.RootElement.GetProperty("id").GetString() ?? "";
                eventType = doc.RootElement.GetProperty("type").GetString() ?? "";
            }
            catch (JsonException ex)
            {
                logger.LogWarning(
                    ex,
                    "{PaymentObservabilityEvent} Stripe webhook rejected invalid JSON for correlation {CorrelationId}.",
                    "webhook_failed",
                    requestCorrelationId);
                return FunctionHttpResults.Problem(
                    "Invalid webhook JSON",
                    null,
                    StatusCodes.Status400BadRequest);
            }
        }

        if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(eventType))
        {
            logger.LogWarning(
                "{PaymentObservabilityEvent} Stripe webhook rejected missing event fields for correlation {CorrelationId}.",
                "webhook_failed",
                requestCorrelationId);
            return FunctionHttpResults.Problem(
                "Invalid webhook event",
                "Event id and type are required.",
                StatusCodes.Status400BadRequest);
        }

        bool processed;
        try
        {
            processed = await stripeEventService.ProcessWebhookEventAsync(
                eventId,
                eventType,
                rawBody,
                DateTimeOffset.UtcNow,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "{PaymentObservabilityEvent} Stripe webhook processing failed for correlation {CorrelationId}, event {EventId} of type {EventType}.",
                "webhook_failed",
                eventId,
                eventId,
                eventType);
            throw;
        }

        return new OkObjectResult(new { received = true, processed });
    }

    private static string ResolveCorrelationId(HttpRequest request)
    {
        var header = request.Headers["X-Correlation-Id"].ToString();
        return string.IsNullOrWhiteSpace(header)
            ? request.HttpContext.TraceIdentifier
            : header;
    }
}
