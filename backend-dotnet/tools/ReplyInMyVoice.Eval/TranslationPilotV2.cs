using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.RewriteEngine;
using ReplyInMyVoice.Infrastructure.Providers;

// Round-2 translation pilot (T3 native-repair). EVAL-ONLY; triggered by T3_PILOT=1 in Program.cs.
// See plans/translation-roundtrip-pilot.md + the round-2 design.
//
// Round-1 (TA_raw) let Youdao be the FINAL author -> Pangram dropped hard but the text read like
// machine translation, and business nouns drifted (saucer->tea tray). Round-2 changes the ORDER:
//   T0      = production baseline (engine v0, internal Sapling gate, no translation)
//   TA_raw  = T0 -> mask -> Youdao en->zh-CHS->en -> unmask   (a PERTURBATION DRAFT ONLY, not final)
//   T3      = DeepSeek "native English repair" of TA_raw, anchored to the ORIGINAL draft + a
//             ProtectedTermLedger (TA_raw is treated as untrustworthy for facts); final IF gates pass
//   T3b     = one minimal-edit repair of T3 targeting only the gate-flagged issues
// Gates: semantic fact + forbidden + protected-term presence + a SendabilityGate (translationese /
// garble / wrong-agent / broken sign-off). Pangram is read once per gate survivor. The report
// separates "Pangram lowered" from "send-ready", because a low score on unreadable text is not a win.
//
// Honest prior: re-authoring in English re-adds the LLM surface signal, so T3 Pangram may climb back
// toward T0. The point is to measure whether ANY drop survives once the text is genuinely readable.

// Minimal DeepSeek (OpenAI-compatible) chat client returning the raw message content. Shared by the
// span proposer, native-repair, and sendability judge so the HTTP/auth/uri boilerplate lives once.
internal sealed class DeepSeekChatClient(HttpClient httpClient, string apiKey, string model, string baseUrl, TimeSpan timeout)
{
    public int CallCount { get; private set; }

    public async Task<string?> CompleteAsync(string system, string user, int maxTokens, double temperature, CancellationToken cancellationToken)
    {
        CallCount += 1;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["temperature"] = temperature,
            ["response_format"] = new { type = "json_object" },
            ["max_tokens"] = maxTokens,
            ["messages"] = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user },
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
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }
        catch (Exception e) when (e is OperationCanceledException or HttpRequestException or JsonException
            or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            return null;
        }
    }

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

// Proposes business objects / status phrases / action phrases that must survive a re-edit unchanged
// (round-1 showed dates/IDs are not enough: "saucer"->"tea tray" and "jar lid"->"bottle cap" slipped
// through). Every proposed term is verified to be an exact substring of the source; others discarded.
internal sealed class ProtectedTermProposer(DeepSeekChatClient chat) : ReplyInMyVoice.Domain.Quality.IProtectedTermProposer
{
    private const string SystemPrompt =
        "You list the terms that MUST be preserved verbatim when an email draft is re-edited, because "
        + "rewording them would change the meaning. Include concrete business objects (e.g. 'saucer', "
        + "'blender jar lid', 'dish rack'), status phrases (e.g. 'marked delivered', 'pending'), and the "
        + "specific action being offered or refused (e.g. 'one no-cost replacement', 'cannot refund the "
        + "full order'). Exclude greetings, filler, and generic words. Each term MUST be copied EXACTLY "
        + "as a substring of the provided text, max ~4 words each, at most 20 terms. Return JSON: "
        + "{\"terms\":[\"...\"]}.";

