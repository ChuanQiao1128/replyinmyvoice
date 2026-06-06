using System.Globalization;
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
    private const int DefaultFreeBaselineRewrites = 0;

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

    public async Task<AppUser?> FindUserAsync(
        string externalAuthUserId,
        CancellationToken cancellationToken)
    {
        var normalizedExternalId = NormalizeExternalAuthUserId(externalAuthUserId);

        await using var db = dbContextFactory();
        return await db.AppUsers.AsNoTracking().SingleOrDefaultAsync(
            x => x.ExternalAuthUserId == normalizedExternalId,
            cancellationToken);
    }

    public async Task<AccountSummary> GetOrCreateAccountSummaryAsync(
        string externalAuthUserId,
        string? email,
        CancellationToken cancellationToken)
    {
        var user = await GetOrCreateUserAsync(externalAuthUserId, email, cancellationToken);
        await using var db = dbContextFactory();
        var usagePlan = GetUsagePlan(user, configuration);
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
        var creditUsed = activeCredits
            .Sum(x => Math.Max(x.AmountConsumed, 0));
        var activePromoCredits = activeCredits
            .Where(x => x.Source == "PROMO")
            .ToList();
        var trialRemaining = activePromoCredits
            .Sum(x => Math.Max(x.AmountGranted - x.AmountConsumed, 0));
        var trialExpiresAt = activePromoCredits
            .Where(x => x.ExpiresAt is not null && x.AmountGranted - x.AmountConsumed > 0)
            .Select(x => x.ExpiresAt!.Value)
            .OrderBy(x => x)
            .Cast<DateTimeOffset?>()
            .FirstOrDefault();
        var hasRedeemedPromo = await db.PromoCodeRedemptions
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == user.Id && x.Status == PromoCodeRedemptionStatus.Applied,
                cancellationToken);
        var promoCodes = await db.PromoCodes
            .AsNoTracking()
            .Select(x => new
            {
                x.IsActive,
                x.ValidFrom,
                x.ValidUntil,
                x.MaxRedemptionsGlobal,
                x.RedemptionCount,
            })
            .ToListAsync(cancellationToken);
        var hasRedeemableCode = promoCodes.Any(x =>
            x.IsActive &&
            x.ValidFrom <= now &&
            now <= x.ValidUntil &&
            (x.MaxRedemptionsGlobal is null || x.RedemptionCount < x.MaxRedemptionsGlobal));
        var remaining = periodRemaining + creditRemaining;
        var totalUsed = used + creditUsed;
        var quota = totalUsed + reserved + remaining;
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
                LabelForCreditSource(x.Source),
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
            user.PaymentGraceEndsAt,
            new AccountUsageSummary(
                usagePlan.Scope,
                usagePlan.PeriodKey,
                quota,
                totalUsed,
                reserved,
                remaining,
                remaining <= 0)
            {
                Sources = sources,
            },
            new AccountPromoSummary(
                hasRedeemedPromo,
                !hasRedeemedPromo && hasRedeemableCode,
                trialRemaining,
                trialExpiresAt));
    }

    public async Task<IReadOnlyList<AccountPayment>> GetPurchaseHistoryAsync(
        string externalAuthUserId,
        string? email,
        CancellationToken cancellationToken)
    {
        var user = await GetOrCreateUserAsync(externalAuthUserId, email, cancellationToken);
        await using var db = dbContextFactory();
        var purchases = await db.RewriteCredits
            .AsNoTracking()
            .Where(x => x.UserId == user.Id && x.Source == "PURCHASE")
            .Select(x => new
            {
                x.StripeSku,
                x.StripePaymentIntentId,
                x.StripeAmountTotal,
                x.StripeCurrency,
                x.StripeReceiptUrl,
                x.GrantedAt,
                x.ExpiresAt,
                x.AmountGranted,
                x.AmountConsumed,
            })
            .ToListAsync(cancellationToken);

        return purchases
            .OrderByDescending(x => x.GrantedAt)
            .Select(x => new AccountPayment(
                x.StripeSku,
                x.StripePaymentIntentId,
                x.StripeAmountTotal,
                x.StripeCurrency,
                x.StripeReceiptUrl,
                x.GrantedAt,
                x.ExpiresAt,
                Math.Max(x.AmountGranted - x.AmountConsumed, 0)))
            .ToList();
    }

    public async Task<bool> HasPaidApiEntitlementAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var db = dbContextFactory();
        var subscriptionStatus = await db.AppUsers
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => (SubscriptionStatus?)x.SubscriptionStatus)
            .SingleOrDefaultAsync(cancellationToken);

        if (subscriptionStatus is null)
        {
            return false;
        }

        if (IsPaidApiSubscriptionStatus(subscriptionStatus.Value))
        {
            return true;
        }

        var purchasedCredits = await db.RewriteCredits
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Source == "PURCHASE")
            .Select(x => new { x.AmountGranted, x.AmountConsumed, x.ExpiresAt })
            .ToListAsync(cancellationToken);

        return purchasedCredits.Any(x =>
            (x.ExpiresAt is null || x.ExpiresAt > now) &&
            x.AmountGranted - x.AmountConsumed > 0);
    }

    public async Task<IReadOnlyList<AccountBillingHistoryItem>> GetBillingHistoryAsync(
        string externalAuthUserId,
        string? email,
        CancellationToken cancellationToken)
    {
        var purchases = await GetPurchaseHistoryAsync(externalAuthUserId, email, cancellationToken);
        var user = await FindUserAsync(externalAuthUserId, cancellationToken);
        if (user is null)
        {
            return Array.Empty<AccountBillingHistoryItem>();
        }

        await using var db = dbContextFactory();
        var invoices = await db.StripeInvoices
            .AsNoTracking()
            .Where(x => x.UserId == user.Id)
            .Select(x => new
            {
                x.Status,
                x.AmountDue,
                x.AmountPaid,
                x.Currency,
                x.PeriodStart,
                x.PeriodEnd,
                x.HostedInvoiceUrl,
                x.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var refundedCredits = await db.RewriteCredits
            .AsNoTracking()
            .Where(x =>
                x.UserId == user.Id &&
                x.Source == "PURCHASE" &&
                x.OriginalAmountGranted != null &&
                x.OriginalAmountGranted > x.AmountGranted)
            .Select(x => new
            {
                x.StripeSku,
                x.StripeAmountTotal,
                x.StripeCurrency,
                x.GrantedAt,
                x.AmountGranted,
                x.OriginalAmountGranted,
            })
            .ToListAsync(cancellationToken);

        var history = new List<AccountBillingHistoryItem>();

        history.AddRange(purchases.Select(x => new AccountBillingHistoryItem(
            "pack",
            x.Date,
            string.IsNullOrWhiteSpace(x.Sku) ? "Credit pack" : x.Sku!,
            x.Amount,
            x.Currency,
            "paid",
            x.ReceiptUrl,
            null)));

        history.AddRange(invoices.Select(x => new AccountBillingHistoryItem(
            "subscription",
            x.CreatedAt,
            FormatSubscriptionInvoiceDescription(x.PeriodStart, x.PeriodEnd),
            x.AmountPaid > 0 ? x.AmountPaid : x.AmountDue,
            x.Currency,
            x.Status,
            null,
            x.HostedInvoiceUrl)));

        history.AddRange(refundedCredits.Select(x => new AccountBillingHistoryItem(
            "refund",
            x.GrantedAt,
            FormatRefundDescription(x.StripeSku),
            CalculateRefundAmount(x.StripeAmountTotal, x.OriginalAmountGranted, x.AmountGranted),
            x.StripeCurrency,
            "refunded",
            null,
            null)));

        return history
            .OrderByDescending(x => x.Date)
            .ThenBy(x => x.Type, StringComparer.Ordinal)
            .ToList();
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

        await using var strategyDb = dbContextFactory();
        var strategy = strategyDb.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
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
            var promoRedemptions = await db.PromoCodeRedemptions
                .AsTracking()
                .Where(x => x.UserId == user.Id)
                .ToListAsync(cancellationToken);
            var billingSupportRequests = await db.BillingSupportRequests
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
                credit.OriginalAmountGranted = null;
                credit.AmountConsumed = 0;
                credit.ExpiresAt = now;
                credit.RowVersion = Guid.NewGuid();
            }

            foreach (var redemption in promoRedemptions)
            {
                redemption.RedeemIpHash = null;
                redemption.RowVersion = Guid.NewGuid();
            }

            foreach (var billingSupportRequest in billingSupportRequests)
            {
                billingSupportRequest.RelatedPaymentIntentId = null;
                billingSupportRequest.Message = "erased";
                billingSupportRequest.Status = BillingSupportRequestStatus.Resolved;
                billingSupportRequest.ResolvedAt ??= now;
                billingSupportRequest.UpdatedAt = now;
                billingSupportRequest.RowVersion = Guid.NewGuid();
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    public static AccountUsagePlan GetUsagePlan(AppUser user, IConfiguration? configuration = null)
    {
        if (user.SubscriptionStatus is SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.Testing or SubscriptionStatus.PastDue)
        {
            return new AccountUsagePlan(
                "paid",
                $"paid:{user.StripeSubscriptionId ?? user.Id.ToString()}:{user.CurrentPeriodEnd?.ToString("O") ?? "no-period"}",
                user.SubscriptionStatus == SubscriptionStatus.Testing ? 10_000 : 90);
        }

        return new AccountUsagePlan(
            "free",
            "free:lifetime",
            ResolveFreeBaselineRewrites(configuration));
    }

    private static bool IsPaidApiSubscriptionStatus(SubscriptionStatus subscriptionStatus) =>
        subscriptionStatus is SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.Testing;

    private static int ResolveFreeBaselineRewrites(IConfiguration? configuration)
    {
        var configuredValue = configuration?["FREE_BASELINE_REWRITES"];
        return int.TryParse(configuredValue, out var parsed) && parsed >= 0
            ? parsed
            : DefaultFreeBaselineRewrites;
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

    public static bool IsErasedExternalAuthUserId(string externalAuthUserId) =>
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

    private static string LabelForCreditSource(string source) =>
        source == "PROMO" ? "Trial rewrites" : source;

    private static string FormatSubscriptionInvoiceDescription(DateTimeOffset? periodStart, DateTimeOffset? periodEnd)
    {
        if (periodStart is not null && periodEnd is not null)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Subscription invoice {periodStart.Value:yyyy-MM-dd} - {periodEnd.Value:yyyy-MM-dd}");
        }

        return "Subscription invoice";
    }

    private static string FormatRefundDescription(string? sku) =>
        string.IsNullOrWhiteSpace(sku) ? "Pack refund" : $"Refund for {sku}";

    private static long? CalculateRefundAmount(long? originalAmount, int? originalGranted, int currentGranted)
    {
        if (originalAmount is not > 0 || originalGranted is not > 0 || currentGranted >= originalGranted.Value)
        {
            return null;
        }

        var refundedCredits = originalGranted.Value - currentGranted;
        var amount = Math.Round(
            originalAmount.Value * refundedCredits / (decimal)originalGranted.Value,
            MidpointRounding.AwayFromZero);
        return -(long)amount;
    }
}

internal sealed record DeleteAccountLookup(string? StripeSubscriptionId);

public sealed record AccountSummary(
    Guid Id,
    string ExternalAuthUserId,
    string? Email,
    string SubscriptionStatus,
    DateTimeOffset? PaymentGraceEndsAt,
    AccountUsageSummary Usage,
    AccountPromoSummary Promo);

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

public sealed record AccountPromoSummary(
    bool HasRedeemed,
    bool Eligible,
    int TrialRemaining,
    DateTimeOffset? TrialExpiresAt);

public sealed record AccountPayment(
    string? Sku,
    string? PaymentIntentId,
    long? Amount,
    string? Currency,
    string? ReceiptUrl,
    DateTimeOffset Date,
    DateTimeOffset? Expiry,
    int Remaining);

public sealed record AccountBillingHistoryItem(
    string Type,
    DateTimeOffset Date,
    string Description,
    long? Amount,
    string? Currency,
    string Status,
    string? ReceiptUrl,
    string? HostedInvoiceUrl);

public sealed record AccountUsagePlan(string Scope, string PeriodKey, int QuotaLimit);
