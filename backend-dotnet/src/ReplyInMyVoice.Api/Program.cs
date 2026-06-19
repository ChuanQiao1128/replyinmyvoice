using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Account;
using ReplyInMyVoice.Application.UseCases.Billing;
using ReplyInMyVoice.Application.UseCases.Promo;
using ReplyInMyVoice.Application.UseCases.Rewrite;
using ReplyInMyVoice.Application.UseCases.StripeEvent;
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
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApplicationResultKind = ReplyInMyVoice.Application.Common.ApplicationResultKind;
using ApplicationPromoRedeemResultKind = ReplyInMyVoice.Application.Common.PromoRedeemResultKind;
using PromoRedeemResultDto = ReplyInMyVoice.Application.Common.PromoRedeemResultDto;
using RewriteAttemptDto = ReplyInMyVoice.Application.Common.RewriteAttemptDto;

const string V1RewriteEndpointName = "v1/rewrite";
const string V1RewriteResultEndpointName = "v1/rewrite/{id}";
const string V1UsageEndpointName = "v1/usage";
const int V1MinimumDraftLength = 10;
const int V1MaximumDraftWords = 300;
const int V1MaximumDraftCharacters = 2400;
const int V1MaximumIdempotencyKeyLength = 120;
const string V1SandboxAttemptPrefix = SandboxAttemptConventions.IdempotencyKeyPrefix;
const string V1SandboxUsagePeriodKey = "test:sandbox";
const string V1SandboxResultJson = """
    {
      "rewrittenText": "Sandbox example: thanks for the update. I will review the details and follow up shortly.",
      "changeSummary": [],
      "riskNotes": [],
      "naturalness": {
        "draftAiLikePercent": 76,
        "rewriteAiLikePercent": 18
      }
    }
    """;

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
    GetAccountSummaryHandler getAccountSummaryHandler,
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

    var account = await getAccountSummaryHandler.HandleAsync(
        new GetAccountSummaryQuery(
            externalUserId,
            ResolveRequestEmail(httpRequest, app.Environment, builder.Configuration)),
        cancellationToken);

    return Results.Ok(account);
});

app.MapGet("/api/me/payments", async (
    HttpRequest httpRequest,
    GetPurchaseHistoryHandler getPurchaseHistoryHandler,
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

    var payments = await getPurchaseHistoryHandler.HandleAsync(
        new GetPurchaseHistoryQuery(
            externalUserId,
            ResolveRequestEmail(httpRequest, app.Environment, builder.Configuration)),
        cancellationToken);

    return Results.Ok(payments);
});

app.MapGet("/api/me/billing/history", async (
    HttpRequest httpRequest,
    GetBillingHistoryHandler getBillingHistoryHandler,
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

    var history = await getBillingHistoryHandler.HandleAsync(
        new GetBillingHistoryQuery(
            externalUserId,
            ResolveRequestEmail(httpRequest, app.Environment, builder.Configuration)),
        cancellationToken);

    return Results.Ok(history);
});

