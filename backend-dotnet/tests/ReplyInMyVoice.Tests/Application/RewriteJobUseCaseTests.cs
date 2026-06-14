using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Quota;
using ReplyInMyVoice.Application.UseCases.RewriteJob;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Tests.TestDoubles;

namespace ReplyInMyVoice.Tests.Application;

public sealed class RewriteJobUseCaseTests
{
    private const string ValidResultJson = "{\"rewrittenText\":\"Hi Jordan, I can send this today.\",\"changeSummary\":[],\"riskNotes\":[]}";

    [Fact]
    public async Task ProcessRewriteJobAsync_finalizes_quota_and_writes_cost_log_when_engine_returns_rewrite()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.UtcNow;
        var attemptId = await ReserveAttemptAsync(fixture, user.Id, "job-success", now);
        var engine = new FakeRewriteEngineClient(new RewriteEngineResult(
            ValidResultJson,
            Success: true,
            ErrorCode: null,
            [Metric(success: true)]));
        var costLogger = new FakeRewriteCostLogger();

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateHandler(handlerDb, engine, costLogger);

        await handler.HandleAsync(new ProcessRewriteJobCommand(attemptId));

        engine.CallCount.Should().Be(1);
        engine.SeenAttemptId.Should().Be(attemptId);
        engine.SeenRequest!.RoughDraftReply.Should().Be("Tell Jordan I can send this today.");

        await using var verifyDb = fixture.CreateContext();
        var period = await verifyDb.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(1);
        period.ReservedCount.Should().Be(0);

        var reservation = await verifyDb.UsageReservations.SingleAsync();
        reservation.Status.Should().Be(UsageReservationStatus.Finalized);
        reservation.FinalizedAt.Should().NotBeNull();

        var attempt = await verifyDb.RewriteAttempts.SingleAsync();
        attempt.Status.Should().Be(RewriteAttemptStatus.Succeeded);
        attempt.ResultJson.Should().Be(ValidResultJson);
        attempt.ErrorCode.Should().BeNull();

