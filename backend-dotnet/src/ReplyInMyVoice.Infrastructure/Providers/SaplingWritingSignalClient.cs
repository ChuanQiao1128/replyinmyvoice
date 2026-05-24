using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReplyInMyVoice.Infrastructure.Providers;

public sealed class SaplingWritingSignalClient(
    HttpClient httpClient,
    string apiKey,
    TimeSpan timeout) : IWritingSignalClient
{
    private static readonly Regex ListMarkerRegex = new(@"^(\d+[.)]|[a-z][.)]|[-*•·–—]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<WritingSignalResult> MeasureAsync(string text, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sapling.ai/api/v1/aidetect");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                key = apiKey,
                text,
                sent_scores = true,
                score_string = false,
            }),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var response = await httpClient.SendAsync(request, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new WritingSignalResult(false, null, $"sapling_http_{(int)response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("score", out var scoreElement) ||
                scoreElement.ValueKind is not JsonValueKind.Number ||
                !scoreElement.TryGetDouble(out var rawScore))
            {
                return new WritingSignalResult(false, null, "sapling_schema_changed");
            }

            var rawAiLikePercent = ScoreToPercent(rawScore);
            var sentenceScores = ExtractSentenceScores(doc.RootElement);
            var aiLikePercent = RobustAiLikePercent(rawAiLikePercent, sentenceScores);

            return new WritingSignalResult(true, aiLikePercent, null);
        }
        catch (OperationCanceledException)
        {
            return new WritingSignalResult(false, null, "sapling_timeout");
        }
        catch (JsonException)
        {
            return new WritingSignalResult(false, null, "sapling_json_parse_failed");
        }
        catch (HttpRequestException)
        {
            return new WritingSignalResult(false, null, "sapling_network_failed");
        }
    }

    private static IReadOnlyList<SentenceSignal> ExtractSentenceScores(JsonElement root)
    {
        if (!root.TryGetProperty("sentence_scores", out var sentenceScoresElement) ||
            sentenceScoresElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var scores = new List<SentenceSignal>();
        foreach (var item in sentenceScoresElement.EnumerateArray())
        {
            if (!item.TryGetProperty("sentence", out var sentenceElement) ||
                sentenceElement.ValueKind != JsonValueKind.String ||
                !item.TryGetProperty("score", out var scoreElement) ||
                scoreElement.ValueKind != JsonValueKind.Number ||
                !scoreElement.TryGetDouble(out var score))
            {
                continue;
            }

            scores.Add(new SentenceSignal(sentenceElement.GetString() ?? string.Empty, ScoreToPercent(score)));
        }

        return scores;
    }

    private static int ScoreToPercent(double score)
    {
        if (!double.IsFinite(score))
        {
            return 0;
        }

        return (int)Math.Round(Math.Clamp(score, 0, 1) * 100, MidpointRounding.AwayFromZero);
    }

    private static int RobustAiLikePercent(int rawAiLikePercent, IReadOnlyList<SentenceSignal> sentenceScores)
    {
        var scorable = sentenceScores
            .Where(sentence => IsScorableSentence(sentence.Sentence))
            .Select(sentence => sentence.AiLikePercent)
            .Order()
            .ToArray();

        if (scorable.Length == 0)
        {
            return rawAiLikePercent;
        }

        var mid = scorable.Length / 2;
        return scorable.Length % 2 == 1
            ? scorable[mid]
            : (int)Math.Round((scorable[mid - 1] + scorable[mid]) / 2.0, MidpointRounding.AwayFromZero);
    }

    private static bool IsScorableSentence(string sentence)
    {
        var trimmed = sentence.Trim();
        if (trimmed.Length == 0 || ListMarkerRegex.IsMatch(trimmed))
        {
            return false;
        }

        return Regex.Matches(trimmed, @"\b[\p{L}\p{N}'-]+\b").Count >= 2;
    }

    private sealed record SentenceSignal(string Sentence, int AiLikePercent);
}
