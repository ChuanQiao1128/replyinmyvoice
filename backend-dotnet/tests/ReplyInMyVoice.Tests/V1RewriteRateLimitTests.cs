using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Queueing;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

[Collection("ApiKeyPepper")]
public sealed class V1RewriteRateLimitTests
{
    private const string TestApiKeyPepper = "rewrite-api-v1-rate-limit-test-pepper";
    private static readonly string?[] ApiKeyPepperVariants =
    [
        TestApiKeyPepper,
        "rewrite-api-v1-test-pepper",
        "api-key-service-test-pepper",
        "api-key-http-functions-test-pepper",
        null,
    ];

    [Fact]
    public async Task V1_rewrite_submit_enforces_per_key_rate_limit_under_concurrent_usage_write_failures()
    {
        const int rateLimitPerMinute = 2;
        await using var fixture = await FileBackedApiDbFixture.CreateAsync();
        var (user, token) = await SeedApiKeyUserAsync(
            fixture,
            "clerk_v1_rate_limit_concurrent",
            SubscriptionStatus.Active,
            DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
            rateLimitPerMinute);
        await using (var db = fixture.CreateContext())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER RejectApiKeyUsageWrites
                BEFORE INSERT ON ApiKeyUsages
                BEGIN
                    SELECT RAISE(ABORT, 'api usage write failed');
                END;
                """);
        }

        await using var factory = CreateFactory(fixture);
        var client = CreateClient(factory);
        var responses = await Task.WhenAll(
            Enumerable.Range(0, 10)
                .Select(index => PostV1RewriteAsync(
                    client,
                    token,
                    $"v1-concurrent-rate-{index}",
                    ValidV1Draft())));

        responses.Count(x => x.StatusCode == HttpStatusCode.Accepted).Should().Be(rateLimitPerMinute);
        responses.Count(x => x.StatusCode == HttpStatusCode.TooManyRequests).Should().Be(8);
        foreach (var response in responses.Where(x => x.StatusCode == HttpStatusCode.TooManyRequests))
        {
            await AssertErrorCodeAsync(response, "rate_limited");
            GetRequiredHeader(response, "X-RateLimit-Remaining").Should().Be("0");
        }

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(rateLimitPerMinute);
        (await verifyDb.UsageReservations.CountAsync()).Should().Be(rateLimitPerMinute);
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(rateLimitPerMinute);
        (await verifyDb.ApiKeyUsages.CountAsync()).Should().Be(0);
        var period = await verifyDb.UsagePeriods.SingleAsync();
        period.UserId.Should().Be(user.Id);
        period.ReservedCount.Should().Be(rateLimitPerMinute);
    }

    [Fact]
    public async Task V1_rewrite_submit_rejects_over_length_idempotency_key_without_attempt()
    {
        await using var fixture = await FileBackedApiDbFixture.CreateAsync();
        var (_, token) = await SeedApiKeyUserAsync(
            fixture,
            "clerk_v1_long_idempotency_key",
            SubscriptionStatus.Active,
            DateTimeOffset.Parse("2026-07-01T00:00:00Z"));
        await using var factory = CreateFactory(fixture);
        var client = CreateClient(factory);
        var overLengthIdempotencyKey = new string('k', 121);

        var response = await PostV1RewriteAsync(
            client,
            token,
            overLengthIdempotencyKey,
            ValidV1Draft());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorCodeAsync(response, "invalid_request");
        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(0);
        (await verifyDb.UsageReservations.CountAsync()).Should().Be(0);
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task V1_rewrite_submit_fails_closed_when_rate_limiter_is_unavailable()
    {
        await using var fixture = await FileBackedApiDbFixture.CreateAsync();
        var (_, token) = await SeedApiKeyUserAsync(
            fixture,
            "clerk_v1_rate_limiter_unavailable",
            SubscriptionStatus.Active,
            DateTimeOffset.Parse("2026-07-01T00:00:00Z"));
        await using var factory = CreateFactory(
            fixture,
            services =>
            {
                services.RemoveAll<IApiKeyRateLimiter>();
                services.AddScoped<IApiKeyRateLimiter, UnavailableRateLimiter>();
            });
        var client = CreateClient(factory);

        var response = await PostV1RewriteAsync(
            client,
            token,
            "v1-limiter-unavailable",
            ValidV1Draft());

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await AssertErrorCodeAsync(response, "rate_limit_unavailable");
        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(0);
        (await verifyDb.UsageReservations.CountAsync()).Should().Be(0);
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task V1_rewrite_submit_rejects_live_key_without_rewrite_scope_before_attempt_creation()
    {
        await using var fixture = await FileBackedApiDbFixture.CreateAsync();
        var (_, token) = await SeedApiKeyUserAsync(
            fixture,
            "clerk_v1_missing_rewrite_scope",
            SubscriptionStatus.Active,
            DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
            scope: "[\"usage\"]");
        await using var factory = CreateFactory(fixture);
        var client = CreateClient(factory);

        var response = await PostV1RewriteAsync(
            client,
            token,
            "v1-missing-rewrite-scope",
            ValidV1Draft());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertErrorCodeAsync(response, "insufficient_scope");
        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(0);
        (await verifyDb.UsageReservations.CountAsync()).Should().Be(0);
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("default", null)]
    [InlineData("empty", "")]
    [InlineData("whitespace", "   ")]
    [InlineData("empty-array", "[]")]
    public async Task V1_rewrite_submit_and_usage_allow_full_default_scopes(string keySuffix, string? scope)
    {
        await using var fixture = await FileBackedApiDbFixture.CreateAsync();
        var (_, token) = await SeedApiKeyUserAsync(
            fixture,
            $"clerk_v1_full_scope_{keySuffix}",
            SubscriptionStatus.Active,
            DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
            scope: scope);
        await using var factory = CreateFactory(fixture);
        var client = CreateClient(factory);

        var submit = await PostV1RewriteAsync(
            client,
            token,
            $"v1-full-scope-{keySuffix}",
            ValidV1Draft());
        var usage = await GetV1UsageAsync(client, token);

        submit.StatusCode.Should().Be(HttpStatusCode.Accepted);
        usage.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task V1_rewrite_submit_returns_429_when_db_window_was_filled_by_another_instance()
    {
        const int rateLimitPerMinute = 2;
        await using var fixture = await FileBackedApiDbFixture.CreateAsync();
        var (_, token) = await SeedApiKeyUserAsync(
            fixture,
            "clerk_v1_authoritative_db_window",
            SubscriptionStatus.Active,
            DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
            rateLimitPerMinute);
        var now = DateTimeOffset.UtcNow;
        await using (var db = fixture.CreateContext())
        {
            var apiKey = await db.ApiKeys.SingleAsync(x => x.Name == "V1 rate limit key");
            foreach (var windowStart in new[] { ToMinuteWindowStart(now), ToMinuteWindowStart(now.AddMinutes(1)) })
            {
                db.ApiKeyRateLimitWindows.Add(new ApiKeyRateLimitWindow
                {
                    ApiKeyId = apiKey.Id,
                    WindowStart = windowStart,
                    Count = rateLimitPerMinute,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }

            await db.SaveChangesAsync();
        }

        await using var factory = CreateFactory(fixture);
        var client = CreateClient(factory);

        var response = await PostV1RewriteAsync(
            client,
            token,
            "v1-authoritative-db-window",
            ValidV1Draft());

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        await AssertErrorCodeAsync(response, "rate_limited");
        GetRequiredHeader(response, "X-RateLimit-Remaining").Should().Be("0");
        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(0);
        (await verifyDb.UsageReservations.CountAsync()).Should().Be(0);
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void Migration_adds_api_key_usage_request_id_index()
    {
        var migrationsDirectory = FindMigrationsDirectory();
        var migrationText = string.Join(
            '\n',
            Directory.EnumerateFiles(migrationsDirectory, "*.cs")
                .Where(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal) &&
                    !path.EndsWith("ModelSnapshot.cs", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));

        migrationText.Should().Contain("IX_ApiKeyUsages_RequestId");
        migrationText.Should().Contain("table: \"ApiKeyUsages\"");
    }

    private static WebApplicationFactory<Program> CreateFactory(
        FileBackedApiDbFixture fixture,
        Action<IServiceCollection>? configureServices = null) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.RemoveAll<Func<AppDbContext>>();
                    services.AddDbContext<AppDbContext>(options => options.UseSqlite(fixture.ConnectionString));
                    services.AddScoped<Func<AppDbContext>>(sp =>
                    {
                        var options = sp.GetRequiredService<DbContextOptions<AppDbContext>>();
                        return () => new AppDbContext(options);
                    });
                    services.RemoveAll<IRewriteJobPublisher>();
                    services.RemoveAll<InMemoryRewriteJobPublisher>();
                    services.AddSingleton<InMemoryRewriteJobPublisher>();
                    services.AddSingleton<IRewriteJobPublisher>(sp => sp.GetRequiredService<InMemoryRewriteJobPublisher>());
                    configureServices?.Invoke(services);
                });
            });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
        });

    private static async Task<(AppUser User, string Token)> SeedApiKeyUserAsync(
        FileBackedApiDbFixture fixture,
        string externalAuthUserId,
        SubscriptionStatus subscriptionStatus,
        DateTimeOffset? currentPeriodEnd,
        int rateLimitPerMinute = 60,
        string? scope = null)
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestApiKeyPepper);
        var now = DateTimeOffset.UtcNow;
        var token = $"rmv_live_{externalAuthUserId}_token";
        await using var db = fixture.CreateContext();
        var user = new AppUser
        {
            ExternalAuthUserId = externalAuthUserId,
            Email = $"{externalAuthUserId}@example.com",
            StripeCustomerId = subscriptionStatus == SubscriptionStatus.Inactive ? null : $"cus_{externalAuthUserId}",
            StripeSubscriptionId = subscriptionStatus == SubscriptionStatus.Inactive ? null : $"sub_{externalAuthUserId}",
            SubscriptionStatus = subscriptionStatus,
            CurrentPeriodEnd = currentPeriodEnd,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.AppUsers.Add(user);
        var seenHashes = new HashSet<string>(StringComparer.Ordinal);
        var keyIndex = 0;
        foreach (var pepper in ApiKeyPepperVariants)
        {
            var keyHash = ComputeApiKeyHash(token, pepper);
            if (!seenHashes.Add(keyHash))
            {
                continue;
            }

            var apiKey = new ApiKey
            {
                User = user,
                Name = keyIndex == 0 ? "V1 rate limit key" : $"V1 rate limit key {keyIndex}",
                KeyHash = keyHash,
                Last4 = token[^4..],
                RateLimitPerMinute = rateLimitPerMinute,
                CreatedAt = now,
                UpdatedAt = now,
            };
            if (scope is not null)
            {
                apiKey.Scope = scope;
            }

            db.ApiKeys.Add(apiKey);
            keyIndex += 1;
        }

        await db.SaveChangesAsync();
        return (user, token);
    }

    private static Task<HttpResponseMessage> PostV1RewriteAsync(
        HttpClient client,
        string token,
        string idempotencyKey,
        string draft)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/rewrite")
        {
            Content = JsonContent.Create(new { draft }),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> GetV1UsageAsync(HttpClient client, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/usage");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    private static async Task AssertErrorCodeAsync(HttpResponseMessage response, string expectedCode)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        document.RootElement
            .GetProperty("error")
            .GetProperty("code")
            .GetString()
            .Should()
            .Be(expectedCode);
    }

    private static string GetRequiredHeader(HttpResponseMessage response, string name)
    {
        response.Headers.TryGetValues(name, out var values).Should().BeTrue();
        return values!.Single();
    }

    private static string ValidV1Draft() =>
        "Please let the client know the report is still being checked and I will send a clear update soon.";

    private static DateTimeOffset ToMinuteWindowStart(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        return new DateTimeOffset(
            utc.Year,
            utc.Month,
            utc.Day,
            utc.Hour,
            utc.Minute,
            0,
            TimeSpan.Zero);
    }

    private static string ComputeApiKeyHash(string plaintext, string? pepper)
    {
        var material = string.IsNullOrWhiteSpace(pepper)
            ? plaintext
            : string.Concat(pepper, plaintext);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string FindMigrationsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "ReplyInMyVoice.Infrastructure",
                "Migrations");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate EF migrations directory.");
    }

    private sealed class UnavailableRateLimiter : IApiKeyRateLimiter
    {
        public Task<ApiKeyRateLimitResult> CheckAndIncrementAsync(
            Guid apiKeyId,
            int rateLimitPerMinute,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.FromResult(ApiKeyRateLimitResult.Unavailable(rateLimitPerMinute, now));
    }

    private sealed class FileBackedApiDbFixture : IAsyncDisposable
    {
        private readonly string _databasePath;

        private FileBackedApiDbFixture(string databasePath)
        {
            _databasePath = databasePath;
        }

        public string ConnectionString => $"Data Source={_databasePath};Default Timeout=5";

        public static async Task<FileBackedApiDbFixture> CreateAsync()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"replyinmyvoice-api-rate-limit-{Guid.NewGuid():N}.db");
            var fixture = new FileBackedApiDbFixture(databasePath);
            await using var db = fixture.CreateContext();
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;");
            return fixture;
        }

        public AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(ConnectionString)
                .EnableSensitiveDataLogging()
                .Options;
            return new AppDbContext(options);
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
