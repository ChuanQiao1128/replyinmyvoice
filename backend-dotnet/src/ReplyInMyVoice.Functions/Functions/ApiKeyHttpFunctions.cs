using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Http;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class ApiKeyHttpFunctions(
    IConfiguration configuration,
    AccountService accountService,
    ApiKeyService apiKeyService)
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

        var user = await accountService.GetOrCreateUserAsync(
            authUser.ExternalAuthUserId,
            authUser.Email,
            cancellationToken);
        var generated = await apiKeyService.GenerateAsync(user.Id, name, cancellationToken);

        return new CreatedResult(
            $"/api/keys/{generated.Id}",
            new ApiKeyCreateResponse(
                generated.Id,
                name,
                generated.Plaintext,
                generated.CreatedAt));
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

        var user = await accountService.GetOrCreateUserAsync(
            authUser.ExternalAuthUserId,
            authUser.Email,
            cancellationToken);
        var summaries = await apiKeyService.ListAsync(user.Id, cancellationToken);
        var response = summaries
            .Select(x => new ApiKeyListResponse(
                x.Id,
                x.Name,
                x.MaskedKey,
                x.LastUsedAt,
                x.CreatedAt,
                x.RevokedAt))
            .ToArray();

        return new OkObjectResult(response);
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

        var user = await accountService.GetOrCreateUserAsync(
            authUser.ExternalAuthUserId,
            authUser.Email,
            cancellationToken);
        var revoked = await apiKeyService.RevokeAsync(user.Id, id, cancellationToken);

        return revoked ? new NoContentResult() : new NotFoundResult();
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
}

public sealed record ApiKeyCreateRequest(string? Name);

public sealed record ApiKeyCreateResponse(
    Guid Id,
    string Name,
    string Key,
    DateTimeOffset CreatedAt);

public sealed record ApiKeyListResponse(
    Guid Id,
    string Name,
    string MaskedKey,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt);
