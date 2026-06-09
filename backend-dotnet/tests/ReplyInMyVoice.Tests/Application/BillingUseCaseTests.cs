using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Billing;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests.Application;

public sealed class BillingUseCaseTests
{
    [Fact]
    public async Task CreateCheckoutSessionAsync_creates_user_after_provider_success()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var stripeClient = new FakeStripeBillingClient
        {
            CheckoutResult = new StripeCheckoutSessionResult(
                "https://billing.test/checkout",
                "cus_created"),
        };
        await using var handlerDb = fixture.CreateContext();
        var handler = new CreateCheckoutSessionHandler(
            new AppUserRepository(handlerDb),
            stripeClient,
            new UnitOfWork(handlerDb));

        var result = await handler.HandleAsync(new CreateCheckoutSessionCommand(
            "  clerk_checkout_create  ",
            " buyer@example.com ",
            "quick_pack"));

        result.Url.Should().Be("https://billing.test/checkout");
        stripeClient.CheckoutRequests.Should().ContainSingle();
        stripeClient.CheckoutRequests[0].ExternalAuthUserId.Should().Be("clerk_checkout_create");
        stripeClient.CheckoutRequests[0].CustomerId.Should().BeNull();
        stripeClient.CheckoutRequests[0].CustomerEmail.Should().Be("buyer@example.com");
        stripeClient.CheckoutRequests[0].Sku.Should().Be("quick_pack");

