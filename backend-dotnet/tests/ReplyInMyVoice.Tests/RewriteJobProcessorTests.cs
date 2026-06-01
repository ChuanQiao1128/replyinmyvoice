using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Providers;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class RewriteJobProcessorTests
{
    [Fact]
    public async Task ProcessAsync_throws_permanent_signal_when_attempt_is_missing()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var provider = new FakeRewriteProvider(new RewriteProviderResult("{\"rewrittenText\":\"Should not run\",\"changeSummary\":[],\"riskNotes\":[]}", true, null));
        var processor = new RewriteJobProcessor(fixture.CreateContext, provider);
        var missingAttemptId = Guid.NewGuid();

        var act = () => processor.ProcessAsync(new RewriteJob(missingAttemptId), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<RewriteAttemptNotFoundException>();
        exception.Which.AttemptId.Should().Be(missingAttemptId);
        provider.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessAsync_finalizes_reservation_when_provider_returns_rewrite()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var quota = new QuotaService(fixture.CreateContext);
        var reservedAttempt = await quota.ReserveAsync(
            user.Id,
            "idem-worker-ok",
            "hash",
            "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            "free:lifetime",
            3,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(10));
        var provider = new FakeRewriteProvider(new RewriteProviderResult("{\"rewrittenText\":\"Hi there\",\"changeSummary\":[],\"riskNotes\":[]}", true, null));
        var processor = new RewriteJobProcessor(fixture.CreateContext, provider);

        await processor.ProcessAsync(new RewriteJob(reservedAttempt.AttemptId), CancellationToken.None);

        provider.SeenRequest!.RoughDraftReply.Should().Be("Thanks for your message. I will reply soon.");
        await using var db = fixture.CreateContext();
        var period = await db.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(1);
        period.ReservedCount.Should().Be(0);
        (await db.RewriteAttempts.SingleAsync()).Status.Should().Be(RewriteAttemptStatus.Succeeded);
    }

    [Fact]
    public async Task ProcessAsync_releases_reservation_when_provider_fails()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var quota = new QuotaService(fixture.CreateContext);
        var reservedAttempt = await quota.ReserveAsync(
            user.Id,
            "idem-worker-fail",
            "hash",
            "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            "free:lifetime",
            3,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(10));
        var processor = new RewriteJobProcessor(
            fixture.CreateContext,
            new FakeRewriteProvider(new RewriteProviderResult(null, false, "openai_failed")));

        await processor.ProcessAsync(new RewriteJob(reservedAttempt.AttemptId), CancellationToken.None);

        await using var db = fixture.CreateContext();
        var period = await db.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(0);
        period.ReservedCount.Should().Be(0);
        var attempt = await db.RewriteAttempts.SingleAsync();
        attempt.Status.Should().Be(RewriteAttemptStatus.Failed);
        attempt.ErrorCode.Should().Be("openai_failed");
    }

    [Fact]
    public async Task ProcessAsync_does_not_call_provider_again_when_queue_redelivers_succeeded_attempt()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var quota = new QuotaService(fixture.CreateContext);
        var reservedAttempt = await quota.ReserveAsync(
            user.Id,
            "idem-worker-redelivery",
            "hash",
            "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            "free:lifetime",
            3,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(10));
        await quota.FinalizeSuccessAsync(
            reservedAttempt.AttemptId,
            "{\"rewrittenText\":\"Already done\",\"changeSummary\":[],\"riskNotes\":[]}",
            DateTimeOffset.UtcNow);
        var provider = new FakeRewriteProvider(new RewriteProviderResult("{\"rewrittenText\":\"Should not run\",\"changeSummary\":[],\"riskNotes\":[]}", true, null));
        var processor = new RewriteJobProcessor(fixture.CreateContext, provider);

        await processor.ProcessAsync(new RewriteJob(reservedAttempt.AttemptId), CancellationToken.None);

        provider.CallCount.Should().Be(0);
        await using var db = fixture.CreateContext();
        var period = await db.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(1);
        period.ReservedCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessAsync_releases_reservation_when_provider_returns_malformed_json()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var quota = new QuotaService(fixture.CreateContext);
        var reservedAttempt = await quota.ReserveAsync(
            user.Id,
            "idem-worker-bad-json",
            "hash",
            "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            "free:lifetime",
            3,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(10));
        var processor = new RewriteJobProcessor(
            fixture.CreateContext,
            new FakeRewriteProvider(new RewriteProviderResult("not json", true, null)));

        await processor.ProcessAsync(new RewriteJob(reservedAttempt.AttemptId), CancellationToken.None);

        await using var db = fixture.CreateContext();
        var period = await db.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(0);
        period.ReservedCount.Should().Be(0);
        var attempt = await db.RewriteAttempts.SingleAsync();
        attempt.Status.Should().Be(RewriteAttemptStatus.Failed);
        attempt.ErrorCode.Should().Be("provider_json_parse_failed");
    }

    [Theory]
    [InlineData("operation", false)]
    [InlineData("task", true)]
    public async Task ProcessAsync_releases_reservation_when_provider_times_out(
        string exceptionKind,
        bool creditBacked)
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.UtcNow;
        if (creditBacked)
        {
            await using var seedDb = fixture.CreateContext();
            seedDb.UsagePeriods.Add(new ReplyInMyVoice.Domain.Entities.UsagePeriod
            {
                UserId = user.Id,
                PeriodKey = "free:lifetime",
                QuotaLimit = 1,
                UsedCount = 1,
                ReservedCount = 0,
                CreatedAt = now,
                UpdatedAt = now,
            });
            seedDb.RewriteCredits.Add(new ReplyInMyVoice.Domain.Entities.RewriteCredit
            {
                UserId = user.Id,
                Source = "PURCHASE",
                AmountGranted = 1,
                AmountConsumed = 0,
                GrantedAt = now.AddDays(-1),
                ExpiresAt = now.AddDays(30),
            });
            await seedDb.SaveChangesAsync();
        }

        var quota = new QuotaService(fixture.CreateContext);
        var reservedAttempt = await quota.ReserveAsync(
            user.Id,
            $"idem-worker-timeout-{exceptionKind}",
            "hash",
            "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            "free:lifetime",
            creditBacked ? 1 : 3,
            now,
            TimeSpan.FromMinutes(10));
        var provider = new ThrowingRewriteProvider(
            exceptionKind == "task"
                ? new TaskCanceledException("provider call timed out")
                : new OperationCanceledException("provider call timed out"));
        var processor = new RewriteJobProcessor(fixture.CreateContext, provider);

        await processor.ProcessAsync(new RewriteJob(reservedAttempt.AttemptId), CancellationToken.None);

        provider.CallCount.Should().Be(1);
        await using var db = fixture.CreateContext();
        var period = await db.UsagePeriods.SingleAsync();
        if (creditBacked)
        {
            period.UsedCount.Should().Be(1);
            period.ReservedCount.Should().Be(0);
            (await db.RewriteCredits.SingleAsync()).AmountConsumed.Should().Be(0);
        }
        else
        {
            period.UsedCount.Should().Be(0);
            period.ReservedCount.Should().Be(0);
        }

        (await db.UsageReservations.SingleAsync()).Status.Should().Be(UsageReservationStatus.Released);
        var attempt = await db.RewriteAttempts.SingleAsync();
        attempt.Status.Should().Be(RewriteAttemptStatus.Failed);
        attempt.ErrorCode.Should().Be("provider_timeout");
    }
}

internal sealed class FakeRewriteProvider(RewriteProviderResult result) : IRewriteProvider
{
    public int CallCount { get; private set; }
    public RewriteRequest? SeenRequest { get; private set; }

    public Task<RewriteProviderResult> RewriteAsync(Guid attemptId, RewriteRequest request, CancellationToken cancellationToken)
    {
        CallCount += 1;
        SeenRequest = request;
        return Task.FromResult(result);
    }
}

internal sealed class ThrowingRewriteProvider(Exception exception) : IRewriteProvider
{
    public int CallCount { get; private set; }

    public Task<RewriteProviderResult> RewriteAsync(Guid attemptId, RewriteRequest request, CancellationToken cancellationToken)
    {
        CallCount += 1;
        return Task.FromException<RewriteProviderResult>(exception);
    }
}
