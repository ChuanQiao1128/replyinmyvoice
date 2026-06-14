using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Account;
using ReplyInMyVoice.Application.UseCases.Rewrite;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

[Collection("ApiKeyPepper")]
public sealed class V1RewriteRepositoryRoutingTests
{
    private const string TestApiKeyPepper = "v1-repository-routing-test-pepper";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly string?[] ApiKeyPepperVariants =
    [
        TestApiKeyPepper,
        "rewrite-api-v1-test-pepper",
        "api-key-service-test-pepper",
        null,
    ];

    [Fact]
    public async Task Live_submit_routes_attempt_and_usage_writes_through_repositories()
    {
        await using var fixture = await FileBackedApiDbFixture.CreateAsync();
        var recorder = new RepositoryCallRecorder();
        var (user, token) = await SeedApiKeyUserAsync(
            fixture,
            "clerk_v1_repository_live",
            SubscriptionStatus.Active,
            DateTimeOffset.Parse("2026-07-01T00:00:00Z"));
        await using var functionDb = fixture.CreateContext();
        var functions = CreateV1Functions(functionDb, fixture.CreateContext, recorder);

        var result = await functions.SubmitRewrite(
            CreateV1Request(token, "v1-repository-live", ValidV1Draft()),
            CancellationToken.None);

        var body = ReadAcceptedResult(result);
        body.Status.Should().Be("processing");
        await using var db = fixture.CreateContext();
        var attempt = await db.RewriteAttempts.SingleAsync();
        attempt.Id.Should().Be(body.Id);
        attempt.UserId.Should().Be(user.Id);
        attempt.Status.Should().Be(RewriteAttemptStatus.Pending);
        var usage = await db.ApiKeyUsages.SingleAsync();
        usage.Endpoint.Should().Be("v1/rewrite");
        usage.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        usage.RequestId.Should().Be(body.Id.ToString());
        recorder.ApiKeyHashLookups.Should().BeGreaterThan(0);
        recorder.ApiKeyLastUsedTouches.Should().BeGreaterThan(0);
        recorder.RewriteAttemptAdds.Should().Be(1);
        recorder.ApiKeyUsageAdds.Should().Be(1);
    }

    [Fact]
    public async Task Sandbox_submit_creates_attempt_and_reuses_idempotency_through_repositories()
    {
        await using var fixture = await FileBackedApiDbFixture.CreateAsync();
        var recorder = new RepositoryCallRecorder();
        var (_, token) = await SeedApiKeyUserAsync(
            fixture,
            "clerk_v1_repository_sandbox",
            SubscriptionStatus.Inactive,
            currentPeriodEnd: null,
            isTest: true);
        await using var functionDb = fixture.CreateContext();
        var functions = CreateV1Functions(functionDb, fixture.CreateContext, recorder);
        const string idempotencyKey = "v1-repository-sandbox";
        var draft = ValidV1Draft();

        var first = await functions.SubmitRewrite(
            CreateV1Request(token, idempotencyKey, draft),
            CancellationToken.None);
        var second = await functions.SubmitRewrite(
            CreateV1Request(token, idempotencyKey, draft),
            CancellationToken.None);

        var firstBody = ReadAcceptedResult(first);
        var secondBody = ReadAcceptedResult(second);
        secondBody.Id.Should().Be(firstBody.Id);
        secondBody.Status.Should().Be("processing");
        await using var db = fixture.CreateContext();
        var attempt = await db.RewriteAttempts.SingleAsync();
        attempt.Id.Should().Be(firstBody.Id);
        attempt.Status.Should().Be(RewriteAttemptStatus.Succeeded);
        (await db.UsageReservations.CountAsync()).Should().Be(0);
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
        (await db.ApiKeyUsages.CountAsync()).Should().Be(2);
        recorder.RewriteAttemptIdempotencyLookups.Should().Be(2);
        recorder.RewriteAttemptAdds.Should().Be(1);
        recorder.ApiKeyUsageAdds.Should().Be(2);
    }

