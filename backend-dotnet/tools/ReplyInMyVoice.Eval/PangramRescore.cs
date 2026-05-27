using ReplyInMyVoice.Infrastructure.Providers;

// Minimal "score one fixed text on Pangram" mode (PANGRAM_RESCORE_FILE=<path>). EVAL-ONLY.
// Re-scores the SAME text N times (N = PANGRAM_MAX_CALLS, default 1) to isolate Pangram's own
// measurement noise from pipeline nondeterminism. Used to check whether a single low reading
// (e.g. case 001 at 28) reproduces or was a noise-low. Budget-frugal: exactly N Pangram calls.
internal static class PangramRescore
{
    public static async Task<int> RunAsync(string path)
    {
        var key = Environment.GetEnvironmentVariable("PANGRAM_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            Console.Error.WriteLine("PANGRAM_RESCORE_FILE: missing PANGRAM_API_KEY.");
            return 2;
        }

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"PANGRAM_RESCORE_FILE: file not found: {path}");
            return 2;
        }

        var text = (await File.ReadAllTextAsync(path)).Trim();
        var n = int.TryParse(Environment.GetEnvironmentVariable("PANGRAM_MAX_CALLS"), out var x) && x > 0 ? x : 1;

        using var http = new HttpClient();
        var pangram = new PangramWritingSignalClient(http, key, TimeSpan.FromSeconds(45));
        Console.WriteLine($"Re-scoring {path} ({text.Length} chars) {n}x on Pangram (lower = more AI-like):");
        for (var i = 0; i < n; i++)
        {
            var r = await pangram.MeasureAsync(text, CancellationToken.None);
            Console.WriteLine($"  run {i + 1}: {(r.Available ? r.AiLikePercent + "% AI" : "unavailable (" + r.ErrorCode + ")")}");
        }

        return 0;
    }
}
