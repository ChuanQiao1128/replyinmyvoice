using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Account;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests.Application;

public sealed class AccountUseCaseTests
{
    [Fact]
    public async Task GetOrCreateUserAsync_creates_normalized_user_and_find_returns_it()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await using var handlerDb = fixture.CreateContext();
        var getOrCreate = CreateGetOrCreateUserHandler(handlerDb);
        var find = new FindUserHandler(new AppUserRepository(handlerDb));

        var created = await getOrCreate.HandleAsync(new GetOrCreateUserCommand(
            "  entra-account-create  ",
            "  Casey@example.com  "));
        var found = await find.HandleAsync(new FindUserQuery(" entra-account-create "));

        created.ExternalAuthUserId.Should().Be("entra-account-create");
        created.Email.Should().Be("Casey@example.com");
        found.Should().NotBeNull();
        found!.Id.Should().Be(created.Id);

        var updated = await getOrCreate.HandleAsync(new GetOrCreateUserCommand(
            "entra-account-create",
            "casey-updated@example.com"));

        updated.Id.Should().Be(created.Id);
        updated.Email.Should().Be("casey-updated@example.com");

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.AppUsers.CountAsync()).Should().Be(1);
        (await verifyDb.AppUsers.SingleAsync()).Email.Should().Be("casey-updated@example.com");
    }

    [Fact]
    public async Task GetAccountSummaryAsync_reports_usage_credits_and_promo_state()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var promoCodeId = Guid.NewGuid();
        var creditId = Guid.NewGuid();
        var trialExpiresAt = now.AddDays(30);
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.AppUsers.Add(new AppUser
            {
                Id = userId,
                ExternalAuthUserId = "entra-summary",
                Email = "old-summary@example.com",
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now.AddDays(-1),
            });
            seedDb.UsagePeriods.Add(new UsagePeriod
            {
                UserId = userId,
                PeriodKey = "free:lifetime",
                QuotaLimit = 3,
                UsedCount = 1,
                ReservedCount = 1,
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now.AddDays(-1),
            });
            seedDb.PromoCodes.Add(new PromoCode
            {
                Id = promoCodeId,
                Code = "SUMMARYCHECK",
                DisplayCode = "SummaryCheck",
                Description = "Trial credits",
                Kind = PromoCodeKind.TrialCredits,
                CreditsGranted = 3,
                GrantTtlDays = 90,
                ValidFrom = now.AddDays(-1),
                ValidUntil = now.AddDays(30),
                MaxRedemptionsGlobal = 100,
                MaxRedemptionsPerUser = 1,
                RedemptionCount = 1,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            seedDb.RewriteCredits.Add(new RewriteCredit
            {
                Id = creditId,
                UserId = userId,
                Source = "PROMO",
                AmountGranted = 3,
                AmountConsumed = 1,
                GrantedAt = now.AddDays(-1),
                ExpiresAt = trialExpiresAt,
            });
            seedDb.RewriteCredits.Add(new RewriteCredit
            {
                UserId = userId,
                Source = "PURCHASE",
                AmountGranted = 10,
                AmountConsumed = 0,
                GrantedAt = now.AddDays(-10),
                ExpiresAt = now.AddDays(-1),
            });
            seedDb.PromoCodeRedemptions.Add(new PromoCodeRedemption
            {
                PromoCodeId = promoCodeId,
                UserId = userId,
                RewriteCreditId = creditId,
                CreditsGranted = 3,
                CodeSnapshot = "SUMMARYCHECK",
                Status = PromoCodeRedemptionStatus.Applied,
                RedeemedAt = now,
            });
            await seedDb.SaveChangesAsync();
        }

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateAccountSummaryHandler(handlerDb, freeBaselineRewrites: 3);

        var summary = await handler.HandleAsync(new GetAccountSummaryQuery(
            "entra-summary",
            "new-summary@example.com"));

        summary.Id.Should().Be(userId);
        summary.Email.Should().Be("new-summary@example.com");
        summary.Usage.Scope.Should().Be("free");
        summary.Usage.Quota.Should().Be(6);
        summary.Usage.Used.Should().Be(2);
        summary.Usage.Reserved.Should().Be(1);
        summary.Usage.Remaining.Should().Be(3);
        summary.Usage.Exhausted.Should().BeFalse();
        summary.Promo.HasRedeemed.Should().BeTrue();
        summary.Promo.Eligible.Should().BeFalse();
        summary.Promo.TrialRemaining.Should().Be(2);
        summary.Promo.TrialExpiresAt.Should().Be(trialExpiresAt);
        summary.Usage.Sources.Should().ContainSingle(x =>
            x.Source == "PROMO" &&
            x.Label == "Trial rewrites" &&
            x.Used == 1 &&
            x.Limit == 3 &&
            x.Remaining == 2);
    }

    [Fact]
    public async Task GetPurchaseHistoryAsync_returns_caller_purchases_ordered_by_date()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var callerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.AppUsers.AddRange(
                NewUser(callerId, "entra-purchase-caller", now),
                NewUser(otherUserId, "entra-purchase-other", now));
            seedDb.RewriteCredits.AddRange(
                new RewriteCredit
                {
                    UserId = callerId,
                    Source = "PURCHASE",
                    AmountGranted = 10,
                    AmountConsumed = 3,
                    GrantedAt = now.AddDays(-2),
                    ExpiresAt = now.AddDays(88),
                    StripePaymentIntentId = "pi_old",
                    StripeSku = "starter_pack",
                    StripeAmountTotal = 1500,
                    StripeCurrency = "nzd",
                    StripeReceiptUrl = "https://pay.stripe.test/receipts/starter",
                },
                new RewriteCredit
                {
                    UserId = callerId,
                    Source = "PURCHASE",
                    AmountGranted = 5,
                    AmountConsumed = 1,
                    GrantedAt = now,
                    ExpiresAt = now.AddDays(90),
                    StripePaymentIntentId = "pi_new",
                    StripeSku = "quick_pack",
                    StripeAmountTotal = 900,
                    StripeCurrency = "nzd",
                },
                new RewriteCredit
                {
                    UserId = callerId,
                    Source = "PROMO",
                    AmountGranted = 3,
                    AmountConsumed = 0,
                    GrantedAt = now.AddHours(1),
                },
                new RewriteCredit
                {
                    UserId = otherUserId,
                    Source = "PURCHASE",
                    AmountGranted = 30,
                    AmountConsumed = 0,
                    GrantedAt = now.AddHours(2),
                    StripeSku = "other_pack",
                    StripeAmountTotal = 3000,
                    StripeCurrency = "usd",
                });
            await seedDb.SaveChangesAsync();
        }

        await using var handlerDb = fixture.CreateContext();
        var handler = CreatePurchaseHistoryHandler(handlerDb);

        var payments = await handler.HandleAsync(new GetPurchaseHistoryQuery(
            "entra-purchase-caller",
            "buyer@example.com"));

        payments.Should().HaveCount(2);
        payments[0].Sku.Should().Be("quick_pack");
        payments[0].PaymentIntentId.Should().Be("pi_new");
        payments[0].Amount.Should().Be(900);
        payments[0].Remaining.Should().Be(4);
        payments[1].Sku.Should().Be("starter_pack");
        payments[1].ReceiptUrl.Should().Be("https://pay.stripe.test/receipts/starter");
        payments[1].Remaining.Should().Be(7);
    }

    [Fact]
    public async Task HasPaidApiEntitlementAsync_allows_paid_status_or_usable_purchase_credit_only()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var activeId = Guid.NewGuid();
        var purchaseId = Guid.NewGuid();
        var promoOnlyId = Guid.NewGuid();
        var expiredPurchaseId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.AppUsers.AddRange(
                NewUser(activeId, "entra-active-api", now, SubscriptionStatus.Active),
                NewUser(purchaseId, "entra-purchase-api", now),
                NewUser(promoOnlyId, "entra-promo-api", now),
                NewUser(expiredPurchaseId, "entra-expired-api", now));
            seedDb.RewriteCredits.AddRange(
                new RewriteCredit
                {
                    UserId = purchaseId,
                    Source = "PURCHASE",
                    AmountGranted = 3,
                    AmountConsumed = 2,
                    GrantedAt = now.AddDays(-1),
                    ExpiresAt = now.AddDays(1),
                },
                new RewriteCredit
                {
                    UserId = promoOnlyId,
                    Source = "PROMO",
                    AmountGranted = 3,
                    AmountConsumed = 0,
                    GrantedAt = now.AddDays(-1),
                    ExpiresAt = now.AddDays(1),
                },
                new RewriteCredit
                {
                    UserId = expiredPurchaseId,
                    Source = "PURCHASE",
                    AmountGranted = 3,
                    AmountConsumed = 0,
                    GrantedAt = now.AddDays(-10),
                    ExpiresAt = now.AddSeconds(-1),
                });
            await seedDb.SaveChangesAsync();
        }

        await using var handlerDb = fixture.CreateContext();
        var handler = new HasPaidApiEntitlementHandler(
            new AppUserRepository(handlerDb),
            new RewriteCreditRepository(handlerDb));

        (await handler.HandleAsync(new HasPaidApiEntitlementQuery(activeId, now))).Should().BeTrue();
        (await handler.HandleAsync(new HasPaidApiEntitlementQuery(purchaseId, now))).Should().BeTrue();
        (await handler.HandleAsync(new HasPaidApiEntitlementQuery(promoOnlyId, now))).Should().BeFalse();
        (await handler.HandleAsync(new HasPaidApiEntitlementQuery(expiredPurchaseId, now))).Should().BeFalse();
        (await handler.HandleAsync(new HasPaidApiEntitlementQuery(Guid.NewGuid(), now))).Should().BeFalse();
    }

    [Fact]
    public async Task GetBillingHistoryAsync_merges_packs_invoices_and_refunds_for_caller_only()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var callerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.AppUsers.AddRange(
                NewUser(callerId, "entra-billing-caller", now, SubscriptionStatus.Active, "sub_caller"),
                NewUser(otherUserId, "entra-billing-other", now, SubscriptionStatus.Active, "sub_other"));
            seedDb.RewriteCredits.AddRange(
                new RewriteCredit
                {
                    UserId = callerId,
                    Source = "PURCHASE",
                    AmountGranted = 5,
                    OriginalAmountGranted = 10,
                    AmountConsumed = 1,
                    GrantedAt = now.AddHours(10),
                    StripePaymentIntentId = "pi_refunded",
                    StripeSku = "quick_pack",
                    StripeAmountTotal = 1200,
                    StripeCurrency = "nzd",
                    StripeReceiptUrl = "https://pay.stripe.test/receipts/quick",
                },
                new RewriteCredit
                {
                    UserId = callerId,
                    Source = "PURCHASE",
                    AmountGranted = 20,
                    OriginalAmountGranted = 20,
                    AmountConsumed = 2,
                    GrantedAt = now.AddDays(-3),
                    StripePaymentIntentId = "pi_starter",
                    StripeSku = "starter_pack",
                    StripeAmountTotal = 1500,
                    StripeCurrency = "nzd",
                },
                new RewriteCredit
                {
                    UserId = otherUserId,
                    Source = "PURCHASE",
                    AmountGranted = 3,
                    OriginalAmountGranted = 10,
                    AmountConsumed = 0,
                    GrantedAt = now.AddHours(11),
                    StripeSku = "other_pack",
                    StripeAmountTotal = 900,
                    StripeCurrency = "usd",
                });
            seedDb.StripeInvoices.AddRange(
                new StripeInvoice
                {
                    Id = "in_caller_latest",
                    UserId = callerId,
                    SubscriptionId = "sub_caller",
                    Status = "paid",
                    AmountDue = 900,
                    AmountPaid = 900,
                    Currency = "nzd",
                    PeriodStart = DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
                    PeriodEnd = DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
                    HostedInvoiceUrl = "https://invoice.stripe.test/caller/latest",
                    CreatedAt = now.AddHours(12),
                    UpdatedAt = now.AddHours(12),
                },
                new StripeInvoice
                {
                    Id = "in_other_latest",
                    UserId = otherUserId,
                    SubscriptionId = "sub_other",
                    Status = "paid",
                    AmountDue = 1900,
                    AmountPaid = 1900,
                    Currency = "usd",
                    CreatedAt = now.AddHours(13),
                    UpdatedAt = now.AddHours(13),
                });
            await seedDb.SaveChangesAsync();
        }

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateBillingHistoryHandler(handlerDb);

        var history = await handler.HandleAsync(new GetBillingHistoryQuery(
            "entra-billing-caller",
            "caller@example.com"));

        history.Should().HaveCount(4);
        history.Select(x => x.Date).Should().BeInDescendingOrder();
        history.Should().ContainSingle(x =>
            x.Type == "subscription" &&
            x.Description == "Subscription invoice 2026-06-01 - 2026-07-01" &&
            x.Amount == 900 &&
            x.Currency == "nzd" &&
            x.Status == "paid" &&
            x.HostedInvoiceUrl == "https://invoice.stripe.test/caller/latest");
        history.Should().ContainSingle(x =>
            x.Type == "pack" &&
            x.Description == "quick_pack" &&
            x.Amount == 1200 &&
            x.ReceiptUrl == "https://pay.stripe.test/receipts/quick");
        history.Should().ContainSingle(x =>
            x.Type == "pack" &&
            x.Description == "starter_pack" &&
            x.Amount == 1500);
        history.Should().ContainSingle(x =>
            x.Type == "refund" &&
            x.Description == "Refund for quick_pack" &&
            x.Amount == -600 &&
            x.Status == "refunded");
        history.Should().NotContain(x => x.Currency == "usd");
    }

    [Fact]
    public async Task DeleteAccountAsync_erases_user_children_and_cancels_subscription_once()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var userId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var usagePeriodId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var creditId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.AppUsers.Add(new AppUser
            {
                Id = userId,
                ExternalAuthUserId = "entra-delete-handler",
                Email = "delete@example.com",
                StripeCustomerId = "cus_delete",
                StripeSubscriptionId = "sub_delete",
                SubscriptionStatus = SubscriptionStatus.Active,
                CurrentPeriodEnd = now.AddDays(20),
                CreatedAt = now.AddDays(-20),
                UpdatedAt = now.AddDays(-1),
            });
            seedDb.RewriteAttempts.Add(new RewriteAttempt
            {
                Id = attemptId,
                UserId = userId,
                IdempotencyKey = "idem-delete",
                RequestHash = "hash-delete",
                RequestJson = "{\"roughDraftReply\":\"private draft\"}",
                ResultJson = "{\"reply\":\"private result\"}",
                Status = RewriteAttemptStatus.Processing,
                ExpiresAt = now.AddDays(1),
                CreatedAt = now.AddDays(-1),
            });
            seedDb.UsagePeriods.Add(new UsagePeriod
            {
                Id = usagePeriodId,
                UserId = userId,
                PeriodKey = "paid:sub_delete:2026-06-30T00:00:00.0000000+00:00",
                QuotaLimit = 90,
                UsedCount = 4,
                ReservedCount = 1,
                PeriodStart = now.AddDays(-10),
                PeriodEnd = now.AddDays(20),
                CreatedAt = now.AddDays(-10),
                UpdatedAt = now,
            });
            seedDb.UsageReservations.Add(new UsageReservation
            {
                Id = reservationId,
                UserId = userId,
                UsagePeriodId = usagePeriodId,
                RewriteAttemptId = attemptId,
                RewriteCreditId = creditId,
                Status = UsageReservationStatus.Pending,
                ExpiresAt = now.AddMinutes(30),
                CreatedAt = now.AddMinutes(-10),
            });
            seedDb.RewriteCredits.Add(new RewriteCredit
            {
                Id = creditId,
                UserId = userId,
                Source = "PURCHASE",
                AmountGranted = 10,
                OriginalAmountGranted = 10,
                AmountConsumed = 2,
                GrantedAt = now.AddDays(-5),
                ExpiresAt = now.AddDays(25),
                StripeEventId = "evt_credit",
            });
            seedDb.PromoCodes.Add(new PromoCode
            {
                Id = Guid.NewGuid(),
                Code = "ERASECHECK",
                DisplayCode = "EraseCheck",
                Description = "Account erase coverage",
                Kind = PromoCodeKind.TrialCredits,
                CreditsGranted = 3,
                GrantTtlDays = 90,
                ValidFrom = now.AddDays(-1),
                ValidUntil = now.AddDays(30),
                MaxRedemptionsGlobal = 100,
                MaxRedemptionsPerUser = 1,
                RedemptionCount = 1,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                Redemptions =
                {
                    new PromoCodeRedemption
                    {
                        UserId = userId,
                        RewriteCreditId = creditId,
                        CreditsGranted = 3,
                        CodeSnapshot = "ERASECHECK",
                        RedeemIpHash = "ip-hash-to-clear",
                        Status = PromoCodeRedemptionStatus.Applied,
                        RedeemedAt = now,
                    },
                },
            });
            seedDb.BillingSupportRequests.Add(new BillingSupportRequest
            {
                UserId = userId,
                Type = BillingSupportRequestType.Refund,
                RelatedPaymentIntentId = "pi_support",
                Message = "please review",
                Status = BillingSupportRequestStatus.Open,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddDays(-1),
            });
            await seedDb.SaveChangesAsync();
        }

        var cancellationService = new RecordingCancellationService();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateDeleteAccountHandler(handlerDb, cancellationService);

        await handler.HandleAsync(new DeleteAccountCommand("entra-delete-handler"));
        await handler.HandleAsync(new DeleteAccountCommand("entra-delete-handler"));

        cancellationService.SubscriptionIds.Should().Equal("sub_delete");

        await using var verifyDb = fixture.CreateContext();
        var user = await verifyDb.AppUsers.SingleAsync(x => x.Id == userId);
        user.ExternalAuthUserId.Should().StartWith("erased:");
        user.Email.Should().BeNull();
        user.StripeCustomerId.Should().Be("cus_delete");
        user.StripeSubscriptionId.Should().Be("sub_delete");
        user.SubscriptionStatus.Should().Be(SubscriptionStatus.Canceled);
        user.CurrentPeriodEnd.Should().BeNull();

        var attempt = await verifyDb.RewriteAttempts.SingleAsync(x => x.Id == attemptId);
        attempt.IdempotencyKey.Should().StartWith("erased:");
        attempt.RequestHash.Should().Be("erased");
        attempt.RequestJson.Should().Be("{}");
        attempt.ResultJson.Should().BeNull();
        attempt.Status.Should().Be(RewriteAttemptStatus.Failed);
        attempt.ErrorCode.Should().Be("account_erased");

        var period = await verifyDb.UsagePeriods.SingleAsync(x => x.Id == usagePeriodId);
        period.PeriodKey.Should().StartWith("erased:");
        period.QuotaLimit.Should().Be(0);
        period.UsedCount.Should().Be(0);
        period.ReservedCount.Should().Be(0);
        period.PeriodStart.Should().BeNull();
        period.PeriodEnd.Should().BeNull();

        var reservation = await verifyDb.UsageReservations.SingleAsync(x => x.Id == reservationId);
        reservation.Status.Should().Be(UsageReservationStatus.Released);
        reservation.ReleasedAt.Should().NotBeNull();

        var credit = await verifyDb.RewriteCredits.SingleAsync(x => x.Id == creditId);
        credit.Source.Should().Be("ERASED");
        credit.AmountGranted.Should().Be(0);
        credit.OriginalAmountGranted.Should().BeNull();
        credit.AmountConsumed.Should().Be(0);
        credit.ExpiresAt.Should().NotBeNull();
        credit.StripeEventId.Should().Be("evt_credit");

        var redemption = await verifyDb.PromoCodeRedemptions.SingleAsync();
        redemption.RedeemIpHash.Should().BeNull();
        redemption.Status.Should().Be(PromoCodeRedemptionStatus.Applied);

        var supportRequest = await verifyDb.BillingSupportRequests.SingleAsync();
        supportRequest.RelatedPaymentIntentId.Should().BeNull();
        supportRequest.Message.Should().Be("erased");
        supportRequest.Status.Should().Be(BillingSupportRequestStatus.Resolved);
        supportRequest.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAccountAsync_scrubs_soft_deleted_attempts()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var userId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");

        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.AppUsers.Add(NewUser(
                userId,
                "entra-delete-soft-attempt",
                now,
                SubscriptionStatus.Active));
            seedDb.RewriteAttempts.Add(new RewriteAttempt
            {
                Id = attemptId,
                UserId = userId,
                IdempotencyKey = "idem-delete-soft",
                RequestHash = "hash-delete-soft",
                RequestJson = "{\"roughDraftReply\":\"private draft\"}",
                ResultJson = "{\"rewrittenText\":\"private result\"}",
                Status = RewriteAttemptStatus.Succeeded,
                CreatedAt = now.AddDays(-2),
                CompletedAt = now.AddDays(-2).AddMinutes(1),
                DeletedAt = now.AddDays(-1),
                ExpiresAt = now.AddDays(1),
            });
            await seedDb.SaveChangesAsync();
        }

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateDeleteAccountHandler(handlerDb, new RecordingCancellationService());

        await handler.HandleAsync(new DeleteAccountCommand("entra-delete-soft-attempt"));

        await using var verifyDb = fixture.CreateContext();
        var attempt = await verifyDb.RewriteAttempts
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == attemptId);
        attempt.RequestJson.Should().Be("{}");
        attempt.ResultJson.Should().BeNull();
        attempt.IdempotencyKey.Should().StartWith("erased:");
        attempt.RequestHash.Should().Be("erased");
    }

    private static GetOrCreateUserHandler CreateGetOrCreateUserHandler(AppDbContext db) =>
        new(new AppUserRepository(db), new UnitOfWork(db));

    private static GetAccountSummaryHandler CreateAccountSummaryHandler(
        AppDbContext db,
        int freeBaselineRewrites) =>
        new(
            new AppUserRepository(db),
            new UsagePeriodRepository(db),
            new RewriteCreditRepository(db),
            new PromoCodeRedemptionRepository(db),
            new PromoCodeRepository(db),
            new TestAccountUsagePlanProvider(freeBaselineRewrites),
            new UnitOfWork(db));

    private static GetPurchaseHistoryHandler CreatePurchaseHistoryHandler(AppDbContext db) =>
        new(
            new AppUserRepository(db),
            new RewriteCreditRepository(db),
            new UnitOfWork(db));

    private static GetBillingHistoryHandler CreateBillingHistoryHandler(AppDbContext db) =>
        new(
            new AppUserRepository(db),
            new RewriteCreditRepository(db),
            new StripeInvoiceRepository(db),
            new UnitOfWork(db));

    private static DeleteAccountHandler CreateDeleteAccountHandler(
        AppDbContext db,
        IStripeSubscriptionCancellationService cancellationService) =>
        new(
            new AppUserRepository(db),
            new RewriteAttemptRepository(db),
            new UsagePeriodRepository(db),
            new UsageReservationRepository(db),
            new RewriteCreditRepository(db),
            new PromoCodeRedemptionRepository(db),
            new BillingSupportRequestRepository(db),
            new UnitOfWork(db),
            cancellationService);

    private static AppUser NewUser(
        Guid userId,
        string externalAuthUserId,
        DateTimeOffset now,
        SubscriptionStatus subscriptionStatus = SubscriptionStatus.Inactive,
        string? stripeSubscriptionId = null) =>
        new()
        {
            Id = userId,
            ExternalAuthUserId = externalAuthUserId,
            Email = $"{externalAuthUserId}@example.com",
            StripeSubscriptionId = stripeSubscriptionId,
            SubscriptionStatus = subscriptionStatus,
            CreatedAt = now,
            UpdatedAt = now,
        };

    private sealed class TestAccountUsagePlanProvider(int freeBaselineRewrites) : IAccountUsagePlanProvider
    {
        public AccountUsagePlanDto GetUsagePlan(AppUser user)
        {
            if (user.SubscriptionStatus is SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.Testing or SubscriptionStatus.PastDue)
            {
                return new AccountUsagePlanDto(
                    "paid",
                    $"paid:{user.StripeSubscriptionId ?? user.Id.ToString()}:{user.CurrentPeriodEnd?.ToString("O") ?? "no-period"}",
                    user.SubscriptionStatus == SubscriptionStatus.Testing ? 10_000 : 90);
            }

            return new AccountUsagePlanDto("free", "free:lifetime", freeBaselineRewrites);
        }
    }

    private sealed class RecordingCancellationService : IStripeSubscriptionCancellationService
    {
        public List<string> SubscriptionIds { get; } = [];

        public Task CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default)
        {
            SubscriptionIds.Add(stripeSubscriptionId);
            return Task.CompletedTask;
        }
    }
}
