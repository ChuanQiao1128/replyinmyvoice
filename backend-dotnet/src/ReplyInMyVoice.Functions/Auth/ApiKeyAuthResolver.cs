using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Auth;

public sealed class ApiKeyAuthResolver(
    IApiKeyRepository apiKeys,
    IUnitOfWork unitOfWork)
{
    private const string LiveKeyPrefix = "rmv_live_";
    private const string TestKeyPrefix = "rmv_test_";

    public async Task<Guid?> ResolveUserIdAsync(
        HttpRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var auth = await ResolveAsync(request, now, cancellationToken);
        return auth.UserId;
    }

    public async Task<ApiKeyAuthResult> ResolveAsync(
        HttpRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var token = ResolveBearerToken(request);
        if (string.IsNullOrWhiteSpace(token) ||
            !HasKnownPrefix(token))
        {
            return new ApiKeyAuthResult(null, null, 0);
        }

        var keyHash = ApiKeyHashing.ComputeHash(token);
        var apiKey = await apiKeys.GetByKeyHashAsync(keyHash, cancellationToken);

        if (apiKey is null ||
            apiKey.RevokedAt is not null ||
            (apiKey.ExpiresAt is not null && apiKey.ExpiresAt <= now))
        {
            return new ApiKeyAuthResult(null, null, 0);
        }

        apiKeys.TouchLastUsed(apiKey, now);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            apiKeys.DiscardPendingChanges(apiKey);
        }

        return new ApiKeyAuthResult(apiKey.UserId, apiKey.Id, apiKey.RateLimitPerMinute, apiKey.IsTest);
    }

    private static bool HasKnownPrefix(string token) =>
        token.StartsWith(LiveKeyPrefix, StringComparison.Ordinal) ||
        token.StartsWith(TestKeyPrefix, StringComparison.Ordinal);

    private static string? ResolveBearerToken(HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..].Trim()
            : null;
    }
}

public sealed record ApiKeyAuthResult(Guid? UserId, Guid? ApiKeyId, int RateLimitPerMinute, bool IsTest = false);
