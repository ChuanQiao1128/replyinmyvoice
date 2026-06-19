namespace ReplyInMyVoice.Tests.ApiKeyPepperRotation;

internal sealed class PepperEnvironmentScope : IDisposable
{
    private static readonly string[] Names =
    [
        "API_KEY_PEPPER",
        "API_KEY_PEPPER_V1",
        "API_KEY_PEPPER_V2",
        "API_KEY_PEPPER_V3",
        "DOTNET_ENVIRONMENT",
        "ASPNETCORE_ENVIRONMENT",
        "AZURE_FUNCTIONS_ENVIRONMENT",
    ];

    private readonly Dictionary<string, string?> _previous = new(StringComparer.Ordinal);

    public PepperEnvironmentScope(IReadOnlyDictionary<string, string?> values)
    {
        foreach (var name in Names)
        {
            _previous[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }

        foreach (var (name, value) in values)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    public void Dispose()
    {
        foreach (var (name, value) in _previous)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
