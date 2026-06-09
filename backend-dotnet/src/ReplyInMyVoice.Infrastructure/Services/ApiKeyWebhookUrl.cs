using System.Net;

namespace ReplyInMyVoice.Infrastructure.Services;

public static class ApiKeyWebhookUrl
{
    public static bool TryNormalizeWebhookUrl(string? value, out string normalizedUrl) =>
        WebhookEndpointSafety.TryNormalizeUrl(value, out normalizedUrl);

    internal static bool TryNormalizeWebhookUrl(
        string? value,
        out string normalizedUrl,
        Func<string, IPAddress[]> resolveHost) =>
        WebhookEndpointSafety.TryNormalizeUrl(value, out normalizedUrl, resolveHost);
}
