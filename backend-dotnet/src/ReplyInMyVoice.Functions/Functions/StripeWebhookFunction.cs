using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ReplyInMyVoice.Functions.Http;
using ReplyInMyVoice.Infrastructure.Services;
using Stripe;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class StripeWebhookFunction(
    IConfiguration configuration,
    IHostEnvironment environment,
    StripeEventService stripeEventService)
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

        string eventId;
        string eventType;

        if (!string.IsNullOrWhiteSpace(webhookSecret) &&
            !string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase))
        {
            var signature = request.Headers["Stripe-Signature"].ToString();
            if (string.IsNullOrWhiteSpace(signature))
            {
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
            catch (StripeException)
            {
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
            catch (JsonException)
            {
                return FunctionHttpResults.Problem(
                    "Invalid webhook JSON",
                    null,
                    StatusCodes.Status400BadRequest);
            }
        }

        if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(eventType))
        {
            return FunctionHttpResults.Problem(
                "Invalid webhook event",
                "Event id and type are required.",
                StatusCodes.Status400BadRequest);
        }

        var processed = await stripeEventService.ProcessWebhookEventAsync(
            eventId,
            eventType,
            rawBody,
            DateTimeOffset.UtcNow,
            cancellationToken);

        return new OkObjectResult(new { received = true, processed });
    }
}
