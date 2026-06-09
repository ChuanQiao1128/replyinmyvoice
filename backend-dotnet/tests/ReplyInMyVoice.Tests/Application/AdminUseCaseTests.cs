using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Admin;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests.Application;

public sealed class AdminUseCaseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-09T12:00:00Z");

    [Fact]
    public async Task GetAdminUsersAsync_filters_pages_and_includes_usage_credit_and_cost_summary()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var olderUser = await SeedUserAsync(
            fixture,
            "clerk_older",
            "older@example.com",
            Now.AddDays(-4));
        var middleUser = await SeedUserAsync(
            fixture,
            "clerk_middle",
            "middle@example.com",
            Now.AddDays(-3));
        var newestUser = await SeedUserAsync(
            fixture,
            "clerk_newest",
            "newest@example.com",
            Now.AddDays(-2));
        await SeedUserAsync(
            fixture,
            "clerk_other",
            "other@sample.test",
            Now.AddDays(-1));
        await SeedUsagePeriodAsync(fixture, newestUser.Id, "free:lifetime", quota: 3, used: 2, reserved: 1);
        await SeedUsagePeriodAsync(fixture, middleUser.Id, "free:lifetime", quota: 3, used: 1, reserved: 0);
        await SeedUsagePeriodAsync(fixture, olderUser.Id, "free:lifetime", quota: 3, used: 3, reserved: 0);
        await SeedCreditAsync(fixture, middleUser.Id, source: "ADMIN", amountGranted: 5, amountConsumed: 2);
        await SeedCostLogAsync(fixture, middleUser.Id, "middle-request", 0.025m);
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateUsersHandler(handlerDb);

        var result = await handler.HandleAsync(new GetAdminUsersQuery(2, 1, "example.com"));

        result.Page.Should().Be(2);
        result.PageSize.Should().Be(1);
        result.TotalCount.Should().Be(3);
        result.TotalPages.Should().Be(3);
        result.Users.Should().ContainSingle();
        result.Users[0].Id.Should().Be(middleUser.Id);
        result.Users[0].Email.Should().Be("middle@example.com");
        result.Users[0].UsedRewrites.Should().Be(1);
        result.Users[0].ReservedRewrites.Should().Be(0);
        result.Users[0].CreditRemaining.Should().Be(3);
        result.Users[0].CostToDateUsd.Should().Be(0.025m);
    }

    [Fact]
    public async Task GetAdminUserDetailAsync_returns_usage_credits_payments_subscription_and_cost()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await SeedUserAsync(
            fixture,
            "clerk_paid",
            "paid@example.com",
            Now.AddDays(-5),
            SubscriptionStatus.Active,
            stripeCustomerId: "cus_paid",
            stripeSubscriptionId: "sub_paid",
            currentPeriodEnd: Now.AddDays(30));
        await SeedUsagePeriodAsync(
            fixture,
            user.Id,
            "paid:sub_paid",
            quota: 90,
            used: 7,
            reserved: 2,
            start: Now.AddDays(-30),
            end: Now.AddDays(30));
        await SeedCreditAsync(
            fixture,
            user.Id,
            source: "PURCHASE",
            amountGranted: 20,
            amountConsumed: 4,
            stripeEventId: "evt_paid",
            stripePaymentIntentId: "pi_paid",
            stripeSku: "quick_pack",
            stripeAmountTotal: 1200,
            stripeCurrency: "nzd",
            stripeReceiptUrl: "https://pay.stripe.com/receipts/test_receipt");
        await SeedCreditAsync(fixture, user.Id, source: "PROMO", amountGranted: 5, amountConsumed: 1);
        await SeedCostLogAsync(fixture, user.Id, "paid-request-1", 0.0123m);
        await SeedCostLogAsync(fixture, user.Id, "paid-request-2", 0.0100m);
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateDetailHandler(handlerDb);

        var detail = await handler.HandleAsync(new GetAdminUserDetailQuery(user.Id));

        detail.Should().NotBeNull();
        detail!.Id.Should().Be(user.Id);
        detail.Subscription.Status.Should().Be("Active");
        detail.Subscription.StripeCustomerId.Should().Be("cus_paid");
        detail.Subscription.StripeSubscriptionId.Should().Be("sub_paid");
        detail.Usage.Should().ContainSingle(x => x.PeriodKey == "paid:sub_paid");
        detail.Usage[0].Used.Should().Be(7);
        detail.Usage[0].Reserved.Should().Be(2);
        detail.Credits.Should().HaveCount(2);
        detail.Credits.Sum(x => x.Remaining).Should().Be(20);
        detail.Payments.Should().ContainSingle();
        detail.Payments[0].PaymentIntentId.Should().Be("pi_paid");
        detail.Payments[0].AmountTotal.Should().Be(1200);
        detail.Payments[0].Currency.Should().Be("nzd");
        detail.Payments[0].ReceiptUrl.Should().Be("https://pay.stripe.com/receipts/test_receipt");
        detail.CostToDateUsd.Should().Be(0.0223m);
    }

    [Fact]
    public async Task GetAdminStatsAsync_returns_aggregate_usage_payments_cost_and_tax_summary()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var paidUser = await SeedUserAsync(
            fixture,
            "clerk_paid_stats",
            "paid-stats@example.com",
            Now.AddDays(-6),
            SubscriptionStatus.Active);
        var freeUser = await SeedUserAsync(
            fixture,
            "clerk_free_stats",
            "free-stats@example.com",
            Now.AddDays(-5));
        await SeedUsagePeriodAsync(fixture, paidUser.Id, "paid:stats", quota: 90, used: 8, reserved: 1);
        await SeedUsagePeriodAsync(fixture, freeUser.Id, "free:lifetime", quota: 3, used: 2, reserved: 0);
        await SeedCreditAsync(
            fixture,
            paidUser.Id,
            source: "PURCHASE",
            amountGranted: 10,
            amountConsumed: 3,
            stripePaymentIntentId: "pi_stats",
            stripeAmountTotal: 900,
            stripeCurrency: "nzd",
            grantedAt: Now.AddDays(-10));
        await SeedCreditAsync(
            fixture,
            paidUser.Id,
            source: "PURCHASE",
            amountGranted: 30,
            amountConsumed: 0,
            stripePaymentIntentId: "pi_stats_old",
            stripeAmountTotal: 6900,
            stripeCurrency: "nzd",
            grantedAt: Now.AddMonths(-13));
        await SeedCostLogAsync(fixture, paidUser.Id, "stats-request-1", 0.030m);
        await SeedCostLogAsync(fixture, freeUser.Id, "stats-request-2", 0.020m);
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateStatsHandler(handlerDb);

        var stats = await handler.HandleAsync(new GetAdminStatsQuery(Now));

        stats.TotalUsers.Should().Be(2);
        stats.PaidUsers.Should().Be(1);
        stats.FreeUsers.Should().Be(1);
        stats.UsageUsed.Should().Be(10);
        stats.UsageReserved.Should().Be(1);
        stats.CreditRemaining.Should().Be(37);
        stats.PaymentCount.Should().Be(2);
        stats.PaymentAmountTotal.Should().Be(7800);
        stats.CostToDateUsd.Should().Be(0.050m);
        stats.GstTurnover.Currency.Should().Be("nzd");
        stats.GstTurnover.GrossAmountTotal.Should().Be(900);
        stats.GstTurnover.Warning.Should().BeNull();
    }

    [Fact]
    public async Task GrantCreditsAsync_creates_admin_credit_and_audit_log()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateGrantHandler(handlerDb);

        var result = await handler.HandleAsync(new GrantCreditsCommand(
            " admin-owner-oid ",
            " owner@example.com ",
            user.Id,
            7,
            " support adjustment ",
            Now));

        result.Kind.Should().Be(AdminCreditGrantResultKind.Success);
        result.Response.Should().NotBeNull();
        result.Response!.TargetUserId.Should().Be(user.Id);
        result.Response.Source.Should().Be("ADMIN");
        result.Response.AmountGranted.Should().Be(7);
        result.Response.AmountConsumed.Should().Be(0);
        result.Response.Remaining.Should().Be(7);
        result.Response.GrantedAt.Should().Be(Now);
        result.Response.ExpiresAt.Should().Be(Now.AddDays(90));

        await using var verifyDb = fixture.CreateContext();
        var credit = await verifyDb.RewriteCredits.SingleAsync();
        credit.Id.Should().Be(result.Response.CreditId);
        credit.UserId.Should().Be(user.Id);
        credit.Source.Should().Be("ADMIN");
        credit.AmountGranted.Should().Be(7);
        credit.OriginalAmountGranted.Should().Be(7);
        credit.AmountConsumed.Should().Be(0);

        var audit = await verifyDb.AdminAuditLogs.SingleAsync();
        audit.AdminExternalAuthUserId.Should().Be("admin-owner-oid");
        audit.AdminEmail.Should().Be("owner@example.com");
        audit.Action.Should().Be("grant_credits");
        audit.TargetUserId.Should().Be(user.Id);
        audit.CreatedAt.Should().Be(Now);
        using var details = JsonDocument.Parse(audit.DetailsJson!);
        details.RootElement.GetProperty("creditId").GetGuid().Should().Be(credit.Id);
        details.RootElement.GetProperty("amountGranted").GetInt32().Should().Be(7);
        details.RootElement.GetProperty("reason").GetString().Should().Be("support adjustment");
    }

    [Fact]
    public async Task GrantCreditsAsync_missing_user_returns_not_found_without_side_effects()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateGrantHandler(handlerDb);

        var result = await handler.HandleAsync(new GrantCreditsCommand(
            "admin-owner-oid",
            "owner@example.com",
            Guid.NewGuid(),
            7,
            "support adjustment",
            Now));

        result.Kind.Should().Be(AdminCreditGrantResultKind.UserNotFound);
        result.Detail.Should().Be("No user exists for the requested id.");

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteCredits.CountAsync()).Should().Be(0);
        (await verifyDb.AdminAuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteAdminUserAsync_erases_target_and_writes_audit_log()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await SeedUsagePeriodAsync(fixture, user.Id, "free:lifetime", quota: 3, used: 2, reserved: 1);
        await SeedCreditAsync(fixture, user.Id, source: "PROMO", amountGranted: 5, amountConsumed: 1);
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateDeleteHandler(handlerDb);

        var result = await handler.HandleAsync(new DeleteAdminUserCommand(
            "admin-owner-oid",
            "owner@example.com",
            user.Id,
            Now));

        result.Kind.Should().Be(AdminDeleteUserResultKind.Success);
        result.Response.Should().NotBeNull();
        result.Response!.UserId.Should().Be(user.Id);
        result.Response.Status.Should().Be("erased");

        await using var verifyDb = fixture.CreateContext();
        var erasedUser = await verifyDb.AppUsers.SingleAsync(x => x.Id == user.Id);
        erasedUser.ExternalAuthUserId.Should().StartWith("erased:");
        erasedUser.Email.Should().BeNull();
        erasedUser.SubscriptionStatus.Should().Be(SubscriptionStatus.Canceled);

        var usagePeriod = await verifyDb.UsagePeriods.SingleAsync(x => x.UserId == user.Id);
        usagePeriod.PeriodKey.Should().StartWith("erased:");
        usagePeriod.QuotaLimit.Should().Be(0);
        usagePeriod.UsedCount.Should().Be(0);
        usagePeriod.ReservedCount.Should().Be(0);

        var credit = await verifyDb.RewriteCredits.SingleAsync(x => x.UserId == user.Id);
        credit.Source.Should().Be("ERASED");
        credit.AmountGranted.Should().Be(0);
        credit.AmountConsumed.Should().Be(0);

        var audit = await verifyDb.AdminAuditLogs.SingleAsync();
        audit.AdminExternalAuthUserId.Should().Be("admin-owner-oid");
        audit.AdminEmail.Should().Be("owner@example.com");
        audit.Action.Should().Be("user.delete");
        audit.TargetUserId.Should().Be(user.Id);
        audit.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task DeleteAdminUserAsync_forbids_self_delete_without_side_effects()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateDeleteHandler(handlerDb);

        var result = await handler.HandleAsync(new DeleteAdminUserCommand(
            user.ExternalAuthUserId,
            user.Email,
            user.Id,
            Now));

        result.Kind.Should().Be(AdminDeleteUserResultKind.Forbidden);
        result.Detail.Should().Be("an admin cannot delete their own account from the console");

        await using var verifyDb = fixture.CreateContext();
        var storedUser = await verifyDb.AppUsers.SingleAsync(x => x.Id == user.Id);
        storedUser.ExternalAuthUserId.Should().Be(user.ExternalAuthUserId);
        storedUser.Email.Should().Be(user.Email);
        (await verifyDb.AdminAuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteAdminUserAsync_missing_user_returns_not_found_without_side_effects()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateDeleteHandler(handlerDb);

        var result = await handler.HandleAsync(new DeleteAdminUserCommand(
            "admin-owner-oid",
            "owner@example.com",
            Guid.NewGuid(),
            Now));

        result.Kind.Should().Be(AdminDeleteUserResultKind.UserNotFound);
        result.Detail.Should().Be("No user exists for the requested id.");

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.AdminAuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetBillingSupportQueueAsync_returns_open_requests_in_created_order_with_user_details()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await SeedUserAsync(
            fixture,
            "clerk_support_queue",
            "support-queue@example.com",
            Now.AddDays(-5));
        var newer = await SeedBillingSupportRequestAsync(
            fixture,
            user.Id,
            BillingSupportRequestType.Refund,
            "pi_newer",
            "Please review this newer payment.",
            BillingSupportRequestStatus.Open,
            Now.AddHours(-1));
        var older = await SeedBillingSupportRequestAsync(
            fixture,
            user.Id,
            BillingSupportRequestType.BillingQuestion,
            null,
            "Please review this older billing question.",
            BillingSupportRequestStatus.Open,
            Now.AddHours(-2));
        await SeedBillingSupportRequestAsync(
            fixture,
            user.Id,
            BillingSupportRequestType.Refund,
            "pi_resolved",
            "This one is already complete.",
            BillingSupportRequestStatus.Resolved,
            Now.AddHours(-3),
            resolvedAt: Now.AddHours(-1));
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateBillingSupportQueueHandler(handlerDb);

        var queue = await handler.HandleAsync(new GetBillingSupportQueueQuery());

        queue.Select(x => x.Id).Should().Equal(older, newer);
        queue[0].UserEmail.Should().Be("support-queue@example.com");
        queue[0].ExternalAuthUserId.Should().Be("clerk_support_queue");
        queue[0].Type.Should().Be("billing-question");
        queue[0].Status.Should().Be("open");
    }

    [Fact]
    public async Task ResolveBillingSupportRequestAsync_marks_open_request_resolved_and_audits_once()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await SeedUserAsync(
            fixture,
            "clerk_support_resolve",
            "support-resolve@example.com",
            Now.AddDays(-5));
        var requestId = await SeedBillingSupportRequestAsync(
            fixture,
            user.Id,
            BillingSupportRequestType.Refund,
            "pi_support_resolve",
            "Please review this payment.",
            BillingSupportRequestStatus.Open,
            Now.AddHours(-2));
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateResolveBillingSupportHandler(handlerDb);
        var command = new ResolveBillingSupportRequestCommand(
            " admin-owner-oid ",
            " owner@example.com ",
            requestId,
            Now);

        var first = await handler.HandleAsync(command);
        var second = await handler.HandleAsync(command);

        first.Should().NotBeNull();
        first!.Id.Should().Be(requestId);
        first.Status.Should().Be("resolved");
        first.ResolvedAt.Should().Be(Now);
        second.Should().NotBeNull();
        second!.Status.Should().Be("resolved");

        await using var verifyDb = fixture.CreateContext();
        var stored = await verifyDb.BillingSupportRequests.SingleAsync(x => x.Id == requestId);
        stored.Status.Should().Be(BillingSupportRequestStatus.Resolved);
        stored.ResolvedAt.Should().Be(Now);
        var audit = await verifyDb.AdminAuditLogs.SingleAsync();
        audit.AdminExternalAuthUserId.Should().Be("admin-owner-oid");
        audit.AdminEmail.Should().Be("owner@example.com");
        audit.Action.Should().Be("resolve_billing_support_request");
        audit.TargetUserId.Should().Be(user.Id);
        using var details = JsonDocument.Parse(audit.DetailsJson!);
        details.RootElement.GetProperty("requestId").GetGuid().Should().Be(requestId);
        details.RootElement.GetProperty("type").GetString().Should().Be("refund");
        details.RootElement.GetProperty("relatedPaymentIntentId").GetString().Should().Be("pi_support_resolve");
    }

    [Fact]
    public async Task ExportAccountingRevenueAsync_returns_payment_credit_rows_for_date_range()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await SeedUserAsync(
            fixture,
            "clerk_accounting_use_case",
            "accounting-use-case@example.com",
            Now.AddDays(-5));
        await SeedCreditAsync(
            fixture,
            user.Id,
            source: "PURCHASE",
            amountGranted: 10,
            amountConsumed: 3,
            stripePaymentIntentId: "pi_revenue_in_range",
            stripeSku: "quick_pack",
            stripeAmountTotal: 250,
            stripeCurrency: "nzd",
            grantedAt: Now.AddDays(-1));
        await SeedCreditAsync(
            fixture,
            user.Id,
            source: "PURCHASE",
            amountGranted: 30,
            amountConsumed: 0,
            stripePaymentIntentId: "pi_revenue_outside",
            stripeSku: "value_pack",
            stripeAmountTotal: 690,
            stripeCurrency: "nzd",
            grantedAt: Now.AddMonths(-2));
        await SeedCreditAsync(
            fixture,
            user.Id,
            source: "ADMIN",
            amountGranted: 5,
            amountConsumed: 0,
            grantedAt: Now.AddDays(-1));
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateAccountingRevenueHandler(handlerDb);

        var export = await handler.HandleAsync(new ExportAccountingRevenueQuery(
            Now.AddDays(-7),
            Now.AddDays(1)));

        export.Rows.Should().ContainSingle();
        var row = export.Rows[0];
        row.UserId.Should().Be(user.Id);
        row.PaymentIntentId.Should().Be("pi_revenue_in_range");
        row.Sku.Should().Be("quick_pack");
        row.AmountTotal.Should().Be(250);
        row.Currency.Should().Be("nzd");
        row.AmountGranted.Should().Be(10);
        row.AmountConsumed.Should().Be(3);
        row.CreditsRemaining.Should().Be(7);
    }

    [Fact]
    public async Task SetUserSuspensionAsync_toggles_user_suspension_and_writes_audit_logs()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateSuspensionHandler(handlerDb);

        var suspend = await handler.HandleAsync(new SetUserSuspensionCommand(
            " admin-owner-oid ",
            " owner@example.com ",
            user.Id,
            Suspended: true,
            Now));
        var unsuspend = await handler.HandleAsync(new SetUserSuspensionCommand(
            "admin-owner-oid",
            "owner@example.com",
            user.Id,
            Suspended: false,
            Now.AddMinutes(1)));

        suspend.Kind.Should().Be(AdminSuspensionResultKind.Success);
        suspend.Response!.TargetUserId.Should().Be(user.Id);
        suspend.Response.Suspended.Should().BeTrue();
        suspend.Response.SuspendedAt.Should().Be(Now);
        unsuspend.Kind.Should().Be(AdminSuspensionResultKind.Success);
        unsuspend.Response!.Suspended.Should().BeFalse();
        unsuspend.Response.SuspendedAt.Should().BeNull();

        await using var verifyDb = fixture.CreateContext();
        var storedUser = await verifyDb.AppUsers.SingleAsync(x => x.Id == user.Id);
        storedUser.SuspendedAt.Should().BeNull();
        var audits = (await verifyDb.AdminAuditLogs.ToListAsync())
            .OrderBy(x => x.CreatedAt)
            .ToList();
        audits.Select(x => x.Action).Should().Equal("suspend_user", "unsuspend_user");
        audits.Should().OnlyContain(x =>
            x.TargetUserId == user.Id &&
            x.AdminExternalAuthUserId == "admin-owner-oid" &&
            x.AdminEmail == "owner@example.com");
    }

    [Fact]
    public async Task IssueRefundAsync_uses_refund_port_and_returns_existing_refund_without_second_call()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await SeedCreditAsync(
            fixture,
            user.Id,
            source: "PURCHASE",
            amountGranted: 10,
            amountConsumed: 0,
            stripePaymentIntentId: "pi_refund_use_case",
            stripeAmountTotal: 1200,
            stripeCurrency: "nzd");
        var refundClient = new FakeStripeRefundClient("re_use_case");
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateRefundHandler(handlerDb, refundClient);
        var command = new IssueRefundCommand(
            " admin-owner-oid ",
            " owner@example.com ",
            user.Id,
            " pi_refund_use_case ",
            1200,
            null);

        var first = await handler.HandleAsync(command);
        var second = await handler.HandleAsync(command);

        first.Kind.Should().Be(AdminRefundResultKind.Success);
        first.Response!.TargetUserId.Should().Be(user.Id);
        first.Response.PaymentIntentId.Should().Be("pi_refund_use_case");
        first.Response.Amount.Should().Be(1200);
        first.Response.Currency.Should().Be("nzd");
        first.Response.RefundId.Should().Be("re_use_case");
        first.Response.AlreadyRefunded.Should().BeFalse();
        second.Kind.Should().Be(AdminRefundResultKind.Success);
        second.Response!.AlreadyRefunded.Should().BeTrue();
        second.Response.RefundId.Should().Be("re_use_case");
        refundClient.Calls.Should().ContainSingle();
        refundClient.Calls[0].IdempotencyKey.Should().Be(
            $"admin-refund:{user.Id:N}:pi_refund_use_case:1200");

        await using var verifyDb = fixture.CreateContext();
        var audit = await verifyDb.AdminAuditLogs.SingleAsync();
        audit.AdminExternalAuthUserId.Should().Be("admin-owner-oid");
        audit.AdminEmail.Should().Be("owner@example.com");
        audit.Action.Should().Be("refund");
        audit.TargetUserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task IssueRefundAsync_provider_failure_writes_no_audit_log()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await SeedCreditAsync(
            fixture,
            user.Id,
            source: "PURCHASE",
            amountGranted: 10,
            amountConsumed: 0,
            stripePaymentIntentId: "pi_refund_failure_use_case",
            stripeAmountTotal: 1200,
            stripeCurrency: "nzd");
        var refundClient = new FakeStripeRefundClient("re_failure")
        {
            RefundError = new TaskCanceledException("simulated refund timeout"),
        };
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateRefundHandler(handlerDb, refundClient);

        var act = () => handler.HandleAsync(new IssueRefundCommand(
            "admin-owner-oid",
            "owner@example.com",
            user.Id,
            "pi_refund_failure_use_case",
            1200,
            "nzd"));

        await act.Should().ThrowAsync<TaskCanceledException>()
            .WithMessage("*simulated refund timeout*");

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.AdminAuditLogs.CountAsync()).Should().Be(0);
    }

    private static GetAdminUsersHandler CreateUsersHandler(AppDbContext db) =>
        new(new AdminUserRepository(db));

    private static GetAdminUserDetailHandler CreateDetailHandler(AppDbContext db) =>
        new(new AdminUserRepository(db));

    private static GetAdminStatsHandler CreateStatsHandler(AppDbContext db) =>
        new(new AdminStatsRepository(db), new StaticTaxTurnoverSettingsProvider());

    private static GrantCreditsHandler CreateGrantHandler(AppDbContext db) =>
        new(new AdminUserRepository(db), new UnitOfWork(db));

    private static DeleteAdminUserHandler CreateDeleteHandler(AppDbContext db) =>
        new(new AdminUserRepository(db), new UnitOfWork(db), new RecordingSubscriptionCancellationService());

    private static GetBillingSupportQueueHandler CreateBillingSupportQueueHandler(AppDbContext db) =>
        new(new BillingSupportRequestRepository(db));

    private static ResolveBillingSupportRequestHandler CreateResolveBillingSupportHandler(AppDbContext db) =>
        new(new BillingSupportRequestRepository(db), new AdminUserRepository(db), new UnitOfWork(db));

    private static ExportAccountingRevenueHandler CreateAccountingRevenueHandler(AppDbContext db) =>
        new(new RewriteCreditRepository(db));

    private static SetUserSuspensionHandler CreateSuspensionHandler(AppDbContext db) =>
        new(new AdminUserRepository(db), new UnitOfWork(db));

    private static IssueRefundHandler CreateRefundHandler(
        AppDbContext db,
        IStripeRefundClient refundClient) =>
        new(new AdminUserRepository(db), new RewriteCreditRepository(db), refundClient, new UnitOfWork(db));

    private static async Task<AppUser> SeedUserAsync(
        DbFixture fixture,
        string externalAuthUserId,
        string email,
        DateTimeOffset createdAt,
        SubscriptionStatus subscriptionStatus = SubscriptionStatus.Inactive,
        string? stripeCustomerId = null,
        string? stripeSubscriptionId = null,
        DateTimeOffset? currentPeriodEnd = null)
    {
        await using var db = fixture.CreateContext();
        var user = new AppUser
        {
            ExternalAuthUserId = externalAuthUserId,
            Email = email,
            SubscriptionStatus = subscriptionStatus,
            StripeCustomerId = stripeCustomerId,
            StripeSubscriptionId = stripeSubscriptionId,
            CurrentPeriodEnd = currentPeriodEnd,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task SeedUsagePeriodAsync(
        DbFixture fixture,
        Guid userId,
        string periodKey,
        int quota,
        int used,
        int reserved,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null)
    {
        await using var db = fixture.CreateContext();
        db.UsagePeriods.Add(new UsagePeriod
        {
            UserId = userId,
            PeriodKey = periodKey,
            QuotaLimit = quota,
            UsedCount = used,
            ReservedCount = reserved,
            PeriodStart = start,
            PeriodEnd = end,
            CreatedAt = start ?? Now,
            UpdatedAt = start ?? Now,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedCreditAsync(
        DbFixture fixture,
        Guid userId,
        string source,
        int amountGranted,
        int amountConsumed,
        string? stripeEventId = null,
        string? stripePaymentIntentId = null,
        string? stripeSku = null,
        long? stripeAmountTotal = null,
        string? stripeCurrency = null,
        string? stripeReceiptUrl = null,
        DateTimeOffset? grantedAt = null)
    {
        await using var db = fixture.CreateContext();
        db.RewriteCredits.Add(new RewriteCredit
        {
            UserId = userId,
            Source = source,
            AmountGranted = amountGranted,
            AmountConsumed = amountConsumed,
            GrantedAt = grantedAt ?? Now.AddDays(-1),
            StripeEventId = stripeEventId,
            StripePaymentIntentId = stripePaymentIntentId,
            StripeSku = stripeSku,
            StripeAmountTotal = stripeAmountTotal,
            StripeCurrency = stripeCurrency,
            StripeReceiptUrl = stripeReceiptUrl,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedCostLogAsync(
        DbFixture fixture,
        Guid userId,
        string requestId,
        decimal totalCost)
    {
        await using var db = fixture.CreateContext();
        db.RewriteCostLogs.Add(new RewriteCostLog
        {
            UserId = userId,
            RequestId = requestId,
            StrategyVersion = "test",
            Scenario = "email",
            TonePreset = "warm",
            Status = "succeeded",
            StartedAt = Now.AddMinutes(-1),
            FinishedAt = Now,
            TotalEstimatedCostUsd = totalCost,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedBillingSupportRequestAsync(
        DbFixture fixture,
        Guid userId,
        BillingSupportRequestType type,
        string? paymentIntentId,
        string message,
        BillingSupportRequestStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? resolvedAt = null)
    {
        await using var db = fixture.CreateContext();
        var request = new BillingSupportRequest
        {
            UserId = userId,
            Type = type,
            RelatedPaymentIntentId = paymentIntentId,
            Message = message,
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = resolvedAt ?? createdAt,
            ResolvedAt = resolvedAt,
        };
        db.BillingSupportRequests.Add(request);
        await db.SaveChangesAsync();
        return request.Id;
    }

    private sealed class StaticTaxTurnoverSettingsProvider : ITaxTurnoverSettingsProvider
    {
        public TaxTurnoverSettings GetSettings() =>
            new(
                RegistrationThresholdAmountTotal: 6_000_000,
                WarningFraction: 0.80m);
    }

    private sealed class RecordingSubscriptionCancellationService : IStripeSubscriptionCancellationService
    {
        public Task CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeStripeRefundClient(string refundId) : IStripeRefundClient
    {
        public List<StripeRefundRequest> Calls { get; } = [];

        public Exception? RefundError { get; init; }

        public Task<StripeRefundResult> RefundPaymentAsync(
            StripeRefundRequest request,
            CancellationToken ct = default)
        {
            Calls.Add(request);
            if (RefundError is not null)
            {
                throw RefundError;
            }

            return Task.FromResult(new StripeRefundResult(
                refundId,
                request.PaymentIntentId,
                request.Amount,
                request.Currency,
                "succeeded"));
        }
    }
}