        var costEntry = costLogger.Entries.Should().ContainSingle().Subject;
        costEntry.AttemptId.Should().Be(attemptId);
        costEntry.Status.Should().Be("succeeded");
        costEntry.ErrorCode.Should().BeNull();
        costEntry.Request.RoughDraftReply.Should().Be("Tell Jordan I can send this today.");
        costEntry.ProviderCalls.Should().ContainSingle();
    }

    [Fact]
    public async Task ProcessRewriteJobAsync_releases_quota_when_engine_returns_failure()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.UtcNow;
        var attemptId = await ReserveAttemptAsync(fixture, user.Id, "job-engine-failure", now);
        var engine = new FakeRewriteEngineClient(new RewriteEngineResult(
            ResultJson: null,
            Success: false,
            ErrorCode: "openai_failed",
            [Metric(success: false, errorCode: "openai_failed")]));
        var costLogger = new FakeRewriteCostLogger();

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateHandler(handlerDb, engine, costLogger);

        await handler.HandleAsync(new ProcessRewriteJobCommand(attemptId));

        await using var verifyDb = fixture.CreateContext();
        var period = await verifyDb.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(0);
        period.ReservedCount.Should().Be(0);

        var reservation = await verifyDb.UsageReservations.SingleAsync();
        reservation.Status.Should().Be(UsageReservationStatus.Released);
        reservation.ReleasedAt.Should().NotBeNull();

        var attempt = await verifyDb.RewriteAttempts.SingleAsync();
        attempt.Status.Should().Be(RewriteAttemptStatus.Failed);
        attempt.ErrorCode.Should().Be("openai_failed");

        var costEntry = costLogger.Entries.Should().ContainSingle().Subject;
        costEntry.Status.Should().Be("failed");
        costEntry.ErrorCode.Should().Be("openai_failed");
        costEntry.ProviderCalls.Should().ContainSingle();
    }

    [Fact]
    public async Task ProcessRewriteJobAsync_refunds_credit_when_engine_throws()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.UtcNow;
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.UsagePeriods.Add(new UsagePeriod
            {
                UserId = user.Id,
                PeriodKey = "free:lifetime",
                QuotaLimit = 1,
                UsedCount = 1,
                ReservedCount = 0,
                CreatedAt = now,
                UpdatedAt = now,
            });
            seedDb.RewriteCredits.Add(new RewriteCredit
            {
                UserId = user.Id,
                Source = "PROMO",
                AmountGranted = 1,
                AmountConsumed = 0,
                GrantedAt = now.AddDays(-1),
                ExpiresAt = now.AddDays(30),
            });
            await seedDb.SaveChangesAsync();
        }

        var attemptId = await ReserveAttemptAsync(
            fixture,
            user.Id,
            "job-credit-refund",
            now,
            quotaLimit: 1);
        var engine = new ThrowingRewriteEngineClient(new InvalidOperationException("provider failed"));
        var costLogger = new FakeRewriteCostLogger();

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateHandler(handlerDb, engine, costLogger);

        await handler.HandleAsync(new ProcessRewriteJobCommand(attemptId));

        await using var verifyDb = fixture.CreateContext();
        var period = await verifyDb.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(1);
        period.ReservedCount.Should().Be(0);
        (await verifyDb.RewriteCredits.SingleAsync()).AmountConsumed.Should().Be(0);

        var reservation = await verifyDb.UsageReservations.SingleAsync();
        reservation.Status.Should().Be(UsageReservationStatus.Released);
        reservation.RewriteCreditId.Should().NotBeNull();

        var attempt = await verifyDb.RewriteAttempts.SingleAsync();
        attempt.Status.Should().Be(RewriteAttemptStatus.Failed);
        attempt.ErrorCode.Should().Be("provider_failed");

        var costEntry = costLogger.Entries.Should().ContainSingle().Subject;
        costEntry.Status.Should().Be("failed");
        costEntry.ErrorCode.Should().Be("provider_failed");
    }

    [Fact]
    public async Task ProcessRewriteJobAsync_records_quality_failure_and_quota_release_metrics_when_quality_gate_fails()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.UtcNow;
        var attemptId = await ReserveAttemptAsync(fixture, user.Id, "job-quality-failure", now);
        var engine = new FakeRewriteEngineClient(new RewriteEngineResult(
            ResultJson: null,
            Success: false,
            ErrorCode: "naturalness_gate_failed",
            [Metric(success: false, errorCode: "naturalness_gate_failed")]));
        var metrics = new RecordingBusinessMetrics();

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateHandler(handlerDb, engine, new FakeRewriteCostLogger(), metrics);

        await handler.HandleAsync(new ProcessRewriteJobCommand(attemptId));

        metrics.Records.Should().Contain(record =>
            record.Name == BusinessMetricNames.RewriteQualityFailureTotal &&
            record.Value == 1 &&
            record.DimensionName == BusinessMetricDimensions.ErrorCode &&
            record.DimensionValue == "naturalness_gate_failed");
        metrics.Records.Should().Contain(record =>
            record.Name == BusinessMetricNames.QuotaReleasedTotal &&
            record.Value == 1 &&
            record.DimensionName == BusinessMetricDimensions.ErrorCode &&
            record.DimensionValue == "naturalness_gate_failed");

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.UsageReservations.SingleAsync()).Status.Should().Be(UsageReservationStatus.Released);
        var attempt = await verifyDb.RewriteAttempts.SingleAsync();
        attempt.Status.Should().Be(RewriteAttemptStatus.Failed);
        attempt.ErrorCode.Should().Be("naturalness_gate_failed");
    }

    [Fact]
    public async Task ProcessRewriteJobAsync_records_quota_release_without_quality_metric_when_provider_throws()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.UtcNow;
        var attemptId = await ReserveAttemptAsync(fixture, user.Id, "job-provider-throws", now);
        var engine = new ThrowingRewriteEngineClient(new InvalidOperationException("provider failed"));
        var metrics = new RecordingBusinessMetrics();

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateHandler(handlerDb, engine, new FakeRewriteCostLogger(), metrics);

        await handler.HandleAsync(new ProcessRewriteJobCommand(attemptId));

        metrics.Records.Should().ContainSingle(record =>
            record.Name == BusinessMetricNames.QuotaReleasedTotal &&
            record.Value == 1 &&
            record.DimensionName == BusinessMetricDimensions.ErrorCode &&
            record.DimensionValue == RewriteEngineErrorCodes.ProviderFailed);
        metrics.Records.Should().NotContain(record =>
            record.Name == BusinessMetricNames.RewriteQualityFailureTotal);
    }

    [Fact]
    public async Task ProcessRewriteJobAsync_records_no_failure_metrics_on_success()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.UtcNow;
        var attemptId = await ReserveAttemptAsync(fixture, user.Id, "job-success-no-failure-metrics", now);
        var engine = new FakeRewriteEngineClient(new RewriteEngineResult(
            ValidResultJson,
            Success: true,
            ErrorCode: null,
            [Metric(success: true)]));
        var metrics = new RecordingBusinessMetrics();

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateHandler(handlerDb, engine, new FakeRewriteCostLogger(), metrics);

        await handler.HandleAsync(new ProcessRewriteJobCommand(attemptId));

        metrics.Records.Should().NotContain(record =>
            record.Name == BusinessMetricNames.QuotaReleasedTotal ||
            record.Name == BusinessMetricNames.RewriteQualityFailureTotal);
    }

    private static async Task<Guid> ReserveAttemptAsync(
        DbFixture fixture,
        Guid userId,
        string idempotencyKey,
        DateTimeOffset now,
        int quotaLimit = 3)
    {
        await using var reserveDb = fixture.CreateContext();
        var result = await CreateReserveHandler(reserveDb).HandleAsync(new ReserveQuotaCommand(
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
            quotaLimit,
            now,
            TimeSpan.FromMinutes(10)));

        result.Kind.Should().Be(ReserveQuotaResultKind.Created);
        return result.AttemptId;
    }

    private static ReserveQuotaHandler CreateReserveHandler(AppDbContext db) =>
        new(
            new UsagePeriodRepository(db),
            new RewriteAttemptRepository(db),
            new UsageReservationRepository(db),
            new RewriteCreditRepository(db),
            new OutboxMessageRepository(db),
            new UnitOfWork(db));

    private static ProcessRewriteJobHandler CreateHandler(
        AppDbContext db,
        IRewriteEngineClient engine,
        IRewriteCostLogger costLogger,
        IBusinessMetrics? metrics = null) =>
        new(
            new RewriteAttemptRepository(db),
            new UsageReservationRepository(db),
            new UsagePeriodRepository(db),
            new RewriteCreditRepository(db),
            new UnitOfWork(db),
            engine,
            costLogger,
            metrics);

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
}

internal sealed class FakeRewriteEngineClient(RewriteEngineResult result) : IRewriteEngineClient
{
    public int CallCount { get; private set; }
    public Guid? SeenAttemptId { get; private set; }
    public RewriteRequest? SeenRequest { get; private set; }

    public Task<RewriteEngineResult> RewriteAsync(
        Guid attemptId,
        RewriteRequest request,
        CancellationToken ct = default)
    {
        CallCount += 1;
        SeenAttemptId = attemptId;
        SeenRequest = request;
        return Task.FromResult(result);
    }
}

internal sealed class ThrowingRewriteEngineClient(Exception exception) : IRewriteEngineClient
{
    public Task<RewriteEngineResult> RewriteAsync(
        Guid attemptId,
        RewriteRequest request,
        CancellationToken ct = default) =>
        Task.FromException<RewriteEngineResult>(exception);
}

internal sealed class FakeRewriteCostLogger : IRewriteCostLogger
{
    private readonly List<RewriteCostLogEntry> _entries = [];

    public IReadOnlyList<RewriteCostLogEntry> Entries => _entries;

    public Task WriteAsync(
        RewriteCostLogEntry entry,
        CancellationToken ct = default)
    {
        _entries.Add(entry);
        return Task.CompletedTask;
    }
}
