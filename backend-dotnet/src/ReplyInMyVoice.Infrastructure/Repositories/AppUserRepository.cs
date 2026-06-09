using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class AppUserRepository(AppDbContext db) : IAppUserRepository
{
    public async Task AddAsync(AppUser user, CancellationToken ct = default)
    {
        await db.AppUsers.AddAsync(user, ct);
    }

    public async Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.AppUsers
            .AsTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<AppUser?> GetByExternalAuthUserIdAsync(string externalAuthUserId, CancellationToken ct = default) =>
        await db.AppUsers
            .AsTracking()
            .SingleOrDefaultAsync(x => x.ExternalAuthUserId == externalAuthUserId, ct);

    public async Task<AppUser?> GetByStripeCustomerIdAsync(
        string stripeCustomerId,
        CancellationToken ct = default) =>
        await db.AppUsers
            .AsTracking()
            .SingleOrDefaultAsync(x => x.StripeCustomerId == stripeCustomerId, ct);

    public async Task<AppUser?> FindForStripeCheckoutAsync(
        string? externalAuthUserId,
        string? stripeCustomerId,
        CancellationToken ct = default) =>
        await db.AppUsers
            .AsTracking()
            .SingleOrDefaultAsync(
                x => (!string.IsNullOrWhiteSpace(externalAuthUserId) && x.ExternalAuthUserId == externalAuthUserId) ||
                    (!string.IsNullOrWhiteSpace(stripeCustomerId) && x.StripeCustomerId == stripeCustomerId),
                ct);

    public async Task<AppUser?> FindByStripeCustomerOrSubscriptionAsync(
        string? stripeCustomerId,
        string? stripeSubscriptionId,
        CancellationToken ct = default) =>
        await db.AppUsers
            .AsTracking()
            .SingleOrDefaultAsync(
                x => (!string.IsNullOrWhiteSpace(stripeCustomerId) && x.StripeCustomerId == stripeCustomerId) ||
                    (!string.IsNullOrWhiteSpace(stripeSubscriptionId) && x.StripeSubscriptionId == stripeSubscriptionId),
                ct);

    public async Task<IReadOnlyList<AppUser>> ListExpiredPaymentGraceBatchAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken ct = default)
    {
        var graceUsers = await db.AppUsers
            .AsTracking()
            .Where(x => x.SubscriptionStatus == SubscriptionStatus.PastDue &&
                x.PaymentGraceEndsAt != null)
            .ToListAsync(ct);

        return graceUsers
            .Where(x => x.PaymentGraceEndsAt <= now)
            .OrderBy(x => x.PaymentGraceEndsAt)
            .ThenBy(x => x.Id)
            .Take(batchSize)
            .ToList();
    }

    public async Task<IReadOnlyList<AppUser>> ListPaymentGraceReminderCandidatesBatchAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken ct = default)
    {
        var graceUsers = await db.AppUsers
            .AsTracking()
            .Where(x => x.SubscriptionStatus == SubscriptionStatus.PastDue &&
                x.PaymentGraceEndsAt != null &&
                x.PaymentGraceReminderSentAt == null)
            .ToListAsync(ct);

        return graceUsers
            .Where(x => ShouldSendPaymentGraceReminder(x, now))
            .OrderBy(x => x.PaymentGraceEndsAt)
            .ThenBy(x => x.Id)
            .Take(batchSize)
            .ToList();
    }

    private static bool ShouldSendPaymentGraceReminder(AppUser user, DateTimeOffset now)
    {
        if (user.PaymentGraceEndsAt is null || user.PaymentGraceEndsAt <= now)
        {
            return false;
        }

        var reminderAt = user.PaymentFailedAt is { } failedAt && failedAt < user.PaymentGraceEndsAt
            ? failedAt.AddDays(5)
            : user.PaymentGraceEndsAt.Value.AddDays(-2);

        return reminderAt <= now;
    }
}
