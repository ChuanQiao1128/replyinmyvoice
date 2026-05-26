using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

// Semantic fact + forbidden-claim verifier for the eval tool (LLM judge). Replaces the
// over-literal FactExpectationChecker matcher and the keyword ForbiddenClaimScreen, which the
// 2026-05-27 AI-draft baseline showed are wrong in both directions (false-negatives like
// set~confirmed, and false-positives like a faithful "$40 refund in 3-5 business days").
// Exact preservation for amounts/dates/IDs/invoice numbers/names; semantic equivalence for
// status/action/intent. EVAL-ONLY (never wired into the production engine).

public sealed record SemFactVerdict(string Fact, string Status, string? EvidenceQuote, string? Reason);

public sealed record SemForbiddenVerdict(string Rule, bool Violated, string? Reason);

public sealed record SemVerdict(
    IReadOnlyList<SemFactVerdict> Facts,
    IReadOnlyList<SemForbiddenVerdict> Forbidden,
    bool MeaningChanged,
    bool SendReady,
    string? Error = null)
{
    // A fact passes unless it is missing or contradicted; "unverifiable" does not fail it.
    public bool FactsReallyPass => Error is null && !Facts.Any(f =>
        string.Equals(f.Status, "missing", StringComparison.OrdinalIgnoreCase)
        || string.Equals(f.Status, "contradicted", StringComparison.OrdinalIgnoreCase));

    public int RealForbidden => Forbidden.Count(f => f.Violated);
}

public sealed class SemanticEvalJudge(
    HttpClient httpClient,
    string apiKey,
    string model,
    string baseUrl,
    TimeSpan timeout)
{
    // Bump when the judge prompt changes so re-scores across agent versions stay comparable.
    public const string PromptVersion = "semverify-v1-2026-05-27";

    public string Model => model;

    private const string SystemPrompt =
        "You are a strict evaluation judge for an email-rewriting system. Decide whether a REWRITE "
        + "preserved each required fact from the source and whether it made any forbidden claim. Rules: "
        + "(1) For amounts/money/dates/deadlines/invoice numbers/IDs/codes/proper names: require EXACT "
        + "preservation - a changed or dropped number, date, or name = missing or contradicted. "
        + "(2) For statuses/actions/intent/requests: accept semantic equivalence - e.g. 'set' preserves "
        + "'confirmed'; 'wrapped up' preserves 'finished'; 'Would that suit you?' preserves 'asks what works'; "
        + "'we can walk through the options' preserves 'changes can be discussed'. "
        + "(3) A must_not_claim is violated ONLY if the rewrite actually asserts the forbidden thing, changes "
        + "an amount/time/liability, or turns uncertainty into certainty. 'refund in 3-5 business days' does "
        + "NOT violate 'no instant refund'. Do not flag a word just because it appears. Return JSON only.";

    public async Task<SemVerdict> VerifyAsync(
        string rewrite,
        IReadOnlyList<string> mustKeep,
        IReadOnlyList<string> mustNotClaim,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rewrite))
        {
            return Error("empty_rewrite");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["temperature"] = 0,
            ["response_format"] = new { type = "json_object" },
            ["max_tokens"] = 1600,
            ["messages"] = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = BuildUserPrompt(rewrite, mustKeep, mustNotClaim) },
            },
        };
        if (IsDeepSeekBaseUrl(baseUrl))
        {
            payload["thinking"] = new { type = "disabled" };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri(baseUrl));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            using var response = await httpClient.SendAsync(request, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return Error($"judge_http_{(int)response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return Parse(content);
        }
        catch (OperationCanceledException)
        {
            return Error("judge_timeout");
        }
        catch (HttpRequestException)
        {
            return Error("judge_network_failed");
        }
        catch (JsonException)
        {
            return Error("judge_response_parse_failed");
        }
    }

    private static string BuildUserPrompt(string rewrite, IReadOnlyList<string> mustKeep, IReadOnlyList<string> mustNotClaim) =>
        "REWRITE:\n" + rewrite
        + "\n\nMUST_KEEP (each must be preserved):\n" + string.Join("\n", mustKeep.Select(f => "- " + f))
        + "\n\nMUST_NOT_CLAIM:\n" + string.Join("\n", mustNotClaim.Select(f => "- " + f))
        + "\n\nReturn JSON: {\"facts\":[{\"fact\":\"...\",\"status\":\"preserved|missing|contradicted|unverifiable\","
        + "\"evidence_quote\":\"...\",\"reason\":\"...\"}],\"forbidden\":[{\"rule\":\"...\",\"violated\":true,"
        + "\"reason\":\"...\"}],\"meaning_changed\":false,\"send_ready\":true}";

    private static SemVerdict Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Error("judge_empty");
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var facts = new List<SemFactVerdict>();
            if (root.TryGetProperty("facts", out var factsEl) && factsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in factsEl.EnumerateArray())
                {
                    facts.Add(new SemFactVerdict(
                        Str(f, "fact"), Str(f, "status"), StrOrNull(f, "evidence_quote"), StrOrNull(f, "reason")));
                }
            }

            var forbidden = new List<SemForbiddenVerdict>();
            if (root.TryGetProperty("forbidden", out var forbEl) && forbEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in forbEl.EnumerateArray())
                {
                    forbidden.Add(new SemForbiddenVerdict(Str(f, "rule"), Bool(f, "violated"), StrOrNull(f, "reason")));
                }
            }

            return new SemVerdict(facts, forbidden, Bool(root, "meaning_changed"), Bool(root, "send_ready"));
        }
        catch (JsonException)
        {
            return Error("judge_json_parse_failed");
        }
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static string? StrOrNull(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True
            || (v.ValueKind == JsonValueKind.String && bool.TryParse(v.GetString(), out var b) && b));

    private static SemVerdict Error(string code) => new([], [], false, false, code);

    private static bool IsDeepSeekBaseUrl(string value)
    {
        try
        {
            var host = new Uri(value).Host.ToLowerInvariant();
            return host == "api.deepseek.com" || host.EndsWith(".deepseek.com", StringComparison.Ordinal);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static Uri BuildChatCompletionsUri(string configuredBaseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? "https://api.openai.com/v1"
            : configuredBaseUrl.Trim().TrimEnd('/');
        if (normalized.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(normalized);
        }

        return normalized.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? new Uri($"{normalized}/chat/completions")
            : new Uri($"{normalized}/v1/chat/completions");
    }
}
