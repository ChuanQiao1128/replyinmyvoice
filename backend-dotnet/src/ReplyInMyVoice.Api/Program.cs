using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;
using Stripe;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
Console.Error.WriteLine("TRACE api: builder created");

builder.Services.AddEndpointsApiExplorer();
Console.Error.WriteLine("TRACE api: endpoints registered");
builder.Services.AddSwaggerGen();
Console.Error.WriteLine("TRACE api: swagger registered");
builder.Services.AddApplicationInsightsTelemetry();
Console.Error.WriteLine("TRACE api: app insights registered");

builder.Services.AddReplyInMyVoiceInfrastructure(builder.Configuration);
Console.Error.WriteLine("TRACE api: infrastructure registered");

var app = builder.Build();
Console.Error.WriteLine("TRACE api: app built");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { ok = true, service = "replyinmyvoice-api" }));

app.MapGet("/api/me", async (
    HttpRequest httpRequest,
    ReplyInMyVoice.Infrastructure.Services.AccountService accountService,
    CancellationToken cancellationToken) =>
{
    var externalUserId = ResolveExternalUserId(httpRequest, app.Environment, builder.Configuration);
    if (string.IsNullOrWhiteSpace(externalUserId))
    {
        return Results.Problem(
            title: "Authentication required",
            detail: "A valid authenticated user is required.",
            statusCode: StatusCodes.Status401Unauthorized);
    }

    var account = await accountService.GetOrCreateAccountSummaryAsync(
        externalUserId,
        ResolveRequestEmail(httpRequest, app.Environment, builder.Configuration),
        cancellationToken);

    return Results.Ok(account);
});

app.MapGet("/api/me/payments", async (
    HttpRequest httpRequest,
    ReplyInMyVoice.Infrastructure.Services.AccountService accountService,
    CancellationToken cancellationToken) =>
{
    var externalUserId = ResolveExternalUserId(httpRequest, app.Environment, builder.Configuration);
    if (string.IsNullOrWhiteSpace(externalUserId))
    {
        return Results.Problem(
            title: "Authentication required",
            detail: "A valid authenticated user is required.",
            statusCode: StatusCodes.Status401Unauthorized);
    }

    var payments = await accountService.GetPurchaseHistoryAsync(
        externalUserId,
        ResolveRequestEmail(httpRequest, app.Environment, builder.Configuration),
        cancellationToken);

    return Results.Ok(payments);
});

