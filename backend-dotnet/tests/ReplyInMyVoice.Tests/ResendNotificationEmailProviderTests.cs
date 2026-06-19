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

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task SendAsync_throws_on_5xx_for_transient_retry(HttpStatusCode statusCode)
    {
        var handler = new CapturingHandler(statusCode);
        var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<RetryableNotificationException>(
            () => provider.SendAsync(CreateEmail()));

        exception.Message.Should().Contain(((int)statusCode).ToString());
    }

    [Fact]
    public async Task SendAsync_throws_on_429_rate_limit_for_retry()
    {
        var handler = new CapturingHandler(HttpStatusCode.TooManyRequests);
        var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<RetryableNotificationException>(
            () => provider.SendAsync(CreateEmail()));

        exception.Message.Should().Contain("429");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task SendAsync_returns_skipped_on_4xx_no_retry(HttpStatusCode statusCode)
    {
        var handler = new CapturingHandler(statusCode);
        var provider = CreateProvider(handler);

        var result = await provider.SendAsync(CreateEmail());

        result.Sent.Should().BeFalse();
        result.Provider.Should().Be("resend");
        result.Reason.Should().Be("provider_error");
    }

    [Fact]
    public async Task SendAsync_throws_on_network_timeout_for_retry()
    {
        var timeout = new TaskCanceledException("Resend request timed out.");
        var handler = new CapturingHandler((_, _) => Task.FromException<HttpResponseMessage>(timeout));
        var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<RetryableNotificationException>(
            () => provider.SendAsync(CreateEmail()));

        exception.InnerException.Should().BeSameAs(timeout);
    }

    [Fact]
    public async Task SendAsync_preserves_cancellation_token_cancel()
    {
        using var cts = new CancellationTokenSource();
        var handler = new CapturingHandler((_, ct) =>
        {
            cts.Cancel();
            return Task.FromException<HttpResponseMessage>(new OperationCanceledException(ct));
        });
        var provider = CreateProvider(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.SendAsync(CreateEmail(), cts.Token));
    }

    private static ResendNotificationEmailProvider CreateProvider(CapturingHandler handler) =>
        new(
            new HttpClient(handler),
            "resend-test-key",
            "Reply In My Voice <info@timeawake.co.nz>",
            replyToEmail: null,
            NullLogger<ResendNotificationEmailProvider>.Instance);

    private static NotificationEmail CreateEmail() =>
        new(
            "failed-payment",
            new NotificationRecipient("customer@example.com"),
            "Subject",
            "Plain body",
            "<p>Html body</p>",
            IdempotencyKey: "outbox-xyz");

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync;

        public CapturingHandler()
            : this(HttpStatusCode.OK)
        {
        }

        public CapturingHandler(HttpStatusCode statusCode)
            : this((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("{\"id\":\"em_123\"}"),
            }))
        {
        }

        public CapturingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        {
            _sendAsync = sendAsync;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return _sendAsync(request, cancellationToken);
        }
    }
}
