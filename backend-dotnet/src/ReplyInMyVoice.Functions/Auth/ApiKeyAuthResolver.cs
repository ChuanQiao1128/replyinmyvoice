using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;
using System.Security.Cryptography;
using System.Text;

namespace ReplyInMyVoice.Functions.Auth;

public static class ApiKeyAuthResolver
{
    private const string LiveKeyPrefix = "rmv_live_";
    private const string TestKeyPrefix = "rmv_test_";

    public static async Task<Guid?> ResolveUserIdAsync(
        HttpRequest request,
        AppDbContext db,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var auth = await ResolveAsync(request, db, now, cancellationToken);
        return auth.UserId;
    }

    public static async Task<ApiKeyAuthResult> ResolveAsync(
        HttpRequest request,
        AppDbContext db,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var token = ResolveBearerToken(request);
        if (string.IsNullOrWhiteSpace(token) ||
            !HasKnownPrefix(token))
        {
            return new ApiKeyAuthResult(null, null, 0);
        }

        var hashAttempts = BuildHashAttempts(token);
        var attemptedHashes = hashAttempts
            .Select(x => x.Hash)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var apiKeys = await db.ApiKeys
            .Where(x => attemptedHashes.Contains(x.KeyHash))
            .ToListAsync(cancellationToken);
        var match = FindMatch(apiKeys, hashAttempts);
        var apiKey = match?.ApiKey;

        if (apiKey is null ||
            apiKey.RevokedAt is not null ||
            (apiKey.ExpiresAt is not null && apiKey.ExpiresAt <= now))
        {
            return new ApiKeyAuthResult(null, null, 0);
        }

        apiKey.LastUsedAt = now;
        apiKey.UpdatedAt = now;
        apiKey.RowVersion = Guid.NewGuid();
        if (match!.NeedsRehash)
        {
            apiKey.RehashPending = true;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // This timestamp is best-effort and must not block an otherwise valid key.
            db.Entry(apiKey).State = EntityState.Unchanged;
        }

        return new ApiKeyAuthResult(
            apiKey.UserId,
            apiKey.Id,
            apiKey.RateLimitPerMinute,
            apiKey.IsTest,
            match.NeedsRehash);
    }

    public static async Task<bool> RehashIfNeededAsync(
        HttpRequest request,
        AppDbContext db,
        ApiKeyAuthResult auth,
        CancellationToken cancellationToken)
    {
        var token = ResolveBearerToken(request);
        if (string.IsNullOrWhiteSpace(token) ||
            !HasKnownPrefix(token))
        {
            return false;
        }

        return await RehashIfNeededAsync(token, db, auth, cancellationToken);
    }

    public static async Task<bool> RehashIfNeededAsync(
        string token,
        AppDbContext db,
        ApiKeyAuthResult auth,
        CancellationToken cancellationToken)
    {
        if (!auth.NeedsRehash ||
            auth.ApiKeyId is null ||
            string.IsNullOrWhiteSpace(token) ||
            !HasKnownPrefix(token))
        {
            return false;
        }

        var currentVersion = ApiKeyPepperVersions.GetCurrentPepperVersion();
        var apiKey = await db.ApiKeys
            .AsTracking()
            .SingleOrDefaultAsync(
                x => x.Id == auth.ApiKeyId.Value && x.RehashPending,
                cancellationToken);
        if (apiKey is null || apiKey.PepperVersion >= currentVersion)
        {
            return false;
        }

        apiKey.KeyHash = ApiKeyHashing.ComputeHashWithVersion(token, currentVersion);
        apiKey.PepperVersion = currentVersion;
        apiKey.RehashPending = false;
        apiKey.UpdatedAt = DateTimeOffset.UtcNow;
        apiKey.RowVersion = Guid.NewGuid();

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            db.Entry(apiKey).State = EntityState.Unchanged;
            return false;
        }
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

    private static IReadOnlyList<ApiKeyHashAttempt> BuildHashAttempts(string token)
    {
        var currentVersion = ApiKeyPepperVersions.GetCurrentPepperVersion();
        var attempts = new List<ApiKeyHashAttempt>
        {
            new(
                currentVersion,
                ApiKeyHashing.ComputeHashWithVersion(token, currentVersion),
                NeedsRehash: false),
        };

        var previousVersion = currentVersion - 1;
        if (previousVersion >= ApiKeyPepperVersions.LegacyVersion &&
            ApiKeyPepperVersions.HasPepperForVersion(previousVersion))
        {
            attempts.Add(new(
                previousVersion,
                ApiKeyHashing.ComputeHashWithVersion(token, previousVersion),
                NeedsRehash: true));
        }

        return attempts;
    }

    private static ApiKeyMatch? FindMatch(
        IReadOnlyCollection<Domain.Entities.ApiKey> apiKeys,
        IReadOnlyList<ApiKeyHashAttempt> hashAttempts)
    {
        foreach (var attempt in hashAttempts)
        {
            foreach (var apiKey in apiKeys)
            {
                if (FixedTimeEquals(apiKey.KeyHash, attempt.Hash))
                {
                    return new ApiKeyMatch(apiKey, attempt.NeedsRehash);
                }
            }
        }

        return null;
    }

    private static bool FixedTimeEquals(string storedHash, string computedHash)
    {
        var storedBytes = Encoding.UTF8.GetBytes(storedHash);
        var computedBytes = Encoding.UTF8.GetBytes(computedHash);
        return storedBytes.Length == computedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(storedBytes, computedBytes);
    }

    private sealed record ApiKeyHashAttempt(int Version, string Hash, bool NeedsRehash);

    private sealed record ApiKeyMatch(Domain.Entities.ApiKey ApiKey, bool NeedsRehash);
}

public sealed record ApiKeyAuthResult(
    Guid? UserId,
    Guid? ApiKeyId,
    int RateLimitPerMinute,
    bool IsTest = false,
    bool NeedsRehash = false);
