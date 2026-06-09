using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class AdminDeleteUserTests
{
    [Fact]
    public async Task DeleteUserAsyncErasesTargetAndWritesAuditLog()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-03T02:00:00Z");
        var user = await fixture.CreateUserAsync();
        await SeedUserStateAsync(fixture, user.Id, now.AddDays(-1));
        var service = new AdminService(fixture.CreateContext);

        var result = await service.DeleteUserAsync(
            "admin-owner-oid",
            "owner@example.com",
            user.Id,
            now,
            CancellationToken.None);

        result.Kind.Should().Be(AdminDeleteUserResultKind.Success);
        result.Response.Should().NotBeNull();
        result.Response!.UserId.Should().Be(user.Id);
        result.Response.Status.Should().Be("erased");

        await using var db = fixture.CreateContext();
        var erasedUser = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
        ExternalAuthUserId.IsErasedExternalAuthUserId(erasedUser.ExternalAuthUserId).Should().BeTrue();
        erasedUser.Email.Should().BeNull();
        erasedUser.SubscriptionStatus.Should().Be(SubscriptionStatus.Canceled);

        var usagePeriod = await db.UsagePeriods.SingleAsync(x => x.UserId == user.Id);
        usagePeriod.PeriodKey.Should().StartWith("erased:");
        usagePeriod.QuotaLimit.Should().Be(0);
        usagePeriod.UsedCount.Should().Be(0);
        usagePeriod.ReservedCount.Should().Be(0);

        var credit = await db.RewriteCredits.SingleAsync(x => x.UserId == user.Id);
        credit.Source.Should().Be("ERASED");
        credit.AmountGranted.Should().Be(0);
        credit.AmountConsumed.Should().Be(0);

        var audit = await db.AdminAuditLogs.SingleAsync();
        audit.AdminExternalAuthUserId.Should().Be("admin-owner-oid");
        audit.AdminEmail.Should().Be("owner@example.com");
        audit.Action.Should().Be("user.delete");
        audit.TargetUserId.Should().Be(user.Id);
        audit.CreatedAt.Should().Be(now);
    }

    [Fact]
    public async Task DeleteUserAsyncReturnsNotFoundForMissingUser()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var service = new AdminService(fixture.CreateContext);

        var result = await service.DeleteUserAsync(
            "admin-owner-oid",
            "owner@example.com",
            Guid.NewGuid(),
            DateTimeOffset.Parse("2026-06-03T02:00:00Z"),
            CancellationToken.None);

        result.Kind.Should().Be(AdminDeleteUserResultKind.UserNotFound);
        result.Detail.Should().Be("No user exists for the requested id.");

        await using var db = fixture.CreateContext();
        (await db.AdminAuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteUserAsyncForbidsSelfDelete()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new AdminService(fixture.CreateContext);

        var result = await service.DeleteUserAsync(
            user.ExternalAuthUserId,
            user.Email,
            user.Id,
            DateTimeOffset.Parse("2026-06-03T02:00:00Z"),
            CancellationToken.None);

        result.Kind.Should().Be(AdminDeleteUserResultKind.Forbidden);
        result.Detail.Should().Be("an admin cannot delete their own account from the console");

        await using var db = fixture.CreateContext();
        var storedUser = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
        storedUser.ExternalAuthUserId.Should().Be(user.ExternalAuthUserId);
        storedUser.Email.Should().Be(user.Email);
        (await db.AdminAuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteUserAsyncForbidsAlreadyErasedTarget()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using (var db = fixture.CreateContext())
        {
            var storedUser = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.ExternalAuthUserId = $"erased:{user.Id:N}";
            storedUser.Email = null;
            storedUser.SubscriptionStatus = SubscriptionStatus.Canceled;
            await db.SaveChangesAsync();
        }

        var service = new AdminService(fixture.CreateContext);

        var result = await service.DeleteUserAsync(
            "admin-owner-oid",
            "owner@example.com",
            user.Id,
            DateTimeOffset.Parse("2026-06-03T02:00:00Z"),
            CancellationToken.None);

        result.Kind.Should().Be(AdminDeleteUserResultKind.Forbidden);
        result.Detail.Should().Be("account already erased");

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.AdminAuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteUserEndpointRequiresAdminAndMapsSuccess()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var function = AdminHttpFunctionsTestFactory.Create(BuildConfiguration(), fixture.CreateContext);

        var forbidden = await function.DeleteUser(
            CreateRequest("regular-user-oid", "regular@example.com"),
            user.Id.ToString(),
            CancellationToken.None);

        var forbiddenResult = forbidden.Should().BeOfType<ObjectResult>().Subject;
        forbiddenResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        await using (var db = fixture.CreateContext())
        {
            (await db.AdminAuditLogs.CountAsync()).Should().Be(0);
            var storedUser = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
            ExternalAuthUserId.IsErasedExternalAuthUserId(storedUser.ExternalAuthUserId).Should().BeFalse();
        }

        var deleted = await function.DeleteUser(
            CreateRequest("admin-owner-oid", "owner@example.com"),
            user.Id.ToString(),
            CancellationToken.None);

        var response = deleted.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<AdminDeleteUserResponse>().Subject;
        response.UserId.Should().Be(user.Id);
        response.Status.Should().Be("erased");
    }

    private static async Task SeedUserStateAsync(
        DbFixture fixture,
        Guid userId,
        DateTimeOffset now)
    {
        await using var db = fixture.CreateContext();
        db.UsagePeriods.Add(new UsagePeriod
        {
            UserId = userId,
            PeriodKey = "free:lifetime",
            QuotaLimit = 3,
            UsedCount = 2,
            ReservedCount = 1,
            CreatedAt = now,
            UpdatedAt = now,
        });
        db.RewriteCredits.Add(new RewriteCredit
        {
            UserId = userId,
            Source = "PROMO",
            AmountGranted = 5,
            AmountConsumed = 1,
            GrantedAt = now,
        });
        await db.SaveChangesAsync();
    }

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
