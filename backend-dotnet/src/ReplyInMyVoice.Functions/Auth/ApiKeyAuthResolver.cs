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
        var token = ResolveBearerToken(request);
        if (string.IsNullOrWhiteSpace(token) ||
            !token.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var keyHash = ApiKeyService.ComputeHash(token);
        var apiKey = await db.ApiKeys
            .SingleOrDefaultAsync(x => x.KeyHash == keyHash, cancellationToken);

        if (apiKey is null ||
            apiKey.RevokedAt is not null ||
            (apiKey.ExpiresAt is not null && apiKey.ExpiresAt <= now))
        {
            return null;
        }

        apiKey.LastUsedAt = now;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // This timestamp is best-effort and must not block an otherwise valid key.
        }

        return apiKey.UserId;
    }

    private static string? ResolveBearerToken(HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..].Trim()
            : null;
    }
}
