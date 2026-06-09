using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using ReplyInMyVoice.Application.UseCases.Quota;
using ReplyInMyVoice.Application.UseCases.RewriteJob;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Providers;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class RewriteCostTrackingTests
{
    private const decimal InputRatePer1K = 0.0100m;
    private const decimal OutputRatePer1K = 0.0200m;

    [Fact]
    public async Task RewriteWritesCostLog()
    {
        ConfigureRates();
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var attemptId = await ReserveAttemptAsync(fixture.CreateContext, user.Id, "idem-cost-log");
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateProcessHandler(
            handlerDb,
            fixture.CreateContext,
            CreateOpenAiBackedProvider(promptTokens: 150, completionTokens: 50));

        await handler.HandleAsync(new ProcessRewriteJobCommand(attemptId), CancellationToken.None);

        await using var db = fixture.CreateContext();
        var log = await db.RewriteCostLogs.Include(x => x.ProviderCalls).SingleAsync();
        var providerCall = log.ProviderCalls.Single();

        log.UserId.Should().Be(user.Id);
        log.RequestId.Should().Be(attemptId.ToString());
        log.OpenAiInputTokens.Should().Be(150);
        log.OpenAiOutputTokens.Should().Be(50);
        log.OpenAiCostUsd.Should().BeGreaterThan(0);
        log.TotalEstimatedCostUsd.Should().Be(log.OpenAiCostUsd);
        providerCall.Model.Should().Be("deepseek-v4-pro");
        providerCall.InputTokens.Should().Be(150);
        providerCall.OutputTokens.Should().Be(50);
        providerCall.EstimatedCostUsd.Should().Be(log.OpenAiCostUsd);
        providerCall.LatencyMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task CostComputedFromRates()
    {
        ConfigureRates();
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var attemptId = await ReserveAttemptAsync(fixture.CreateContext, user.Id, "idem-cost-rates");
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateProcessHandler(
            handlerDb,
            fixture.CreateContext,
            CreateOpenAiBackedProvider(promptTokens: 250, completionTokens: 125));

        await handler.HandleAsync(new ProcessRewriteJobCommand(attemptId), CancellationToken.None);

        await using var db = fixture.CreateContext();
        var log = await db.RewriteCostLogs.Include(x => x.ProviderCalls).SingleAsync();
        var providerCall = log.ProviderCalls.Single();
        var expectedCost = (250m / 1000m * InputRatePer1K) + (125m / 1000m * OutputRatePer1K);

        log.OpenAiCostUsd.Should().Be(expectedCost);
        log.TotalEstimatedCostUsd.Should().Be(expectedCost);
        providerCall.EstimatedCostUsd.Should().Be(expectedCost);
    }

    [Fact]
    public async Task ReprocessingSucceededAttemptDoesNotThrowOrDuplicateCostLogWhenRequestIdRaceOccurs()
    {
        ConfigureRates();
        await using var fixture = await CostLogRaceFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var attemptId = await ReserveAttemptAsync(fixture.CreateContext, user.Id, "idem-cost-race");
        var provider = CreateOpenAiBackedProvider(promptTokens: 150, completionTokens: 50);

        var act = async () =>
        {
            await using (var firstDb = fixture.CreateContext())
            {
                await CreateProcessHandler(firstDb, fixture.CreateContext, provider)
                    .HandleAsync(new ProcessRewriteJobCommand(attemptId), CancellationToken.None);
            }

            await using (var secondDb = fixture.CreateContext())
            {
                await CreateProcessHandler(secondDb, fixture.CreateContext, provider)
                    .HandleAsync(new ProcessRewriteJobCommand(attemptId), CancellationToken.None);
            }
        };

        await act.Should().NotThrowAsync();

        await using var db = fixture.CreateContext();
        var requestId = attemptId.ToString();
        var logCount = await db.RewriteCostLogs.CountAsync(x => x.RequestId == requestId);
        logCount.Should().Be(1);
        (await db.RewriteCostLogs.CountAsync()).Should().Be(1);
    }

    private static void ConfigureRates()
    {
        Environment.SetEnvironmentVariable("REWRITE_COST_INPUT_PER_1K", InputRatePer1K.ToString("0.0000"));
        Environment.SetEnvironmentVariable("REWRITE_COST_OUTPUT_PER_1K", OutputRatePer1K.ToString("0.0000"));
    }

    private static async Task<Guid> ReserveAttemptAsync(
        Func<AppDbContext> dbContextFactory,
        Guid userId,
        string idempotencyKey)
    {
        await using var db = dbContextFactory();
        var result = await CreateReserveHandler(db).HandleAsync(new ReserveQuotaCommand(
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
            3,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(10)));
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

    private static ProcessRewriteJobHandler CreateProcessHandler(
        AppDbContext db,
        Func<AppDbContext> dbContextFactory,
        IRewriteProvider provider) =>
        new(
            new RewriteAttemptRepository(db),
            new UsageReservationRepository(db),
            new UsagePeriodRepository(db),
            new RewriteCreditRepository(db),
            new UnitOfWork(db),
            new RewriteProviderEngineClient(provider),
            new RewriteCostLogger(dbContextFactory));

    private static IRewriteProvider CreateOpenAiBackedProvider(int promptTokens, int completionTokens)
    {
        var httpClient = new HttpClient(new RecordingHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                    {
                      "choices": [
                        {
                          "message": {
                            "content": "{\"rewrittenText\":\"Hi Jordan, I can send this today.\"}"
                          }
                        }
                      ],
                      "usage": {
                        "prompt_tokens": {{promptTokens}},
                        "completion_tokens": {{completionTokens}},
                        "total_tokens": {{promptTokens + completionTokens}}
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"),
            })));
        var client = new OpenAiCompatibleRewriteModelClient(
            httpClient,
            "test-key",
            "deepseek-v4-pro",
            "https://api.deepseek.com",
            TimeSpan.FromSeconds(5));
        return new OpenAiBackedRewriteProvider(client);
    }

    private sealed class OpenAiBackedRewriteProvider(IRewriteModelClient modelClient) : IRewriteProvider
    {
        public async Task<RewriteProviderResult> RewriteAsync(
            Guid attemptId,
            RewriteRequest request,
            CancellationToken cancellationToken)
        {
            var modelRequest = new RewriteModelRequest(
                attemptId,
                request,
                ReplyInMyVoice.Domain.RewriteEngine.RewriteInputAnalyzer.Analyze(request),
                ReplyInMyVoice.Domain.RewriteEngine.FactLedgerExtractor.Extract(request),
                ReplyInMyVoice.Domain.RewriteEngine.RewriteStrategy.FactsFirstReconstruct,
                []);
            var result = await modelClient.GenerateCandidateAsync(modelRequest, cancellationToken);
            return result.Success
                ? new RewriteProviderResult(
                    $$"""{"rewrittenText":{{System.Text.Json.JsonSerializer.Serialize(result.CandidateText)}},"changeSummary":[],"riskNotes":[]}""",
                    true,
                    null)
                : new RewriteProviderResult(null, false, result.ErrorCode);
        }
    }

    private sealed class CostLogRaceFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DuplicateCostLogSaveInterceptor _duplicateCostLogSaveInterceptor = new();

        private CostLogRaceFixture(SqliteConnection connection)
        {
            _connection = connection;
        }

        public static async Task<CostLogRaceFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var fixture = new CostLogRaceFixture(connection);
            await using var db = fixture.CreateContext();
            await db.Database.EnsureCreatedAsync();
            return fixture;
        }

        public AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(_duplicateCostLogSaveInterceptor)
                .EnableSensitiveDataLogging()
                .Options;
            return new AppDbContext(options);
        }

        public async Task<AppUser> CreateUserAsync()
        {
            await using var db = CreateContext();
            var user = new AppUser
            {
                ExternalAuthUserId = $"clerk_{Guid.NewGuid():N}",
                Email = "test@example.com",
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.AppUsers.Add(user);
            await db.SaveChangesAsync();
            return user;
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }
    }

    private sealed class DuplicateCostLogSaveInterceptor : SaveChangesInterceptor
    {
        private bool _insertedDuplicate;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            var costLog = context?.ChangeTracker
                .Entries<RewriteCostLog>()
                .Where(x => x.State == EntityState.Added)
                .Select(x => x.Entity)
                .SingleOrDefault();

            if (!_insertedDuplicate && context is not null && costLog is not null)
            {
                _insertedDuplicate = true;
                await InsertDuplicateCostLogAsync(context, costLog.RequestId, cancellationToken);
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static async Task InsertDuplicateCostLogAsync(
            DbContext context,
            string requestId,
            CancellationToken cancellationToken)
        {
            var command = context.Database.GetDbConnection().CreateCommand();
            await using (command.ConfigureAwait(false))
            {
                command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
                command.CommandText =
                    """
                    INSERT INTO RewriteCostLogs (
                        Id,
                        RequestId,
                        StrategyVersion,
                        Scenario,
                        TonePreset,
                        Status,
                        StartedAt,
                        FinishedAt,
                        DurationMs,
                        InputCharCount,
                        DraftWordCount,
                        InternalStrategies,
                        RepairCandidates,
                        RejectedCandidates,
                        UsedEscalation,
                        OpenAiInputTokens,
                        OpenAiOutputTokens,
                        OpenAiCostUsd,
                        SaplingCallCount,
                        SaplingCharacters,
                        SaplingCostUsd,
                        TotalEstimatedCostUsd,
                        ModelsUsedJson,
                        ProviderCallsJson,
                        CreatedAt,
                        UpdatedAt,
                        RowVersion
                    )
                    VALUES (
                        $id,
                        $requestId,
                        'race',
                        'race',
                        'warm',
                        'succeeded',
                        $now,
                        $now,
                        1,
                        1,
                        1,
                        0,
                        0,
                        0,
                        0,
                        1,
                        1,
                        0.000001,
                        0,
                        0,
                        0,
                        0.000001,
                        '[]',
                        '[]',
                        $now,
                        $now,
                        $rowVersion
                    );
                    """;

                AddParameter(command, "$id", Guid.NewGuid().ToString());
                AddParameter(command, "$requestId", requestId);
                AddParameter(command, "$now", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                AddParameter(command, "$rowVersion", Guid.NewGuid().ToString());

                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        private static void AddParameter(DbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
    }
}
