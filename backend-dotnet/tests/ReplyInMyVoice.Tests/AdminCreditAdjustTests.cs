using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Account;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Services;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests;

public sealed class AdminCreditAdjustTests
{
    [Fact]
    public async Task AdminGrantChangesBalance()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await ExhaustFreeQuotaAsync(fixture, user.Id);
        var function = AdminHttpFunctionsTestFactory.Create(BuildConfiguration(), fixture.CreateContext);

        var before = await GetAccountSummaryAsync(
            fixture,
            user.ExternalAuthUserId,
            user.Email);
        before.Usage.Remaining.Should().Be(0);

        var result = await function.GrantCredits(
            CreateRequest("admin-owner-oid", "owner@example.com", new
            {
                amount = 7,
                reason = "manual account correction",
            }),
            user.Id.ToString(),
            CancellationToken.None);

        var response = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<AdminCreditGrantResponse>().Subject;
        response.TargetUserId.Should().Be(user.Id);
        response.AmountGranted.Should().Be(7);
        response.Source.Should().Be("ADMIN");
        response.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow.AddDays(89));

        var after = await GetAccountSummaryAsync(
            fixture,
            user.ExternalAuthUserId,
            user.Email);
        after.Usage.Remaining.Should().Be(7);
        after.Usage.Quota.Should().Be(10);
        after.Usage.Used.Should().Be(3);
        (after.Usage.Quota - after.Usage.Used - after.Usage.Reserved).Should().Be(after.Usage.Remaining);
        after.Usage.Sources.Should().ContainSingle(x =>
            x.Source == "ADMIN" &&
            x.Limit == 7 &&
            x.Used == 0 &&
            x.Remaining == 7);

        await using var db = fixture.CreateContext();
        var credit = await db.RewriteCredits.SingleAsync();
        credit.UserId.Should().Be(user.Id);
        credit.Source.Should().Be("ADMIN");
        credit.AmountGranted.Should().Be(7);
        credit.AmountConsumed.Should().Be(0);
        credit.ExpiresAt.Should().Be(response.ExpiresAt);
    }

    [Fact]
    public async Task AdminGrantForbiddenAndAudited()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var function = AdminHttpFunctionsTestFactory.Create(BuildConfiguration(), fixture.CreateContext);

        var forbidden = await function.GrantCredits(
            CreateRequest("regular-user-oid", "regular@example.com", new
            {
                amount = 3,
            }),
            user.Id.ToString(),
            CancellationToken.None);

        var forbiddenResult = forbidden.Should().BeOfType<ObjectResult>().Subject;
        forbiddenResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        await using (var db = fixture.CreateContext())
        {
            (await db.RewriteCredits.CountAsync()).Should().Be(0);
            (await db.AdminAuditLogs.CountAsync()).Should().Be(0);
        }

        var granted = await function.GrantCredits(
            CreateRequest("admin-owner-oid", "owner@example.com", new
            {
                amount = 3,
                reason = "support adjustment",
            }),
            user.Id.ToString(),
            CancellationToken.None);

        granted.Should().BeOfType<OkObjectResult>();

        await using (var db = fixture.CreateContext())
        {
            var credit = await db.RewriteCredits.SingleAsync();
            credit.Source.Should().Be("ADMIN");
            credit.AmountGranted.Should().Be(3);
            credit.AmountConsumed.Should().Be(0);

            var audit = await db.AdminAuditLogs.SingleAsync();
            audit.AdminExternalAuthUserId.Should().Be("admin-owner-oid");
            audit.AdminEmail.Should().Be("owner@example.com");
            audit.Action.Should().Be("grant_credits");
            audit.TargetUserId.Should().Be(user.Id);

            using var details = JsonDocument.Parse(audit.DetailsJson!);
            details.RootElement.GetProperty("creditId").GetGuid().Should().Be(credit.Id);
            details.RootElement.GetProperty("amountGranted").GetInt32().Should().Be(3);
            details.RootElement.GetProperty("source").GetString().Should().Be("ADMIN");
            details.RootElement.GetProperty("reason").GetString().Should().Be("support adjustment");
        }
    }

    private static async Task ExhaustFreeQuotaAsync(DbFixture fixture, Guid userId)
    {
        await using var db = fixture.CreateContext();
        db.UsagePeriods.Add(new UsagePeriod
        {
            UserId = userId,
            PeriodKey = "free:lifetime",
            QuotaLimit = 3,
            UsedCount = 3,
            ReservedCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<AccountSummaryDto> GetAccountSummaryAsync(
        DbFixture fixture,
        string externalAuthUserId,
        string? email)
    {
        await using var db = fixture.CreateContext();
        var handler = new GetAccountSummaryHandler(
            new AppUserRepository(db),
            new UsagePeriodRepository(db),
            new RewriteCreditRepository(db),
            new PromoCodeRedemptionRepository(db),
            new PromoCodeRepository(db),
            new AccountUsagePlanProvider(BuildConfiguration()),
            new UnitOfWork(db));

        return await handler.HandleAsync(new GetAccountSummaryQuery(externalAuthUserId, email));
    }

    private static HttpRequest CreateRequest(string oid, string email, object body)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("oid", oid),
                new Claim("email", email),
            ], "Bearer")),
        };

        var json = JsonSerializer.Serialize(body);
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        context.Request.ContentType = "application/json";
        return context.Request;
    }

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ADMIN_EMAILS"] = "admin-owner-oid, owner@example.com",
            })
            .Build();
}
