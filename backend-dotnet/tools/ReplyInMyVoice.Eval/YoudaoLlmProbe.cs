using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Infrastructure.Providers;

// Probe of Youdao's LLM (large-model) translation API (YOUDAO_LLM_PROBE=1). EVAL-ONLY.
// Endpoint: POST https://openapi.youdao.com/proxy/http/llm-trans  (text/event-stream).
// Tests whether a MORE-NATIVE (LLM) translator changes the round-trip outcome vs the NMT used in
// rounds 1-7. Prediction (from the 10-round law): more-native English => Pangram climbs back toward
// ~99 (the NMT translationese was the only thing lowering it). Pangram here is OFFLINE OBSERVATION
// ONLY — this is a quality-engine data point, not a detection-track reopening.

internal sealed class YoudaoLlmTranslationClient
{
    private readonly HttpClient _http;
    private readonly string _appKey;
    private readonly string _appSecret;
    private readonly TimeSpan _timeout;

    public int CallCount { get; private set; }

    public YoudaoLlmTranslationClient(HttpClient http, string appKey, string appSecret, TimeSpan timeout)
    {
        _http = http;
        _appKey = appKey;
        _appSecret = appSecret;
        _timeout = timeout;
    }

    public async Task<(bool Success, string Text, string? Error)> TranslateAsync(string text, string from, string to, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (true, text, null);
        }

        CallCount += 1;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        var salt = Guid.NewGuid().ToString();
        var curtime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var input = text.Length <= 20 ? text : text.Substring(0, 10) + text.Length + text.Substring(text.Length - 10);
        var sign = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_appKey + input + salt + curtime + _appSecret))).ToLowerInvariant();

        var form = new Dictionary<string, string>
        {
            ["i"] = text,
            ["from"] = from,
            ["to"] = to,
            ["appKey"] = _appKey,
            ["salt"] = salt,
            ["sign"] = sign,
            ["signType"] = "v3",
            ["curtime"] = curtime,
            ["streamType"] = "full",   // emit the complete translation in data.transFull
            ["handleOption"] = "0",     // Pro 14B (best quality)
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://openapi.youdao.com/proxy/http/llm-trans")
            {
                Content = new FormUrlEncodedContent(form),
            };
            using var resp = await _http.SendAsync(req, cts.Token);
            var body = await resp.Content.ReadAsStringAsync(cts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                return (false, string.Empty, $"llm_http_{(int)resp.StatusCode}");
            }

            // Response is text/event-stream: lines like "data:{json}". Parse each data: line, keep the
            // last non-empty data.transFull (with streamType=full it carries the complete translation).
            string? full = null;
            string? errCode = null;
            foreach (var raw in body.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                var json = line.StartsWith("data:", StringComparison.Ordinal) ? line[5..].Trim() : line;
                if (json.Length == 0 || json[0] != '{')
                {
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("code", out var c))
                    {
                        errCode = c.ValueKind == JsonValueKind.String ? c.GetString() : c.ToString();
                    }

                    if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                    {
                        if (data.TryGetProperty("transFull", out var tf) && tf.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(tf.GetString()))
                        {
                            full = tf.GetString();
                        }
                        else if (data.TryGetProperty("transIncre", out var ti) && ti.ValueKind == JsonValueKind.String)
                        {
                            full = (full ?? string.Empty) + ti.GetString();
                        }
                    }
                }
                catch (JsonException)
                {
                    // skip non-JSON event lines
                }
            }

            if (!string.IsNullOrWhiteSpace(full))
            {
                return (true, full!, null);
            }

            return (false, string.Empty, errCode is not null and not "0" ? $"llm_code_{errCode}" : "llm_no_translation");
        }
        catch (OperationCanceledException)
        {
            return (false, string.Empty, "llm_timeout");
        }
        catch (HttpRequestException)
        {
            return (false, string.Empty, "llm_network_failed");
        }
    }
}

