using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.RewriteEngine;
using ReplyInMyVoice.Infrastructure.Providers;

// Round-6 external-rewrite-engine A/B pilot (EXTERNAL_REWRITE_AB_PILOT=1). EVAL-ONLY.
// See plans/translation-roundtrip-pilot.md + the round-6 design.
//
// Different variable from rounds 1-5: swap the REWRITE ENGINE itself. T0 = production baseline;
// EX1 = an external provider (Manus API task, or a generic HTTP endpoint). Same draft, same tone,
// same ledger constraints, same hard gates. This is primarily a QUALITY A/B — per the doc, a low
// Pangram on non-native text is NOT success; an engine that is more fact-safe + more natural is a
// quality win even if Pangram doesn't move. Facts are always extracted from the original/T0, never
// from EX1; EX1's self-reported "facts_preserved" is ignored. Pangram is read once per gate survivor.

internal sealed record LedgerItem(string Category, string Text);

internal sealed record ExternalRewriteRequest(
    string CaseId, string Draft, string Tone,
    IReadOnlyList<LedgerItem> FactLedger,
    IReadOnlyList<LedgerItem> BoundaryLedger,
    IReadOnlyList<LedgerItem> ProtectedTermLedger,
    string SourceType, string RiskTier);

internal sealed record ExternalRewriteResult(
    bool Success, string Text, string ProviderName, string RawResponse, string ErrorCode, TimeSpan Latency, int RetryCount);

internal interface IExternalRewriteProvider
{
    string ProviderName { get; }

    Task<ExternalRewriteResult> RewriteAsync(ExternalRewriteRequest request, CancellationToken cancellationToken);
}

internal static class ExternalRewritePrompt
{
    public const string PromptVersion = "ext-rewrite-v1-2026-05-27";

    public const string StructuredSchema =
        "{\"type\":\"object\",\"properties\":{\"rewritten_text\":{\"type\":\"string\"},"
        + "\"risk_notes\":{\"type\":\"string\"},\"facts_preserved_claim\":{\"type\":\"boolean\"},"
        + "\"boundary_preserved_claim\":{\"type\":\"boolean\"}},\"required\":[\"rewritten_text\","
        + "\"risk_notes\",\"facts_preserved_claim\",\"boundary_preserved_claim\"],\"additionalProperties\":false}";

    public static string Build(ExternalRewriteRequest r) =>
        "Rewrite the following English draft into a warm, natural, send-ready message.\n\n"
        + "Hard requirements:\n"
        + "1. Preserve every fact in FactLedger.\n"
        + "2. Preserve every boundary in BoundaryLedger: do not change cannot/may/not yet/no decision/"
        + "no advice/no refund/no guarantee meanings.\n"
        + "3. Preserve every business object and protected term in ProtectedTermLedger.\n"
        + "4. Do not add new facts, promises, apologies, discounts, refunds, medical advice, legal advice, "
        + "hiring decisions, deadline extensions, or policy exceptions.\n"
        + "5. Tone should be warm, clear, and human. Do not over-polish into a generic corporate template.\n"
        + "6. Do not research or browse the web. Only rewrite. Output only the rewritten text and short risk notes.\n\n"
        + $"Tone: {r.Tone}\nSource type: {r.SourceType}\nRisk tier: {r.RiskTier}\n\n"
        + "Original draft:\n" + r.Draft + "\n\n"
        + "FactLedger:\n" + ToJson(r.FactLedger) + "\n\n"
        + "BoundaryLedger:\n" + ToJson(r.BoundaryLedger) + "\n\n"
        + "ProtectedTermLedger:\n" + ToJson(r.ProtectedTermLedger);

    private static string ToJson(IReadOnlyList<LedgerItem> items) =>
        JsonSerializer.Serialize(items.Select(i => new { category = i.Category, text = i.Text }));
}

// Manus API v2 provider: one task per case (task.create with structured_output_schema), then poll
// task.listMessages until agent_status=stopped and read structured_output_result.value.rewritten_text.
internal sealed class ManusRewriteProvider : IExternalRewriteProvider
{
    public string ProviderName { get; }

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly Uri _baseUrl;
    private readonly string? _agentProfile;
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _pollInterval;

    public ManusRewriteProvider(HttpClient http, string apiKey, string baseUrl, string? agentProfile, TimeSpan timeout, TimeSpan pollInterval)
    {
        _http = http;
        _apiKey = apiKey;
        _baseUrl = new Uri((string.IsNullOrWhiteSpace(baseUrl) ? "https://api.manus.ai" : baseUrl).TrimEnd('/') + "/");
        _agentProfile = string.IsNullOrWhiteSpace(agentProfile) ? null : agentProfile;
        _timeout = timeout;
        _pollInterval = pollInterval;
        ProviderName = _agentProfile is null ? "manus" : $"manus:{_agentProfile}";
    }

