using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Account;
using ReplyInMyVoice.Application.UseCases.Rewrite;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Http;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class RewriteHttpFunctions(
    IConfiguration configuration,
    AppDbContext db,
    GetOrCreateUserHandler getOrCreateUserHandler,
    FindUserHandler findUserHandler,
    CreateRewriteAttemptHandler createRewriteAttemptHandler,
    GetRewriteAttemptHandler getRewriteAttemptHandler,
    IUserRewriteRateLimiter userRateLimiter)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Function("CreateRewriteAttempt")]
    public async Task<IActionResult> CreateRewriteAttempt(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "rewrite")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var authUser = await FunctionAuthResolver.ResolveUserAsync(request, configuration, cancellationToken);
        if (authUser is null)
        {
            return FunctionHttpResults.Problem(
                "Authentication required",
                "A valid authenticated user is required.",
                StatusCodes.Status401Unauthorized);
        }

        var idempotencyKey = request.Headers["X-Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return FunctionHttpResults.Problem(
                "Missing idempotency key",
                "X-Idempotency-Key is required for rewrite requests.",
                StatusCodes.Status400BadRequest);
        }

        RewriteRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<RewriteRequest>(
                request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return FunctionHttpResults.Problem(
                "Invalid rewrite request",
                "Request body must be valid JSON.",
                StatusCodes.Status400BadRequest);
        }

        if (body is null)
        {
            return FunctionHttpResults.Problem(
                "Invalid rewrite request",
                "Request body is required.",
                StatusCodes.Status400BadRequest);
        }

        var validationError = ValidateRewriteRequest(body);
        if (validationError is not null)
        {
            return FunctionHttpResults.Problem(
                "Invalid rewrite request",
                validationError,
                StatusCodes.Status400BadRequest);
        }

        var user = await getOrCreateUserHandler.HandleAsync(
            new GetOrCreateUserCommand(authUser.ExternalAuthUserId, authUser.Email),
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
                return FunctionHttpResults.Problem(
                    "Rate limit check unavailable",
                    "Rewrite limit could not be checked. Please retry shortly.",
                    StatusCodes.Status503ServiceUnavailable,
                    "rate_limit_unavailable");
            }

            SetRateLimitHeaders(
                request.HttpContext.Response,
                rateLimit,
                rateLimit.IsLimited,
                now);
            if (rateLimit.IsLimited)
            {
                return FunctionHttpResults.Problem(
                    "Too many requests",
                    "Rewrite rate limit reached. Please wait a moment and retry.",
                    StatusCodes.Status429TooManyRequests,
                    "rate_limited");
            }
        }

        var plan = AccountUsagePlans.GetUsagePlan(user, configuration);
        var result = await createRewriteAttemptHandler.HandleAsync(
            new CreateRewriteAttemptCommand(
                user.Id,
                idempotencyKey,
                body,
                plan.PeriodKey,
                plan.QuotaLimit,
                now),
            cancellationToken);

        return ToCreateRewriteAttemptHttpResult(result);
    }

    [Function("GetRewriteAttempt")]
    public async Task<IActionResult> GetRewriteAttempt(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rewrite-attempts/{attemptId:guid}")]
        HttpRequest request,
        Guid attemptId,
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

        var user = await findUserHandler.HandleAsync(
            new FindUserQuery(authUser.ExternalAuthUserId),
            cancellationToken);
        if (user is null)
        {
            return new NotFoundResult();
        }

        var result = await getRewriteAttemptHandler.HandleAsync(
            new GetRewriteAttemptQuery(attemptId, user.Id),
            cancellationToken);

        return result.Kind == ApplicationResultKind.NotFound || result.Value is null
            ? new NotFoundResult()
            : new OkObjectResult(ToResponse(result.Value));
    }

    [Function("ListMyRewriteAttempts")]
    public async Task<IActionResult> ListMyRewriteAttempts(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me/rewrites")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var page = ParsePositiveInt(request.Query["page"].ToString(), 1);
        var pageSize = Math.Min(ParsePositiveInt(request.Query["pageSize"].ToString(), 20), 50);
        var authUser = await FunctionAuthResolver.ResolveUserAsync(request, configuration, cancellationToken);
        if (authUser is null)
        {
            return FunctionHttpResults.Problem(
                "Authentication required",
                null,
                StatusCodes.Status401Unauthorized);
        }

        var user = await findUserHandler.HandleAsync(
            new FindUserQuery(authUser.ExternalAuthUserId),
            cancellationToken);
        if (user is null)
        {
            return new OkObjectResult(new RewriteHistoryPageResponse(
                page,
                pageSize,
                0,
                []));
        }

        // TODO(DDD): no list-history handler yet - DDD-64
        var baseQuery = db.RewriteAttempts
            .AsNoTracking()
            .Where(x => x.UserId == user.Id && x.DeletedAt == null);
        var totalCount = await baseQuery.CountAsync(cancellationToken);
        List<RewriteHistoryItemResponse> attempts;
        if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            var allAttempts = await baseQuery
                .Select(x => new RewriteHistoryItemResponse(
                    x.Id,
                    x.Status.ToString(),
                    x.ResultJson,
                    x.ErrorCode,
                    x.CreatedAt,
                    x.CompletedAt))
                .ToListAsync(cancellationToken);
            attempts = allAttempts
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.AttemptId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }
        else
        {
            attempts = await baseQuery
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new RewriteHistoryItemResponse(
                    x.Id,
                    x.Status.ToString(),
                    x.ResultJson,
                    x.ErrorCode,
                    x.CreatedAt,
                    x.CompletedAt))
                .ToListAsync(cancellationToken);
        }

        return new OkObjectResult(new RewriteHistoryPageResponse(
            page,
            pageSize,
            totalCount,
            attempts));
    }

    [Function("GetMyRewriteAttempt")]
    public async Task<IActionResult> GetMyRewriteAttempt(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me/rewrites/{attemptId:guid}")]
        HttpRequest request,
        Guid attemptId,
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

        var user = await findUserHandler.HandleAsync(
            new FindUserQuery(authUser.ExternalAuthUserId),
            cancellationToken);
        if (user is null)
        {
            return new NotFoundResult();
        }

        var result = await getRewriteAttemptHandler.HandleAsync(
            new GetRewriteAttemptQuery(attemptId, user.Id),
            cancellationToken);
        if (result.Kind == ApplicationResultKind.NotFound || result.Value is null)
        {
            return new NotFoundResult();
        }

        return new OkObjectResult(new RewriteHistoryDetailResponse(
            result.Value.AttemptId,
            result.Value.Status,
            result.Value.RequestJson,
            result.Value.ResultJson,
            result.Value.ErrorCode,
            result.Value.ErrorMessage,
            result.Value.CreatedAt,
            result.Value.CompletedAt));
    }

    [Function("DeleteMyRewriteAttempt")]
    public async Task<IActionResult> DeleteMyRewriteAttempt(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "me/rewrites/{attemptId:guid}")]
        HttpRequest request,
        Guid attemptId,
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

        var user = await findUserHandler.HandleAsync(
            new FindUserQuery(authUser.ExternalAuthUserId),
            cancellationToken);
        if (user is null)
        {
            return new NotFoundResult();
        }

        // TODO(DDD): no delete-history handler yet - DDD-64
        var attempt = await db.RewriteAttempts
            .AsTracking()
            .SingleOrDefaultAsync(
                x => x.Id == attemptId && x.UserId == user.Id && x.DeletedAt == null,
                cancellationToken);
        if (attempt is null)
        {
            return new NotFoundResult();
        }

        attempt.DeletedAt = DateTimeOffset.UtcNow;
        attempt.RowVersion = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);
        return new NoContentResult();
    }

    private static string? ValidateRewriteRequest(RewriteRequest request)
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

    private static int ParsePositiveInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : fallback;
    }

    private static IActionResult ToCreateRewriteAttemptHttpResult(
        ApplicationResult<RewriteAttemptDto> result) =>
        result.Kind switch
        {
            ApplicationResultKind.Created or ApplicationResultKind.Existing or ApplicationResultKind.Success
                when result.Value is not null => ToRewriteAttemptHttpResult(result.Value),
            ApplicationResultKind.QuotaExceeded => FunctionHttpResults.Problem(
                "Rewrite quota exhausted",
                "No rewrite quota remains for the current period.",
                StatusCodes.Status402PaymentRequired,
                result.ErrorCode),
            ApplicationResultKind.Conflict => FunctionHttpResults.Problem(
                "Idempotency key conflict",
                "The same idempotency key was reused with a different rewrite request.",
                StatusCodes.Status409Conflict),
            ApplicationResultKind.NotFound => new NotFoundResult(),
            _ => FunctionHttpResults.Problem(
                "Rewrite request failed",
                "The rewrite request could not be processed.",
                StatusCodes.Status500InternalServerError),
        };

    private static IActionResult ToRewriteAttemptHttpResult(RewriteAttemptDto attempt)
    {
        var response = ToResponse(attempt);

        return attempt.Status == RewriteAttemptStatus.Succeeded.ToString()
            ? new OkObjectResult(response)
            : FunctionHttpResults.Accepted($"/api/rewrite-attempts/{attempt.AttemptId}", response);
    }

    private static RewriteAttemptResponse ToResponse(RewriteAttemptDto attempt) =>
        new(
            attempt.AttemptId,
            attempt.Status,
            attempt.ResultJson,
            attempt.ErrorCode);
}

public sealed record RewriteAttemptResponse(
    Guid AttemptId,
    string Status,
    string? ResultJson,
    string? ErrorCode);

public sealed record RewriteHistoryPageResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<RewriteHistoryItemResponse> Items);

public sealed record RewriteHistoryItemResponse(
    Guid AttemptId,
    string Status,
    string? ResultJson,
    string? ErrorCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record RewriteHistoryDetailResponse(
    Guid AttemptId,
    string Status,
    string RequestJson,
    string? ResultJson,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);
