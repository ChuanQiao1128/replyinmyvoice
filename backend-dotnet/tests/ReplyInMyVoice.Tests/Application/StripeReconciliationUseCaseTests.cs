using System.Data;
using FluentAssertions;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.StripeReconciliation;

namespace ReplyInMyVoice.Tests.Application;

public sealed class StripeReconciliationUseCaseTests
{
    private static readonly DateTimeOffset WindowStart = DateTimeOffset.Parse("2026-06-01T00:00:00Z");
    private static readonly DateTimeOffset WindowEnd = DateTimeOffset.Parse("2026-06-02T00:00:00Z");
    private static readonly DateTimeOffset CompletedAt = DateTimeOffset.Parse("2026-06-02T00:05:00Z");

    [Fact]
    public async Task ReconcileAsync_returns_clean_report_without_alert_when_payment_has_matching_grant()
    {
        var creditId = Guid.NewGuid();
        var stripeClient = new FakeStripePaymentReconciliationClient
        {
            Payments =
            [
                new StripePaidPaymentDto(
                    " pi_clean ",
                    250,
                    "NZD",
                    WindowStart.AddHours(1)),
            ],
        };
        var grantRepository = new FakePaymentGrantRepository
        {
            Grants =
            [
                new PaymentGrantSnapshot(
                    creditId,
                    "pi_clean",
                    250,
                    "nzd",
                    WindowStart.AddHours(1).AddMinutes(2)),
            ],
        };
        var alerter = new FakeStripeReconciliationAlerter();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ReconcileStripeHandler(
            grantRepository,
            stripeClient,
            alerter,
            unitOfWork);

        var report = await handler.HandleAsync(new ReconcileStripeCommand(
            WindowStart,
            WindowEnd,
            CompletedAt));

        report.WindowStart.Should().Be(WindowStart);
        report.WindowEnd.Should().Be(WindowEnd);
        report.CompletedAt.Should().Be(CompletedAt);
        report.StripePaymentCount.Should().Be(1);
        report.PurchaseGrantCount.Should().Be(1);
        report.DiscrepancyCount.Should().Be(0);
        report.Discrepancies.Should().BeEmpty();
        grantRepository.PaymentIntentIds.Should().Equal("pi_clean");
        alerter.Reports.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task ReconcileAsync_alerts_when_paid_payment_has_no_grant()
    {
        var stripeClient = new FakeStripePaymentReconciliationClient
        {
            Payments =
            [
                new StripePaidPaymentDto(
                    "pi_missing_grant",
                    750,
                    "nzd",
                    WindowStart.AddHours(2)),
            ],
        };
        var grantRepository = new FakePaymentGrantRepository();
        var alerter = new FakeStripeReconciliationAlerter();
        var handler = new ReconcileStripeHandler(
            grantRepository,
            stripeClient,
            alerter,
            new FakeUnitOfWork());

        var report = await handler.HandleAsync(new ReconcileStripeCommand(
            WindowStart,
            WindowEnd,
            CompletedAt));

        report.PaidButNoGrantCount.Should().Be(1);
        report.GrantButNoPaymentCount.Should().Be(0);
        report.AmountMismatchCount.Should().Be(0);
        report.Discrepancies.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new StripeReconciliationDiscrepancyDto(
                StripeReconciliationDiscrepancyKindDto.PaidButNoGrant,
                "pi_missing_grant",
                CreditId: null,
                StripeAmount: 750,
                LedgerAmount: null,
                StripeCurrency: "nzd",
                LedgerCurrency: null,
                StripePaidAt: WindowStart.AddHours(2),
                LedgerGrantedAt: null));
        alerter.Reports.Should().ContainSingle().Which.Should().BeSameAs(report);
    }

