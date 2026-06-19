using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ReplyInMyVoice.Infrastructure.Notifications;

namespace ReplyInMyVoice.Tests;

public sealed class ResendNotificationEmailProviderTests
{
    [Fact]
    public async Task SendAsync_sets_idempotency_key_header_when_present()
    {
        var handler = new CapturingHandler();
        var provider = CreateProvider(handler);

        var result = await provider.SendAsync(new NotificationEmail(
            "failed-payment",
            new NotificationRecipient("customer@example.com"),
            "Subject",
            "Plain body",
            "<p>Html body</p>",
            IdempotencyKey: "outbox-xyz"));

        result.Sent.Should().BeTrue();
        handler.LastRequest!.Headers.GetValues("Idempotency-Key").Should().Equal("outbox-xyz");
    }

    [Fact]
    public async Task SendAsync_omits_idempotency_key_header_when_absent()
    {
        var handler = new CapturingHandler();
        var provider = CreateProvider(handler);

        await provider.SendAsync(new NotificationEmail(
            "failed-payment",
            new NotificationRecipient("customer@example.com"),
            "Subject",
            "Plain body",
            "<p>Html body</p>"));

        handler.LastRequest!.Headers.Contains("Idempotency-Key").Should().BeFalse();
    }

    private static ResendNotificationEmailProvider CreateProvider(CapturingHandler handler) =>
        new(
            new HttpClient(handler),
            "resend-test-key",
            "Reply In My Voice <info@timeawake.co.nz>",
            replyToEmail: null,
            NullLogger<ResendNotificationEmailProvider>.Instance);

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"em_123\"}"),
            });
        }
    }
}
