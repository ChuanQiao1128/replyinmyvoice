using FluentAssertions;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.ApiKey;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests.Application;

[Collection("ApiKeyPepper")]
public sealed class ApiKeyUseCaseTests
{
    private const string TestPepper = "api-key-use-case-test-pepper";

    [Fact]
    public async Task ApiKey_handlers_generate_list_rotate_revoke_and_update_webhooks()
    {
        typeof(ApiKey).GetProperty("Scope").Should().BeNull();

        Environment.SetEnvironmentVariable("API_KEY_PEPPER", TestPepper);
        await using var fixture = await DbFixture.CreateAsync();
        var owner = await fixture.CreateUserAsync();
        var other = await fixture.CreateUserAsync();
        var now = DateTimeOffset.UtcNow;
        string generatedPlaintext;

        await using (var handlerDb = fixture.CreateContext())
        {
            var generated = await CreateGenerateHandler(handlerDb).HandleAsync(
                new GenerateApiKeyCommand(owner.Id, "Primary integration key", IsTest: true));

            generated.Plaintext.Should().StartWith("rmv_test_");
            generated.Id.Should().NotBeEmpty();
            generatedPlaintext = generated.Plaintext;

            await handlerDb.SaveChangesAsync();
        }

        Guid keyId;
        string plaintext;
        await using (var verifyDb = fixture.CreateContext())
        {
            var stored = await verifyDb.ApiKeys.SingleAsync();
            keyId = stored.Id;
            plaintext = generatedPlaintext;
            stored.UserId.Should().Be(owner.Id);
            stored.Name.Should().Be("Primary integration key");
            stored.KeyHash.Should().NotBe(plaintext);
            stored.Last4.Should().NotBeNull();
            stored.IsTest.Should().BeTrue();
        }

        await using (var seedUsageDb = fixture.CreateContext())
        {
            seedUsageDb.ApiKeyUsages.AddRange(
                Usage(keyId, "req-owned-ok", 200, now.AddDays(-1)),
                Usage(keyId, "req-owned-accepted", 202, now.AddDays(-2)),
                Usage(keyId, "req-owned-failed", 500, now.AddDays(-3)),
                Usage(keyId, "req-owned-old", 200, now.AddDays(-31)));
            await seedUsageDb.SaveChangesAsync();
        }

        await using (var listDb = fixture.CreateContext())
        {
            var summaries = await CreateListHandler(listDb).HandleAsync(new ListApiKeysQuery(owner.Id));

            var summary = summaries.Should().ContainSingle().Subject;
            summary.Id.Should().Be(keyId);
            summary.Name.Should().Be("Primary integration key");
            summary.MaskedKey.Should().StartWith("rmv_test_");
            summary.Last30dUsage.Should().Be(new ApiUsageCountDto(3, 2, 1));
        }

        await using (var webhookDb = fixture.CreateContext())
        {
            var setWebhook = await CreateSetWebhookHandler(webhookDb).HandleAsync(
                new SetApiKeyWebhookCommand(
                    owner.Id,
                    keyId,
                    "https://93.184.216.34/rewrite"));

            setWebhook.Should().NotBeNull();
            setWebhook!.WebhookUrl.Should().Be("https://93.184.216.34/rewrite");
            setWebhook.WebhookSecret.Should().MatchRegex("^[0-9a-f]{64}$");
        }

        Guid webhookRowVersion;
        await using (var afterWebhookDb = fixture.CreateContext())
        {
            var stored = await afterWebhookDb.ApiKeys.SingleAsync(x => x.Id == keyId);
            stored.WebhookUrl.Should().Be("https://93.184.216.34/rewrite");
            stored.WebhookSecret.Should().NotBeNullOrWhiteSpace();
            webhookRowVersion = stored.RowVersion;
        }

        await using (var clearDb = fixture.CreateContext())
        {
            var cleared = await CreateClearWebhookHandler(clearDb).HandleAsync(
                new ClearApiKeyWebhookCommand(owner.Id, keyId));

            cleared.Should().BeTrue();
        }

        await using (var afterClearDb = fixture.CreateContext())
        {
            var stored = await afterClearDb.ApiKeys.SingleAsync(x => x.Id == keyId);
            stored.WebhookUrl.Should().BeNull();
            stored.WebhookSecret.Should().BeNull();
            stored.RowVersion.Should().NotBe(webhookRowVersion);
        }

        await using (var copyFieldsDb = fixture.CreateContext())
        {
            var stored = await copyFieldsDb.ApiKeys.SingleAsync(x => x.Id == keyId);
            stored.PlanTier = "business";
            stored.RateLimitPerMinute = 123;
            stored.MonthlyQuota = 4567;
            stored.WebhookUrl = "https://93.184.216.34/rotated";
            stored.WebhookSecret = "rotation-webhook-secret";
            await copyFieldsDb.SaveChangesAsync();
        }

        await using (var rotateDb = fixture.CreateContext())
        {
            var nonOwnerRotate = await CreateRotateHandler(rotateDb).HandleAsync(
                new RotateApiKeyCommand(other.Id, keyId));
            nonOwnerRotate.Should().BeNull();
        }

        ApiKeyRotationResultDto rotated;
        await using (var rotateDb = fixture.CreateContext())
        {
            rotated = (await CreateRotateHandler(rotateDb).HandleAsync(
                new RotateApiKeyCommand(owner.Id, keyId)))!;

            rotated.Id.Should().NotBe(keyId);
            rotated.Name.Should().Be("Primary integration key");
            rotated.Plaintext.Should().StartWith("rmv_test_");
            rotated.IsTest.Should().BeTrue();
        }

        await using (var afterRotateDb = fixture.CreateContext())
        {
            var oldKey = await afterRotateDb.ApiKeys.SingleAsync(x => x.Id == keyId);
            var newKey = await afterRotateDb.ApiKeys.SingleAsync(x => x.Id == rotated.Id);
            oldKey.RevokedAt.Should().NotBeNull();
            newKey.RevokedAt.Should().BeNull();
            newKey.KeyHash.Should().Be(ApiKeyCredential.ComputeHash(rotated.Plaintext));
            newKey.Last4.Should().Be(rotated.Plaintext[^4..]);
            newKey.PlanTier.Should().Be("business");
            newKey.RateLimitPerMinute.Should().Be(123);
            newKey.MonthlyQuota.Should().Be(4567);
            newKey.WebhookUrl.Should().Be("https://93.184.216.34/rotated");
            newKey.WebhookSecret.Should().Be("rotation-webhook-secret");
            newKey.IsTest.Should().BeTrue();
        }

        await using (var revokeDb = fixture.CreateContext())
        {
            var missingRevoke = await CreateRevokeHandler(revokeDb).HandleAsync(
                new RevokeApiKeyCommand(owner.Id, Guid.NewGuid()));
            var revoked = await CreateRevokeHandler(revokeDb).HandleAsync(
                new RevokeApiKeyCommand(owner.Id, rotated.Id));

            missingRevoke.Should().BeFalse();
            revoked.Should().BeTrue();
        }

        await using (var afterRevokeDb = fixture.CreateContext())
        {
            var stored = await afterRevokeDb.ApiKeys.SingleAsync(x => x.Id == rotated.Id);
            stored.RevokedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Api_usage_handlers_scope_bucket_and_clamp_usage_windows()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var commands = new CommandCaptureInterceptor();

        AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(commands)
                .Options;
            return new AppDbContext(options);
        }

        var now = DateTimeOffset.Parse("2026-06-05T12:00:00+12:00");
        var userA = NewUser("clerk_usage_a", "usage-a@example.com", now);
        var userB = NewUser("clerk_usage_b", "usage-b@example.com", now);

        await using (var db = CreateContext())
        {
            await db.Database.EnsureCreatedAsync();
            db.AppUsers.AddRange(userA, userB);
            var keyA1 = AddKey(db, userA.Id, "A primary", "1111", now);
            var keyA2 = AddKey(db, userA.Id, "A secondary", "2222", now);
            var keyB = AddKey(db, userB.Id, "B primary", "9999", now);

            db.ApiKeyUsages.AddRange(
                Usage(keyA1.Id, "req-a-1", "/api/v1/rewrite", 200, 120, DateTimeOffset.Parse("2026-06-04T11:59:00Z")),
                Usage(keyA1.Id, "req-a-2", "/api/v1/rewrite", 500, 400, DateTimeOffset.Parse("2026-06-04T12:01:00Z")),
                Usage(keyA2.Id, "req-a-3", "/api/v1/rewrite", 202, 180, DateTimeOffset.Parse("2026-06-03T12:30:00Z")),
                Usage(keyA1.Id, "req-a-4", "/api/v1/usage", 401, null, DateTimeOffset.Parse("2026-05-20T00:00:00Z")),
                Usage(keyA1.Id, "req-a-old", "/api/v1/rewrite", 200, 90, now.AddDays(-100)),
                Usage(keyB.Id, "req-b-1", "/api/v1/rewrite-b", 200, 90, DateTimeOffset.Parse("2026-06-04T22:00:00Z")));
            db.UsagePeriods.Add(new UsagePeriod
            {
                UserId = userA.Id,
                PeriodKey = "free:lifetime",
                UsedCount = 1,
                ReservedCount = 0,
                CreatedAt = now,
                UpdatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        await using var handlerDb = CreateContext();
        var summary = await CreateUsageSummaryHandler(handlerDb).HandleAsync(
            new GetApiUsageSummaryQuery(userA.ExternalAuthUserId, userA.Email, now));
        var series = await CreateUsageSeriesHandler(handlerDb).HandleAsync(
            new GetApiUsageSeriesQuery(userA.Id, now, Days: 999));
        var recent = await CreateUsageRecentHandler(handlerDb).HandleAsync(
            new GetApiUsageRecentQuery(userA.Id, now, Limit: 2));

        summary.Should().NotBeNull();
        summary!.Today.Should().Be(new ApiUsageCountDto(1, 0, 1));
        summary.Yesterday.Should().Be(new ApiUsageCountDto(2, 2, 0));
        summary.MonthToDate.Should().Be(new ApiUsageCountDto(3, 2, 1));
        summary.Last30dCalls.Should().Be(4);
        summary.Quota.Should().Be(3);
        summary.Used.Should().Be(1);
        summary.Remaining.Should().Be(2);
        summary.PeriodEnd.Should().BeNull();

        series.Should().HaveCount(90);
        series.Sum(x => x.Calls).Should().Be(4);
        series.Should().Contain(x => x.Date == "2026-06-04" && x.Calls == 2 && x.Succeeded == 2 && x.Failed == 0);
        series.Should().Contain(x => x.Date == "2026-06-05" && x.Calls == 1 && x.Succeeded == 0 && x.Failed == 1);

        recent.Should().HaveCount(2);
        recent.Select(x => x.KeyLast4).Should().OnlyContain(last4 => last4 == "1111" || last4 == "2222");
        recent.Select(x => x.Endpoint).Should().NotContain("/api/v1/rewrite-b");
        recent.Select(x => x.CreatedAt).Should().BeInDescendingOrder();
        commands.CommandTexts.Should().Contain(commandText => HasApiUsageCreatedAtLowerBound(commandText));
    }

    private static GenerateApiKeyHandler CreateGenerateHandler(AppDbContext db) =>
        new(new ApiKeyRepository(db), new UnitOfWork(db));

    private static ListApiKeysHandler CreateListHandler(AppDbContext db) =>
        new(new ApiKeyRepository(db), new ApiKeyUsageRepository(db));

    private static RotateApiKeyHandler CreateRotateHandler(AppDbContext db) =>
        new(new ApiKeyRepository(db), new UnitOfWork(db));

    private static RevokeApiKeyHandler CreateRevokeHandler(AppDbContext db) =>
        new(new ApiKeyRepository(db), new UnitOfWork(db));

    private static SetApiKeyWebhookHandler CreateSetWebhookHandler(AppDbContext db) =>
        new(new ApiKeyRepository(db), new UnitOfWork(db));

    private static ClearApiKeyWebhookHandler CreateClearWebhookHandler(AppDbContext db) =>
        new(new ApiKeyRepository(db), new UnitOfWork(db));

    private static GetApiUsageSummaryHandler CreateUsageSummaryHandler(AppDbContext db) =>
        new(
            new AppUserRepository(db),
            new UsagePeriodRepository(db),
            new RewriteCreditRepository(db),
            new ApiKeyUsageRepository(db),
            new AccountUsagePlanProvider(TestConfiguration()));

    private static GetApiUsageSeriesHandler CreateUsageSeriesHandler(AppDbContext db) =>
        new(new ApiKeyUsageRepository(db));

    private static GetApiUsageRecentHandler CreateUsageRecentHandler(AppDbContext db) =>
        new(new ApiKeyUsageRepository(db));

    private static IConfiguration TestConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FREE_BASELINE_REWRITES"] = "3",
            })
            .Build();

    private static AppUser NewUser(
        string externalAuthUserId,
        string email,
        DateTimeOffset now) =>
        new()
        {
            ExternalAuthUserId = externalAuthUserId,
            Email = email,
            CreatedAt = now,
            UpdatedAt = now,
        };

    private static ApiKey AddKey(
        AppDbContext db,
        Guid userId,
        string name,
        string last4,
        DateTimeOffset now)
    {
        var key = new ApiKey
        {
            UserId = userId,
            Name = name,
            KeyHash = $"hash-{Guid.NewGuid():N}",
            Last4 = last4,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.ApiKeys.Add(key);
        return key;
    }

    private static ApiKeyUsage Usage(
        Guid apiKeyId,
        string requestId,
        int statusCode,
        DateTimeOffset createdAt) =>
        Usage(apiKeyId, requestId, "v1/rewrite", statusCode, null, createdAt);

    private static ApiKeyUsage Usage(
        Guid apiKeyId,
        string requestId,
        string endpoint,
        int statusCode,
        int? latencyMs,
        DateTimeOffset createdAt) =>
        new()
        {
            ApiKeyId = apiKeyId,
            RequestId = requestId,
            Endpoint = endpoint,
            StatusCode = statusCode,
            LatencyMs = latencyMs,
            CreatedAt = createdAt,
        };

    private static bool HasApiUsageCreatedAtLowerBound(string commandText) =>
        commandText.Contains("ApiKeyUsages", StringComparison.Ordinal) &&
        commandText.Contains("CreatedAt", StringComparison.Ordinal) &&
        commandText.Contains(">=", StringComparison.Ordinal);

    private sealed class CommandCaptureInterceptor : DbCommandInterceptor
    {
        private readonly List<string> _commandTexts = new();

        public IReadOnlyList<string> CommandTexts => _commandTexts;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            _commandTexts.Add(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            _commandTexts.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