    [Fact]
    public async Task ReconcileAsync_alerts_when_purchase_grant_has_no_paid_payment()
    {
        var creditId = Guid.NewGuid();
        var grantRepository = new FakePaymentGrantRepository
        {
            Grants =
            [
                new PaymentGrantSnapshot(
                    creditId,
                    "pi_missing_payment",
                    900,
                    "nzd",
                    WindowStart.AddHours(3)),
            ],
        };
        var alerter = new FakeStripeReconciliationAlerter();
        var handler = new ReconcileStripeHandler(
            grantRepository,
            new FakeStripePaymentReconciliationClient(),
            alerter,
            new FakeUnitOfWork());

        var report = await handler.HandleAsync(new ReconcileStripeCommand(
            WindowStart,
            WindowEnd,
            CompletedAt));

        report.PaidButNoGrantCount.Should().Be(0);
        report.GrantButNoPaymentCount.Should().Be(1);
        report.AmountMismatchCount.Should().Be(0);
        report.Discrepancies.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new StripeReconciliationDiscrepancyDto(
                StripeReconciliationDiscrepancyKindDto.GrantButNoPayment,
                "pi_missing_payment",
                creditId,
                StripeAmount: null,
                LedgerAmount: 900,
                StripeCurrency: null,
                LedgerCurrency: "nzd",
                StripePaidAt: null,
                LedgerGrantedAt: WindowStart.AddHours(3)));
        alerter.Reports.Should().ContainSingle().Which.Should().BeSameAs(report);
    }

    [Fact]
    public async Task ReconcileAsync_alerts_when_matching_grant_amount_differs_from_paid_payment()
    {
        var creditId = Guid.NewGuid();
        var stripeClient = new FakeStripePaymentReconciliationClient
        {
            Payments =
            [
                new StripePaidPaymentDto(
                    "pi_amount_mismatch",
                    1_200,
                    "nzd",
                    WindowStart.AddHours(4)),
            ],
        };
        var grantRepository = new FakePaymentGrantRepository
        {
            Grants =
            [
                new PaymentGrantSnapshot(
                    creditId,
                    "pi_amount_mismatch",
                    1_000,
                    "nzd",
                    WindowStart.AddHours(4).AddMinutes(2)),
            ],
        };
        var alerter = new FakeStripeReconciliationAlerter();
        var handler = new ReconcileStripeHandler(
            grantRepository,
            stripeClient,
            alerter,
            new FakeUnitOfWork());

        var report = await handler.HandleAsync(new ReconcileStripeCommand(
            WindowStart,
            WindowEnd,
            CompletedAt));

        report.PaidButNoGrantCount.Should().Be(0);
        report.GrantButNoPaymentCount.Should().Be(0);
        report.AmountMismatchCount.Should().Be(1);
        report.Discrepancies.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new StripeReconciliationDiscrepancyDto(
                StripeReconciliationDiscrepancyKindDto.AmountMismatch,
                "pi_amount_mismatch",
                creditId,
                StripeAmount: 1_200,
                LedgerAmount: 1_000,
                StripeCurrency: "nzd",
                LedgerCurrency: "nzd",
                StripePaidAt: WindowStart.AddHours(4),
                LedgerGrantedAt: WindowStart.AddHours(4).AddMinutes(2)));
        alerter.Reports.Should().ContainSingle().Which.Should().BeSameAs(report);
    }

    private sealed class FakePaymentGrantRepository : IPaymentGrantRepository
    {
        public IReadOnlyList<PaymentGrantSnapshot> Grants { get; init; } = [];
        public IReadOnlyList<string> PaymentIntentIds { get; private set; } = [];

        public Task<IReadOnlyList<PaymentGrantSnapshot>> ListPurchaseGrantsForReconciliationAsync(
            DateTimeOffset windowStart,
            DateTimeOffset windowEnd,
            IReadOnlyCollection<string> paymentIntentIds,
            CancellationToken ct = default)
        {
            windowStart.Should().Be(WindowStart);
            windowEnd.Should().Be(WindowEnd);
            PaymentIntentIds = paymentIntentIds.ToList();
            return Task.FromResult(Grants);
        }
    }

    private sealed class FakeStripePaymentReconciliationClient : IStripePaymentReconciliationClient
    {
        public IReadOnlyList<StripePaidPaymentDto> Payments { get; init; } = [];

        public Task<IReadOnlyList<StripePaidPaymentDto>> ListPaidPaymentIntentsAsync(
            DateTimeOffset windowStart,
            DateTimeOffset windowEnd,
            CancellationToken ct = default)
        {
            windowStart.Should().Be(WindowStart);
            windowEnd.Should().Be(WindowEnd);
            return Task.FromResult(Payments);
        }
    }

    private sealed class FakeStripeReconciliationAlerter : IStripeReconciliationAlerter
    {
        public List<StripeReconciliationReportDto> Reports { get; } = [];

        public Task AlertAsync(
            StripeReconciliationReportDto report,
            CancellationToken ct = default)
        {
            Reports.Add(report);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCount++;
            return Task.FromResult(0);
        }

        public async Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken ct = default)
        {
            await operation(ct);
        }

        public async Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> operation,
            IsolationLevel isolationLevel,
            CancellationToken ct = default)
        {
            await operation(ct);
        }

        public async Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            IsolationLevel isolationLevel,
            CancellationToken ct = default)
        {
            return await operation(ct);
        }

        public async Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            IsolationLevel isolationLevel,
            int maxAttempts,
            CancellationToken ct = default)
        {
            return await operation(ct);
        }
    }
}
