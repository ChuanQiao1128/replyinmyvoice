using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Services;
using Stripe;

namespace ReplyInMyVoice.Tests;

public sealed class StripeBillingServiceTests
{
    [Fact]
    public async Task CreateCheckoutSessionUrlAsync_persists_no_user_mutation_or_credit_when_checkout_session_create_fails()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var existingUser = await CreateUserAsync(fixture);
        var originalRowVersion = existingUser.RowVersion;
        var fakeStripeClient = new FakeStripeBillingClient
        {
            CheckoutError = new TaskCanceledException("simulated Stripe session timeout"),
        };
        var service = new StripeBillingService(
            fixture.CreateContext,
            BuildConfiguration(),
            fakeStripeClient);

        var act = () => service.CreateCheckoutSessionUrlAsync(
            existingUser.ExternalAuthUserId,
            "changed@example.com",
            "quick_pack",
            CancellationToken.None);

        await act.Should().ThrowAsync<TaskCanceledException>()
            .WithMessage("*simulated Stripe session timeout*");

        fakeStripeClient.CheckoutRequests.Should().ContainSingle();
        var checkoutRequest = fakeStripeClient.CheckoutRequests[0];
        checkoutRequest.CustomerId.Should().BeNull();
        checkoutRequest.CustomerEmail.Should().Be("changed@example.com");
        checkoutRequest.Mode.Should().Be("payment");
        checkoutRequest.PriceId.Should().Be("price_quick_pack_test");

        await using var db = fixture.CreateContext();
        var storedUser = await db.AppUsers.SingleAsync(x => x.Id == existingUser.Id);
        storedUser.Email.Should().Be("original@example.com");
        storedUser.StripeCustomerId.Should().BeNull();
        storedUser.RowVersion.Should().Be(originalRowVersion);
        (await db.RewriteCredits.CountAsync()).Should().Be(0);
    }

    private static async Task<AppUser> CreateUserAsync(DbFixture fixture)
    {
        await using var db = fixture.CreateContext();
        var user = new AppUser
        {
            ExternalAuthUserId = $"clerk_checkout_{Guid.NewGuid():N}",
            Email = "original@example.com",
            SubscriptionStatus = SubscriptionStatus.Inactive,
            CreatedAt = DateTimeOffset.Parse("2026-05-30T00:00:00Z"),
            UpdatedAt = DateTimeOffset.Parse("2026-05-30T00:00:00Z"),
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["STRIPE_SECRET_KEY"] = "sk_test_local",
                ["NEXT_PUBLIC_APP_URL"] = "https://app.example.test",
                ["STRIPE_PRICE_QUICK_PACK_NZD"] = "price_quick_pack_test",
            })
            .Build();

    private sealed class FakeStripeBillingClient : IStripeBillingClient
    {
        public List<StripeCheckoutSessionCreateRequest> CheckoutRequests { get; } = [];

        public Exception? CheckoutError { get; init; }

        public Task<StripeCheckoutSessionResult> CreateCheckoutSessionAsync(
            StripeCheckoutSessionCreateRequest request,
            IStripeClient stripeClient,
            CancellationToken cancellationToken)
        {
            CheckoutRequests.Add(request);
            if (CheckoutError is not null)
            {
                throw CheckoutError;
            }

            return Task.FromResult(new StripeCheckoutSessionResult(
                "https://billing.test/checkout",
                "cus_checkout_success"));
        }

        public Task<StripePortalSessionResult> CreatePortalSessionAsync(
            string customerId,
            string returnUrl,
            IStripeClient stripeClient,
            CancellationToken cancellationToken) =>
            Task.FromResult(new StripePortalSessionResult("https://billing.test/portal"));

        public Task CancelSubscriptionAsync(
            string stripeSubscriptionId,
            IStripeClient stripeClient,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<StripeRefundResult> RefundPaymentAsync(
            StripeRefundRequest request,
            IStripeClient stripeClient,
            CancellationToken cancellationToken) =>
            Task.FromResult(new StripeRefundResult(
                "re_unused",
                request.PaymentIntentId,
                request.Amount,
                request.Currency,
                "succeeded"));
    }
}