    public async Task<ExternalRewriteResult> RewriteAsync(ExternalRewriteRequest request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var prompt = ExternalRewritePrompt.Build(request);

        string taskId;
        try
        {
            var createBody = new Dictionary<string, object?>
            {
                ["message"] = new { content = prompt },
                ["structured_output_schema"] = JsonSerializer.Deserialize<JsonElement>(ExternalRewritePrompt.StructuredSchema),
                ["hide_in_task_list"] = true,
            };
            if (_agentProfile is not null)
            {
                createBody["agent_profile"] = _agentProfile;
            }

            using var createReq = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUrl, "v2/task.create"));
            createReq.Headers.Add("x-manus-api-key", _apiKey);
            createReq.Content = new StringContent(JsonSerializer.Serialize(createBody), Encoding.UTF8, "application/json");
            using var createResp = await _http.SendAsync(createReq, cancellationToken);
            var createJson = await createResp.Content.ReadAsStringAsync(cancellationToken);
            if (!createResp.IsSuccessStatusCode)
            {
                return Fail($"manus_create_http_{(int)createResp.StatusCode}", sw, createJson, 0);
            }

            using var createDoc = JsonDocument.Parse(createJson);
            if (!createDoc.RootElement.TryGetProperty("task_id", out var idEl) || idEl.ValueKind != JsonValueKind.String)
            {
                return Fail("manus_no_task_id", sw, createJson, 0);
            }

            taskId = idEl.GetString()!;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or OperationCanceledException)
        {
            return Fail("manus_create_failed:" + e.GetType().Name, sw, string.Empty, 0);
        }

        var polls = 0;
        while (sw.Elapsed < _timeout)
        {
            await Task.Delay(_pollInterval, cancellationToken);
            polls++;
            try
            {
                var uri = new Uri(_baseUrl, $"v2/task.listMessages?task_id={Uri.EscapeDataString(taskId)}&order=desc&limit=50&verbose=true");
                using var listReq = new HttpRequestMessage(HttpMethod.Get, uri);
                listReq.Headers.Add("x-manus-api-key", _apiKey);
                using var listResp = await _http.SendAsync(listReq, cancellationToken);
                var listJson = await listResp.Content.ReadAsStringAsync(cancellationToken);
                if (!listResp.IsSuccessStatusCode)
                {
                    continue; // transient; keep polling until timeout
                }

                var (text, raw, status, found) = ParseMessages(listJson);
                if (found && !string.IsNullOrWhiteSpace(text))
                {
                    return new ExternalRewriteResult(true, text!, ProviderName, raw ?? listJson, string.Empty, sw.Elapsed, polls);
                }

                if (status is "stopped")
                {
                    // Stopped but no structured rewrite found — fall back to the last assistant message.
                    return string.IsNullOrWhiteSpace(text)
                        ? Fail("manus_stopped_no_output", sw, raw ?? listJson, polls)
                        : new ExternalRewriteResult(true, text!, ProviderName, raw ?? listJson, "manus_fallback_assistant_text", sw.Elapsed, polls);
                }

                if (status is "error")
                {
                    return Fail("manus_task_error", sw, raw ?? listJson, polls);
                }
            }
            catch (Exception e) when (e is HttpRequestException or JsonException)
            {
                // keep polling
            }
        }

        return Fail("manus_timeout", sw, string.Empty, polls);
    }

    // Returns (structuredOrAssistantText, rawValueJson, agentStatus, foundStructured).
    private static (string? Text, string? Raw, string? Status, bool FoundStructured) ParseMessages(string listJson)
    {
        using var doc = JsonDocument.Parse(listJson);
        if (!doc.RootElement.TryGetProperty("messages", out var msgs) || msgs.ValueKind != JsonValueKind.Array)
        {
            return (null, null, null, false);
        }

        string? status = null;
        string? assistantText = null;
        foreach (var m in msgs.EnumerateArray())
        {
            var type = m.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
            if (type == "structured_output_result" && m.TryGetProperty("structured_output_result", out var sor))
            {
                var ok = sor.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
                if (ok && sor.TryGetProperty("value", out var val) && val.ValueKind == JsonValueKind.Object
                    && val.TryGetProperty("rewritten_text", out var rt) && rt.ValueKind == JsonValueKind.String)
                {
                    return (rt.GetString(), val.GetRawText(), status, true);
                }
            }
            else if (type == "status_update" && m.TryGetProperty("status_update", out var su)
                && su.TryGetProperty("agent_status", out var asx) && asx.ValueKind == JsonValueKind.String)
            {
                // messages are desc (newest first); keep the first (latest) status we see
                status ??= asx.GetString();
            }
            else if (type == "assistant_message" && assistantText is null && m.TryGetProperty("assistant_message", out var am)
                && am.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
            {
                assistantText = c.GetString();
            }
        }

        return (assistantText, null, status, false);
    }

    private ExternalRewriteResult Fail(string code, Stopwatch sw, string raw, int polls) =>
        new(false, string.Empty, ProviderName, raw, code, sw.Elapsed, polls);
}

