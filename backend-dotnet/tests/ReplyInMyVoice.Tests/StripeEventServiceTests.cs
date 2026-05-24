using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class StripeEventServiceTests
{
    [Fact]
    public async Task TryMarkProcessedAsync_returns_false_for_duplicate_event_id()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var service = new StripeEventService(fixture.CreateContext);

        var first = await service.TryMarkProcessedAsync("evt_123", "customer.subscription.updated", DateTimeOffset.UtcNow);
        var second = await service.TryMarkProcessedAsync("evt_123", "customer.subscription.updated", DateTimeOffset.UtcNow);

        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_failed_sync_can_be_retried_and_processed()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new StripeEventService(fixture.CreateContext);

        var act = async () => await service.ProcessWebhookEventAsync(
            "evt_retry",
            "checkout.session.completed",
            "{not-valid-json",
            DateTimeOffset.Parse("2026-05-20T00:00:00Z"));
        await act.Should().ThrowAsync<System.Text.Json.JsonException>();

        var retry = await service.ProcessWebhookEventAsync(
            "evt_retry",
            "checkout.session.completed",
            $$"""
            {
              "id": "evt_retry",
              "type": "checkout.session.completed",
              "data": {
                "object": {
                  "client_reference_id": "{{user.ExternalAuthUserId}}",
                  "customer": "cus_retry",
                  "subscription": "sub_retry"
                }
              }
            }
            """,
            DateTimeOffset.Parse("2026-05-20T00:00:10Z"));

        retry.Should().BeTrue();
        await using var db = fixture.CreateContext();
        var storedEvent = await db.StripeEvents.SingleAsync(x => x.EventId == "evt_retry");
        storedEvent.Status.Should().Be(StripeEventStatus.Processed);
        storedEvent.AttemptCount.Should().Be(2);
        storedEvent.LastError.Should().BeNull();

        var updatedUser = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.StripeCustomerId.Should().Be("cus_retry");
        updatedUser.StripeSubscriptionId.Should().Be("sub_retry");
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_paid_checkout_session_grants_rewrite_credit_once_per_event()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new StripeEventService(fixture.CreateContext);
        var now = DateTimeOffset.Parse("2026-05-25T00:00:00Z");

        var first = await service.ProcessWebhookEventAsync(
            "evt_paid_pack",
            "checkout.session.completed",
            $$"""
            {
              "id": "evt_paid_pack",
              "type": "checkout.session.completed",
              "data": {
                "object": {
                  "client_reference_id": "{{user.ExternalAuthUserId}}",
                  "customer": "cus_pack",
                  "mode": "payment",
                  "payment_status": "paid",
                  "metadata": {
                    "sku": "value_pack",
                    "rewrites": "30",
                    "externalAuthUserId": "{{user.ExternalAuthUserId}}"
                  }
                }
              }
            }
            """,
            now);
        var second = await service.ProcessWebhookEventAsync(
            "evt_paid_pack",
            "checkout.session.completed",
            "{}",
            now.AddSeconds(1));

        first.Should().BeTrue();
        second.Should().BeFalse();

        await using var db = fixture.CreateContext();
        var credit = await db.RewriteCredits.SingleAsync();
        credit.UserId.Should().Be(user.Id);
        credit.Source.Should().Be("PURCHASE");
        credit.AmountGranted.Should().Be(30);
        credit.AmountConsumed.Should().Be(0);
        credit.StripeEventId.Should().Be("evt_paid_pack");
        credit.GrantedAt.Should().Be(now);
        credit.ExpiresAt.Should().NotBeNull();
        credit.ExpiresAt!.Value.Should().BeCloseTo(now.AddDays(90), TimeSpan.FromSeconds(1));

        var updatedUser = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
        updatedUser.StripeCustomerId.Should().Be("cus_pack");
    }
}
