using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class ApiKeyUsageAnomalyServiceTests
{
    [Fact]
    public async Task Flags_key_when_current_window_exceeds_baseline_multiplier()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-08T12:00:00Z");
        var logger = new CapturingLogger<ApiKeyUsageAnomalyService>();

        await using (var db = fixture.CreateContext())
        {
            var key = AddKey(db, user.Id, "spike key", "1111", now);
            AddUsageRows(db, key.Id, "base", 4, now.AddMinutes(-95), TimeSpan.FromMinutes(5));
            AddUsageRows(db, key.Id, "current", 13, now.AddMinutes(-55), TimeSpan.FromMinutes(3));
            await db.SaveChangesAsync();

            var service = CreateService(fixture, logger);
            var result = await service.EvaluateAsync(key.Id, now, CancellationToken.None);

            result.IsFlagged.Should().BeTrue();
            result.ObservedCount.Should().Be(13);
            result.ExpectedCount.Should().Be(4);
            result.Reason.Should().Be("baseline_multiplier");
        }

        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.State.Should().ContainKey("EventName")
            .WhoseValue.Should().Be(ApiKeyUsageAnomalyService.AlertEventName);
        entry.State.Should().ContainKey("ObservedCount")
            .WhoseValue.Should().Be(13);
    }

    [Fact]
    public async Task Does_not_flag_key_when_usage_is_steady()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-08T12:00:00Z");

        await using (var db = fixture.CreateContext())
        {
            var key = AddKey(db, user.Id, "steady key", "2222", now);
            AddUsageRows(db, key.Id, "base", 10, now.AddMinutes(-110), TimeSpan.FromMinutes(5));
            AddUsageRows(db, key.Id, "current", 12, now.AddMinutes(-55), TimeSpan.FromMinutes(4));
            await db.SaveChangesAsync();

            var service = CreateService(fixture);
            var result = await service.EvaluateAsync(key.Id, now, CancellationToken.None);

            result.IsFlagged.Should().BeFalse();
            result.ObservedCount.Should().Be(12);
            result.ExpectedCount.Should().Be(10);
            result.Reason.Should().Be("normal");
        }
    }

    [Fact]
    public async Task Does_not_flag_key_with_zero_usage()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-08T12:00:00Z");

        await using (var db = fixture.CreateContext())
        {
            var key = AddKey(db, user.Id, "idle key", "3333", now);
            await db.SaveChangesAsync();

            var service = CreateService(fixture);
            var result = await service.EvaluateAsync(key.Id, now, CancellationToken.None);

            result.IsFlagged.Should().BeFalse();
            result.ObservedCount.Should().Be(0);
            result.ExpectedCount.Should().Be(0);
            result.Reason.Should().Be("no_usage");
        }
    }

    private static ApiKeyUsageAnomalyService CreateService(
        DbFixture fixture,
        ILogger<ApiKeyUsageAnomalyService>? logger = null) =>
        new(
            fixture.CreateContext,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ApiKeyUsageAnomaly:WindowMinutes"] = "60",
                    ["ApiKeyUsageAnomaly:SpikeMultiplier"] = "3",
                    ["ApiKeyUsageAnomaly:AbsoluteCeiling"] = "100",
                })
                .Build(),
            logger);

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

    private static void AddUsageRows(
        AppDbContext db,
        Guid apiKeyId,
        string requestPrefix,
        int count,
        DateTimeOffset firstCreatedAt,
        TimeSpan step)
    {
        for (var index = 0; index < count; index++)
        {
            db.ApiKeyUsages.Add(new ApiKeyUsage
            {
                ApiKeyId = apiKeyId,
                RequestId = $"{requestPrefix}-{index}",
                Endpoint = "/api/v1/rewrite",
                StatusCode = 200,
                LatencyMs = 100,
                CreatedAt = firstCreatedAt.Add(step * index),
            });
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

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
            var values = state as IEnumerable<KeyValuePair<string, object?>>;
            var stateValues = values?.ToDictionary(x => x.Key, x => x.Value) ??
                new Dictionary<string, object?>();
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), stateValues));
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        IReadOnlyDictionary<string, object?> State);
}
