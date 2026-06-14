using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class RewriteCreditRepository(AppDbContext db) : IRewriteCreditRepository
{
    public async Task AddAsync(RewriteCredit credit, CancellationToken ct = default)
    {
        await db.RewriteCredits.AddAsync(credit, ct);
    }

    public async Task<RewriteCredit?> GetByIdAsync(
        Guid creditId,
        CancellationToken ct = default) =>
        await db.RewriteCredits
            .AsTracking()
            .SingleOrDefaultAsync(x => x.Id == creditId, ct);

    public async Task<RewriteCredit?> GetUsableForReservationAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var userCredits = await db.RewriteCredits
            .AsTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        return UsableForReservation(userCredits, now).FirstOrDefault();
    }

    public async Task<IReadOnlyList<Guid>> ListUsableForReservationIdsAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var userCredits = await db.RewriteCredits
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        return UsableForReservation(userCredits, now)
            .Select(x => x.Id)
            .ToList();
    }

    public async Task<int> TryConsumeForReservationAsync(
        Guid creditId,
        CancellationToken ct = default)
    {
        var rowVersion = Guid.NewGuid();
        return await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE RewriteCredits
            SET AmountConsumed = AmountConsumed + 1,
                RowVersion = {rowVersion}
            WHERE Id = {creditId}
              AND AmountConsumed < AmountGranted
            """,
            ct);
    }

    public async Task<int> ReleaseConsumedAsync(
        Guid creditId,
        CancellationToken ct = default)
    {
        var rowVersion = Guid.NewGuid();
        return await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE RewriteCredits
            SET AmountConsumed = CASE WHEN AmountConsumed > 0 THEN AmountConsumed - 1 ELSE 0 END,
                RowVersion = {rowVersion}
            WHERE Id = {creditId}
            """,
            ct);
    }

    public async Task<IReadOnlyList<RewriteCredit>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default) =>
        await db.RewriteCredits
            .AsTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

    public async Task<RewriteCredit?> GetByUserIdAndPaymentIntentIdAsync(
        Guid userId,
        string paymentIntentId,
        CancellationToken ct = default) =>
        await db.RewriteCredits
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == userId && x.StripePaymentIntentId == paymentIntentId,
                ct);

    public async Task<AdminRefundPaymentLookupDto?> GetRefundPaymentLookupAsync(
        Guid userId,
        string paymentIntentId,
        CancellationToken ct = default) =>
        await db.RewriteCredits
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.StripePaymentIntentId == paymentIntentId)
            .Select(x => new AdminRefundPaymentLookupDto(
                x.StripePaymentIntentId,
                x.StripeAmountTotal,
                x.StripeCurrency))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<AdminAccountingRevenueRowDto>> ListAccountingRevenueRowsAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        int pageSize,
        CancellationToken ct = default)
    {
        var rows = new List<AdminAccountingRevenueRowDto>();
        var skip = 0;

        if (db.Database.IsSqlite())
        {
            while (true)
            {
                var page = await PaymentCredits(db.RewriteCredits)
                    .OrderBy(x => x.Id)
                    .Skip(skip)
                    .Take(pageSize)
                    .Select(x => new AccountingRevenueCreditRow(
                        x.Id,
                        x.UserId,
                        x.GrantedAt,
                        x.StripeSku,
                        x.StripeAmountTotal,
                        x.StripeCurrency,
                        x.StripePaymentIntentId,
                        x.AmountGranted,
                        x.AmountConsumed))
                    .ToListAsync(ct);

                if (page.Count == 0)
                {
                    return rows;
                }

                rows.AddRange(page
                    .Where(x => x.GrantedAt >= fromInclusive && x.GrantedAt < toExclusive)
                    .OrderBy(x => x.GrantedAt)
                    .ThenBy(x => x.CreditId)
                    .Select(ToAccountingRevenueRow));

                if (page.Count < pageSize)
                {
                    return rows;
                }

                skip += page.Count;
            }
        }

        while (true)
        {
            var page = await PaymentCredits(db.RewriteCredits)
                .Where(x => x.GrantedAt >= fromInclusive && x.GrantedAt < toExclusive)
                .OrderBy(x => x.GrantedAt)
                .ThenBy(x => x.Id)
                .Skip(skip)
                .Take(pageSize)
                .Select(x => new AccountingRevenueCreditRow(
                    x.Id,
                    x.UserId,
                    x.GrantedAt,
                    x.StripeSku,
                    x.StripeAmountTotal,
                    x.StripeCurrency,
                    x.StripePaymentIntentId,
                    x.AmountGranted,
                    x.AmountConsumed))
                .ToListAsync(ct);

            if (page.Count == 0)
            {
                return rows;
            }

            rows.AddRange(page.Select(ToAccountingRevenueRow));

            if (page.Count < pageSize)
            {
                return rows;
            }

            skip += page.Count;
        }
    }

    public async Task<bool> ExistsByStripeEventIdAsync(
        string stripeEventId,
        CancellationToken ct = default) =>
        await db.RewriteCredits
            .AsNoTracking()
            .AnyAsync(x => x.StripeEventId == stripeEventId, ct);

    public async Task<IReadOnlyList<RewriteCredit>> ListByStripePaymentIntentIdAsync(
        string paymentIntentId,
        CancellationToken ct = default) =>
        await db.RewriteCredits
            .AsTracking()
            .Where(x => x.StripePaymentIntentId == paymentIntentId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RewriteCredit>> ListPurchaseCreditsForTurnoverAsync(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default)
    {
        var rows = await db.RewriteCredits
            .AsNoTracking()
            .Where(x =>
                x.Source == "PURCHASE" &&
                x.StripeAmountTotal.HasValue)
            .ToListAsync(ct);

        return rows
            .Where(x =>
                x.StripeAmountTotal is > 0 &&
                x.GrantedAt >= windowStart &&
                x.GrantedAt <= windowEnd)
            .ToList();
    }

    public async Task<IReadOnlyList<RewriteCredit>> ListExpiryReminderCandidatesAsync(
        DateTimeOffset now,
        DateTimeOffset windowEnd,
        int batchSize,
        CancellationToken ct = default)
    {
        var query = db.RewriteCredits
            .AsTracking()
            .Include(x => x.User)
            .Where(x =>
                x.ExpiryReminderSentAt == null &&
                x.ExpiresAt != null &&
                x.AmountGranted > x.AmountConsumed);

        if (db.Database.IsSqlite())
        {
            var candidates = await query.ToListAsync(ct);
            return candidates
                .Where(x => x.ExpiresAt > now && x.ExpiresAt <= windowEnd)
                .OrderBy(x => x.ExpiresAt)
                .ThenBy(x => x.Id)
                .Take(batchSize)
                .ToList();
        }

        return await query
            .Where(x => x.ExpiresAt > now && x.ExpiresAt <= windowEnd)
            .OrderBy(x => x.ExpiresAt)
            .ThenBy(x => x.Id)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public Task MarkExpiryReminderSentAsync(
        RewriteCredit credit,
        DateTimeOffset sentAt,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        credit.ExpiryReminderSentAt = sentAt;
        return Task.CompletedTask;
    }

    public bool IsStripeEventIdWriteFailure(Exception exception)
    {
        var message = exception.ToString();
        return message.Contains("IX_RewriteCredits_StripeEventId", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("RewriteCredits.StripeEventId", StringComparison.OrdinalIgnoreCase) ||
            (message.Contains("RewriteCredits", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("StripeEventId", StringComparison.OrdinalIgnoreCase));
    }

    private static IQueryable<RewriteCredit> PaymentCredits(IQueryable<RewriteCredit> credits) =>
        credits
            .AsNoTracking()
            .Where(x =>
                (x.StripePaymentIntentId != null && x.StripePaymentIntentId != string.Empty) ||
                (x.StripeEventId != null && x.StripeEventId != string.Empty) ||
                (x.StripeSku != null && x.StripeSku != string.Empty) ||
                (x.StripeCurrency != null && x.StripeCurrency != string.Empty) ||
                x.StripeAmountTotal != null ||
                x.Source == "PURCHASE" ||
                x.Source == "Purchase" ||
                x.Source == "purchase");

    private static IEnumerable<RewriteCredit> UsableForReservation(
        IEnumerable<RewriteCredit> credits,
        DateTimeOffset now) =>
        credits
            .Where(x => (x.ExpiresAt == null || x.ExpiresAt > now) && x.AmountGranted - x.AmountConsumed > 0)
            .OrderBy(x => x.ExpiresAt.HasValue ? 0 : 1)
            .ThenBy(x => x.ExpiresAt ?? DateTimeOffset.MaxValue)
            .ThenBy(x => x.GrantedAt);

    private static AdminAccountingRevenueRowDto ToAccountingRevenueRow(AccountingRevenueCreditRow row) =>
        new(
            row.CreditId,
            row.UserId,
            row.GrantedAt,
            row.Sku,
            row.AmountTotal,
            row.Currency,
            row.PaymentIntentId,
            row.AmountGranted,
            row.AmountConsumed,
            Math.Max(row.AmountGranted - row.AmountConsumed, 0));

    private sealed record AccountingRevenueCreditRow(
        Guid CreditId,
        Guid UserId,
        DateTimeOffset GrantedAt,
        string? Sku,
        long? AmountTotal,
        string? Currency,
        string? PaymentIntentId,
        int AmountGranted,
        int AmountConsumed);
}
