using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ReplyInMyVoice.Infrastructure.Services;

public static class ApiKeyHashing
{
    private static int s_missingPepperWarningLogged;
    private static ILogger<ApiKeyService>? s_logger;

    public static string ComputeHash(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var pepper = Environment.GetEnvironmentVariable("API_KEY_PEPPER");
        if (string.IsNullOrWhiteSpace(pepper) && IsProductionRuntimeEnvironment())
        {
            throw new InvalidOperationException("API key hashing requires API_KEY_PEPPER in Production.");
        }

        if (string.IsNullOrWhiteSpace(pepper) &&
            Interlocked.Exchange(ref s_missingPepperWarningLogged, 1) == 0)
        {
            const string warning = "API_KEY_PEPPER is not set; API key hashes are being computed without the configured pepper.";
            if (s_logger is not null)
            {
                s_logger.LogWarning("{Warning}", warning);
            }
            else
            {
                Trace.TraceWarning(warning);
            }
        }

        // Keep this formula stable for existing keys; a future keyed-HMAC migration needs
        // explicit key versioning and rotation.
        var material = string.IsNullOrWhiteSpace(pepper)
            ? plaintext
            : string.Concat(pepper, plaintext);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    internal static void UseLogger(ILogger<ApiKeyService> logger) =>
        s_logger = logger;

    private static bool IsProductionRuntimeEnvironment() =>
        IsProductionEnvironmentName(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")) ||
        IsProductionEnvironmentName(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")) ||
        IsProductionEnvironmentName(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT"));

    private static bool IsProductionEnvironmentName(string? environmentName) =>
        string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);
}
