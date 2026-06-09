using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.PromoAdmin;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests.Application;

public sealed class PromoAdminUseCaseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-02T12:00:00Z");

    [Fact]
    public async Task CreatePromoCodeAsync_creates_row_and_audit_log()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateCreateHandler(handlerDb);

        var result = await handler.HandleAsync(new CreatePromoCodeCommand(
            " admin-owner-oid ",
            " owner@example.com ",
            "Teacher-Trial",
            " Teacher launch credit ",
            4,
            45,
            Now.AddDays(-1),
            Now.AddDays(30),
            100,
            1,
            Now));

        result.Kind.Should().Be(AdminPromoResultKind.Success);
        result.Response.Should().NotBeNull();
        result.Response!.Code.Should().Be("TEACHERTRIAL");
        result.Response.DisplayCode.Should().Be("Teacher-Trial");
        result.Response.Description.Should().Be("Teacher launch credit");
        result.Response.Status.Should().Be("active");

        await using var verifyDb = fixture.CreateContext();
        var code = await verifyDb.PromoCodes.SingleAsync();
        code.Id.Should().Be(result.Response.Id);
        code.Code.Should().Be("TEACHERTRIAL");
        code.DisplayCode.Should().Be("Teacher-Trial");
        code.Description.Should().Be("Teacher launch credit");
        code.CreditsGranted.Should().Be(4);
        code.GrantTtlDays.Should().Be(45);
        code.MaxRedemptionsGlobal.Should().Be(100);
        code.MaxRedemptionsPerUser.Should().Be(1);

        var audit = await verifyDb.AdminAuditLogs.SingleAsync();
        audit.AdminExternalAuthUserId.Should().Be("admin-owner-oid");
        audit.AdminEmail.Should().Be("owner@example.com");
        audit.Action.Should().Be("promo_code_create");
        AssertAuditFields(
            audit,
            code.Id,
            [
                "code",
                "displayCode",
                "description",
                "creditsGranted",
                "grantTtlDays",
                "validFrom",
                "validUntil",
                "maxRedemptionsGlobal",
                "maxRedemptionsPerUser",
                "isActive",
            ]);
    }

    [Fact]
    public async Task CreatePromoCodeAsync_duplicate_normalized_code_returns_duplicate_without_audit()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedPromoCodeAsync(fixture.CreateContext, "SALESTRIAL", Now);
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateCreateHandler(handlerDb);

        var result = await handler.HandleAsync(new CreatePromoCodeCommand(
            "admin-owner-oid",
            "owner@example.com",
            "sales-trial",
            null,
            3,
            90,
            Now.AddDays(-1),
            Now.AddDays(30),
            100,
            1,
            Now));

        result.Kind.Should().Be(AdminPromoResultKind.DuplicateCode);
        result.Response.Should().BeNull();

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.PromoCodes.CountAsync()).Should().Be(1);
        (await verifyDb.AdminAuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UpdatePromoCodeAsync_updates_fields_and_audit_log()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var promoCode = await SeedPromoCodeAsync(
            fixture.CreateContext,
            "UPDATEME",
            Now,
            description: "Original",
            creditsGranted: 3,
            grantTtlDays: 90);
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateUpdateHandler(handlerDb);
        var newValidUntil = Now.AddDays(60);

        var result = await handler.HandleAsync(new UpdatePromoCodeCommand(
            "admin-owner-oid",
            "owner@example.com",
            promoCode.Id,
            " Updated description ",
            5,
            null,
            null,
            newValidUntil,
            null,
            null,
            Now.AddMinutes(5)));

        result.Kind.Should().Be(AdminPromoResultKind.Success);
        result.Response.Should().NotBeNull();
        result.Response!.Description.Should().Be("Updated description");
        result.Response.CreditsGranted.Should().Be(5);
        result.Response.ValidUntil.Should().Be(newValidUntil);

        await using var verifyDb = fixture.CreateContext();
        var stored = await verifyDb.PromoCodes.SingleAsync();
        stored.Description.Should().Be("Updated description");
        stored.CreditsGranted.Should().Be(5);
        stored.ValidUntil.Should().Be(newValidUntil);
        stored.UpdatedAt.Should().Be(Now.AddMinutes(5));

        var audit = await verifyDb.AdminAuditLogs.SingleAsync();
        audit.Action.Should().Be("promo_code_update");
        AssertAuditFields(
            audit,
            promoCode.Id,
            ["creditsGranted", "description", "validUntil"]);
    }

    [Fact]
    public async Task UpdatePromoCodeAsync_missing_code_returns_not_found_without_audit()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateUpdateHandler(handlerDb);

        var result = await handler.HandleAsync(new UpdatePromoCodeCommand(
            "admin-owner-oid",
            "owner@example.com",
            Guid.NewGuid(),
            "Updated description",
            null,
            null,
            null,
            null,
            null,
            null,
            Now));

        result.Kind.Should().Be(AdminPromoResultKind.NotFound);

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.AdminAuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ListPromoCodesAsync_returns_ordered_codes_with_resolved_status()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedPromoCodeAsync(
            fixture.CreateContext,
            "EXPIREDCODE",
            Now.AddHours(-2),
            validFrom: Now.AddDays(-10),
            validUntil: Now.AddSeconds(-1));
        await SeedPromoCodeAsync(
            fixture.CreateContext,
            "ARCHIVEDCODE",
            Now.AddHours(-1),
            isActive: false,
            archivedAt: Now.AddMinutes(-10));
        await SeedPromoCodeAsync(
            fixture.CreateContext,
            "ACTIVEONE",
            Now,
            displayCode: "Active-One");
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateListHandler(handlerDb);

        var result = await handler.HandleAsync(new ListPromoCodesQuery(Now));

        result.PromoCodes.Select(x => x.Code).Should().Equal("ACTIVEONE", "ARCHIVEDCODE", "EXPIREDCODE");
        result.PromoCodes.Select(x => x.Status).Should().Equal("active", "archived", "expired");
    }

    [Fact]
    public async Task GetPromoCodeDetailAsync_returns_stats_for_applied_redemptions_only()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var promoCode = await SeedPromoCodeAsync(fixture.CreateContext, "STATSCODE", Now);
        var activatedUser = await fixture.CreateUserAsync();
        var dormantUser = await fixture.CreateUserAsync();
        await SeedRedemptionAsync(fixture.CreateContext, promoCode.Id, activatedUser.Id, "cluster-a", consumed: 1, Now.AddHours(-3));
        await SeedRedemptionAsync(fixture.CreateContext, promoCode.Id, dormantUser.Id, "cluster-a", consumed: 0, Now.AddHours(-2));
        await SeedRedemptionAsync(
            fixture.CreateContext,
            promoCode.Id,
            (await fixture.CreateUserAsync()).Id,
            "cluster-a",
            consumed: 1,
            Now.AddHours(-1),
            PromoCodeRedemptionStatus.Reversed);
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateDetailHandler(handlerDb);

        var detail = await handler.HandleAsync(new GetPromoCodeDetailQuery(promoCode.Id, Now));

        detail.Should().NotBeNull();
        detail!.PromoCode.Code.Should().Be("STATSCODE");
        detail.Stats.TotalRedemptions.Should().Be(2);
        detail.Stats.DistinctUsers.Should().Be(2);
        detail.Stats.ActivationRate.Should().Be(0.5);
        detail.Stats.DailyCurve.Should().ContainSingle(x => x.Redemptions == 2);
        detail.Stats.IpHashClusters.Should().ContainSingle(x =>
            x.IpHash == "cluster-a" &&
            x.Redemptions == 2 &&
            x.DistinctUsers == 2);
    }

    [Fact]
    public async Task GetPromoCodeDetailAsync_missing_code_returns_null()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateDetailHandler(handlerDb);

        var detail = await handler.HandleAsync(new GetPromoCodeDetailQuery(Guid.NewGuid(), Now));

        detail.Should().BeNull();
    }

    private static CreatePromoCodeHandler CreateCreateHandler(AppDbContext db) =>
        new(new PromoAdminRepository(db), new UnitOfWork(db));

    private static UpdatePromoCodeHandler CreateUpdateHandler(AppDbContext db) =>
        new(new PromoAdminRepository(db), new UnitOfWork(db));

    private static ListPromoCodesHandler CreateListHandler(AppDbContext db) =>
        new(new PromoAdminRepository(db));

    private static GetPromoCodeDetailHandler CreateDetailHandler(AppDbContext db) =>
        new(new PromoAdminRepository(db));

    private static async Task<PromoCode> SeedPromoCodeAsync(
        Func<AppDbContext> createContext,
        string code,
        DateTimeOffset now,
        string? displayCode = null,
        string? description = "Trial credits",
        int creditsGranted = 3,
        int grantTtlDays = 90,
        int? maxRedemptionsGlobal = 100,
        int maxRedemptionsPerUser = 1,
        int redemptionCount = 0,
        bool isActive = true,
        DateTimeOffset? archivedAt = null,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validUntil = null)
    {
        await using var db = createContext();
        var promoCode = new PromoCode
        {
            Code = code,
            DisplayCode = displayCode ?? code,
            Description = description,
            Kind = PromoCodeKind.TrialCredits,
            CreditsGranted = creditsGranted,
            GrantTtlDays = grantTtlDays,
            ValidFrom = validFrom ?? now.AddDays(-1),
            ValidUntil = validUntil ?? now.AddDays(30),
            MaxRedemptionsGlobal = maxRedemptionsGlobal,
            MaxRedemptionsPerUser = maxRedemptionsPerUser,
            RedemptionCount = redemptionCount,
            IsActive = isActive,
            ArchivedAt = archivedAt,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.PromoCodes.Add(promoCode);
        await db.SaveChangesAsync();
        return promoCode;
    }

    private static async Task SeedRedemptionAsync(
        Func<AppDbContext> createContext,
        Guid promoCodeId,
        Guid userId,
        string ipHash,
        int consumed,
        DateTimeOffset redeemedAt,
        PromoCodeRedemptionStatus status = PromoCodeRedemptionStatus.Applied)
    {
        await using var db = createContext();
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
            CodeSnapshot = "STATSCODE",
            RedeemIpHash = ipHash,
            Status = status,
            RedeemedAt = redeemedAt,
        });

        if (status == PromoCodeRedemptionStatus.Applied)
        {
            var promoCode = await db.PromoCodes.SingleAsync(x => x.Id == promoCodeId);
            promoCode.RedemptionCount += 1;
            promoCode.UpdatedAt = redeemedAt;
        }

        await db.SaveChangesAsync();
    }

    private static void AssertAuditFields(
        AdminAuditLog audit,
        Guid promoCodeId,
        IReadOnlyCollection<string> expectedFields)
    {
        using var details = JsonDocument.Parse(audit.DetailsJson!);
        details.RootElement.GetProperty("promoCodeId").GetGuid().Should().Be(promoCodeId);
        details.RootElement.GetProperty("changedFields")
            .EnumerateArray()
            .Select(x => x.GetString())
            .Should()
            .BeEquivalentTo(expectedFields);
    }
}
