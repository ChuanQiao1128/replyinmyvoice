using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

// Minimal GPTZero cross-signal probe (GPTZERO_PROBE=1, GPTZERO_FILES="label:path,label:path").
// EVAL-ONLY. Scores a few fixed texts on GPTZero. Budget-frugal: one GPTZero call per file.
// Now dumps the PER-SENTENCE breakdown (generated_prob + perplexity + AI highlight) so we can see
// whether the document is uniformly pinned at the ceiling (no gradient) or has a targetable subset
// of weak sentences. Lower perplexity = more predictable to the reference model = more AI-like.
internal static class GptzeroProbe
{
    public static async Task<int> RunAsync()
    {
        var key = Environment.GetEnvironmentVariable("GPTZero_API_KEY")
            ?? Environment.GetEnvironmentVariable("GPTZERO_API_KEY")
            ?? Environment.GetEnvironmentVariable("gptzero_api_key");
        if (string.IsNullOrWhiteSpace(key))
        {
            Console.Error.WriteLine("GPTZERO_PROBE: missing GPTZero_API_KEY.");
            return 2;
        }

        var spec = Environment.GetEnvironmentVariable("GPTZERO_FILES");
        if (string.IsNullOrWhiteSpace(spec))
        {
            Console.Error.WriteLine("GPTZERO_PROBE: set GPTZERO_FILES=\"label:path,label:path\".");
            return 2;
        }

        using var http = new HttpClient();
        Console.WriteLine("GPTZero probe — ai% = P(AI); perplexity lower = more predictable = more AI-like; hl '*' = highlighted as AI.");
        foreach (var item in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = item.IndexOf(':');
            var label = idx > 0 ? item[..idx] : item;
            var path = idx > 0 ? item[(idx + 1)..] : item;
            if (!File.Exists(path))
            {
                Console.WriteLine($"\n{label}: file not found ({path})");
                continue;
            }

            var text = (await File.ReadAllTextAsync(path)).Trim();
            var r = await ScoreAsync(http, key, text, CancellationToken.None);
            if (r.Err is not null)
            {
                Console.WriteLine($"\n{label}: error: {r.Err}  ({text.Length} chars)");
                continue;
            }

            Console.WriteLine(
                $"\n{label}: document = {r.Ai}% AI  [{r.Cls}]  "
                + $"avg_generated_prob={Fmt(r.AvgGenProb)}  burstiness={Fmt(r.Burstiness)}  doc_perplexity={Fmt(r.DocPerplexity)}  "
                + $"({text.Length} chars, {r.Sentences.Count} sentences)");
            Console.WriteLine("    # | ai% | perplexity | hl | sentence");
            Console.WriteLine("   -- | --- | ---------- | -- | --------");
            var n = 0;
            foreach (var s in r.Sentences)
            {
                n++;
                var snippet = s.Sentence.Replace("\n", " ", StringComparison.Ordinal).Trim();
                if (snippet.Length > 64)
                {
                    snippet = snippet[..63] + "…";
                }

                Console.WriteLine(
                    $"   {n,2} | {s.Ai,3} | {Fmt(s.Perplexity),10} | {(s.Highlight ? "* " : "  ")} | {snippet}");
            }
        }

        return 0;
    }

    private static string Fmt(double? v) =>
        v is null ? "-" : v.Value.ToString(v.Value >= 100 ? "F0" : "F2", CultureInfo.InvariantCulture);

    private sealed record SentenceRow(string Sentence, int Ai, double? Perplexity, bool Highlight);

    private sealed record ProbeResult(
        int? Ai, string? Cls, double? AvgGenProb, double? Burstiness, double? DocPerplexity,
        IReadOnlyList<SentenceRow> Sentences, string? Err);

    private static async Task<ProbeResult> ScoreAsync(HttpClient http, string key, string text, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.gptzero.me/v2/predict/text");
            req.Headers.Add("x-api-key", key);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = new StringContent(JsonSerializer.Serialize(new { document = text }), Encoding.UTF8, "application/json");
            using var resp = await http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                // Never echo the response body: GPTZero's error payload reflects the api key back.
                return new ProbeResult(null, null, null, null, null, Array.Empty<SentenceRow>(), $"http_{(int)resp.StatusCode}");
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("documents", out var docs) || docs.ValueKind != JsonValueKind.Array || docs.GetArrayLength() == 0)
            {
                return new ProbeResult(null, null, null, null, null, Array.Empty<SentenceRow>(), "no_documents (unexpected response shape)");
            }

            var d = docs[0];
            double? aiProb = null;
            if (d.TryGetProperty("class_probabilities", out var cp) && cp.ValueKind == JsonValueKind.Object
                && cp.TryGetProperty("ai", out var aiEl) && aiEl.ValueKind == JsonValueKind.Number)
            {
                aiProb = aiEl.GetDouble();
            }
            else if (d.TryGetProperty("completely_generated_prob", out var cg) && cg.ValueKind == JsonValueKind.Number)
            {
                aiProb = cg.GetDouble();
            }

            var cls = d.TryGetProperty("document_classification", out var dc) && dc.ValueKind == JsonValueKind.String ? dc.GetString() : null;
            if (aiProb is null && cls is not null)
            {
                aiProb = cls == "AI_ONLY" ? 0.99 : cls == "HUMAN_ONLY" ? 0.01 : 0.5;
            }

            var sentences = new List<SentenceRow>();
            if (d.TryGetProperty("sentences", out var sents) && sents.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in sents.EnumerateArray())
                {
                    var sentence = s.TryGetProperty("sentence", out var se) && se.ValueKind == JsonValueKind.String ? se.GetString() : null;
                    if (sentence is null)
                    {
                        continue;
                    }

                    var gp = s.TryGetProperty("generated_prob", out var ge) && ge.ValueKind == JsonValueKind.Number ? ge.GetDouble() : (double?)null;
                    var perp = s.TryGetProperty("perplexity", out var pe) && pe.ValueKind == JsonValueKind.Number ? pe.GetDouble() : (double?)null;
                    var hl = s.TryGetProperty("highlight_sentence_for_ai", out var he) && (he.ValueKind == JsonValueKind.True || he.ValueKind == JsonValueKind.False) && he.GetBoolean();
                    sentences.Add(new SentenceRow(sentence, gp is null ? -1 : (int)Math.Round(gp.Value * 100), perp, hl));
                }
            }

            return new ProbeResult(
                aiProb is null ? null : (int)Math.Round(aiProb.Value * 100),
                cls,
                Num(d, "average_generated_prob"),
                Num(d, "overall_burstiness"),
                Num(d, "perplexity"),
                sentences,
                null);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or OperationCanceledException)
        {
            return new ProbeResult(null, null, null, null, null, Array.Empty<SentenceRow>(), e.GetType().Name);
        }
    }

    private static double? Num(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
}
