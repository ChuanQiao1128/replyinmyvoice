using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.UseCases.Admin;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests.Application;

public sealed class ListDeadLettersHandlerTests
{
    [Fact]
    public async Task ListDeadLettersHandler_paginates_filters_sorts_and_audits()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-19T01:00:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            for (var index = 0; index < 12; index += 1)
            {
                seedDb.DeadLetterMessages.Add(CreateDeadLetter(
                    sourceType: index % 2 == 0 ? "OutboxMessage" : "StripeEvent",
                    sourceId: $"source-{index:D2}",
                    createdAt: now.AddMinutes(index),
                    attemptCount: index + 1));
            }

            await seedDb.SaveChangesAsync();
        }

        await using var db = fixture.CreateContext();
        var handler = CreateListHandler(db);

        var page = await handler.HandleAsync(
            new ListDeadLettersQuery(
                "admin-owner-oid",
                "owner@example.com",
                Page: 1,
                PageSize: 3,
                SourceType: "OutboxMessage",
                Now: now.AddHours(1)),
            CancellationToken.None);

        page.Page.Should().Be(1);
        page.PageSize.Should().Be(3);
        page.TotalCount.Should().Be(6);
        page.TotalPages.Should().Be(2);
        page.DeadLetters.Should().HaveCount(3);
        page.DeadLetters.Select(x => x.SourceType).Should().OnlyContain(x => x == "OutboxMessage");
        page.DeadLetters.Select(x => x.CreatedAt)
            .Should()
            .BeInDescendingOrder();
        page.DeadLetters[0].SourceId.Should().Be("source-10");
        page.DeadLetters[0].AttemptCount.Should().Be(11);
        page.DeadLetters[0].LastError.Should().Be("failure 11");

        var audit = await db.AdminAuditLogs.SingleAsync();
        audit.AdminExternalAuthUserId.Should().Be("admin-owner-oid");
        audit.AdminEmail.Should().Be("owner@example.com");
        audit.Action.Should().Be("list_dead_letters");
        audit.TargetUserId.Should().BeNull();
        using var details = JsonDocument.Parse(audit.DetailsJson!);
        details.RootElement.GetProperty("sourceType").GetString().Should().Be("OutboxMessage");
        details.RootElement.GetProperty("sourceId").ValueKind.Should().Be(JsonValueKind.Null);
        details.RootElement.GetProperty("attemptCount").ValueKind.Should().Be(JsonValueKind.Null);
        details.RootElement.GetProperty("lastError").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetDeadLetterDetailHandler_returns_full_record_and_audits()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-19T02:00:00Z");
        Guid deadLetterId;
        await using (var seedDb = fixture.CreateContext())
        {
            var deadLetter = CreateDeadLetter("StripeEvent", "evt_dead_letter_detail", now, attemptCount: 4);
            seedDb.DeadLetterMessages.Add(deadLetter);
            await seedDb.SaveChangesAsync();
            deadLetterId = deadLetter.Id;
        }

        await using var db = fixture.CreateContext();
        var handler = CreateDetailHandler(db);

        var detail = await handler.HandleAsync(
            new GetDeadLetterDetailQuery(
                "admin-owner-oid",
                "owner@example.com",
                deadLetterId,
                now.AddHours(1)),
            CancellationToken.None);

        detail.Should().NotBeNull();
        detail!.Id.Should().Be(deadLetterId);
        detail.SourceType.Should().Be("StripeEvent");
        detail.SourceId.Should().Be("evt_dead_letter_detail");
        detail.SourceData.Should().Contain("evt_dead_letter_detail");
        detail.FailureReason.Should().Be("failure 4");
        detail.RequeuedAt.Should().BeNull();
        detail.IsRequeued.Should().BeFalse();
        detail.AttemptCount.Should().Be(4);
        detail.LastError.Should().Be("failure 4");

        var audit = await db.AdminAuditLogs.SingleAsync();
        audit.Action.Should().Be("get_dead_letter");
        audit.TargetUserId.Should().BeNull();
        using var details = JsonDocument.Parse(audit.DetailsJson!);
        details.RootElement.GetProperty("sourceType").GetString().Should().Be("StripeEvent");
        details.RootElement.GetProperty("sourceId").GetString().Should().Be("evt_dead_letter_detail");
        details.RootElement.GetProperty("attemptCount").GetInt32().Should().Be(4);
        details.RootElement.GetProperty("lastError").GetString().Should().Be("failure 4");
    }

    [Fact]
    public async Task GetDeadLetterDetailHandler_returns_null_without_audit_when_missing()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await using var db = fixture.CreateContext();
        var handler = CreateDetailHandler(db);

        var detail = await handler.HandleAsync(
            new GetDeadLetterDetailQuery(
                "admin-owner-oid",
                "owner@example.com",
                Guid.NewGuid(),
                DateTimeOffset.Parse("2026-06-19T02:30:00Z")),
            CancellationToken.None);

        detail.Should().BeNull();
        (await db.AdminAuditLogs.CountAsync()).Should().Be(0);
    }

    private static ListDeadLettersHandler CreateListHandler(ReplyInMyVoice.Infrastructure.Data.AppDbContext db) =>
        new(
            new DeadLetterMessageRepository(db),
            new AdminUserRepository(db),
            new UnitOfWork(db));

    private static GetDeadLetterDetailHandler CreateDetailHandler(ReplyInMyVoice.Infrastructure.Data.AppDbContext db) =>
        new(
            new DeadLetterMessageRepository(db),
            new AdminUserRepository(db),
            new UnitOfWork(db));

    private static DeadLetterMessage CreateDeadLetter(
        string sourceType,
        string sourceId,
        DateTimeOffset createdAt,
        int attemptCount) =>
        new()
        {
            SourceType = sourceType,
            SourceId = sourceId,
            SourceData = $$"""{"sourceType":"{{sourceType}}","sourceId":"{{sourceId}}","attemptCount":{{attemptCount}},"lastError":"failure {{attemptCount}}"}""",
            FailureReason = $"failure {attemptCount}",
            CreatedAt = createdAt,
        };
}
