using System.Text;
using System.Text.Json;

namespace ReplyInMyVoice.Infrastructure.Providers;

// Pangram AI-detection signal provider (https://text.api.pangram.com/v3). A drop-in
// IWritingSignalClient alternative to Sapling, selected via WRITING_SIGNAL_PROVIDER=pangram.
// Pangram is detection-first and more accurate than Sapling, so its scores tend to run higher
// (harder to satisfy) — switching is a deliberate "stricter, more honest yardstick" trade, not
// a free win. The overall AI-like score is the mean of the per-window ai_assistance_score
// values (0..1 -> 0..100), mirroring the Sapling mean-of-sentences design and giving the
// refinement loop a continuous gradient; each window is also surfaced as a SentenceSignalScore
// so the loop can target the most AI-like segments. Falls back to fraction_ai when no windows.
public sealed class PangramWritingSignalClient(
    HttpClient httpClient,
    string apiKey,
    TimeSpan timeout) : IWritingSignalClient
{
    public async Task<WritingSignalResult> MeasureAsync(string text, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://text.api.pangram.com/v3");
        request.Headers.Add("x-api-key", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { text }),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var response = await httpClient.SendAsync(request, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new WritingSignalResult(false, null, $"pangram_http_{(int)response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var windows = ExtractWindows(root);
            int? aiLikePercent = windows.Count > 0
                ? (int)Math.Round(windows.Average(window => window.AiLikePercent), MidpointRounding.AwayFromZero)
                : FractionAiPercent(root);

            if (aiLikePercent is null)
            {
                return new WritingSignalResult(false, null, "pangram_schema_changed");
            }

            var perSentence = windows
                .Where(window => !string.IsNullOrWhiteSpace(window.Sentence))
                .Select(window => new SentenceSignalScore(window.Sentence, window.AiLikePercent))
                .ToArray();

            return new WritingSignalResult(true, aiLikePercent, null, perSentence);
        }
        catch (OperationCanceledException)
        {
            return new WritingSignalResult(false, null, "pangram_timeout");
        }
        catch (JsonException)
        {
            return new WritingSignalResult(false, null, "pangram_json_parse_failed");
        }
        catch (HttpRequestException)
        {
            return new WritingSignalResult(false, null, "pangram_network_failed");
        }
    }

    private static IReadOnlyList<SentenceSignalScore> ExtractWindows(JsonElement root)
    {
        if (!root.TryGetProperty("windows", out var windowsElement) ||
            windowsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var windows = new List<SentenceSignalScore>();
        foreach (var window in windowsElement.EnumerateArray())
        {
            if (!window.TryGetProperty("ai_assistance_score", out var scoreElement) ||
                scoreElement.ValueKind != JsonValueKind.Number ||
                !scoreElement.TryGetDouble(out var score))
            {
                continue;
            }

            var segment = window.TryGetProperty("text", out var textElement) &&
                textElement.ValueKind == JsonValueKind.String
                ? textElement.GetString() ?? string.Empty
                : string.Empty;

            windows.Add(new SentenceSignalScore(segment.Trim(), ScoreToPercent(score)));
        }

        return windows;
    }

    // Fallback when the response carries no per-window breakdown: the document-level portion
    // classified as fully AI-generated, expressed as a percent.
    private static int? FractionAiPercent(JsonElement root) =>
        root.TryGetProperty("fraction_ai", out var fractionAi) &&
        fractionAi.ValueKind is JsonValueKind.Number &&
        fractionAi.TryGetDouble(out var value)
            ? ScoreToPercent(value)
            : null;

    private static int ScoreToPercent(double score)
    {
        if (!double.IsFinite(score))
        {
            return 0;
        }

        return (int)Math.Round(Math.Clamp(score, 0, 1) * 100, MidpointRounding.AwayFromZero);
    }
}