app.MapPost("/api/rewrite", async (
    HttpRequest httpRequest,
    [FromBody] RewriteRequest request,
    GetOrCreateUserHandler getOrCreateUserHandler,
    CreateRewriteAttemptHandler createRewriteAttemptHandler,
    IUserRewriteRateLimiter userRateLimiter,
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

    var user = await getOrCreateUserHandler.HandleAsync(
        new GetOrCreateUserCommand(
            externalUserId,
            ResolveRequestEmail(httpRequest, app.Environment, builder.Configuration)),
        cancellationToken);
    var now = DateTimeOffset.UtcNow;
    if (userRateLimiter.Enabled)
    {
        var rateLimit = await userRateLimiter.CheckAndIncrementAsync(
            user.Id,
            now,
            cancellationToken);
        if (rateLimit.IsUnavailable)
        {
            return V1Error(
                "rate_limit_unavailable",
                "Rewrite limit could not be checked. Please retry shortly.",
                StatusCodes.Status503ServiceUnavailable);
        }

        SetV1RateLimitHeaders(
            httpRequest.HttpContext.Response,
            rateLimit,
            rateLimit.IsLimited,
            now);
        if (rateLimit.IsLimited)
        {
            return V1Error(
                "rate_limited",
                "Rewrite rate limit reached. Please wait a moment and retry.",
                StatusCodes.Status429TooManyRequests);
        }
    }

    var plan = AccountUsagePlans.GetUsagePlan(user, builder.Configuration);

    var result = await createRewriteAttemptHandler.HandleAsync(
        new CreateRewriteAttemptCommand(
            user.Id,
            idempotencyKey,
            request,
            plan.PeriodKey,
            plan.QuotaLimit,
            now),
        cancellationToken);

    if (result.Kind == ApplicationResultKind.QuotaExceeded)
    {
        if (!string.IsNullOrWhiteSpace(result.ErrorCode))
        {
            return V1Error(
                result.ErrorCode,
                "No rewrite quota remains for the current period.",
                StatusCodes.Status402PaymentRequired);
        }

        return Results.Problem(
            title: "Rewrite quota exhausted",
            detail: "No rewrite quota remains for the current period.",
            statusCode: StatusCodes.Status402PaymentRequired);
    }

    if (result.Kind == ApplicationResultKind.Conflict)
    {
        return Results.Problem(
            title: "Idempotency key conflict",
            detail: "The same idempotency key was reused with a different rewrite request.",
            statusCode: StatusCodes.Status409Conflict);
    }

    return result.Value is not null
        ? ToRewriteAttemptHttpResult(result.Value)
        : Results.Problem(
            title: "Rewrite request failed",
            detail: "The rewrite request could not be processed.",
            statusCode: StatusCodes.Status500InternalServerError);
});

