using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Http;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class V1RewriteHttpFunctions(
    IConfiguration configuration,
    AppDbContext db,
    RewriteRequestService rewriteRequestService)
{
    private const string EndpointName = "v1/rewrite";
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
        var bearerToken = ResolveBearerToken(request);
        var userId = await ApiKeyAuthResolver.ResolveUserIdAsync(
            request,
            db,
            now,
            cancellationToken);

        if (userId is null)
        {
            return Error("invalid_key", "A valid API key is required.", StatusCodes.Status401Unauthorized);
        }

        var apiKeyId = await ResolveApiKeyIdAsync(bearerToken, userId.Value, cancellationToken);

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
            .SingleOrDefaultAsync(x => x.Id == userId.Value, cancellationToken);
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
            stopwatch.Stop();
            await TryWriteApiKeyUsageAsync(
                apiKeyId,
                requestId ?? Guid.NewGuid().ToString("D"),
                statusCode,
                (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue),
                now,
                cancellationToken);
            return result;
        }
    }

    private async Task<Guid?> ResolveApiKeyIdAsync(
        string? bearerToken,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return null;
        }

        var keyHash = ApiKeyService.ComputeHash(bearerToken);
        return await db.ApiKeys
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.KeyHash == keyHash)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task TryWriteApiKeyUsageAsync(
        Guid? apiKeyId,
        string requestId,
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
                Endpoint = EndpointName,
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

    private static string? ResolveBearerToken(HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..].Trim()
            : null;
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
