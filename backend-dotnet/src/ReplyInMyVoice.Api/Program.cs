using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Api;
using ReplyInMyVoice.Infrastructure;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;
using Stripe;
using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

const string V1RewriteEndpointName = "v1/rewrite";
const string V1RewriteResultEndpointName = "v1/rewrite/{id}";
const int V1MinimumDraftLength = 10;
const int V1MaximumDraftWords = 300;
const int V1MaximumDraftCharacters = 2400;

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

app.MapGet("/api/me/billing/history", async (
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

    var history = await accountService.GetBillingHistoryAsync(
        externalUserId,
        ResolveRequestEmail(httpRequest, app.Environment, builder.Configuration),
        cancellationToken);

    return Results.Ok(history);
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
    var plan = ReplyInMyVoice.Infrastructure.Services.AccountService.GetUsagePlan(user, builder.Configuration);

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

app.MapPost("/api/v1/rewrite", async (
    HttpRequest httpRequest,
    AppDbContext db,
    RewriteRequestService rewriteRequestService,
    CancellationToken cancellationToken) =>
{
    var stopwatch = Stopwatch.StartNew();
    var now = DateTimeOffset.UtcNow;
    var bearerToken = ResolveBearerToken(httpRequest);
    var auth = await ResolveApiKeyAuthAsync(db, bearerToken, now, cancellationToken);
    if (auth.UserId is null)
    {
        return V1Error("invalid_key", "A valid API key is required.", StatusCodes.Status401Unauthorized);
    }

    if (auth.ApiKeyId is not null &&
        await IsV1RateLimitedAsync(
            db,
            auth.ApiKeyId.Value,
            auth.RateLimitPerMinute,
            now,
            cancellationToken))
    {
        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            V1Error("rate_limited", "Request limit reached. Please retry later.", StatusCodes.Status429TooManyRequests),
            StatusCodes.Status429TooManyRequests,
            stopwatch,
            now,
            cancellationToken);
    }

    V1RewriteSubmitRequest? body;
    try
    {
        body = await ReadV1RewriteSubmitRequestAsync(httpRequest, cancellationToken);
    }
    catch (JsonException)
    {
        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            V1Error("invalid_request", "Request body must be valid JSON.", StatusCodes.Status400BadRequest),
            StatusCodes.Status400BadRequest,
            stopwatch,
            now,
            cancellationToken);
    }

    var draft = body?.Draft?.Trim();
    if (string.IsNullOrWhiteSpace(draft) || draft.Length < V1MinimumDraftLength)
    {
        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            V1Error("invalid_request", "A draft of at least 10 characters is required.", StatusCodes.Status400BadRequest),
            StatusCodes.Status400BadRequest,
            stopwatch,
            now,
            cancellationToken);
    }

    if (draft.Length > V1MaximumDraftCharacters || CountWords(draft) > V1MaximumDraftWords)
    {
        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            V1Error("input_too_long", "Draft must be 300 words or fewer and no more than 2400 characters.", StatusCodes.Status400BadRequest),
            StatusCodes.Status400BadRequest,
            stopwatch,
            now,
            cancellationToken);
    }

    var user = await db.AppUsers
        .AsNoTracking()
        .SingleOrDefaultAsync(x => x.Id == auth.UserId.Value, cancellationToken);
    if (user is null)
    {
        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            V1Error("invalid_key", "A valid API key is required.", StatusCodes.Status401Unauthorized),
            StatusCodes.Status401Unauthorized,
            stopwatch,
            now,
            cancellationToken);
    }

    var idempotencyKey = httpRequest.Headers["Idempotency-Key"].ToString();
    if (string.IsNullOrWhiteSpace(idempotencyKey))
    {
        idempotencyKey = Guid.NewGuid().ToString("D");
    }

    var plan = ReplyInMyVoice.Infrastructure.Services.AccountService.GetUsagePlan(user, builder.Configuration);
    var result = await rewriteRequestService.CreateAttemptAsync(
        user.Id,
        idempotencyKey,
        new RewriteRequest(null, draft, null, null, null, null, "warm"),
        plan.PeriodKey,
        plan.QuotaLimit,
        now,
        cancellationToken);

    if (result.Kind == ReserveRewriteResultKind.QuotaExceeded)
    {
        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            V1Error("quota_exhausted", "No rewrite quota remains for the current period.", StatusCodes.Status402PaymentRequired),
            StatusCodes.Status402PaymentRequired,
            stopwatch,
            now,
            cancellationToken);
    }

    if (result.Kind == ReserveRewriteResultKind.Conflict)
    {
        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            V1Error("idempotency_conflict", "The idempotency key was reused with a different draft.", StatusCodes.Status409Conflict),
            StatusCodes.Status409Conflict,
            stopwatch,
            now,
            cancellationToken,
            result.AttemptId.ToString());
    }

    return await CompleteV1Async(
        db,
        auth.ApiKeyId,
        Results.Accepted(
            $"/api/v1/rewrite/{result.AttemptId}",
            new V1RewriteSubmitResponse(result.AttemptId, "processing")),
        StatusCodes.Status202Accepted,
        stopwatch,
        now,
        cancellationToken,
        result.AttemptId.ToString());
});

