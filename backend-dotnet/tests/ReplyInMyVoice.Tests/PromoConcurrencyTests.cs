using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class PromoConcurrencyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-02T12:00:00Z");

    static PromoConcurrencyTests()
    {
        Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
        Environment.SetEnvironmentVariable("ASPNETCORE_hostBuilder__reloadConfigOnChange", "false");
    }

    [Fact]
    public async Task Same_user_double_click_redeem_grants_exactly_one_credit()
    {
        await using var fixture = await PromoFileDbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync("promo_double_click_user", "double-click@example.com");
        await SeedPromoCodeAsync(fixture.CreateContext, "DOUBLECLICK", Now, maxRedemptionsGlobal: 10);
        var service = CreateService(fixture.CreateContext);

        var results = await Task.WhenAll(
            service.RedeemAsync(user.ExternalAuthUserId, user.Email, "DOUBLECLICK", "203.0.113.10", Now),
            service.RedeemAsync(user.ExternalAuthUserId, user.Email, "double-click", "203.0.113.10", Now));

        results.Count(x => x.Kind == PromoRedeemResultKind.Success).Should().Be(1);
        results.Count(x => x.Kind == PromoRedeemResultKind.AlreadyRedeemed).Should().Be(1);

        await using var db = fixture.CreateContext();
        (await db.RewriteCredits.CountAsync()).Should().Be(1);
        (await db.PromoCodeRedemptions.CountAsync(x => x.Status == PromoCodeRedemptionStatus.Applied)).Should().Be(1);
        (await db.PromoCodes.SingleAsync()).RedemptionCount.Should().Be(1);
    }

    [Fact]
    public async Task Global_cap_one_with_parallel_users_grants_one_success_and_exhausts_the_rest()
    {
        const int requestCount = 12;
        await using var fixture = await PromoFileDbFixture.CreateAsync();
        await SeedPromoCodeAsync(fixture.CreateContext, "CAPONE", Now, maxRedemptionsGlobal: 1);
        var service = CreateService(fixture.CreateContext);

        var results = await Task.WhenAll(Enumerable.Range(1, requestCount).Select(index =>
            service.RedeemAsync(
                $"promo_cap_one_{index}",
                $"cap-one-{index}@example.com",
                "CAPONE",
                $"203.0.113.{20 + index}",
                Now)));

        results.Count(x => x.Kind == PromoRedeemResultKind.Success).Should().Be(1);
        results.Count(x => x.Kind == PromoRedeemResultKind.CapReached).Should().Be(requestCount - 1);

        await using var db = fixture.CreateContext();
        (await db.RewriteCredits.CountAsync()).Should().Be(1);
        (await db.PromoCodeRedemptions.CountAsync(x => x.Status == PromoCodeRedemptionStatus.Applied)).Should().Be(1);
        (await db.PromoCodes.SingleAsync()).RedemptionCount.Should().Be(1);
    }

    [Fact]
    public async Task Expired_and_disabled_codes_grant_no_credit()
    {
        await using var fixture = await PromoFileDbFixture.CreateAsync();
        await SeedPromoCodeAsync(
            fixture.CreateContext,
            "EXPIREDCASE",
            Now,
            validUntil: Now.AddTicks(-1));
        await SeedPromoCodeAsync(
            fixture.CreateContext,
            "DISABLEDCASE",
            Now,
            isActive: false);
        var service = CreateService(fixture.CreateContext);

        var expired = await service.RedeemAsync(
            "promo_expired_user",
            "expired@example.com",
            "EXPIREDCASE",
            "203.0.113.40",
            Now);
        var disabled = await service.RedeemAsync(
            "promo_disabled_user",
            "disabled@example.com",
            "DISABLEDCASE",
            "203.0.113.41",
            Now);

        expired.Kind.Should().Be(PromoRedeemResultKind.Expired);
        disabled.Kind.Should().Be(PromoRedeemResultKind.InvalidCode);

        await using var db = fixture.CreateContext();
        (await db.RewriteCredits.CountAsync()).Should().Be(0);
        (await db.PromoCodeRedemptions.CountAsync()).Should().Be(0);
        (await db.PromoCodes.SumAsync(x => x.RedemptionCount)).Should().Be(0);
    }

    [Theory]
    [InlineData(ProxySecretScenario.Missing)]
    [InlineData(ProxySecretScenario.Mismatched)]
    public async Task Redeem_api_treats_client_ip_as_untrusted_when_proxy_secret_is_missing_or_mismatched(
        ProxySecretScenario scenario)
    {
        await using var fixture = await PromoApiFixture.CreateAsync();
        await fixture.SeedPromoCodeAsync($"PROXY{scenario.ToString().ToUpperInvariant()}", Now);
        await using var factory = fixture.CreateFactory();
        var client = CreateClient(factory);

        var response = await PostRedeemAsync(
            client,
            $"PROXY{scenario.ToString().ToUpperInvariant()}",
            userId: $"promo_proxy_{scenario.ToString().ToLowerInvariant()}",
            clientIp: "203.0.113.80",
            proxySecret: scenario == ProxySecretScenario.Missing ? null : NewToken());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = fixture.CreateContext();
        var redemption = await db.PromoCodeRedemptions.SingleAsync();
        redemption.RedeemIpHash.Should().BeNull();
        (await db.RewriteCredits.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RedeemAsync_in_production_without_trusted_proxy_ip_fails_closed_without_credit()
    {
        await using var fixture = await PromoFileDbFixture.CreateAsync();
        await SeedPromoCodeAsync(fixture.CreateContext, "PRODPROXY", Now);
        var service = CreateService(fixture.CreateContext, BuildConfiguration(environmentName: "Production"));

        var result = await service.RedeemAsync(
            "promo_prod_proxy_user",
            "prod-proxy@example.com",
            "PRODPROXY",
            trustedClientIp: null,
            Now);

        result.Kind.Should().Be(PromoRedeemResultKind.ServerConfig);

        await using var db = fixture.CreateContext();
        (await db.AppUsers.CountAsync()).Should().Be(0);
        (await db.RewriteCredits.CountAsync()).Should().Be(0);
        (await db.PromoCodeRedemptions.CountAsync()).Should().Be(0);
        (await db.PromoCodes.SingleAsync()).RedemptionCount.Should().Be(0);
    }

    [Fact]
    public async Task Same_ip_across_many_accounts_is_flagged_from_two_and_blocked_at_five()
    {
        await using var fixture = await PromoFileDbFixture.CreateAsync();
        await SeedPromoCodeAsync(fixture.CreateContext, "IPVELOCITY", Now, maxRedemptionsGlobal: 20);
        var logger = new CapturingLogger<PromoService>();
        var service = CreateService(fixture.CreateContext, logger: logger);

        var accepted = new List<PromoRedeemResult>();
        for (var index = 1; index <= 5; index += 1)
        {
            accepted.Add(await service.RedeemAsync(
                $"promo_same_ip_{index}",
                $"same-ip-{index}@example.com",
                "IPVELOCITY",
                "203.0.113.90",
                Now.AddMinutes(index)));
        }

        var blocked = await service.RedeemAsync(
            "promo_same_ip_blocked",
            "same-ip-blocked@example.com",
            "IPVELOCITY",
            "203.0.113.90",
            Now.AddMinutes(6));

        accepted.Should().OnlyContain(x => x.Kind == PromoRedeemResultKind.Success);
        blocked.Kind.Should().Be(PromoRedeemResultKind.IpVelocityBlocked);
        logger.Messages.Should().Contain(x =>
            x.Level == LogLevel.Warning &&
            x.Message.Contains("velocity flag", StringComparison.OrdinalIgnoreCase) &&
            x.Message.Contains("with 2 recent", StringComparison.OrdinalIgnoreCase));
        logger.Messages.Should().Contain(x =>
            x.Level == LogLevel.Warning &&
            x.Message.Contains("velocity blocked", StringComparison.OrdinalIgnoreCase) &&
            x.Message.Contains("with 5 recent", StringComparison.OrdinalIgnoreCase));
        logger.Messages.Should().NotContain(x => x.Message.Contains("203.0.113.90", StringComparison.OrdinalIgnoreCase));

        await using var db = fixture.CreateContext();
        (await db.PromoCodeRedemptions.CountAsync(x => x.Status == PromoCodeRedemptionStatus.Applied)).Should().Be(5);
        (await db.RewriteCredits.CountAsync()).Should().Be(5);
        (await db.PromoCodes.SingleAsync()).RedemptionCount.Should().Be(5);
    }

    [Fact]
    public async Task ValidUntil_boundary_is_inclusive()
    {
        await using var fixture = await PromoFileDbFixture.CreateAsync();
        await SeedPromoCodeAsync(
            fixture.CreateContext,
            "BOUNDARY",
            Now,
            validFrom: Now.AddDays(-1),
            validUntil: Now);
        var service = CreateService(fixture.CreateContext);

        var result = await service.RedeemAsync(
            "promo_boundary_user",
            "boundary@example.com",
            "BOUNDARY",
            "203.0.113.100",
            Now);

        result.Kind.Should().Be(PromoRedeemResultKind.Success);

        await using var db = fixture.CreateContext();
        (await db.RewriteCredits.CountAsync()).Should().Be(1);
        (await db.PromoCodeRedemptions.CountAsync(x => x.Status == PromoCodeRedemptionStatus.Applied)).Should().Be(1);
        (await db.PromoCodes.SingleAsync()).RedemptionCount.Should().Be(1);
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
                ["PROMO_IP_HASH_SALT"] = NewToken(),
                ["PROMO_PROXY_SHARED_SECRET"] = NewToken(),
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

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
        });

    private static Task<HttpResponseMessage> PostRedeemAsync(
        HttpClient client,
        string code,
        string userId,
        string? clientIp,
        string? proxySecret)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/promo/redeem")
        {
            Content = JsonContent.Create(new { code, turnstileToken = NewToken() }),
        };
        request.Headers.Add("X-External-User-Id", userId);
        request.Headers.Add("X-User-Email", $"{userId}@example.com");

        if (!string.IsNullOrWhiteSpace(clientIp))
        {
            request.Headers.Add("X-Client-IP", clientIp);
        }

        if (!string.IsNullOrWhiteSpace(proxySecret))
        {
            request.Headers.Add("X-RIMV-Proxy-Secret", proxySecret);
        }

        return client.SendAsync(request);
    }

    private static string NewToken() => Guid.NewGuid().ToString("N");

    public enum ProxySecretScenario
    {
        Missing,
        Mismatched,
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
            var databasePath = Path.Combine(Path.GetTempPath(), $"replyinmyvoice-promo-concurrency-{Guid.NewGuid():N}.db");
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

    private sealed class PromoApiFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private readonly string _ipHashSalt = NewToken();
        private readonly string _proxySecret = NewToken();

        private PromoApiFixture()
        {
        }

        public static async Task<PromoApiFixture> CreateAsync()
        {
            var fixture = new PromoApiFixture();
            await fixture._connection.OpenAsync();
            await using var db = fixture.CreateContext();
            await db.Database.EnsureCreatedAsync();
            return fixture;
        }

        public WebApplicationFactory<Program> CreateFactory()
        {
            return new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Testing");
                    builder.ConfigureLogging(logging => logging.ClearProviders());
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["ALLOW_HEADER_AUTH"] = "true",
                            ["AZURE_FUNCTIONS_ENVIRONMENT"] = "Testing",
                            ["PROMO_IP_HASH_SALT"] = _ipHashSalt,
                            ["PROMO_PROXY_SHARED_SECRET"] = _proxySecret,
                        });
                    });
                    builder.ConfigureServices(services =>
                    {
                        var dbOptions = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                        if (dbOptions is not null)
                        {
                            services.Remove(dbOptions);
                        }

                        services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
                    });
                });
        }

        public async Task SeedPromoCodeAsync(string code, DateTimeOffset now)
        {
            await using var db = CreateContext();
            db.PromoCodes.Add(new PromoCode
            {
                Code = code,
                DisplayCode = code,
                Description = "Trial credits",
                Kind = PromoCodeKind.TrialCredits,
                CreditsGranted = 3,
                GrantTtlDays = 90,
                ValidFrom = now.AddDays(-1),
                ValidUntil = now.AddDays(30),
                MaxRedemptionsGlobal = 100,
                MaxRedemptionsPerUser = 1,
                RedemptionCount = 0,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        public AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;
            return new AppDbContext(options);
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }
    }
}
