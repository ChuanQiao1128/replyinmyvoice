using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    AccountService accountService,
    RewriteRequestService rewriteRequestService)
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

        var user = await accountService.GetOrCreateUserAsync(
            authUser.ExternalAuthUserId,
            authUser.Email,
            cancellationToken);
        var plan = AccountService.GetUsagePlan(user);
        var result = await rewriteRequestService.CreateAttemptAsync(
            user.Id,
            idempotencyKey,
            body,
            plan.PeriodKey,
            plan.QuotaLimit,
            DateTimeOffset.UtcNow,
            cancellationToken);

        if (result.Kind == ReserveRewriteResultKind.QuotaExceeded)
        {
            return FunctionHttpResults.Problem(
                "Rewrite quota exhausted",
                "No rewrite quota remains for the current period.",
                StatusCodes.Status402PaymentRequired);
        }

        if (result.Kind == ReserveRewriteResultKind.Conflict)
        {
            return FunctionHttpResults.Problem(
                "Idempotency key conflict",
                "The same idempotency key was reused with a different rewrite request.",
                StatusCodes.Status409Conflict);
        }

        var response = new RewriteAttemptResponse(
            result.AttemptId,
            result.Status.ToString(),
            result.ResultJson,
            result.ErrorCode);

        return result.Status == RewriteAttemptStatus.Succeeded
            ? new OkObjectResult(response)
            : FunctionHttpResults.Accepted($"/api/rewrite-attempts/{result.AttemptId}", response);
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

        var user = await db.AppUsers.SingleOrDefaultAsync(
            x => x.ExternalAuthUserId == authUser.ExternalAuthUserId,
            cancellationToken);
        if (user is null)
        {
            return new NotFoundResult();
        }

        var attempt = await db.RewriteAttempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == attemptId && x.UserId == user.Id,
                cancellationToken);
        if (attempt is null)
        {
            return new NotFoundResult();
        }

        return new OkObjectResult(new RewriteAttemptResponse(
            attempt.Id,
            attempt.Status.ToString(),
            attempt.ResultJson,
            attempt.ErrorCode));
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
}

public sealed record RewriteAttemptResponse(
    Guid AttemptId,
    string Status,
    string? ResultJson,
    string? ErrorCode);