// Generic HTTP provider: POST a JSON body to EXTERNAL_REWRITE_URL, read rewritten_text from the response.
internal sealed class GenericHttpRewriteProvider : IExternalRewriteProvider
{
    public string ProviderName => "generic_http";

    private readonly HttpClient _http;
    private readonly Uri _url;
    private readonly string? _apiKey;
    private readonly TimeSpan _timeout;

    public GenericHttpRewriteProvider(HttpClient http, string url, string? apiKey, TimeSpan timeout)
    {
        _http = http;
        _url = new Uri(url);
        _apiKey = apiKey;
        _timeout = timeout;
    }

    public async Task<ExternalRewriteResult> RewriteAsync(ExternalRewriteRequest request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);

        var body = new
        {
            draft = request.Draft,
            tone = request.Tone,
            source_type = request.SourceType,
            risk_tier = request.RiskTier,
            fact_ledger = request.FactLedger.Select(i => new { category = i.Category, text = i.Text }),
            boundary_ledger = request.BoundaryLedger.Select(i => new { category = i.Category, text = i.Text }),
            protected_term_ledger = request.ProtectedTermLedger.Select(i => new { category = i.Category, text = i.Text }),
            output_format = "json",
            instructions = "Preserve facts and boundaries. Return rewritten_text only.",
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, _url)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }

            using var resp = await _http.SendAsync(req, cts.Token);
            var json = await resp.Content.ReadAsStringAsync(cts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                return new ExternalRewriteResult(false, string.Empty, ProviderName, json, $"generic_http_{(int)resp.StatusCode}", sw.Elapsed, 0);
            }

            using var doc = JsonDocument.Parse(json);
            var text = doc.RootElement.TryGetProperty("rewritten_text", out var rt) && rt.ValueKind == JsonValueKind.String
                ? rt.GetString()
                : doc.RootElement.TryGetProperty("text", out var tx) && tx.ValueKind == JsonValueKind.String ? tx.GetString() : null;
            return string.IsNullOrWhiteSpace(text)
                ? new ExternalRewriteResult(false, string.Empty, ProviderName, json, "generic_no_text", sw.Elapsed, 0)
                : new ExternalRewriteResult(true, text!, ProviderName, json, string.Empty, sw.Elapsed, 0);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or OperationCanceledException)
        {
            return new ExternalRewriteResult(false, string.Empty, ProviderName, string.Empty, "generic_failed:" + e.GetType().Name, sw.Elapsed, 0);
        }
    }
}

internal sealed record CandidateGates(
    bool FactPass, int Forbid, bool MeaningChanged, IReadOnlyList<string> ProtectedMissing,
    bool Understandable, bool NativeSendReady, IReadOnlyList<string> MissingFacts)
{
    // ProtectedTerm exact-presence is RECORDED but not part of HardPass: the deterministic check
    // over-flags legitimate paraphrases of soft action verbs (e.g. EX1 used "decide", matching
    // must_keep, where T0 said "confirm"). The semantic FactGate is the authority on object/fact
    // drift (it sees the business nouns via must_keep, so saucer->tea tray is still caught).
    public bool HardPass => FactPass && Forbid == 0 && !MeaningChanged && Understandable;
}

internal sealed record PilotV5Row(
    string CaseId, int CaseNumber, string Category, string Tone, string RiskTier,
    string T0Text, string ExternalText, string ExternalProvider, bool ExternalSuccess, string ExternalErrorCode,
    long LatencyMs, int RetryCount,
    bool T0FactPass, bool T0HardPass, bool T0NativeSendReady,
    bool ExFactPass, bool ExBoundaryPass, bool ExProtectedPass, bool ExForbiddenPass, bool ExUnderstandable, bool ExNativeSendReady, bool ExHardPass,
    IReadOnlyList<string> ExMissingFacts, IReadOnlyList<string> ExProtectedMissing,
    int? PangramT0, int? PangramExternal, int? DeltaExternalMinusT0,
    string QualityWinner, string DetectionWinner, string Notes);