app.MapPost("/api/v1/rewrite", async (
    HttpRequest httpRequest,
    AppDbContext db,
    IApiKeyRateLimiter rateLimiter,
    HasPaidApiEntitlementHandler hasPaidApiEntitlementHandler,
    CreateRewriteAttemptHandler createRewriteAttemptHandler,
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

    var rateLimit = auth.ApiKeyId is null
        ? null
        : await rateLimiter.CheckAndIncrementAsync(
            auth.ApiKeyId.Value,
            auth.RateLimitPerMinute,
            now,
            cancellationToken);
    if (rateLimit?.IsUnavailable == true)
    {
        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            V1Error("rate_limit_unavailable", "Request limit could not be checked. Please retry later.", StatusCodes.Status503ServiceUnavailable),
            StatusCodes.Status503ServiceUnavailable,
            stopwatch,
            now,
            cancellationToken,
            response: httpRequest.HttpContext.Response);
    }

    if (rateLimit?.IsLimited == true)
    {
        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            V1Error("rate_limited", "Request limit reached. Please retry later.", StatusCodes.Status429TooManyRequests),
            StatusCodes.Status429TooManyRequests,
            stopwatch,
            now,
            cancellationToken,
            response: httpRequest.HttpContext.Response,
            rateLimit: rateLimit);
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
            cancellationToken,
            response: httpRequest.HttpContext.Response,
            rateLimit: rateLimit);
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
            cancellationToken,
            response: httpRequest.HttpContext.Response,
            rateLimit: rateLimit);
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
            cancellationToken,
            response: httpRequest.HttpContext.Response,
            rateLimit: rateLimit);
    }

    // TODO(DDD): still uses inline db — DDD-67/68
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
            cancellationToken,
            response: httpRequest.HttpContext.Response,
            rateLimit: rateLimit);
    }

    var idempotencyKey = httpRequest.Headers["Idempotency-Key"].ToString();
    if (string.IsNullOrWhiteSpace(idempotencyKey))
    {
        idempotencyKey = Guid.NewGuid().ToString("D");
    }
    else if (idempotencyKey.Length > V1MaximumIdempotencyKeyLength)
    {
        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            V1Error("invalid_request", "Idempotency-Key must be 120 characters or fewer.", StatusCodes.Status400BadRequest),
            StatusCodes.Status400BadRequest,
            stopwatch,
            now,
            cancellationToken,
            response: httpRequest.HttpContext.Response,
            rateLimit: rateLimit);
    }

    if (auth.IsTest)
    {
        var sandboxResult = await CreateV1SandboxAttemptAsync(
            db,
            user.Id,
            auth.ApiKeyId!.Value,
            idempotencyKey,
            draft,
            now,
            cancellationToken);

        if (sandboxResult.IsConflict)
        {
            return await CompleteV1Async(
                db,
                auth.ApiKeyId,
                V1Error("idempotency_conflict", "The idempotency key was reused with a different draft.", StatusCodes.Status409Conflict),
                StatusCodes.Status409Conflict,
                stopwatch,
                now,
                cancellationToken,
                sandboxResult.AttemptId.ToString(),
                response: httpRequest.HttpContext.Response,
                rateLimit: rateLimit);
        }

        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            Results.Accepted(
                $"/api/v1/rewrite/{sandboxResult.AttemptId}",
                new V1RewriteSubmitResponse(sandboxResult.AttemptId, "processing")),
            StatusCodes.Status202Accepted,
            stopwatch,
            now,
            cancellationToken,
            sandboxResult.AttemptId.ToString(),
            response: httpRequest.HttpContext.Response,
            rateLimit: rateLimit);
    }

    if (!await hasPaidApiEntitlementHandler.HandleAsync(
            new HasPaidApiEntitlementQuery(user.Id, now),
            cancellationToken))
    {
        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            V1Error(
                "api_requires_paid_plan",
                "Public API access requires an active paid plan or usable purchased rewrite credit.",
                StatusCodes.Status402PaymentRequired),
            StatusCodes.Status402PaymentRequired,
            stopwatch,
            now,
            cancellationToken,
            response: httpRequest.HttpContext.Response,
            rateLimit: rateLimit);
    }

    var plan = AccountUsagePlans.GetUsagePlan(user, builder.Configuration);
    var result = await createRewriteAttemptHandler.HandleAsync(
        new CreateRewriteAttemptCommand(
            user.Id,
            idempotencyKey,
            new RewriteRequest(null, draft, null, null, null, null, "warm"),
            plan.PeriodKey,
            plan.QuotaLimit,
            now,
            auth.ApiKeyId),
        cancellationToken);

    if (result.Kind == ApplicationResultKind.QuotaExceeded)
    {
        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            V1Error(
                result.ErrorCode ?? "quota_exhausted",
                "No rewrite quota remains for the current period.",
                StatusCodes.Status402PaymentRequired),
            StatusCodes.Status402PaymentRequired,
            stopwatch,
            now,
            cancellationToken,
            response: httpRequest.HttpContext.Response,
            rateLimit: rateLimit);
    }

    if (result.Kind == ApplicationResultKind.Conflict)
    {
        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            V1Error("idempotency_conflict", "The idempotency key was reused with a different draft.", StatusCodes.Status409Conflict),
            StatusCodes.Status409Conflict,
            stopwatch,
            now,
            cancellationToken,
            result.Value?.AttemptId.ToString(),
            response: httpRequest.HttpContext.Response,
            rateLimit: rateLimit);
    }

    if (result.Value is null)
    {
        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            V1Error("rewrite_failed", "The rewrite request could not be processed.", StatusCodes.Status500InternalServerError),
            StatusCodes.Status500InternalServerError,
            stopwatch,
            now,
            cancellationToken,
            response: httpRequest.HttpContext.Response,
            rateLimit: rateLimit);
    }

    return await CompleteV1Async(
        db,
        auth.ApiKeyId,
        Results.Accepted(
            $"/api/v1/rewrite/{result.Value.AttemptId}",
            new V1RewriteSubmitResponse(result.Value.AttemptId, "processing")),
        StatusCodes.Status202Accepted,
        stopwatch,
        now,
        cancellationToken,
        result.Value.AttemptId.ToString(),
        response: httpRequest.HttpContext.Response,
        rateLimit: rateLimit);
});

