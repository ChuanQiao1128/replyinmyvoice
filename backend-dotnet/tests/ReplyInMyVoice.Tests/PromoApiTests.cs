using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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

namespace ReplyInMyVoice.Tests;

public sealed class PromoApiTests : IAsyncLifetime
{
    private const string ProxySecret = "proxy-test-secret";
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    static PromoApiTests()
    {
        Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
        Environment.SetEnvironmentVariable("ASPNETCORE_hostBuilder__reloadConfigOnChange", "false");
    }

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
    public async Task Redeem_requires_authentication()
    {
        await SeedPromoCodeAsync("AUTHCHECK");
        await using var factory = CreateFactory();
        var client = CreateClient(factory);

        var response = await PostRedeemAsync(client, "AUTHCHECK", authenticated: false);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await using var db = CreateContext();
        (await db.AppUsers.CountAsync()).Should().Be(0);
        (await db.RewriteCredits.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Redeem_rejects_invalid_request_body()
    {
        await using var factory = CreateFactory();
        var client = CreateClient(factory);

        var response = await PostRedeemAsync(client, rawJson: "{");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorAsync(response)).Should().Be("invalid_request");
    }

    [Fact]
    public async Task Redeem_rejects_request_without_verified_turnstile_token()
    {
        await SeedPromoCodeAsync("TURNSTILECHECK");
        await using var factory = CreateFactory();
        var client = CreateClient(factory);

        var response = await PostRedeemAsync(
            client,
            "TURNSTILECHECK",
            turnstileToken: "");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadErrorAsync(response)).Should().Be("invalid_captcha");
        await using var db = CreateContext();
        (await db.RewriteCredits.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Redeem_unknown_inactive_and_not_yet_valid_are_identical_invalid_code_errors()
    {
        await SeedPromoCodeAsync("INACTIVECHECK", isActive: false);
        await SeedPromoCodeAsync(
            "FUTURECHECK",
            validFrom: DateTimeOffset.UtcNow.AddHours(1),
            validUntil: DateTimeOffset.UtcNow.AddDays(2));
        await using var factory = CreateFactory();
        var client = CreateClient(factory);

        var unknown = await PostRedeemAsync(client, "UNKNOWNCHECK", userId: "promo_unknown");
        var inactive = await PostRedeemAsync(client, "INACTIVECHECK", userId: "promo_inactive");
        var future = await PostRedeemAsync(client, "FUTURECHECK", userId: "promo_future");

        var bodies = new[]
        {
            await ReadRawBodyAsync(unknown),
            await ReadRawBodyAsync(inactive),
            await ReadRawBodyAsync(future),
        };

        unknown.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        inactive.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        future.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        bodies.Should().OnlyContain(x => x == bodies[0]);
        bodies[0].Should().Contain("invalid_code");
    }

    [Fact]
    public async Task Redeem_expired_code_returns_code_expired()
    {
        await SeedPromoCodeAsync("EXPIREDCHECK", validUntil: DateTimeOffset.UtcNow.AddSeconds(-1));
        await using var factory = CreateFactory();
        var client = CreateClient(factory);

        var response = await PostRedeemAsync(client, "EXPIREDCHECK");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadErrorAsync(response)).Should().Be("code_expired");
    }

    [Fact]
    public async Task Redeem_second_attempt_returns_already_redeemed_without_second_credit()
    {
        await SeedPromoCodeAsync("ALREADYCHECK");
        await using var factory = CreateFactory();
        var client = CreateClient(factory);

        var first = await PostRedeemAsync(client, "ALREADYCHECK", userId: "promo_already");
        var second = await PostRedeemAsync(client, "ALREADYCHECK", userId: "promo_already");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadErrorAsync(second)).Should().Be("already_redeemed");
        await using var db = CreateContext();
        (await db.RewriteCredits.CountAsync()).Should().Be(1);
        (await db.PromoCodeRedemptions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Redeem_exhausted_code_returns_code_exhausted()
    {
        await SeedPromoCodeAsync("CAPCHECK", maxRedemptionsGlobal: 1);
        await using var factory = CreateFactory();
        var client = CreateClient(factory);

        var first = await PostRedeemAsync(client, "CAPCHECK", userId: "promo_cap_first");
        var second = await PostRedeemAsync(client, "CAPCHECK", userId: "promo_cap_second");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadErrorAsync(second)).Should().Be("code_exhausted");
    }

    [Fact]
    public async Task Redeem_blocks_ip_velocity_after_five_recent_redemptions()
    {
        await SeedPromoCodeAsync("IPCHECK", maxRedemptionsGlobal: 20);
        await using var factory = CreateFactory();
        var client = CreateClient(factory);

        for (var index = 1; index <= 5; index += 1)
        {
            var accepted = await PostRedeemAsync(
                client,
                "IPCHECK",
                userId: $"promo_ip_{index}",
                clientIp: "203.0.113.44");
            accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var blocked = await PostRedeemAsync(
            client,
            "IPCHECK",
            userId: "promo_ip_blocked",
            clientIp: "203.0.113.44");

        blocked.StatusCode.Should().Be((HttpStatusCode)429);
        (await ReadErrorAsync(blocked)).Should().Be("ip_velocity");
    }

    [Fact]
    public async Task Redeem_returns_server_config_when_ip_hash_salt_is_missing()
    {
        await SeedPromoCodeAsync("CONFIGCHECK");
        await using var factory = CreateFactory(includeIpHashSalt: false);
        var client = CreateClient(factory);

        var response = await PostRedeemAsync(
            client,
            "CONFIGCHECK",
            userId: "promo_config",
            clientIp: "203.0.113.77");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await ReadErrorAsync(response)).Should().Be("server_config");
        await using var db = CreateContext();
        (await db.AppUsers.CountAsync()).Should().Be(0);
        (await db.RewriteCredits.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Redeem_success_grants_promo_credit_and_reports_total_remaining()
    {
        await SeedPromoCodeAsync("SUCCESSCHECK");
        await using var factory = CreateFactory();
        var client = CreateClient(factory);

        var before = DateTimeOffset.UtcNow;
        var response = await PostRedeemAsync(
            client,
            "success-check",
            userId: "promo_success",
            email: "success@example.com",
            clientIp: "203.0.113.55");
        var after = DateTimeOffset.UtcNow;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PromoRedeemResponse>();
        body.Should().NotBeNull();
        body!.CreditsGranted.Should().Be(3);
        body.TotalRemaining.Should().Be(3);
        body.ExpiresAt.Should().NotBeNull();
        body.ExpiresAt!.Value.Should().BeOnOrAfter(before.AddDays(90));
        body.ExpiresAt.Value.Should().BeOnOrBefore(after.AddDays(90).AddSeconds(1));
        body.AlreadyRedeemed.Should().BeFalse();

        await using var db = CreateContext();
        var user = await db.AppUsers.SingleAsync();
        user.ExternalAuthUserId.Should().Be("promo_success");
        var credit = await db.RewriteCredits.SingleAsync();
        credit.Source.Should().Be("PROMO");
        credit.AmountGranted.Should().Be(3);
        var redemption = await db.PromoCodeRedemptions.SingleAsync();
        redemption.UserId.Should().Be(user.Id);
        redemption.RedeemIpHash.Should().NotBeNullOrWhiteSpace();
        redemption.RedeemIpHash.Should().NotContain("203.0.113.55");
    }

    [Fact]
    public async Task Redeem_ignores_client_forwarded_for_without_proxy_secret()
    {
        await SeedPromoCodeAsync("XFFCHECK");
        await using var factory = CreateFactory();
        var client = CreateClient(factory);

        var response = await PostRedeemAsync(
            client,
            "XFFCHECK",
            userId: "promo_xff",
            forwardedFor: "203.0.113.66");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var db = CreateContext();
        var redemption = await db.PromoCodeRedemptions.SingleAsync();
        redemption.RedeemIpHash.Should().BeNull();
    }

    private WebApplicationFactory<Program> CreateFactory(
        string environment = "Testing",
        bool includeIpHashSalt = true)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environment);
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    var values = new Dictionary<string, string?>
                    {
                        ["ALLOW_HEADER_AUTH"] = "true",
                        ["AZURE_FUNCTIONS_ENVIRONMENT"] = environment,
                        ["PROMO_PROXY_SHARED_SECRET"] = ProxySecret,
                    };
                    if (includeIpHashSalt)
                    {
                        values["PROMO_IP_HASH_SALT"] = "promo-test-salt";
                    }

                    config.AddInMemoryCollection(values);
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

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
        });

