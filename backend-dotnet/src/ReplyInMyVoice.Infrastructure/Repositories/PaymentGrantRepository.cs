using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class PaymentGrantRepository(AppDbContext db) : IPaymentGrantRepository
{
    private const string PurchaseSource = "PURCHASE";

    public async Task<IReadOnlyList<PaymentGrantSnapshot>> ListPurchaseGrantsForReconciliationAsync(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        IReadOnlyCollection<string> paymentIntentIds,
        CancellationToken ct = default)
    {
        var normalizedPaymentIntentIds = paymentIntentIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (db.Database.IsSqlite())
        {
            var rows = await db.RewriteCredits
                .AsNoTracking()
                .Where(x => x.Source == PurchaseSource)
                .Select(x => new PaymentGrantSnapshot(
                    x.Id,
                    x.StripePaymentIntentId,
                    x.StripeAmountTotal,
                    x.StripeCurrency,
                    x.GrantedAt))
                .ToListAsync(ct);

            return rows
                .Where(x =>
                    (x.GrantedAt >= windowStart && x.GrantedAt < windowEnd) ||
                    (!string.IsNullOrWhiteSpace(x.PaymentIntentId) &&
                        normalizedPaymentIntentIds.Contains(x.PaymentIntentId)))
                .ToList();
        }

        IQueryable<RewriteCredit> query = db.RewriteCredits
            .AsNoTracking()
            .Where(x => x.Source == PurchaseSource);

        query = normalizedPaymentIntentIds.Length == 0
            ? query.Where(x => x.GrantedAt >= windowStart && x.GrantedAt < windowEnd)
            : query.Where(x =>
                (x.GrantedAt >= windowStart && x.GrantedAt < windowEnd) ||
                (x.StripePaymentIntentId != null && normalizedPaymentIntentIds.Contains(x.StripePaymentIntentId)));

        return await query
            .Select(x => new PaymentGrantSnapshot(
                x.Id,
                x.StripePaymentIntentId,
                x.StripeAmountTotal,
                x.StripeCurrency,
                x.GrantedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SubscriptionUserSnapshot>> ListSubscriptionUsersForReconciliationAsync(
        CancellationToken ct = default) =>
        await db.AppUsers
            .AsNoTracking()
            .Where(x =>
                x.SubscriptionStatus != SubscriptionStatus.Testing &&
                (x.StripeSubscriptionId != null ||
                    x.SubscriptionStatus == SubscriptionStatus.Active ||
                    x.SubscriptionStatus == SubscriptionStatus.Trialing ||
                    x.SubscriptionStatus == SubscriptionStatus.PastDue))
            .Select(x => new SubscriptionUserSnapshot(
                x.Id,
                x.StripeCustomerId,
                x.StripeSubscriptionId,
                x.SubscriptionStatus))
            .ToListAsync(ct);
}