        await using var verifyDb = fixture.CreateContext();
        var user = await verifyDb.AppUsers.SingleAsync();
        user.ExternalAuthUserId.Should().Be("clerk_checkout_create");
        user.Email.Should().Be("buyer@example.com");
        user.StripeCustomerId.Should().Be("cus_created");
        user.SubscriptionStatus.Should().Be(SubscriptionStatus.Inactive);
    }

    [Fact]
    public async Task CreatePortalSessionAsync_uses_existing_customer()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedUserAsync(
            fixture,
            "clerk_portal",
            stripeCustomerId: "cus_portal",
            stripeSubscriptionId: null);
        var stripeClient = new FakeStripeBillingClient
        {
            PortalResult = new StripePortalSessionResult("https://billing.test/portal"),
        };
        await using var handlerDb = fixture.CreateContext();
        var handler = new CreatePortalSessionHandler(
            new AppUserRepository(handlerDb),
            stripeClient);

        var result = await handler.HandleAsync(new CreatePortalSessionQuery(" clerk_portal "));

        result.Url.Should().Be("https://billing.test/portal");
        stripeClient.PortalCustomerIds.Should().Equal("cus_portal");
    }

    [Fact]
    public async Task CancelSubscriptionAsync_calls_provider_for_existing_subscription()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedUserAsync(
            fixture,
            "clerk_cancel",
            stripeCustomerId: "cus_cancel",
            stripeSubscriptionId: " sub_active ");
        var stripeClient = new FakeStripeBillingClient();
        await using var handlerDb = fixture.CreateContext();
        var handler = new CancelSubscriptionHandler(
            new AppUserRepository(handlerDb),
            stripeClient);

        var result = await handler.HandleAsync(new CancelSubscriptionCommand("clerk_cancel"));

        result.Canceled.Should().BeTrue();
        result.SubscriptionId.Should().Be("sub_active");
        stripeClient.CanceledSubscriptionIds.Should().Equal("sub_active");
    }

    [Fact]
    public async Task CancelSubscriptionAsync_skips_user_without_subscription()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedUserAsync(
            fixture,
            "clerk_cancel_none",
            stripeCustomerId: "cus_cancel_none",
            stripeSubscriptionId: null);
        var stripeClient = new FakeStripeBillingClient();
        await using var handlerDb = fixture.CreateContext();
        var handler = new CancelSubscriptionHandler(
            new AppUserRepository(handlerDb),
            stripeClient);

        var result = await handler.HandleAsync(new CancelSubscriptionCommand("clerk_cancel_none"));

        result.Canceled.Should().BeFalse();
        result.SubscriptionId.Should().BeNull();
        stripeClient.CanceledSubscriptionIds.Should().BeEmpty();
    }

    [Fact]
    public async Task RefundPaymentAsync_refunds_existing_payment()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        await SeedCreditAsync(fixture, user.Id, "pi_refund", 1_200, "nzd", now);
        var stripeClient = new FakeStripeBillingClient
        {
            RefundResult = new StripeRefundResult(
                "re_created",
                "pi_refund",
                1_200,
                "nzd",
                "succeeded"),
        };
        await using var handlerDb = fixture.CreateContext();
        var handler = new RefundPaymentHandler(
            new AppUserRepository(handlerDb),
            new RewriteCreditRepository(handlerDb),
            stripeClient);

        var result = await handler.HandleAsync(new RefundPaymentCommand(
            user.Id,
            " pi_refund ",
            Amount: null,
            Currency: null,
            IdempotencyKey: "refund-key"));

        result.Kind.Should().Be(ApplicationResultKind.Success);
        result.Value.Should().NotBeNull();
        result.Value!.RefundId.Should().Be("re_created");
        result.Value.Amount.Should().Be(1_200);
        stripeClient.RefundRequests.Should().ContainSingle();
        stripeClient.RefundRequests[0].PaymentIntentId.Should().Be("pi_refund");
        stripeClient.RefundRequests[0].Amount.Should().Be(1_200);
        stripeClient.RefundRequests[0].Currency.Should().Be("nzd");
        stripeClient.RefundRequests[0].IdempotencyKey.Should().Be("refund-key");
        stripeClient.RefundRequests[0].TargetUserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task RefundPaymentAsync_returns_not_found_for_missing_payment()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var stripeClient = new FakeStripeBillingClient();
        await using var handlerDb = fixture.CreateContext();
        var handler = new RefundPaymentHandler(
            new AppUserRepository(handlerDb),
            new RewriteCreditRepository(handlerDb),
            stripeClient);

        var result = await handler.HandleAsync(new RefundPaymentCommand(
            user.Id,
            "pi_missing",
            Amount: 500,
            Currency: "nzd",
            IdempotencyKey: "refund-missing"));

        result.Kind.Should().Be(ApplicationResultKind.NotFound);
        result.Value.Should().BeNull();
        stripeClient.RefundRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task ListPaidPaymentsAsync_returns_paid_payments_from_provider()
    {
        var stripeClient = new FakeStripeBillingClient
        {
            PaidPayments =
            [
                new StripePaidPaymentDto(
                    "pi_paid",
                    900,
                    "nzd",
                    DateTimeOffset.Parse("2026-06-01T00:00:00Z")),
            ],
        };
        var handler = new ListPaidPaymentsHandler(stripeClient);
        var windowStart = DateTimeOffset.Parse("2026-06-01T00:00:00Z");
        var windowEnd = DateTimeOffset.Parse("2026-07-01T00:00:00Z");

        var payments = await handler.HandleAsync(new ListPaidPaymentsQuery(windowStart, windowEnd));

        payments.Should().ContainSingle();
        payments[0].PaymentIntentId.Should().Be("pi_paid");
        stripeClient.ListPaymentWindows.Should().ContainSingle(x =>
            x.WindowStart == windowStart &&
            x.WindowEnd == windowEnd);
    }

    [Fact]
    public async Task GetTaxTurnoverReportAsync_sums_gross_nzd_purchases_and_sends_warning()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-01T12:00:00Z");
        await SeedCreditAsync(fixture, user.Id, "pi_one", 5_000, "nzd", now.AddMonths(-2));
        await SeedCreditAsync(fixture, user.Id, "pi_two", 3_100, "NZD", now.AddMonths(-11));
        await SeedCreditAsync(fixture, user.Id, "pi_old", 9_000, "nzd", now.AddMonths(-13));
        await SeedCreditAsync(fixture, user.Id, "pi_usd", 4_000, "usd", now.AddMonths(-1));
        var notifier = new FakeTaxTurnoverNotifier();
        await using var handlerDb = fixture.CreateContext();
        var handler = new GetTaxTurnoverReportHandler(
            new RewriteCreditRepository(handlerDb),
            notifier,
            new FakeTaxTurnoverSettingsProvider(
                RegistrationThresholdAmountTotal: 10_000,
                WarningFraction: 0.80m));

        var report = await handler.HandleAsync(new GetTaxTurnoverReportQuery(now));

        report.WindowStart.Should().Be(now.AddMonths(-12));
        report.WindowEnd.Should().Be(now);
        report.Currency.Should().Be("nzd");
        report.GrossAmountTotal.Should().Be(8_100);
        report.RegistrationThresholdAmountTotal.Should().Be(10_000);
        report.WarningFraction.Should().Be(0.80m);
        report.WarningAmountTotal.Should().Be(8_000);
        report.IgnoredNonNzdPaymentCount.Should().Be(1);
        report.Warning.Should().NotBeNull();
        report.Notification.Should().NotBeNull();
        report.Notification!.Attempted.Should().BeTrue();
        notifier.Requests.Should().ContainSingle();
        notifier.Requests[0].GrossAmountTotal.Should().Be(8_100);
    }

    private static async Task SeedUserAsync(
        DbFixture fixture,
        string externalAuthUserId,
        string? stripeCustomerId,
        string? stripeSubscriptionId)
    {
        await using var db = fixture.CreateContext();
        db.AppUsers.Add(new AppUser
        {
            ExternalAuthUserId = externalAuthUserId,
            Email = $"{externalAuthUserId}@example.com",
            StripeCustomerId = stripeCustomerId,
            StripeSubscriptionId = stripeSubscriptionId,
            SubscriptionStatus = stripeSubscriptionId is null
                ? SubscriptionStatus.Inactive
                : SubscriptionStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedCreditAsync(
        DbFixture fixture,
        Guid userId,
        string paymentIntentId,
        long? amountTotal,
        string? currency,
        DateTimeOffset grantedAt)
    {
        await using var db = fixture.CreateContext();
        db.RewriteCredits.Add(new RewriteCredit
        {
            UserId = userId,
            Source = "PURCHASE",
            AmountGranted = 10,
            AmountConsumed = 0,
            GrantedAt = grantedAt,
            StripePaymentIntentId = paymentIntentId,
            StripeAmountTotal = amountTotal,
            StripeCurrency = currency,
        });
        await db.SaveChangesAsync();
    }

    private sealed class FakeStripeBillingClient : IStripeBillingClient
    {
        public List<StripeCheckoutSessionCreateRequest> CheckoutRequests { get; } = [];
        public List<string> PortalCustomerIds { get; } = [];
        public List<string> CanceledSubscriptionIds { get; } = [];
        public List<StripeRefundRequest> RefundRequests { get; } = [];
        public List<(DateTimeOffset WindowStart, DateTimeOffset WindowEnd)> ListPaymentWindows { get; } = [];

        public StripeCheckoutSessionResult CheckoutResult { get; init; } =
            new("https://billing.test/checkout", "cus_default");

        public StripePortalSessionResult PortalResult { get; init; } =
            new("https://billing.test/portal");

        public StripeRefundResult RefundResult { get; init; } =
            new("re_default", "pi_default", 100, "nzd", "succeeded");

        public IReadOnlyList<StripePaidPaymentDto> PaidPayments { get; init; } = [];

        public Task<StripeCheckoutSessionResult> CreateCheckoutSessionAsync(
            StripeCheckoutSessionCreateRequest request,
            CancellationToken ct = default)
        {
            CheckoutRequests.Add(request);
            return Task.FromResult(CheckoutResult);
        }

        public Task<StripePortalSessionResult> CreatePortalSessionAsync(
            string customerId,
            CancellationToken ct = default)
        {
            PortalCustomerIds.Add(customerId);
            return Task.FromResult(PortalResult);
        }

        public Task CancelSubscriptionAsync(
            string stripeSubscriptionId,
            CancellationToken ct = default)
        {
            CanceledSubscriptionIds.Add(stripeSubscriptionId);
            return Task.CompletedTask;
        }

        public Task<StripeRefundResult> RefundPaymentAsync(
            StripeRefundRequest request,
            CancellationToken ct = default)
        {
            RefundRequests.Add(request);
            return Task.FromResult(RefundResult);
        }

        public Task<IReadOnlyList<StripePaidPaymentDto>> ListPaidPaymentIntentsAsync(
            DateTimeOffset windowStart,
            DateTimeOffset windowEnd,
            CancellationToken ct = default)
        {
            ListPaymentWindows.Add((windowStart, windowEnd));
            return Task.FromResult(PaidPayments);
        }
    }

    private sealed class FakeTaxTurnoverNotifier : ITaxTurnoverNotifier
    {
        public List<TaxTurnoverNotificationRequest> Requests { get; } = [];

        public Task<TaxTurnoverNotificationResultDto> TrySendWarningNotificationAsync(
            TaxTurnoverNotificationRequest request,
            CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(new TaxTurnoverNotificationResultDto(
                Attempted: true,
                Sent: true,
                Provider: "test",
                Reason: null));
        }
    }

    private sealed class FakeTaxTurnoverSettingsProvider(
        long RegistrationThresholdAmountTotal,
        decimal WarningFraction) : ITaxTurnoverSettingsProvider
    {
        public TaxTurnoverSettings GetSettings() =>
            new(RegistrationThresholdAmountTotal, WarningFraction);
    }
}