    private static V1RewriteHttpFunctions CreateV1Functions(
        AppDbContext db,
        Func<AppDbContext> createContext,
        RepositoryCallRecorder recorder)
    {
        var configuration = new ConfigurationBuilder().Build();
        var appUsers = new AppUserRepository(db);
        var usagePeriods = new UsagePeriodRepository(db);
        var rewriteAttempts = new RecordingRewriteAttemptRepository(
            new RewriteAttemptRepository(db),
            recorder);
        var reservations = new UsageReservationRepository(db);
        var credits = new RewriteCreditRepository(db);
        var outboxMessages = new OutboxMessageRepository(db);
        var promoRedemptions = new PromoCodeRedemptionRepository(db);
        var promoCodes = new PromoCodeRepository(db);
        var apiKeys = new RecordingApiKeyRepository(
            new ApiKeyRepository(db),
            recorder);
        var apiKeyUsages = new RecordingApiKeyUsageRepository(
            new ApiKeyUsageRepository(db),
            recorder);
        var usagePlans = new AccountUsagePlanProvider(configuration);
        var unitOfWork = new UnitOfWork(db);

        return new V1RewriteHttpFunctions(
            configuration,
            new ApiKeyAuthResolver(apiKeys, unitOfWork),
            appUsers,
            rewriteAttempts,
            apiKeyUsages,
            unitOfWork,
            new ApiKeyRateLimiter(createContext),
            new HasPaidApiEntitlementHandler(appUsers, credits),
            new CreateRewriteAttemptHandler(
                appUsers,
                usagePeriods,
                rewriteAttempts,
                reservations,
                credits,
                outboxMessages,
                unitOfWork,
                new NoopOutboxFastPathDispatcher()),
            new GetRewriteAttemptHandler(rewriteAttempts),
            new GetAccountSummaryHandler(
                appUsers,
                usagePeriods,
                credits,
                promoRedemptions,
                promoCodes,
                usagePlans,
                unitOfWork));
    }

