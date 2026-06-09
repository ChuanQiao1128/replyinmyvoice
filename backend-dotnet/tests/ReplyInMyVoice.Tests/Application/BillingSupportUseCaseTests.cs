using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.BillingSupport;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests.Application;

public sealed class BillingSupportUseCaseTests
{
    [Fact]
    public async Task CreateBillingSupportRequestAsync_creates_open_request_for_owned_payment()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.RewriteCredits.Add(new RewriteCredit
            {
                UserId = user.Id,
                Source = "PURCHASE",
                AmountGranted = 10,
                AmountConsumed = 0,
                GrantedAt = now.AddDays(-1),
                StripePaymentIntentId = "pi_support_owned",
            });
            await seedDb.SaveChangesAsync();
        }

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateCreateHandler(handlerDb);

        var result = await handler.HandleAsync(new CreateBillingSupportRequestCommand(
            user.Id,
            " refund ",
            " pi_support_owned ",
            " Please review this duplicate payment on my account. ",
            now));

        result.Kind.Should().Be(BillingSupportRequestResultKind.Success);
        result.Response.Should().NotBeNull();
        result.Response!.UserId.Should().Be(user.Id);
        result.Response.Type.Should().Be("refund");
        result.Response.RelatedPaymentIntentId.Should().Be("pi_support_owned");
        result.Response.Message.Should().Be("Please review this duplicate payment on my account.");
        result.Response.Status.Should().Be("open");
        result.Response.CreatedAt.Should().Be(now);

        await using var verifyDb = fixture.CreateContext();
        var stored = await verifyDb.BillingSupportRequests.SingleAsync();
        stored.Id.Should().Be(result.Response.Id);
        stored.UserId.Should().Be(user.Id);
        stored.Type.Should().Be(BillingSupportRequestType.Refund);
        stored.Status.Should().Be(BillingSupportRequestStatus.Open);
        stored.RelatedPaymentIntentId.Should().Be("pi_support_owned");
        stored.Message.Should().Be("Please review this duplicate payment on my account.");
        stored.CreatedAt.Should().Be(now);
        stored.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public async Task CreateBillingSupportRequestAsync_rejects_duplicate_open_request_without_side_effects()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.BillingSupportRequests.Add(new BillingSupportRequest
            {
                UserId = user.Id,
                Type = BillingSupportRequestType.BillingQuestion,
                Message = "I already have an open billing support request.",
                Status = BillingSupportRequestStatus.Open,
                CreatedAt = now.AddMinutes(-5),
                UpdatedAt = now.AddMinutes(-5),
            });
            await seedDb.SaveChangesAsync();
        }

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateCreateHandler(handlerDb);

        var result = await handler.HandleAsync(new CreateBillingSupportRequestCommand(
            user.Id,
            "billing-question",
            null,
            "Please add another billing support request for my account.",
            now));

        result.Kind.Should().Be(BillingSupportRequestResultKind.InvalidRequest);
        result.Response.Should().BeNull();
        result.Detail.Should().Contain("open");

        await using var verifyDb = fixture.CreateContext();
        (await verifyDb.BillingSupportRequests.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetBillingSupportRequestsAsync_returns_empty_list_for_user_without_requests()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateGetHandler(handlerDb);

        var result = await handler.HandleAsync(new GetBillingSupportRequestsQuery(user.Id));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBillingSupportRequestsAsync_returns_user_requests_newest_first()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var owner = await fixture.CreateUserAsync();
        var other = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        Guid olderId;
        Guid newerId;
        await using (var seedDb = fixture.CreateContext())
        {
            var older = new BillingSupportRequest
            {
                UserId = owner.Id,
                Type = BillingSupportRequestType.Refund,
                RelatedPaymentIntentId = "pi_older",
                Message = "Please review the older payment.",
                Status = BillingSupportRequestStatus.Resolved,
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now.AddDays(-1),
                ResolvedAt = now.AddDays(-1),
            };
            var newer = new BillingSupportRequest
            {
                UserId = owner.Id,
                Type = BillingSupportRequestType.BillingQuestion,
                Message = "Please answer the newer billing question.",
                Status = BillingSupportRequestStatus.Open,
                CreatedAt = now,
                UpdatedAt = now,
            };
            var otherRequest = new BillingSupportRequest
            {
                UserId = other.Id,
                Type = BillingSupportRequestType.Refund,
                Message = "This belongs to another user.",
                Status = BillingSupportRequestStatus.Open,
                CreatedAt = now.AddHours(1),
                UpdatedAt = now.AddHours(1),
            };
            seedDb.BillingSupportRequests.AddRange(older, newer, otherRequest);
            await seedDb.SaveChangesAsync();
            olderId = older.Id;
            newerId = newer.Id;
        }

        await using var handlerDb = fixture.CreateContext();
        var handler = CreateGetHandler(handlerDb);

        var result = await handler.HandleAsync(new GetBillingSupportRequestsQuery(owner.Id));

        result.Should().HaveCount(2);
        result.Select(x => x.Id).Should().Equal(newerId, olderId);
        result[0].Type.Should().Be("billing-question");
        result[0].Status.Should().Be("open");
        result[1].Type.Should().Be("refund");
        result[1].Status.Should().Be("resolved");
        result[1].ResolvedAt.Should().Be(now.AddDays(-1));
    }

    private static CreateBillingSupportRequestHandler CreateCreateHandler(AppDbContext db) =>
        new(
            new BillingSupportRepository(db),
            new UnitOfWork(db));

    private static GetBillingSupportRequestsHandler CreateGetHandler(AppDbContext db) =>
        new(new BillingSupportRepository(db));
}
