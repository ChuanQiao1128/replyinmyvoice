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
}
