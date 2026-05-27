using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Infrastructure.Providers;

// Round-4 gating diagnostic (ORIGINAL_PANGRAM=1). EVAL-ONLY. Answers the round-4 doc's §9 load-bearing
// question BEFORE building the T5/T6/T8 sweep: is the user's ORIGINAL draft meaningfully less AI-like
// (lower Pangram) than the production T0 rewrite? If the originals are already ~99, "preserve the
// original surface" cannot lower the reading and the whole sweep is pointless. Scores original draft
// and a fresh T0 for each case (2 Pangram calls/case), prints the paired delta.
internal static class OriginalPangramDiagnostic
{
    public static async Task<int> RunAsync(
        FactReconstructRewriteProvider provider, IReadOnlyList<EvalCase> cases, EvalConfig config)
    {
        var pangramKey = Environment.GetEnvironmentVariable("PANGRAM_API_KEY");
        var maxCalls = int.TryParse(Environment.GetEnvironmentVariable("PANGRAM_MAX_CALLS"), out var m) && m > 0 ? m : 0;
        if (string.IsNullOrWhiteSpace(pangramKey) || maxCalls == 0)
        {
            Console.Error.WriteLine("ORIGINAL_PANGRAM: set PANGRAM_API_KEY and PANGRAM_MAX_CALLS>0.");
            return 2;
        }

        using var http = new HttpClient();
        var pangram = new PangramWritingSignalClient(http, pangramKey, TimeSpan.FromSeconds(45));
        var calls = 0;
        var rows = new List<(string Id, string Category, int Words, int? Original, int? T0, int? Delta)>();

        foreach (var sample in cases)
        {
            var draft = sample.InputDraft;
            var words = draft.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

            int? originalScore = null;
            if (calls < maxCalls)
            {
                originalScore = await Measure(pangram, draft);
                calls++;
            }

            int? t0Score = null;
            var t0 = await provider.RewriteAsync(Guid.NewGuid(), sample.ToRewriteRequest(), CancellationToken.None);
            var t0Text = RewritePayload.TryParse(t0.ResultJson)?.RewrittenText ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(t0Text) && calls < maxCalls)
            {
                t0Score = await Measure(pangram, t0Text);
                calls++;
            }

            int? delta = originalScore.HasValue && t0Score.HasValue ? t0Score - originalScore : null;
            rows.Add((sample.Id, sample.Category, words, originalScore, t0Score, delta));
            Console.WriteLine($"{sample.Id}: words={words} original={Fmt(originalScore)} T0={Fmt(t0Score)} (T0-original)={Fmt(delta)}");
        }

        var measured = rows.Where(r => r.Original.HasValue && r.T0.HasValue).ToList();
        var meanOrig = measured.Count == 0 ? null : (int?)Math.Round(measured.Average(r => r.Original!.Value));
        var meanT0 = measured.Count == 0 ? null : (int?)Math.Round(measured.Average(r => r.T0!.Value));
        var originalLowerBy15 = measured.Count(r => r.T0!.Value - r.Original!.Value >= 15);

        var lines = new List<string>
        {
            "# Round-4 gating diagnostic — original draft vs T0 Pangram",
            "",
            "Does the user's ORIGINAL draft read as less AI-like than the T0 rewrite? If not (originals already high), the T5/T6 'preserve the original surface' premise has no room.",
            "",
            "| Case | Category | Draft words | Original Pangram | T0 Pangram | T0 − Original |",
            "| --- | --- | ---: | ---: | ---: | ---: |",
        };
        lines.AddRange(rows.Select(r =>
            $"| {r.Id} | {r.Category} | {r.Words} | {Fmt(r.Original)} | {Fmt(r.T0)} | {Fmt(r.Delta)} |"));
        lines.Add("");
        lines.Add($"Mean original Pangram: **{Fmt(meanOrig)}** · Mean T0 Pangram: **{Fmt(meanT0)}** · cases where original is ≥15 lower than T0: **{originalLowerBy15}/{measured.Count}**");
        lines.Add("");
        lines.Add(originalLowerBy15 >= 4
            ? "Signal: originals are materially less AI-like than T0 in several cases → 'preserve original surface' (T5/T6) is worth building."
            : "Signal: originals are NOT meaningfully less AI-like than T0 → preserving the original surface cannot lower the reading; the T5/T6 premise does not hold.");

        Directory.CreateDirectory(config.OutputDirectory);
        var outPath = Path.Combine(config.OutputDirectory, $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-original-vs-t0-pangram.md");
        await File.WriteAllTextAsync(outPath, string.Join("\n", lines) + "\n");

        Console.WriteLine($"\nMean original={Fmt(meanOrig)} meanT0={Fmt(meanT0)} | original ≥15 lower than T0: {originalLowerBy15}/{measured.Count} | pangram calls={calls}");
        Console.WriteLine($"Wrote {outPath}");
        return 0;
    }

    private static async Task<int?> Measure(IWritingSignalClient client, string text)
    {
        var r = await client.MeasureAsync(text, CancellationToken.None);
        return r.Available ? r.AiLikePercent : null;
    }

    private static string Fmt(int? v) => v?.ToString() ?? "-";
}
