using System.Data;
using System.Text.Json;
using FluentAssertions;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.StripeReconciliation;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Tests.Application;

public sealed class StripeReconciliationUseCaseTests
{
    private static readonly DateTimeOffset WindowStart = DateTimeOffset.Parse("2026-06-01T00:00:00Z");
    private static readonly DateTimeOffset WindowEnd = DateTimeOffset.Parse("2026-06-02T00:00:00Z");
    private static readonly DateTimeOffset CompletedAt = DateTimeOffset.Parse("2026-06-02T00:05:00Z");

    [Fact]
    public async Task Reconcile_clean_run_persists_run_row_without_alert()
    {
        var creditId = Guid.NewGuid();
        var stripeClient = new FakeStripePaymentReconciliationClient
        {
            Payments =
            [
                new StripePaidPaymentDto(
                    " pi_clean ",
                    250,
                    "NZD",
                    WindowStart.AddHours(1)),
            ],
        };
        var grantRepository = new FakePaymentGrantRepository
        {
            Grants =
            [
                new PaymentGrantSnapshot(
                    creditId,
                    "pi_clean",
                    250,
                    "nzd",
                WindowStart.AddHours(1).AddMinutes(2)),
            ],
        };
        var unitOfWork = new FakeUnitOfWork();
        var runs = new FakeStripeReconciliationRunRepository(unitOfWork);
        var outbox = new FakeOutboxMessageRepository(unitOfWork);
        var handler = CreateHandler(
            grantRepository,
            stripeClient,
            runs: runs,
            outboxMessages: outbox,
            unitOfWork: unitOfWork);

        var report = await handler.HandleAsync(new ReconcileStripeCommand(
            WindowStart,
            WindowEnd,
            CompletedAt));

        report.WindowStart.Should().Be(WindowStart);
        report.WindowEnd.Should().Be(WindowEnd);
        report.CompletedAt.Should().Be(CompletedAt);
        report.StripePaymentCount.Should().Be(1);
        report.PurchaseGrantCount.Should().Be(1);
        report.DiscrepancyCount.Should().Be(0);
        report.Discrepancies.Should().BeEmpty();
        report.AutoGrantedCount.Should().Be(0);
        report.AutoGrantSkippedCount.Should().Be(0);
        report.ManualReviewCount.Should().Be(0);
        report.SubscriptionMismatchCount.Should().Be(0);
        grantRepository.PaymentIntentIds.Should().Equal("pi_clean");
        runs.Runs.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new
            {
                WindowStart,
                WindowEnd,
                CompletedAt,
                StripePaymentCount = 1,
                PurchaseGrantCount = 1,
                PaidButNoGrantCount = 0,
                GrantButNoPaymentCount = 0,
                AmountMismatchCount = 0,
                SubscriptionMismatchCount = 0,
                AutoGrantedCount = 0,
                AutoGrantSkippedCount = 0,
                ManualReviewCount = 0,
            });
        outbox.Messages.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(1);
        runs.TransactionIndexes.Should().ContainSingle().Which.Should().Be(1);
    }

    [Fact]
    public async Task Reconcile_auto_grants_missing_credit_with_audit_row()
    {
        var userId = Guid.NewGuid();
        var stripeClient = new FakeStripePaymentReconciliationClient
        {
            Payments =
            [
                new StripePaidPaymentDto(
                    "pi_auto_grant",
                    750,
                    "nzd",
                    WindowStart.AddHours(2)),
            ],
            Sessions =
            {
                ["pi_auto_grant"] = new StripeCheckoutSessionSnapshotDto(
                    "cs_auto_grant",
                    "pi_auto_grant",
                    "payment",
                    "paid",
                    "clerk_auto_grant",
                    "cus_auto_grant",
                    "quick_pack",
                    10,
                    750,
                    "nzd"),
            },
        };
        var grantRepository = new FakePaymentGrantRepository();
        var unitOfWork = new FakeUnitOfWork();
        var credits = new FakeRewriteCreditRepository(unitOfWork);
        var adminUsers = new FakeAdminUserRepository(unitOfWork);
        var appUsers = new FakeAppUserRepository
        {
            Users =
            [
                new AppUser
                {
                    Id = userId,
                    ExternalAuthUserId = "clerk_auto_grant",
                    StripeCustomerId = "cus_auto_grant",
                },
            ],
        };
        var runs = new FakeStripeReconciliationRunRepository(unitOfWork);
        var outbox = new FakeOutboxMessageRepository(unitOfWork);
        var handler = CreateHandler(
            grantRepository,
            stripeClient,
            credits,
            appUsers,
            adminUsers,
            runs,
            outbox,
            unitOfWork: unitOfWork);

        var report = await handler.HandleAsync(new ReconcileStripeCommand(
            WindowStart,
            WindowEnd,
            CompletedAt));

        report.PaidButNoGrantCount.Should().Be(1);
        report.AutoGrantedCount.Should().Be(1);
        report.AutoGrantSkippedCount.Should().Be(0);
        report.ManualReview.Should().BeEmpty();
        var autoGrant = report.AutoGrants.Should().ContainSingle().Subject;
        autoGrant.PaymentIntentId.Should().Be("pi_auto_grant");
        autoGrant.UserId.Should().Be(userId);
        autoGrant.Rewrites.Should().Be(10);
        autoGrant.Sku.Should().Be("quick_pack");

        var credit = credits.AddedCredits.Should().ContainSingle().Subject;
        credit.Id.Should().Be(autoGrant.CreditId);
        credit.UserId.Should().Be(userId);
        credit.Source.Should().Be("PURCHASE");
        credit.AmountGranted.Should().Be(10);
        credit.OriginalAmountGranted.Should().Be(10);
        credit.AmountConsumed.Should().Be(0);
        credit.GrantedAt.Should().Be(CompletedAt);
        credit.ExpiresAt.Should().Be(CompletedAt.AddDays(90));
        credit.StripeEventId.Should().Be("reconciliation:pi_auto_grant");
        credit.StripePaymentIntentId.Should().Be("pi_auto_grant");
        credit.StripeSku.Should().Be("quick_pack");
        credit.StripeAmountTotal.Should().Be(750);
        credit.StripeCurrency.Should().Be("nzd");

        var audit = adminUsers.AuditLogs.Should().ContainSingle().Subject;
        audit.Action.Should().Be("reconciliation_auto_grant");
        audit.TargetUserId.Should().Be(userId);
        audit.DetailsJson.Should().Contain("\"source\":\"reconciliation\"");
        audit.DetailsJson.Should().Contain("pi_auto_grant");
        credits.AddTransactionIndexes.Should().ContainSingle().Which.Should().Be(1);
        adminUsers.AuditTransactionIndexes.Should().ContainSingle().Which.Should().Be(1);
        runs.TransactionIndexes.Should().ContainSingle().Which.Should().Be(2);
        outbox.TransactionIndexes.Should().ContainSingle().Which.Should().Be(2);
    }

    [Fact]
    public async Task Reconcile_auto_grant_skips_when_credit_already_exists()
    {
        var stripeClient = CreateEligibleAutoGrantStripeClient("pi_already_exists", WindowStart.AddHours(2));
        var grantRepository = new FakePaymentGrantRepository();
        var unitOfWork = new FakeUnitOfWork();
        var credits = new FakeRewriteCreditRepository(unitOfWork)
        {
            ExistingStripeEventIds = { "reconciliation:pi_already_exists" },
        };
        var adminUsers = new FakeAdminUserRepository(unitOfWork);
        var handler = CreateHandler(
            grantRepository,
            stripeClient,
            credits,
            CreateUserRepository("clerk_pi_already_exists", "cus_pi_already_exists"),
            adminUsers,
            unitOfWork: unitOfWork);

        var report = await handler.HandleAsync(new ReconcileStripeCommand(
            WindowStart,
            WindowEnd,
            CompletedAt));

        report.AutoGrantedCount.Should().Be(0);
        report.ManualReview.Should().BeEmpty();
        credits.AddedCredits.Should().BeEmpty();
        adminUsers.AuditLogs.Should().BeEmpty();

        var secondUnitOfWork = new FakeUnitOfWork();
        var secondCredits = new FakeRewriteCreditRepository(secondUnitOfWork)
        {
            ExistingCreditsByPaymentIntent =
            {
                ["pi_already_exists_by_payment"] =
                [
                    new RewriteCredit
                    {
                        UserId = Guid.NewGuid(),
                        Source = "PURCHASE",
                        AmountGranted = 10,
                        StripePaymentIntentId = "pi_already_exists_by_payment",
                    },
                ],
            },
        };
        var secondAdminUsers = new FakeAdminUserRepository(secondUnitOfWork);
        var secondHandler = CreateHandler(
            new FakePaymentGrantRepository(),
            CreateEligibleAutoGrantStripeClient("pi_already_exists_by_payment", WindowStart.AddHours(2)),
            secondCredits,
            CreateUserRepository("clerk_pi_already_exists_by_payment", "cus_pi_already_exists_by_payment"),
            secondAdminUsers,
            unitOfWork: secondUnitOfWork);

        var secondReport = await secondHandler.HandleAsync(new ReconcileStripeCommand(
            WindowStart,
            WindowEnd,
            CompletedAt));

        secondReport.AutoGrantedCount.Should().Be(0);
        secondReport.ManualReview.Should().BeEmpty();
        secondCredits.AddedCredits.Should().BeEmpty();
        secondAdminUsers.AuditLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task Reconcile_auto_grant_respects_cap_and_flags_overflow()
    {
        var stripeClient = new FakeStripePaymentReconciliationClient();
        for (var i = 1; i <= 3; i++)
        {
            var paymentIntentId = $"pi_cap_{i}";
            stripeClient.Payments.Add(new StripePaidPaymentDto(
                paymentIntentId,
                500 + i,
                "nzd",
                WindowStart.AddHours(i)));
            stripeClient.Sessions[paymentIntentId] = new StripeCheckoutSessionSnapshotDto(
                $"cs_cap_{i}",
                paymentIntentId,
                "payment",
                "paid",
                $"clerk_cap_{i}",
                $"cus_cap_{i}",
                "quick_pack",
                10,
                500 + i,
                "nzd");
        }

        var unitOfWork = new FakeUnitOfWork();
        var credits = new FakeRewriteCreditRepository(unitOfWork);
        var adminUsers = new FakeAdminUserRepository(unitOfWork);
        var appUsers = new FakeAppUserRepository
        {
            Users =
            [
                new AppUser { ExternalAuthUserId = "clerk_cap_1", StripeCustomerId = "cus_cap_1" },
                new AppUser { ExternalAuthUserId = "clerk_cap_2", StripeCustomerId = "cus_cap_2" },
                new AppUser { ExternalAuthUserId = "clerk_cap_3", StripeCustomerId = "cus_cap_3" },
            ],
        };
        var handler = CreateHandler(
            new FakePaymentGrantRepository(),
            stripeClient,
            credits,
            appUsers,
            adminUsers,
            options: new StripeReconciliationOptions(1, 60, 3),
            unitOfWork: unitOfWork);

        var report = await handler.HandleAsync(new ReconcileStripeCommand(
            WindowStart,
            WindowEnd,
            CompletedAt));

        credits.AddedCredits.Should().ContainSingle();
        adminUsers.AuditLogs.Should().ContainSingle();
        report.AutoGrantedCount.Should().Be(1);
        report.AutoGrantSkippedCount.Should().Be(2);
        report.ManualReview.Should().HaveCount(2);
        report.ManualReview.Select(x => x.PaymentIntentId).Should().Equal("pi_cap_2", "pi_cap_3");
        report.ManualReview.Select(x => x.Reason).Should().AllBeEquivalentTo("over_cap");
    }

    [Fact]
    public async Task Reconcile_flags_manual_review_when_session_missing_or_not_payment_mode_or_user_unresolvable()
    {
        var stripeClient = new FakeStripePaymentReconciliationClient
        {
            Payments =
            [
                new StripePaidPaymentDto("pi_no_session", 500, "nzd", WindowStart.AddHours(1)),
                new StripePaidPaymentDto("pi_not_payment", 600, "nzd", WindowStart.AddHours(2)),
                new StripePaidPaymentDto("pi_no_user", 700, "nzd", WindowStart.AddHours(3)),
            ],
            Sessions =
            {
                ["pi_not_payment"] = new StripeCheckoutSessionSnapshotDto(
                    "cs_not_payment",
                    "pi_not_payment",
                    "subscription",
                    "paid",
                    "clerk_not_payment",
                    "cus_not_payment",
                    "quick_pack",
                    10,
                    600,
                    "nzd"),
                ["pi_no_user"] = new StripeCheckoutSessionSnapshotDto(
                    "cs_no_user",
                    "pi_no_user",
                    "payment",
                    "paid",
                    "clerk_no_user",
                    "cus_no_user",
                    "quick_pack",
                    10,
                    700,
                    "nzd"),
            },
        };
        var unitOfWork = new FakeUnitOfWork();
        var credits = new FakeRewriteCreditRepository(unitOfWork);
        var handler = CreateHandler(
            new FakePaymentGrantRepository(),
            stripeClient,
            credits,
            new FakeAppUserRepository(),
            new FakeAdminUserRepository(unitOfWork),
            unitOfWork: unitOfWork);

        var report = await handler.HandleAsync(new ReconcileStripeCommand(
            WindowStart,
            WindowEnd,
            CompletedAt));

        credits.AddedCredits.Should().BeEmpty();
        report.AutoGrantedCount.Should().Be(0);
        report.ManualReview.Select(x => x.PaymentIntentId).Should().Equal(
            "pi_no_session",
            "pi_not_payment",
            "pi_no_user");
        report.ManualReview.Select(x => x.Reason).Should().Equal(
            "no_checkout_session",
            "not_payment_mode",
            "user_not_found");
    }

    [Fact]
    public async Task Reconcile_defers_recent_payment_below_min_age()
    {
        var stripeClient = new FakeStripePaymentReconciliationClient
        {
            Payments =
            [
                new StripePaidPaymentDto(
                    "pi_recent",
                    500,
                    "nzd",
                    CompletedAt.AddMinutes(-20)),
            ],
        };
        var unitOfWork = new FakeUnitOfWork();
        var credits = new FakeRewriteCreditRepository(unitOfWork);
        var handler = CreateHandler(
            new FakePaymentGrantRepository(),
            stripeClient,
            credits,
            new FakeAppUserRepository(),
            new FakeAdminUserRepository(unitOfWork),
            options: new StripeReconciliationOptions(10, 60, 3),
            unitOfWork: unitOfWork);

        var report = await handler.HandleAsync(new ReconcileStripeCommand(
            WindowStart,
            WindowEnd,
            CompletedAt));

        credits.AddedCredits.Should().BeEmpty();
        report.AutoGrantedCount.Should().Be(0);
        report.ManualReview.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new StripeReconciliationManualReviewDto("pi_recent", "payment_too_recent"));
    }

    [Fact]
    public async Task Reconcile_never_mutates_grant_but_no_payment()
    {
        var creditId = Guid.NewGuid();
        var grantRepository = new FakePaymentGrantRepository
        {
            Grants =
            [
                new PaymentGrantSnapshot(
                    creditId,
                    "pi_missing_payment",
                    900,
                    "nzd",
                WindowStart.AddHours(3)),
            ],
        };
        var unitOfWork = new FakeUnitOfWork();
        var credits = new FakeRewriteCreditRepository(unitOfWork);
        var appUsers = new FakeAppUserRepository();
        var adminUsers = new FakeAdminUserRepository(unitOfWork);
        var handler = CreateHandler(
            grantRepository,
            new FakeStripePaymentReconciliationClient(),
            credits,
            appUsers,
            adminUsers,
            unitOfWork: unitOfWork);

        var report = await handler.HandleAsync(new ReconcileStripeCommand(
            WindowStart,
            WindowEnd,
            CompletedAt));

        report.PaidButNoGrantCount.Should().Be(0);
        report.GrantButNoPaymentCount.Should().Be(1);
        report.AmountMismatchCount.Should().Be(0);
        report.Discrepancies.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new StripeReconciliationDiscrepancyDto(
                StripeReconciliationDiscrepancyKindDto.GrantButNoPayment,
                "pi_missing_payment",
                creditId,
                StripeAmount: null,
                LedgerAmount: 900,
                StripeCurrency: null,
                LedgerCurrency: "nzd",
                StripePaidAt: null,
                LedgerGrantedAt: WindowStart.AddHours(3)));
        credits.AddedCredits.Should().BeEmpty();
        adminUsers.AuditLogs.Should().BeEmpty();
        appUsers.FindForCheckoutCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ReconcileAsync_alerts_when_matching_grant_amount_differs_from_paid_payment()
    {
        var creditId = Guid.NewGuid();
        var stripeClient = new FakeStripePaymentReconciliationClient
        {
            Payments =
            [
                new StripePaidPaymentDto(
                    "pi_amount_mismatch",
                    1_200,
                    "nzd",
                    WindowStart.AddHours(4)),
            ],
        };
        var grantRepository = new FakePaymentGrantRepository
        {
            Grants =
            [
                new PaymentGrantSnapshot(
                    creditId,
                    "pi_amount_mismatch",
                    1_000,
                    "nzd",
                WindowStart.AddHours(4).AddMinutes(2)),
            ],
        };
        var handler = CreateHandler(
            grantRepository,
            stripeClient,
            unitOfWork: new FakeUnitOfWork());

        var report = await handler.HandleAsync(new ReconcileStripeCommand(
            WindowStart,
            WindowEnd,
            CompletedAt));

        report.PaidButNoGrantCount.Should().Be(0);
        report.GrantButNoPaymentCount.Should().Be(0);
        report.AmountMismatchCount.Should().Be(1);
        report.Discrepancies.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new StripeReconciliationDiscrepancyDto(
                StripeReconciliationDiscrepancyKindDto.AmountMismatch,
                "pi_amount_mismatch",
                creditId,
                StripeAmount: 1_200,
                LedgerAmount: 1_000,
                StripeCurrency: "nzd",
                LedgerCurrency: "nzd",
                StripePaidAt: WindowStart.AddHours(4),
                LedgerGrantedAt: WindowStart.AddHours(4).AddMinutes(2)));
    }

    [Fact]
    public async Task Reconcile_reports_subscription_mismatch_both_directions()
    {
        var inactiveUserId = Guid.NewGuid();
        var localActiveUserId = Guid.NewGuid();
        var stripeClient = new FakeStripePaymentReconciliationClient
        {
            Subscriptions =
            [
                new StripeSubscriptionSnapshotDto("sub_stripe_active", "cus_stripe_active", "active"),
                new StripeSubscriptionSnapshotDto("sub_stripe_inactive", "cus_stripe_inactive", "canceled"),
            ],
        };
        var grantRepository = new FakePaymentGrantRepository
        {
            SubscriptionUsers =
            [
                new SubscriptionUserSnapshot(
                    inactiveUserId,
                    "cus_stripe_active",
                    "sub_stripe_active",
                    SubscriptionStatus.Inactive),
                new SubscriptionUserSnapshot(
                    localActiveUserId,
                    "cus_local_active",
                    "sub_missing_in_stripe",
                    SubscriptionStatus.Active),
                new SubscriptionUserSnapshot(
                    Guid.NewGuid(),
                    "cus_testing",
                    "sub_testing",
                    SubscriptionStatus.Testing),
            ],
        };
        var handler = CreateHandler(
            grantRepository,
            stripeClient,
            unitOfWork: new FakeUnitOfWork());

        var report = await handler.HandleAsync(new ReconcileStripeCommand(
            WindowStart,
            WindowEnd,
            CompletedAt));

        report.SubscriptionMismatchCount.Should().Be(2);
        report.SubscriptionMismatches.Should().BeEquivalentTo(
        [
            new StripeSubscriptionDiscrepancyDto(
                "stripe_active_local_not",
                "sub_stripe_active",
                "cus_stripe_active",
                inactiveUserId,
                "active",
                "Inactive"),
            new StripeSubscriptionDiscrepancyDto(
                "local_active_stripe_not",
                "sub_missing_in_stripe",
                "cus_local_active",
                localActiveUserId,
                null,
                "Active"),
        ]);
    }

    [Fact]
    public async Task Reconcile_persists_run_row_and_enqueues_alert_outbox_in_same_transaction()
    {
        var stripeClient = new FakeStripePaymentReconciliationClient
        {
            Payments =
            [
                new StripePaidPaymentDto("pi_missing_for_run", 750, "nzd", WindowStart.AddHours(2)),
            ],
        };
        var unitOfWork = new FakeUnitOfWork();
        var runs = new FakeStripeReconciliationRunRepository(unitOfWork);
        var outbox = new FakeOutboxMessageRepository(unitOfWork);
        var handler = CreateHandler(
            new FakePaymentGrantRepository(),
            stripeClient,
            runs: runs,
            outboxMessages: outbox,
            unitOfWork: unitOfWork);

        var report = await handler.HandleAsync(new ReconcileStripeCommand(
            WindowStart,
            WindowEnd,
            CompletedAt));

        var run = runs.Runs.Should().ContainSingle().Subject;
        run.WindowStart.Should().Be(WindowStart);
        run.WindowEnd.Should().Be(WindowEnd);
        run.CompletedAt.Should().Be(CompletedAt);
        run.StripePaymentCount.Should().Be(report.StripePaymentCount);
        run.PurchaseGrantCount.Should().Be(report.PurchaseGrantCount);
        run.PaidButNoGrantCount.Should().Be(report.PaidButNoGrantCount);
        run.GrantButNoPaymentCount.Should().Be(report.GrantButNoPaymentCount);
        run.AmountMismatchCount.Should().Be(report.AmountMismatchCount);
        run.SubscriptionMismatchCount.Should().Be(report.SubscriptionMismatchCount);
        run.AutoGrantedCount.Should().Be(report.AutoGrantedCount);
        run.AutoGrantSkippedCount.Should().Be(report.AutoGrantSkippedCount);
        run.ManualReviewCount.Should().Be(report.ManualReviewCount);
        run.ReportJson.Should().Contain("\"paidButNoGrantCount\":1");

        var message = outbox.Messages.Should().ContainSingle().Subject;
        message.MessageType.Should().Be("StripeReconciliationAlertRequested");
        message.Status.Should().Be(OutboxMessageStatus.Pending);
        message.CreatedAt.Should().Be(CompletedAt);
        message.NextAttemptAt.Should().Be(CompletedAt);
        message.MaxAttempts.Should().Be(10);
        message.CorrelationId.Should().Be(run.Id.ToString());
        message.PayloadJson.Should().Contain("\"paidButNoGrantCount\":1");
        runs.TransactionIndexes.Should().ContainSingle().Which.Should().Be(1);
        outbox.TransactionIndexes.Should().ContainSingle().Which.Should().Be(1);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Reconcile_continues_after_single_grant_write_failure()
    {
        var stripeClient = new FakeStripePaymentReconciliationClient();
        foreach (var paymentIntentId in new[] { "pi_write_fails", "pi_write_succeeds" })
        {
            stripeClient.Payments.Add(new StripePaidPaymentDto(
                paymentIntentId,
                500,
                "nzd",
                WindowStart.AddHours(stripeClient.Payments.Count + 1)));
            stripeClient.Sessions[paymentIntentId] = new StripeCheckoutSessionSnapshotDto(
                $"cs_{paymentIntentId}",
                paymentIntentId,
                "payment",
                "paid",
                $"clerk_{paymentIntentId}",
                $"cus_{paymentIntentId}",
                "quick_pack",
                10,
                500,
                "nzd");
        }

        var unitOfWork = new FakeUnitOfWork();
        var credits = new FakeRewriteCreditRepository(unitOfWork)
        {
            ThrowOnAddPaymentIntentIds = { "pi_write_fails" },
        };
        var adminUsers = new FakeAdminUserRepository(unitOfWork);
        var handler = CreateHandler(
            new FakePaymentGrantRepository(),
            stripeClient,
            credits,
            new FakeAppUserRepository
            {
                Users =
                [
                    new AppUser { ExternalAuthUserId = "clerk_pi_write_fails", StripeCustomerId = "cus_pi_write_fails" },
                    new AppUser { ExternalAuthUserId = "clerk_pi_write_succeeds", StripeCustomerId = "cus_pi_write_succeeds" },
                ],
            },
            adminUsers,
            unitOfWork: unitOfWork);

        var report = await handler.HandleAsync(new ReconcileStripeCommand(
            WindowStart,
            WindowEnd,
            CompletedAt));

        credits.AddedCredits.Should().ContainSingle().Which.StripePaymentIntentId.Should().Be("pi_write_succeeds");
        adminUsers.AuditLogs.Should().ContainSingle();
        report.AutoGrantedCount.Should().Be(1);
        report.ManualReview.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new StripeReconciliationManualReviewDto("pi_write_fails", "grant_write_failed"));
    }

    private static ReconcileStripeHandler CreateHandler(
        FakePaymentGrantRepository? paymentGrants = null,
        FakeStripePaymentReconciliationClient? stripeClient = null,
        FakeRewriteCreditRepository? credits = null,
        FakeAppUserRepository? appUsers = null,
        FakeAdminUserRepository? adminUsers = null,
        FakeStripeReconciliationRunRepository? runs = null,
        FakeOutboxMessageRepository? outboxMessages = null,
        StripeReconciliationOptions? options = null,
        FakeUnitOfWork? unitOfWork = null)
    {
        unitOfWork ??= new FakeUnitOfWork();
        return new ReconcileStripeHandler(
            paymentGrants ?? new FakePaymentGrantRepository(),
            stripeClient ?? new FakeStripePaymentReconciliationClient(),
            credits ?? new FakeRewriteCreditRepository(unitOfWork),
            appUsers ?? new FakeAppUserRepository(),
            adminUsers ?? new FakeAdminUserRepository(unitOfWork),
            runs ?? new FakeStripeReconciliationRunRepository(unitOfWork),
            outboxMessages ?? new FakeOutboxMessageRepository(unitOfWork),
            options ?? new StripeReconciliationOptions(10, 60, 3),
            unitOfWork);
    }

    private static FakeStripePaymentReconciliationClient CreateEligibleAutoGrantStripeClient(
        string paymentIntentId,
        DateTimeOffset paidAt)
    {
        var externalId = $"clerk_{paymentIntentId}";
        var customerId = $"cus_{paymentIntentId}";
        return new FakeStripePaymentReconciliationClient
        {
            Payments =
            [
                new StripePaidPaymentDto(
                    paymentIntentId,
                    500,
                    "nzd",
                    paidAt),
            ],
            Sessions =
            {
                [paymentIntentId] = new StripeCheckoutSessionSnapshotDto(
                    $"cs_{paymentIntentId}",
                    paymentIntentId,
                    "payment",
                    "paid",
                    externalId,
                    customerId,
                    "quick_pack",
                    10,
                    500,
                    "nzd"),
            },
        };
    }

    private static FakeAppUserRepository CreateUserRepository(
        string externalAuthUserId,
        string stripeCustomerId) =>
        new()
        {
            Users =
            [
                new AppUser
                {
                    ExternalAuthUserId = externalAuthUserId,
                    StripeCustomerId = stripeCustomerId,
                },
            ],
        };

    private sealed class FakePaymentGrantRepository : IPaymentGrantRepository
    {
        public IReadOnlyList<PaymentGrantSnapshot> Grants { get; init; } = [];
        public IReadOnlyList<SubscriptionUserSnapshot> SubscriptionUsers { get; init; } = [];
        public IReadOnlyList<string> PaymentIntentIds { get; private set; } = [];

        public Task<IReadOnlyList<PaymentGrantSnapshot>> ListPurchaseGrantsForReconciliationAsync(
            DateTimeOffset windowStart,
            DateTimeOffset windowEnd,
            IReadOnlyCollection<string> paymentIntentIds,
            CancellationToken ct = default)
        {
            windowStart.Should().Be(WindowStart);
            windowEnd.Should().Be(WindowEnd);
            PaymentIntentIds = paymentIntentIds.ToList();
            return Task.FromResult(Grants);
        }

        public Task<IReadOnlyList<SubscriptionUserSnapshot>> ListSubscriptionUsersForReconciliationAsync(
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SubscriptionUserSnapshot>>(SubscriptionUsers
                .Where(x => x.Status != SubscriptionStatus.Testing)
                .ToList());
    }

    private sealed class FakeStripePaymentReconciliationClient : IStripePaymentReconciliationClient
    {
        public List<StripePaidPaymentDto> Payments { get; init; } = [];
        public Dictionary<string, StripeCheckoutSessionSnapshotDto?> Sessions { get; init; } = [];
        public IReadOnlyList<StripeSubscriptionSnapshotDto> Subscriptions { get; init; } = [];

        public Task<IReadOnlyList<StripePaidPaymentDto>> ListPaidPaymentIntentsAsync(
            DateTimeOffset windowStart,
            DateTimeOffset windowEnd,
            CancellationToken ct = default)
        {
            windowStart.Should().Be(WindowStart);
            windowEnd.Should().Be(WindowEnd);
            return Task.FromResult<IReadOnlyList<StripePaidPaymentDto>>(Payments);
        }

        public Task<StripeCheckoutSessionSnapshotDto?> FindCheckoutSessionForPaymentIntentAsync(
            string paymentIntentId,
            CancellationToken ct = default) =>
            Task.FromResult(Sessions.GetValueOrDefault(paymentIntentId));

        public Task<IReadOnlyList<StripeSubscriptionSnapshotDto>> ListSubscriptionsAsync(
            CancellationToken ct = default) =>
            Task.FromResult(Subscriptions);
    }

    private sealed class FakeRewriteCreditRepository(FakeUnitOfWork unitOfWork) : IRewriteCreditRepository
    {
        public List<RewriteCredit> AddedCredits { get; } = [];
        public List<int> AddTransactionIndexes { get; } = [];
        public HashSet<string> ExistingStripeEventIds { get; } = [];
        public Dictionary<string, IReadOnlyList<RewriteCredit>> ExistingCreditsByPaymentIntent { get; init; } = [];
        public HashSet<string> ThrowOnAddPaymentIntentIds { get; } = [];

        public Task AddAsync(RewriteCredit credit, CancellationToken ct = default)
        {
            if (credit.StripePaymentIntentId is not null &&
                ThrowOnAddPaymentIntentIds.Contains(credit.StripePaymentIntentId))
            {
                throw new InvalidOperationException("grant_write_failed");
            }

            AddedCredits.Add(credit);
            AddTransactionIndexes.Add(unitOfWork.CurrentTransactionIndex);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsByStripeEventIdAsync(string stripeEventId, CancellationToken ct = default) =>
            Task.FromResult(ExistingStripeEventIds.Contains(stripeEventId));

        public Task<IReadOnlyList<RewriteCredit>> ListByStripePaymentIntentIdAsync(
            string paymentIntentId,
            CancellationToken ct = default) =>
            Task.FromResult(ExistingCreditsByPaymentIntent.GetValueOrDefault(paymentIntentId) ?? []);

        public bool IsStripeEventIdWriteFailure(Exception exception) => false;

        public Task<RewriteCredit?> GetByIdAsync(Guid creditId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<RewriteCredit?> GetUsableForReservationAsync(
            Guid userId,
            DateTimeOffset now,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RewriteCredit>> ListByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<RewriteCredit?> GetByUserIdAndPaymentIntentIdAsync(
            Guid userId,
            string paymentIntentId,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<AdminRefundPaymentLookupDto?> GetRefundPaymentLookupAsync(
            Guid userId,
            string paymentIntentId,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AdminAccountingRevenueRowDto>> ListAccountingRevenueRowsAsync(
            DateTimeOffset fromInclusive,
            DateTimeOffset toExclusive,
            int pageSize,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RewriteCredit>> ListPurchaseCreditsForTurnoverAsync(
            DateTimeOffset windowStart,
            DateTimeOffset windowEnd,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RewriteCredit>> ListExpiryReminderCandidatesAsync(
            DateTimeOffset now,
            DateTimeOffset windowEnd,
            int batchSize,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task MarkExpiryReminderSentAsync(
            RewriteCredit credit,
            DateTimeOffset sentAt,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> ListUsableForReservationIdsAsync(
            Guid userId,
            DateTimeOffset now,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<int> TryConsumeForReservationAsync(Guid creditId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<int> ReleaseConsumedAsync(Guid creditId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeAppUserRepository : IAppUserRepository
    {
        public IReadOnlyList<AppUser> Users { get; init; } = [];
        public int FindForCheckoutCallCount { get; private set; }

        public Task<AppUser?> FindForStripeCheckoutAsync(
            string? externalAuthUserId,
            string? stripeCustomerId,
            CancellationToken ct = default)
        {
            FindForCheckoutCallCount++;
            return Task.FromResult(Users.FirstOrDefault(user =>
                (!string.IsNullOrWhiteSpace(externalAuthUserId) &&
                    user.ExternalAuthUserId == externalAuthUserId) ||
                (!string.IsNullOrWhiteSpace(stripeCustomerId) &&
                    user.StripeCustomerId == stripeCustomerId)));
        }

        public Task AddAsync(AppUser user, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<AppUser?> GetByExternalAuthUserIdAsync(string externalAuthUserId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<AppUser?> GetByStripeCustomerIdAsync(string stripeCustomerId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<AppUser?> FindByStripeCustomerOrSubscriptionAsync(
            string? stripeCustomerId,
            string? stripeSubscriptionId,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AppUser>> ListExpiredPaymentGraceBatchAsync(
            DateTimeOffset now,
            int batchSize,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AppUser>> ListPaymentGraceReminderCandidatesBatchAsync(
            DateTimeOffset now,
            int batchSize,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeAdminUserRepository(FakeUnitOfWork unitOfWork) : IAdminUserRepository
    {
        public List<AdminAuditLog> AuditLogs { get; } = [];
        public List<int> AuditTransactionIndexes { get; } = [];

        public Task AddAuditLogAsync(AdminAuditLog auditLog, CancellationToken ct = default)
        {
            AuditLogs.Add(auditLog);
            AuditTransactionIndexes.Add(unitOfWork.CurrentTransactionIndex);
            return Task.CompletedTask;
        }

        public Task<AdminUsersListDto> ListUsersAsync(
            int page,
            int pageSize,
            string? search,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<AdminUserDetailDto?> GetUserDetailAsync(Guid userId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<bool> UserExistsAsync(Guid userId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task AddCreditAsync(RewriteCredit credit, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<AdminSuspensionMutationDto?> SetUserSuspensionAsync(
            Guid userId,
            bool suspended,
            DateTimeOffset now,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<AdminRefundAuditDetailsDto?> FindRefundAuditDetailsAsync(
            Guid targetUserId,
            string paymentIntentId,
            long amount,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<AdminDeleteUserLookupDto?> GetDeleteUserLookupAsync(Guid userId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<bool> EraseUserAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeStripeReconciliationRunRepository(FakeUnitOfWork unitOfWork)
        : IStripeReconciliationRunRepository
    {
        public List<StripeReconciliationRun> Runs { get; } = [];
        public List<int> TransactionIndexes { get; } = [];

        public Task AddAsync(StripeReconciliationRun run, CancellationToken ct = default)
        {
            Runs.Add(run);
            TransactionIndexes.Add(unitOfWork.CurrentTransactionIndex);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOutboxMessageRepository(FakeUnitOfWork unitOfWork) : IOutboxMessageRepository
    {
        public List<OutboxMessage> Messages { get; } = [];
        public List<int> TransactionIndexes { get; } = [];

        public Task AddAsync(OutboxMessage message, CancellationToken ct = default)
        {
            Messages.Add(message);
            TransactionIndexes.Add(unitOfWork.CurrentTransactionIndex);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(
            DateTimeOffset now,
            string lockedBy,
            int batchSize,
            TimeSpan claimLease,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<OutboxMessage?> ClaimByIdAsync(
            Guid messageId,
            DateTimeOffset now,
            string lockedBy,
            TimeSpan claimLease,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task MarkSentAsync(Guid messageId, DateTimeOffset now, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<DateTimeOffset?> GetOldestIncompleteCreatedAtAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<OutboxMessageFailureInfo> MarkFailedAttemptAsync(
            Guid messageId,
            DateTimeOffset now,
            string error,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public int CurrentTransactionIndex { get; private set; }
        private int TransactionSequence { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCount++;
            return Task.FromResult(0);
        }

        public async Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken ct = default)
        {
            await ExecuteInTransactionAsync(operation, IsolationLevel.Serializable, ct);
        }

        public async Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> operation,
            IsolationLevel isolationLevel,
            CancellationToken ct = default)
        {
            TransactionSequence++;
            CurrentTransactionIndex = TransactionSequence;
            try
            {
                await operation(ct);
            }
            finally
            {
                CurrentTransactionIndex = 0;
            }
        }

        public async Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            IsolationLevel isolationLevel,
            CancellationToken ct = default)
        {
            TransactionSequence++;
            CurrentTransactionIndex = TransactionSequence;
            try
            {
                return await operation(ct);
            }
            finally
            {
                CurrentTransactionIndex = 0;
            }
        }

        public async Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            IsolationLevel isolationLevel,
            int maxAttempts,
            CancellationToken ct = default)
        {
            return await ExecuteInTransactionAsync(operation, isolationLevel, ct);
        }
    }
}