app.MapGet("/api/v1/rewrite/{id:guid}", async (
    Guid id,
    HttpRequest httpRequest,
    AppDbContext db,
    IApiKeyRateLimiter rateLimiter,
    GetRewriteAttemptHandler getRewriteAttemptHandler,
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

    var rateLimit = auth.ApiKeyId is null
        ? null
        : await rateLimiter.CheckAndIncrementAsync(
            auth.ApiKeyId.Value,
            auth.RateLimitPerMinute,
            now,
            cancellationToken);
    if (rateLimit?.IsUnavailable == true)
    {
        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            V1Error("rate_limit_unavailable", "Request limit could not be checked. Please retry later.", StatusCodes.Status503ServiceUnavailable),
            StatusCodes.Status503ServiceUnavailable,
            stopwatch,
            now,
            cancellationToken,
            id.ToString(),
            V1RewriteResultEndpointName,
            response: httpRequest.HttpContext.Response);
    }

    if (rateLimit?.IsLimited == true)
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
            V1RewriteResultEndpointName,
            response: httpRequest.HttpContext.Response,
            rateLimit: rateLimit);
    }

    var getAttemptResult = await getRewriteAttemptHandler.HandleAsync(
        new GetRewriteAttemptQuery(id, auth.UserId.Value),
        cancellationToken);
    var attempt = getAttemptResult.Value;

    if (getAttemptResult.Kind == ApplicationResultKind.NotFound ||
        attempt is null ||
        IsV1SandboxAttempt(attempt) != auth.IsTest)
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
            V1RewriteResultEndpointName,
            response: httpRequest.HttpContext.Response,
            rateLimit: rateLimit);
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
        V1RewriteResultEndpointName,
        response: httpRequest.HttpContext.Response,
        rateLimit: rateLimit);
});

app.MapGet("/api/v1/usage", async (
    HttpRequest httpRequest,
    AppDbContext db,
    IApiKeyRateLimiter rateLimiter,
    GetAccountSummaryHandler getAccountSummaryHandler,
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

    var rateLimit = auth.ApiKeyId is null
        ? null
        : await rateLimiter.CheckAndIncrementAsync(
            auth.ApiKeyId.Value,
            auth.RateLimitPerMinute,
            now,
            cancellationToken);
    if (rateLimit?.IsUnavailable == true)
    {
        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            V1Error("rate_limit_unavailable", "Request limit could not be checked. Please retry later.", StatusCodes.Status503ServiceUnavailable),
            StatusCodes.Status503ServiceUnavailable,
            stopwatch,
            now,
            cancellationToken,
            endpoint: V1UsageEndpointName,
            response: httpRequest.HttpContext.Response);
    }

    if (rateLimit?.IsLimited == true)
    {
        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            V1Error("rate_limited", "Request limit reached. Please retry later.", StatusCodes.Status429TooManyRequests),
            StatusCodes.Status429TooManyRequests,
            stopwatch,
            now,
            cancellationToken,
            endpoint: V1UsageEndpointName,
            response: httpRequest.HttpContext.Response,
            rateLimit: rateLimit);
    }

    if (auth.IsTest)
    {
        return await CompleteV1Async(
            db,
            auth.ApiKeyId,
            Results.Ok(new V1UsageResponse(
                "test",
                V1SandboxUsagePeriodKey,
                0,
                0,
                0,
                null)),
            StatusCodes.Status200OK,
            stopwatch,
            now,
            cancellationToken,
            endpoint: V1UsageEndpointName,
            response: httpRequest.HttpContext.Response,
            rateLimit: rateLimit);
    }

    // TODO(DDD): still uses inline db — DDD-67/68
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
            cancellationToken,
            endpoint: V1UsageEndpointName,
            response: httpRequest.HttpContext.Response,
            rateLimit: rateLimit);
    }

    var account = await getAccountSummaryHandler.HandleAsync(
        new GetAccountSummaryQuery(user.ExternalAuthUserId, user.Email),
        cancellationToken);

    return await CompleteV1Async(
        db,
        auth.ApiKeyId,
        Results.Ok(new V1UsageResponse(
            account.Usage.Scope,
            account.Usage.PeriodKey,
            account.Usage.Quota,
            account.Usage.Used,
            account.Usage.Remaining,
            user.CurrentPeriodEnd)),
        StatusCodes.Status200OK,
        stopwatch,
        now,
        cancellationToken,
        endpoint: V1UsageEndpointName,
        response: httpRequest.HttpContext.Response,
        rateLimit: rateLimit);
});

