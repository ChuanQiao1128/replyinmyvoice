using System.Text.Json;
using FluentAssertions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Services;
using AppStripeReconciliationAlerter = ReplyInMyVoice.Application.Abstractions.IStripeReconciliationAlerter;

namespace ReplyInMyVoice.Tests.Application;

public sealed class StripeReconciliationAlertOutboxHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void MessageType_is_pinned()
    {
        var handler = new StripeReconciliationAlertOutboxMessageHandler(new FakeAlerter());

        handler.MessageType.Should().Be("StripeReconciliationAlertRequested");
    }

    [Fact]
    public async Task Handle_deserializes_report_and_calls_alerter()
    {
        var report = StripeReconciliationReportDto.Create(
            DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-06-02T00:00:00Z"),
            DateTimeOffset.Parse("2026-06-02T00:05:00Z"),
            stripePaymentCount: 2,
            purchaseGrantCount: 1,
            discrepancies:
            [
                new StripeReconciliationDiscrepancyDto(
                    StripeReconciliationDiscrepancyKindDto.PaidButNoGrant,
                    "pi_alert",
                    null,
                    500,
                    null,
                    "nzd",
                    null,
                    DateTimeOffset.Parse("2026-06-01T03:00:00Z"),
                    null),
            ],
            autoGrants:
            [
                new StripeReconciliationAutoGrantDto(
                    "pi_alert",
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    10,
                    "quick_pack"),
            ],
            manualReview:
            [
                new StripeReconciliationManualReviewDto("pi_later", "payment_too_recent"),
            ],
            subscriptionMismatches:
            [
                new StripeSubscriptionDiscrepancyDto(
                    "local_active_stripe_not",
                    "sub_alert",
                    "cus_alert",
                    Guid.NewGuid(),
                    null,
                    "Active"),
            ],
            autoGrantSkippedCount: 0);
        var alerter = new FakeAlerter();
        var handler = new StripeReconciliationAlertOutboxMessageHandler(alerter);
        var message = new OutboxMessage
        {
            MessageType = handler.MessageType,
            PayloadJson = JsonSerializer.Serialize(report, JsonOptions),
        };

        await handler.HandleAsync(message);

        var delivered = alerter.Reports.Should().ContainSingle().Subject;
        delivered.PaidButNoGrantCount.Should().Be(1);
        delivered.AutoGrantedCount.Should().Be(1);
        delivered.ManualReviewCount.Should().Be(1);
        delivered.SubscriptionMismatchCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_throws_on_malformed_payload()
    {
        var handler = new StripeReconciliationAlertOutboxMessageHandler(new FakeAlerter());
        var message = new OutboxMessage
        {
            MessageType = handler.MessageType,
            PayloadJson = "{",
        };

        var act = async () => await handler.HandleAsync(message);

        await act.Should().ThrowAsync<JsonException>();
    }

    private sealed class FakeAlerter : AppStripeReconciliationAlerter
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
}
