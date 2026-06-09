using System.Numerics;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class ApiKeyService
{
    private const string LiveKeyPrefix = "rmv_live_";
    private const string TestKeyPrefix = "rmv_test_";
    private const string Base62Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private readonly Func<AppDbContext> _dbContextFactory;

    public ApiKeyService(
        Func<AppDbContext> dbContextFactory,
        ILogger<ApiKeyService>? logger = null)
    {
        _dbContextFactory = dbContextFactory;
        if (logger is not null)
        {
            ApiKeyHashing.UseLogger(logger);
        }
    }

    public async Task<(Guid Id, string Plaintext, DateTimeOffset CreatedAt)> GenerateAsync(
        Guid userId,
        string name,
        CancellationToken cancellationToken,
        bool isTest = false)
    {
        var plaintext = GeneratePlaintext(isTest);
        var now = DateTimeOffset.UtcNow;
        var apiKey = new ApiKey
        {
            UserId = userId,
            Name = name,
            KeyHash = ApiKeyHashing.ComputeHash(plaintext),
            Last4 = plaintext[^4..],
            IsTest = isTest,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await using var db = _dbContextFactory();
        db.ApiKeys.Add(apiKey);
        await db.SaveChangesAsync(cancellationToken);
        return (apiKey.Id, plaintext, apiKey.CreatedAt);
    }

    public async Task<IReadOnlyList<ApiKeySummary>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();
        var now = DateTimeOffset.UtcNow;
        var usageStart = now.AddDays(-30);
        var rows = await db.ApiKeys
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Last4,
                x.IsTest,
                x.LastUsedAt,
                x.CreatedAt,
                x.RevokedAt,
                x.WebhookUrl,
            })
            .ToListAsync(cancellationToken);
        var keyIds = rows.Select(x => x.Id).ToArray();
        var usageByKeyId = new Dictionary<Guid, ApiUsageCount>();
        if (keyIds.Length > 0)
        {
            var usageQuery = db.ApiKeyUsages
                .AsNoTracking()
                .Where(x => keyIds.Contains(x.ApiKeyId));
            var filtersDatesInMemory = string.Equals(
                db.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.Sqlite",
                StringComparison.OrdinalIgnoreCase);
            if (!filtersDatesInMemory)
            {
                usageQuery = usageQuery.Where(x => x.CreatedAt >= usageStart && x.CreatedAt <= now);
            }

            var usageRows = await usageQuery
                .Select(x => new
                {
                    x.ApiKeyId,
                    x.CreatedAt,
                    x.StatusCode,
                })
                .ToListAsync(cancellationToken);
            var countedRows = filtersDatesInMemory
                ? usageRows.Where(x => x.CreatedAt >= usageStart && x.CreatedAt <= now)
                : usageRows;
            usageByKeyId = countedRows
                .GroupBy(x => x.ApiKeyId)
                .ToDictionary(
                    x => x.Key,
                    x =>
                    {
                        var calls = x.Count();
                        var succeeded = x.Count(row => row.StatusCode is 200 or 202);
                        return new ApiUsageCount(calls, succeeded, calls - succeeded);
                    });
        }

        return rows
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ApiKeySummary(
                x.Id,
                x.Name,
                MaskKey(x.Last4, x.IsTest),
                x.IsTest,
                x.LastUsedAt,
                x.CreatedAt,
                x.RevokedAt,
                x.WebhookUrl,
                usageByKeyId.GetValueOrDefault(x.Id, new ApiUsageCount(0, 0, 0))))
            .ToList();
    }

    public async Task<ApiKeyRotationResult?> RotateAsync(
        Guid userId,
        Guid keyId,
        CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();
        var apiKey = await db.ApiKeys
            .AsTracking()
            .SingleOrDefaultAsync(
                x => x.Id == keyId && x.UserId == userId,
                cancellationToken);

        if (apiKey is null || apiKey.RevokedAt is not null)
        {
            return null;
        }

        var plaintext = GeneratePlaintext(apiKey.IsTest);
        var now = DateTimeOffset.UtcNow;
        apiKey.RevokedAt = now;
        apiKey.UpdatedAt = now;
        apiKey.RowVersion = Guid.NewGuid();

        var replacement = new ApiKey
        {
            UserId = userId,
            Name = apiKey.Name,
            KeyHash = ApiKeyHashing.ComputeHash(plaintext),
            Last4 = plaintext[^4..],
            IsTest = apiKey.IsTest,
            PlanTier = apiKey.PlanTier,
            Scope = apiKey.Scope,
            RateLimitPerMinute = apiKey.RateLimitPerMinute,
            MonthlyQuota = apiKey.MonthlyQuota,
            WebhookUrl = apiKey.WebhookUrl,
            WebhookSecret = apiKey.WebhookSecret,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.ApiKeys.Add(replacement);
        await db.SaveChangesAsync(cancellationToken);
        return new ApiKeyRotationResult(
            replacement.Id,
            replacement.Name,
            plaintext,
            replacement.CreatedAt,
            replacement.IsTest);
    }

    public async Task<bool> RevokeAsync(
        Guid userId,
        Guid keyId,
        CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();
        var apiKey = await db.ApiKeys
            .AsTracking()
            .SingleOrDefaultAsync(
                x => x.Id == keyId && x.UserId == userId,
                cancellationToken);

        if (apiKey is null)
        {
            return false;
        }

        if (apiKey.RevokedAt is null)
        {
            apiKey.RevokedAt = DateTimeOffset.UtcNow;
            apiKey.UpdatedAt = apiKey.RevokedAt.Value;
            apiKey.RowVersion = Guid.NewGuid();
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<ApiKeyWebhookResult?> SetWebhookAsync(
        Guid userId,
        Guid keyId,
        string webhookUrl,
        CancellationToken cancellationToken)
    {
        if (!ApiKeyWebhookUrl.TryNormalizeWebhookUrl(webhookUrl, out var normalizedUrl))
        {
            throw new ArgumentException("Webhook URL must be an absolute HTTPS URL that resolves to a public address.", nameof(webhookUrl));
        }

        await using var db = _dbContextFactory();
        var apiKey = await db.ApiKeys
            .AsTracking()
            .SingleOrDefaultAsync(
                x => x.Id == keyId && x.UserId == userId,
                cancellationToken);

        if (apiKey is null || apiKey.RevokedAt is not null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var webhookSecret = GenerateWebhookSecret();
        apiKey.WebhookUrl = normalizedUrl;
        apiKey.WebhookSecret = webhookSecret;
        apiKey.UpdatedAt = now;
        apiKey.RowVersion = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);

        return new ApiKeyWebhookResult(apiKey.Id, normalizedUrl, webhookSecret);
    }

    public async Task<bool> ClearWebhookAsync(
        Guid userId,
        Guid keyId,
        CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();
        var apiKey = await db.ApiKeys
            .AsTracking()
            .SingleOrDefaultAsync(
                x => x.Id == keyId && x.UserId == userId,
                cancellationToken);

        if (apiKey is null)
        {
            return false;
        }

        if (apiKey.WebhookUrl is not null || apiKey.WebhookSecret is not null)
        {
            apiKey.WebhookUrl = null;
            apiKey.WebhookSecret = null;
            apiKey.UpdatedAt = DateTimeOffset.UtcNow;
            apiKey.RowVersion = Guid.NewGuid();
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private static string GeneratePlaintext(bool isTest)
    {
        Span<byte> randomBytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        return (isTest ? TestKeyPrefix : LiveKeyPrefix) + ToBase62(randomBytes);
    }

    private static string GenerateWebhookSecret()
    {
        Span<byte> randomBytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        return Convert.ToHexString(randomBytes).ToLowerInvariant();
    }

    private static string ToBase62(ReadOnlySpan<byte> bytes)
    {
        var value = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        if (value.IsZero)
        {
            return "0";
        }

        Span<char> buffer = stackalloc char[64];
        var position = buffer.Length;
        while (value > BigInteger.Zero)
        {
            value = BigInteger.DivRem(value, 62, out var remainder);
            buffer[--position] = Base62Alphabet[(int)remainder];
        }

        return new string(buffer[position..]);
    }

    private static string MaskKey(string? last4, bool isTest) =>
        string.Concat(isTest ? TestKeyPrefix : LiveKeyPrefix, "\u2022\u2022\u2022\u2022", last4 ?? string.Empty);
}

public sealed record ApiKeySummary(
    Guid Id,
    string Name,
    string MaskedKey,
    bool IsTest,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt,
    string? WebhookUrl,
    ApiUsageCount Last30dUsage);

public sealed record ApiKeyRotationResult(
    Guid Id,
    string Name,
    string Plaintext,
    DateTimeOffset CreatedAt,
    bool IsTest);

public sealed record ApiKeyWebhookResult(
    Guid Id,
    string WebhookUrl,
    string WebhookSecret);
