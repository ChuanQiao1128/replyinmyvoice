using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Tests.SqlServer;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "SqlServer")]
public sealed class SqlServerIdempotencyAndWebhookReplayTests(SqlServerDbFixture fixture)
{
    [Fact]
    public async Task Duplicate_rewrite_attempt_idempotency_key_is_rejected_by_unique_index()
    {
        var now = DateTimeOffset.Parse("2026-06-13T12:00:00Z");
        var user = await fixture.CreateUserAsync();
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.RewriteAttempts.Add(CreateRewriteAttempt(user.Id, "sqlserver-idem-duplicate", "hash-one", now));
            await seedDb.SaveChangesAsync();
        }

        await using var duplicateDb = fixture.CreateContext();
        duplicateDb.RewriteAttempts.Add(CreateRewriteAttempt(user.Id, "sqlserver-idem-duplicate", "hash-two", now));

        var act = () => duplicateDb.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.RewriteAttempts.CountAsync(x =>
            x.UserId == user.Id &&
            x.IdempotencyKey == "sqlserver-idem-duplicate")).Should().Be(1);
    }

    [Fact]
    public async Task Duplicate_stripe_event_id_is_rejected_by_primary_key()
    {
        var now = DateTimeOffset.Parse("2026-06-13T12:00:00Z");
        var eventId = $"evt_sqlserver_duplicate_{Guid.NewGuid():N}";
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.StripeEvents.Add(CreateStripeEvent(eventId, now));
            await seedDb.SaveChangesAsync();
        }

        await using var duplicateDb = fixture.CreateContext();
        duplicateDb.StripeEvents.Add(CreateStripeEvent(eventId, now));

        var act = () => duplicateDb.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.StripeEvents.CountAsync(x => x.EventId == eventId)).Should().Be(1);
    }

    private static RewriteAttempt CreateRewriteAttempt(
        Guid userId,
        string idempotencyKey,
        string requestHash,
        DateTimeOffset now) =>
        new()
        {
            UserId = userId,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            RequestJson = "{\"roughDraftReply\":\"Thanks for the note.\"}",
            Status = RewriteAttemptStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(10),
        };

    private static StripeEvent CreateStripeEvent(string eventId, DateTimeOffset now) =>
        new()
        {
            EventId = eventId,
            Type = "customer.subscription.updated",
            Status = StripeEventStatus.Pending,
            CreatedAt = now,
            PayloadJson = "{\"id\":\"evt_sqlserver_duplicate\"}",
        };
}
