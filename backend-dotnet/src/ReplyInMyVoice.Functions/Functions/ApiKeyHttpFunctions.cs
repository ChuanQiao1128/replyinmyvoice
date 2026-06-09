using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.UseCases.Account;
using ReplyInMyVoice.Application.UseCases.ApiKey;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Http;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class ApiKeyHttpFunctions(
    IConfiguration configuration,
    GetOrCreateUserHandler getOrCreateUserHandler,
    GenerateApiKeyHandler generateApiKeyHandler,
    ListApiKeysHandler listApiKeysHandler,
    RotateApiKeyHandler rotateApiKeyHandler,
    RevokeApiKeyHandler revokeApiKeyHandler,
    SetApiKeyWebhookHandler setApiKeyWebhookHandler,
    ClearApiKeyWebhookHandler clearApiKeyWebhookHandler)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Function("CreateApiKey")]
    public async Task<IActionResult> CreateApiKey(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "keys")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var authUser = await FunctionAuthResolver.ResolveUserAsync(request, configuration, cancellationToken);
        if (authUser is null)
        {
            return AuthenticationRequired();
        }

        ApiKeyCreateRequest? body;
        try
        {
            body = await ReadCreateRequestAsync(request, cancellationToken);
        }
        catch (JsonException)
        {
            return InvalidRequest("Request body must be valid JSON.");
        }

        var name = body?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return InvalidRequest("Name is required.");
        }

        if (name.Length > 200)
        {
            return InvalidRequest("Name must be 200 characters or less.");
        }

        var user = await getOrCreateUserHandler.HandleAsync(
            new GetOrCreateUserCommand(authUser.ExternalAuthUserId, authUser.Email),
            cancellationToken);
        var isTest = body?.Test == true;
        var generated = await generateApiKeyHandler.HandleAsync(
            new GenerateApiKeyCommand(user.Id, name, isTest),
            cancellationToken);

        return new CreatedResult(
            $"/api/keys/{generated.Id}",
            new ApiKeyCreateResponse(
                generated.Id,
                name,
                generated.Plaintext,
                generated.CreatedAt,
                isTest));
    }

    [Function("ListApiKeys")]
    public async Task<IActionResult> ListApiKeys(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "keys")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var authUser = await FunctionAuthResolver.ResolveUserAsync(request, configuration, cancellationToken);
        if (authUser is null)
        {
            return AuthenticationRequired();
        }

        var user = await getOrCreateUserHandler.HandleAsync(
            new GetOrCreateUserCommand(authUser.ExternalAuthUserId, authUser.Email),
            cancellationToken);
        var summaries = await listApiKeysHandler.HandleAsync(
            new ListApiKeysQuery(user.Id),
            cancellationToken);
        var response = summaries
            .Select(x => new ApiKeyListResponse(
                x.Id,
                x.Name,
                x.MaskedKey,
                x.IsTest,
                x.LastUsedAt,
                x.CreatedAt,
                x.RevokedAt,
                x.WebhookUrl,
                ToResponse(x.Last30dUsage)))
            .ToArray();

        return new OkObjectResult(response);
    }

    [Function("RotateApiKey")]
    public async Task<IActionResult> RotateApiKey(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "keys/{id:guid}/rotate")]
        HttpRequest request,
        Guid id,
        CancellationToken cancellationToken)
    {
        var authUser = await FunctionAuthResolver.ResolveUserAsync(request, configuration, cancellationToken);
        if (authUser is null)
        {
            return AuthenticationRequired();
        }

        var user = await getOrCreateUserHandler.HandleAsync(
            new GetOrCreateUserCommand(authUser.ExternalAuthUserId, authUser.Email),
            cancellationToken);
        var rotated = await rotateApiKeyHandler.HandleAsync(
            new RotateApiKeyCommand(user.Id, id),
            cancellationToken);

        return rotated is null
            ? new NotFoundResult()
            : new CreatedResult(
                $"/api/keys/{rotated.Id}",
                new ApiKeyCreateResponse(
                    rotated.Id,
                    rotated.Name,
                    rotated.Plaintext,
                    rotated.CreatedAt,
                    rotated.IsTest));
    }

    [Function("RevokeApiKey")]
    public async Task<IActionResult> RevokeApiKey(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "keys/{id:guid}")]
        HttpRequest request,
        Guid id,
        CancellationToken cancellationToken)
    {
        var authUser = await FunctionAuthResolver.ResolveUserAsync(request, configuration, cancellationToken);
        if (authUser is null)
        {
            return AuthenticationRequired();
        }

        var user = await getOrCreateUserHandler.HandleAsync(
            new GetOrCreateUserCommand(authUser.ExternalAuthUserId, authUser.Email),
            cancellationToken);
        var revoked = await revokeApiKeyHandler.HandleAsync(
            new RevokeApiKeyCommand(user.Id, id),
            cancellationToken);

        return revoked ? new NoContentResult() : new NotFoundResult();
    }

    [Function("SetApiKeyWebhook")]
    public async Task<IActionResult> SetApiKeyWebhook(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "keys/{id:guid}/webhook")]
        HttpRequest request,
        Guid id,
        CancellationToken cancellationToken)
    {
        var authUser = await FunctionAuthResolver.ResolveUserAsync(request, configuration, cancellationToken);
        if (authUser is null)
        {
            return AuthenticationRequired();
        }

        ApiKeyWebhookRequest? body;
        try
        {
            body = await ReadWebhookRequestAsync(request, cancellationToken);
        }
        catch (JsonException)
        {
            return InvalidRequest("Request body must be valid JSON.");
        }

        if (!ApiKeyWebhookUrl.TryNormalizeWebhookUrl(body?.WebhookUrl, out var webhookUrl))
        {
            return InvalidRequest("Webhook URL must be an absolute HTTPS URL that resolves to a public address.");
        }

        var user = await getOrCreateUserHandler.HandleAsync(
            new GetOrCreateUserCommand(authUser.ExternalAuthUserId, authUser.Email),
            cancellationToken);
        var updated = await setApiKeyWebhookHandler.HandleAsync(
            new SetApiKeyWebhookCommand(user.Id, id, webhookUrl),
            cancellationToken);

        return updated is null
            ? new NotFoundResult()
            : new OkObjectResult(new ApiKeyWebhookResponse(
                updated.Id,
                updated.WebhookUrl,
                updated.WebhookSecret));
    }

    [Function("ClearApiKeyWebhook")]
    public async Task<IActionResult> ClearApiKeyWebhook(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "keys/{id:guid}/webhook")]
        HttpRequest request,
        Guid id,
        CancellationToken cancellationToken)
    {
        var authUser = await FunctionAuthResolver.ResolveUserAsync(request, configuration, cancellationToken);
        if (authUser is null)
        {
            return AuthenticationRequired();
        }

        var user = await getOrCreateUserHandler.HandleAsync(
            new GetOrCreateUserCommand(authUser.ExternalAuthUserId, authUser.Email),
            cancellationToken);
        var cleared = await clearApiKeyWebhookHandler.HandleAsync(
            new ClearApiKeyWebhookCommand(user.Id, id),
            cancellationToken);

        return cleared ? new NoContentResult() : new NotFoundResult();
    }

    private static async Task<ApiKeyCreateRequest?> ReadCreateRequestAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ApiKeyCreateRequest>(body, JsonOptions);
    }

    private static async Task<ApiKeyWebhookRequest?> ReadWebhookRequestAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ApiKeyWebhookRequest>(body, JsonOptions);
    }

    private static IActionResult AuthenticationRequired() =>
        FunctionHttpResults.Problem(
            "Authentication required",
            "A valid authenticated user is required.",
            StatusCodes.Status401Unauthorized);

    private static IActionResult InvalidRequest(string detail) =>
        FunctionHttpResults.Problem(
            "Invalid API key request",
            detail,
            StatusCodes.Status400BadRequest);

    private static ApiUsageCount ToResponse(ReplyInMyVoice.Application.Common.ApiUsageCountDto count) =>
        new(count.Calls, count.Succeeded, count.Failed);
}

public sealed record ApiKeyCreateRequest(string? Name, bool Test = false);

public sealed record ApiKeyWebhookRequest(string? WebhookUrl);

public sealed record ApiKeyCreateResponse(
    Guid Id,
    string Name,
    string Key,
    DateTimeOffset CreatedAt,
    bool IsTest);

public sealed record ApiKeyListResponse(
    Guid Id,
    string Name,
    string MaskedKey,
    bool IsTest,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt,
    string? WebhookUrl,
    ApiUsageCount Last30dUsage);

public sealed record ApiKeyWebhookResponse(
    Guid Id,
    string WebhookUrl,
    string WebhookSecret);