app.MapPost("/api/rewrite", async (
    HttpRequest httpRequest,
    [FromBody] RewriteRequest request,
    ReplyInMyVoice.Infrastructure.Services.AccountService accountService,
    RewriteRequestService rewriteRequestService,
    CancellationToken cancellationToken) =>
{
    var externalUserId = ResolveExternalUserId(httpRequest, app.Environment, builder.Configuration);
    if (string.IsNullOrWhiteSpace(externalUserId))
    {
        return Results.Problem(
            title: "Authentication required",
            detail: "A valid authenticated user is required.",
            statusCode: StatusCodes.Status401Unauthorized);
    }

    var idempotencyKey = httpRequest.Headers["X-Idempotency-Key"].ToString();
    if (string.IsNullOrWhiteSpace(idempotencyKey))
    {
        return Results.Problem(
            title: "Missing idempotency key",
            detail: "X-Idempotency-Key is required for rewrite requests.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    var validationError = ValidateRewriteRequest(request);
    if (validationError is not null)
    {
        return Results.Problem(
            title: "Invalid rewrite request",
            detail: validationError,
            statusCode: StatusCodes.Status400BadRequest);
    }

    var user = await accountService.GetOrCreateUserAsync(
        externalUserId,
        ResolveRequestEmail(httpRequest, app.Environment, builder.Configuration),
        cancellationToken);
    var plan = ReplyInMyVoice.Infrastructure.Services.AccountService.GetUsagePlan(user);

    var result = await rewriteRequestService.CreateAttemptAsync(
        user.Id,
        idempotencyKey,
        request,
        plan.PeriodKey,
        plan.QuotaLimit,
        DateTimeOffset.UtcNow,
        cancellationToken);

    if (result.Kind == ReserveRewriteResultKind.QuotaExceeded)
    {
        return Results.Problem(
            title: "Rewrite quota exhausted",
            detail: "No rewrite quota remains for the current period.",
            statusCode: StatusCodes.Status402PaymentRequired);
    }

    if (result.Kind == ReserveRewriteResultKind.Conflict)
    {
        return Results.Problem(
            title: "Idempotency key conflict",
            detail: "The same idempotency key was reused with a different rewrite request.",
            statusCode: StatusCodes.Status409Conflict);
    }

    var response = new RewriteAttemptResponse(
        result.AttemptId,
        result.Status.ToString(),
        result.ResultJson,
        result.ErrorCode);

    return result.Status == RewriteAttemptStatus.Succeeded
        ? Results.Ok(response)
        : Results.Accepted($"/api/rewrite-attempts/{result.AttemptId}", response);
});

app.MapGet("/api/rewrite-attempts/{attemptId:guid}", async (
    Guid attemptId,
    HttpRequest httpRequest,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var externalUserId = ResolveExternalUserId(httpRequest, app.Environment, builder.Configuration);
    if (string.IsNullOrWhiteSpace(externalUserId))
    {
        return Results.Problem(
            title: "Authentication required",
            statusCode: StatusCodes.Status401Unauthorized);
    }

    var user = await db.AppUsers.SingleOrDefaultAsync(
        x => x.ExternalAuthUserId == externalUserId,
        cancellationToken);

    if (user is null)
    {
        return Results.NotFound();
    }

    var attempt = await db.RewriteAttempts
        .AsNoTracking()
        .SingleOrDefaultAsync(
            x => x.Id == attemptId && x.UserId == user.Id,
            cancellationToken);

    if (attempt is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(new RewriteAttemptResponse(
        attempt.Id,
        attempt.Status.ToString(),
        attempt.ResultJson,
        attempt.ErrorCode));
});

app.MapPost("/api/stripe/checkout", async (
    HttpRequest httpRequest,
    IStripeBillingService billingService,
    ICheckoutVelocityLimiter checkoutVelocityLimiter,
    CancellationToken cancellationToken) =>
{
    var externalUserId = ResolveExternalUserId(httpRequest, app.Environment, builder.Configuration);
    if (string.IsNullOrWhiteSpace(externalUserId))
    {
        return Results.Problem(
            title: "Authentication required",
            statusCode: StatusCodes.Status401Unauthorized);
    }

    try
    {
        var checkoutRequest = await ReadCheckoutSessionRequestAsync(httpRequest, cancellationToken);
        var sku = checkoutRequest?.Sku?.Trim();
        if (!string.IsNullOrWhiteSpace(sku) && !StripeBillingService.IsKnownSku(sku))
        {
            return Results.Problem(
                title: "Unknown checkout SKU",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var velocity = checkoutVelocityLimiter.Check(externalUserId);
        if (!velocity.Allowed)
        {
            SetRetryAfter(httpRequest, velocity);
            return Results.Problem(
                title: "Checkout rate limit reached",
                detail: "Too many checkout sessions were requested. Please wait before trying again.",
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        var url = await billingService.CreateCheckoutSessionUrlAsync(
            externalUserId,
            ResolveRequestEmail(httpRequest, app.Environment, builder.Configuration),
            string.IsNullOrWhiteSpace(sku) ? null : sku,
            cancellationToken);
        return Results.Ok(new BillingUrlResponse(url));
    }
    catch (JsonException)
    {
        return Results.Problem(
            title: "Invalid checkout request",
            statusCode: StatusCodes.Status400BadRequest);
    }
    catch (InvalidOperationException ex) when (ex.Message.EndsWith("_missing", StringComparison.Ordinal))
    {
        return Results.Problem(
            title: "Billing is not configured",
            statusCode: StatusCodes.Status500InternalServerError);
    }
    catch (Exception ex) when (IsBillingProviderFailure(ex, cancellationToken))
    {
        return Results.Problem(
            title: "Billing provider request failed",
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/stripe/portal", async (
    HttpRequest httpRequest,
    IStripeBillingService billingService,
    CancellationToken cancellationToken) =>
{
    var externalUserId = ResolveExternalUserId(httpRequest, app.Environment, builder.Configuration);
    if (string.IsNullOrWhiteSpace(externalUserId))
    {
        return Results.Problem(
            title: "Authentication required",
            statusCode: StatusCodes.Status401Unauthorized);
    }

    try
    {
        var url = await billingService.CreatePortalSessionUrlAsync(externalUserId, cancellationToken);
        return Results.Ok(new BillingUrlResponse(url));
    }
    catch (InvalidOperationException ex) when (ex.Message == "stripe_customer_missing")
    {
        return Results.Problem(
            title: "Billing customer not found",
            statusCode: StatusCodes.Status400BadRequest);
    }
});

app.MapPost("/api/stripe/webhook", async (
    HttpRequest request,
    IConfiguration configuration,
    StripeEventService stripeEventService,
    CancellationToken cancellationToken) =>
{
    using var reader = new StreamReader(request.Body);
    var rawBody = await reader.ReadToEndAsync(cancellationToken);

    string eventId;
    string eventType;
    var webhookSecret = configuration["STRIPE_WEBHOOK_SECRET"];

    if (!string.IsNullOrWhiteSpace(webhookSecret) && !app.Environment.IsEnvironment("Testing"))
    {
        var signature = request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrWhiteSpace(signature))
        {
            return Results.Problem(
                title: "Invalid webhook event",
                detail: "Missing Stripe-Signature header.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(rawBody, signature, webhookSecret);
        }
        catch (StripeException)
        {
            return Results.Problem(
                title: "Invalid webhook event",
                statusCode: StatusCodes.Status400BadRequest);
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
            return Results.Problem(
                title: "Invalid webhook JSON",
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(eventType))
    {
        return Results.Problem(
            title: "Invalid webhook event",
            detail: "Event id and type are required.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    var processed = await stripeEventService.ProcessWebhookEventAsync(
        eventId,
        eventType,
        rawBody,
        DateTimeOffset.UtcNow,
        cancellationToken);

    return Results.Ok(new { received = true, processed });
});

app.Run();

static string? ValidateRewriteRequest(RewriteRequest request)
{
    if (request.RoughDraftReply.Trim().Length < 10)
    {
        return "Rough draft reply must be at least 10 characters.";
    }

    if (request.RoughDraftReply.Length > 5000)
    {
        return "Rough draft reply must be 5000 characters or less.";
    }

    if (request.MessageToReplyTo?.Length > 5000)
    {
        return "Message to reply to must be 5000 characters or less.";
    }

    if (request.Tone is not ("warm" or "direct"))
    {
        return "Tone must be warm or direct.";
    }

    return null;
}

static async Task<CheckoutSessionRequest?> ReadCheckoutSessionRequestAsync(
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

static void SetRetryAfter(HttpRequest request, CheckoutVelocityLimitResult velocity)
{
    if (velocity.RetryAfter is not { } retryAfter)
    {
        return;
    }

    var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
    request.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
}

static string? ResolveExternalUserId(
    HttpRequest request,
    IWebHostEnvironment environment,
    IConfiguration configuration)
{
    var headerUserId = request.Headers["X-External-User-Id"].ToString();
    var allowHeaderAuth = !IsProductionEnvironment(environment, configuration) &&
        (environment.IsDevelopment() ||
            environment.IsEnvironment("Testing") ||
            string.Equals(configuration["ALLOW_HEADER_AUTH"], "true", StringComparison.OrdinalIgnoreCase));

    if (allowHeaderAuth && !string.IsNullOrWhiteSpace(headerUserId))
    {
        return headerUserId;
    }

    if (request.HttpContext.User.Identity?.IsAuthenticated == true)
    {
        // Canonical user key = the Entra `oid` (stable across the ID token and the access token);
        // `sub` is pairwise per audience and differs between them. See FunctionAuthResolver for the
        // full rationale. Check raw `oid` + the inbound-mapped long URI before falling back to `sub`.
        return request.HttpContext.User.FindFirstValue("oid") ??
            request.HttpContext.User.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier") ??
            request.HttpContext.User.FindFirstValue("sub") ??
            request.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    return null;
}

static string? ResolveRequestEmail(
    HttpRequest request,
    IWebHostEnvironment environment,
    IConfiguration configuration)
{
    var allowHeaderAuth = !IsProductionEnvironment(environment, configuration) &&
        (environment.IsDevelopment() ||
            environment.IsEnvironment("Testing") ||
            string.Equals(configuration["ALLOW_HEADER_AUTH"], "true", StringComparison.OrdinalIgnoreCase));

    if (allowHeaderAuth)
    {
        var headerEmail = request.Headers["X-User-Email"].ToString();
        if (!string.IsNullOrWhiteSpace(headerEmail))
        {
            return headerEmail;
        }
    }

    return request.HttpContext.User.FindFirstValue(ClaimTypes.Email) ??
        request.HttpContext.User.FindFirstValue("email") ??
        request.HttpContext.User.FindFirstValue("emails") ??
        request.HttpContext.User.FindFirstValue("preferred_username");
}

static bool IsProductionEnvironment(
    IWebHostEnvironment environment,
    IConfiguration configuration) =>
    environment.IsProduction() ||
    IsProductionEnvironmentName(configuration["ASPNETCORE_ENVIRONMENT"]) ||
    IsProductionEnvironmentName(configuration["AZURE_FUNCTIONS_ENVIRONMENT"]);

static bool IsProductionEnvironmentName(string? environmentName) =>
    string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);

static bool IsBillingProviderFailure(Exception exception, CancellationToken cancellationToken) =>
    exception is StripeException ||
    exception is TaskCanceledException && !cancellationToken.IsCancellationRequested;

public sealed record RewriteAttemptResponse(
    Guid AttemptId,
    string Status,
    string? ResultJson,
    string? ErrorCode);

public sealed record BillingUrlResponse(string Url);

public sealed record CheckoutSessionRequest(string? Sku);

public partial class Program;
