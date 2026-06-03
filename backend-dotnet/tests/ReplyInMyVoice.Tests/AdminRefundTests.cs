using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class AdminRefundTests : IAsyncLifetime
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
    public async Task AdminRefundCallsStripeAndAudits()
    {
        var user = await SeedPaidUserAsync();
        await SeedPaymentCreditAsync(user.Id, "pi_refund_target", 1200, "nzd");
        var fakeRefundClient = new FakeStripeRefundClient("re_admin_refund");
        var function = CreateFunction(fakeRefundClient);
        var request = CreateRequest("admin-owner-oid", "owner@example.com", new
        {
            paymentIntentId = "pi_refund_target",
            amount = 1200,
        });

        var result = await function.IssueRefund(request, user.Id.ToString(), CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be((int)HttpStatusCode.OK);
        var response = okResult.Value.Should().BeOfType<AdminRefundResponse>().Subject;
        response.TargetUserId.Should().Be(user.Id);
        response.PaymentIntentId.Should().Be("pi_refund_target");
        response.Amount.Should().Be(1200);
        response.Currency.Should().Be("nzd");
        response.RefundId.Should().Be("re_admin_refund");
        response.AlreadyRefunded.Should().BeFalse();

        fakeRefundClient.Calls.Should().ContainSingle();
        fakeRefundClient.Calls[0].PaymentIntentId.Should().Be("pi_refund_target");
        fakeRefundClient.Calls[0].Amount.Should().Be(1200);
        fakeRefundClient.Calls[0].Currency.Should().Be("nzd");
        fakeRefundClient.Calls[0].IdempotencyKey.Should().NotBeNullOrWhiteSpace();

        await using var db = CreateContext();
        var audit = await db.AdminAuditLogs.SingleAsync();
        audit.AdminExternalAuthUserId.Should().Be("admin-owner-oid");
        audit.AdminEmail.Should().Be("owner@example.com");
        audit.Action.Should().Be("refund");
        audit.TargetUserId.Should().Be(user.Id);

        using var details = JsonDocument.Parse(audit.DetailsJson!);
        details.RootElement.GetProperty("paymentIntentId").GetString().Should().Be("pi_refund_target");
        details.RootElement.GetProperty("refundId").GetString().Should().Be("re_admin_refund");
        details.RootElement.GetProperty("amount").GetInt64().Should().Be(1200);
        details.RootElement.GetProperty("currency").GetString().Should().Be("nzd");
    }

    [Fact]
    public async Task AdminRefundForbiddenAndIdempotent()
    {
        var user = await SeedPaidUserAsync();
        await SeedPaymentCreditAsync(user.Id, "pi_refund_repeat", 900, "nzd");
        var fakeRefundClient = new FakeStripeRefundClient("re_repeat_refund");
        var function = CreateFunction(fakeRefundClient);

        var forbidden = await function.IssueRefund(
            CreateRequest("regular-user-oid", "regular@example.com", new
            {
                paymentIntentId = "pi_refund_repeat",
                amount = 900,
            }),
            user.Id.ToString(),
            CancellationToken.None);

        var forbiddenResult = forbidden.Should().BeOfType<ObjectResult>().Subject;
        forbiddenResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        fakeRefundClient.Calls.Should().BeEmpty();

        var first = await function.IssueRefund(
            CreateRequest("admin-owner-oid", "owner@example.com", new
            {
                paymentIntentId = "pi_refund_repeat",
                amount = 900,
            }),
            user.Id.ToString(),
            CancellationToken.None);

        var firstResponse = first.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<AdminRefundResponse>().Subject;
        firstResponse.AlreadyRefunded.Should().BeFalse();

        var second = await function.IssueRefund(
            CreateRequest("admin-owner-oid", "owner@example.com", new
            {
                paymentIntentId = "pi_refund_repeat",
                amount = 900,
            }),
            user.Id.ToString(),
            CancellationToken.None);

        var secondResponse = second.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<AdminRefundResponse>().Subject;
        secondResponse.AlreadyRefunded.Should().BeTrue();
        secondResponse.RefundId.Should().Be("re_repeat_refund");
        fakeRefundClient.Calls.Should().ContainSingle();

        await using var db = CreateContext();
        (await db.AdminAuditLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AdminRefundDoesNotAuditOrClawBackCreditWhenStripeRefundFails()
    {
        var user = await SeedPaidUserAsync();
        await SeedPaymentCreditAsync(user.Id, "pi_refund_failure", 1200, "nzd");
        var fakeRefundClient = new FakeStripeRefundClient("re_never_saved")
        {
            RefundError = new TaskCanceledException("simulated Stripe refund timeout"),
        };
        var service = new AdminService(CreateContext, fakeRefundClient);
        var request = new AdminRefundRequest("pi_refund_failure", 1200, "nzd");

        var act = () => service.IssueRefundAsync(
            "admin-owner-oid",
            "owner@example.com",
            user.Id,
            request,
            CancellationToken.None);

        await act.Should().ThrowAsync<TaskCanceledException>()
            .WithMessage("*simulated Stripe refund timeout*");

        fakeRefundClient.Calls.Should().ContainSingle();
        fakeRefundClient.Calls[0].IdempotencyKey.Should()
            .Be($"admin-refund:{user.Id:N}:pi_refund_failure:1200");

        await using var db = CreateContext();
        (await db.AdminAuditLogs.CountAsync()).Should().Be(0);
        (await db.AdminAuditLogs.CountAsync(x => x.Action == "refund")).Should().Be(0);
        var credit = await db.RewriteCredits.SingleAsync(x => x.StripePaymentIntentId == "pi_refund_failure");
        credit.AmountGranted.Should().Be(10);
        credit.AmountConsumed.Should().Be(0);
    }

    private AdminHttpFunctions CreateFunction(FakeStripeRefundClient fakeRefundClient) =>
        new(BuildConfiguration(), CreateContext, fakeRefundClient);

    private async Task<AppUser> SeedPaidUserAsync()
    {
        await using var db = CreateContext();
        var user = new AppUser
        {
            ExternalAuthUserId = "clerk_paid_refund",
            Email = "paid-refund@example.com",
            SubscriptionStatus = SubscriptionStatus.Active,
            CreatedAt = DateTimeOffset.Parse("2026-05-30T00:00:00Z"),
            UpdatedAt = DateTimeOffset.Parse("2026-05-30T00:00:00Z"),
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task SeedPaymentCreditAsync(
        Guid userId,
        string paymentIntentId,
        long amount,
        string currency)
    {
        await using var db = CreateContext();
        db.RewriteCredits.Add(new RewriteCredit
        {
            UserId = userId,
            Source = "PURCHASE",
            AmountGranted = 10,
            AmountConsumed = 0,
            GrantedAt = DateTimeOffset.Parse("2026-05-30T00:00:00Z"),
            StripeEventId = $"evt_{paymentIntentId}",
            StripePaymentIntentId = paymentIntentId,
            StripeSku = "quick_pack",
            StripeAmountTotal = amount,
            StripeCurrency = currency,
        });
        await db.SaveChangesAsync();
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }

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

    private sealed class FakeStripeRefundClient(string refundId) : IStripeRefundClient
    {
        public List<StripeRefundRequest> Calls { get; } = [];

        public Exception? RefundError { get; init; }

        public Task<StripeRefundResult> RefundPaymentAsync(
            StripeRefundRequest request,
            CancellationToken cancellationToken)
        {
            Calls.Add(request);
            if (RefundError is not null)
            {
                throw RefundError;
            }

            return Task.FromResult(new StripeRefundResult(
                refundId,
                request.PaymentIntentId,
                request.Amount,
                request.Currency,
                "succeeded"));
        }
    }
}