internal static class ExternalRewriteAbRunner
{
    public static async Task<int> RunAsync(
        FactReconstructRewriteProvider t0Provider,
        CountingRewriteModelClient modelCounter,
        CountingWritingSignalClient saplingCounter,
        IReadOnlyList<EvalCase> cases,
        EvalConfig config,
        string apiKey,
        DateTimeOffset startedAt)
    {
        var providerKind = (Environment.GetEnvironmentVariable("EXTERNAL_REWRITE_PROVIDER") ?? "manus").Trim().ToLowerInvariant();
        var timeout = TimeSpan.FromSeconds(IntEnv("EXTERNAL_REWRITE_TIMEOUT_SECONDS", 240));

        using var externalHttp = new HttpClient { Timeout = timeout + TimeSpan.FromSeconds(30) };
        using var deepseekHttp = new HttpClient();
        using var judgeHttp = new HttpClient();
        using var pangramHttp = new HttpClient();

        IExternalRewriteProvider? external = null;
        if (providerKind == "manus")
        {
            var manusKey = FirstEnv("MANUS_API_KEY", "Manus_API_KEY", "manus_api_key");
            if (string.IsNullOrWhiteSpace(manusKey))
            {
                Console.Error.WriteLine("EXTERNAL_REWRITE_AB_PILOT: provider=manus but MANUS_API_KEY/Manus_API_KEY missing.");
                return 2;
            }

            external = new ManusRewriteProvider(
                externalHttp, manusKey!,
                Environment.GetEnvironmentVariable("MANUS_API_BASE_URL") ?? "https://api.manus.ai",
                Environment.GetEnvironmentVariable("MANUS_AGENT_PROFILE") ?? "manus-1.6-lite",
                timeout, TimeSpan.FromSeconds(IntEnv("EXTERNAL_REWRITE_POLL_SECONDS", 6)));
        }
        else if (providerKind == "generic_http")
        {
            var url = Environment.GetEnvironmentVariable("EXTERNAL_REWRITE_URL");
            if (string.IsNullOrWhiteSpace(url))
            {
                Console.Error.WriteLine("EXTERNAL_REWRITE_AB_PILOT: provider=generic_http but EXTERNAL_REWRITE_URL missing.");
                return 2;
            }

            external = new GenericHttpRewriteProvider(externalHttp, url!, Environment.GetEnvironmentVariable("EXTERNAL_REWRITE_API_KEY"), timeout);
        }
        else
        {
            Console.Error.WriteLine($"EXTERNAL_REWRITE_AB_PILOT: unknown EXTERNAL_REWRITE_PROVIDER '{providerKind}'.");
            return 2;
        }

        var pangramMaxCalls = IntEnv("PANGRAM_MAX_CALLS", 0);
        var pangramKey = Environment.GetEnvironmentVariable("PANGRAM_API_KEY");
        var pangramEnabled = pangramMaxCalls > 0 && !string.IsNullOrWhiteSpace(pangramKey);

        var deepseek = new DeepSeekChatClient(deepseekHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));
        var termProposer = new ProtectedTermProposer(deepseek);
        var understandability = new UnderstandabilityJudge(deepseek);
        var tierJudge = new SendabilityTierJudge(deepseek);
        var judge = new SemanticEvalJudge(judgeHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));
        var pangram = pangramEnabled ? new PangramWritingSignalClient(pangramHttp, pangramKey!, TimeSpan.FromSeconds(45)) : null;

        Console.WriteLine(
            $"External A/B: provider={external.ProviderName} cases={cases.Count} timeout={timeout.TotalSeconds}s "
            + $"pangram={(pangramEnabled ? $"on(max {pangramMaxCalls})" : "off")} promptVersion={ExternalRewritePrompt.PromptVersion}");

        var rows = new List<PilotV5Row>();
        var pangramCalls = 0;

        foreach (var sample in cases)
        {
            var request = sample.ToRewriteRequest();
            var tone = request.Tone;
            var mustKeep = sample.MustKeep;
            var mustNotClaim = sample.MustNotClaim;
            var riskTier = RiskTier(sample.Category);

            var t0Result = await t0Provider.RewriteAsync(Guid.NewGuid(), request, CancellationToken.None);
            var t0Text = RewritePayload.TryParse(t0Result.ResultJson)?.RewrittenText ?? string.Empty;

            var protectedTerms = (await termProposer.ProposeAsync(sample.InputDraft, CancellationToken.None)).ToList();
            var extReq = new ExternalRewriteRequest(
                sample.Id, sample.InputDraft, tone,
                mustKeep.Select(f => new LedgerItem("Fact", f)).ToList(),
                mustNotClaim.Select(f => new LedgerItem("Boundary", f)).ToList(),
                protectedTerms.Select(p => new LedgerItem("ProtectedTerm", p)).ToList(),
                string.IsNullOrWhiteSpace(sample.SourceType) ? "email" : sample.SourceType, riskTier);

            var ex = await external.RewriteAsync(extReq, CancellationToken.None);
            var exText = ex.Text ?? string.Empty;

            var t0Gates = string.IsNullOrWhiteSpace(t0Text)
                ? null
                : await EvaluateAsync(t0Text, mustKeep, mustNotClaim, protectedTerms, judge, understandability, tierJudge);
            var exGates = ex.Success && !string.IsNullOrWhiteSpace(exText)
                ? await EvaluateAsync(exText, mustKeep, mustNotClaim, protectedTerms, judge, understandability, tierJudge)
                : null;

            int? pT0 = null, pEx = null;
            if (pangram is not null)
            {
                if (t0Gates is { HardPass: true } && pangramCalls < pangramMaxCalls)
                {
                    pT0 = await Measure(pangram, t0Text);
                    pangramCalls++;
                }

                if (exGates is { HardPass: true } && pangramCalls < pangramMaxCalls)
                {
                    pEx = await Measure(pangram, exText);
                    pangramCalls++;
                }
            }

            int? delta = pT0.HasValue && pEx.HasValue ? pEx.Value - pT0.Value : null;
            var qualityWinner = QualityWinner(t0Gates, exGates);
            var detectionWinner = pT0.HasValue && pEx.HasValue ? (pEx.Value < pT0.Value ? "EX1" : pEx.Value > pT0.Value ? "T0" : "tie") : "n/a";

            rows.Add(new PilotV5Row(
                sample.Id, sample.CaseNumber, sample.Category, tone, riskTier,
                t0Text, exText, ex.ProviderName, ex.Success, ex.ErrorCode,
                (long)ex.Latency.TotalMilliseconds, ex.RetryCount,
                t0Gates?.FactPass ?? false, t0Gates?.HardPass ?? false, t0Gates?.NativeSendReady ?? false,
                exGates?.FactPass ?? false, exGates is not null && exGates.Forbid == 0 && !exGates.MeaningChanged,
                exGates is not null && exGates.ProtectedMissing.Count == 0, exGates is not null && exGates.Forbid == 0,
                exGates?.Understandable ?? false, exGates?.NativeSendReady ?? false, exGates?.HardPass ?? false,
                exGates?.MissingFacts ?? Array.Empty<string>(), exGates?.ProtectedMissing ?? Array.Empty<string>(),
                pT0, pEx, delta,
                qualityWinner, detectionWinner,
                Notes(ex, exGates, delta)));

            Console.WriteLine(
                $"{sample.Id}: ext={ex.ProviderName} ok={ex.Success}{(ex.Success ? "" : ":" + ex.ErrorCode)} {ex.Latency.TotalSeconds:F0}s "
                + $"exHard={exGates?.HardPass ?? false} exNative={exGates?.NativeSendReady ?? false} "
                + $"pangram T0={Fmt(pT0)} EX1={Fmt(pEx)} delta={Fmt(delta)}");
        }

        var summary = PilotV5Summary.Create(startedAt, DateTimeOffset.UtcNow, external.ProviderName, rows,
            pangramCalls, deepseek.CallCount, modelCounter.CallCount, saplingCounter.CallCount, pangramEnabled);

        var stamp = startedAt.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(config.OutputDirectory);
        var jsonPath = Path.Combine(config.OutputDirectory, $"{stamp}-external-rewrite-ab.json");
        var mdPath = Path.Combine(config.OutputDirectory, $"{stamp}-external-rewrite-ab.md");
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(new { summary, rows }, jsonOptions));
        await File.WriteAllTextAsync(mdPath, PilotV5Report.Render(summary, rows));

        Console.WriteLine($"Wrote {jsonPath}");
        Console.WriteLine($"Wrote {mdPath}");
        Console.WriteLine(summary.OneLine());
        return 0;
    }

    private static async Task<CandidateGates> EvaluateAsync(
        string text, IReadOnlyList<string> mustKeep, IReadOnlyList<string> mustNotClaim, IReadOnlyList<string> protectedTerms,
        SemanticEvalJudge judge, UnderstandabilityJudge understandability, SendabilityTierJudge tierJudge)
    {
        var protectedMissing = protectedTerms
            .Where(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 3 && !text.Contains(t, StringComparison.Ordinal))
            .ToList();
        var sem = await judge.VerifyAsync(text, mustKeep, mustNotClaim, CancellationToken.None);
        var u = await understandability.JudgeAsync(text, CancellationToken.None);
        var tier = await tierJudge.JudgeAsync(text, CancellationToken.None);
        var missing = sem.Error is null
            ? sem.Facts.Where(f => f.Status is "missing" or "contradicted").Select(f => $"{f.Status}:{f.Fact}").ToList()
            : new List<string> { $"judge_error:{sem.Error}" };
        return new CandidateGates(
            sem.Error is null && sem.FactsReallyPass, sem.RealForbidden, sem.MeaningChanged, protectedMissing,
            u.Understandable, tier.Tier == "sendable", missing);
    }

    private static string QualityWinner(CandidateGates? t0, CandidateGates? ex)
    {
        var t0Ok = t0 is { HardPass: true };
        var exOk = ex is { HardPass: true };
        if (exOk && ex!.NativeSendReady && (!t0Ok || !t0!.NativeSendReady))
        {
            return "EX1";
        }

        if (t0Ok && (!exOk || !ex!.NativeSendReady))
        {
            return "T0";
        }

        return t0Ok && exOk ? "tie" : t0Ok ? "T0" : exOk ? "EX1" : "neither";
    }

    private static string RiskTier(string category) => category.ToLowerInvariant() switch
    {
        var c when c.Contains("medical") => "R3",
        var c when c.Contains("billing") || c.Contains("hr_") || c.Contains("recruiting") => "R3",
        var c when c.Contains("support") => "R2",
        var c when c.Contains("workplace") => "R1",
        _ => "R2",
    };

    private static async Task<int?> Measure(IWritingSignalClient client, string text)
    {
        var r = await client.MeasureAsync(text, CancellationToken.None);
        return r.Available ? r.AiLikePercent : null;
    }

    private static string Notes(ExternalRewriteResult ex, CandidateGates? g, int? delta)
    {
        if (!ex.Success)
        {
            return $"EX1 provider failed ({ex.ErrorCode}).";
        }

        if (g is null || !g.HardPass)
        {
            return $"EX1 failed hard gates (fact={g?.FactPass}, forbid={g?.Forbid}, protMissing={g?.ProtectedMissing.Count}, understandable={g?.Understandable}).";
        }

        var d = delta is null ? "Pangram not measured" : delta < 0 ? $"Pangram {Math.Abs(delta.Value)} lower" : delta > 0 ? $"Pangram {delta.Value} higher" : "Pangram equal";
        return $"EX1 passed hard gates (native-send-ready={g.NativeSendReady}); {d} vs T0.";
    }

    private static string? FirstEnv(params string[] names) =>
        names.Select(Environment.GetEnvironmentVariable).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static int IntEnv(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var v) && v > 0 ? v : fallback;

    private static string Fmt(int? v) => v?.ToString() ?? "-";
}

