using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class ApiKeyService
{
    private const string KeyPrefix = "rmv_live_";
    private const string Base62Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private static readonly string MaskPrefix = $"{KeyPrefix}\u2022\u2022\u2022\u2022";
    private static int s_missingPepperWarningLogged;
    private static ILogger<ApiKeyService>? s_logger;

    private readonly Func<AppDbContext> _dbContextFactory;

    public ApiKeyService(
        Func<AppDbContext> dbContextFactory,
        ILogger<ApiKeyService>? logger = null)
    {
        _dbContextFactory = dbContextFactory;
        if (logger is not null)
        {
            s_logger = logger;
        }
    }

    public static string ComputeHash(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var pepper = Environment.GetEnvironmentVariable("API_KEY_PEPPER");
        if (string.IsNullOrWhiteSpace(pepper) &&
            Interlocked.Exchange(ref s_missingPepperWarningLogged, 1) == 0)
        {
            const string warning = "API_KEY_PEPPER is not set; API key hashes are being computed without the configured pepper.";
            if (s_logger is not null)
            {
                s_logger.LogWarning("{Warning}", warning);
            }
            else
            {
                Trace.TraceWarning(warning);
            }
        }

        var material = string.IsNullOrWhiteSpace(pepper)
            ? plaintext
            : string.Concat(pepper, plaintext);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<(Guid Id, string Plaintext, DateTimeOffset CreatedAt)> GenerateAsync(
        Guid userId,
        string name,
        CancellationToken cancellationToken)
    {
        var plaintext = GeneratePlaintext();
        var now = DateTimeOffset.UtcNow;
        var apiKey = new ApiKey
        {
            UserId = userId,
            Name = name,
            KeyHash = ComputeHash(plaintext),
            Last4 = plaintext[^4..],
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
                x.LastUsedAt,
                x.CreatedAt,
                x.RevokedAt,
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
                MaskKey(x.Last4),
                x.LastUsedAt,
                x.CreatedAt,
                x.RevokedAt,
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

        var plaintext = GeneratePlaintext();
        var now = DateTimeOffset.UtcNow;
        apiKey.RevokedAt = now;
        apiKey.UpdatedAt = now;
        apiKey.RowVersion = Guid.NewGuid();

        var replacement = new ApiKey
        {
            UserId = userId,
            Name = apiKey.Name,
            KeyHash = ComputeHash(plaintext),
            Last4 = plaintext[^4..],
            PlanTier = apiKey.PlanTier,
            Scope = apiKey.Scope,
            RateLimitPerMinute = apiKey.RateLimitPerMinute,
            MonthlyQuota = apiKey.MonthlyQuota,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.ApiKeys.Add(replacement);
        await db.SaveChangesAsync(cancellationToken);
        return new ApiKeyRotationResult(
            replacement.Id,
            replacement.Name,
            plaintext,
            replacement.CreatedAt);
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

    private static string GeneratePlaintext()
    {
        Span<byte> randomBytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        return KeyPrefix + ToBase62(randomBytes);
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

    private static string MaskKey(string? last4) => string.Concat(MaskPrefix, last4 ?? string.Empty);
}

public sealed record ApiKeySummary(
    Guid Id,
    string Name,
    string MaskedKey,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt,
    ApiUsageCount Last30dUsage);

public sealed record ApiKeyRotationResult(
    Guid Id,
    string Name,
    string Plaintext,
    DateTimeOffset CreatedAt);
