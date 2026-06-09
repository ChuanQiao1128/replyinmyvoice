using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.UseCases.Account;
using ReplyInMyVoice.Application.UseCases.Rewrite;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class RewriteHistoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task RewriteHistoryListsCallerOnly()
    {
        var caller = await SeedUserAsync("history-list-caller");
        var other = await SeedUserAsync("history-list-other");
        var oldest = await SeedAttemptAsync(caller.Id, createdAt: DateTimeOffset.Parse("2026-05-01T00:00:00Z"));
        var middle = await SeedAttemptAsync(caller.Id, createdAt: DateTimeOffset.Parse("2026-05-02T00:00:00Z"));
        var newest = await SeedAttemptAsync(caller.Id, createdAt: DateTimeOffset.Parse("2026-05-03T00:00:00Z"));
        await SeedAttemptAsync(other.Id, createdAt: DateTimeOffset.Parse("2026-05-04T00:00:00Z"));
        await using var db = CreateContext();
        var functions = CreateFunctions(db);

        var firstPage = await functions.ListMyRewriteAttempts(
            CreateRequest(caller.ExternalAuthUserId, "?page=1&pageSize=2"),
            CancellationToken.None);

        var firstBody = AssertOk<RewriteHistoryPageResponse>(firstPage);
        firstBody.Page.Should().Be(1);
        firstBody.PageSize.Should().Be(2);
        firstBody.TotalCount.Should().Be(3);
        firstBody.Items.Select(x => x.AttemptId).Should().Equal(newest.Id, middle.Id);

        var secondPage = await functions.ListMyRewriteAttempts(
            CreateRequest(caller.ExternalAuthUserId, "?page=2&pageSize=2"),
            CancellationToken.None);

        var secondBody = AssertOk<RewriteHistoryPageResponse>(secondPage);
        secondBody.Items.Select(x => x.AttemptId).Should().Equal(oldest.Id);
    }

    [Fact]
    public async Task RewriteHistoryCrossUserDenied()
    {
        var owner = await SeedUserAsync("history-owner");
        var other = await SeedUserAsync("history-other");
        var ownerAttempt = await SeedAttemptAsync(owner.Id);
        await using var db = CreateContext();
        var functions = CreateFunctions(db);

        var otherLookup = await functions.GetMyRewriteAttempt(
            CreateRequest(other.ExternalAuthUserId),
            ownerAttempt.Id,
            CancellationToken.None);

        otherLookup.Should().BeOfType<NotFoundResult>();

        var ownerLookup = await functions.GetMyRewriteAttempt(
            CreateRequest(owner.ExternalAuthUserId),
            ownerAttempt.Id,
            CancellationToken.None);
        var ownerBody = AssertOk<RewriteHistoryDetailResponse>(ownerLookup);
        ownerBody.AttemptId.Should().Be(ownerAttempt.Id);
    }

    [Fact]
    public async Task RewriteHistorySoftDelete()
    {
        var caller = await SeedUserAsync("history-delete-caller");
        var deleted = await SeedAttemptAsync(caller.Id, createdAt: DateTimeOffset.Parse("2026-05-02T00:00:00Z"));
        var retained = await SeedAttemptAsync(caller.Id, createdAt: DateTimeOffset.Parse("2026-05-01T00:00:00Z"));
        await using var db = CreateContext();
        var functions = CreateFunctions(db);

        var deleteResult = await functions.DeleteMyRewriteAttempt(
            CreateRequest(caller.ExternalAuthUserId),
            deleted.Id,
            CancellationToken.None);

        deleteResult.Should().BeOfType<NoContentResult>();
        var listResult = await functions.ListMyRewriteAttempts(
            CreateRequest(caller.ExternalAuthUserId),
            CancellationToken.None);
        var listBody = AssertOk<RewriteHistoryPageResponse>(listResult);
        listBody.Items.Select(x => x.AttemptId).Should().Equal(retained.Id);

        await using var verifyDb = CreateContext();
        var deletedAttempt = await verifyDb.RewriteAttempts.SingleAsync(x => x.Id == deleted.Id);
        deletedAttempt.DeletedAt.Should().NotBeNull();
    }

    private RewriteHttpFunctions CreateFunctions(AppDbContext db)
    {
        var appUserRepository = new AppUserRepository(db);
        var unitOfWork = new UnitOfWork(db);
        var rewriteAttemptRepository = new RewriteAttemptRepository(db);
        var createRewriteAttemptHandler = new CreateRewriteAttemptHandler(
            appUserRepository,
            new UsagePeriodRepository(db),
            rewriteAttemptRepository,
            new UsageReservationRepository(db),
            new RewriteCreditRepository(db),
            new OutboxMessageRepository(db),
            unitOfWork);
        var getOrCreateUserHandler = new GetOrCreateUserHandler(appUserRepository, unitOfWork);
        var findUserHandler = new FindUserHandler(appUserRepository);
        var getRewriteAttemptHandler = new GetRewriteAttemptHandler(rewriteAttemptRepository);

        return new RewriteHttpFunctions(
            BuildConfiguration(),
            db,
            getOrCreateUserHandler,
            findUserHandler,
            createRewriteAttemptHandler,
            getRewriteAttemptHandler);
    }

    private async Task<AppUser> SeedUserAsync(string externalAuthUserId)
    {
        await using var db = CreateContext();
        var user = new AppUser
        {
            ExternalAuthUserId = externalAuthUserId,
            Email = $"{externalAuthUserId}@example.com",
            SubscriptionStatus = SubscriptionStatus.Inactive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<RewriteAttempt> SeedAttemptAsync(
        Guid userId,
        DateTimeOffset? createdAt = null,
        RewriteAttemptStatus status = RewriteAttemptStatus.Succeeded)
    {
        await using var db = CreateContext();
        var attempt = new RewriteAttempt
        {
            UserId = userId,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            RequestHash = Guid.NewGuid().ToString("N"),
            RequestJson = "{\"roughDraftReply\":\"Please send the update.\"}",
            ResultJson = "{\"rewrittenText\":\"Please send the update.\"}",
            Status = status,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            CompletedAt = createdAt ?? DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
        };
        db.RewriteAttempts.Add(attempt);
        await db.SaveChangesAsync();
        return attempt;
    }

    private static HttpRequest CreateRequest(string externalAuthUserId, string? queryString = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-External-User-Id"] = externalAuthUserId;
        if (!string.IsNullOrWhiteSpace(queryString))
        {
            context.Request.QueryString = new QueryString(queryString);
        }

        return context.Request;
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ALLOW_HEADER_AUTH"] = "true",
                ["AZURE_FUNCTIONS_ENVIRONMENT"] = "Testing",
            })
            .Build();

    private static T AssertOk<T>(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be((int)HttpStatusCode.OK);
        return ok.Value.Should().BeAssignableTo<T>().Subject;
    }
}
