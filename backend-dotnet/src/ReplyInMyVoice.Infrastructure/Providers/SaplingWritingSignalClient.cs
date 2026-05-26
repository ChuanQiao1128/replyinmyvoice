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
            var aiLikePercent = MeanAiLikePercent(rawAiLikePercent, sentenceScores);

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

    // Displayed/gated AI-like score = MEAN of scorable per-sentence scores (was the median).
    // Sapling's per-sentence scores are near-binary (a sentence reads ~0% or ~100%), so for
    // the short emails this product handles the MEDIAN collapsed to the floor (0%) almost
    // regardless of content: on the 2026-05-26 100-case corpus the median moved in only
    // 15/100 draft->rewrite pairs and pinned 99/100 rewrites to 0%, making the before/after
    // meaningless and the naturalness gate a no-op. Sapling's document score is the opposite
    // failure (saturates to 100% on any formal email — 84/100 rewrites, 100/100 drafts), so it
    // is unusable as a gate. The MEAN keeps a live gradient (moved in 85/100 pairs, net drop
    // 14.7->7.4) and, unlike the old raw-overall score, one boilerplate sentence cannot pin a
    // multi-sentence email to 100% (it contributes only 100/n). List markers and sub-two-word
    // fragments stay excluded so segmentation artifacts do not skew the mean.
    private static int MeanAiLikePercent(int rawAiLikePercent, IReadOnlyList<SentenceSignal> sentenceScores)
    {
        var scorable = sentenceScores
            .Where(sentence => IsScorableSentence(sentence.Sentence))
            .Select(sentence => sentence.AiLikePercent)
            .ToArray();

        return scorable.Length == 0
            ? rawAiLikePercent
            : (int)Math.Round(scorable.Average(), MidpointRounding.AwayFromZero);
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
