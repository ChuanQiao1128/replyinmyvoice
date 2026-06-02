using System.Data;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class PromoService(
    Func<AppDbContext> dbContextFactory,
    IConfiguration? configuration = null,
    ILogger<PromoService>? logger = null)
{
    private const string PromoCreditSource = "PROMO";
    private const int DefaultIpVelocityMax24Hours = 5;
    private const int DefaultIpVelocityFlagFrom = 2;
    private const int SqliteBusyRetryCount = 8;

    public async Task<PromoRedeemResult> RedeemAsync(
        string externalAuthUserId,
        string? email,
        string rawCode,
        string? trustedClientIp,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(rawCode);
        if (normalizedCode is null)
        {
            return PromoRedeemResult.InvalidCode();
        }

        var ipDefense = ResolveIpDefense(trustedClientIp);
        if (ipDefense.ServerConfigError)
        {
            return PromoRedeemResult.ServerConfig();
        }

        var user = await GetOrCreateUserAsync(externalAuthUserId, email, cancellationToken);

        if (ipDefense.IpHash is { } ipHash)
        {
            var recentCount = await CountRecentIpRedemptionsAsync(ipHash, now, cancellationToken);
            var blockFrom = GetConfiguredPositiveInt("PROMO_IP_VELOCITY_MAX_24H", DefaultIpVelocityMax24Hours);
            var flagFrom = GetConfiguredPositiveInt("PROMO_IP_VELOCITY_FLAG_FROM", DefaultIpVelocityFlagFrom);

            if (recentCount >= blockFrom)
            {
                logger?.LogWarning(
                    "Promo IP velocity blocked for user {UserId} with {RecentRedemptionCount} recent applied redemptions.",
                    user.Id,
                    recentCount);
                return PromoRedeemResult.IpVelocityBlocked();
            }

            if (recentCount >= flagFrom)
            {
                logger?.LogWarning(
                    "Promo IP velocity flag for user {UserId} with {RecentRedemptionCount} recent applied redemptions.",
                    user.Id,
                    recentCount);
            }
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await ExecuteInTransactionAsync(
                    db => RedeemInTransactionAsync(db, user.Id, normalizedCode, ipDefense.IpHash, now, cancellationToken),
                    cancellationToken);
            }
            catch (DbUpdateException ex) when (IsPromoRedemptionUniqueConstraintViolation(ex))
            {
                return PromoRedeemResult.AlreadyRedeemed();
            }
            catch (Exception ex) when (IsSqliteBusy(ex) && attempt < SqliteBusyRetryCount)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }
    }

    public async Task<PromoStatusResult> GetStatusAsync(
        string externalAuthUserId,
        string? email,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var user = await GetOrCreateUserAsync(externalAuthUserId, email, cancellationToken);

        await using var db = dbContextFactory();
        var hasRedeemed = await db.PromoCodeRedemptions
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == user.Id && x.Status == PromoCodeRedemptionStatus.Applied,
                cancellationToken);

        var promoCredits = await db.RewriteCredits
            .AsNoTracking()
            .Where(x => x.UserId == user.Id && x.Source == PromoCreditSource)
            .Select(x => new { x.AmountGranted, x.AmountConsumed, x.ExpiresAt })
            .ToListAsync(cancellationToken);

        var activeCredits = promoCredits
            .Where(x => x.ExpiresAt is null || x.ExpiresAt > now)
            .ToList();
        var trialRemaining = activeCredits.Sum(x => Math.Max(x.AmountGranted - x.AmountConsumed, 0));
        var trialExpiresAt = activeCredits
            .Where(x => x.ExpiresAt is not null && x.AmountGranted - x.AmountConsumed > 0)
            .Select(x => x.ExpiresAt!.Value)
            .OrderBy(x => x)
            .Cast<DateTimeOffset?>()
            .FirstOrDefault();

        var promoCodes = await db.PromoCodes.AsNoTracking().ToListAsync(cancellationToken);
        var hasRedeemableCode = promoCodes.Any(x =>
            x.IsActive &&
            x.ValidFrom <= now &&
            now <= x.ValidUntil &&
            (x.MaxRedemptionsGlobal is null || x.RedemptionCount < x.MaxRedemptionsGlobal));

        return new PromoStatusResult(
            hasRedeemed,
            Eligible: !hasRedeemed && hasRedeemableCode,
            trialRemaining,
            trialExpiresAt);
    }

    private async Task<PromoRedeemResult> RedeemInTransactionAsync(
        AppDbContext db,
        Guid userId,
        string normalizedCode,
        string? ipHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var promoCode = await db.PromoCodes
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Code == normalizedCode, cancellationToken);

        if (promoCode is null || !promoCode.IsActive)
        {
            return PromoRedeemResult.InvalidCode();
        }

        if (now < promoCode.ValidFrom)
        {
            return PromoRedeemResult.InvalidCode();
        }

        if (now > promoCode.ValidUntil)
        {
            return PromoRedeemResult.Expired();
        }

        var alreadyRedeemed = await db.PromoCodeRedemptions
            .AsNoTracking()
            .AnyAsync(x => x.PromoCodeId == promoCode.Id && x.UserId == userId, cancellationToken);
        if (alreadyRedeemed)
        {
            return PromoRedeemResult.AlreadyRedeemed();
        }

        var rowsAffected = await IncrementRedemptionCountAsync(db, promoCode.Id, now, cancellationToken);
        if (rowsAffected == 0)
        {
            return await ResolveAtomicUpdateMissAsync(db, promoCode.Id, now, cancellationToken);
        }

        var expiresAt = now.AddDays(promoCode.GrantTtlDays);
        var credit = new RewriteCredit
        {
            UserId = userId,
            Source = PromoCreditSource,
            AmountGranted = promoCode.CreditsGranted,
            AmountConsumed = 0,
            GrantedAt = now,
            ExpiresAt = expiresAt,
        };
        db.RewriteCredits.Add(credit);

        db.PromoCodeRedemptions.Add(new PromoCodeRedemption
        {
            PromoCodeId = promoCode.Id,
            UserId = userId,
            RewriteCreditId = credit.Id,
            CreditsGranted = promoCode.CreditsGranted,
            CodeSnapshot = promoCode.Code,
            RedeemIpHash = ipHash,
            Status = PromoCodeRedemptionStatus.Applied,
            RedeemedAt = now,
        });

        await db.SaveChangesAsync(cancellationToken);
        return PromoRedeemResult.Success(promoCode.CreditsGranted, expiresAt, credit.Id);
    }

    private static async Task<int> IncrementRedemptionCountAsync(
        AppDbContext db,
        Guid promoCodeId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var rowVersion = Guid.NewGuid();
        return await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE PromoCodes
            SET RedemptionCount = RedemptionCount + 1,
                UpdatedAt = {now},
                RowVersion = {rowVersion}
            WHERE Id = {promoCodeId}
              AND IsActive = 1
              AND {now} BETWEEN ValidFrom AND ValidUntil
              AND (MaxRedemptionsGlobal IS NULL OR RedemptionCount < MaxRedemptionsGlobal)
            """,
            cancellationToken);
    }

    private static async Task<PromoRedeemResult> ResolveAtomicUpdateMissAsync(
        AppDbContext db,
        Guid promoCodeId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var current = await db.PromoCodes
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == promoCodeId, cancellationToken);

        if (current is null || !current.IsActive || now < current.ValidFrom)
        {
            return PromoRedeemResult.InvalidCode();
        }

        if (now > current.ValidUntil)
        {
            return PromoRedeemResult.Expired();
        }

        if (current.MaxRedemptionsGlobal is not null &&
            current.RedemptionCount >= current.MaxRedemptionsGlobal)
        {
            return PromoRedeemResult.CapReached();
        }

        return PromoRedeemResult.InvalidCode();
    }

    private async Task<int> CountRecentIpRedemptionsAsync(
        string ipHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var since = now.AddHours(-24);
        await using var db = dbContextFactory();
        var redeemedAtValues = await db.PromoCodeRedemptions
            .AsNoTracking()
            .Where(x => x.RedeemIpHash == ipHash && x.Status == PromoCodeRedemptionStatus.Applied)
            .Select(x => x.RedeemedAt)
            .ToListAsync(cancellationToken);

        return redeemedAtValues.Count(x => x > since);
    }

    private PromoIpDefense ResolveIpDefense(string? trustedClientIp)
    {
        var isProduction = IsProductionEnvironment();
        var proxySecret = configuration?["PROMO_PROXY_SHARED_SECRET"];
        if (isProduction && string.IsNullOrWhiteSpace(proxySecret))
        {
            return PromoIpDefense.ServerConfig();
        }

        var normalizedIp = NormalizeTrustedClientIp(trustedClientIp);
        if (normalizedIp is null)
        {
            return isProduction
                ? PromoIpDefense.ServerConfig()
                : PromoIpDefense.Skip();
        }

        var salt = configuration?["PROMO_IP_HASH_SALT"];
        if (string.IsNullOrWhiteSpace(salt))
        {
            return PromoIpDefense.ServerConfig();
        }

        return PromoIpDefense.WithHash(HashIp(normalizedIp, salt));
    }

    private bool IsProductionEnvironment()
    {
        var environmentName = configuration?["DOTNET_ENVIRONMENT"]
            ?? configuration?["ASPNETCORE_ENVIRONMENT"]
            ?? configuration?["AZURE_FUNCTIONS_ENVIRONMENT"];
        return string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);
    }

    private int GetConfiguredPositiveInt(string key, int defaultValue)
    {
        if (int.TryParse(configuration?[key], out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return defaultValue;
    }

    private static string? NormalizeTrustedClientIp(string? trustedClientIp)
    {
        var trimmed = trustedClientIp?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return IPAddress.TryParse(trimmed, out var parsedIp)
            ? parsedIp.ToString()
            : null;
    }

    private static string HashIp(string trustedClientIp, string salt)
    {
        var payload = Encoding.UTF8.GetBytes($"{salt}:{trustedClientIp}");
        var hash = SHA256.HashData(payload);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task<AppUser> GetOrCreateUserAsync(
        string externalAuthUserId,
        string? email,
        CancellationToken cancellationToken)
    {
        var normalizedExternalId = NormalizeExternalAuthUserId(externalAuthUserId);
        var normalizedEmail = NormalizeEmail(email);

        await using (var db = dbContextFactory())
        {
            var existing = await db.AppUsers
                .AsTracking()
                .SingleOrDefaultAsync(x => x.ExternalAuthUserId == normalizedExternalId, cancellationToken);
            if (existing is not null)
            {
                await UpdateEmailIfNeededAsync(db, existing, normalizedEmail, cancellationToken);
                return existing;
            }

            var user = new AppUser
            {
                ExternalAuthUserId = normalizedExternalId,
                Email = normalizedEmail,
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            db.AppUsers.Add(user);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return user;
            }
            catch (DbUpdateException ex) when (IsAppUserExternalIdUniqueConstraintViolation(ex))
            {
            }
        }

        await using var retryDb = dbContextFactory();
        var retryUser = await retryDb.AppUsers
            .AsTracking()
            .SingleAsync(x => x.ExternalAuthUserId == normalizedExternalId, cancellationToken);
        await UpdateEmailIfNeededAsync(retryDb, retryUser, normalizedEmail, cancellationToken);
        return retryUser;
    }

    private static async Task UpdateEmailIfNeededAsync(
        AppDbContext db,
        AppUser user,
        string? normalizedEmail,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(normalizedEmail) && user.Email != normalizedEmail)
        {
            user.Email = normalizedEmail;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            user.RowVersion = Guid.NewGuid();
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<AppDbContext, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await using var strategyDb = dbContextFactory();
        var strategy = strategyDb.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var db = dbContextFactory();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var result = await operation(db);

            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private static string NormalizeExternalAuthUserId(string externalAuthUserId)
    {
        var normalized = externalAuthUserId.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 160)
        {
            throw new ArgumentException("A valid external auth user id is required.", nameof(externalAuthUserId));
        }

        return normalized;
    }

    private static string? NormalizeEmail(string? email)
    {
        var normalized = email?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= 320 ? normalized : normalized[..320];
    }

    private static string? NormalizeCode(string rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
        {
            return null;
        }

        var builder = new StringBuilder(rawCode.Length);
        foreach (var ch in rawCode.Trim())
        {
            if (ch == '-' || char.IsWhiteSpace(ch))
            {
                continue;
            }

            var upper = char.ToUpperInvariant(ch);
            if (!IsAsciiLetterOrDigit(upper))
            {
                return null;
            }

            builder.Append(upper);
            if (builder.Length > 40)
            {
                return null;
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    private static bool IsAsciiLetterOrDigit(char ch) =>
        ch is >= 'A' and <= 'Z' or >= '0' and <= '9';

    private static bool IsPromoRedemptionUniqueConstraintViolation(DbUpdateException exception)
    {
        var message = exception.ToString();
        return message.Contains("IX_PromoCodeRedemptions_PromoCodeId_UserId", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("PromoCodeRedemptions.PromoCodeId", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("PromoCodeRedemptions.UserId", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAppUserExternalIdUniqueConstraintViolation(DbUpdateException exception)
    {
        var message = exception.ToString();
        return message.Contains("IX_AppUsers_ExternalAuthUserId", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("AppUsers.ExternalAuthUserId", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSqliteBusy(Exception exception)
    {
        if (exception is SqliteException sqliteException)
        {
            return sqliteException.SqliteErrorCode is 5 or 6;
        }

        if (exception.InnerException is not null && IsSqliteBusy(exception.InnerException))
        {
            return true;
        }

        return exception.ToString().Contains("database is locked", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record PromoIpDefense(bool ServerConfigError, string? IpHash)
    {
        public static PromoIpDefense ServerConfig() => new(true, null);

        public static PromoIpDefense Skip() => new(false, null);

        public static PromoIpDefense WithHash(string ipHash) => new(false, ipHash);
    }
}

public sealed record PromoRedeemResult(
    PromoRedeemResultKind Kind,
    int CreditsGranted = 0,
    DateTimeOffset? ExpiresAt = null,
    Guid? RewriteCreditId = null)
{
    public static PromoRedeemResult Success(
        int creditsGranted,
        DateTimeOffset expiresAt,
        Guid rewriteCreditId) =>
        new(PromoRedeemResultKind.Success, creditsGranted, expiresAt, rewriteCreditId);

    public static PromoRedeemResult InvalidCode() => new(PromoRedeemResultKind.InvalidCode);

    public static PromoRedeemResult Expired() => new(PromoRedeemResultKind.Expired);

    public static PromoRedeemResult AlreadyRedeemed() => new(PromoRedeemResultKind.AlreadyRedeemed);

    public static PromoRedeemResult CapReached() => new(PromoRedeemResultKind.CapReached);

    public static PromoRedeemResult IpVelocityBlocked() => new(PromoRedeemResultKind.IpVelocityBlocked);

    public static PromoRedeemResult ServerConfig() => new(PromoRedeemResultKind.ServerConfig);
}

public enum PromoRedeemResultKind
{
    Success,
    InvalidCode,
    Expired,
    AlreadyRedeemed,
    CapReached,
    IpVelocityBlocked,
    ServerConfig,
}

public sealed record PromoStatusResult(
    bool HasRedeemed,
    bool Eligible,
    int TrialRemaining,
    DateTimeOffset? TrialExpiresAt);
