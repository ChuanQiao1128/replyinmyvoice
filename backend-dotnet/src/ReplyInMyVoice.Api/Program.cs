using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;
using Stripe;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplicationInsightsTelemetry();

var clerkIssuer = ResolveClerkIssuer(builder.Configuration);
if (!string.IsNullOrWhiteSpace(clerkIssuer))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = clerkIssuer;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = clerkIssuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(builder.Configuration["CLERK_JWT_AUDIENCE"]),
                ValidAudience = builder.Configuration["CLERK_JWT_AUDIENCE"],
                ValidateLifetime = true,
            };
        });
    builder.Services.AddAuthorization();
}

builder.Services.AddReplyInMyVoiceInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!string.IsNullOrWhiteSpace(clerkIssuer))
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapGet("/health", () => Results.Ok(new { ok = true, service = "replyinmyvoice-api" }));

app.MapPost("/api/rewrite", async (
    HttpRequest httpRequest,
    [FromBody] RewriteRequest request,
    AppDbContext db,
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

    var user = await GetOrCreateUserAsync(db, externalUserId, cancellationToken);
    var plan = GetUsagePlan(user);

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
        var url = await billingService.CreateCheckoutSessionUrlAsync(
            externalUserId,
            ResolveEmail(httpRequest),
            cancellationToken);
        return Results.Ok(new BillingUrlResponse(url));
    }
    catch (InvalidOperationException ex) when (ex.Message.EndsWith("_missing", StringComparison.Ordinal))
    {
        return Results.Problem(
            title: "Billing is not configured",
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

static async Task<AppUser> GetOrCreateUserAsync(
    AppDbContext db,
    string externalUserId,
    CancellationToken cancellationToken)
{
    var user = await db.AppUsers.SingleOrDefaultAsync(
        x => x.ExternalAuthUserId == externalUserId,
        cancellationToken);

    if (user is not null)
    {
        return user;
    }

    user = new AppUser
    {
        ExternalAuthUserId = externalUserId,
        SubscriptionStatus = SubscriptionStatus.Inactive,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };
    db.AppUsers.Add(user);
    await db.SaveChangesAsync(cancellationToken);
    return user;
}

static UsagePlan GetUsagePlan(AppUser user)
{
    return user.SubscriptionStatus is SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.Testing
        ? new UsagePlan($"paid:{user.StripeSubscriptionId ?? user.Id.ToString()}:{user.CurrentPeriodEnd?.ToString("O") ?? "no-period"}", user.SubscriptionStatus == SubscriptionStatus.Testing ? 10_000 : 100)
        : new UsagePlan("free:lifetime", 3);
}

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

static string? ResolveExternalUserId(
    HttpRequest request,
    IWebHostEnvironment environment,
    IConfiguration configuration)
{
    var headerUserId = request.Headers["X-External-User-Id"].ToString();
    var allowHeaderAuth = environment.IsDevelopment() ||
        environment.IsEnvironment("Testing") ||
        string.Equals(configuration["ALLOW_HEADER_AUTH"], "true", StringComparison.OrdinalIgnoreCase);

    if (allowHeaderAuth && !string.IsNullOrWhiteSpace(headerUserId))
    {
        return headerUserId;
    }

    if (request.HttpContext.User.Identity?.IsAuthenticated == true)
    {
        return request.HttpContext.User.FindFirstValue("sub") ??
            request.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    return null;
}

static string? ResolveEmail(HttpRequest request) =>
    request.HttpContext.User.FindFirstValue(ClaimTypes.Email) ??
    request.HttpContext.User.FindFirstValue("email");

static string? ResolveClerkIssuer(IConfiguration configuration)
{
    var explicitIssuer = configuration["CLERK_JWT_ISSUER"] ?? configuration["CLERK_ISSUER"];
    if (!string.IsNullOrWhiteSpace(explicitIssuer))
    {
        return explicitIssuer.TrimEnd('/');
    }

    var publishableKey = configuration["NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY"];
    if (string.IsNullOrWhiteSpace(publishableKey))
    {
        return null;
    }

    var encoded = publishableKey.Split('_').LastOrDefault();
    if (string.IsNullOrWhiteSpace(encoded))
    {
        return null;
    }

    try
    {
        var base64 = encoded.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2:
                base64 += "==";
                break;
            case 3:
                base64 += "=";
                break;
        }

        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64)).TrimEnd('$');
        if (string.IsNullOrWhiteSpace(decoded))
        {
            return null;
        }

        return decoded.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? decoded.TrimEnd('/')
            : $"https://{decoded.TrimEnd('/')}";
    }
    catch (FormatException)
    {
        return null;
    }
}

public sealed record RewriteAttemptResponse(
    Guid AttemptId,
    string Status,
    string? ResultJson,
    string? ErrorCode);

public sealed record UsagePlan(string PeriodKey, int QuotaLimit);

public sealed record BillingUrlResponse(string Url);

public partial class Program;
