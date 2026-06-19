using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Infrastructure.Services;

public static class ApiKeyHashing
{
    private static int s_missingPepperWarningLogged;

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
            const string warning = "API_KEY_PEPPER is not set; API key hashes are being computed without the configured pepper.";
            Trace.TraceWarning(warning);
        }

        // Keep this formula stable for existing keys.
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

    private static bool IsProductionRuntimeEnvironment() =>
        IsProductionEnvironmentName(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")) ||
        IsProductionEnvironmentName(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")) ||
        IsProductionEnvironmentName(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT"));

    private static bool IsProductionEnvironmentName(string? environmentName) =>
        string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);
}
