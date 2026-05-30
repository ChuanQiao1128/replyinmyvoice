using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class AccountService(
    Func<AppDbContext> dbContextFactory,
    IConfiguration? configuration = null,
    Func<string, CancellationToken, Task>? cancelSubscriptionAsync = null)
{
    public async Task<AppUser> GetOrCreateUserAsync(
        string externalAuthUserId,
        string? email,
        CancellationToken cancellationToken)
    {
        var normalizedExternalId = NormalizeExternalAuthUserId(externalAuthUserId);
        var normalizedEmail = NormalizeEmail(email);

        await using var db = dbContextFactory();
        var user = await db.AppUsers.AsTracking().SingleOrDefaultAsync(
            x => x.ExternalAuthUserId == normalizedExternalId,
            cancellationToken);

        if (user is not null)
        {
            if (!string.IsNullOrWhiteSpace(normalizedEmail) && user.Email != normalizedEmail)
            {
                user.Email = normalizedEmail;
                user.UpdatedAt = DateTimeOffset.UtcNow;
                user.RowVersion = Guid.NewGuid();
                await db.SaveChangesAsync(cancellationToken);
            }

            return user;
        }

        user = new AppUser
        {
            ExternalAuthUserId = normalizedExternalId,
            Email = normalizedEmail,
            SubscriptionStatus = SubscriptionStatus.Inactive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<AccountSummary> GetOrCreateAccountSummaryAsync(
        string externalAuthUserId,
        string? email,
        CancellationToken cancellationToken)
    {
        var user = await GetOrCreateUserAsync(externalAuthUserId, email, cancellationToken);
        await using var db = dbContextFactory();
        var usagePlan = GetUsagePlan(user);
        var period = await db.UsagePeriods.AsNoTracking().SingleOrDefaultAsync(
            x => x.UserId == user.Id && x.PeriodKey == usagePlan.PeriodKey,
            cancellationToken);
        var used = period?.UsedCount ?? 0;
        var reserved = period?.ReservedCount ?? 0;
        var periodRemaining = Math.Max(usagePlan.QuotaLimit - used - reserved, 0);
        var now = DateTimeOffset.UtcNow;
        // Materialize by user id, then filter ExpiresAt in memory: SQLite (test DB)
        // cannot translate DateTimeOffset comparisons in SQL.
        var userCredits = await db.RewriteCredits
            .AsNoTracking()
            .Where(x => x.UserId == user.Id)
            .Select(x => new { x.Source, x.AmountGranted, x.AmountConsumed, x.ExpiresAt })
            .ToListAsync(cancellationToken);
        var activeCredits = userCredits
            .Where(x => x.ExpiresAt == null || x.ExpiresAt > now)
            .ToList();
        var creditRemaining = activeCredits
            .Sum(x => Math.Max(x.AmountGranted - x.AmountConsumed, 0));
        var remaining = periodRemaining + creditRemaining;
        var sources = new List<AccountUsageSource>
        {
            new(
                usagePlan.Scope,
                usagePlan.Scope == "paid" ? "Included rewrites" : "Free rewrites",
                used,
                usagePlan.QuotaLimit,
                reserved,
                periodRemaining,
                null,
                null),
        };
        sources.AddRange(activeCredits.Select(x =>
        {
            var sourceRemaining = Math.Max(x.AmountGranted - x.AmountConsumed, 0);
            return new AccountUsageSource(
                x.Source,
                x.Source,
                x.AmountConsumed,
                x.AmountGranted,
                0,
                sourceRemaining,
                x.ExpiresAt,
                CalculateExpiresInDays(x.ExpiresAt, now));
        }));

        return new AccountSummary(
            user.Id,
            user.ExternalAuthUserId,
            user.Email,
            user.SubscriptionStatus.ToString(),
            new AccountUsageSummary(
                usagePlan.Scope,
                usagePlan.PeriodKey,
                usagePlan.QuotaLimit + creditRemaining,
                used,
                reserved,
                remaining,
                remaining <= 0)
            {
                Sources = sources,
            });
    }

    public async Task DeleteAccountAsync(
        string externalAuthUserId,
        CancellationToken cancellationToken)
    {
        var normalizedExternalId = NormalizeExternalAuthUserId(externalAuthUserId);

        string? stripeSubscriptionId;
        await using (var lookupDb = dbContextFactory())
        {
            var account = await lookupDb.AppUsers
                .AsNoTracking()
                .Where(x => x.ExternalAuthUserId == normalizedExternalId)
                .Select(x => new DeleteAccountLookup(x.StripeSubscriptionId))
                .SingleOrDefaultAsync(cancellationToken);
            if (account is null)
            {
                return;
            }

            stripeSubscriptionId = account.StripeSubscriptionId;
        }

        if (!string.IsNullOrWhiteSpace(stripeSubscriptionId))
        {
            await CancelStripeSubscriptionAsync(stripeSubscriptionId, cancellationToken);
        }

        await using var db = dbContextFactory();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await db.AppUsers
            .AsTracking()
            .SingleOrDefaultAsync(x => x.ExternalAuthUserId == normalizedExternalId, cancellationToken);
        if (user is null || IsErasedExternalAuthUserId(user.ExternalAuthUserId))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var attempts = await db.RewriteAttempts
            .AsTracking()
            .Where(x => x.UserId == user.Id)
            .ToListAsync(cancellationToken);
        var usagePeriods = await db.UsagePeriods
            .AsTracking()
            .Where(x => x.UserId == user.Id)
            .ToListAsync(cancellationToken);
        var reservations = await db.UsageReservations
            .AsTracking()
            .Where(x => x.UserId == user.Id)
            .ToListAsync(cancellationToken);
        var credits = await db.RewriteCredits
            .AsTracking()
            .Where(x => x.UserId == user.Id)
            .ToListAsync(cancellationToken);

        user.ExternalAuthUserId = CreateErasedExternalAuthUserId(user.Id);
        user.Email = null;
        user.SubscriptionStatus = SubscriptionStatus.Canceled;
        user.CurrentPeriodEnd = null;
        user.UpdatedAt = now;
        user.RowVersion = Guid.NewGuid();

        foreach (var attempt in attempts)
        {
            attempt.IdempotencyKey = CreateErasedChildToken(attempt.Id);
            attempt.RequestHash = "erased";
            attempt.RequestJson = "{}";
            attempt.ResultJson = null;
            attempt.ErrorMessage = null;
            attempt.ExpiresAt = now;
            if (attempt.Status is RewriteAttemptStatus.Pending or RewriteAttemptStatus.Processing)
            {
                attempt.Status = RewriteAttemptStatus.Failed;
                attempt.ErrorCode = "account_erased";
                attempt.CompletedAt = now;
            }

            attempt.RowVersion = Guid.NewGuid();
        }

        foreach (var period in usagePeriods)
        {
            period.PeriodKey = CreateErasedChildToken(period.Id);
            period.QuotaLimit = 0;
            period.UsedCount = 0;
            period.ReservedCount = 0;
            period.PeriodStart = null;
            period.PeriodEnd = null;
            period.UpdatedAt = now;
            period.RowVersion = Guid.NewGuid();
        }

        foreach (var reservation in reservations)
        {
            reservation.Status = UsageReservationStatus.Released;
            reservation.FinalizedAt = null;
            reservation.ReleasedAt = now;
            reservation.ExpiresAt = now;
            reservation.RowVersion = Guid.NewGuid();
        }

        foreach (var credit in credits)
        {
            credit.Source = "ERASED";
            credit.AmountGranted = 0;
            credit.AmountConsumed = 0;
            credit.ExpiresAt = now;
            credit.RowVersion = Guid.NewGuid();
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public static AccountUsagePlan GetUsagePlan(AppUser user)
    {
        if (user.SubscriptionStatus is SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.Testing)
        {
            return new AccountUsagePlan(
                "paid",
                $"paid:{user.StripeSubscriptionId ?? user.Id.ToString()}:{user.CurrentPeriodEnd?.ToString("O") ?? "no-period"}",
                user.SubscriptionStatus == SubscriptionStatus.Testing ? 10_000 : 90);
        }

        return new AccountUsagePlan("free", "free:lifetime", 3);
    }

    private async Task CancelStripeSubscriptionAsync(
        string stripeSubscriptionId,
        CancellationToken cancellationToken)
    {
        if (cancelSubscriptionAsync is not null)
        {
            await cancelSubscriptionAsync(stripeSubscriptionId, cancellationToken);
            return;
        }

        if (configuration is null)
        {
            throw new InvalidOperationException("STRIPE_SECRET_KEY_missing");
        }

        var billingService = new StripeBillingService(dbContextFactory, configuration);
        await billingService.CancelSubscriptionAsync(stripeSubscriptionId, cancellationToken);
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

    private static bool IsErasedExternalAuthUserId(string externalAuthUserId) =>
        externalAuthUserId.StartsWith("erased:", StringComparison.Ordinal);

    private static string CreateErasedExternalAuthUserId(Guid userId) =>
        $"erased:{userId:N}";

    private static string CreateErasedChildToken(Guid id) =>
        $"erased:{id:N}";

    private static int? CalculateExpiresInDays(DateTimeOffset? expiresAt, DateTimeOffset now)
    {
        if (expiresAt is null)
        {
            return null;
        }

        return Math.Max(0, (int)Math.Ceiling((expiresAt.Value - now).TotalDays));
    }
}

internal sealed record DeleteAccountLookup(string? StripeSubscriptionId);

public sealed record AccountSummary(
    Guid Id,
    string ExternalAuthUserId,
    string? Email,
    string SubscriptionStatus,
    AccountUsageSummary Usage);

public sealed record AccountUsageSummary(
    string Scope,
    string PeriodKey,
    int Quota,
    int Used,
    int Reserved,
    int Remaining,
    bool Exhausted)
{
    public IReadOnlyList<AccountUsageSource> Sources { get; init; } = Array.Empty<AccountUsageSource>();
}

public sealed record AccountUsageSource(
    string Source,
    string Label,
    int Used,
    int Limit,
    int Reserved,
    int Remaining,
    DateTimeOffset? ExpiresAt,
    int? ExpiresInDays);

public sealed record AccountUsagePlan(string Scope, string PeriodKey, int QuotaLimit);