    private async Task SeedPromoCodeAsync(
        string code,
        int creditsGranted = 3,
        int grantTtlDays = 90,
        int? maxRedemptionsGlobal = 100,
        bool isActive = true,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validUntil = null)
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = CreateContext();
        db.PromoCodes.Add(new PromoCode
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
        });
        await db.SaveChangesAsync();
    }

    private static Task<HttpResponseMessage> PostRedeemAsync(
        HttpClient client,
        string code = "PROMOCHECK",
        string userId = "promo_user",
        string email = "promo@example.com",
        string turnstileToken = "turnstile-test-token",
        string? clientIp = null,
        string? forwardedFor = null,
        string? rawJson = null,
        bool authenticated = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/promo/redeem")
        {
            Content = rawJson is null
                ? JsonContent.Create(new { code, turnstileToken })
                : new StringContent(rawJson, Encoding.UTF8, "application/json"),
        };
        if (authenticated)
        {
            request.Headers.Add("X-External-User-Id", userId);
            request.Headers.Add("X-User-Email", email);
        }

        if (!string.IsNullOrWhiteSpace(clientIp))
        {
            request.Headers.Add("X-Client-IP", clientIp);
            request.Headers.Add("X-RIMV-Proxy-Secret", ProxySecret);
        }

        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return client.SendAsync(request);
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.TryGetProperty("error", out var error)
            ? error.GetString()
            : null;
    }

    private static Task<string> ReadRawBodyAsync(HttpResponseMessage response) =>
        response.Content.ReadAsStringAsync();

    private sealed record PromoRedeemResponse(
        int CreditsGranted,
        int TotalRemaining,
        DateTimeOffset? ExpiresAt,
        bool AlreadyRedeemed);
}