internal sealed record PilotV5Summary(
    DateTimeOffset StartedAt, DateTimeOffset FinishedAt, string Provider,
    int Cases, int ExternalSuccess, int ExHardPass, int ExNativeSendReady, int T0HardPass, int T0NativeSendReady,
    int ExFactSafe, int QualityWinnerEx, int QualityWinnerT0,
    int PangramPairs, int ExLower, int ExHigher, int ExEqual, int? MeanDelta, int? MedianDelta,
    int? MeanT0, int? MeanEx, long MedianLatencyMs, long P90LatencyMs,
    IReadOnlyDictionary<string, int> ExErrorCodes,
    bool PangramEnabled, int PangramCalls, int DeepSeekCalls, int ModelCalls, int SaplingCalls)
{
    public static PilotV5Summary Create(
        DateTimeOffset startedAt, DateTimeOffset finishedAt, string provider, IReadOnlyList<PilotV5Row> rows,
        int pangramCalls, int deepseekCalls, int modelCalls, int saplingCalls, bool pangramEnabled)
    {
        var exHard = rows.Where(r => r.ExHardPass).ToList();
        var pairs = exHard.Where(r => r.DeltaExternalMinusT0.HasValue).Select(r => r.DeltaExternalMinusT0!.Value).ToList();
        var t0Scores = exHard.Where(r => r.PangramT0.HasValue).Select(r => r.PangramT0!.Value).ToList();
        var exScores = exHard.Where(r => r.PangramExternal.HasValue).Select(r => r.PangramExternal!.Value).ToList();
        var latencies = rows.Where(r => r.ExternalSuccess).Select(r => r.LatencyMs).OrderBy(x => x).ToList();
        var errs = rows.Where(r => !r.ExternalSuccess).GroupBy(r => r.ExternalErrorCode).ToDictionary(g => g.Key, g => g.Count());

        return new PilotV5Summary(
            startedAt, finishedAt, provider,
            rows.Count,
            rows.Count(r => r.ExternalSuccess),
            rows.Count(r => r.ExHardPass),
            rows.Count(r => r.ExHardPass && r.ExNativeSendReady),
            rows.Count(r => r.T0HardPass),
            rows.Count(r => r.T0HardPass && r.T0NativeSendReady),
            rows.Count(r => r.ExFactPass),
            rows.Count(r => r.QualityWinner == "EX1"),
            rows.Count(r => r.QualityWinner == "T0"),
            pairs.Count, pairs.Count(d => d < 0), pairs.Count(d => d > 0), pairs.Count(d => d == 0),
            pairs.Count == 0 ? null : (int?)Math.Round(pairs.Average(), MidpointRounding.AwayFromZero),
            Median(pairs),
            t0Scores.Count == 0 ? null : (int?)Math.Round(t0Scores.Average(), MidpointRounding.AwayFromZero),
            exScores.Count == 0 ? null : (int?)Math.Round(exScores.Average(), MidpointRounding.AwayFromZero),
            latencies.Count == 0 ? 0 : latencies[latencies.Count / 2],
            latencies.Count == 0 ? 0 : latencies[Math.Min(latencies.Count - 1, (int)(latencies.Count * 0.9))],
            errs, pangramEnabled, pangramCalls, deepseekCalls, modelCalls, saplingCalls);
    }

    private static int? Median(IReadOnlyList<int> v)
    {
        if (v.Count == 0)
        {
            return null;
        }

        var s = v.OrderBy(x => x).ToList();
        var m = s.Count / 2;
        return s.Count % 2 == 1 ? s[m] : (int)Math.Round((s[m - 1] + s[m]) / 2.0, MidpointRounding.AwayFromZero);
    }

    public string OneLine() =>
        $"External A/B [{Provider}]: extSuccess={ExternalSuccess}/{Cases}, exHardPass={ExHardPass}/{Cases}, exNative={ExNativeSendReady}, "
        + $"T0HardPass={T0HardPass}, T0native={T0NativeSendReady}, qualityWinner EX1={QualityWinnerEx}/T0={QualityWinnerT0}, "
        + $"pangramPairs={PangramPairs} (exLower={ExLower}/higher={ExHigher}/eq={ExEqual}), meanT0={MeanT0}, meanEX={MeanEx}, meanDelta={(MeanDelta?.ToString() ?? "n/a")}, "
        + $"medLatency={MedianLatencyMs}ms, pangram={PangramCalls}, deepseek={DeepSeekCalls}";
}