app.MapGet("/api/v1/rewrite/{id:guid}", async (
    Guid id,
    HttpRequest httpRequest,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var stopwatch = Stopwatch.StartNew();
    var now = DateTimeOffset.UtcNow;
    var bearerToken = ResolveBearerToken(httpRequest);
    var auth = await ResolveApiKeyAuthAsync(db, bearerToken, now, cancellationToken);
    if (auth.UserId is null)
    {
        return V1Error("invalid_key", "A valid API key is required.", StatusCodes.Status401Unauthorized);
    }

    if (auth.ApiKeyId is not null &&
        await IsV1RateLimitedAsync(
            db,
            auth.ApiKeyId.Value,
            auth.RateLimitPerMinute,
            now,
            cancellationToken))
    {
        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            V1Error("rate_limited", "Request limit reached. Please retry later.", StatusCodes.Status429TooManyRequests),
            StatusCodes.Status429TooManyRequests,
            stopwatch,
            now,
            cancellationToken,
            id.ToString(),
            V1RewriteResultEndpointName);
    }

    var attempt = await db.RewriteAttempts
        .AsNoTracking()
        .SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == auth.UserId.Value,
            cancellationToken);

    if (attempt is null)
    {
        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            V1Error("not_found", "Rewrite result was not found.", StatusCodes.Status404NotFound),
            StatusCodes.Status404NotFound,
            stopwatch,
            now,
            cancellationToken,
            id.ToString(),
            V1RewriteResultEndpointName);
    }

    return await CompleteV1Async(
        db,
        auth.ApiKeyId,
        MapV1RewriteResult(attempt),
        StatusCodes.Status200OK,
        stopwatch,
        now,
        cancellationToken,
        id.ToString(),
        V1RewriteResultEndpointName);
});

