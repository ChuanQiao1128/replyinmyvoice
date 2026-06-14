using System.Text.Json;
using Azure.Messaging.ServiceBus;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Quota;
using ReplyInMyVoice.Application.UseCases.RewriteJob;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests;

public sealed class RewriteJobFunctionTests
{
    private const string ValidResultJson = "{\"rewrittenText\":\"Hi Jordan, I can send this today.\",\"changeSummary\":[],\"riskNotes\":[]}";

    [Fact]
    public async Task Run_deadletters_when_body_is_not_valid_json()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await using var db = fixture.CreateContext();
        var actions = new RecordingMessageActions();
        var function = CreateFunction(db);

        await function.Run(CreateMessage("{"), actions, CancellationToken.None);

        actions.DeadLetterCallCount.Should().Be(1);
        actions.DeadLetterReason.Should().Be("invalid_json");
        actions.CompleteCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Run_deadletters_when_body_is_null_job()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await using var db = fixture.CreateContext();
        var actions = new RecordingMessageActions();
        var function = CreateFunction(db);

        await function.Run(CreateMessage("null"), actions, CancellationToken.None);

        actions.DeadLetterCallCount.Should().Be(1);
        actions.DeadLetterReason.Should().Be("invalid_job");
        actions.DeadLetterDescription.Should().Be("Message body did not contain a rewrite job.");
        actions.CompleteCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Run_deadletters_when_attempt_id_is_empty()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await using var db = fixture.CreateContext();
        var actions = new RecordingMessageActions();
        var function = CreateFunction(db);
        var body = JsonSerializer.Serialize(new RewriteJob(Guid.Empty));

        await function.Run(CreateMessage(body), actions, CancellationToken.None);

        actions.DeadLetterCallCount.Should().Be(1);
        actions.DeadLetterReason.Should().Be("invalid_job");
        actions.DeadLetterDescription.Should().Be("Message body did not contain a valid attempt id.");
        actions.CompleteCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Run_deadletters_when_attempt_is_missing()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await using var db = fixture.CreateContext();
        var actions = new RecordingMessageActions();
        var function = CreateFunction(db);
        var body = JsonSerializer.Serialize(new RewriteJob(Guid.NewGuid()));

        await function.Run(CreateMessage(body), actions, CancellationToken.None);

        actions.DeadLetterCallCount.Should().Be(1);
        actions.DeadLetterReason.Should().Be("attempt_not_found");
        actions.CompleteCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Run_rethrows_on_transient_handler_failure()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await using var db = fixture.CreateContext();
        var actions = new RecordingMessageActions();
        var function = CreateFunctionWithThrowingAttemptRepository(
            db,
            new TimeoutException("attempt read failed"));
        var attemptId = Guid.NewGuid();
        var body = JsonSerializer.Serialize(new RewriteJob(attemptId));

        Func<Task> act = () => function.Run(CreateMessage(body), actions, CancellationToken.None);

        await act.Should().ThrowAsync<TimeoutException>();
        actions.DeadLetterCallCount.Should().Be(0);
        actions.CompleteCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Run_completes_message_on_success()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var attemptId = await ReserveAttemptAsync(fixture, user.Id, "function-success", DateTimeOffset.UtcNow);
        await using var db = fixture.CreateContext();
        var actions = new RecordingMessageActions();
        var function = CreateFunction(
            db,
            new FakeRewriteEngineClient(new RewriteEngineResult(
                ValidResultJson,
                Success: true,
                ErrorCode: null,
                [Metric(success: true)])));
        var body = JsonSerializer.Serialize(new RewriteJob(attemptId));

        await function.Run(CreateMessage(body), actions, CancellationToken.None);

