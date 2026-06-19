using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace ReplyInMyVoice.Application.Common;

public static class ApiKeyCredential
{
    private const string LiveKeyPrefix = "rmv_live_";
    private const string TestKeyPrefix = "rmv_test_";
    private const string Base62Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private static int s_missingPepperWarningLogged;
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

    public static string ComputeHash(string plaintext)
    {
        return ComputeHashWithVersion(plaintext, ApiKeyPepperVersions.LegacyVersion);
    }

    public static string ComputeHashWithVersion(string plaintext, int version)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var pepper = GetPepperForVersion(version);
        if (string.IsNullOrWhiteSpace(pepper) && IsProductionRuntimeEnvironment())
        {
            throw new InvalidOperationException(
                $"API key hashing requires {PepperSettingName(version)} in Production.");
        }

        if (string.IsNullOrWhiteSpace(pepper) &&
            Interlocked.Exchange(ref s_missingPepperWarningLogged, 1) == 0)
        {
            Trace.TraceWarning("API_KEY_PEPPER is not set; API key hashes are being computed without the configured pepper.");
        }

        var material = string.IsNullOrWhiteSpace(pepper)
            ? plaintext
            : string.Concat(pepper, plaintext);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string? GetPepperForVersion(int version) =>
        ApiKeyPepperVersions.GetPepperForVersion(version);

    private static string PepperSettingName(int version) =>
        version == ApiKeyPepperVersions.LegacyVersion
            ? "API_KEY_PEPPER"
            : ApiKeyPepperVersions.BuildVersionedPepperName(version);

    public static string GeneratePlaintext(bool isTest)
    {
        Span<byte> randomBytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        return (isTest ? TestKeyPrefix : LiveKeyPrefix) + ToBase62(randomBytes);
    }

    public static string GenerateWebhookSecret()
    {
        Span<byte> randomBytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        return Convert.ToHexString(randomBytes).ToLowerInvariant();
    }

    public static string MaskKey(string? last4, bool isTest) =>
        string.Concat(isTest ? TestKeyPrefix : LiveKeyPrefix, "\u2022\u2022\u2022\u2022", last4 ?? string.Empty);

    public static bool TryNormalizeWebhookUrl(string? value, out string normalizedUrl) =>
        TryNormalizeWebhookUrl(value, out normalizedUrl, ResolveHost);

    public static bool TryNormalizeWebhookUrl(
        string? value,
        out string normalizedUrl,
        Func<string, IPAddress[]> resolveHost)
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
            addresses = resolveHost(uri.IdnHost);
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

    private static bool AreAllowedAddresses(IReadOnlyCollection<IPAddress> addresses) =>
        addresses.Count > 0 && addresses.All(IsAllowedAddress);

    private static bool IsAllowedAddress(IPAddress address)
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

    private static IPAddress[] ResolveHost(string host)
    {
        if (IPAddress.TryParse(host, out var address))
        {
            return [address];
        }

        return Dns.GetHostAddresses(host);
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
        return bytes[0] != 10 &&
            bytes[0] != 127 &&
            (bytes[0] != 169 || bytes[1] != 254) &&
            (bytes[0] != 172 || bytes[1] is < 16 or > 31) &&
            (bytes[0] != 192 || bytes[1] != 168) &&
            (bytes[0] != 100 || bytes[1] is < 64 or > 127);
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

    private static bool IsProductionRuntimeEnvironment() =>
        IsProductionEnvironmentName(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")) ||
        IsProductionEnvironmentName(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")) ||
        IsProductionEnvironmentName(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT"));

    private static bool IsProductionEnvironmentName(string? environmentName) =>
        string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);

    private static string ToBase62(ReadOnlySpan<byte> bytes)
    {
        var value = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        if (value.IsZero)
        {
            return "0";
        }

        Span<char> buffer = stackalloc char[64];
        var position = buffer.Length;
        while (value > BigInteger.Zero)
        {
            value = BigInteger.DivRem(value, 62, out var remainder);
            buffer[--position] = Base62Alphabet[(int)remainder];
        }

        return new string(buffer[position..]);
    }
}
