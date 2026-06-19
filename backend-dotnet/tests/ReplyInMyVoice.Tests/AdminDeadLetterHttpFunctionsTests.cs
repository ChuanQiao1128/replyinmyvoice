using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Functions;

namespace ReplyInMyVoice.Tests;

public sealed class AdminDeadLetterHttpFunctionsTests
{
    [Fact]
    public async Task ListDeadLetters_requires_admin()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var function = CreateFunction(fixture);

        var result = await function.ListDeadLetters(
            CreateRequest("regular-user-oid", "regular@example.com"),
            CancellationToken.None);

        var forbidden = result.Should().BeOfType<ObjectResult>().Subject;
        forbidden.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task RequeueDeadLetter_requires_admin()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var function = CreateFunction(fixture);

        var result = await function.RequeueDeadLetter(
            CreateRequest("regular-user-oid", "regular@example.com"),
            Guid.NewGuid().ToString("D"),
            CancellationToken.None);

        var forbidden = result.Should().BeOfType<ObjectResult>().Subject;
        forbidden.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task ListDeadLetters_returns_paginated_response_and_audits()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-19T06:00:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.DeadLetterMessages.Add(new DeadLetterMessage
            {
                SourceType = "OutboxMessage",
                SourceId = Guid.NewGuid().ToString("D"),
                SourceData = """{"sourceType":"OutboxMessage","sourceId":"outbox-http","attemptCount":10,"lastError":"handler failed"}""",
                FailureReason = "handler failed",
                CreatedAt = now,
            });
            await seedDb.SaveChangesAsync();
        }

        var function = CreateFunction(fixture);
        var request = CreateRequest("admin-owner-oid", "owner@example.com");
        request.QueryString = new QueryString("?sourceType=OutboxMessage&page=1&pageSize=25");

        var result = await function.ListDeadLetters(request, CancellationToken.None);

        var response = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<AdminDeadLettersListDto>().Subject;
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(25);
        response.TotalCount.Should().Be(1);
        response.DeadLetters.Should().ContainSingle(x => x.SourceType == "OutboxMessage");

        await using var verifyDb = fixture.CreateContext();
        var audit = await verifyDb.AdminAuditLogs.SingleAsync();
        audit.Action.Should().Be("list_dead_letters");
    }

    [Fact]
    public async Task GetDeadLetterDetail_returns_not_found_for_missing_record()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var function = CreateFunction(fixture);

        var result = await function.GetDeadLetterDetail(
            CreateRequest("admin-owner-oid", "owner@example.com"),
            Guid.NewGuid().ToString("D"),
            CancellationToken.None);

        var notFound = result.Should().BeOfType<ObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task RequeueDeadLetter_maps_second_requeue_to_conflict()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-19T07:00:00Z");
        Guid deadLetterId;
        await using (var seedDb = fixture.CreateContext())
        {
            var outbox = new OutboxMessage
            {
                MessageType = "RewriteJobCreated",
                PayloadJson = """{"attemptId":"http-requeue"}""",
                Status = OutboxMessageStatus.Failed,
                CreatedAt = now.AddMinutes(-30),
                NextAttemptAt = now.AddMinutes(-20),
                AttemptCount = 10,
                MaxAttempts = 10,
                LastError = "handler failed",
            };
            seedDb.OutboxMessages.Add(outbox);
            var deadLetter = new DeadLetterMessage
            {
                SourceType = "OutboxMessage",
                SourceId = outbox.Id.ToString("D"),
                SourceData = $$"""{"sourceType":"OutboxMessage","sourceId":"{{outbox.Id:D}}","attemptCount":10,"lastError":"handler failed"}""",
                FailureReason = "handler failed",
                CreatedAt = now.AddMinutes(-10),
            };
            seedDb.DeadLetterMessages.Add(deadLetter);
            await seedDb.SaveChangesAsync();
            deadLetterId = deadLetter.Id;
        }

        var function = CreateFunction(fixture);
        var first = await function.RequeueDeadLetter(
            CreateRequest("admin-owner-oid", "owner@example.com"),
            deadLetterId.ToString("D"),
            CancellationToken.None);
        first.Should().BeOfType<OkObjectResult>();

        var second = await function.RequeueDeadLetter(
            CreateRequest("admin-owner-oid", "owner@example.com"),
            deadLetterId.ToString("D"),
            CancellationToken.None);

        var conflict = second.Should().BeOfType<ObjectResult>().Subject;
        conflict.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.AdminAuditLogs.CountAsync(x => x.Action == "requeue_dead_letter")).Should().Be(1);
    }

    private static AdminHttpFunctions CreateFunction(DbFixture fixture) =>
        AdminHttpFunctionsTestFactory.Create(BuildConfiguration(), fixture.CreateContext);

    private static HttpRequest CreateRequest(string oid, string email)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("oid", oid),
                new Claim("email", email),
            ], "Bearer")),
        };

        return context.Request;
    }

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ADMIN_EMAILS"] = "admin-owner-oid, owner@example.com",
            })
            .Build();
}