        actions.CompleteCallCount.Should().Be(1);
        actions.DeadLetterCallCount.Should().Be(0);
    }

    private static ServiceBusReceivedMessage CreateMessage(string body) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(body));

    private static RewriteJobFunction CreateFunction(
        AppDbContext db,
        IRewriteEngineClient? engine = null,
        IRewriteCostLogger? costLogger = null) =>
        new(
            CreateProcessHandler(
                db,
                engine ?? new FakeRewriteEngineClient(new RewriteEngineResult(
                    ValidResultJson,
                    Success: true,
                    ErrorCode: null,
                    [Metric(success: true)])),
                costLogger ?? new FakeRewriteCostLogger()),
            NullLogger<RewriteJobFunction>.Instance);

    private static RewriteJobFunction CreateFunctionWithThrowingAttemptRepository(
        AppDbContext db,
        Exception exception) =>
        new(
            new ProcessRewriteJobHandler(
                new ThrowingRewriteAttemptRepository(exception),
                new UsageReservationRepository(db),
                new UsagePeriodRepository(db),
                new RewriteCreditRepository(db),
                new UnitOfWork(db),
                new FakeRewriteEngineClient(new RewriteEngineResult(
                    ValidResultJson,
                    Success: true,
                    ErrorCode: null,
                    [Metric(success: true)])),
                new FakeRewriteCostLogger()),
            NullLogger<RewriteJobFunction>.Instance);

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
            new UnitOfWork(reserveDb),
            NullLogger<ReserveQuotaHandler>.Instance).HandleAsync(new ReserveQuotaCommand(
                userId,
                idempotencyKey,
                $"hash-{idempotencyKey}",
                """
                {
                  "messageToReplyTo": "Jordan asked for the details.",
                  "roughDraftReply": "Tell Jordan I can send this today.",
                  "audience": "Client",
                  "purpose": "Reply.",
                  "factsToPreserve": "Preserve today.",
                  "tone": "warm"
                }
                """,
                "free:lifetime",
                QuotaLimit: 3,
                now,
                TimeSpan.FromMinutes(10)));

        result.Kind.Should().Be(ReserveQuotaResultKind.Created);
        return result.AttemptId;
    }

    private static RewriteEngineCallMetric Metric(bool success) =>
        new(
            Provider: "openai-compatible",
            Role: "rewrite_model",
            Model: "deepseek-v4-pro",
            InputTokens: 100,
            OutputTokens: 40,
            Characters: null,
            LatencyMs: 12,
            Success: success,
            ErrorCode: null);

    private sealed class RecordingMessageActions : ServiceBusMessageActions
    {
        public int DeadLetterCallCount { get; private set; }
        public int CompleteCallCount { get; private set; }
        public string? DeadLetterReason { get; private set; }
        public string? DeadLetterDescription { get; private set; }

        public override Task DeadLetterMessageAsync(
            ServiceBusReceivedMessage message,
            Dictionary<string, object>? propertiesToModify = null,
            string? deadLetterReason = null,
            string? deadLetterErrorDescription = null,
            CancellationToken cancellationToken = default)
        {
            DeadLetterCallCount += 1;
            DeadLetterReason = deadLetterReason;
            DeadLetterDescription = deadLetterErrorDescription;
            return Task.CompletedTask;
        }

        public override Task CompleteMessageAsync(
            ServiceBusReceivedMessage message,
            CancellationToken cancellationToken = default)
        {
            CompleteCallCount += 1;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRewriteEngineClient(RewriteEngineResult result) : IRewriteEngineClient
    {
        public Task<RewriteEngineResult> RewriteAsync(
            Guid attemptId,
            RewriteRequest request,
            CancellationToken ct = default) =>
            Task.FromResult(result);
    }

    private sealed class FakeRewriteCostLogger : IRewriteCostLogger
    {
        public Task WriteAsync(RewriteCostLogEntry entry, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class ThrowingRewriteAttemptRepository(Exception exception) : IRewriteAttemptRepository
    {
        public Task AddAsync(RewriteAttempt attempt, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<RewriteAttempt?> GetByIdAsync(Guid attemptId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<RewriteAttempt?> GetByIdNoTrackingAsync(Guid attemptId, CancellationToken ct = default) =>
            Task.FromException<RewriteAttempt?>(exception);

        public Task<RewriteAttempt?> GetByIdForUserAsync(
            Guid attemptId,
            Guid userId,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<RewriteAttempt?> GetByUserIdAndIdempotencyKeyAsync(
            Guid userId,
            string idempotencyKey,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RewriteAttempt>> ListByUserIdAsync(
            Guid userId,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