internal static class YoudaoLlmProbeRunner
{
    public static async Task<int> RunAsync(
        FactReconstructRewriteProvider provider, IReadOnlyList<EvalCase> cases, EvalConfig config, string apiKey)
    {
        var appKey = FirstEnv("YOUDAO_APP_KEY", "AppID", "YouDao_API_KEY");
        var appSecret = FirstEnv("YOUDAO_APP_SECRET", "AppSecret");
        if (string.IsNullOrWhiteSpace(appKey) || string.IsNullOrWhiteSpace(appSecret))
        {
            Console.Error.WriteLine("YOUDAO_LLM_PROBE: missing Youdao credentials.");
            return 2;
        }

        var pangramMax = int.TryParse(Environment.GetEnvironmentVariable("PANGRAM_MAX_CALLS"), out var pm) && pm > 0 ? pm : 0;
        var pangramKey = Environment.GetEnvironmentVariable("PANGRAM_API_KEY");
        var pangramOn = pangramMax > 0 && !string.IsNullOrWhiteSpace(pangramKey);

        using var youdaoHttp = new HttpClient();
        using var pangramHttp = new HttpClient();
        using var judgeHttp = new HttpClient();
        var llm = new YoudaoLlmTranslationClient(youdaoHttp, appKey!, appSecret!, TimeSpan.FromSeconds(60));
        var pangram = pangramOn ? new PangramWritingSignalClient(pangramHttp, pangramKey!, TimeSpan.FromSeconds(45)) : null;
        var judge = new SemanticEvalJudge(judgeHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));
        var pangramCalls = 0;

        Console.WriteLine($"Youdao LLM-translation probe (offline Pangram observation): cases={cases.Count} pangram={(pangramOn ? "on" : "off")}");
        var sb = new StringBuilder("# Youdao LLM-translation round-trip probe (eval-only; Pangram = offline observation)\n\n");
        sb.AppendLine("| Case | Pangram T0 | Pangram LLM-trans | Δ | facts pass(sem) | meaning_changed |");
        sb.AppendLine("| --- | ---: | ---: | ---: | :---: | :---: |");

        foreach (var sample in cases)
        {
            var req = sample.ToRewriteRequest();
            var t0 = await provider.RewriteAsync(Guid.NewGuid(), req, CancellationToken.None);
            var t0Text = RewritePayload.TryParse(t0.ResultJson)?.RewrittenText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(t0Text))
            {
                Console.WriteLine($"{sample.Id}: T0 no output; skipped.");
                continue;
            }

            var toZh = await llm.TranslateAsync(t0Text, "en", "zh-CHS", CancellationToken.None);
            var back = toZh.Success ? await llm.TranslateAsync(toZh.Text, "zh-CHS", "en", CancellationToken.None) : (false, string.Empty, toZh.Error);
            if (!back.Item1)
            {
                Console.WriteLine($"{sample.Id}: LLM-translation failed ({back.Item3 ?? toZh.Error}).");
                sb.AppendLine($"| {sample.Id} | - | - | - | - | (translate failed: {back.Item3 ?? toZh.Error}) |");
                continue;
            }

            var llmText = back.Item2;
            var sem = await judge.VerifyAsync(llmText, sample.MustKeep, sample.MustNotClaim, CancellationToken.None);
            int? p0 = null, pl = null;
            if (pangram is not null)
            {
                if (pangramCalls < pangramMax) { p0 = (await pangram.MeasureAsync(t0Text, CancellationToken.None)).AiLikePercent; pangramCalls++; }
                if (pangramCalls < pangramMax) { pl = (await pangram.MeasureAsync(llmText, CancellationToken.None)).AiLikePercent; pangramCalls++; }
            }

            int? delta = p0.HasValue && pl.HasValue ? pl - p0 : null;
            Console.WriteLine($"{sample.Id}: pangram T0={p0?.ToString() ?? "-"} LLM={pl?.ToString() ?? "-"} delta={delta?.ToString() ?? "-"} facts(sem)={(sem.Error is null && sem.FactsReallyPass)} meaningChanged={sem.MeaningChanged}");
            sb.AppendLine($"| {sample.Id} | {p0?.ToString() ?? "-"} | {pl?.ToString() ?? "-"} | {(delta is null ? "-" : delta > 0 ? "+" + delta : delta.ToString())} | {(sem.Error is null && sem.FactsReallyPass ? "yes" : "no")} | {sem.MeaningChanged} |");
            sb.AppendLine();
            sb.AppendLine($"**{sample.Id} — T0:**\n\n> " + t0Text.Replace("\n", "\n> ", StringComparison.Ordinal));
            sb.AppendLine();
            sb.AppendLine($"**{sample.Id} — LLM round-trip:**\n\n> " + llmText.Replace("\n", "\n> ", StringComparison.Ordinal));
            sb.AppendLine();
        }

        Directory.CreateDirectory(config.OutputDirectory);
        var outPath = Path.Combine(config.OutputDirectory, $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-youdao-llm-probe.md");
        await File.WriteAllTextAsync(outPath, sb.ToString());
        Console.WriteLine($"youdaoLlmCalls={llm.CallCount} pangram={pangramCalls}; wrote {outPath}");
        return 0;
    }

    private static string? FirstEnv(params string[] names) =>
        names.Select(Environment.GetEnvironmentVariable).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