app.MapGet("/api/v1/usage", async (
    HttpRequest httpRequest,
    AppDbContext db,
    ReplyInMyVoice.Infrastructure.Services.AccountService accountService,
    CancellationToken cancellationToken) =>
{
    var now = DateTimeOffset.UtcNow;
    var bearerToken = ResolveBearerToken(httpRequest);
    var auth = await ResolveApiKeyAuthAsync(db, bearerToken, now, cancellationToken);
    if (auth.UserId is null)
    {
        return V1Error("invalid_key", "A valid API key is required.", StatusCodes.Status401Unauthorized);
    }

    var user = await db.AppUsers
        .AsNoTracking()
        .SingleOrDefaultAsync(x => x.Id == auth.UserId.Value, cancellationToken);
    if (user is null)
    {
        return V1Error("invalid_key", "A valid API key is required.", StatusCodes.Status401Unauthorized);
    }

    var account = await accountService.GetOrCreateAccountSummaryAsync(
        user.ExternalAuthUserId,
        user.Email,
        cancellationToken);

    return Results.Ok(new V1UsageResponse(
        account.Usage.Scope,
        account.Usage.PeriodKey,
        account.Usage.Quota,
        account.Usage.Used,
        account.Usage.Remaining,
        user.CurrentPeriodEnd));
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

app.MapPost("/api/promo/redeem", async (
    HttpRequest httpRequest,
    PromoService promoService,
    ReplyInMyVoice.Infrastructure.Services.AccountService accountService,
    CancellationToken cancellationToken) =>
{
    var externalUserId = ResolveExternalUserId(httpRequest, app.Environment, builder.Configuration);
    if (string.IsNullOrWhiteSpace(externalUserId))
    {
        return PromoError("authentication_required", StatusCodes.Status401Unauthorized);
    }

    PromoRedeemRequest? request;
    try
    {
        request = await ReadPromoRedeemRequestAsync(httpRequest, cancellationToken);
    }
    catch (JsonException)
    {
        return PromoError("invalid_request", StatusCodes.Status400BadRequest);
    }

    if (request is null || string.IsNullOrWhiteSpace(request.Code))
    {
        return PromoError("invalid_request", StatusCodes.Status400BadRequest);
    }

    if (string.IsNullOrWhiteSpace(request.TurnstileToken))
    {
        return PromoError("invalid_captcha", StatusCodes.Status403Forbidden);
    }

    try
    {
        var email = ResolveRequestEmail(httpRequest, app.Environment, builder.Configuration);
        var result = await promoService.RedeemAsync(
            externalUserId,
            email,
            request.Code,
            ResolveTrustedPromoClientIp(httpRequest, builder.Configuration),
            DateTimeOffset.UtcNow,
            cancellationToken);

        return await MapPromoRedeemResultAsync(
            result,
            externalUserId,
            email,
            accountService,
            cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        throw;
    }
    catch
    {
        return PromoError("server_error", StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/api/promo/status", async (
    HttpRequest httpRequest,
    PromoService promoService,
    CancellationToken cancellationToken) =>
{
    var externalUserId = ResolveExternalUserId(httpRequest, app.Environment, builder.Configuration);
    if (string.IsNullOrWhiteSpace(externalUserId))
    {
        return PromoError("authentication_required", StatusCodes.Status401Unauthorized);
    }

    try
    {
        var status = await promoService.GetStatusAsync(
            externalUserId,
            ResolveRequestEmail(httpRequest, app.Environment, builder.Configuration),
            DateTimeOffset.UtcNow,
            cancellationToken);

        return Results.Ok(new PromoStatusResponse(
            status.HasRedeemed,
            status.Eligible,
            status.TrialRemaining,
            status.TrialExpiresAt));
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        throw;
    }
    catch
    {
        return PromoError("server_error", StatusCodes.Status500InternalServerError);
    }
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

static async Task<PromoRedeemRequest?> ReadPromoRedeemRequestAsync(
    HttpRequest request,
    CancellationToken cancellationToken)
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync(cancellationToken);
    if (string.IsNullOrWhiteSpace(body))
    {
        return null;
    }

    return JsonSerializer.Deserialize<PromoRedeemRequest>(
        body,
        new JsonSerializerOptions(JsonSerializerDefaults.Web));
}

static async Task<ApiKeyAuthResult> ResolveApiKeyAuthAsync(
    AppDbContext db,
    string? bearerToken,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(bearerToken) ||
        !bearerToken.StartsWith("rmv_live_", StringComparison.Ordinal))
    {
        return new ApiKeyAuthResult(null, null, 0);
    }

    var keyHash = ApiKeyService.ComputeHash(bearerToken);
    var apiKey = await db.ApiKeys
        .SingleOrDefaultAsync(x => x.KeyHash == keyHash, cancellationToken);

    if (apiKey is null ||
        apiKey.RevokedAt is not null ||
        (apiKey.ExpiresAt is not null && apiKey.ExpiresAt <= now))
    {
        return new ApiKeyAuthResult(null, null, 0);
    }

    apiKey.LastUsedAt = now;
    try
    {
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException)
    {
        db.Entry(apiKey).State = EntityState.Unchanged;
    }

    return new ApiKeyAuthResult(apiKey.UserId, apiKey.Id, apiKey.RateLimitPerMinute);
}

static async Task<bool> IsV1RateLimitedAsync(
    AppDbContext db,
    Guid apiKeyId,
    int rateLimitPerMinute,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    if (rateLimitPerMinute <= 0)
    {
        return true;
    }

    var windowStart = now.AddMinutes(-1);
    var usageQuery = db.ApiKeyUsages
        .AsNoTracking()
        .Where(x => x.ApiKeyId == apiKeyId);

    if (string.Equals(
            db.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.Sqlite",
            StringComparison.OrdinalIgnoreCase))
    {
        var createdAtValues = await usageQuery
            .Select(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return createdAtValues.Count(x => x >= windowStart) >= rateLimitPerMinute;
    }

    var recentCallCount = await usageQuery.CountAsync(
        x => x.CreatedAt >= windowStart,
        cancellationToken);

    return recentCallCount >= rateLimitPerMinute;
}

static async Task<V1RewriteSubmitRequest?> ReadV1RewriteSubmitRequestAsync(
    HttpRequest request,
    CancellationToken cancellationToken)
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync(cancellationToken);
    if (string.IsNullOrWhiteSpace(body))
    {
        return null;
    }

    return JsonSerializer.Deserialize<V1RewriteSubmitRequest>(
        body,
        new JsonSerializerOptions(JsonSerializerDefaults.Web));
}

static async Task<IResult> CompleteV1Async(
    AppDbContext db,
    Guid? apiKeyId,
    IResult result,
    int statusCode,
    Stopwatch stopwatch,
    DateTimeOffset now,
    CancellationToken cancellationToken,
    string? requestId = null,
    string endpoint = V1RewriteEndpointName)
{
    stopwatch.Stop();
    await TryWriteV1ApiKeyUsageAsync(
        db,
        apiKeyId,
        requestId ?? Guid.NewGuid().ToString("D"),
        endpoint,
        statusCode,
        (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue),
        now,
        cancellationToken);
    return result;
}

static async Task TryWriteV1ApiKeyUsageAsync(
    AppDbContext db,
    Guid? apiKeyId,
    string requestId,
    string endpoint,
    int statusCode,
    int latencyMs,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    if (apiKeyId is null)
    {
        return;
    }

    try
    {
        db.ApiKeyUsages.Add(new ApiKeyUsage
        {
            ApiKeyId = apiKeyId.Value,
            RequestId = requestId,
            Endpoint = endpoint,
            StatusCode = statusCode,
            LatencyMs = latencyMs,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (Exception)
    {
    }
}

static IResult V1Error(string code, string message, int statusCode) =>
    Results.Json(new V1ErrorResponse(new V1Error(code, message)), statusCode: statusCode);

static IResult MapV1RewriteResult(RewriteAttempt attempt)
{
    if (attempt.Status is RewriteAttemptStatus.Pending or RewriteAttemptStatus.Processing)
    {
        return Results.Ok(new V1RewriteProcessingResponse(attempt.Id, "processing"));
    }

    if (attempt.Status == RewriteAttemptStatus.Succeeded &&
        TryReadV1SucceededResult(attempt.ResultJson, out var rewrittenText, out var draftSignal, out var rewriteSignal))
    {
        return Results.Ok(new V1RewriteSucceededResponse(
            attempt.Id,
            "succeeded",
            rewrittenText,
            new V1RewriteSignal(draftSignal, rewriteSignal)));
    }

    var code = string.IsNullOrWhiteSpace(attempt.ErrorCode)
        ? "engine_unavailable"
        : attempt.ErrorCode;
    return Results.Ok(new V1RewriteFailedResponse(
        attempt.Id,
        "failed",
        new V1Error(code, V1FailureMessage(attempt))));
}

static bool TryReadV1SucceededResult(
    string? resultJson,
    out string rewrittenText,
    out decimal draftSignal,
    out decimal rewriteSignal)
{
    rewrittenText = string.Empty;
    draftSignal = 0;
    rewriteSignal = 0;

    if (string.IsNullOrWhiteSpace(resultJson))
    {
        return false;
    }

    try
    {
        using var document = JsonDocument.Parse(resultJson);
        var root = document.RootElement;
        if (!root.TryGetProperty("rewrittenText", out var rewrittenTextElement) ||
            rewrittenTextElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(rewrittenTextElement.GetString()))
        {
            return false;
        }

        if (!root.TryGetProperty("naturalness", out var naturalness) ||
            naturalness.ValueKind != JsonValueKind.Object ||
            !TryGetDecimal(naturalness, "draftAiLikePercent", out draftSignal) ||
            !TryGetDecimal(naturalness, "rewriteAiLikePercent", out rewriteSignal))
        {
            return false;
        }

        rewrittenText = rewrittenTextElement.GetString()!;
        return true;
    }
    catch (JsonException)
    {
        return false;
    }
}

static bool TryGetDecimal(JsonElement parent, string propertyName, out decimal value)
{
    value = 0;
    return parent.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.Number &&
        property.TryGetDecimal(out value);
}

static string V1FailureMessage(RewriteAttempt attempt) =>
    attempt.Status == RewriteAttemptStatus.Expired
        ? "The rewrite did not finish in time. Please submit a new request."
        : "The rewrite could not be completed. Please try again.";

static string? ResolveBearerToken(HttpRequest request)
{
    var authorization = request.Headers.Authorization.ToString();
    return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? authorization["Bearer ".Length..].Trim()
        : null;
}

static int CountWords(string value)
{
    var count = 0;
    var inWord = false;

    foreach (var character in value)
    {
        if (char.IsWhiteSpace(character))
        {
            inWord = false;
            continue;
        }

        if (inWord)
        {
            continue;
        }

        count += 1;
        inWord = true;
    }

    return count;
}

static async Task<IResult> MapPromoRedeemResultAsync(
    PromoRedeemResult result,
    string externalUserId,
    string? email,
    ReplyInMyVoice.Infrastructure.Services.AccountService accountService,
    CancellationToken cancellationToken)
{
    switch (result.Kind)
    {
        case PromoRedeemResultKind.Success:
            var account = await accountService.GetOrCreateAccountSummaryAsync(
                externalUserId,
                email,
                cancellationToken);
            return Results.Ok(new PromoRedeemResponse(
                result.CreditsGranted,
                account.Usage.Remaining,
                result.ExpiresAt,
                AlreadyRedeemed: false));

        case PromoRedeemResultKind.InvalidCode:
            return PromoError("invalid_code", StatusCodes.Status422UnprocessableEntity);

        case PromoRedeemResultKind.Expired:
            return PromoError("code_expired", StatusCodes.Status422UnprocessableEntity);

        case PromoRedeemResultKind.AlreadyRedeemed:
            return PromoError("already_redeemed", StatusCodes.Status409Conflict);

        case PromoRedeemResultKind.CapReached:
            return PromoError("code_exhausted", StatusCodes.Status409Conflict);

        case PromoRedeemResultKind.IpVelocityBlocked:
            return PromoError("ip_velocity", StatusCodes.Status429TooManyRequests);

        case PromoRedeemResultKind.ServerConfig:
            return PromoError("server_config", StatusCodes.Status500InternalServerError);

        default:
            return PromoError("server_error", StatusCodes.Status500InternalServerError);
    }
}

static string? ResolveTrustedPromoClientIp(HttpRequest request, IConfiguration configuration)
{
    const string clientIpHeader = "X-Client-IP";
    const string proxySecretHeader = "X-RIMV-Proxy-Secret";

    var candidateIp = request.Headers[clientIpHeader].ToString();
    if (string.IsNullOrWhiteSpace(candidateIp))
    {
        return null;
    }

    var expectedSecret = configuration["PROMO_PROXY_SHARED_SECRET"];
    var suppliedSecret = request.Headers[proxySecretHeader].ToString();
    if (string.IsNullOrWhiteSpace(expectedSecret) ||
        string.IsNullOrWhiteSpace(suppliedSecret) ||
        !SecretsMatch(expectedSecret, suppliedSecret))
    {
        return null;
    }

    return candidateIp;
}

static bool SecretsMatch(string expectedSecret, string suppliedSecret)
{
    var expectedBytes = Encoding.UTF8.GetBytes(expectedSecret);
    var suppliedBytes = Encoding.UTF8.GetBytes(suppliedSecret);
    return expectedBytes.Length == suppliedBytes.Length &&
        CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
}

static IResult PromoError(string error, int statusCode) =>
    Results.Json(new PromoErrorResponse(error), statusCode: statusCode);

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

    return AuthEmailResolver.ResolveEmailFromClaims(request.HttpContext.User);
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

public sealed record PromoRedeemRequest(string? Code, string? TurnstileToken);

public sealed record PromoRedeemResponse(
    int CreditsGranted,
    int TotalRemaining,
    DateTimeOffset? ExpiresAt,
    bool AlreadyRedeemed);

public sealed record PromoStatusResponse(
    bool HasRedeemed,
    bool Eligible,
    int TrialRemaining,
    DateTimeOffset? TrialExpiresAt);

public sealed record PromoErrorResponse(string Error);

public sealed record V1RewriteSubmitRequest(string? Draft);

public sealed record V1RewriteSubmitResponse(Guid Id, string Status);

public sealed record V1RewriteProcessingResponse(Guid Id, string Status);

public sealed record V1RewriteSucceededResponse(
    Guid Id,
    string Status,
    string RewrittenText,
    V1RewriteSignal Signal);

public sealed record V1RewriteSignal(decimal Draft, decimal Rewrite);

public sealed record V1RewriteFailedResponse(Guid Id, string Status, V1Error Error);

public sealed record V1UsageResponse(
    string Scope,
    string PeriodKey,
    int Quota,
    int Used,
    int Remaining,
    DateTimeOffset? PeriodEnd);

public sealed record V1ErrorResponse(V1Error Error);

public sealed record V1Error(string Code, string Message);

public sealed record ApiKeyAuthResult(Guid? UserId, Guid? ApiKeyId, int RateLimitPerMinute);

public partial class Program;
