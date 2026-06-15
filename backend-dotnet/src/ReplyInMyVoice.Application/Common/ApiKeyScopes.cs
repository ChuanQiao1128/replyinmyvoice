using System.Text.Json;

namespace ReplyInMyVoice.Application.Common;

public static class ApiKeyScopes
{
    public const string Rewrite = "rewrite";

    public static IReadOnlySet<string> Parse(string? scopeJson)
    {
        if (string.IsNullOrWhiteSpace(scopeJson))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var values = JsonSerializer.Deserialize<string?[]>(scopeJson);
            if (values is null || values.Length == 0)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var scopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    scopes.Add(value.Trim());
                }
            }

            return scopes;
        }
        catch (JsonException)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static bool Allows(IReadOnlySet<string>? granted, string required)
    {
        if (granted is null || granted.Count == 0)
        {
            return true;
        }

        return granted.Any(scope => string.Equals(scope, required, StringComparison.OrdinalIgnoreCase));
    }
}
