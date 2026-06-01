using FluentAssertions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class StripeReconciliationServiceTests
{
    private static readonly DateTimeOffset WindowStart = DateTimeOffset.Parse("2026-06-01T00:00:00Z");
    private static readonly DateTimeOffset WindowEnd = DateTimeOffset.Parse("2026-06-02T00:00:00Z");
    private static readonly DateTimeOffset CompletedAt = DateTimeOffset.Parse("2026-06-02T01:00:00Z");

    [Fact]
    public async Task ReconcileAsync_flags_paid_payment_without_grant_and_amount_mismatch()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await SeedPurchaseCreditAsync(
            fixture,
            user.Id,
            paymentIntentId: "pi_mismatch",
            amountTotal: 500,
            currency: "nzd");
        var stripe = new FailingOnWriteStripeReconciliationClient(
        [
            new StripePaidPayment("pi_missing", 250, "nzd", WindowStart.AddHours(1)),
            new StripePaidPayment("pi_mismatch", 690, "nzd", WindowStart.AddHours(2)),
        ]);
        var alerter = new RecordingStripeReconciliationAlerter();
        var service = new StripeReconciliationService(fixture.CreateContext, stripe, alerter);

        var report = await service.ReconcileAsync(WindowStart, WindowEnd, CompletedAt, CancellationToken.None);

        report.DiscrepancyCount.Should().Be(2);
        report.PaidButNoGrantCount.Should().Be(1);
        report.GrantButNoPaymentCount.Should().Be(0);
        report.AmountMismatchCount.Should().Be(1);
        report.Discrepancies.Select(x => x.Kind)
            .Should()
            .BeEquivalentTo(
            [
                StripeReconciliationDiscrepancyKind.PaidButNoGrant,
                StripeReconciliationDiscrepancyKind.AmountMismatch,
            ]);
        alerter.Reports.Should().ContainSingle().Which.DiscrepancyCount.Should().Be(2);

        await using var db = fixture.CreateContext();
        var storedRun = db.StripeReconciliationRuns.Should().ContainSingle().Subject;
        storedRun.PaidButNoGrantCount.Should().Be(1);
        storedRun.AmountMismatchCount.Should().Be(1);
        storedRun.ReportJson.Should().Contain("pi_missing");
        storedRun.ReportJson.Should().Contain("pi_mismatch");
    }

    [Fact]
    public async Task ReconcileAsync_flags_purchase_grant_without_paid_payment()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await SeedPurchaseCreditAsync(
            fixture,
            user.Id,
            paymentIntentId: "pi_internal_only",
            amountTotal: 250,
            currency: "nzd");
        var service = new StripeReconciliationService(
            fixture.CreateContext,
            new FailingOnWriteStripeReconciliationClient([]),
            new RecordingStripeReconciliationAlerter());

        var report = await service.ReconcileAsync(WindowStart, WindowEnd, CompletedAt, CancellationToken.None);

        report.DiscrepancyCount.Should().Be(1);
        report.PaidButNoGrantCount.Should().Be(0);
        report.GrantButNoPaymentCount.Should().Be(1);
        report.AmountMismatchCount.Should().Be(0);
        report.Discrepancies.Should().ContainSingle(x =>
            x.Kind == StripeReconciliationDiscrepancyKind.GrantButNoPayment &&
            x.PaymentIntentId == "pi_internal_only");
    }

    [Fact]
    public async Task ReconcileAsync_clean_dataset_reports_zero_discrepancies_and_stats_summary()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await SeedPurchaseCreditAsync(
            fixture,
            user.Id,
            paymentIntentId: "pi_clean",
            amountTotal: 250,
            currency: "nzd");
        var service = new StripeReconciliationService(
            fixture.CreateContext,
            new FailingOnWriteStripeReconciliationClient(
            [
                new StripePaidPayment("pi_clean", 250, "nzd", WindowStart.AddHours(1)),
            ]),
            new RecordingStripeReconciliationAlerter());

        var report = await service.ReconcileAsync(WindowStart, WindowEnd, CompletedAt, CancellationToken.None);
        var stats = await new AdminService(fixture.CreateContext).GetStatsAsync(CancellationToken.None);

        report.DiscrepancyCount.Should().Be(0);
        report.Discrepancies.Should().BeEmpty();
        stats.PaymentReconciliation.Should().NotBeNull();
        stats.PaymentReconciliation!.LastCompletedAt.Should().Be(CompletedAt);
        stats.PaymentReconciliation.DiscrepancyCount.Should().Be(0);
        stats.PaymentReconciliation.StripePaymentCount.Should().Be(1);
        stats.PaymentReconciliation.PurchaseGrantCount.Should().Be(1);
    }

    [Fact]
    public async Task ReconcileAsync_uses_read_only_stripe_client()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        await SeedPurchaseCreditAsync(
            fixture,
            user.Id,
            paymentIntentId: "pi_read_only",
            amountTotal: 250,
            currency: "nzd");
        var stripe = new FailingOnWriteStripeReconciliationClient(
        [
            new StripePaidPayment("pi_read_only", 250, "nzd", WindowStart.AddHours(1)),
        ]);
        var service = new StripeReconciliationService(
            fixture.CreateContext,
            stripe,
            new RecordingStripeReconciliationAlerter());

        await service.ReconcileAsync(WindowStart, WindowEnd, CompletedAt, CancellationToken.None);

        stripe.ReadCallCount.Should().Be(1);
        stripe.WriteCallCount.Should().Be(0);
    }

    private static async Task SeedPurchaseCreditAsync(
        DbFixture fixture,
        Guid userId,
        string paymentIntentId,
        long? amountTotal,
        string? currency)
    {
        await using var db = fixture.CreateContext();
        db.RewriteCredits.Add(new RewriteCredit
        {
            UserId = userId,
            Source = "PURCHASE",
            AmountGranted = 10,
            AmountConsumed = 0,
            GrantedAt = WindowStart.AddHours(3),
            ExpiresAt = WindowStart.AddDays(90),
            StripeEventId = $"evt_{paymentIntentId}",
            StripePaymentIntentId = paymentIntentId,
            StripeSku = "quick_pack",
            StripeAmountTotal = amountTotal,
            StripeCurrency = currency,
        });
        await db.SaveChangesAsync();
    }

    private sealed class FailingOnWriteStripeReconciliationClient(
        IReadOnlyList<StripePaidPayment> payments) : IStripePaymentReconciliationClient
    {
        public int ReadCallCount { get; private set; }
        public int WriteCallCount { get; private set; }

        public Task<IReadOnlyList<StripePaidPayment>> ListPaidPaymentIntentsAsync(
            DateTimeOffset windowStart,
            DateTimeOffset windowEnd,
            CancellationToken cancellationToken)
        {
            ReadCallCount++;
            return Task.FromResult(payments);
        }

        public Task FailIfAnyStripeWriteIsAttemptedAsync()
        {
            WriteCallCount++;
            throw new InvalidOperationException("stripe_write_not_allowed");
        }
    }

    private sealed class RecordingStripeReconciliationAlerter : IStripeReconciliationAlerter
    {
        public List<StripeReconciliationReport> Reports { get; } = [];

        public Task AlertAsync(
            StripeReconciliationReport report,
            CancellationToken cancellationToken)
        {
            Reports.Add(report);
            return Task.CompletedTask;
        }
    }
}
