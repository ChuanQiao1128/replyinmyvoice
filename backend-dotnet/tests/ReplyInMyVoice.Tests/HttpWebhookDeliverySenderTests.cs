using FluentAssertions;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class HttpWebhookDeliverySenderTests
{
    [Fact]
    public async Task SendAsync_rejects_disallowed_url_before_http_send()
    {
        using var httpClient = new HttpClient(new ThrowingHandler());
        var sender = new HttpWebhookDeliverySender(httpClient);
        var request = new WebhookSendRequest(
            "https://127.0.0.1/rewrite",
            "{}",
            "sha256=test",
            "1780696800",
            Guid.NewGuid(),
            Guid.NewGuid());

        var act = () => sender.SendAsync(request, CancellationToken.None);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Webhook URL is not allowed.");
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("HTTP should not be called for rejected webhook URLs.");
    }
}
