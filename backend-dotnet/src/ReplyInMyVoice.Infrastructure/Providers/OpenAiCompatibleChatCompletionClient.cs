using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ReplyInMyVoice.Infrastructure.Providers;

// Minimal OpenAI-compatible (DeepSeek-routed) chat-completion client for prompt-only judges such as the
// FidelityJudge. Separate from OpenAiCompatibleRewriteModelClient, which is rewrite-specific (parses
// {rewrittenText}); this returns the raw assistant message content for the caller to parse. Returns null
// on any HTTP/parse/timeout failure so the caller (the fail-closed FidelityJudge) treats it as a judge
// error, never a silent pass.
public sealed class OpenAiCompatibleChatCompletionClient(
    HttpClient httpClient,
    string apiKey,
    string model,
    string baseUrl,
    TimeSpan timeout)
{
    public async Task<string?> CompleteAsync(
        string system,
        string user,
        int maxTokens,
        double temperature,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["temperature"] = temperature,
            ["response_format"] = new { type = "json_object" },
            ["max_tokens"] = maxTokens,
            ["messages"] = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user },
            },
        };
        if (IsDeepSeekBaseUrl(baseUrl))
        {
            payload["thinking"] = new { type = "disabled" };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri(baseUrl));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, timeoutCts.Token);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
    }

    private static bool IsDeepSeekBaseUrl(string value)
    {
        try
        {
            var host = new Uri(value).Host.ToLowerInvariant();
            return host == "api.deepseek.com" || host.EndsWith(".deepseek.com", StringComparison.Ordinal);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static Uri BuildChatCompletionsUri(string configuredBaseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? "https://api.openai.com/v1"
            : configuredBaseUrl.Trim().TrimEnd('/');

        if (normalized.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(normalized);
        }

        return normalized.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? new Uri($"{normalized}/chat/completions")
            : new Uri($"{normalized}/v1/chat/completions");
    }
}
