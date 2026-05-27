using System.Text.RegularExpressions;

namespace ReplyInMyVoice.Domain.Quality;

// Per-user voice as a Domain VALUE OBJECT (the EF entity + Azure SQL persistence is a later phase that
// needs data-module-review first). Holds the deterministic stats; CommonPhrases is filled by a bounded
// LLM summarizer later (kept empty here so the extractor stays pure/testable).
public sealed record VoiceProfile(
    string? OpeningStyle,        // e.g. "Hi {name}," | "Dear {name}," | "Hi," | null (no consistent greeting)
    string? ClosingStyle,        // e.g. "Thanks," | "Best," | "Best regards," | null
    int? MedianSentenceWords,
    double ContractionRate,      // contractions / total words, 0..1
    string PolitenessLevel,      // low | medium | high
    IReadOnlyList<string> CommonPhrases,
    int SampleCount)
{
    public static VoiceProfile Empty { get; } =
        new(null, null, null, 0d, "medium", Array.Empty<string>(), 0);

    // Floor below which VoiceEdit degrades to MinimalHumanEdit (too little signal to mimic a voice).
    public bool HasEnoughSamples => SampleCount >= 2;
}

// Deterministically derives a VoiceProfile from a user's past email samples: greeting/sign-off pattern,
// median sentence length, contraction rate, and politeness level. Pure (no LLM, no I/O) and unit-testable;
// the LLM common/avoided-phrase summary and persistence are layered separately.
public static class VoiceProfileExtractor
{
    private static readonly Regex GreetingRegex = new(
        @"^\s*(hi|hello|hey|dear|good\s+morning|good\s+afternoon)\b\s*,?\s*([A-Z][a-zA-Z]+)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ContractionRegex = new(
        @"\b\p{L}+['’](?:s|t|re|ve|ll|d|m)\b|\b\p{L}+n['’]t\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SentenceRegex = new(@"[^.!?\n]+[.!?]", RegexOptions.Compiled);
    private static readonly Regex WordRegex = new(@"\b[\p{L}\p{N}'’-]+\b", RegexOptions.Compiled);

    // Longest-first so "best regards" wins over "best".
    private static readonly string[] SignoffCandidates =
    {
        "best regards", "kind regards", "warm regards", "best wishes", "many thanks", "thank you",
        "talk soon", "speak soon", "sincerely", "regards", "cheers", "thanks", "best",
    };

    private static readonly string[] PolitenessMarkers =
    {
        "please", "thank you", "thanks", "appreciate", "grateful", "would you mind", "if you could",
        "sorry", "apolog", "kindly", "much appreciated", "i hope this", "at your convenience",
    };

    public static VoiceProfile Extract(IReadOnlyList<string> samples)
    {
        var clean = (samples ?? Array.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        if (clean.Count == 0)
        {
            return VoiceProfile.Empty;
        }

        var opening = MostCommon(clean.Select(DetectOpening).Where(o => o is not null).Select(o => o!).ToList());
        var closing = MostCommon(clean.Select(DetectClosing).Where(c => c is not null).Select(c => c!).ToList());

        var sentenceLengths = clean.SelectMany(SentenceWordCounts).Where(n => n > 0).OrderBy(n => n).ToList();
        int? medianWords = sentenceLengths.Count == 0 ? null : Median(sentenceLengths);

        var totalWords = clean.Sum(s => WordRegex.Matches(s).Count);
        var contractions = clean.Sum(s => ContractionRegex.Matches(s).Count);
        var contractionRate = totalWords == 0 ? 0d : Math.Round((double)contractions / totalWords, 4);

        return new VoiceProfile(
            opening,
            closing,
            medianWords,
            contractionRate,
            PolitenessLevelOf(clean),
            Array.Empty<string>(),
            clean.Count);
    }

    private static string? DetectOpening(string sample)
    {
        var firstLine = sample.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
        if (firstLine is null)
        {
            return null;
        }

        var match = GreetingRegex.Match(firstLine);
        if (!match.Success)
        {
            return null;
        }

        var greeting = Capitalize(Regex.Replace(match.Groups[1].Value.Trim(), @"\s+", " "));
        return match.Groups[2].Success ? $"{greeting} {{name}}," : $"{greeting},";
    }

    private static string? DetectClosing(string sample)
    {
        var lines = sample.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        // Sign-off is usually one of the last two non-empty lines (last line is often the name).
        foreach (var line in lines.AsEnumerable().Reverse().Take(2))
        {
            var lower = line.ToLowerInvariant().TrimEnd('.', ',', '!', ' ');
            var hit = SignoffCandidates.FirstOrDefault(c => lower == c || lower.StartsWith(c + " ", StringComparison.Ordinal));
            if (hit is not null)
            {
                return Capitalize(hit) + ",";
            }
        }

        return null;
    }

    private static IEnumerable<int> SentenceWordCounts(string sample) =>
        SentenceRegex.Matches(sample).Select(m => WordRegex.Matches(m.Value).Count);

    private static string PolitenessLevelOf(IReadOnlyList<string> samples)
    {
        var joined = string.Join("\n", samples).ToLowerInvariant();
        var hits = PolitenessMarkers.Sum(marker => CountOccurrences(joined, marker));
        var perSample = (double)hits / samples.Count;
        return perSample >= 2.0 ? "high" : perSample >= 0.5 ? "medium" : "low";
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
        {
            count++;
            i += needle.Length;
        }

        return count;
    }

    private static string? MostCommon(IReadOnlyList<string> values) =>
        values.Count == 0
            ? null
            : values.GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .First().Key;

    private static int Median(IReadOnlyList<int> sorted)
    {
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[mid]
            : (int)Math.Round((sorted[mid - 1] + sorted[mid]) / 2.0, MidpointRounding.AwayFromZero);
    }

    private static string Capitalize(string value) =>
        string.IsNullOrEmpty(value)
            ? value
            : string.Join(" ", value.Split(' ').Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..]));
}
