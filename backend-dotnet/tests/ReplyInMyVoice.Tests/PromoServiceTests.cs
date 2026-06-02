using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class PromoServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-02T12:00:00Z");

    [Fact]
    public async Task RedeemAsync_happy_path_grants_promo_credit_and_redemption()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var promoCode = await SeedPromoCodeAsync(
            fixture.CreateContext,
            "TEACHERTRIAL",
            Now,
            creditsGranted: 4,
            grantTtlDays: 45);
        var service = CreateService(fixture.CreateContext);

        var result = await service.RedeemAsync(
            " clerk_promo_happy ",
            " PromoUser@Example.COM ",
            " teacher-trial ",
            "203.0.113.10",
            Now);

        result.Kind.Should().Be(PromoRedeemResultKind.Success);
        result.CreditsGranted.Should().Be(4);
        result.ExpiresAt.Should().BeCloseTo(Now.AddDays(45), TimeSpan.FromSeconds(1));

        await using var db = fixture.CreateContext();
        var user = await db.AppUsers.SingleAsync();
        user.ExternalAuthUserId.Should().Be("clerk_promo_happy");
        user.Email.Should().Be("PromoUser@Example.COM");

        var credit = await db.RewriteCredits.SingleAsync();
        credit.UserId.Should().Be(user.Id);
        credit.Source.Should().Be("PROMO");
        credit.AmountGranted.Should().Be(4);
        credit.AmountConsumed.Should().Be(0);
        credit.GrantedAt.Should().Be(Now);
        credit.ExpiresAt.Should().BeCloseTo(Now.AddDays(45), TimeSpan.FromSeconds(1));

        var redemption = await db.PromoCodeRedemptions.SingleAsync();
        redemption.PromoCodeId.Should().Be(promoCode.Id);
        redemption.UserId.Should().Be(user.Id);
        redemption.RewriteCreditId.Should().Be(credit.Id);
        redemption.CreditsGranted.Should().Be(4);
        redemption.CodeSnapshot.Should().Be("TEACHERTRIAL");
        redemption.RedeemIpHash.Should().NotBeNullOrWhiteSpace();
        redemption.RedeemIpHash.Should().HaveLength(64);
        redemption.RedeemIpHash.Should().NotContain("203.0.113.10");
        redemption.Status.Should().Be(PromoCodeRedemptionStatus.Applied);

        (await db.PromoCodes.SingleAsync()).RedemptionCount.Should().Be(1);
    }

    [Fact]
    public async Task RedeemAsync_second_redeem_returns_already_redeemed_without_second_credit()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedPromoCodeAsync(fixture.CreateContext, "SALESFOLLOWUP", Now);
        var service = CreateService(fixture.CreateContext);

        var first = await service.RedeemAsync(
            "clerk_promo_repeat",
            "repeat@example.com",
            "sales follow-up",
            "203.0.113.11",
            Now);
        var second = await service.RedeemAsync(
            "clerk_promo_repeat",
            "repeat@example.com",
            "SALES-FOLLOWUP",
            "203.0.113.11",
            Now.AddSeconds(1));

        first.Kind.Should().Be(PromoRedeemResultKind.Success);
        second.Kind.Should().Be(PromoRedeemResultKind.AlreadyRedeemed);

        await using var db = fixture.CreateContext();
        (await db.RewriteCredits.CountAsync()).Should().Be(1);
        (await db.PromoCodeRedemptions.CountAsync()).Should().Be(1);
        (await db.PromoCodes.SingleAsync()).RedemptionCount.Should().Be(1);
    }

    [Fact]
    public async Task RedeemAsync_concurrent_same_user_redeem_grants_once()
    {
        await using var fixture = await PromoFileDbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync("clerk_promo_double", "double@example.com");
        await SeedPromoCodeAsync(fixture.CreateContext, "DOUBLECHECK", Now, maxRedemptionsGlobal: 10);
        var service = CreateService(fixture.CreateContext);

        var results = await Task.WhenAll(
            service.RedeemAsync(user.ExternalAuthUserId, user.Email, "DOUBLECHECK", "203.0.113.12", Now),
            service.RedeemAsync(user.ExternalAuthUserId, user.Email, "double-check", "203.0.113.12", Now));

        results.Count(x => x.Kind == PromoRedeemResultKind.Success).Should().Be(1);
        results.Count(x => x.Kind == PromoRedeemResultKind.AlreadyRedeemed).Should().Be(1);

        await using var db = fixture.CreateContext();
        (await db.RewriteCredits.CountAsync()).Should().Be(1);
        (await db.PromoCodeRedemptions.CountAsync()).Should().Be(1);
        (await db.PromoCodes.SingleAsync()).RedemptionCount.Should().Be(1);
    }

    [Theory]
    [InlineData("missing", PromoRedeemResultKind.InvalidCode)]
    [InlineData("inactive", PromoRedeemResultKind.InvalidCode)]
    [InlineData("future", PromoRedeemResultKind.InvalidCode)]
    [InlineData("expired", PromoRedeemResultKind.Expired)]
    public async Task RedeemAsync_validity_gates_return_expected_kind(
        string scenario,
        PromoRedeemResultKind expectedKind)
    {
        await using var fixture = await DbFixture.CreateAsync();
        if (scenario != "missing")
        {
            await SeedPromoCodeAsync(
                fixture.CreateContext,
                "VALIDITY",
                Now,
                isActive: scenario != "inactive",
                validFrom: scenario == "future" ? Now.AddHours(1) : Now.AddDays(-1),
                validUntil: scenario == "expired" ? Now.AddSeconds(-1) : Now.AddDays(1));
        }

        var service = CreateService(fixture.CreateContext);

        var result = await service.RedeemAsync(
            $"clerk_promo_{scenario}",
            $"{scenario}@example.com",
            "VALIDITY",
            "203.0.113.13",
            Now);

        result.Kind.Should().Be(expectedKind);

        await using var db = fixture.CreateContext();
        (await db.RewriteCredits.CountAsync()).Should().Be(0);
        (await db.PromoCodeRedemptions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RedeemAsync_global_cap_allows_exactly_n_parallel_redemptions()
    {
        await using var fixture = await PromoFileDbFixture.CreateAsync();
        await SeedPromoCodeAsync(fixture.CreateContext, "CAPCHECK", Now, maxRedemptionsGlobal: 3);
        var service = CreateService(fixture.CreateContext);

        var results = await Task.WhenAll(Enumerable.Range(1, 8).Select(i =>
            service.RedeemAsync(
                $"clerk_promo_cap_{i}",
                $"cap-{i}@example.com",
                "CAPCHECK",
                $"203.0.113.{20 + i}",
                Now)));

        results.Count(x => x.Kind == PromoRedeemResultKind.Success).Should().Be(3);
        results.Count(x => x.Kind == PromoRedeemResultKind.CapReached).Should().Be(5);

        await using var db = fixture.CreateContext();
        (await db.PromoCodeRedemptions.CountAsync(x => x.Status == PromoCodeRedemptionStatus.Applied)).Should().Be(3);
        (await db.RewriteCredits.CountAsync()).Should().Be(3);
        (await db.PromoCodes.SingleAsync()).RedemptionCount.Should().Be(3);
    }

    [Fact]
    public async Task RedeemAsync_ip_velocity_flags_from_two_and_blocks_at_five()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedPromoCodeAsync(fixture.CreateContext, "IPCHECK", Now, maxRedemptionsGlobal: 20);
        var logger = new CapturingLogger<PromoService>();
        var service = CreateService(fixture.CreateContext, logger: logger);

        var successfulResults = new List<PromoRedeemResult>();
        for (var i = 1; i <= 5; i++)
        {
            successfulResults.Add(await service.RedeemAsync(
                $"clerk_promo_ip_{i}",
                $"ip-{i}@example.com",
                "IPCHECK",
                "203.0.113.99",
                Now.AddMinutes(i)));
        }

        var blocked = await service.RedeemAsync(
            "clerk_promo_ip_6",
            "ip-6@example.com",
            "IPCHECK",
            "203.0.113.99",
            Now.AddMinutes(6));

        successfulResults.Should().OnlyContain(x => x.Kind == PromoRedeemResultKind.Success);
        blocked.Kind.Should().Be(PromoRedeemResultKind.IpVelocityBlocked);
        logger.Messages.Should().Contain(x => x.Level == LogLevel.Warning && x.Message.Contains("velocity flag", StringComparison.OrdinalIgnoreCase));
        logger.Messages.Should().NotContain(x => x.Message.Contains("203.0.113.99", StringComparison.OrdinalIgnoreCase));

        await using var db = fixture.CreateContext();
        (await db.PromoCodeRedemptions.CountAsync(x => x.Status == PromoCodeRedemptionStatus.Applied)).Should().Be(5);
        (await db.RewriteCredits.CountAsync()).Should().Be(5);
    }

    [Fact]
    public async Task RedeemAsync_in_production_without_trusted_ip_returns_server_config()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedPromoCodeAsync(fixture.CreateContext, "PRODIP", Now);
        var service = CreateService(
            fixture.CreateContext,
            BuildConfiguration(environmentName: "Production"));

        var result = await service.RedeemAsync(
            "clerk_promo_prod_ip",
            "prod-ip@example.com",
            "PRODIP",
            trustedClientIp: null,
            Now);

        result.Kind.Should().Be(PromoRedeemResultKind.ServerConfig);

        await using var db = fixture.CreateContext();
        (await db.AppUsers.CountAsync()).Should().Be(0);
        (await db.RewriteCredits.CountAsync()).Should().Be(0);
        (await db.PromoCodeRedemptions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetStatusAsync_reports_promo_credit_state_for_user()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedPromoCodeAsync(fixture.CreateContext, "STATUSCHECK", Now);
        var service = CreateService(fixture.CreateContext);

        var before = await service.GetStatusAsync(
            "clerk_promo_status",
            "status@example.com",
            Now);
        await service.RedeemAsync(
            "clerk_promo_status",
            "status@example.com",
            "STATUSCHECK",
            "203.0.113.14",
            Now);
        var after = await service.GetStatusAsync(
            "clerk_promo_status",
            "status@example.com",
            Now.AddMinutes(1));

        before.HasRedeemed.Should().BeFalse();
        before.Eligible.Should().BeTrue();
        before.TrialRemaining.Should().Be(0);
        before.TrialExpiresAt.Should().BeNull();
        after.HasRedeemed.Should().BeTrue();
        after.Eligible.Should().BeFalse();
        after.TrialRemaining.Should().Be(3);
        after.TrialExpiresAt.Should().BeCloseTo(Now.AddDays(90), TimeSpan.FromSeconds(1));
    }

    private static PromoService CreateService(
        Func<AppDbContext> createContext,
        IConfiguration? configuration = null,
        ILogger<PromoService>? logger = null) =>
        new(createContext, configuration ?? BuildConfiguration(), logger);

    private static IConfiguration BuildConfiguration(string environmentName = "Testing") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AZURE_FUNCTIONS_ENVIRONMENT"] = environmentName,
                ["PROMO_IP_HASH_SALT"] = "promo-test-salt",
                ["PROMO_PROXY_SHARED_SECRET"] = "proxy-test-secret",
            })
            .Build();

    private static async Task<PromoCode> SeedPromoCodeAsync(
        Func<AppDbContext> createContext,
        string code,
        DateTimeOffset now,
        int creditsGranted = 3,
        int grantTtlDays = 90,
        int? maxRedemptionsGlobal = 100,
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
            RedemptionCount = 0,
            IsActive = isActive,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.PromoCodes.Add(promoCode);
        await db.SaveChangesAsync();
        return promoCode;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class PromoFileDbFixture : IAsyncDisposable
    {
        private readonly string _databasePath;

        private PromoFileDbFixture(string databasePath)
        {
            _databasePath = databasePath;
        }

        public static async Task<PromoFileDbFixture> CreateAsync()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"replyinmyvoice-promo-{Guid.NewGuid():N}.db");
            var fixture = new PromoFileDbFixture(databasePath);
            await using var db = fixture.CreateContext();
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=30000;");
            return fixture;
        }

        public AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_databasePath};Default Timeout=30")
                .EnableSensitiveDataLogging()
                .Options;
            return new AppDbContext(options);
        }

        public async Task<AppUser> CreateUserAsync(string externalAuthUserId, string email)
        {
            await using var db = CreateContext();
            var user = new AppUser
            {
                ExternalAuthUserId = externalAuthUserId,
                Email = email,
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            db.AppUsers.Add(user);
            await db.SaveChangesAsync();
            return user;
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            TryDelete(_databasePath);
            TryDelete($"{_databasePath}-wal");
            TryDelete($"{_databasePath}-shm");
            return ValueTask.CompletedTask;
        }

        private static void TryDelete(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