    public async Task<IReadOnlyList<string>> ProposeAsync(string sourceText, CancellationToken cancellationToken)
    {
        var content = await chat.CompleteAsync(SystemPrompt, sourceText, 700, 0, cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<string>();
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("terms", out var terms) || terms.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            // Only keep terms that genuinely appear verbatim in the source — the proposer must not
            // invent spans.
            return terms.EnumerateArray()
                .Select(t => (t.GetString() ?? string.Empty).Trim())
                .Where(t => t.Length >= 2 && sourceText.Contains(t, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Take(20)
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}

// Re-authors a machine-translated draft into clean native English with MINIMAL changes, anchored to
// the original message + protected terms (the MT draft is explicitly untrusted for facts).
internal sealed class NativeRepairClient(DeepSeekChatClient chat)
{
    private static string SystemPrompt(string tone) =>
        "You are a native English editor. The MACHINE-TRANSLATED DRAFT below came from a translation "
        + "round-trip, so it may have awkward phrasing, wrong prepositions, mistranslated business nouns, "
        + "scrambled numbers/times, wrong subject/agent, or a broken sign-off. Produce clean, natural, "
        + "send-ready English with MINIMAL changes: keep the draft's sentence order and wording wherever "
        + "it is already acceptable, and fix only what is wrong. The machine-translated draft is NOT a "
        + "reliable source of facts. The SOURCE OF TRUTH is the ORIGINAL MESSAGE plus the PROTECTED TERMS; "
        + "restore every protected term to its exact original form. Do not add facts, promises, apologies, "
        + "greetings, or sign-offs that the original does not support. Keep a " + tone + " tone. "
        + "Return JSON: {\"rewrittenText\":\"...\"}.";

    public Task<string?> RepairAsync(string taRaw, string originalDraft, IReadOnlyList<string> protectedTerms, string tone, CancellationToken cancellationToken)
    {
        var user =
            "ORIGINAL MESSAGE (source of truth for facts):\n" + originalDraft
            + "\n\nPROTECTED TERMS (preserve each exactly):\n" + Bullets(protectedTerms)
            + "\n\nMACHINE-TRANSLATED DRAFT (clean this up; do NOT trust its facts):\n" + taRaw;
        return RunAsync(SystemPrompt(tone), user, cancellationToken);
    }

    public Task<string?> RepairMinimalAsync(string text, string originalDraft, IReadOnlyList<string> protectedTerms, string tone, IReadOnlyList<string> issues, CancellationToken cancellationToken)
    {
        var system =
            "You are a native English editor doing a MINIMAL repair. Fix ONLY the listed issues and change "
            + "nothing else. Preserve every protected term exactly. Keep a " + tone + " tone. The original "
            + "message is the source of truth for facts. Return JSON: {\"rewrittenText\":\"...\"}.";
        var user =
            "ORIGINAL MESSAGE (source of truth):\n" + originalDraft
            + "\n\nPROTECTED TERMS (preserve each exactly):\n" + Bullets(protectedTerms)
            + "\n\nISSUES TO FIX (only these):\n" + Bullets(issues)
            + "\n\nTEXT TO REPAIR:\n" + text;
        return RunAsync(system, user, cancellationToken);
    }

    private async Task<string?> RunAsync(string system, string user, CancellationToken cancellationToken)
    {
        var content = await chat.CompleteAsync(system, user, 1400, 0.2, cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            return doc.RootElement.TryGetProperty("rewrittenText", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Bullets(IReadOnlyList<string> items) =>
        items.Count == 0 ? "(none)" : string.Join("\n", items.Select(i => "- " + i));
}

internal sealed record SendabilityVerdict(bool Sendable, IReadOnlyList<string> Issues, string? Error = null);

// The SendabilityGate the round-1 retro asked for: catches machine-translation artifacts the
// fact-only semantic judge let through (case 002 "create the goods", case 036 scrambled phone/time).
internal sealed class SendabilityJudge(DeepSeekChatClient chat)
{
    private const string SystemPrompt =
        "You are a strict editor deciding if an email is send-ready, fluent, native English. Mark it NOT "
        + "sendable if it has any of: machine-translation artifacts, awkward/wrong prepositions, wrong "
        + "subject or agent (e.g. the sender appears to request their OWN refund), garbled or scrambled "
        + "clauses, a broken or nonsensical sign-off, scrambled phone numbers / times / dates, or doubled "
        + "words. A clear, professional, natural email is sendable. Be strict about fluency but do not "
        + "judge facts. Return JSON: {\"sendable\":true,\"issues\":[\"short issue\"]}.";

    public async Task<SendabilityVerdict> JudgeAsync(string text, CancellationToken cancellationToken)
    {
        var content = await chat.CompleteAsync(SystemPrompt, text, 500, 0, cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new SendabilityVerdict(false, Array.Empty<string>(), "judge_failed");
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var sendable = root.TryGetProperty("sendable", out var s) && s.ValueKind == JsonValueKind.True;
            var issues = root.TryGetProperty("issues", out var iss) && iss.ValueKind == JsonValueKind.Array
                ? iss.EnumerateArray().Select(i => i.GetString() ?? string.Empty).Where(i => i.Length > 0).ToList()
                : new List<string>();
            return new SendabilityVerdict(sendable, issues);
        }
        catch (JsonException)
        {
            return new SendabilityVerdict(false, Array.Empty<string>(), "judge_parse_failed");
        }
    }
}

internal sealed record PilotV2Row(
    string CaseId,
    int CaseNumber,
    string Category,
    string Tone,
    bool T0HasOutput,
    string T0Text,
    bool SentinelPass,
    int AnchorCount,
    string TaRawText,
    int ProtectedTermCount,
    bool T3Generated,
    string T3Text,
    bool UsedT3b,
    bool T3FactPass,
    int T3Forbid,
    bool T3MeaningChanged,
    IReadOnlyList<string> T3ProtectedMissing,
    bool T3Sendable,
    IReadOnlyList<string> T3SendIssues,
    bool T3GatePass,
    int? PangramT0,
    int? PangramT3,
    int? DeltaT3MinusT0,
    string ChosenVariant,
    string FallbackReason,
    IReadOnlyList<string> T3MissingFacts,
    string Notes);

internal static class TranslationPilotV2Runner
{
    public static async Task<int> RunAsync(
        FactReconstructRewriteProvider provider,
        CountingRewriteModelClient modelCounter,
        CountingWritingSignalClient saplingCounter,
        IReadOnlyList<EvalCase> cases,
        EvalConfig config,
        string apiKey,
        DateTimeOffset startedAt)
    {
        var youdaoKey = FirstEnv("YOUDAO_APP_KEY", "AppID", "YouDao_API_KEY");
        var youdaoSecret = FirstEnv("YOUDAO_APP_SECRET", "AppSecret");
        var youdaoUrl = Environment.GetEnvironmentVariable("YOUDAO_API_URL") ?? "https://openapi.youdao.com/api";
        if (string.IsNullOrWhiteSpace(youdaoKey) || string.IsNullOrWhiteSpace(youdaoSecret))
        {
            Console.Error.WriteLine("T3_PILOT: missing Youdao credentials (YOUDAO_APP_KEY/AppID + YOUDAO_APP_SECRET/AppSecret).");
            return 2;
        }

        var youdaoMaxCalls = IntEnv("YOUDAO_MAX_CALLS", 40);
        var pangramMaxCalls = IntEnv("PANGRAM_MAX_CALLS", 0);
        var pangramKey = Environment.GetEnvironmentVariable("PANGRAM_API_KEY");
        var pangramEnabled = pangramMaxCalls > 0 && !string.IsNullOrWhiteSpace(pangramKey);

        using var youdaoHttp = new HttpClient();
        using var deepseekHttp = new HttpClient();
        using var judgeHttp = new HttpClient();
        using var pangramHttp = new HttpClient();

        var youdao = new YoudaoTranslationClient(youdaoHttp, youdaoKey!, youdaoSecret!, youdaoUrl, TimeSpan.FromSeconds(30));
        var deepseek = new DeepSeekChatClient(deepseekHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));
        var proposer = new ProtectedTermProposer(deepseek);
        var repair = new NativeRepairClient(deepseek);
        var sendability = new SendabilityJudge(deepseek);
        var judge = new SemanticEvalJudge(judgeHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));
        var pangram = pangramEnabled
            ? new PangramWritingSignalClient(pangramHttp, pangramKey!, TimeSpan.FromSeconds(45))
            : null;

        Console.WriteLine(
            $"T3 pilot: cases={cases.Count} youdaoMax={youdaoMaxCalls} pangram={(pangramEnabled ? $"on(max {pangramMaxCalls})" : "off")} model={config.Model}");

        var rows = new List<PilotV2Row>();
        var pangramCalls = 0;

        foreach (var sample in cases)
        {
            var request = sample.ToRewriteRequest();
            var tone = request.Tone;
            var mustKeep = sample.MustKeep;
            var mustNotClaim = sample.MustNotClaim;

            var t0Result = await provider.RewriteAsync(Guid.NewGuid(), request, CancellationToken.None);
            var t0Text = RewritePayload.TryParse(t0Result.ResultJson)?.RewrittenText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(t0Text))
            {
                rows.Add(EmptyRow(sample, tone, t0Result.ErrorCode));
                Console.WriteLine($"{sample.Id}: T0 produced no output ({t0Result.ErrorCode ?? "unknown"}); skipped.");
                continue;
            }

            // ProtectedTermLedger: ledger anchors present in T0 + DeepSeek-proposed business spans.
            var ledger = FactLedgerExtractor.Extract(request);
            var protectedTerms = BuildProtectedTerms(t0Text, ledger, await proposer.ProposeAsync(sample.InputDraft, CancellationToken.None));

            // TA_raw = perturbation draft (mask -> Youdao -> unmask). Not a final candidate.
            var masked = AnchorMasker.Mask(t0Text, ledger, mustKeep, mustNotClaim);
            var taRaw = string.Empty;
            var sentinelPass = false;
            var t3Generated = false;
            var t3Text = string.Empty;
            var usedT3b = false;
            var t3FactPass = false;
            var t3Forbid = 0;
            var t3Meaning = false;
            IReadOnlyList<string> protectedMissing = Array.Empty<string>();
            var sendable = false;
            IReadOnlyList<string> sendIssues = Array.Empty<string>();
            IReadOnlyList<string> t3Missing = Array.Empty<string>();
            string fallbackReason;

            if (youdao.CallCount + 2 > youdaoMaxCalls)
            {
                fallbackReason = "youdao_budget_exhausted";
            }
            else
            {
                var toZh = await youdao.TranslateAsync(masked.MaskedText, "en", "zh-CHS", CancellationToken.None);
                var backEn = toZh.Success
                    ? await youdao.TranslateAsync(toZh.Text, "zh-CHS", "en", CancellationToken.None)
                    : new TranslateResult(false, string.Empty, toZh.ErrorCode);
                if (!backEn.Success)
                {
                    fallbackReason = backEn.ErrorCode ?? "youdao_failed";
                }
                else
                {
                    var unmask = AnchorMasker.Unmask(backEn.Text, masked.Map);
                    taRaw = unmask.Restored;
                    sentinelPass = unmask.IntegrityOk;
                    if (!sentinelPass)
                    {
                        // Per design: sentinel broken -> do not repair raw TA, fall back to T0.
                        fallbackReason = "sentinel_broken";
                    }
                    else
                    {
                        // T3: native repair of TA_raw, anchored to the original draft + protected terms.
                        var repaired = await repair.RepairAsync(taRaw, sample.InputDraft, protectedTerms, tone, CancellationToken.None);
                        if (string.IsNullOrWhiteSpace(repaired))
                        {
                            fallbackReason = "t3_repair_failed";
                        }
                        else
                        {
                            t3Generated = true;
                            t3Text = repaired;
                            var gate = await GateAsync(t3Text, mustKeep, mustNotClaim, protectedTerms, judge, sendability);
                            t3FactPass = gate.FactPass;
                            t3Forbid = gate.Forbid;
                            t3Meaning = gate.MeaningChanged;
                            protectedMissing = gate.ProtectedMissing;
                            sendable = gate.Sendable;
                            sendIssues = gate.SendIssues;
                            t3Missing = gate.MissingFacts;

                            if (!gate.Pass)
                            {
                                // One minimal T3b repair targeting only the flagged issues.
                                var issues = BuildIssueList(gate);
                                var t3b = await repair.RepairMinimalAsync(t3Text, sample.InputDraft, protectedTerms, tone, issues, CancellationToken.None);
                                if (!string.IsNullOrWhiteSpace(t3b))
                                {
                                    usedT3b = true;
                                    t3Text = t3b;
                                    var gate2 = await GateAsync(t3Text, mustKeep, mustNotClaim, protectedTerms, judge, sendability);
                                    t3FactPass = gate2.FactPass;
                                    t3Forbid = gate2.Forbid;
                                    t3Meaning = gate2.MeaningChanged;
                                    protectedMissing = gate2.ProtectedMissing;
                                    sendable = gate2.Sendable;
                                    sendIssues = gate2.SendIssues;
                                    t3Missing = gate2.MissingFacts;
                                    fallbackReason = gate2.Pass ? string.Empty : gate2.Reason;
                                }
                                else
                                {
                                    fallbackReason = gate.Reason;
                                }
                            }
                            else
                            {
                                fallbackReason = string.Empty;
                            }
                        }
                    }
                }
            }

            var t3GatePass = t3Generated && string.IsNullOrEmpty(fallbackReason)
                && t3FactPass && t3Forbid == 0 && !t3Meaning && protectedMissing.Count == 0 && sendable;
            var chosen = t3GatePass ? "T3" : "T0";

            int? pangramT0 = null;
            int? pangramT3 = null;
            if (pangram is not null)
            {
                if (pangramCalls < pangramMaxCalls)
                {
                    pangramT0 = await MeasureAsync(pangram, t0Text);
                    pangramCalls++;
                }

                if (t3GatePass && !string.Equals(t3Text, t0Text, StringComparison.Ordinal) && pangramCalls < pangramMaxCalls)
                {
                    pangramT3 = await MeasureAsync(pangram, t3Text);
                    pangramCalls++;
                }
                else
                {
                    pangramT3 = pangramT0;
                }
            }

            int? delta = pangramT0.HasValue && pangramT3.HasValue ? pangramT3.Value - pangramT0.Value : null;

            rows.Add(new PilotV2Row(
                sample.Id, sample.CaseNumber, sample.Category, tone,
                true, t0Text,
                sentinelPass, masked.AnchorCount, taRaw,
                protectedTerms.Count,
                t3Generated, t3Text, usedT3b,
                t3FactPass, t3Forbid, t3Meaning, protectedMissing, sendable, sendIssues,
                t3GatePass,
                pangramT0, pangramT3, delta,
                chosen, t3GatePass ? string.Empty : fallbackReason,
                t3Missing,
                Notes(t3GatePass, fallbackReason, delta, sendable)));

            Console.WriteLine(
                $"{sample.Id}: anchors={masked.AnchorCount} prot={protectedTerms.Count} sentinel={(sentinelPass ? "ok" : "BROKEN")} "
                + $"t3b={(usedT3b ? "y" : "n")} t3Gate={(t3GatePass ? "pass" : "fallback:" + fallbackReason)} "
                + $"sendable={sendable} pangram T0={Fmt(pangramT0)} T3={Fmt(pangramT3)} delta={Fmt(delta)}");
        }

        var summary = PilotV2Summary.Create(startedAt, DateTimeOffset.UtcNow, rows,
            youdao.CallCount, pangramCalls, deepseek.CallCount, modelCounter.CallCount, saplingCounter.CallCount, pangramEnabled);

        var stamp = startedAt.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(config.OutputDirectory);
        var jsonPath = Path.Combine(config.OutputDirectory, $"{stamp}-t3-translation-pilot.json");
        var mdPath = Path.Combine(config.OutputDirectory, $"{stamp}-t3-translation-pilot.md");
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(new { summary, rows }, jsonOptions));
        await File.WriteAllTextAsync(mdPath, PilotV2Report.Render(summary, rows));

        Console.WriteLine($"Wrote {jsonPath}");
        Console.WriteLine($"Wrote {mdPath}");
        Console.WriteLine(summary.OneLine());
        return 0;
    }

    private sealed record GateResult(
        bool FactPass, int Forbid, bool MeaningChanged, IReadOnlyList<string> ProtectedMissing,
        bool Sendable, IReadOnlyList<string> SendIssues, IReadOnlyList<string> MissingFacts)
    {
        public bool Pass => FactPass && Forbid == 0 && !MeaningChanged && ProtectedMissing.Count == 0 && Sendable;

        public string Reason =>
            !Sendable ? "t3_not_sendable"
            : ProtectedMissing.Count > 0 ? "t3_protected_term_lost"
            : !FactPass ? "t3_facts_drifted"
            : Forbid > 0 ? "t3_forbidden_violation"
            : MeaningChanged ? "t3_meaning_changed"
            : string.Empty;
    }

    private static async Task<GateResult> GateAsync(
        string t3, IReadOnlyList<string> mustKeep, IReadOnlyList<string> mustNotClaim,
        IReadOnlyList<string> protectedTerms, SemanticEvalJudge judge, SendabilityJudge sendability)
    {
        // Exact-presence check for short protected spans (objects/IDs/amounts). Longer phrases are
        // left to the semantic judge, which accepts equivalent status/action wording.
        var protectedMissing = protectedTerms
            .Where(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 3
                && !t3.Contains(t, StringComparison.Ordinal))
            .ToList();

        var sem = await judge.VerifyAsync(t3, mustKeep, mustNotClaim, CancellationToken.None);
        var send = await sendability.JudgeAsync(t3, CancellationToken.None);
        var missing = sem.Error is null
            ? sem.Facts.Where(f => f.Status is "missing" or "contradicted").Select(f => $"{f.Status}:{f.Fact}").ToList()
            : new List<string> { $"judge_error:{sem.Error}" };

        return new GateResult(
            sem.Error is null && sem.FactsReallyPass,
            sem.RealForbidden,
            sem.MeaningChanged,
            protectedMissing,
            send.Sendable,
            send.Issues,
            missing);
    }

    private static List<string> BuildIssueList(GateResult gate)
    {
        var issues = new List<string>();
        foreach (var t in gate.ProtectedMissing)
        {
            issues.Add($"Restore this exact term from the original: \"{t}\"");
        }

        foreach (var m in gate.MissingFacts)
        {
            issues.Add($"Fact problem: {m}");
        }

        if (!gate.Sendable)
        {
            foreach (var s in gate.SendIssues)
            {
                issues.Add($"Fluency problem: {s}");
            }
        }

        if (gate.MeaningChanged)
        {
            issues.Add("A fact's meaning changed vs the original; restore the original meaning.");
        }

        return issues.Count > 0 ? issues : new List<string> { "Make the text read as fluent, send-ready native English." };
    }

    private static List<string> BuildProtectedTerms(string t0Text, RewriteFactLedger ledger, IReadOnlyList<string> proposed)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fact in ledger.Facts)
        {
            if (fact.Category is RewriteFactCategory.Amount or RewriteFactCategory.Identifier
                or RewriteFactCategory.DateOrDeadline or RewriteFactCategory.Person
                or RewriteFactCategory.Count
                && t0Text.Contains(fact.Text, StringComparison.Ordinal))
            {
                set.Add(fact.Text.Trim());
            }
        }

        foreach (var term in proposed)
        {
            set.Add(term);
        }

        return set.Where(t => t.Length >= 2).Take(30).ToList();
    }

    private static async Task<int?> MeasureAsync(IWritingSignalClient client, string text)
    {
        var result = await client.MeasureAsync(text, CancellationToken.None);
        return result.Available ? result.AiLikePercent : null;
    }

    private static PilotV2Row EmptyRow(EvalCase sample, string tone, string? errorCode) => new(
        sample.Id, sample.CaseNumber, sample.Category, tone,
        false, string.Empty,
        false, 0, string.Empty, 0,
        false, string.Empty, false,
        false, 0, false, Array.Empty<string>(), false, Array.Empty<string>(),
        false,
        null, null, null,
        "T0", $"t0_no_output:{errorCode ?? "unknown"}",
        Array.Empty<string>(),
        "T0 produced no output; T3 not attempted.");

    private static string Notes(bool t3GatePass, string fallbackReason, int? delta, bool sendable)
    {
        if (!t3GatePass)
        {
            return $"T3 fell back to T0 ({fallbackReason}).";
        }

        var pangramNote = delta is null ? "Pangram not measured"
            : delta < 0 ? $"Pangram dropped {Math.Abs(delta.Value)} pts"
            : delta > 0 ? $"Pangram rose {delta.Value} pts"
            : "Pangram unchanged";
        return $"T3 chosen (sendable={sendable}); {pangramNote} vs T0.";
    }

    private static string? FirstEnv(params string[] names) =>
        names.Select(Environment.GetEnvironmentVariable).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static int IntEnv(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var v) && v >= 0 ? v : fallback;

    private static string Fmt(int? v) => v?.ToString() ?? "-";
}

internal sealed record PilotV2Summary(
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    int Cases,
    int T0WithOutput,
    int SentinelPass,
    int T3Generated,
    int T3GatePass,
    int Sendable,
    int Fallback,
    IReadOnlyDictionary<string, int> FallbackReasons,
    int PangramPairs,
    int PangramLower,
    int PangramHigher,
    int PangramEqual,
    int PangramLowerAndSendable,
    int? MeanDelta,
    int? MedianDelta,
    bool PangramEnabled,
    int YoudaoCalls,
    int PangramCalls,
    int DeepSeekCalls,
    int ModelCalls,
    int SaplingCalls)
{
    public static PilotV2Summary Create(
        DateTimeOffset startedAt, DateTimeOffset finishedAt, IReadOnlyList<PilotV2Row> rows,
        int youdaoCalls, int pangramCalls, int deepseekCalls, int modelCalls, int saplingCalls, bool pangramEnabled)
    {
        var withOutput = rows.Where(r => r.T0HasOutput).ToList();
        var pairs = withOutput.Where(r => r.T3GatePass && r.DeltaT3MinusT0.HasValue).Select(r => r.DeltaT3MinusT0!.Value).ToList();
        var fallbackReasons = withOutput.Where(r => !r.T3GatePass)
            .GroupBy(r => r.FallbackReason).ToDictionary(g => g.Key, g => g.Count());

        return new PilotV2Summary(
            startedAt, finishedAt,
            rows.Count,
            withOutput.Count,
            rows.Count(r => r.SentinelPass),
            rows.Count(r => r.T3Generated),
            rows.Count(r => r.T3GatePass),
            rows.Count(r => r.T3GatePass && r.T3Sendable),
            withOutput.Count(r => !r.T3GatePass),
            fallbackReasons,
            pairs.Count,
            pairs.Count(d => d < 0),
            pairs.Count(d => d > 0),
            pairs.Count(d => d == 0),
            withOutput.Count(r => r.T3GatePass && r.T3Sendable && r.DeltaT3MinusT0 < 0),
            pairs.Count == 0 ? null : (int?)Math.Round(pairs.Average(), MidpointRounding.AwayFromZero),
            Median(pairs),
            pangramEnabled,
            youdaoCalls, pangramCalls, deepseekCalls, modelCalls, saplingCalls);
    }

    private static int? Median(IReadOnlyList<int> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (int)Math.Round((sorted[mid - 1] + sorted[mid]) / 2.0, MidpointRounding.AwayFromZero);
    }

    public string OneLine() =>
        $"T3 pilot: T0out={T0WithOutput}/{Cases}, sentinel={SentinelPass}/{Cases}, t3gen={T3Generated}, "
        + $"t3GatePass={T3GatePass}/{Cases}, pangramPairs={PangramPairs} (lower={PangramLower}/higher={PangramHigher}/equal={PangramEqual}), "
        + $"**lower+sendable={PangramLowerAndSendable}**, meanDelta={(MeanDelta?.ToString() ?? "n/a")}, "
        + $"youdao={YoudaoCalls}, pangram={PangramCalls}, deepseek={DeepSeekCalls}";
}

internal static class PilotV2Report
{
    public static string Render(PilotV2Summary s, IReadOnlyList<PilotV2Row> rows)
    {
        var lines = new List<string>
        {
            "# T3 native-repair translation pilot (round 2)",
            "",
            "**Eval-only research pilot.** Not wired into the production engine. T0 = production baseline.",
            "TA_raw = Youdao en→zh-CHS→en perturbation draft (NOT a candidate). T3 = DeepSeek native repair of TA_raw, anchored to the original draft + protected terms. Gates: facts + boundary + protected-term presence + SendabilityGate. Pangram read once per gate survivor.",
            "",
            $"Started: {s.StartedAt:O}  Finished: {s.FinishedAt:O}",
            "",
            "## Headline — can we keep the Pangram drop AND be send-ready?",
            "",
        };

        if (!s.PangramEnabled)
        {
            lines.Add("Pangram disabled — only the fact/protected/sendability gate ran.");
        }
        else if (s.PangramPairs == 0)
        {
            lines.Add("No T3 passed the gate as a distinct output, so there is no T0-vs-T3 Pangram pair.");
        }
        else
        {
            lines.Add($"- T3 passed the full gate (fact + boundary + protected + **send-ready**) in **{s.T3GatePass}/{s.Cases}**.");
            lines.Add($"- Of the {s.PangramPairs} measured T3 outputs, Pangram **lowered in {s.PangramLower}**, rose in {s.PangramHigher}, unchanged in {s.PangramEqual} (mean Δ **{Signed(s.MeanDelta)}**, median **{Signed(s.MedianDelta)}**).");
            lines.Add($"- **Both lower AND send-ready: {s.PangramLowerAndSendable}/{s.Cases}** ← the only result that would count as a real win.");
        }

        lines.Add("");
        lines.Add("## Gate / safety");
        lines.Add("");
        lines.Add($"- T0 with output: **{s.T0WithOutput}/{s.Cases}**");
        lines.Add($"- Sentinel held (TA_raw usable): **{s.SentinelPass}/{s.Cases}**");
        lines.Add($"- T3 generated: **{s.T3Generated}** · T3 send-ready & gate-pass: **{s.T3GatePass}** · used T3b minimal repair: {rows.Count(r => r.UsedT3b)}");
        lines.Add($"- Fell back to T0: **{s.Fallback}/{s.T0WithOutput}**" + (s.FallbackReasons.Count > 0
            ? " — " + string.Join(", ", s.FallbackReasons.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}×{kv.Value}"))
            : string.Empty));
        lines.Add("");
        lines.Add("## Cost / calls");
        lines.Add("");
        lines.Add($"Youdao: **{s.YoudaoCalls}** · Pangram: **{s.PangramCalls}** · DeepSeek (propose+repair+sendability+judge): **{s.DeepSeekCalls}** · DeepSeek model (T0): **{s.ModelCalls}** · Sapling (engine gate): **{s.SaplingCalls}**");
        lines.Add("");
        lines.Add("## Per-case");
        lines.Add("");
        lines.Add("| Case | Category | Sentinel | T3b | Send-ready | T3 gate | Pangram T0 | Pangram T3 | Δ | Chosen | Fallback / issues |");
        lines.Add("| --- | --- | :---: | :---: | :---: | :---: | ---: | ---: | ---: | :---: | --- |");
        foreach (var r in rows)
        {
            var detail = r.T3GatePass
                ? string.Empty
                : (r.FallbackReason
                   + (r.T3ProtectedMissing.Count > 0 ? " | prot:" + string.Join(";", r.T3ProtectedMissing) : string.Empty)
                   + (r.T3SendIssues.Count > 0 ? " | send:" + string.Join(";", r.T3SendIssues) : string.Empty)
                   + (r.T3MissingFacts.Count > 0 ? " | facts:" + string.Join(";", r.T3MissingFacts) : string.Empty));
            lines.Add(string.Join(" | ", new[]
            {
                r.CaseId, r.Category,
                r.SentinelPass ? "ok" : "broken",
                r.UsedT3b ? "y" : "-",
                r.T3Sendable ? "yes" : "no",
                r.T3GatePass ? "pass" : "fallback",
                r.PangramT0?.ToString() ?? "-",
                r.PangramT3?.ToString() ?? "-",
                Signed(r.DeltaT3MinusT0),
                r.ChosenVariant,
                detail.Replace("|", "/", StringComparison.Ordinal),
            }));
        }

        lines.Add("");
        lines.Add("## Text comparison (T0 → TA_raw → T3)");
        foreach (var r in rows.Where(r => r.T0HasOutput))
        {
            lines.Add("");
            lines.Add($"### {r.CaseId} — {r.Category} (chosen: {r.ChosenVariant})");
            lines.Add("");
            lines.Add("**T0 (baseline):**");
            lines.Add("> " + r.T0Text.Replace("\n", "\n> ", StringComparison.Ordinal));
            if (r.SentinelPass)
            {
                lines.Add("");
                lines.Add("**TA_raw (Youdao round-trip, perturbation draft):**");
                lines.Add("> " + r.TaRawText.Replace("\n", "\n> ", StringComparison.Ordinal));
            }

            if (r.T3Generated)
            {
                lines.Add("");
                lines.Add($"**T3 (native repair{(r.UsedT3b ? " + T3b" : "")}, send-ready={r.T3Sendable}, gate={(r.T3GatePass ? "pass" : "fallback")}):**");
                lines.Add("> " + r.T3Text.Replace("\n", "\n> ", StringComparison.Ordinal));
            }
        }

        lines.Add("");
        return string.Join("\n", lines) + "\n";
    }

    private static string Signed(int? v) => v is null ? "-" : v.Value > 0 ? $"+{v.Value}" : v.Value.ToString();
}
