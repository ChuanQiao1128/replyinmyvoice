using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

// Minimal GPTZero cross-signal probe (GPTZERO_PROBE=1, GPTZERO_FILES="label:path,label:path").
// EVAL-ONLY. Scores a few fixed texts on GPTZero to check whether the Pangram findings are
// tool-general (clean rewrite = AI, broken machine-translation = human) and whether the case-001
// low reading was Pangram-specific. Budget-frugal: one GPTZero call per file.
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
        Console.WriteLine("GPTZero scores (ai% = probability AI-generated; higher = more AI):");
        foreach (var item in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = item.IndexOf(':');
            var label = idx > 0 ? item[..idx] : item;
            var path = idx > 0 ? item[(idx + 1)..] : item;
            if (!File.Exists(path))
            {
                Console.WriteLine($"  {label}: file not found ({path})");
                continue;
            }

            var text = (await File.ReadAllTextAsync(path)).Trim();
            var (ai, cls, err) = await ScoreAsync(http, key, text, CancellationToken.None);
            Console.WriteLine($"  {label}: {(err is null ? $"{ai}% AI  [{cls}]" : "error: " + err)}  ({text.Length} chars)");
        }

        return 0;
    }

    private static async Task<(int? Ai, string? Cls, string? Err)> ScoreAsync(HttpClient http, string key, string text, CancellationToken ct)
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
                return (null, null, $"http_{(int)resp.StatusCode}");
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("documents", out var docs) || docs.ValueKind != JsonValueKind.Array || docs.GetArrayLength() == 0)
            {
                return (null, null, "no_documents (unexpected response shape)");
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

            return (aiProb is null ? null : (int)Math.Round(aiProb.Value * 100), cls, null);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or OperationCanceledException)
        {
            return (null, null, e.GetType().Name);
        }
    }
}
