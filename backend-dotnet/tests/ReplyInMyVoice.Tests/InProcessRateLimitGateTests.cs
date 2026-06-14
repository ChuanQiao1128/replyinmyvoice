using FluentAssertions;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class InProcessRateLimitGateTests
{
    [Fact]
    public void Gate_sheds_only_after_limit_arrivals_within_same_minute()
    {
        var gate = new InProcessRateLimitGate();
        var key = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-06-13T02:10:15Z");

        gate.ShouldShed(key, 3, now, out var firstReset).Should().BeFalse();
        gate.ShouldShed(key, 3, now.AddSeconds(1), out _).Should().BeFalse();
        gate.ShouldShed(key, 3, now.AddSeconds(2), out _).Should().BeFalse();
        gate.ShouldShed(key, 3, now.AddSeconds(3), out var shedReset).Should().BeTrue();
        gate.ShouldShed(key, 3, now.AddSeconds(4), out _).Should().BeTrue();

        firstReset.Should().Be(DateTimeOffset.Parse("2026-06-13T02:11:00Z"));
        shedReset.Should().Be(firstReset);
    }

    [Fact]
    public void Gate_resets_counter_in_new_minute_window()
    {
        var gate = new InProcessRateLimitGate();
        var key = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-06-13T02:10:55Z");

        gate.ShouldShed(key, 1, now, out _).Should().BeFalse();
        gate.ShouldShed(key, 1, now.AddSeconds(1), out _).Should().BeTrue();
        gate.ShouldShed(key, 1, now.AddSeconds(5), out var resetAt).Should().BeFalse();

        resetAt.Should().Be(DateTimeOffset.Parse("2026-06-13T02:12:00Z"));
    }

    [Fact]
    public void Gate_tracks_keys_independently()
    {
        var gate = new InProcessRateLimitGate();
        var keyA = Guid.NewGuid();
        var keyB = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-06-13T02:10:15Z");

        gate.ShouldShed(keyA, 1, now, out _).Should().BeFalse();
        gate.ShouldShed(keyA, 1, now.AddSeconds(1), out _).Should().BeTrue();

        gate.ShouldShed(keyB, 1, now.AddSeconds(2), out _).Should().BeFalse();
    }

    [Fact]
    public async Task Precheck_sheds_flood_without_calling_db_limiter()
    {
        var now = DateTimeOffset.Parse("2026-06-13T02:10:15Z");
        var inner = new CountingApiKeyRateLimiter((apiKeyId, limit, requestTime) =>
            ApiKeyRateLimitResult.Allowed(limit, 1, requestTime.AddMinutes(1)));
        var limiter = new PreCheckedApiKeyRateLimiter(inner, new InProcessRateLimitGate());
        var key = Guid.NewGuid();

        var results = new List<ApiKeyRateLimitResult>();
        for (var index = 0; index < 5; index += 1)
        {
            results.Add(await limiter.CheckAndIncrementAsync(key, 2, now.AddSeconds(index), CancellationToken.None));
        }

        inner.CallCount.Should().Be(2);
        results.Take(2).Should().OnlyContain(x => !x.IsLimited);
        results.Skip(2).Should().OnlyContain(x => x.IsLimited && x.Remaining == 0);
    }

    [Fact]
    public async Task Precheck_delegates_when_limit_is_non_positive()
    {
        var now = DateTimeOffset.Parse("2026-06-13T02:10:15Z");
        var inner = new CountingApiKeyRateLimiter((apiKeyId, limit, requestTime) =>
            ApiKeyRateLimitResult.Limited(0, 0, requestTime.AddMinutes(1)));
        var limiter = new PreCheckedApiKeyRateLimiter(inner, new InProcessRateLimitGate());
        var key = Guid.NewGuid();

        var result = await limiter.CheckAndIncrementAsync(key, 0, now, CancellationToken.None);

        inner.CallCount.Should().Be(1);
        result.IsLimited.Should().BeTrue();
        result.Limit.Should().Be(0);
    }

    [Fact]
    public async Task Precheck_does_not_mask_db_authoritative_limit()
    {
        var now = DateTimeOffset.Parse("2026-06-13T02:10:15Z");
        var inner = new CountingApiKeyRateLimiter((apiKeyId, limit, requestTime) =>
            ApiKeyRateLimitResult.Limited(limit, limit, requestTime.AddMinutes(1)));
        var limiter = new PreCheckedApiKeyRateLimiter(inner, new InProcessRateLimitGate());

        var result = await limiter.CheckAndIncrementAsync(Guid.NewGuid(), 5, now, CancellationToken.None);

        inner.CallCount.Should().Be(1);
        result.IsLimited.Should().BeTrue();
        result.Remaining.Should().Be(0);
    }

    private sealed class CountingApiKeyRateLimiter(
        Func<Guid, int, DateTimeOffset, ApiKeyRateLimitResult> handler) : IApiKeyRateLimiter
    {
        public int CallCount { get; private set; }

        public Task<ApiKeyRateLimitResult> CheckAndIncrementAsync(
            Guid apiKeyId,
            int rateLimitPerMinute,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            CallCount += 1;
            return Task.FromResult(handler(apiKeyId, rateLimitPerMinute, now));
        }
    }
}
