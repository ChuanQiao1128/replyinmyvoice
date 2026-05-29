// Judge an arbitrary text file against fact lists (JUDGE_FILE=1, JF_TEXT, JF_KEEP, JF_FORBID). EVAL-ONLY.
// Runs SemanticEvalJudge and prints how many must_keep facts are preserved + which drifted, so we can
// re-judge a hand-edited / surgically-repaired candidate without the full loop.
internal static class JudgeFile
{
    public static async Task<int> RunAsync(string apiKey, string model, string baseUrl)
    {
        var textFile = Environment.GetEnvironmentVariable("JF_TEXT");
        if (string.IsNullOrWhiteSpace(textFile) || !File.Exists(textFile))
        {
            Console.Error.WriteLine("JUDGE_FILE: set JF_TEXT=path (and JF_KEEP / JF_FORBID).");
            return 2;
        }

        var text = (await File.ReadAllTextAsync(textFile)).Trim();
        var keepFile = Environment.GetEnvironmentVariable("JF_KEEP");
        var forbidFile = Environment.GetEnvironmentVariable("JF_FORBID");
        var mustKeep = !string.IsNullOrWhiteSpace(keepFile) && File.Exists(keepFile) ? (await File.ReadAllLinesAsync(keepFile)).Where(l => !string.IsNullOrWhiteSpace(l)).ToList() : new List<string>();
        var mustNotClaim = !string.IsNullOrWhiteSpace(forbidFile) && File.Exists(forbidFile) ? (await File.ReadAllLinesAsync(forbidFile)).Where(l => !string.IsNullOrWhiteSpace(l)).ToList() : new List<string>();

        using var http = new HttpClient();
        var judge = new SemanticEvalJudge(http, apiKey, model, baseUrl, TimeSpan.FromSeconds(90));
        var sem = await judge.VerifyAsync(text, mustKeep, mustNotClaim, CancellationToken.None);
        if (sem.Error is not null)
        {
            Console.WriteLine($"JUDGE_FILE error: {sem.Error}");
            return 1;
        }

        var failed = sem.Facts.Count(f => string.Equals(f.Status, "missing", StringComparison.OrdinalIgnoreCase) || string.Equals(f.Status, "contradicted", StringComparison.OrdinalIgnoreCase));
        var preserved = sem.Facts.Count - failed;
        Console.WriteLine($"JUDGE_FILE: facts {preserved}/{sem.Facts.Count} preserved · FactsReallyPass={sem.FactsReallyPass} · MeaningChanged={sem.MeaningChanged} · RealForbidden={sem.RealForbidden} · SendReady={sem.SendReady}");
        foreach (var f in sem.Facts.Where(f => string.Equals(f.Status, "missing", StringComparison.OrdinalIgnoreCase) || string.Equals(f.Status, "contradicted", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine($"  [{f.Status}] {f.Fact}" + (string.IsNullOrWhiteSpace(f.Reason) ? string.Empty : $" — {f.Reason}"));
        }

        foreach (var f in sem.Forbidden.Where(f => f.Violated))
        {
            Console.WriteLine($"  [forbidden] {f.Rule}");
        }

        return 0;
    }
}
