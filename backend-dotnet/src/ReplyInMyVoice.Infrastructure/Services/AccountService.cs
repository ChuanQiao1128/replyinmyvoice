using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class AccountService(Func<AppDbContext> dbContextFactory)
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
        var remaining = Math.Max(usagePlan.QuotaLimit - used - reserved, 0);

        return new AccountSummary(
            user.Id,
            user.ExternalAuthUserId,
            user.Email,
            user.SubscriptionStatus.ToString(),
            new AccountUsageSummary(
                usagePlan.Scope,
                usagePlan.PeriodKey,
                usagePlan.QuotaLimit,
                used,
                reserved,
                remaining,
                remaining <= 0));
    }

    public static AccountUsagePlan GetUsagePlan(AppUser user)
    {
        if (user.SubscriptionStatus is SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.Testing)
        {
            return new AccountUsagePlan(
                "paid",
                $"paid:{user.StripeSubscriptionId ?? user.Id.ToString()}:{user.CurrentPeriodEnd?.ToString("O") ?? "no-period"}",
                user.SubscriptionStatus == SubscriptionStatus.Testing ? 10_000 : 40);
        }

        return new AccountUsagePlan("free", "free:lifetime", 3);
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
}

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
    bool Exhausted);

public sealed record AccountUsagePlan(string Scope, string PeriodKey, int QuotaLimit);
