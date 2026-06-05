using System.Diagnostics;
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
    private const int MinimumDraftLength = 10;
    private const int MaximumDraftWords = 300;
    private const int MaximumDraftCharacters = 2400;

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

        if (auth.ApiKeyId is not null &&
            await IsRateLimitedAsync(
                auth.ApiKeyId.Value,
                auth.RateLimitPerMinute,
                now,
                cancellationToken))
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
                requestId);
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

        if (auth.ApiKeyId is not null &&
            await IsRateLimitedAsync(
                auth.ApiKeyId.Value,
                auth.RateLimitPerMinute,
                now,
                cancellationToken))
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
                id.ToString());
        }

        var attempt = await db.RewriteAttempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == id && x.UserId == auth.UserId.Value,
                cancellationToken);

        if (attempt is null)
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
                id.ToString());
        }

        return await CompleteWithUsageAsync(
            auth.ApiKeyId,
            MapRewriteResult(attempt),
            StatusCodes.Status200OK,
            stopwatch,
            now,
            cancellationToken,
            ResultEndpointName,
            id.ToString());
    }

    [Function("V1GetUsage")]
    public async Task<IActionResult> GetUsage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/usage")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var userId = await ApiKeyAuthResolver.ResolveUserIdAsync(
            request,
            db,
            now,
            cancellationToken);

        if (userId is null)
        {
            return FunctionHttpResults.Problem(
                "A valid API key is required.",
                "A valid API key is required.",
                StatusCodes.Status401Unauthorized,
                "invalid_key");
        }

        var user = await db.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == userId.Value, cancellationToken);
        if (user is null)
        {
            return FunctionHttpResults.Problem(
                "A valid API key is required.",
                "A valid API key is required.",
                StatusCodes.Status401Unauthorized,
                "invalid_key");
        }

        var account = await accountService.GetOrCreateAccountSummaryAsync(
            user.ExternalAuthUserId,
            user.Email,
            cancellationToken);

        return new OkObjectResult(new V1UsageResponse(
            account.Usage.Scope,
            account.Usage.PeriodKey,
            account.Usage.Quota,
            account.Usage.Used,
            account.Usage.Remaining,
            user.CurrentPeriodEnd));
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

    private async Task<bool> IsRateLimitedAsync(
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

    private async Task<IActionResult> CompleteWithUsageAsync(
        Guid? apiKeyId,
        IActionResult result,
        int statusCode,
        Stopwatch stopwatch,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        string endpoint,
        string? requestId = null)
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
        return result;
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

    private sealed class V1RewriteSubmitRequest
    {
        public string? Draft { get; set; }
    }
}

public sealed record V1UsageResponse(
    string Scope,
    string PeriodKey,
    int Quota,
    int Used,
    int Remaining,
    DateTimeOffset? PeriodEnd);