    private static async Task<(AppUser User, string Token)> SeedApiKeyUserAsync(
        FileBackedApiDbFixture fixture,
        string externalAuthUserId,
        SubscriptionStatus subscriptionStatus,
        DateTimeOffset? currentPeriodEnd,
        int rateLimitPerMinute = 60,
        bool isTest = false)
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestApiKeyPepper);
        var now = DateTimeOffset.UtcNow;
        var tokenPrefix = isTest ? "rmv_test_" : "rmv_live_";
        var token = $"{tokenPrefix}{externalAuthUserId}_token";
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

            db.ApiKeys.Add(new ApiKey
            {
                User = user,
                Name = keyIndex == 0 ? "V1 repository routing key" : $"V1 repository routing key {keyIndex}",
                KeyHash = keyHash,
                Last4 = token[^4..],
                IsTest = isTest,
                RateLimitPerMinute = rateLimitPerMinute,
                CreatedAt = now,
                UpdatedAt = now,
            });
            keyIndex += 1;
        }

        await db.SaveChangesAsync();
        return (user, token);
    }

    private static HttpRequest CreateV1Request(
        string token,
        string idempotencyKey,
        string draft)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/v1/rewrite";
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { draft })));
        context.Request.Headers.Authorization = $"Bearer {token}";
        context.Request.Headers["Idempotency-Key"] = idempotencyKey;
        return context.Request;
    }

    private static V1RewriteSubmitResponse ReadAcceptedResult(IActionResult result)
    {
        var accepted = result.Should().BeOfType<AcceptedResult>().Subject;
        var json = JsonSerializer.Serialize(accepted.Value);
        return JsonSerializer.Deserialize<V1RewriteSubmitResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException("Accepted result body was not readable.");
    }

    private static string ValidV1Draft() =>
        "Please let the client know the report is still being checked and I will send a clear update soon.";

    private static string ComputeApiKeyHash(string plaintext, string? pepper)
    {
        var material = string.IsNullOrWhiteSpace(pepper)
            ? plaintext
            : string.Concat(pepper, plaintext);
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record V1RewriteSubmitResponse(Guid Id, string Status);

    private sealed class RepositoryCallRecorder
    {
        private int _apiKeyHashLookups;
        private int _apiKeyLastUsedTouches;
        private int _rewriteAttemptAdds;
        private int _rewriteAttemptIdempotencyLookups;
        private int _apiKeyUsageAdds;

        public int ApiKeyHashLookups => _apiKeyHashLookups;
        public int ApiKeyLastUsedTouches => _apiKeyLastUsedTouches;
        public int RewriteAttemptAdds => _rewriteAttemptAdds;
        public int RewriteAttemptIdempotencyLookups => _rewriteAttemptIdempotencyLookups;
        public int ApiKeyUsageAdds => _apiKeyUsageAdds;

        public void RecordApiKeyHashLookup() => Interlocked.Increment(ref _apiKeyHashLookups);
        public void RecordApiKeyLastUsedTouch() => Interlocked.Increment(ref _apiKeyLastUsedTouches);
        public void RecordRewriteAttemptAdd() => Interlocked.Increment(ref _rewriteAttemptAdds);
        public void RecordRewriteAttemptIdempotencyLookup() => Interlocked.Increment(ref _rewriteAttemptIdempotencyLookups);
        public void RecordApiKeyUsageAdd() => Interlocked.Increment(ref _apiKeyUsageAdds);
    }

    private sealed class RecordingApiKeyRepository(
        IApiKeyRepository inner,
        RepositoryCallRecorder recorder) : IApiKeyRepository
    {
        public Task AddAsync(ApiKey apiKey, CancellationToken ct = default) =>
            inner.AddAsync(apiKey, ct);

        public Task<ApiKey?> GetByIdForUserAsync(Guid userId, Guid keyId, CancellationToken ct = default) =>
            inner.GetByIdForUserAsync(userId, keyId, ct);

        public Task<IReadOnlyList<ApiKey>> ListByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            inner.ListByUserIdAsync(userId, ct);

        public Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken ct = default)
        {
            recorder.RecordApiKeyHashLookup();
            return inner.GetByKeyHashAsync(keyHash, ct);
        }

        public void TouchLastUsed(ApiKey apiKey, DateTimeOffset now)
        {
            recorder.RecordApiKeyLastUsedTouch();
            inner.TouchLastUsed(apiKey, now);
        }

        public void DiscardPendingChanges(ApiKey apiKey)
        {
            inner.DiscardPendingChanges(apiKey);
        }
    }

    private sealed class RecordingRewriteAttemptRepository(
        IRewriteAttemptRepository inner,
        RepositoryCallRecorder recorder) : IRewriteAttemptRepository
    {
        public Task AddAsync(RewriteAttempt attempt, CancellationToken ct = default)
        {
            recorder.RecordRewriteAttemptAdd();
            return inner.AddAsync(attempt, ct);
        }

        public Task<RewriteAttempt?> GetByIdAsync(Guid attemptId, CancellationToken ct = default) =>
            inner.GetByIdAsync(attemptId, ct);

        public Task<RewriteAttempt?> GetByIdNoTrackingAsync(Guid attemptId, CancellationToken ct = default) =>
            inner.GetByIdNoTrackingAsync(attemptId, ct);

        public Task<RewriteAttempt?> GetByIdForUserAsync(Guid attemptId, Guid userId, CancellationToken ct = default) =>
            inner.GetByIdForUserAsync(attemptId, userId, ct);

        public Task<RewriteAttempt?> GetByUserIdAndIdempotencyKeyAsync(
            Guid userId,
            string idempotencyKey,
            CancellationToken ct = default)
        {
            recorder.RecordRewriteAttemptIdempotencyLookup();
            return inner.GetByUserIdAndIdempotencyKeyAsync(userId, idempotencyKey, ct);
        }

        public Task<IReadOnlyList<RewriteAttempt>> ListByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            inner.ListByUserIdAsync(userId, ct);
    }

    private sealed class RecordingApiKeyUsageRepository(
        IApiKeyUsageRepository inner,
        RepositoryCallRecorder recorder) : IApiKeyUsageRepository
    {
        public Task AddAsync(ApiKeyUsage usage, CancellationToken ct = default)
        {
            recorder.RecordApiKeyUsageAdd();
            return inner.AddAsync(usage, ct);
        }

        public Task<IReadOnlyDictionary<Guid, ApiUsageCountDto>> CountByApiKeyAsync(
            IReadOnlyCollection<Guid> apiKeyIds,
            DateTimeOffset windowStart,
            DateTimeOffset windowEnd,
            CancellationToken ct = default) =>
            inner.CountByApiKeyAsync(apiKeyIds, windowStart, windowEnd, ct);

        public Task<IReadOnlyList<ApiUsageRowDto>> ListUsageRowsAsync(
            Guid userId,
            DateTimeOffset windowStart,
            CancellationToken ct = default) =>
            inner.ListUsageRowsAsync(userId, windowStart, ct);

        public Task<IReadOnlyList<ApiUsageRecentItemDto>> ListRecentAsync(
            Guid userId,
            DateTimeOffset windowStart,
            int limit,
            CancellationToken ct = default) =>
            inner.ListRecentAsync(userId, windowStart, limit, ct);
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
            var databasePath = Path.Combine(Path.GetTempPath(), $"replyinmyvoice-v1-routing-{Guid.NewGuid():N}.db");
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
