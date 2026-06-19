using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;
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

        var currentPepperVersion = ApiKeyHashing.CurrentPepperVersion;
        var currentKeyHash = ApiKeyHashing.ComputeHashWithVersion(token, currentPepperVersion);
        var apiKey = await ResolveMatchingApiKeyAsync(
            currentKeyHash,
            now,
            cancellationToken);
        var matchedPreviousPepper = false;

        if (apiKey is null &&
            ApiKeyHashing.TryGetPreviousPepperVersion(out var previousPepperVersion))
        {
            var previousKeyHash = ApiKeyHashing.ComputeHashWithVersion(token, previousPepperVersion);
            apiKey = await ResolveMatchingApiKeyAsync(
                previousKeyHash,
                now,
                cancellationToken);
            matchedPreviousPepper = apiKey is not null;
        }

        if (apiKey is null)
        {
            return new ApiKeyAuthResult(null, null, 0);
        }

        apiKeys.TouchLastUsed(apiKey, now);
        if (matchedPreviousPepper)
        {
            apiKey.KeyHash = currentKeyHash;
            apiKey.PepperVersion = currentPepperVersion;
            apiKey.RehashPending = true;
            apiKey.UpdatedAt = now;
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            apiKeys.DiscardPendingChanges(apiKey);
        }

        return new ApiKeyAuthResult(
            apiKey.UserId,
            apiKey.Id,
            apiKey.RateLimitPerMinute,
            apiKey.IsTest,
            ApiKeyScopes.Parse(apiKey.Scope));
    }

    private async Task<ApiKey?> ResolveMatchingApiKeyAsync(
        string keyHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var apiKey = await apiKeys.GetByKeyHashAsync(keyHash, cancellationToken);
        if (apiKey is null ||
            apiKey.RevokedAt is not null ||
            (apiKey.ExpiresAt is not null && apiKey.ExpiresAt <= now) ||
            !ApiKeyHashing.FixedTimeEquals(apiKey.KeyHash, keyHash))
        {
            return null;
        }

        return apiKey;
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

public sealed record ApiKeyAuthResult(
    Guid? UserId,
    Guid? ApiKeyId,
    int RateLimitPerMinute,
    bool IsTest = false,
    IReadOnlySet<string>? Scopes = null);
