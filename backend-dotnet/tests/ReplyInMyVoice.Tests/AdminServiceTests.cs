using System.Net;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class AdminServiceTests
{
    [Fact]
    public async Task AdminUsersList_ReturnsPagedUsersForAdmin()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var olderUser = await SeedUserAsync(fixture, "clerk_older", "older@example.com", DateTimeOffset.Parse("2026-05-01T00:00:00Z"));
        var middleUser = await SeedUserAsync(fixture, "clerk_middle", "middle@example.com", DateTimeOffset.Parse("2026-05-02T00:00:00Z"));
        var newestUser = await SeedUserAsync(fixture, "clerk_newest", "newest@example.com", DateTimeOffset.Parse("2026-05-03T00:00:00Z"));
        await SeedUsagePeriodAsync(fixture, newestUser.Id, "free:lifetime", quota: 3, used: 2, reserved: 1);
        await SeedUsagePeriodAsync(fixture, middleUser.Id, "free:lifetime", quota: 3, used: 1, reserved: 0);
        await SeedUsagePeriodAsync(fixture, olderUser.Id, "free:lifetime", quota: 3, used: 3, reserved: 0);
        await SeedCostLogAsync(fixture, newestUser.Id, "newest-request", 0.015m);
        await SeedCostLogAsync(fixture, middleUser.Id, "middle-request", 0.025m);

        var function = CreateFunction(fixture);
        var request = CreateRequest("admin-owner-oid", "owner@example.com");
        request.QueryString = new QueryString("?page=1&pageSize=2");

        var result = await function.ListUsers(request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be((int)HttpStatusCode.OK);
        var response = okResult.Value.Should().BeOfType<AdminUsersListResponse>().Subject;
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(2);
        response.TotalCount.Should().Be(3);
        response.TotalPages.Should().Be(2);
        response.Users.Select(x => x.Email).Should().Equal("newest@example.com", "middle@example.com");
        response.Users[0].UsedRewrites.Should().Be(2);
        response.Users[0].ReservedRewrites.Should().Be(1);
        response.Users[0].CostToDateUsd.Should().Be(0.015m);
        response.Users[1].CostToDateUsd.Should().Be(0.025m);
    }

    [Fact]
    public async Task AdminUsersList_NonAdminGetsForbidden()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var function = CreateFunction(fixture);
        var request = CreateRequest("regular-user-oid", "regular@example.com");

        var result = await function.ListUsers(request, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task AdminAccountingRevenueCsv_NonAdminGetsForbidden()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var function = CreateFunction(fixture);
        var request = CreateRequest("regular-user-oid", "regular@example.com");
        request.QueryString = new QueryString("?from=2026-05-01T00:00:00Z&to=2026-06-01T00:00:00Z");

        var result = await function.ExportAccountingRevenueCsv(request, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task AdminAccountingRevenueCsv_ReturnsSeededRevenueRowsWithEscapingAndDateRange()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await SeedUserAsync(
            fixture,
            "clerk_accounting",
            "accounting@example.com",
            DateTimeOffset.Parse("2026-05-01T00:00:00Z"));
        await SeedCreditAsync(
            fixture,
            user.Id,
            source: "PURCHASE",
            amountGranted: 10,
            amountConsumed: 2,
            stripePaymentIntentId: "pi_\"csv\"",
            stripeSku: "quick,\"starter\"\npack",
            stripeAmountTotal: 250,
            stripeCurrency: "nzd",
            grantedAt: DateTimeOffset.Parse("2026-05-12T08:30:00Z"));
        await SeedCreditAsync(
            fixture,
            user.Id,
            source: "PURCHASE",
            amountGranted: 30,
            amountConsumed: 0,
            stripePaymentIntentId: "pi_outside",
            stripeSku: "value_pack",
            stripeAmountTotal: 690,
            stripeCurrency: "nzd",
            grantedAt: DateTimeOffset.Parse("2026-04-30T23:59:59Z"));
        await SeedCreditAsync(
            fixture,
            user.Id,
            source: "ADMIN",
            amountGranted: 5,
            amountConsumed: 0,
            grantedAt: DateTimeOffset.Parse("2026-05-13T00:00:00Z"));

        var function = CreateFunction(fixture);
        var request = CreateRequest("admin-owner-oid", "owner@example.com");
        request.QueryString = new QueryString("?from=2026-05-01T00:00:00Z&to=2026-06-01T00:00:00Z");
        request.HttpContext.Response.Body = new MemoryStream();

        var result = await function.ExportAccountingRevenueCsv(request, CancellationToken.None);

        result.Should().BeOfType<EmptyResult>();
        request.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        request.HttpContext.Response.ContentType.Should().Be("text/csv; charset=utf-8");
        var csv = await ReadResponseBodyAsync(request.HttpContext.Response.Body);
        var expectedRow =
            $"2026-05-12T08:30:00.0000000+00:00,{user.Id},\"quick,\"\"starter\"\"\npack\",250,nzd,\"pi_\"\"csv\"\"\",,10,2,8";
        csv.Should().Be(
            "date,userRef,sku,amount,currency,paymentIntent,receiptUrl,creditsGranted,creditsConsumed,creditsRemaining\r\n" +
            expectedRow +
            "\r\n");
    }

    [Fact]
    public async Task AdminAccountingRevenueCsv_UsesPagedRevenueWriterForLargeRanges()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await SeedUserAsync(
            fixture,
            "clerk_large_export",
            "large-export@example.com",
            DateTimeOffset.Parse("2026-05-01T00:00:00Z"));
        for (var i = 0; i < 7; i++)
        {
            await SeedCreditAsync(
                fixture,
                user.Id,
                source: "PURCHASE",
                amountGranted: 10,
                amountConsumed: i % 3,
                stripePaymentIntentId: $"pi_large_{i}",
                stripeSku: $"quick_pack_{i}",
                stripeAmountTotal: 250,
                stripeCurrency: "nzd",
                grantedAt: DateTimeOffset.Parse("2026-05-01T00:00:00Z").AddDays(i));
        }

        await using var output = new MemoryStream();
        var service = new AdminService(fixture.CreateContext);

        await service.WriteAccountingRevenueCsvAsync(
            output,
            DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            pageSize: 2,
            CancellationToken.None);

        var csv = Encoding.UTF8.GetString(output.ToArray());
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(8);
        for (var i = 0; i < 7; i++)
        {
            lines.Should().Contain(line => line.Contains($"pi_large_{i}", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task AdminUserDetailIncludesCost_ReturnsUsageCreditsPaymentsSubscriptionAndCostToDate()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await SeedUserAsync(
            fixture,
            "clerk_paid",
            "paid@example.com",
            DateTimeOffset.Parse("2026-05-04T00:00:00Z"),
            SubscriptionStatus.Active,
            stripeCustomerId: "cus_paid",
            stripeSubscriptionId: "sub_paid",
            currentPeriodEnd: DateTimeOffset.Parse("2026-06-04T00:00:00Z"));
        var otherUser = await SeedUserAsync(fixture, "clerk_other", "other@example.com", DateTimeOffset.Parse("2026-05-05T00:00:00Z"));

        await SeedUsagePeriodAsync(
            fixture,
            user.Id,
            "paid:sub_paid:2026-06-04T00:00:00.0000000+00:00",
            quota: 90,
            used: 7,
            reserved: 2,
            start: DateTimeOffset.Parse("2026-05-04T00:00:00Z"),
            end: DateTimeOffset.Parse("2026-06-04T00:00:00Z"));
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
            stripeCurrency: "nzd");
        await SeedCreditAsync(
            fixture,
            user.Id,
            source: "PROMO",
            amountGranted: 5,
            amountConsumed: 1);
        await SeedCostLogAsync(fixture, user.Id, "paid-request-1", 0.0123m);
        await SeedCostLogAsync(fixture, user.Id, "paid-request-2", 0.0100m);
        await SeedCostLogAsync(fixture, otherUser.Id, "other-request", 0.0999m);

        var function = CreateFunction(fixture);
        var request = CreateRequest("admin-owner-oid", "owner@example.com");

        var result = await function.GetUserDetail(request, user.Id.ToString(), CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var detail = okResult.Value.Should().BeOfType<AdminUserDetailResponse>().Subject;
        detail.Id.Should().Be(user.Id);
        detail.Email.Should().Be("paid@example.com");
        detail.Subscription.Status.Should().Be("Active");
        detail.Subscription.StripeCustomerId.Should().Be("cus_paid");
        detail.Subscription.StripeSubscriptionId.Should().Be("sub_paid");
        detail.Usage.Should().ContainSingle(x => x.PeriodKey.StartsWith("paid:sub_paid", StringComparison.Ordinal));
        detail.Usage[0].Used.Should().Be(7);
        detail.Usage[0].Reserved.Should().Be(2);
        detail.Credits.Should().HaveCount(2);
        detail.Credits.Sum(x => x.Remaining).Should().Be(20);
        detail.Payments.Should().ContainSingle();
        detail.Payments[0].PaymentIntentId.Should().Be("pi_paid");
        detail.Payments[0].AmountTotal.Should().Be(1200);
        detail.Payments[0].Currency.Should().Be("nzd");
        detail.CostToDateUsd.Should().Be(0.0223m);
    }

    [Fact]
    public async Task AdminStats_ReturnsAggregateUsagePaymentsAndCost()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var paidUser = await SeedUserAsync(
            fixture,
            "clerk_paid_stats",
            "paid-stats@example.com",
            DateTimeOffset.Parse("2026-05-06T00:00:00Z"),
            SubscriptionStatus.Active);
        var freeUser = await SeedUserAsync(fixture, "clerk_free_stats", "free-stats@example.com", DateTimeOffset.Parse("2026-05-07T00:00:00Z"));
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
            stripeCurrency: "nzd");
        await SeedCostLogAsync(fixture, paidUser.Id, "stats-request-1", 0.030m);
        await SeedCostLogAsync(fixture, freeUser.Id, "stats-request-2", 0.020m);

        var function = CreateFunction(fixture);
        var request = CreateRequest("admin-owner-oid", "owner@example.com");

        var result = await function.GetStats(request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var stats = okResult.Value.Should().BeOfType<AdminStatsResponse>().Subject;
        stats.TotalUsers.Should().Be(2);
        stats.PaidUsers.Should().Be(1);
        stats.UsageUsed.Should().Be(10);
        stats.UsageReserved.Should().Be(1);
        stats.CreditRemaining.Should().Be(7);
        stats.PaymentCount.Should().Be(1);
        stats.PaymentAmountTotal.Should().Be(900);
        stats.CostToDateUsd.Should().Be(0.050m);
    }

    private static AdminHttpFunctions CreateFunction(DbFixture fixture) =>
        new(BuildConfiguration("admin-owner-oid, owner@example.com"), fixture.CreateContext);

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
            CreatedAt = start ?? DateTimeOffset.UtcNow,
            UpdatedAt = start ?? DateTimeOffset.UtcNow,
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
        DateTimeOffset? grantedAt = null)
    {
        await using var db = fixture.CreateContext();
        db.RewriteCredits.Add(new RewriteCredit
        {
            UserId = userId,
            Source = source,
            AmountGranted = amountGranted,
            AmountConsumed = amountConsumed,
            GrantedAt = grantedAt ?? DateTimeOffset.Parse("2026-05-10T00:00:00Z"),
            StripeEventId = stripeEventId,
            StripePaymentIntentId = stripePaymentIntentId,
            StripeSku = stripeSku,
            StripeAmountTotal = stripeAmountTotal,
            StripeCurrency = stripeCurrency,
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
            StartedAt = DateTimeOffset.Parse("2026-05-10T00:00:00Z"),
            FinishedAt = DateTimeOffset.Parse("2026-05-10T00:00:02Z"),
            TotalEstimatedCostUsd = totalCost,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<string> ReadResponseBodyAsync(Stream body)
    {
        body.Position = 0;
        using var reader = new StreamReader(body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static HttpRequest CreateRequest(string oid, string email)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("oid", oid),
                new Claim("email", email),
            ], "Bearer")),
        };

        return context.Request;
    }

    private static IConfiguration BuildConfiguration(string adminEmails) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ADMIN_EMAILS"] = adminEmails,
            })
            .Build();
}
