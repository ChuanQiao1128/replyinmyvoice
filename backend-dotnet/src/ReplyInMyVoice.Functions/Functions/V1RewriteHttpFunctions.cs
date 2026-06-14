using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Account;
using ReplyInMyVoice.Application.UseCases.Rewrite;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Http;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class V1RewriteHttpFunctions(
    IConfiguration configuration,
    ApiKeyAuthResolver authResolver,
    IAppUserRepository appUsers,
    IRewriteAttemptRepository rewriteAttempts,
    IApiKeyUsageRepository apiKeyUsages,
    IUnitOfWork unitOfWork,
    IApiKeyRateLimiter rateLimiter,
    HasPaidApiEntitlementHandler hasPaidApiEntitlementHandler,
    CreateRewriteAttemptHandler createRewriteAttemptHandler,
    GetRewriteAttemptHandler getRewriteAttemptHandler,
    GetAccountSummaryHandler getAccountSummaryHandler)
{
    private const string EndpointName = "v1/rewrite";
    private const string ResultEndpointName = "v1/rewrite/{id}";
    private const string UsageEndpointName = "v1/usage";
    private const string SandboxAttemptPrefix = SandboxAttemptConventions.IdempotencyKeyPrefix;
    private const string SandboxUsagePeriodKey = "test:sandbox";
    private const string SandboxResultJson = """
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Function("V1SubmitRewrite")]
    public async Task<IActionResult> SubmitRewrite(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/rewrite")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var now = DateTimeOffset.UtcNow;
        var envelopeRequestId = ResolveRequestId(request);
        var auth = await authResolver.ResolveAsync(
            request,
            now,
            cancellationToken);

        if (auth.UserId is null)
        {
            return V1Problem(V1ErrorCatalog.InvalidKey, envelopeRequestId);
        }

        ApiKeyRateLimitResult? rateLimit = null;
        if (!ApiKeyScopes.Allows(auth.Scopes, ApiKeyScopes.Rewrite))
        {
            return await CompleteAsync(
                V1InsufficientScopeProblem(envelopeRequestId),
                StatusCodes.Status403Forbidden);
        }

        rateLimit = auth.ApiKeyId is null
            ? null
            : await rateLimiter.CheckAndIncrementAsync(
                auth.ApiKeyId.Value,
                auth.RateLimitPerMinute,
                now,
                cancellationToken);
        if (rateLimit?.IsUnavailable == true)
        {
            return await CompleteAsync(
                V1Problem(V1ErrorCatalog.RateLimitUnavailable, envelopeRequestId),
                V1ErrorCatalog.RateLimitUnavailable.StatusCode);
        }

        if (rateLimit?.IsLimited == true)
        {
            return await CompleteAsync(
                V1Problem(V1ErrorCatalog.RateLimited, envelopeRequestId),
                V1ErrorCatalog.RateLimited.StatusCode);
        }

        V1RewriteSubmitRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<V1RewriteSubmitRequest>(
                request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return await CompleteAsync(
                V1Problem(V1ErrorCatalog.InvalidJson, envelopeRequestId),
                V1ErrorCatalog.InvalidJson.StatusCode);
        }

        var draftValidation = V1RewriteValidation.ValidateDraft(body?.Draft);
        if (!draftValidation.IsValid)
        {
            var error = draftValidation.Error!;
            return await CompleteAsync(
                V1Problem(error, envelopeRequestId),
                error.StatusCode);
        }

        var draft = draftValidation.Value!;

        var user = await appUsers.GetByIdAsync(auth.UserId.Value, cancellationToken);
        if (user is null)
        {
            return await CompleteAsync(
                V1Problem(V1ErrorCatalog.InvalidKey, envelopeRequestId),
                V1ErrorCatalog.InvalidKey.StatusCode);
        }

        var idempotencyKey = request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            idempotencyKey = Guid.NewGuid().ToString("D");
        }
        else
        {
            var idempotencyKeyValidation = V1RewriteValidation.ValidateIdempotencyKey(idempotencyKey);
            if (!idempotencyKeyValidation.IsValid)
            {
                var error = idempotencyKeyValidation.Error!;
                return await CompleteAsync(
                    V1Problem(error, envelopeRequestId),
                    error.StatusCode);
            }

            idempotencyKey = idempotencyKeyValidation.Value!;
        }

        if (auth.IsTest)
        {
            var sandboxResult = await CreateSandboxAttemptAsync(
                user.Id,
                auth.ApiKeyId!.Value,
                idempotencyKey,
                draft,
                now,
                cancellationToken);

            if (sandboxResult.IsConflict)
            {
                return await CompleteAsync(
                    V1Problem(V1ErrorCatalog.IdempotencyConflict, envelopeRequestId),
                    V1ErrorCatalog.IdempotencyConflict.StatusCode,
                    sandboxResult.AttemptId.ToString());
            }

            return await CompleteAsync(
                FunctionHttpResults.Accepted(
                    $"/api/v1/rewrite/{sandboxResult.AttemptId}",
                    new
                    {
                        id = sandboxResult.AttemptId,
                        status = "processing",
                    }),
                StatusCodes.Status202Accepted,
                sandboxResult.AttemptId.ToString());
        }

        if (!await hasPaidApiEntitlementHandler.HandleAsync(
                new HasPaidApiEntitlementQuery(user.Id, now),
                cancellationToken))
        {
            return await CompleteAsync(
                V1Problem(V1ErrorCatalog.ApiRequiresPaidPlan, envelopeRequestId),
                V1ErrorCatalog.ApiRequiresPaidPlan.StatusCode);
        }

        var plan = AccountUsagePlans.GetUsagePlan(user, configuration);
        var rewriteRequest = new RewriteRequest(
            null,
            draft,
            null,
            null,
            null,
            null,
            "warm");

        var result = await createRewriteAttemptHandler.HandleAsync(
            new CreateRewriteAttemptCommand(
                user.Id,
                idempotencyKey,
                rewriteRequest,
                plan.PeriodKey,
                plan.QuotaLimit,
                now,
                auth.ApiKeyId,
                ResolveCommandCorrelationId(request)),
            cancellationToken);

        if (result.Kind == ApplicationResultKind.QuotaExceeded)
        {
            return await CompleteAsync(
                V1Problem(V1ErrorCatalog.QuotaExhausted, envelopeRequestId),
                V1ErrorCatalog.QuotaExhausted.StatusCode);
        }

        if (result.Kind == ApplicationResultKind.Conflict)
        {
            return await CompleteAsync(
                V1Problem(V1ErrorCatalog.IdempotencyConflict, envelopeRequestId),
                V1ErrorCatalog.IdempotencyConflict.StatusCode,
                result.Value?.AttemptId.ToString());
        }

        if (result.Value is null)
        {
            return await CompleteAsync(
                V1Problem(V1ErrorCatalog.RewriteFailed, envelopeRequestId),
                V1ErrorCatalog.RewriteFailed.StatusCode);
        }

        return await CompleteAsync(
            FunctionHttpResults.Accepted(
                $"/api/v1/rewrite/{result.Value.AttemptId}",
                new
                {
                    id = result.Value.AttemptId,
                    status = "processing",
                }),
            StatusCodes.Status202Accepted,
            result.Value.AttemptId.ToString());

        async Task<IActionResult> CompleteAsync(
            IActionResult result,
            int statusCode,
            string? requestId = null)
        {
            return await CompleteWithUsageAsync(
                auth.ApiKeyId,
                result,
                statusCode,
                stopwatch,
                now,
                cancellationToken,
                EndpointName,
                requestId,
                request.HttpContext.Response,
                rateLimit?.IsUnavailable == true ? null : rateLimit);
        }
    }

    [Function("V1GetRewriteResult")]
    public async Task<IActionResult> GetRewriteResult(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/rewrite/{id:guid}")]
        HttpRequest request,
        Guid id,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var now = DateTimeOffset.UtcNow;
        var envelopeRequestId = ResolveRequestId(request);
        var auth = await authResolver.ResolveAsync(
            request,
            now,
            cancellationToken);

        if (auth.UserId is null)
        {
            return V1Problem(V1ErrorCatalog.InvalidKey, envelopeRequestId);
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
            return await CompleteWithUsageAsync(
                auth.ApiKeyId,
                V1Problem(V1ErrorCatalog.RateLimitUnavailable, envelopeRequestId),
                V1ErrorCatalog.RateLimitUnavailable.StatusCode,
                stopwatch,
                now,
                cancellationToken,
                ResultEndpointName,
                id.ToString(),
                request.HttpContext.Response);
        }

        if (rateLimit?.IsLimited == true)
        {
            return await CompleteWithUsageAsync(
                auth.ApiKeyId,
                V1Problem(V1ErrorCatalog.RateLimited, envelopeRequestId),
                V1ErrorCatalog.RateLimited.StatusCode,
                stopwatch,
                now,
                cancellationToken,
                ResultEndpointName,
                id.ToString(),
                request.HttpContext.Response,
                rateLimit);
        }

        var getAttemptResult = await getRewriteAttemptHandler.HandleAsync(
            new GetRewriteAttemptQuery(id, auth.UserId.Value),
            cancellationToken);
        var attempt = getAttemptResult.Value;

        if (getAttemptResult.Kind == ApplicationResultKind.NotFound ||
            attempt is null ||
            IsSandboxAttempt(attempt) != auth.IsTest)
        {
            return await CompleteWithUsageAsync(
                auth.ApiKeyId,
                V1Problem(V1ErrorCatalog.NotFound, envelopeRequestId),
                V1ErrorCatalog.NotFound.StatusCode,
                stopwatch,
                now,
                cancellationToken,
                ResultEndpointName,
                id.ToString(),
                request.HttpContext.Response,
                rateLimit);
        }

        return await CompleteWithUsageAsync(
            auth.ApiKeyId,
            MapRewriteResult(attempt, envelopeRequestId),
            StatusCodes.Status200OK,
            stopwatch,
            now,
            cancellationToken,
            ResultEndpointName,
            id.ToString(),
            request.HttpContext.Response,
            rateLimit);
    }

    [Function("V1GetUsage")]
    public async Task<IActionResult> GetUsage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/usage")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var now = DateTimeOffset.UtcNow;
        var envelopeRequestId = ResolveRequestId(request);
        var auth = await authResolver.ResolveAsync(
            request,
            now,
            cancellationToken);

        if (auth.UserId is null)
        {
            return V1Problem(V1ErrorCatalog.InvalidKey, envelopeRequestId);
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
            return await CompleteWithUsageAsync(
                auth.ApiKeyId,
                V1Problem(V1ErrorCatalog.RateLimitUnavailable, envelopeRequestId),
                V1ErrorCatalog.RateLimitUnavailable.StatusCode,
                stopwatch,
                now,
                cancellationToken,
                UsageEndpointName,
                response: request.HttpContext.Response);
        }

        if (rateLimit?.IsLimited == true)
        {
            return await CompleteWithUsageAsync(
                auth.ApiKeyId,
                V1Problem(V1ErrorCatalog.RateLimited, envelopeRequestId),
                V1ErrorCatalog.RateLimited.StatusCode,
                stopwatch,
                now,
                cancellationToken,
                UsageEndpointName,
                response: request.HttpContext.Response,
                rateLimit: rateLimit);
        }

        if (auth.IsTest)
        {
            return await CompleteWithUsageAsync(
                auth.ApiKeyId,
                new OkObjectResult(new V1UsageResponse(
                    "test",
                    SandboxUsagePeriodKey,
                    0,
                    0,
                    0,
                    null)),
                StatusCodes.Status200OK,
                stopwatch,
                now,
                cancellationToken,
                UsageEndpointName,
                response: request.HttpContext.Response,
                rateLimit: rateLimit);
        }

        var user = await appUsers.GetByIdAsync(auth.UserId.Value, cancellationToken);
        if (user is null)
        {
            return await CompleteWithUsageAsync(
                auth.ApiKeyId,
                V1Problem(V1ErrorCatalog.InvalidKey, envelopeRequestId),
                V1ErrorCatalog.InvalidKey.StatusCode,
                stopwatch,
                now,
                cancellationToken,
                UsageEndpointName,
                response: request.HttpContext.Response,
                rateLimit: rateLimit);
        }

        var account = await getAccountSummaryHandler.HandleAsync(
            new GetAccountSummaryQuery(user.ExternalAuthUserId, user.Email),
            cancellationToken);

        return await CompleteWithUsageAsync(
            auth.ApiKeyId,
            new OkObjectResult(new V1UsageResponse(
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
            UsageEndpointName,
            response: request.HttpContext.Response,
            rateLimit: rateLimit);
    }

    private static IActionResult MapRewriteResult(RewriteAttemptDto attempt, string requestId)
    {
        if (attempt.Status == RewriteAttemptStatus.Pending.ToString() ||
            attempt.Status == RewriteAttemptStatus.Processing.ToString())
        {
            return new OkObjectResult(new
            {
                id = attempt.AttemptId,
                status = "processing",
            });
        }

        if (attempt.Status == RewriteAttemptStatus.Succeeded.ToString() &&
            TryReadSucceededResult(attempt.ResultJson, out var rewrittenText, out var draftSignal, out var rewriteSignal))
        {
            return new OkObjectResult(new
            {
                id = attempt.AttemptId,
                status = "succeeded",
                rewrittenText,
                signal = new
                {
                    draft = draftSignal,
                    rewrite = rewriteSignal,
                },
            });
        }

        var code = string.IsNullOrWhiteSpace(attempt.ErrorCode)
            ? V1ErrorCatalog.EngineUnavailableCode
            : attempt.ErrorCode;
        return new OkObjectResult(new
        {
            id = attempt.AttemptId,
            status = "failed",
            error = new
            {
                code,
                message = FailureMessage(attempt),
                requestId,
            },
        });
    }

    private async Task<SandboxAttemptResult> CreateSandboxAttemptAsync(
        Guid userId,
        Guid apiKeyId,
        string idempotencyKey,
        string draft,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sandboxIdempotencyKey = BuildSandboxIdempotencyKey(apiKeyId, idempotencyKey);
        var requestHash = ComputeSha256(draft);
        var existingAttempt = await rewriteAttempts.GetByUserIdAndIdempotencyKeyAsync(
            userId,
            sandboxIdempotencyKey,
            cancellationToken);

        if (existingAttempt is not null)
        {
            return string.Equals(existingAttempt.RequestHash, requestHash, StringComparison.Ordinal)
                ? new SandboxAttemptResult(existingAttempt.Id, IsConflict: false)
                : new SandboxAttemptResult(existingAttempt.Id, IsConflict: true);
        }

        var rewriteRequest = new RewriteRequest(
            null,
            draft,
            null,
            null,
            null,
            null,
            "warm");
        var attempt = new RewriteAttempt
        {
            UserId = userId,
            IdempotencyKey = sandboxIdempotencyKey,
            RequestHash = requestHash,
            RequestJson = JsonSerializer.Serialize(rewriteRequest),
            ApiKeyId = apiKeyId,
            Status = RewriteAttemptStatus.Succeeded,
            ResultJson = SandboxResultJson,
            CreatedAt = now,
            CompletedAt = now,
            ExpiresAt = now.AddMinutes(15),
        };

        await rewriteAttempts.AddAsync(attempt, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SandboxAttemptResult(attempt.Id, IsConflict: false);
    }

    private static bool IsSandboxAttempt(RewriteAttemptDto attempt) =>
        attempt.Status == RewriteAttemptStatus.Succeeded.ToString() &&
        attempt.IdempotencyKey.StartsWith(SandboxAttemptPrefix, StringComparison.Ordinal) &&
        string.Equals(attempt.ResultJson, SandboxResultJson, StringComparison.Ordinal);

    private static string BuildSandboxIdempotencyKey(Guid apiKeyId, string idempotencyKey) =>
        string.Concat(SandboxAttemptPrefix, apiKeyId.ToString("N"), ":", ComputeSha256(idempotencyKey));

    private static string ComputeSha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool TryReadSucceededResult(
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

    private static bool TryGetDecimal(JsonElement parent, string propertyName, out decimal value)
    {
        value = 0;
        return parent.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetDecimal(out value);
    }

    private static string FailureMessage(RewriteAttemptDto attempt) =>
        attempt.Status == RewriteAttemptStatus.Expired.ToString()
            ? V1ErrorCatalog.RewriteExpiredMessage
            : V1ErrorCatalog.RewriteCouldNotBeCompletedMessage;

    private static string ResolveRequestId(HttpRequest request)
    {
        if (request.HttpContext.Items.TryGetValue("CorrelationId", out var value) &&
            value is string requestId &&
            !string.IsNullOrWhiteSpace(requestId))
        {
            return requestId;
        }

        return HttpHardeningMiddleware.ResolveCorrelationId(request);
    }

    private static string? ResolveCommandCorrelationId(HttpRequest request)
    {
        if (request.HttpContext.Items.TryGetValue("CorrelationId", out var value) &&
            value is string correlationId &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        return null;
    }

    private static IActionResult V1Problem(V1ErrorCatalog.V1Error error, string requestId) =>
        FunctionHttpResults.Problem(error.Message, error.Message, error.StatusCode, error.Code, requestId);

    private static IActionResult V1InsufficientScopeProblem(string requestId) =>
        FunctionHttpResults.Problem(
            "This API key does not have the required scope.",
            "This API key does not have the required scope.",
            StatusCodes.Status403Forbidden,
            "insufficient_scope",
            requestId);

    private async Task<IActionResult> CompleteWithUsageAsync(
        Guid? apiKeyId,
        IActionResult result,
        int statusCode,
        Stopwatch stopwatch,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        string endpoint,
        string? requestId = null,
        HttpResponse? response = null,
        ApiKeyRateLimitResult? rateLimit = null)
    {
        stopwatch.Stop();
        await TryWriteApiKeyUsageAsync(
            apiKeyId,
            requestId ?? Guid.NewGuid().ToString("D"),
            endpoint,
            statusCode,
            (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue),
            now,
            cancellationToken);
        if (response is not null && rateLimit is not null)
        {
            SetRateLimitHeaders(
                response,
                rateLimit,
                statusCode == StatusCodes.Status429TooManyRequests,
                now);
        }

        return result;
    }

    private static void SetRateLimitHeaders(
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

    private async Task TryWriteApiKeyUsageAsync(
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
            await apiKeyUsages.AddAsync(new ApiKeyUsage
            {
                ApiKeyId = apiKeyId.Value,
                RequestId = requestId,
                Endpoint = endpoint,
                StatusCode = statusCode,
                LatencyMs = latencyMs,
                CreatedAt = now,
            }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
        }
    }

    private sealed class V1RewriteSubmitRequest
    {
        public string? Draft { get; set; }
    }

    private sealed record SandboxAttemptResult(Guid AttemptId, bool IsConflict);
}

public sealed record V1UsageResponse(
    string Scope,
    string PeriodKey,
    int Quota,
    int Used,
    int Remaining,
    DateTimeOffset? PeriodEnd);
