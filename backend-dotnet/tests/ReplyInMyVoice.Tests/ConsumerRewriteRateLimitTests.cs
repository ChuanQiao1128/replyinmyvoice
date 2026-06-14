using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Queueing;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class ConsumerRewriteRateLimitTests
{
    [Fact]
    public async Task Consumer_rewrite_rate_limited_requests_return_429_and_do_not_consume_quota()
    {
        const int rateLimitPerMinute = 2;
        await using var fixture = await FileBackedConsumerDbFixture.CreateAsync();
        var user = await SeedActiveUserAsync(fixture, "clerk_consumer_rate_limit");
        await using var factory = CreateFactory(
            fixture,
            services =>
            {
                services.RemoveAll<IUserRewriteRateLimiter>();
                services.AddScoped<IUserRewriteRateLimiter>(sp =>
                    new UserRewriteRateLimiter(
                        sp.GetRequiredService<Func<AppDbContext>>(),
                        rateLimitPerMinute));
            });
        var client = CreateClient(factory, user.ExternalAuthUserId);

        var responses = new List<HttpResponseMessage>();
        for (var index = 0; index < 5; index += 1)
        {
            responses.Add(await PostRewriteAsync(client, $"consumer-rate-limit-{index}"));
        }

        responses.Take(2).Should().OnlyContain(x => x.StatusCode == HttpStatusCode.Accepted);
        responses.Skip(2).Should().OnlyContain(x => x.StatusCode == HttpStatusCode.TooManyRequests);
        GetRequiredHeader(responses[0], "X-RateLimit-Limit").Should().Be("2");
        GetRequiredHeader(responses[0], "X-RateLimit-Remaining").Should().Be("1");
        GetRequiredHeader(responses[1], "X-RateLimit-Remaining").Should().Be("0");
        foreach (var response in responses.Skip(2))
        {
            await AssertProblemCodeAsync(response, "rate_limited");
            GetRequiredHeader(response, "Retry-After").Should().NotBeNullOrWhiteSpace();
            int.Parse(GetRequiredHeader(response, "Retry-After")).Should().BeGreaterThanOrEqualTo(1);
            GetRequiredHeader(response, "X-RateLimit-Limit").Should().Be("2");
            GetRequiredHeader(response, "X-RateLimit-Remaining").Should().Be("0");
            GetRequiredHeader(response, "X-RateLimit-Reset").Should().NotBeNullOrWhiteSpace();
        }

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(rateLimitPerMinute);
        (await verifyDb.UsageReservations.CountAsync()).Should().Be(rateLimitPerMinute);
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(rateLimitPerMinute);
        var period = await verifyDb.UsagePeriods.SingleAsync();
        period.UserId.Should().Be(user.Id);
        period.ReservedCount.Should().Be(rateLimitPerMinute);
    }

    [Fact]
    public async Task Consumer_rewrite_rate_limiter_unavailable_fails_closed_without_attempts()
    {
        await using var fixture = await FileBackedConsumerDbFixture.CreateAsync();
        var user = await SeedActiveUserAsync(fixture, "clerk_consumer_rate_limit_unavailable");
        await using var factory = CreateFactory(
            fixture,
            services =>
            {
                services.RemoveAll<IUserRewriteRateLimiter>();
                services.AddScoped<IUserRewriteRateLimiter, UnavailableUserRateLimiter>();
            });
        var client = CreateClient(factory, user.ExternalAuthUserId);

        var response = await PostRewriteAsync(client, "consumer-limiter-unavailable");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await AssertProblemCodeAsync(response, "rate_limit_unavailable");
        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(0);
        (await verifyDb.UsageReservations.CountAsync()).Should().Be(0);
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Consumer_rewrite_rate_limit_disabled_when_limit_non_positive()
    {
        await using var fixture = await FileBackedConsumerDbFixture.CreateAsync();
        var user = await SeedActiveUserAsync(fixture, "clerk_consumer_rate_limit_disabled");
        await using var factory = CreateFactory(
            fixture,
            services =>
            {
                services.RemoveAll<IUserRewriteRateLimiter>();
                services.AddScoped<IUserRewriteRateLimiter>(sp =>
                    new UserRewriteRateLimiter(
                        sp.GetRequiredService<Func<AppDbContext>>(),
                        0));
            });
        var client = CreateClient(factory, user.ExternalAuthUserId);

        var responses = new[]
        {
            await PostRewriteAsync(client, "consumer-disabled-0"),
            await PostRewriteAsync(client, "consumer-disabled-1"),
            await PostRewriteAsync(client, "consumer-disabled-2"),
        };

        responses.Should().OnlyContain(x => x.StatusCode == HttpStatusCode.Accepted);
        responses.Should().OnlyContain(x => !x.Headers.Contains("X-RateLimit-Limit"));
        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteAttempts.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task User_rewrite_rate_limiter_resets_in_new_minute_window()
    {
        const int limit = 2;
        var now = DateTimeOffset.Parse("2026-06-13T02:15:12Z");
        await using var fixture = await FileBackedConsumerDbFixture.CreateAsync();
        var user = await SeedActiveUserAsync(fixture, "clerk_consumer_window_reset");
        var limiter = new UserRewriteRateLimiter(fixture.CreateContext, limit);

        var first = await limiter.CheckAndIncrementAsync(user.Id, now, CancellationToken.None);
        var second = await limiter.CheckAndIncrementAsync(user.Id, now.AddSeconds(10), CancellationToken.None);
        var third = await limiter.CheckAndIncrementAsync(user.Id, now.AddSeconds(20), CancellationToken.None);
        var nextWindow = await limiter.CheckAndIncrementAsync(user.Id, now.AddSeconds(61), CancellationToken.None);

        first.IsLimited.Should().BeFalse();
        second.IsLimited.Should().BeFalse();
        third.IsLimited.Should().BeTrue();
        nextWindow.IsLimited.Should().BeFalse();
        nextWindow.Remaining.Should().Be(limit - 1);
        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.UserRewriteRateLimitWindows.CountAsync()).Should().Be(2);
    }

    [Fact]
    public void Migration_adds_user_rewrite_rate_limit_windows()
    {
        var migrationsDirectory = FindMigrationsDirectory();
        var migrationText = string.Join(
            '\n',
            Directory.EnumerateFiles(migrationsDirectory, "*.cs")
                .Where(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal) &&
                    !path.EndsWith("ModelSnapshot.cs", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));

        migrationText.Should().Contain("UserRewriteRateLimitWindows");
        migrationText.Should().Contain("IX_UserRewriteRateLimitWindows_UserId_WindowStart");
    }

    private static WebApplicationFactory<Program> CreateFactory(
        FileBackedConsumerDbFixture fixture,
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

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, string externalAuthUserId)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
        });
        client.DefaultRequestHeaders.Add("X-External-User-Id", externalAuthUserId);
        return client;
    }

    private static async Task<AppUser> SeedActiveUserAsync(
        FileBackedConsumerDbFixture fixture,
        string externalAuthUserId)
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = fixture.CreateContext();
        var user = new AppUser
        {
            ExternalAuthUserId = externalAuthUserId,
            Email = $"{externalAuthUserId}@example.com",
            StripeCustomerId = $"cus_{externalAuthUserId}",
            StripeSubscriptionId = $"sub_{externalAuthUserId}",
            SubscriptionStatus = SubscriptionStatus.Active,
            CurrentPeriodEnd = DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static Task<HttpResponseMessage> PostRewriteAsync(HttpClient client, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/rewrite")
        {
            Content = JsonContent.Create(ValidRequest()),
        };
        request.Headers.Add("X-Idempotency-Key", idempotencyKey);
        return client.SendAsync(request);
    }

    private static RewriteRequest ValidRequest() =>
        new(
            "Can you send an update?",
            "Please let them know the report is still being checked and I will follow up soon.",
            "Client",
            "Reply",
            "The report is still being checked.",
            "No promised timeline.",
            "warm");

    private static async Task AssertProblemCodeAsync(HttpResponseMessage response, string expectedCode)
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

    private sealed class UnavailableUserRateLimiter : IUserRewriteRateLimiter
    {
        public bool Enabled => true;

        public int LimitPerMinute => 6;

        public Task<ApiKeyRateLimitResult> CheckAndIncrementAsync(
            Guid userId,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.FromResult(ApiKeyRateLimitResult.Unavailable(LimitPerMinute, now));
    }

    private sealed class FileBackedConsumerDbFixture : IAsyncDisposable
    {
        private readonly string _databasePath;

        private FileBackedConsumerDbFixture(string databasePath)
        {
            _databasePath = databasePath;
        }

        public string ConnectionString => $"Data Source={_databasePath};Default Timeout=5";

        public static async Task<FileBackedConsumerDbFixture> CreateAsync()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"replyinmyvoice-consumer-rate-limit-{Guid.NewGuid():N}.db");
            var fixture = new FileBackedConsumerDbFixture(databasePath);
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
