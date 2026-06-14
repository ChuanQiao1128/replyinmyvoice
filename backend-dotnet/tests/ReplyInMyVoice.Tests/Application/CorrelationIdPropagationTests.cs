using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Account;
using ReplyInMyVoice.Application.UseCases.Quota;
using ReplyInMyVoice.Application.UseCases.Rewrite;
using ReplyInMyVoice.Application.UseCases.RewriteJob;
using ReplyInMyVoice.Application.UseCases.WebhookOutbox;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Queueing;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests.Application;

public sealed class CorrelationIdPropagationTests
{
    private const string ApiKeyPepper = "correlation-id-test-pepper";
    private const string ValidResultJson = "{\"rewrittenText\":\"Hi Jordan, I can send this today.\",\"changeSummary\":[],\"riskNotes\":[]}";

    [Fact]
    public async Task Outbox_handler_publishes_rewrite_job_with_message_correlation_id()
    {
        var attemptId = Guid.NewGuid();
        const string correlationId = "obs-corr-outbox-817";
        var publisher = new InMemoryRewriteJobPublisher();
        var handler = new RewriteJobCreatedOutboxMessageHandler(publisher);
        var message = new OutboxMessage
        {
            MessageType = "RewriteJobCreated",
            PayloadJson = JsonSerializer.Serialize(new { attemptId }),
            CorrelationId = correlationId,
        };

        await handler.HandleAsync(message);

        var job = publisher.PublishedJobs.Should().ContainSingle().Subject;
        job.AttemptId.Should().Be(attemptId);
        job.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task V1_rewrite_carries_ingress_correlation_id_to_published_job()
    {
        await using var fixture = await DbFixture.CreateAsync();
        const string correlationId = "obs-corr-v1-817";
        var token = await SeedApiKeyUserAsync(fixture, "clerk_corr_v1");
        var publisher = new InMemoryRewriteJobPublisher();
        await using var handlerDb = fixture.CreateContext();
        var function = CreateV1Function(handlerDb, publisher);
        var request = CreateV1RewriteRequest(token, "corr-v1-idem", "Please tell Jordan I can send this today.", correlationId);

        var result = await function.SubmitRewrite(request, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
        var job = publisher.PublishedJobs.Should().ContainSingle().Subject;
        job.CorrelationId.Should().Be(correlationId);
        await using var verifyDb = fixture.CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync();
        outbox.CorrelationId.Should().Be(correlationId);
        outbox.PayloadJson.Should().Contain(job.AttemptId.ToString());
    }

    [Fact]
    public async Task Rewrite_job_function_opens_log_scope_with_correlation_id_and_attempt_id()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-14T00:00:00Z");
        var attemptId = await ReserveAttemptAsync(fixture, user.Id, "corr-function-scope", now);
        const string correlationId = "obs-corr-function-817";
        var logger = new CapturingLogger<RewriteJobFunction>();
        await using var handlerDb = fixture.CreateContext();
        var function = new RewriteJobFunction(
            CreateProcessHandler(
                handlerDb,
                new FakeRewriteEngineClient(new RewriteEngineResult(
                    ValidResultJson,
                    Success: true,
                    ErrorCode: null,
                    [Metric(success: true)])),
                new FakeRewriteCostLogger()),
            logger);
        var messageBody = JsonSerializer.Serialize(new RewriteJob(attemptId, correlationId));

        await function.Run(messageBody, CancellationToken.None);

        var scope = logger.Scopes.Single(s =>
            ScopeValueEquals(s, "CorrelationId", correlationId) &&
            ScopeValueEquals(s, "AttemptId", attemptId));
        scope["CorrelationId"].Should().Be(correlationId);
        scope["AttemptId"].Should().Be(attemptId);
    }

    private static V1RewriteHttpFunctions CreateV1Function(
        AppDbContext db,
        IRewriteJobPublisher publisher)
    {
        var unitOfWork = new UnitOfWork(db);
        var appUsers = new AppUserRepository(db);
        var usagePeriods = new UsagePeriodRepository(db);
        var rewriteAttempts = new RewriteAttemptRepository(db);
        var credits = new RewriteCreditRepository(db);
        var apiKeyUsages = new ApiKeyUsageRepository(db);
        var outboxMessages = new OutboxMessageRepository(db);
        var configuration = new ConfigurationBuilder().Build();
        var outboxHandler = new DispatchDueOutboxHandler(
            outboxMessages,
            [new RewriteJobCreatedOutboxMessageHandler(publisher)],
            new NoOpOutboxDispatchObserver(),
            unitOfWork);
        var fastPath = new OutboxFastPathDispatcher(
            outboxHandler,
            new OutboxFastPathOptions(Enabled: true, TimeSpan.FromSeconds(5)),
            NullLogger<OutboxFastPathDispatcher>.Instance);

        return new V1RewriteHttpFunctions(
            configuration,
            new ApiKeyAuthResolver(new ApiKeyRepository(db), unitOfWork),
            appUsers,
            rewriteAttempts,
            apiKeyUsages,
            unitOfWork,
            new AllowingApiKeyRateLimiter(),
            new HasPaidApiEntitlementHandler(appUsers, credits),
            new CreateRewriteAttemptHandler(
                appUsers,
                usagePeriods,
                rewriteAttempts,
                new UsageReservationRepository(db),
                credits,
                outboxMessages,
                unitOfWork,
                fastPath),
            new GetRewriteAttemptHandler(rewriteAttempts),
            new GetAccountSummaryHandler(
                appUsers,
                usagePeriods,
                credits,
                new PromoCodeRedemptionRepository(db),
                new PromoCodeRepository(db),
                new AccountUsagePlanProvider(configuration),
                unitOfWork));
    }

    private static HttpRequest CreateV1RewriteRequest(
        string token,
        string idempotencyKey,
        string draft,
        string correlationId)
    {
        var context = new DefaultHttpContext();
        context.Items["CorrelationId"] = correlationId;
        var request = context.Request;
        request.Method = HttpMethods.Post;
        request.Path = "/api/v1/rewrite";
        request.ContentType = "application/json";
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { draft })));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token).ToString();
        request.Headers["Idempotency-Key"] = idempotencyKey;
        request.Headers["X-Correlation-Id"] = correlationId;
        return request;
    }

    private static async Task<string> SeedApiKeyUserAsync(
        DbFixture fixture,
        string externalAuthUserId)
    {
        Environment.SetEnvironmentVariable("API_KEY_PEPPER", ApiKeyPepper);
        var now = DateTimeOffset.UtcNow;
        var token = $"rmv_live_{externalAuthUserId}_token";
        await using var db = fixture.CreateContext();
        var user = new AppUser
        {
            ExternalAuthUserId = externalAuthUserId,
            Email = $"{externalAuthUserId}@example.com",
            StripeCustomerId = $"cus_{externalAuthUserId}",
            StripeSubscriptionId = $"sub_{externalAuthUserId}",
            SubscriptionStatus = SubscriptionStatus.Active,
            CurrentPeriodEnd = now.AddDays(30),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.AppUsers.Add(user);
        db.ApiKeys.Add(new ApiKey
        {
            User = user,
            Name = "Correlation propagation key",
            KeyHash = ApiKeyHashing.ComputeHash(token),
            Last4 = token[^4..],
            RateLimitPerMinute = 60,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
        return token;
    }

    private static async Task<Guid> ReserveAttemptAsync(
        DbFixture fixture,
        Guid userId,
        string idempotencyKey,
        DateTimeOffset now)
    {
        await using var reserveDb = fixture.CreateContext();
        var result = await new ReserveQuotaHandler(
            new UsagePeriodRepository(reserveDb),
            new RewriteAttemptRepository(reserveDb),
            new UsageReservationRepository(reserveDb),
            new RewriteCreditRepository(reserveDb),
            new OutboxMessageRepository(reserveDb),
            new UnitOfWork(reserveDb)).HandleAsync(new ReserveQuotaCommand(
                userId,
                idempotencyKey,
                $"hash-{idempotencyKey}",
                JsonSerializer.Serialize(new RewriteRequest(
                    MessageToReplyTo: "Jordan asked for the details.",
                    RoughDraftReply: "Tell Jordan I can send this today.",
                    Audience: "Client",
                    Purpose: "Reply.",
                    WhatHappened: "The update is ready.",
                    FactsToPreserve: "No dates changed.",
                    Tone: "warm")),
                "free:lifetime",
                QuotaLimit: 3,
                now,
                TimeSpan.FromMinutes(10)));

        result.Kind.Should().Be(ReserveQuotaResultKind.Created);
        return result.AttemptId;
    }

    private static ProcessRewriteJobHandler CreateProcessHandler(
        AppDbContext db,
        IRewriteEngineClient engine,
        IRewriteCostLogger costLogger) =>
        new(
            new RewriteAttemptRepository(db),
            new UsageReservationRepository(db),
            new UsagePeriodRepository(db),
            new RewriteCreditRepository(db),
            new UnitOfWork(db),
            engine,
            costLogger);

    private static RewriteEngineCallMetric Metric(
        bool success,
        string? errorCode = null) =>
        new(
            Provider: "openai-compatible",
            Role: "rewrite_model",
            Model: "deepseek-v4-pro",
            InputTokens: 100,
            OutputTokens: 40,
            Characters: null,
            LatencyMs: 12,
            Success: success,
            ErrorCode: errorCode);

    private sealed class AllowingApiKeyRateLimiter : IApiKeyRateLimiter
    {
        public Task<ApiKeyRateLimitResult> CheckAndIncrementAsync(
            Guid apiKeyId,
            int rateLimitPerMinute,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.FromResult(ApiKeyRateLimitResult.Allowed(rateLimitPerMinute, calls: 1, now.AddMinutes(1)));
    }

    private sealed class NoOpOutboxDispatchObserver : IOutboxDispatchObserver
    {
        public Task OnTerminalFailureAsync(
            OutboxMessage message,
            string error,
            CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<IReadOnlyDictionary<string, object?>> Scopes { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            Scopes.Add(CaptureScope(state));
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }

        private static IReadOnlyDictionary<string, object?> CaptureScope<TState>(TState state)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> nullablePairs)
            {
                return nullablePairs.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            }

            if (state is IEnumerable<KeyValuePair<string, object>> objectPairs)
            {
                return objectPairs.ToDictionary(x => x.Key, x => (object?)x.Value, StringComparer.Ordinal);
            }

            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["State"] = state,
            };
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private static bool ScopeValueEquals(
        IReadOnlyDictionary<string, object?> scope,
        string key,
        object expected) =>
        scope.TryGetValue(key, out var actual) && Equals(actual, expected);
}
