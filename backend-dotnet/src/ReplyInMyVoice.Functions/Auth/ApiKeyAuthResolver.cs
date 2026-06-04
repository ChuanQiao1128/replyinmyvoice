using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Auth;

public static class ApiKeyAuthResolver
{
    private const string KeyPrefix = "rmv_live_";

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
            !token.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return new ApiKeyAuthResult(null, null, 0);
        }

        var keyHash = ApiKeyService.ComputeHash(token);
        var apiKey = await db.ApiKeys
            .SingleOrDefaultAsync(x => x.KeyHash == keyHash, cancellationToken);

        if (apiKey is null ||
            apiKey.RevokedAt is not null ||
            (apiKey.ExpiresAt is not null && apiKey.ExpiresAt <= now))
        {
            return new ApiKeyAuthResult(null, null, 0);
        }

        apiKey.LastUsedAt = now;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // This timestamp is best-effort and must not block an otherwise valid key.
            db.Entry(apiKey).State = EntityState.Unchanged;
        }

        return new ApiKeyAuthResult(apiKey.UserId, apiKey.Id, apiKey.RateLimitPerMinute);
    }

    private static string? ResolveBearerToken(HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..].Trim()
            : null;
    }
}

public sealed record ApiKeyAuthResult(Guid? UserId, Guid? ApiKeyId, int RateLimitPerMinute);
