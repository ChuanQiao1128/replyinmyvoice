using System.Net;
using System.Net.Sockets;

namespace ReplyInMyVoice.Infrastructure.Services;

internal static class WebhookHttpClientFactory
{
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan OverallTimeout = TimeSpan.FromSeconds(15);

    public static SocketsHttpHandler CreateHandler() =>
        new()
        {
            AllowAutoRedirect = false,
            ConnectTimeout = ConnectTimeout,
            ConnectCallback = ConnectAsync,
        };

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await WebhookEndpointSafety.ResolveHostAsync(
            context.DnsEndPoint.Host,
            cancellationToken);
        if (!WebhookEndpointSafety.AreAllowedAddresses(addresses))
        {
            throw new HttpRequestException("Webhook URL is not allowed.");
        }

        Exception? lastException = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(
                    new IPEndPoint(address, context.DnsEndPoint.Port),
                    cancellationToken).ConfigureAwait(false);

                if (socket.RemoteEndPoint is IPEndPoint remoteEndPoint &&
                    !WebhookEndpointSafety.IsAllowedAddress(remoteEndPoint.Address))
                {
                    throw new HttpRequestException("Webhook URL is not allowed.");
                }

                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException ex)
            {
                lastException = ex;
                socket.Dispose();
            }
        }

        throw new HttpRequestException("Webhook connection failed.", lastException);
    }
}
