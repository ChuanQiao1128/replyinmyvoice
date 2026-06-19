using System.Collections;

namespace ReplyInMyVoice.Application.Common;

public static class ApiKeyPepperVersions
{
    public const int LegacyVersion = 1;
    private const string LegacyPepperName = "API_KEY_PEPPER";
    private const string VersionedPepperPrefix = "API_KEY_PEPPER_V";

    public static int GetCurrentPepperVersion()
    {
        var highestVersion = LegacyVersion;

        foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            if (variable.Key is not string name ||
                variable.Value is not string value ||
                !HasPepperValue(value) ||
                !TryParseVersionedPepperName(name, out var version))
            {
                continue;
            }

            highestVersion = Math.Max(highestVersion, version);
        }

        return highestVersion;
    }

    public static bool HasPepperForVersion(int version) =>
        HasPepperValue(GetPepperForVersion(version));

    public static string? GetPepperForVersion(int version)
    {
        if (version < LegacyVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Pepper version must be greater than zero.");
        }

        var versionedPepper = Environment.GetEnvironmentVariable(BuildVersionedPepperName(version));
        if (HasPepperValue(versionedPepper))
        {
            return versionedPepper;
        }

        return version == LegacyVersion
            ? Environment.GetEnvironmentVariable(LegacyPepperName)
            : null;
    }

    public static IReadOnlyList<int> GetConfiguredVersionedPepperVersions(IConfigurationReader configuration)
    {
        var versions = new SortedSet<int>();
        foreach (var key in configuration.GetKeys())
        {
            if (TryParseVersionedPepperName(key, out var version) &&
                HasPepperValue(configuration[key]))
            {
                versions.Add(version);
            }
        }

        return versions.ToArray();
    }

    public static string BuildVersionedPepperName(int version) =>
        string.Concat(VersionedPepperPrefix, version.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static bool TryParseVersionedPepperName(string name, out int version)
    {
        version = 0;
        return name.StartsWith(VersionedPepperPrefix, StringComparison.Ordinal) &&
            int.TryParse(
                name[VersionedPepperPrefix.Length..],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out version) &&
            version >= LegacyVersion;
    }

    private static bool HasPepperValue(string? value) =>
        !string.IsNullOrWhiteSpace(value);
}

public interface IConfigurationReader
{
    string? this[string key] { get; }

    IEnumerable<string> GetKeys();
}
