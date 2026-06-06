using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Http;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class V1RewriteHttpFunctions(
    IConfiguration configuration,
    AppDbContext db,
    AccountService accountService,
    RewriteRequestService rewriteRequestService)
{
    private const string EndpointName = "v1/rewrite";
    private const string ResultEndpointName = "v1/rewrite/{id}";
    private const string UsageEndpointName = "v1/usage";
    private const int MinimumDraftLength = 10;
    private const int MaximumDraftWords = 300;
    private const int MaximumDraftCharacters = 2400;
    private const string SandboxAttemptPrefix = "test:";
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
        var auth = await ApiKeyAuthResolver.ResolveAsync(
            request,
            db,
            now,
            cancellationToken);

        if (auth.UserId is null)
        {
            return Error("invalid_key", "A valid API key is required.", StatusCodes.Status401Unauthorized);
        }

        var rateLimitWindow = auth.ApiKeyId is null
            ? null
            : await GetRateLimitWindowAsync(
                auth.ApiKeyId.Value,
                auth.RateLimitPerMinute,
                now,
                cancellationToken);
        if (rateLimitWindow?.IsLimited == true)
        {
            return await CompleteAsync(
                Error("rate_limited", "Request limit reached. Please retry later.", StatusCodes.Status429TooManyRequests),
                StatusCodes.Status429TooManyRequests);
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
                Error("invalid_request", "Request body must be valid JSON.", StatusCodes.Status400BadRequest),
                StatusCodes.Status400BadRequest);
        }

        var draft = body?.Draft?.Trim();
        if (string.IsNullOrWhiteSpace(draft) || draft.Length < MinimumDraftLength)
        {
            return await CompleteAsync(
                Error("invalid_request", "A draft of at least 10 characters is required.", StatusCodes.Status400BadRequest),
                StatusCodes.Status400BadRequest);
        }

        if (draft.Length > MaximumDraftCharacters || CountWords(draft) > MaximumDraftWords)
        {
            return await CompleteAsync(
                Error("input_too_long", "Draft must be 300 words or fewer and no more than 2400 characters.", StatusCodes.Status400BadRequest),
                StatusCodes.Status400BadRequest);
        }

        var user = await db.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == auth.UserId.Value, cancellationToken);
        if (user is null)
        {
            return await CompleteAsync(
                Error("invalid_key", "A valid API key is required.", StatusCodes.Status401Unauthorized),
                StatusCodes.Status401Unauthorized);
        }

        var plan = AccountService.GetUsagePlan(user, configuration);
        var idempotencyKey = request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            idempotencyKey = Guid.NewGuid().ToString("D");
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
                    Error("idempotency_conflict", "The idempotency key was reused with a different draft.", StatusCodes.Status409Conflict),
                    StatusCodes.Status409Conflict,
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

        var rewriteRequest = new RewriteRequest(
            null,
            draft,
            null,
            null,
            null,
            null,
            "warm");

        var result = await rewriteRequestService.CreateAttemptAsync(
            user.Id,
            idempotencyKey,
            rewriteRequest,
            plan.PeriodKey,
            plan.QuotaLimit,
            now,
            cancellationToken);

        if (result.Kind == ReserveRewriteResultKind.QuotaExceeded)
        {
            return await CompleteAsync(
                Error("quota_exhausted", "No rewrite quota remains for the current period.", StatusCodes.Status402PaymentRequired),
                StatusCodes.Status402PaymentRequired);
        }

        if (result.Kind == ReserveRewriteResultKind.Conflict)
        {
            return await CompleteAsync(
                Error("idempotency_conflict", "The idempotency key was reused with a different draft.", StatusCodes.Status409Conflict),
                StatusCodes.Status409Conflict,
                result.AttemptId.ToString());
        }

        return await CompleteAsync(
            FunctionHttpResults.Accepted(
                $"/api/v1/rewrite/{result.AttemptId}",
                new
                {
                    id = result.AttemptId,
                    status = "processing",
                }),
            StatusCodes.Status202Accepted,
            result.AttemptId.ToString());

        IActionResult Error(string code, string message, int statusCode) =>
            FunctionHttpResults.Problem(message, message, statusCode, code);

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
                rateLimitWindow);
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
        var auth = await ApiKeyAuthResolver.ResolveAsync(
            request,
            db,
            now,
            cancellationToken);

        if (auth.UserId is null)
        {
            return FunctionHttpResults.Problem(
                "A valid API key is required.",
                "A valid API key is required.",
                StatusCodes.Status401Unauthorized,
                "invalid_key");
        }

        var rateLimitWindow = auth.ApiKeyId is null
            ? null
            : await GetRateLimitWindowAsync(
                auth.ApiKeyId.Value,
                auth.RateLimitPerMinute,
                now,
                cancellationToken);
        if (rateLimitWindow?.IsLimited == true)
        {
            return await CompleteWithUsageAsync(
                auth.ApiKeyId,
                FunctionHttpResults.Problem(
                    "Request limit reached. Please retry later.",
                    "Request limit reached. Please retry later.",
                    StatusCodes.Status429TooManyRequests,
                    "rate_limited"),
                StatusCodes.Status429TooManyRequests,
                stopwatch,
                now,
                cancellationToken,
                ResultEndpointName,
                id.ToString(),
                request.HttpContext.Response,
                rateLimitWindow);
        }

        var attempt = await db.RewriteAttempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == id && x.UserId == auth.UserId.Value,
                cancellationToken);

        if (attempt is null || (auth.IsTest && !IsSandboxAttempt(attempt)))
        {
            return await CompleteWithUsageAsync(
                auth.ApiKeyId,
                FunctionHttpResults.Problem(
                    "Rewrite result was not found.",
                    "Rewrite result was not found.",
                    StatusCodes.Status404NotFound,
                    "not_found"),
                StatusCodes.Status404NotFound,
                stopwatch,
                now,
                cancellationToken,
                ResultEndpointName,
                id.ToString(),
                request.HttpContext.Response,
                rateLimitWindow);
        }

        return await CompleteWithUsageAsync(
            auth.ApiKeyId,
            MapRewriteResult(attempt),
            StatusCodes.Status200OK,
            stopwatch,
            now,
            cancellationToken,
            ResultEndpointName,
            id.ToString(),
            request.HttpContext.Response,
            rateLimitWindow);
    }

    [Function("V1GetUsage")]
    public async Task<IActionResult> GetUsage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/usage")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var now = DateTimeOffset.UtcNow;
        var auth = await ApiKeyAuthResolver.ResolveAsync(
            request,
            db,
            now,
            cancellationToken);

        if (auth.UserId is null)
        {
            return FunctionHttpResults.Problem(
                "A valid API key is required.",
                "A valid API key is required.",
                StatusCodes.Status401Unauthorized,
                "invalid_key");
        }

        var rateLimitWindow = auth.ApiKeyId is null
            ? null
            : await GetRateLimitWindowAsync(
                auth.ApiKeyId.Value,
                auth.RateLimitPerMinute,
                now,
                cancellationToken);
        if (rateLimitWindow?.IsLimited == true)
        {
            return await CompleteWithUsageAsync(
                auth.ApiKeyId,
                FunctionHttpResults.Problem(
                    "Request limit reached. Please retry later.",
                    "Request limit reached. Please retry later.",
                    StatusCodes.Status429TooManyRequests,
                    "rate_limited"),
                StatusCodes.Status429TooManyRequests,
                stopwatch,
                now,
                cancellationToken,
                UsageEndpointName,
                response: request.HttpContext.Response,
                rateLimitWindow: rateLimitWindow);
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
                rateLimitWindow: rateLimitWindow);
        }

        var user = await db.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == auth.UserId.Value, cancellationToken);
        if (user is null)
        {
            return await CompleteWithUsageAsync(
                auth.ApiKeyId,
                FunctionHttpResults.Problem(
                    "A valid API key is required.",
                    "A valid API key is required.",
                    StatusCodes.Status401Unauthorized,
                    "invalid_key"),
                StatusCodes.Status401Unauthorized,
                stopwatch,
                now,
                cancellationToken,
                UsageEndpointName,
                response: request.HttpContext.Response,
                rateLimitWindow: rateLimitWindow);
        }

        var account = await accountService.GetOrCreateAccountSummaryAsync(
            user.ExternalAuthUserId,
            user.Email,
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
            rateLimitWindow: rateLimitWindow);
    }

    private static IActionResult MapRewriteResult(RewriteAttempt attempt)
    {
        if (attempt.Status is RewriteAttemptStatus.Pending or RewriteAttemptStatus.Processing)
        {
            return new OkObjectResult(new
            {
                id = attempt.Id,
                status = "processing",
            });
        }

        if (attempt.Status == RewriteAttemptStatus.Succeeded &&
            TryReadSucceededResult(attempt.ResultJson, out var rewrittenText, out var draftSignal, out var rewriteSignal))
        {
            return new OkObjectResult(new
            {
                id = attempt.Id,
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
            ? "engine_unavailable"
            : attempt.ErrorCode;
        return new OkObjectResult(new
        {
            id = attempt.Id,
            status = "failed",
            error = new
            {
                code,
                message = FailureMessage(attempt),
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
        var existingAttempt = await db.RewriteAttempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == userId && x.IdempotencyKey == sandboxIdempotencyKey,
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
            Status = RewriteAttemptStatus.Succeeded,
            ResultJson = SandboxResultJson,
            CreatedAt = now,
            CompletedAt = now,
            ExpiresAt = now.AddMinutes(15),
        };

        db.RewriteAttempts.Add(attempt);
        await db.SaveChangesAsync(cancellationToken);

        return new SandboxAttemptResult(attempt.Id, IsConflict: false);
    }

    private static bool IsSandboxAttempt(RewriteAttempt attempt) =>
        attempt.Status == RewriteAttemptStatus.Succeeded &&
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

    private static string FailureMessage(RewriteAttempt attempt) =>
        attempt.Status == RewriteAttemptStatus.Expired
            ? "The rewrite did not finish in time. Please submit a new request."
            : "The rewrite could not be completed. Please try again.";

    private async Task<V1RateLimitWindow> GetRateLimitWindowAsync(
        Guid apiKeyId,
        int rateLimitPerMinute,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (rateLimitPerMinute <= 0)
        {
            return new V1RateLimitWindow(0, 0, now.AddMinutes(1));
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
            var recentCreatedAtValues = createdAtValues
                .Where(x => x >= windowStart)
                .OrderBy(x => x)
                .ToList();
            var sqliteResetAt = recentCreatedAtValues.Count == 0
                ? now.AddMinutes(1)
                : recentCreatedAtValues[0].AddMinutes(1);

            return new V1RateLimitWindow(rateLimitPerMinute, recentCreatedAtValues.Count, sqliteResetAt);
        }

        var recentCallCount = await usageQuery.CountAsync(
            x => x.CreatedAt >= windowStart,
            cancellationToken);
        var earliestCallAt = recentCallCount == 0
            ? now
            : await usageQuery
                .Where(x => x.CreatedAt >= windowStart)
                .MinAsync(x => x.CreatedAt, cancellationToken);
        var resetAt = recentCallCount == 0
            ? now.AddMinutes(1)
            : earliestCallAt.AddMinutes(1);

        return new V1RateLimitWindow(rateLimitPerMinute, recentCallCount, resetAt);
    }

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
        V1RateLimitWindow? rateLimitWindow = null)
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
        if (response is not null && rateLimitWindow is not null)
        {
            SetRateLimitHeaders(
                response,
                rateLimitWindow.WithCurrentCall(now),
                statusCode == StatusCodes.Status429TooManyRequests,
                now);
        }

        return result;
    }

    private static void SetRateLimitHeaders(
        HttpResponse response,
        V1RateLimitWindow rateLimitWindow,
        bool includeRetryAfter,
        DateTimeOffset now)
    {
        response.Headers["X-RateLimit-Limit"] = rateLimitWindow.Limit.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-RateLimit-Remaining"] = rateLimitWindow.Remaining.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-RateLimit-Reset"] = rateLimitWindow.ResetAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        if (includeRetryAfter)
        {
            response.Headers.RetryAfter = rateLimitWindow.RetryAfterSeconds(now).ToString(CultureInfo.InvariantCulture);
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

    private static int CountWords(string value)
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

    private sealed record V1RateLimitWindow(int Limit, int Calls, DateTimeOffset ResetAt)
    {
        public bool IsLimited => Limit <= 0 || Calls >= Limit;

        public int Remaining => Math.Max(0, Limit - Calls);

        public V1RateLimitWindow WithCurrentCall(DateTimeOffset now) =>
            this with
            {
                Calls = Calls + 1,
                ResetAt = Calls == 0 ? now.AddMinutes(1) : ResetAt,
            };

        public int RetryAfterSeconds(DateTimeOffset now) =>
            Math.Max(1, (int)Math.Ceiling((ResetAt - now).TotalSeconds));
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
