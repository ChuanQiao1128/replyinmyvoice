using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class AdminPromoTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-02T12:00:00Z");

    [Fact]
    public async Task AdminPromoCreate_NonAdminGetsForbidden()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var function = CreateFunction(fixture);

        var result = await function.CreatePromoCode(
            CreateRequest("regular-user-oid", "regular@example.com", new
            {
                code = "teacher-trial",
                creditsGranted = 3,
                grantTtlDays = 90,
                validFrom = Now.AddDays(-1),
                validUntil = Now.AddDays(30),
                maxRedemptionsPerUser = 1,
            }),
            CancellationToken.None);

        var forbidden = result.Should().BeOfType<ObjectResult>().Subject;
        forbidden.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        await using var db = fixture.CreateContext();
        (await db.PromoCodes.CountAsync()).Should().Be(0);
        (await db.AdminAuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AdminPromoCreate_AddsRowAndAuditLog()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var function = CreateFunction(fixture);

        var result = await function.CreatePromoCode(
            CreateRequest("admin-owner-oid", "owner@example.com", new
            {
                code = "Teacher-Trial",
                description = "Teacher launch credit",
                creditsGranted = 4,
                grantTtlDays = 45,
                validFrom = Now.AddDays(-1),
                validUntil = Now.AddDays(30),
                maxRedemptionsGlobal = 100,
                maxRedemptionsPerUser = 1,
            }),
            CancellationToken.None);

        var response = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<AdminPromoCodeResponse>().Subject;
        response.Code.Should().Be("TEACHERTRIAL");
        response.DisplayCode.Should().Be("Teacher-Trial");
        response.RedemptionCount.Should().Be(0);
        response.Status.Should().Be("active");

        await using var db = fixture.CreateContext();
        var code = await db.PromoCodes.SingleAsync();
        code.Id.Should().Be(response.Id);
        code.Code.Should().Be("TEACHERTRIAL");
        code.DisplayCode.Should().Be("Teacher-Trial");
        code.CreditsGranted.Should().Be(4);
        code.GrantTtlDays.Should().Be(45);
        code.MaxRedemptionsGlobal.Should().Be(100);
        code.MaxRedemptionsPerUser.Should().Be(1);

        var audit = await db.AdminAuditLogs.SingleAsync();
        audit.AdminExternalAuthUserId.Should().Be("admin-owner-oid");
        audit.AdminEmail.Should().Be("owner@example.com");
        audit.Action.Should().Be("promo_code_create");
        audit.TargetUserId.Should().BeNull();

        using var details = JsonDocument.Parse(audit.DetailsJson!);
        details.RootElement.GetProperty("promoCodeId").GetGuid().Should().Be(code.Id);
        details.RootElement.GetProperty("changedFields")
            .EnumerateArray()
            .Select(x => x.GetString())
            .Should()
            .Contain(["code", "displayCode", "creditsGranted", "grantTtlDays", "validFrom", "validUntil"]);
    }

    [Fact]
    public async Task AdminPromoCreate_DuplicateCodeReturnsBadRequest()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var function = CreateFunction(fixture);

        var first = await function.CreatePromoCode(
            CreateRequest("admin-owner-oid", "owner@example.com", new
            {
                code = "Sales Trial",
                creditsGranted = 3,
                grantTtlDays = 90,
                validFrom = Now.AddDays(-1),
                validUntil = Now.AddDays(30),
                maxRedemptionsPerUser = 1,
            }),
            CancellationToken.None);
        first.Should().BeOfType<OkObjectResult>();

        var duplicate = await function.CreatePromoCode(
            CreateRequest("admin-owner-oid", "owner@example.com", new
            {
                code = "sales-trial",
                creditsGranted = 3,
                grantTtlDays = 90,
                validFrom = Now.AddDays(-1),
                validUntil = Now.AddDays(30),
                maxRedemptionsPerUser = 1,
            }),
            CancellationToken.None);

        var badRequest = duplicate.Should().BeOfType<ObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        await using var db = fixture.CreateContext();
        (await db.PromoCodes.CountAsync()).Should().Be(1);
        (await db.AdminAuditLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AdminPromoDisable_SetsInactiveAndAudits()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var promoCode = await SeedPromoCodeAsync(fixture, "DISABLEME");
        var function = CreateFunction(fixture);

        var result = await function.DisablePromoCode(
            CreateRequest("admin-owner-oid", "owner@example.com"),
            promoCode.Id.ToString(),
            CancellationToken.None);

        var response = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<AdminPromoCodeResponse>().Subject;
        response.IsActive.Should().BeFalse();
        response.Status.Should().Be("disabled");

        await using var db = fixture.CreateContext();
        (await db.PromoCodes.SingleAsync()).IsActive.Should().BeFalse();
        var audit = await db.AdminAuditLogs.SingleAsync();
        audit.Action.Should().Be("promo_code_disable");
        using var details = JsonDocument.Parse(audit.DetailsJson!);
        details.RootElement.GetProperty("promoCodeId").GetGuid().Should().Be(promoCode.Id);
        details.RootElement.GetProperty("changedFields")
            .EnumerateArray()
            .Select(x => x.GetString())
            .Should()
            .ContainSingle("isActive");
    }

    [Fact]
    public async Task AdminPromoDetail_StatsExposeActivationRateAndHashClustersOnly()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var promoCode = await SeedPromoCodeAsync(fixture, "STATSCODE");
        var activatedUser = await fixture.CreateUserAsync();
        var dormantUser = await fixture.CreateUserAsync();
        await SeedRedemptionAsync(fixture, promoCode.Id, activatedUser.Id, "STATSCODE", "hash-cluster-a", consumed: 1, Now.AddHours(-3));
        await SeedRedemptionAsync(fixture, promoCode.Id, dormantUser.Id, "STATSCODE", "hash-cluster-a", consumed: 0, Now.AddHours(-2));
        var function = CreateFunction(fixture);

        var result = await function.GetPromoCodeDetail(
            CreateRequest("admin-owner-oid", "owner@example.com"),
            promoCode.Id.ToString(),
            CancellationToken.None);

        var detail = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<AdminPromoCodeDetailResponse>().Subject;
        detail.Stats.TotalRedemptions.Should().Be(2);
        detail.Stats.DistinctUsers.Should().Be(2);
        detail.Stats.ActivationRate.Should().Be(0.5);
        detail.Stats.DailyCurve.Should().ContainSingle(x => x.Redemptions == 2);
        detail.Stats.IpHashClusters.Should().ContainSingle(x =>
            x.IpHash == "hash-cluster-a" &&
            x.Redemptions == 2 &&
            x.DistinctUsers == 2);

        var serialized = JsonSerializer.Serialize(detail);
        serialized.Should().Contain("hash-cluster-a");
        serialized.Should().NotContain("203.0.113.");
        serialized.ToLowerInvariant().Should().NotContain("rawip");
    }

    private static AdminHttpFunctions CreateFunction(DbFixture fixture) =>
        new(BuildConfiguration(), fixture.CreateContext);

    private static async Task<PromoCode> SeedPromoCodeAsync(DbFixture fixture, string code)
    {
        await using var db = fixture.CreateContext();
        var promoCode = new PromoCode
        {
            Code = code,
            DisplayCode = code,
            Description = "Promo admin test",
            Kind = PromoCodeKind.TrialCredits,
            CreditsGranted = 3,
            GrantTtlDays = 90,
            ValidFrom = Now.AddDays(-1),
            ValidUntil = Now.AddDays(30),
            MaxRedemptionsGlobal = 100,
            MaxRedemptionsPerUser = 1,
            RedemptionCount = 0,
            IsActive = true,
            CreatedAt = Now,
            UpdatedAt = Now,
        };
        db.PromoCodes.Add(promoCode);
        await db.SaveChangesAsync();
        return promoCode;
    }

    private static async Task SeedRedemptionAsync(
        DbFixture fixture,
        Guid promoCodeId,
        Guid userId,
        string code,
        string ipHash,
        int consumed,
        DateTimeOffset redeemedAt)
    {
        await using var db = fixture.CreateContext();
        var credit = new RewriteCredit
        {
            UserId = userId,
            Source = "PROMO",
            AmountGranted = 3,
            AmountConsumed = consumed,
            GrantedAt = redeemedAt,
            ExpiresAt = redeemedAt.AddDays(90),
        };
        db.RewriteCredits.Add(credit);
        db.PromoCodeRedemptions.Add(new PromoCodeRedemption
        {
            PromoCodeId = promoCodeId,
            UserId = userId,
            RewriteCreditId = credit.Id,
            CreditsGranted = 3,
            CodeSnapshot = code,
            RedeemIpHash = ipHash,
            Status = PromoCodeRedemptionStatus.Applied,
            RedeemedAt = redeemedAt,
        });

        var promoCode = await db.PromoCodes.SingleAsync(x => x.Id == promoCodeId);
        promoCode.RedemptionCount += 1;
        promoCode.UpdatedAt = redeemedAt;
        await db.SaveChangesAsync();
    }

    private static HttpRequest CreateRequest(string oid, string email, object? body = null)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("oid", oid),
                new Claim("email", email),
            ], "Bearer")),
        };

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body);
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
            context.Request.ContentType = "application/json";
        }

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
