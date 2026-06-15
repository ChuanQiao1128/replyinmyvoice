using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Tests;

public sealed class RowVersionStampingTests
{
    [Fact]
    public async Task SaveChangesAsync_stamps_modified_entity_with_fresh_row_version()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await CreateUserAsync(fixture);

        await using (var db = fixture.CreateContext())
        {
            var saved = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
            var originalRowVersion = saved.RowVersion;

            saved.Should().BeAssignableTo<IConcurrencyStamped>();
            saved.Email = "updated@example.com";
            await db.SaveChangesAsync();

            saved.RowVersion.Should().NotBe(originalRowVersion);
        }
    }

    [Fact]
    public async Task SaveChangesAsync_does_not_stamp_unmodified_entity()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await CreateUserAsync(fixture);

        await using (var db = fixture.CreateContext())
        {
            var saved = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
            var originalRowVersion = saved.RowVersion;

            await db.SaveChangesAsync();

            saved.RowVersion.Should().Be(originalRowVersion);
        }
    }

    [Fact]
    public async Task SaveChanges_stamps_modified_entity_with_fresh_row_version()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await CreateUserAsync(fixture);

        await using (var db = fixture.CreateContext())
        {
            var saved = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
            var originalRowVersion = saved.RowVersion;

            saved.Should().BeAssignableTo<IConcurrencyStamped>();
            saved.Email = "sync-updated@example.com";
            db.SaveChanges();

            saved.RowVersion.Should().NotBe(originalRowVersion);
        }
    }

    private static async Task<AppUser> CreateUserAsync(DbFixture fixture)
    {
        var now = DateTimeOffset.Parse("2026-06-14T00:00:00Z");
        await using var db = fixture.CreateContext();
        var user = new AppUser
        {
            ExternalAuthUserId = $"rowversion-{Guid.NewGuid():N}",
            Email = "rowversion@example.com",
            SubscriptionStatus = SubscriptionStatus.Inactive,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
