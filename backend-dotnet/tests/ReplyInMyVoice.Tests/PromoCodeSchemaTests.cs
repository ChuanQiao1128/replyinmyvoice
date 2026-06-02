using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Tests;

public sealed class PromoCodeSchemaTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task PromoCodeCodeIsUnique()
    {
        await using var db = CreateContext();
        var now = DateTimeOffset.Parse("2026-06-02T00:00:00Z");
        db.PromoCodes.Add(CreatePromoCode("TEACHERTRIAL", now));
        db.PromoCodes.Add(CreatePromoCode("TEACHERTRIAL", now));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task PromoCodeRedemptionIsUniquePerCodeAndUser()
    {
        var now = DateTimeOffset.Parse("2026-06-02T00:00:00Z");
        var user = new AppUser
        {
            ExternalAuthUserId = "clerk_promo_redemption",
            Email = "promo-redemption@example.com",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var promoCode = CreatePromoCode("SALESFOLLOWUP", now);

        await using (var seedDb = CreateContext())
        {
            seedDb.AppUsers.Add(user);
            seedDb.PromoCodes.Add(promoCode);
            await seedDb.SaveChangesAsync();
        }

        await using var db = CreateContext();
        db.PromoCodeRedemptions.Add(CreateRedemption(promoCode.Id, user.Id, now));
        db.PromoCodeRedemptions.Add(CreateRedemption(promoCode.Id, user.Id, now.AddSeconds(1)));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task PromoCodeChecksRejectNonPositiveCreditsGranted()
    {
        await using var db = CreateContext();
        var now = DateTimeOffset.Parse("2026-06-02T00:00:00Z");
        var promoCode = CreatePromoCode("ZEROCREDIT", now);
        promoCode.CreditsGranted = 0;
        db.PromoCodes.Add(promoCode);

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task PromoCodeChecksRejectValidUntilNotAfterValidFrom()
    {
        await using var db = CreateContext();
        var now = DateTimeOffset.Parse("2026-06-02T00:00:00Z");
        var promoCode = CreatePromoCode("BADWINDOW", now);
        promoCode.ValidUntil = promoCode.ValidFrom;
        db.PromoCodes.Add(promoCode);

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }

    private static PromoCode CreatePromoCode(string code, DateTimeOffset now) =>
        new()
        {
            Code = code,
            DisplayCode = code,
            Description = "Trial credits",
            Kind = PromoCodeKind.TrialCredits,
            CreditsGranted = 3,
            GrantTtlDays = 90,
            ValidFrom = now,
            ValidUntil = now.AddDays(30),
            MaxRedemptionsGlobal = 100,
            MaxRedemptionsPerUser = 1,
            RedemptionCount = 0,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

    private static PromoCodeRedemption CreateRedemption(Guid promoCodeId, Guid userId, DateTimeOffset redeemedAt) =>
        new()
        {
            PromoCodeId = promoCodeId,
            UserId = userId,
            RewriteCreditId = Guid.NewGuid(),
            CreditsGranted = 3,
            CodeSnapshot = "SALESFOLLOWUP",
            RedeemIpHash = "iphash-123",
            Status = PromoCodeRedemptionStatus.Applied,
            RedeemedAt = redeemedAt,
        };
}