internal static class PilotV5Report
{
    public static string Render(PilotV5Summary s, IReadOnlyList<PilotV5Row> rows)
    {
        var lines = new List<string>
        {
            $"# External rewrite-engine A/B (round 6) — T0 vs {s.Provider}",
            "",
            "**Eval-only research pilot.** Not wired into production. T0 = production baseline; EX1 = external provider, one-shot, same draft/tone/ledgers. Facts always extracted from original/T0 (EX1's self-reported claims ignored). Hard gates = Fact + Boundary + ProtectedTerm + Forbidden + Understandability; NativeSendReady recorded. Pangram once per hard-gate survivor.",
            "Per the round-6 doc: a low Pangram on non-native text is NOT success — EX1 must be fact-safe AND more natural to count, with Pangram as observation.",
            "",
            $"Started: {s.StartedAt:O}  Finished: {s.FinishedAt:O}  ·  prompt {ExternalRewritePrompt.PromptVersion}",
            "",
            "## Headline — four questions (§11)",
            "",
            $"1. **Is EX1 fact-safe?** hard-gate pass **{s.ExHardPass}/{s.Cases}** (fact-safe {s.ExFactSafe}/{s.Cases}); T0 hard-gate pass {s.T0HardPass}/{s.Cases}.",
            $"2. **Is EX1 more natural than T0?** native-send-ready EX1 **{s.ExNativeSendReady}** vs T0 **{s.T0NativeSendReady}**; quality-winner EX1 {s.QualityWinnerEx} / T0 {s.QualityWinnerT0}.",
        };

        if (!s.PangramEnabled)
        {
            lines.Add("3. **Does EX1 lower Pangram?** Pangram disabled this run.");
        }
        else if (s.PangramPairs == 0)
        {
            lines.Add("3. **Does EX1 lower Pangram?** No hard-gate-passing pair to compare.");
        }
        else
        {
            lines.Add($"3. **Does EX1 lower Pangram?** of {s.PangramPairs} pairs: lower {s.ExLower}, higher {s.ExHigher}, equal {s.ExEqual} (mean T0 {s.MeanT0} → EX1 {s.MeanEx}, mean Δ **{Signed(s.MeanDelta)}**, median **{Signed(s.MedianDelta)}**).");
        }

        lines.Add($"4. **If better, why?** see NativeSendReady vs Δ per case below — a drop with native-send-ready=no is a non-native artifact, not a real win.");
        lines.Add("");
        lines.Add($"Latency: median **{s.MedianLatencyMs} ms**, p90 **{s.P90LatencyMs} ms**. External success {s.ExternalSuccess}/{s.Cases}"
            + (s.ExErrorCodes.Count > 0 ? " — failures: " + string.Join(", ", s.ExErrorCodes.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}×{kv.Value}")) : "") + ".");
        lines.Add("");
        lines.Add("## Cost / calls");
        lines.Add("");
        lines.Add($"External tasks: **{s.Cases}** ({s.Provider}) · Pangram: **{s.PangramCalls}** · DeepSeek (gates+terms): **{s.DeepSeekCalls}** · DeepSeek model (T0): **{s.ModelCalls}** · Sapling (engine gate): **{s.SaplingCalls}**");
        lines.Add("");
        lines.Add("## Per-case");
        lines.Add("");
        lines.Add("| Case | Cat | ext ok | lat(s) | ex fact | ex bound | ex prot | ex underst | ex native | ex hard | Pangram T0 | Pangram EX1 | Δ | quality | detection |");
        lines.Add("| --- | --- | :---: | ---: | :---: | :---: | :---: | :---: | :---: | :---: | ---: | ---: | ---: | :---: | :---: |");
        foreach (var r in rows)
        {
            lines.Add(string.Join(" | ", new[]
            {
                r.CaseId, r.Category,
                r.ExternalSuccess ? "yes" : "no",
                (r.LatencyMs / 1000).ToString(),
                YN(r.ExFactPass), YN(r.ExBoundaryPass), YN(r.ExProtectedPass), YN(r.ExUnderstandable), YN(r.ExNativeSendReady), YN(r.ExHardPass),
                r.PangramT0?.ToString() ?? "-", r.PangramExternal?.ToString() ?? "-", Signed(r.DeltaExternalMinusT0),
                r.QualityWinner, r.DetectionWinner,
            }));
        }

        lines.Add("");
        lines.Add("## Text comparison (T0 vs EX1)");
        foreach (var r in rows)
        {
            lines.Add("");
            lines.Add($"### {r.CaseId} — {r.Category} (ext ok={r.ExternalSuccess}, hard={r.ExHardPass}, native={r.ExNativeSendReady})");
            if (r.ExternalErrorCode.Length > 0)
            {
                lines.Add($"_EX1 error: {r.ExternalErrorCode}_");
            }

            if (r.ExMissingFacts.Count > 0)
            {
                lines.Add($"_EX1 missing/contradicted: {string.Join("; ", r.ExMissingFacts).Replace("|", "/", StringComparison.Ordinal)}_");
            }

            lines.Add("");
            lines.Add("**T0:**");
            lines.Add("> " + (string.IsNullOrWhiteSpace(r.T0Text) ? "(no output)" : r.T0Text.Replace("\n", "\n> ", StringComparison.Ordinal)));
            lines.Add("");
            lines.Add("**EX1:**");
            lines.Add("> " + (string.IsNullOrWhiteSpace(r.ExternalText) ? "(no output)" : r.ExternalText.Replace("\n", "\n> ", StringComparison.Ordinal)));
        }

        lines.Add("");
        return string.Join("\n", lines) + "\n";
    }

    private static string YN(bool b) => b ? "y" : "n";

    private static string Signed(int? v) => v is null ? "-" : v.Value > 0 ? $"+{v.Value}" : v.Value.ToString();
}
