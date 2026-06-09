using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Promo;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests.Application;

public sealed class PromoUseCaseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-02T12:00:00Z");

    [Fact]
    public async Task RedeemPromoAsync_happy_path_grants_credit_and_records_redemption()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var promoCode = await SeedPromoCodeAsync(
            fixture.CreateContext,
            "TEACHERTRIAL",
            Now,
            creditsGranted: 4,
            grantTtlDays: 45);
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateRedeemHandler(handlerDb);

        var result = await handler.HandleAsync(new RedeemPromoCommand(
            " clerk_promo_happy ",
            " PromoUser@Example.COM ",
            " teacher-trial ",
            "ip-hash-success",
            Now));

        result.Kind.Should().Be(PromoRedeemResultKind.Success);
        result.CreditsGranted.Should().Be(4);
        result.ExpiresAt.Should().BeCloseTo(Now.AddDays(45), TimeSpan.FromSeconds(1));
        result.RewriteCreditId.Should().NotBeNull();

        await using var verifyDb = fixture.CreateContext();
        var user = await verifyDb.AppUsers.SingleAsync();
        user.ExternalAuthUserId.Should().Be("clerk_promo_happy");
        user.Email.Should().Be("PromoUser@Example.COM");

        var credit = await verifyDb.RewriteCredits.SingleAsync();
        credit.Id.Should().Be(result.RewriteCreditId!.Value);
        credit.UserId.Should().Be(user.Id);
        credit.Source.Should().Be("PROMO");
        credit.AmountGranted.Should().Be(4);
        credit.AmountConsumed.Should().Be(0);
        credit.GrantedAt.Should().Be(Now);
        credit.ExpiresAt.Should().BeCloseTo(Now.AddDays(45), TimeSpan.FromSeconds(1));

        var redemption = await verifyDb.PromoCodeRedemptions.SingleAsync();
        redemption.PromoCodeId.Should().Be(promoCode.Id);
        redemption.UserId.Should().Be(user.Id);
        redemption.RewriteCreditId.Should().Be(credit.Id);
        redemption.CreditsGranted.Should().Be(4);
        redemption.CodeSnapshot.Should().Be("TEACHERTRIAL");
        redemption.RedeemIpHash.Should().Be("ip-hash-success");
        redemption.Status.Should().Be(PromoCodeRedemptionStatus.Applied);

        (await verifyDb.PromoCodes.SingleAsync()).RedemptionCount.Should().Be(1);
    }

    [Fact]
    public async Task RedeemPromoAsync_returns_cap_reached_without_grant_when_global_cap_is_full()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedPromoCodeAsync(
            fixture.CreateContext,
            "CAPCHECK",
            Now,
            maxRedemptionsGlobal: 1,
            redemptionCount: 1);
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateRedeemHandler(handlerDb);

        var result = await handler.HandleAsync(new RedeemPromoCommand(
            "clerk_promo_cap",
            "cap@example.com",
            "CAPCHECK",
            "ip-hash-cap",
            Now));

        result.Kind.Should().Be(PromoRedeemResultKind.CapReached);
        result.CreditsGranted.Should().Be(0);
        result.RewriteCreditId.Should().BeNull();

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteCredits.CountAsync()).Should().Be(0);
        (await verifyDb.PromoCodeRedemptions.CountAsync()).Should().Be(0);
        (await verifyDb.PromoCodes.SingleAsync()).RedemptionCount.Should().Be(1);
    }

    [Fact]
    public async Task RedeemPromoAsync_second_redeem_returns_already_redeemed_without_second_credit()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedPromoCodeAsync(fixture.CreateContext, "DOUBLECHECK", Now, maxRedemptionsGlobal: 10);
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateRedeemHandler(handlerDb);

        var first = await handler.HandleAsync(new RedeemPromoCommand(
            "clerk_promo_repeat",
            "repeat@example.com",
            "DOUBLECHECK",
            "ip-hash-repeat",
            Now));
        var second = await handler.HandleAsync(new RedeemPromoCommand(
            "clerk_promo_repeat",
            "repeat@example.com",
            "double-check",
            "ip-hash-repeat",
            Now.AddSeconds(1)));

        first.Kind.Should().Be(PromoRedeemResultKind.Success);
        second.Kind.Should().Be(PromoRedeemResultKind.AlreadyRedeemed);

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteCredits.CountAsync()).Should().Be(1);
        (await verifyDb.PromoCodeRedemptions.CountAsync()).Should().Be(1);
        (await verifyDb.PromoCodes.SingleAsync()).RedemptionCount.Should().Be(1);
    }

    [Fact]
    public async Task RedeemPromoAsync_expired_code_returns_expired_without_side_effects()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedPromoCodeAsync(
            fixture.CreateContext,
            "EXPIREDCHECK",
            Now,
            validUntil: Now.AddSeconds(-1));
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateRedeemHandler(handlerDb);

        var result = await handler.HandleAsync(new RedeemPromoCommand(
            "clerk_promo_expired",
            "expired@example.com",
            "EXPIREDCHECK",
            "ip-hash-expired",
            Now));

        result.Kind.Should().Be(PromoRedeemResultKind.Expired);

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteCredits.CountAsync()).Should().Be(0);
        (await verifyDb.PromoCodeRedemptions.CountAsync()).Should().Be(0);
        (await verifyDb.PromoCodes.SingleAsync()).RedemptionCount.Should().Be(0);
    }

    [Fact]
    public async Task RedeemPromoAsync_blocks_when_ip_hash_has_five_recent_applied_redemptions()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var promoCode = await SeedPromoCodeAsync(
            fixture.CreateContext,
            "IPCHECK",
            Now,
            maxRedemptionsGlobal: 20,
            redemptionCount: 5);
        await SeedAppliedIpRedemptionsAsync(
            fixture.CreateContext,
            promoCode.Id,
            "ip-hash-blocked",
            count: 5);
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateRedeemHandler(handlerDb);

        var result = await handler.HandleAsync(new RedeemPromoCommand(
            "clerk_promo_ip_blocked",
            "ip-blocked@example.com",
            "IPCHECK",
            "ip-hash-blocked",
            Now.AddMinutes(6)));

        result.Kind.Should().Be(PromoRedeemResultKind.IpVelocityBlocked);

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.PromoCodeRedemptions.CountAsync(x => x.Status == PromoCodeRedemptionStatus.Applied)).Should().Be(5);
        (await verifyDb.RewriteCredits.CountAsync()).Should().Be(5);
        (await verifyDb.PromoCodes.SingleAsync()).RedemptionCount.Should().Be(5);
    }

    [Fact]
    public async Task GetPromoStatusAsync_reports_not_redeemed_then_redeemed_state()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedPromoCodeAsync(fixture.CreateContext, "STATUSCHECK", Now);
        await using var handlerDb = fixture.CreateContext();
        var redeem = CreateRedeemHandler(handlerDb);
        var getStatus = CreateStatusHandler(handlerDb);

        var before = await getStatus.HandleAsync(new GetPromoStatusQuery(
            "clerk_promo_status",
            "status@example.com",
            Now));
        await redeem.HandleAsync(new RedeemPromoCommand(
            "clerk_promo_status",
            "status@example.com",
            "STATUSCHECK",
            "ip-hash-status",
            Now));
        var after = await getStatus.HandleAsync(new GetPromoStatusQuery(
            "clerk_promo_status",
            "status@example.com",
            Now.AddMinutes(1)));

        before.HasRedeemed.Should().BeFalse();
        before.Eligible.Should().BeTrue();
        before.TrialRemaining.Should().Be(0);
        before.TrialExpiresAt.Should().BeNull();
        after.HasRedeemed.Should().BeTrue();
        after.Eligible.Should().BeFalse();
        after.TrialRemaining.Should().Be(3);
        after.TrialExpiresAt.Should().BeCloseTo(Now.AddDays(90), TimeSpan.FromSeconds(1));
    }

    private static RedeemPromoHandler CreateRedeemHandler(AppDbContext db) =>
        new(
            new AppUserRepository(db),
            new PromoCodeRepository(db),
            new PromoCodeRedemptionRepository(db),
            new RewriteCreditRepository(db),
            new UnitOfWork(db));

    private static GetPromoStatusHandler CreateStatusHandler(AppDbContext db) =>
        new(
            new AppUserRepository(db),
            new PromoCodeRedemptionRepository(db),
            new PromoCodeRepository(db),
            new RewriteCreditRepository(db),
            new UnitOfWork(db));

    private static async Task<PromoCode> SeedPromoCodeAsync(
        Func<AppDbContext> createContext,
        string code,
        DateTimeOffset now,
        int creditsGranted = 3,
        int grantTtlDays = 90,
        int? maxRedemptionsGlobal = 100,
        int redemptionCount = 0,
        bool isActive = true,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validUntil = null)
    {
        await using var db = createContext();
        var promoCode = new PromoCode
        {
            Code = code,
            DisplayCode = code,
            Description = "Trial credits",
            Kind = PromoCodeKind.TrialCredits,
            CreditsGranted = creditsGranted,
            GrantTtlDays = grantTtlDays,
            ValidFrom = validFrom ?? now.AddDays(-1),
            ValidUntil = validUntil ?? now.AddDays(30),
            MaxRedemptionsGlobal = maxRedemptionsGlobal,
            MaxRedemptionsPerUser = 1,
            RedemptionCount = redemptionCount,
            IsActive = isActive,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.PromoCodes.Add(promoCode);
        await db.SaveChangesAsync();
        return promoCode;
    }

    private static async Task SeedAppliedIpRedemptionsAsync(
        Func<AppDbContext> createContext,
        Guid promoCodeId,
        string ipHash,
        int count)
    {
        await using var db = createContext();
        for (var index = 1; index <= count; index += 1)
        {
            var user = new AppUser
            {
                ExternalAuthUserId = $"clerk_ip_seed_{index}",
                Email = $"ip-seed-{index}@example.com",
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = Now.AddDays(-1),
                UpdatedAt = Now.AddDays(-1),
            };
            var credit = new RewriteCredit
            {
                User = user,
                Source = "PROMO",
                AmountGranted = 3,
                AmountConsumed = 0,
                GrantedAt = Now.AddMinutes(index),
                ExpiresAt = Now.AddDays(90),
            };
            db.RewriteCredits.Add(credit);
            db.PromoCodeRedemptions.Add(new PromoCodeRedemption
            {
                PromoCodeId = promoCodeId,
                User = user,
                RewriteCreditId = credit.Id,
                CreditsGranted = 3,
                CodeSnapshot = "IPCHECK",
                RedeemIpHash = ipHash,
                Status = PromoCodeRedemptionStatus.Applied,
                RedeemedAt = Now.AddMinutes(index),
            });
        }

        await db.SaveChangesAsync();
    }
}
