using System.Net;
using System.Net.Sockets;

namespace ReplyInMyVoice.Infrastructure.Services;

internal static class WebhookEndpointSafety
{
    private static readonly StringComparer HostComparer = StringComparer.OrdinalIgnoreCase;

    private static readonly HashSet<string> BlockedHostNames = new(HostComparer)
    {
        "localhost",
        "localhost.localdomain",
        "metadata",
        "metadata.google.internal",
        "metadata.azure.internal",
        "instance-data",
        "instance-data.ec2.internal",
    };

    public static bool TryNormalizeUrl(
        string? value,
        out string normalizedUrl,
        Func<string, IPAddress[]>? resolveHost = null)
    {
        normalizedUrl = string.Empty;
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 2048)
        {
            return false;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrWhiteSpace(uri.UserInfo) ||
            IsBlockedHostName(uri.IdnHost))
        {
            return false;
        }

        IPAddress[] addresses;
        try
        {
            addresses = (resolveHost ?? ResolveHost)(uri.IdnHost);
        }
        catch (SocketException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (!AreAllowedAddresses(addresses))
        {
            return false;
        }

        normalizedUrl = uri.ToString();
        return true;
    }

    public static bool AreAllowedAddresses(IReadOnlyCollection<IPAddress> addresses) =>
        addresses.Count > 0 && addresses.All(IsAllowedAddress);

    public static bool IsAllowedAddress(IPAddress address)
    {
        var normalized = address.IsIPv4MappedToIPv6
            ? address.MapToIPv4()
            : address;

        if (IPAddress.IsLoopback(normalized) ||
            IPAddress.Any.Equals(normalized) ||
            IPAddress.IPv6Any.Equals(normalized) ||
            IPAddress.Broadcast.Equals(normalized))
        {
            return false;
        }

        return normalized.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsAllowedIPv4(normalized),
            AddressFamily.InterNetworkV6 => IsAllowedIPv6(normalized),
            _ => false,
        };
    }

    public static IPAddress[] ResolveHost(string host)
    {
        if (IPAddress.TryParse(host, out var address))
        {
            return [address];
        }

        return Dns.GetHostAddresses(host);
    }

    public static async Task<IPAddress[]> ResolveHostAsync(string host, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out var address))
        {
            return [address];
        }

        return await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsBlockedHostName(string host)
    {
        var normalized = host.TrimEnd('.');
        return BlockedHostNames.Contains(normalized) ||
            normalized.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedIPv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes[0] == 10 ||
            bytes[0] == 127 ||
            (bytes[0] == 169 && bytes[1] == 254) ||
            (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
            (bytes[0] == 192 && bytes[1] == 168) ||
            (bytes[0] == 100 && bytes[1] is >= 64 and <= 127))
        {
            return false;
        }

        return true;
    }

    private static bool IsAllowedIPv6(IPAddress address)
    {
        if (address.IsIPv6LinkLocal ||
            address.IsIPv6SiteLocal ||
            address.IsIPv6Multicast)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return (bytes[0] & 0xfe) != 0xfc;
    }
}