app.MapGet("/api/rewrite-attempts/{attemptId:guid}", async (
    Guid attemptId,
    HttpRequest httpRequest,
    FindUserHandler findUserHandler,
    GetRewriteAttemptHandler getRewriteAttemptHandler,
    CancellationToken cancellationToken) =>
{
    var externalUserId = ResolveExternalUserId(httpRequest, app.Environment, builder.Configuration);
    if (string.IsNullOrWhiteSpace(externalUserId))
    {
        return Results.Problem(
            title: "Authentication required",
            statusCode: StatusCodes.Status401Unauthorized);
    }

    var user = await findUserHandler.HandleAsync(
        new FindUserQuery(externalUserId),
        cancellationToken);

    if (user is null)
    {
        return Results.NotFound();
    }

    var result = await getRewriteAttemptHandler.HandleAsync(
        new GetRewriteAttemptQuery(attemptId, user.Id),
        cancellationToken);
    var attempt = result.Value;

    if (result.Kind == ApplicationResultKind.NotFound || attempt is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(ToRewriteAttemptResponse(attempt));
});

app.MapPost("/api/promo/redeem", async (
    HttpRequest httpRequest,
    RedeemPromoHandler redeemPromoHandler,
    GetAccountSummaryHandler getAccountSummaryHandler,
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
        var ipDefense = ResolvePromoIpDefense(httpRequest, app.Environment, builder.Configuration);
        var result = ipDefense.ServerConfigError
            ? PromoRedeemResultDto.ServerConfig()
            : await redeemPromoHandler.HandleAsync(
                new RedeemPromoCommand(
                    externalUserId,
                    email,
                    request.Code,
                    ipDefense.IpHash,
                    DateTimeOffset.UtcNow),
                cancellationToken);

        return await MapPromoRedeemResultAsync(
            result,
            externalUserId,
            email,
            getAccountSummaryHandler,
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
    GetPromoStatusHandler getPromoStatusHandler,
    CancellationToken cancellationToken) =>
{
    var externalUserId = ResolveExternalUserId(httpRequest, app.Environment, builder.Configuration);
    if (string.IsNullOrWhiteSpace(externalUserId))
    {
        return PromoError("authentication_required", StatusCodes.Status401Unauthorized);
    }

    try
    {
        var status = await getPromoStatusHandler.HandleAsync(
            new GetPromoStatusQuery(
                externalUserId,
                ResolveRequestEmail(httpRequest, app.Environment, builder.Configuration),
                DateTimeOffset.UtcNow),
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
    CreateCheckoutSessionHandler createCheckoutSessionHandler,
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

        var session = await createCheckoutSessionHandler.HandleAsync(
            new CreateCheckoutSessionCommand(
                externalUserId,
                ResolveRequestEmail(httpRequest, app.Environment, builder.Configuration),
                string.IsNullOrWhiteSpace(sku) ? null : sku),
            cancellationToken);
        return Results.Ok(new BillingUrlResponse(session.Url));
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
    CreatePortalSessionHandler createPortalSessionHandler,
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
        var session = await createPortalSessionHandler.HandleAsync(
            new CreatePortalSessionQuery(externalUserId),
            cancellationToken);
        return Results.Ok(new BillingUrlResponse(session.Url));
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
    IngestStripeWebhookHandler ingestStripeWebhookHandler,
    ProcessPendingStripeEventsHandler processPendingStripeEventsHandler,
    StripeEventProcessingOptions stripeEventProcessingOptions,
    ILoggerFactory loggerFactory,
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

    var now = DateTimeOffset.UtcNow;
    var ingestResult = await ingestStripeWebhookHandler.HandleAsync(
        new IngestStripeWebhookCommand(
            eventId,
            eventType,
            rawBody,
            now),
        cancellationToken);

    var processed = false;
    if (ingestResult != StripeWebhookIngestResult.AlreadyProcessed &&
        stripeEventProcessingOptions.InlineBudgetSeconds > 0)
    {
        using var inlineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        inlineCts.CancelAfter(TimeSpan.FromSeconds(stripeEventProcessingOptions.InlineBudgetSeconds));
        try
        {
            processed = await processPendingStripeEventsHandler.HandleAsync(
                new ProcessPendingStripeEventsCommand(
                    now,
                    BatchSize: 1,
                    eventId),
                inlineCts.Token) > 0;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            loggerFactory
                .CreateLogger("StripeWebhook")
                .LogWarning(
                    "{PaymentObservabilityEvent} Stripe webhook inline processing deferred for event {EventId} of type {EventType}.",
                    "webhook_inline_deferred",
                    eventId,
                    eventType);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            loggerFactory
                .CreateLogger("StripeWebhook")
                .LogWarning(
                    ex,
                    "{PaymentObservabilityEvent} Stripe webhook inline processing deferred for event {EventId} of type {EventType}.",
                    "webhook_inline_deferred",
                    eventId,
                    eventType);
        }
    }

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

static IResult ToRewriteAttemptHttpResult(RewriteAttemptDto attempt)
{
    var response = ToRewriteAttemptResponse(attempt);

    return attempt.Status == RewriteAttemptStatus.Succeeded.ToString()
        ? Results.Ok(response)
        : Results.Accepted($"/api/rewrite-attempts/{attempt.AttemptId}", response);
}

static RewriteAttemptResponse ToRewriteAttemptResponse(RewriteAttemptDto attempt) =>
    new(
        attempt.AttemptId,
        attempt.Status,
        attempt.ResultJson,
        attempt.ErrorCode);

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
    // TODO(DDD): still uses inline db — DDD-67/68
    if (string.IsNullOrWhiteSpace(bearerToken) ||
        !HasKnownApiKeyPrefix(bearerToken))
    {
        return new ApiKeyAuthResult(null, null, 0);
    }

    var keyHash = ApiKeyHashing.ComputeHash(bearerToken);
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

    return new ApiKeyAuthResult(apiKey.UserId, apiKey.Id, apiKey.RateLimitPerMinute, apiKey.IsTest);
}

static bool HasKnownApiKeyPrefix(string token) =>
    token.StartsWith("rmv_live_", StringComparison.Ordinal) ||
    token.StartsWith("rmv_test_", StringComparison.Ordinal);

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
    string endpoint = V1RewriteEndpointName,
    HttpResponse? response = null,
    ApiKeyRateLimitResult? rateLimit = null)
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
    if (response is not null && rateLimit is not null)
    {
        SetV1RateLimitHeaders(
            response,
            rateLimit,
            statusCode == StatusCodes.Status429TooManyRequests,
            now);
    }

    return result;
}

static void SetV1RateLimitHeaders(
    HttpResponse response,
    ApiKeyRateLimitResult rateLimit,
    bool includeRetryAfter,
    DateTimeOffset now)
{
    response.Headers["X-RateLimit-Limit"] = rateLimit.Limit.ToString(CultureInfo.InvariantCulture);
    response.Headers["X-RateLimit-Remaining"] = rateLimit.Remaining.ToString(CultureInfo.InvariantCulture);
    response.Headers["X-RateLimit-Reset"] = rateLimit.ResetAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

    if (includeRetryAfter)
    {
        response.Headers.RetryAfter = rateLimit.RetryAfterSeconds(now).ToString(CultureInfo.InvariantCulture);
    }
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
    // TODO(DDD): still uses inline db — DDD-67/68
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

static async Task<V1SandboxAttemptResult> CreateV1SandboxAttemptAsync(
    AppDbContext db,
    Guid userId,
    Guid apiKeyId,
    string idempotencyKey,
    string draft,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    // TODO(DDD): still uses inline db — DDD-67/68
    var sandboxIdempotencyKey = BuildV1SandboxIdempotencyKey(apiKeyId, idempotencyKey);
    var requestHash = ComputeV1Sha256(draft);
    var existingAttempt = await db.RewriteAttempts
        .IgnoreQueryFilters()
        .AsNoTracking()
        .SingleOrDefaultAsync(
            x => x.UserId == userId && x.IdempotencyKey == sandboxIdempotencyKey,
            cancellationToken);

    if (existingAttempt is not null)
    {
        return string.Equals(existingAttempt.RequestHash, requestHash, StringComparison.Ordinal)
            ? new V1SandboxAttemptResult(existingAttempt.Id, IsConflict: false)
            : new V1SandboxAttemptResult(existingAttempt.Id, IsConflict: true);
    }

    var rewriteRequest = new RewriteRequest(null, draft, null, null, null, null, "warm");
    var attempt = new RewriteAttempt
    {
        UserId = userId,
        IdempotencyKey = sandboxIdempotencyKey,
        RequestHash = requestHash,
        RequestJson = JsonSerializer.Serialize(rewriteRequest),
        ApiKeyId = apiKeyId,
        Status = RewriteAttemptStatus.Succeeded,
        ResultJson = V1SandboxResultJson,
        CreatedAt = now,
        CompletedAt = now,
        ExpiresAt = now.AddMinutes(15),
    };

    db.RewriteAttempts.Add(attempt);
    await db.SaveChangesAsync(cancellationToken);

    return new V1SandboxAttemptResult(attempt.Id, IsConflict: false);
}

static bool IsV1SandboxAttempt(RewriteAttemptDto attempt) =>
    attempt.Status == RewriteAttemptStatus.Succeeded.ToString() &&
    attempt.IdempotencyKey.StartsWith(V1SandboxAttemptPrefix, StringComparison.Ordinal) &&
    string.Equals(attempt.ResultJson, V1SandboxResultJson, StringComparison.Ordinal);

static string BuildV1SandboxIdempotencyKey(Guid apiKeyId, string idempotencyKey) =>
    string.Concat(V1SandboxAttemptPrefix, apiKeyId.ToString("N"), ":", ComputeV1Sha256(idempotencyKey));

static string ComputeV1Sha256(string value)
{
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
    return Convert.ToHexString(hash).ToLowerInvariant();
}

static IResult MapV1RewriteResult(RewriteAttemptDto attempt)
{
    if (attempt.Status == RewriteAttemptStatus.Pending.ToString() ||
        attempt.Status == RewriteAttemptStatus.Processing.ToString())
    {
        return Results.Ok(new V1RewriteProcessingResponse(attempt.AttemptId, "processing"));
    }

    if (attempt.Status == RewriteAttemptStatus.Succeeded.ToString() &&
        TryReadV1SucceededResult(attempt.ResultJson, out var rewrittenText, out var draftSignal, out var rewriteSignal))
    {
        return Results.Ok(new V1RewriteSucceededResponse(
            attempt.AttemptId,
            "succeeded",
            rewrittenText,
            new V1RewriteSignal(draftSignal, rewriteSignal)));
    }

    var code = string.IsNullOrWhiteSpace(attempt.ErrorCode)
        ? "engine_unavailable"
        : attempt.ErrorCode;
    return Results.Ok(new V1RewriteFailedResponse(
        attempt.AttemptId,
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

static string V1FailureMessage(RewriteAttemptDto attempt) =>
    attempt.Status == RewriteAttemptStatus.Expired.ToString()
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
    PromoRedeemResultDto result,
    string externalUserId,
    string? email,
    GetAccountSummaryHandler getAccountSummaryHandler,
    CancellationToken cancellationToken)
{
    switch (result.Kind)
    {
        case ApplicationPromoRedeemResultKind.Success:
            var account = await getAccountSummaryHandler.HandleAsync(
                new GetAccountSummaryQuery(externalUserId, email),
                cancellationToken);
            return Results.Ok(new PromoRedeemResponse(
                result.CreditsGranted,
                account.Usage.Remaining,
                result.ExpiresAt,
                AlreadyRedeemed: false));

        case ApplicationPromoRedeemResultKind.InvalidCode:
            return PromoError("invalid_code", StatusCodes.Status422UnprocessableEntity);

        case ApplicationPromoRedeemResultKind.Expired:
            return PromoError("code_expired", StatusCodes.Status422UnprocessableEntity);

        case ApplicationPromoRedeemResultKind.AlreadyRedeemed:
            return PromoError("already_redeemed", StatusCodes.Status409Conflict);

        case ApplicationPromoRedeemResultKind.CapReached:
            return PromoError("code_exhausted", StatusCodes.Status409Conflict);

        case ApplicationPromoRedeemResultKind.IpVelocityBlocked:
            return PromoError("ip_velocity", StatusCodes.Status429TooManyRequests);

        case ApplicationPromoRedeemResultKind.ServerConfig:
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

static (bool ServerConfigError, string? IpHash) ResolvePromoIpDefense(
    HttpRequest request,
    IWebHostEnvironment environment,
    IConfiguration configuration)
{
    var isProduction = IsProductionEnvironment(environment, configuration);
    if (isProduction && string.IsNullOrWhiteSpace(configuration["PROMO_PROXY_SHARED_SECRET"]))
    {
        return (true, null);
    }

    var normalizedIp = NormalizeTrustedPromoClientIp(
        ResolveTrustedPromoClientIp(request, configuration));
    if (normalizedIp is null)
    {
        return isProduction ? (true, null) : (false, null);
    }

    var salt = configuration["PROMO_IP_HASH_SALT"];
    if (string.IsNullOrWhiteSpace(salt))
    {
        return (true, null);
    }

    return (false, HashPromoIp(normalizedIp, salt));
}

static string? NormalizeTrustedPromoClientIp(string? trustedClientIp)
{
    var trimmed = trustedClientIp?.Trim();
    if (string.IsNullOrWhiteSpace(trimmed))
    {
        return null;
    }

    return IPAddress.TryParse(trimmed, out var parsedIp)
        ? parsedIp.ToString()
        : null;
}

static string HashPromoIp(string trustedClientIp, string salt)
{
    var payload = Encoding.UTF8.GetBytes($"{salt}:{trustedClientIp}");
    var hash = SHA256.HashData(payload);
    return Convert.ToHexString(hash).ToLowerInvariant();
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

public sealed record ApiKeyAuthResult(Guid? UserId, Guid? ApiKeyId, int RateLimitPerMinute, bool IsTest = false);

public sealed record V1SandboxAttemptResult(Guid AttemptId, bool IsConflict);

public partial class Program;
