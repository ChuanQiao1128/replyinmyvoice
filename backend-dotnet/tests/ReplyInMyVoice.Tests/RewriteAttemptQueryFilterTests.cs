using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests;

public sealed class RewriteAttemptQueryFilterTests
{
    [Fact]
    public async Task DefaultQueryExcludesSoftDeletedAttempts()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var seed = await SeedAttemptsAsync(fixture);

        await using var db = fixture.CreateContext();
        var attempts = await db.RewriteAttempts.ToListAsync();

        attempts.Should().ContainSingle();
        attempts[0].Id.Should().Be(seed.Live.Id);
    }

    [Fact]
    public async Task IgnoreQueryFiltersIncludesSoftDeletedAttempts()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var seed = await SeedAttemptsAsync(fixture);

        await using var db = fixture.CreateContext();
        var attempts = await db.RewriteAttempts
            .IgnoreQueryFilters()
            .ToListAsync();

        attempts.Select(x => x.Id).Should().BeEquivalentTo(new[] { seed.Live.Id, seed.Deleted.Id });
    }

    [Fact]
    public async Task SystemLookupsSeeSoftDeletedAttempts()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var seed = await SeedAttemptsAsync(fixture);

        await using var db = fixture.CreateContext();
        var repository = new RewriteAttemptRepository(db);

        (await repository.GetByIdAsync(seed.Deleted.Id)).Should().NotBeNull();
        (await repository.GetByIdNoTrackingAsync(seed.Deleted.Id)).Should().NotBeNull();
        (await repository.GetByUserIdAndIdempotencyKeyAsync(seed.User.Id, seed.Deleted.IdempotencyKey))
            .Should()
            .NotBeNull();
        (await repository.ListByUserIdAsync(seed.User.Id))
            .Select(x => x.Id)
            .Should()
            .Contain(seed.Deleted.Id);
    }

    [Fact]
    public async Task UserScopedLookupHidesSoftDeletedAttempts()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var seed = await SeedAttemptsAsync(fixture);

        await using var db = fixture.CreateContext();
        var repository = new RewriteAttemptRepository(db);

        (await repository.GetByIdForUserAsync(seed.Deleted.Id, seed.User.Id)).Should().BeNull();
    }

    private static async Task<SeededAttempts> SeedAttemptsAsync(DbFixture fixture)
    {
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var live = Attempt(user.Id, "query-filter-live", now, deletedAt: null);
        var deleted = Attempt(user.Id, "query-filter-deleted", now.AddMinutes(-1), now);

        await using var db = fixture.CreateContext();
        db.RewriteAttempts.AddRange(live, deleted);
        await db.SaveChangesAsync();
        return new SeededAttempts(user, live, deleted);
    }

    private static RewriteAttempt Attempt(
        Guid userId,
        string idempotencyKey,
        DateTimeOffset createdAt,
        DateTimeOffset? deletedAt) =>
        new()
        {
            UserId = userId,
            IdempotencyKey = idempotencyKey,
            RequestHash = $"{idempotencyKey}-hash",
            RequestJson = "{\"roughDraftReply\":\"Thanks for your message.\"}",
            ResultJson = "{\"rewrittenText\":\"Thanks for the message.\"}",
            Status = RewriteAttemptStatus.Succeeded,
            CreatedAt = createdAt,
            CompletedAt = createdAt.AddMinutes(1),
            DeletedAt = deletedAt,
            ExpiresAt = createdAt.AddMinutes(15),
        };

    private sealed record SeededAttempts(
        AppUser User,
        RewriteAttempt Live,
        RewriteAttempt Deleted);
}
