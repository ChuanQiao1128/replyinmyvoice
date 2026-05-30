using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class AdminSuspensionTests
{
    [Fact]
    public async Task SuspendedUserRewriteRejected()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-05-30T00:00:00Z");

        await using (var db = fixture.CreateContext())
        {
            var trackedUser = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
            trackedUser.SuspendedAt = now;
            await db.SaveChangesAsync();
        }

        var rewriteService = new RewriteRequestService(
            fixture.CreateContext,
            new QuotaService(fixture.CreateContext));

        var rejected = await rewriteService.CreateAttemptAsync(
            user.Id,
            "idem-suspended",
            CreateRewriteRequest(),
            "free:lifetime",
            quotaLimit: 3,
            now,
            CancellationToken.None);

        rejected.Status.Should().Be(RewriteAttemptStatus.Failed);
        rejected.ErrorCode.Should().Be("user_suspended");
        rejected.AttemptId.Should().Be(Guid.Empty);

        await using (var db = fixture.CreateContext())
        {
            (await db.RewriteAttempts.CountAsync()).Should().Be(0);
            (await db.UsageReservations.CountAsync()).Should().Be(0);
            (await db.UsagePeriods.CountAsync()).Should().Be(0);
            (await db.OutboxMessages.CountAsync()).Should().Be(0);
        }

        var adminService = new AdminService(fixture.CreateContext);
        var unsuspend = await adminService.SetUserSuspensionAsync(
            "admin-owner-oid",
            "owner@example.com",
            user.Id,
            suspended: false,
            now.AddMinutes(1),
            CancellationToken.None);

        unsuspend.Kind.Should().Be(AdminSuspensionResultKind.Success);
        unsuspend.Response!.Suspended.Should().BeFalse();
        unsuspend.Response.SuspendedAt.Should().BeNull();

        var restored = await rewriteService.CreateAttemptAsync(
            user.Id,
            "idem-restored",
            CreateRewriteRequest(),
            "free:lifetime",
            quotaLimit: 3,
            now.AddMinutes(2),
            CancellationToken.None);

        restored.Kind.Should().Be(ReserveRewriteResultKind.Created);

        await using (var db = fixture.CreateContext())
        {
            (await db.RewriteAttempts.CountAsync()).Should().Be(1);
            (await db.UsageReservations.CountAsync()).Should().Be(1);
            (await db.UsagePeriods.SingleAsync()).ReservedCount.Should().Be(1);
            (await db.OutboxMessages.CountAsync()).Should().Be(1);
        }
    }

    [Fact]
    public async Task AdminSuspendTogglesAndAudits()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var function = new AdminHttpFunctions(BuildConfiguration(), fixture.CreateContext);

        var forbidden = await function.SetUserSuspension(
            CreateRequest("regular-user-oid", "regular@example.com", new { suspended = true }),
            user.Id.ToString(),
            CancellationToken.None);

        var forbiddenResult = forbidden.Should().BeOfType<ObjectResult>().Subject;
        forbiddenResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        await using (var db = fixture.CreateContext())
        {
            (await db.AdminAuditLogs.CountAsync()).Should().Be(0);
            (await db.AppUsers.SingleAsync(x => x.Id == user.Id)).SuspendedAt.Should().BeNull();
        }

        var suspend = await function.SetUserSuspension(
            CreateRequest("admin-owner-oid", "owner@example.com", new { suspended = true }),
            user.Id.ToString(),
            CancellationToken.None);

        var suspendedResponse = suspend.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<AdminSuspensionResponse>().Subject;
        suspendedResponse.TargetUserId.Should().Be(user.Id);
        suspendedResponse.Suspended.Should().BeTrue();
        suspendedResponse.SuspendedAt.Should().NotBeNull();

        var unsuspend = await function.SetUserSuspension(
            CreateRequest("admin-owner-oid", "owner@example.com", new { suspended = false }),
            user.Id.ToString(),
            CancellationToken.None);

        var unsuspendedResponse = unsuspend.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<AdminSuspensionResponse>().Subject;
        unsuspendedResponse.TargetUserId.Should().Be(user.Id);
        unsuspendedResponse.Suspended.Should().BeFalse();
        unsuspendedResponse.SuspendedAt.Should().BeNull();

        await using (var db = fixture.CreateContext())
        {
            (await db.AppUsers.SingleAsync(x => x.Id == user.Id)).SuspendedAt.Should().BeNull();
            var audits = (await db.AdminAuditLogs.ToListAsync())
                .OrderBy(x => x.CreatedAt)
                .ToList();
            audits.Should().HaveCount(2);
            audits.Select(x => x.Action).Should().Equal("suspend_user", "unsuspend_user");
            audits.Should().OnlyContain(x =>
                x.TargetUserId == user.Id &&
                x.AdminExternalAuthUserId == "admin-owner-oid" &&
                x.AdminEmail == "owner@example.com");

            using var suspendDetails = JsonDocument.Parse(audits[0].DetailsJson!);
            suspendDetails.RootElement.GetProperty("suspended").GetBoolean().Should().BeTrue();
            suspendDetails.RootElement.GetProperty("suspendedAt").GetString().Should().NotBeNullOrWhiteSpace();

            using var unsuspendDetails = JsonDocument.Parse(audits[1].DetailsJson!);
            unsuspendDetails.RootElement.GetProperty("suspended").GetBoolean().Should().BeFalse();
            unsuspendDetails.RootElement.GetProperty("suspendedAt").ValueKind.Should().Be(JsonValueKind.Null);
        }
    }

    private static RewriteRequest CreateRewriteRequest() =>
        new("message", "rough draft reply", "teacher", "reply", "facts", "preserve", "warm");

    private static HttpRequest CreateRequest(string oid, string email, object body)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("oid", oid),
                new Claim("email", email),
            ], "Bearer")),
        };

        var json = JsonSerializer.Serialize(body);
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        context.Request.ContentType = "application/json";
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
