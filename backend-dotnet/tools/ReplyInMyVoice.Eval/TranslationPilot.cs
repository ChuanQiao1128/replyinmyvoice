using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.RewriteEngine;
using ReplyInMyVoice.Infrastructure.Providers;

// Translation round-trip RESEARCH pilot (Youdao NMT) — see plans/translation-roundtrip-pilot.md.
// EVAL-ONLY. Never wired into the production rewrite engine. Triggered by TA_PILOT=1 in Program.cs.
//
// Pipeline per case (option-1 "translate the rewrite"):
//   T0  = current production baseline rewrite (engine v0, internal Sapling gate, NO translation)
//   TA  = mask anchors in T0 -> Youdao en->zh-CHS->en -> unmask -> gate on FINAL text
//          -> fall back to T0 if sentinels break OR facts/boundary drift (never worse than T0)
//   Pangram is read ONCE per gate-surviving output (T0, and TA only when it is a distinct pass),
//   paired delta TA-T0, never fed back into rewriting. Lower Pangram = more human-like.
//
// Hard rules honored: the fact ledger is extracted from the ORIGINAL draft (ground truth); the
// fact/boundary gate runs on the FINAL (post-translation) text; Youdao is called at most once per
// direction (no translation loop); detection score never drives a rewrite loop.

internal sealed record MaskResult(string MaskedText, IReadOnlyDictionary<string, string> Map)
{
    public int AnchorCount => Map.Count;
}

internal sealed record UnmaskResult(string Restored, bool IntegrityOk, IReadOnlyList<string> Missing, bool Residual);

// Masks corruption-prone fact anchors (money, IDs/SKUs, dates, times, numbers, proper names)
// behind ASCII sentinels so an NMT round-trip cannot silently drift them. Sentinel integrity is
// re-checked after unmasking; any break fails TA -> fallback to T0.
internal static class AnchorMasker
{
    private static readonly Regex MoneyRegex = new(@"\$\d[\d,]*(?:\.\d{1,2})?", RegexOptions.Compiled);
    // Hyphenated codes containing a digit anywhere (BOWL-BLUE-2, SEED-GRW-04, FieldTrip-4A-09,
    // ORD-29447), letter+digit codes (R4821, INV8842), and #-codes. The digit lookahead avoids
    // masking ordinary hyphenated words like "follow-up" / "check-in".
    private static readonly Regex IdentifierRegex = new(
        @"\b(?=[A-Za-z0-9-]*\d)[A-Za-z0-9]+(?:-[A-Za-z0-9]+)+\b|\b[A-Za-z]+\d[A-Za-z0-9]*\b|#[A-Za-z0-9-]+",
        RegexOptions.Compiled);
    private static readonly Regex DateRegex = new(
        @"\b(?:Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday|(?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2}(?:st|nd|rd|th)?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TimeRegex = new(
        @"\b\d{1,2}(?::\d{2})?\s?(?:a\.?m\.?|p\.?m\.?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NumberRegex = new(@"\b\d[\d,]*(?:\.\d+)?%?\b", RegexOptions.Compiled);
    private static readonly Regex ProperNounRegex = new(@"\b[A-Z][A-Za-z][A-Za-z.'-]+\b", RegexOptions.Compiled);

    // Capitalized words that are sentence scaffolding, not fact anchors. Masking these would shred
    // readability and hurt sentinel survival without protecting any real fact.
    private static readonly HashSet<string> NonAnchorCapitalized = new(StringComparer.Ordinal)
    {
        "The", "This", "That", "These", "Those", "Your", "Their", "Our", "His", "Her", "Its",
        "Hi", "Hello", "Hey", "Dear", "Please", "Thanks", "Thank", "Best", "Regards", "Sincerely",
        "Kind", "Warm", "Unfortunately", "However", "Therefore", "Also", "And", "But", "For",
        "With", "From", "Here", "There", "When", "While", "After", "Before", "Once", "Since",
        "Because", "Although", "About", "We", "You", "They", "She", "He", "Him", "It", "If",
        "As", "So", "At", "On", "In", "Of", "To", "Or", "An", "A", "I",
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday",
        "January", "February", "March", "April", "May", "June", "July", "August",
        "September", "October", "November", "December", "NZD", "USD", "AUD",
        // Sentence-initial / content words that appear capitalized inside curated facts but are
        // not identifiers to protect (masking them only invites NMT drop-breakage).
        "Right", "Another", "Note", "Update", "Team", "Project", "Data", "Sync", "Staging",
        "Client", "Chart", "Export", "Final", "Button", "Copy", "Cleanup", "Dashboard", "Logs",
        "Science", "Museum", "Front", "Office", "Both", "Each", "Every", "Some", "Many", "Most",
        "Next", "Last", "First", "Second", "New", "Old", "Good", "Great", "Sorry", "Currently",
        "Meanwhile", "Additionally", "Finally", "Overall", "Regarding", "Subject", "Do", "Not",
    };

    public static MaskResult Mask(
        string text,
        RewriteFactLedger ledger,
        IReadOnlyList<string> mustKeep,
        IReadOnlyList<string> mustNotClaim,
        IReadOnlyList<string>? extraSpans = null,
        bool bracketFree = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new MaskResult(text, new Dictionary<string, string>(StringComparer.Ordinal));
        }

        var anchors = CollectAnchors(text, ledger, mustKeep, mustNotClaim, extraSpans);

        // Find every standalone occurrence of every anchor, then splice in sentinels over the
        // ORIGINAL text by non-overlapping intervals (longest-first). Splicing — rather than
        // iterative string.Replace — means a placed sentinel can never be re-scanned/corrupted by
        // a later (e.g. bare-number) anchor.
        var intervals = new List<(int Start, int End, string Anchor)>();
        foreach (var anchor in anchors)
        {
            var from = 0;
            while (from <= text.Length - anchor.Length)
            {
                var idx = text.IndexOf(anchor, from, StringComparison.Ordinal);
                if (idx < 0)
                {
                    break;
                }

                var end = idx + anchor.Length;
                if (IsStandalone(text, idx, end))
                {
                    intervals.Add((idx, end, anchor));
                }

                from = idx + 1;
            }
        }

        intervals.Sort((a, b) => a.Start != b.Start ? a.Start - b.Start : (b.End - b.Start) - (a.End - a.Start));

        var sentinelByAnchor = new Dictionary<string, string>(StringComparer.Ordinal);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var sb = new StringBuilder(text.Length + 32);
        var cursor = 0;
        var nextId = 0;
        foreach (var iv in intervals)
        {
            if (iv.Start < cursor)
            {
                continue; // overlaps an already-placed sentinel
            }

            sb.Append(text, cursor, iv.Start - cursor);
            if (!sentinelByAnchor.TryGetValue(iv.Anchor, out var sentinel))
            {
                // Bracket-free sentinel (QZAN000QZ) survives an LLM Chinese-polish pass far better than
                // "[[A0]]", whose brackets get mangled/translated (round-7 dry run). Used by R7.
                sentinel = bracketFree ? $"QZAN{nextId++:D3}QZ" : $"[[A{nextId++}]]";
                sentinelByAnchor[iv.Anchor] = sentinel;
                map[sentinel] = iv.Anchor;
            }

            sb.Append(sentinel);
            cursor = iv.End;
        }

        sb.Append(text, cursor, text.Length - cursor);
        return new MaskResult(sb.ToString(), map);
    }

    // Restores sentinels to their original anchors. Tolerant of common NMT manglings (inner
    // spaces, single vs double bracket, full-width brackets). IntegrityOk is false if any sentinel
    // went missing/duplicated or a bracketed residue survived — that fails TA (fallback to T0).
    // Youdao transforms the bracket delimiters (observed: "[[A0]]" -> "(A0)") but preserves the
    // ASCII core token, so recovery matches the core wrapped in any common bracket pair — square,
    // parentheses, or full-width — tolerating inner spaces and zero-padding.
    private static readonly Regex TolerantSentinelRegex = new(
        @"(?<![A-Za-z0-9])[\[(［【]{1,2}\s*[AaＡ]\s*0*(?<n>\d+)\s*[\])］】]{1,2}", RegexOptions.Compiled);

    // Bracket-free QZAN sentinels, tolerant of inserted spaces / case introduced by an LLM/NMT pass.
    private static readonly Regex TolerantQzanRegex = new(
        @"Q\s*Z\s*A\s*N\s*0*(?<n>\d+)\s*Q\s*Z", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // After recovery, any surviving bracketed "A<n>" token (its number not in the map => corrupted),
    // a leftover double-bracket, or a leftover QZAN token is a genuine break => fail, fall back to T0.
    private static readonly Regex ResidualSentinelRegex = new(
        @"[\[(［【]\s*[AaＡ]\s*\d+\s*[\])］】]|\[\[|\]\]|Q\s*Z\s*A\s*N\s*\d+\s*Q\s*Z", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static UnmaskResult Unmask(string text, IReadOnlyDictionary<string, string> map)
    {
        if (map.Count == 0)
        {
            return new UnmaskResult(text, true, Array.Empty<string>(), false);
        }

        var working = text;
        var found = new HashSet<string>(StringComparer.Ordinal);

        foreach (var kv in map)
        {
            if (working.Contains(kv.Key, StringComparison.Ordinal))
            {
                working = working.Replace(kv.Key, kv.Value, StringComparison.Ordinal);
                found.Add(kv.Key);
            }
        }

        if (found.Count < map.Count)
        {
            working = TolerantSentinelRegex.Replace(working, m =>
            {
                var key = $"[[A{m.Groups["n"].Value}]]";
                if (map.TryGetValue(key, out var anchor))
                {
                    found.Add(key);
                    return anchor;
                }

                return m.Value;
            });
        }

        if (found.Count < map.Count)
        {
            working = TolerantQzanRegex.Replace(working, m =>
            {
                var key = $"QZAN{int.Parse(m.Groups["n"].Value):D3}QZ";
                if (map.TryGetValue(key, out var anchor))
                {
                    found.Add(key);
                    return anchor;
                }

                return m.Value;
            });
        }

        var missing = map.Keys.Where(k => !found.Contains(k)).ToList();
        var residual = ResidualSentinelRegex.IsMatch(working);
        return new UnmaskResult(working, missing.Count == 0 && !residual, missing, residual);
    }

    // Anchors are collected from the CURATED ground truth (must_keep + must_not_claim) — never
    // scraped from the rewrite — so non-fact capitalized words (e.g. "Right now") are not masked
    // and cannot cause a false sentinel break when NMT restructures them. The deterministic
    // amount/identifier/date facts from the ledger are unioned in (high-precision regex hits).
    private static List<string> CollectAnchors(
        string text,
        RewriteFactLedger ledger,
        IReadOnlyList<string> mustKeep,
        IReadOnlyList<string> mustNotClaim,
        IReadOnlyList<string>? extraSpans = null)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);

        // Caller-supplied protected spans (T4 "protection-forward": business nouns / boundary phrases
        // masked BEFORE translation so the round-trip cannot drift them).
        if (extraSpans is not null)
        {
            foreach (var span in extraSpans)
            {
                Add(set, span);
            }
        }

        foreach (var phrase in mustKeep.Concat(mustNotClaim))
        {
            AddRegex(set, phrase, MoneyRegex);
            AddRegex(set, phrase, IdentifierRegex);
            AddRegex(set, phrase, DateRegex);
            AddRegex(set, phrase, TimeRegex);
            AddRegex(set, phrase, NumberRegex);
            foreach (Match m in ProperNounRegex.Matches(phrase))
            {
                AddProperNoun(set, m.Value);
            }
        }

        foreach (var fact in ledger.Facts)
        {
            switch (fact.Category)
            {
                case RewriteFactCategory.Amount:
                case RewriteFactCategory.Identifier:
                case RewriteFactCategory.DateOrDeadline:
                    Add(set, fact.Text);
                    break;
                case RewriteFactCategory.Count:
                    if (Regex.IsMatch(fact.Text, @"\d"))
                    {
                        Add(set, fact.Text);
                    }

                    break;
            }
        }

        return set
            .Where(a => a.Length >= 2 && text.Contains(a, StringComparison.Ordinal))
            .ToList();
    }

    private static void AddRegex(HashSet<string> set, string source, Regex regex)
    {
        foreach (Match m in regex.Matches(source))
        {
            Add(set, m.Value);
        }
    }

    private static void AddProperNoun(HashSet<string> set, string value)
    {
        var trimmed = value.Trim().TrimEnd('.', ',', '\'');
        if (trimmed.Length >= 3 && !NonAnchorCapitalized.Contains(trimmed))
        {
            set.Add(trimmed);
        }
    }

    private static void Add(HashSet<string> set, string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2)
        {
            set.Add(trimmed);
        }
    }

    private static bool IsStandalone(string text, int start, int end)
    {
        var leftOk = start == 0
            || (!IsWordChar(text[start - 1]) && !NumberJoinBefore(text, start));
        var rightOk = end >= text.Length
            || (!IsWordChar(text[end]) && !NumberJoinAfter(text, end));
        return leftOk && rightOk;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    // '.'/',' immediately followed by a digit means we are inside a number (e.g. anchor "$12"
    // sitting inside "$12.00") — not a standalone match.
    private static bool NumberJoinAfter(string text, int end) =>
        end < text.Length && (text[end] == '.' || text[end] == ',') && end + 1 < text.Length && char.IsDigit(text[end + 1]);

    private static bool NumberJoinBefore(string text, int start) =>
        start > 0 && (text[start - 1] == '.' || text[start - 1] == ',') && start - 2 >= 0 && char.IsDigit(text[start - 2]);
}

internal sealed record TranslateResult(bool Success, string Text, string? ErrorCode);

// Youdao text-translation API client (signType v3). One call per direction; never loops. Segments
// on paragraph/sentence boundaries when a single query would exceed the 4500-char guard (under the
// 5000 hard limit). Reads credentials from env (caller passes resolved values); never logs them.
internal sealed class YoudaoTranslationClient
{
    private const int MaxQueryChars = 4500;

    private readonly HttpClient _httpClient;
    private readonly string _appKey;
    private readonly string _appSecret;
    private readonly Uri _apiUrl;
    private readonly TimeSpan _timeout;

    public int CallCount { get; private set; }

    public YoudaoTranslationClient(HttpClient httpClient, string appKey, string appSecret, string apiUrl, TimeSpan timeout)
    {
        _httpClient = httpClient;
        _appKey = appKey;
        _appSecret = appSecret;
        _apiUrl = new Uri(string.IsNullOrWhiteSpace(apiUrl) ? "https://openapi.youdao.com/api" : apiUrl);
        _timeout = timeout;
    }

    public async Task<TranslateResult> TranslateAsync(string text, string from, string to, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TranslateResult(true, text, null);
        }

        var segments = Segment(text);
        var output = new StringBuilder();
        foreach (var segment in segments)
        {
            var result = await TranslateOneAsync(segment, from, to, cancellationToken);
            if (!result.Success)
            {
                return result;
            }

            output.Append(result.Text);
        }

        return new TranslateResult(true, output.ToString(), null);
    }

    private async Task<TranslateResult> TranslateOneAsync(string q, string from, string to, CancellationToken cancellationToken)
    {
        CallCount += 1;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);

        var salt = Guid.NewGuid().ToString();
        var curtime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var sign = Sign(q, salt, curtime);

        var form = new Dictionary<string, string>
        {
            ["q"] = q,
            ["from"] = from,
            ["to"] = to,
            ["appKey"] = _appKey,
            ["salt"] = salt,
            ["sign"] = sign,
            ["signType"] = "v3",
            ["curtime"] = curtime,
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl)
            {
                Content = new FormUrlEncodedContent(form),
            };
            using var response = await _httpClient.SendAsync(request, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new TranslateResult(false, string.Empty, $"youdao_http_{(int)response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var errorCode = root.TryGetProperty("errorCode", out var ec) ? ec.GetString() : null;
            if (errorCode != "0")
            {
                return new TranslateResult(false, string.Empty, $"youdao_error_{errorCode}");
            }

            if (!root.TryGetProperty("translation", out var translation) || translation.ValueKind != JsonValueKind.Array)
            {
                return new TranslateResult(false, string.Empty, "youdao_no_translation");
            }

            var joined = string.Join(" ", translation.EnumerateArray().Select(t => t.GetString() ?? string.Empty));
            return new TranslateResult(true, joined, null);
        }
        catch (OperationCanceledException)
        {
            return new TranslateResult(false, string.Empty, "youdao_timeout");
        }
        catch (HttpRequestException)
        {
            return new TranslateResult(false, string.Empty, "youdao_network_failed");
        }
        catch (JsonException)
        {
            return new TranslateResult(false, string.Empty, "youdao_json_parse_failed");
        }
    }

    // sign = sha256(appKey + INPUT + salt + curtime + appSecret); INPUT = q when len<=20, else
    // q[:10] + len + q[-10:] (len counts characters — the classic Youdao gotcha).
    private string Sign(string q, string salt, string curtime)
    {
        var input = q.Length <= 20
            ? q
            : q.Substring(0, 10) + q.Length + q.Substring(q.Length - 10);
        var raw = _appKey + input + salt + curtime + _appSecret;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static IReadOnlyList<string> Segment(string text)
    {
        if (text.Length <= MaxQueryChars)
        {
            return new[] { text };
        }

        var segments = new List<string>();
        var current = new StringBuilder();
        foreach (var paragraph in Regex.Split(text, @"(?<=\n)"))
        {
            if (current.Length + paragraph.Length > MaxQueryChars && current.Length > 0)
            {
                segments.Add(current.ToString());
                current.Clear();
            }

            if (paragraph.Length > MaxQueryChars)
            {
                foreach (var sentence in Regex.Split(paragraph, @"(?<=[.!?])\s"))
                {
                    if (current.Length + sentence.Length > MaxQueryChars && current.Length > 0)
                    {
                        segments.Add(current.ToString());
                        current.Clear();
                    }

                    current.Append(sentence).Append(' ');
                }
            }
            else
            {
                current.Append(paragraph);
            }
        }

        if (current.Length > 0)
        {
            segments.Add(current.ToString());
        }

        return segments;
    }
}

internal sealed record PilotRow(
    string CaseId,
    int CaseNumber,
    string Category,
    string Tone,
    bool T0HasOutput,
    string T0Text,
    bool T0FactPassDet,
    bool T0FactPassSem,
    int T0ForbidDet,
    int T0ForbidSem,
    int AnchorCount,
    bool SentinelPass,
    bool TaTranslated,
    string TaText,
    bool TaFactPassDet,
    bool TaFactPassSem,
    int TaForbidDet,
    int TaForbidSem,
    bool TaMeaningChanged,
    bool TaGatePass,
    int? PangramT0,
    int? PangramTa,
    int? DeltaTaMinusT0,
    string ChosenVariant,
    string FallbackReason,
    IReadOnlyList<string> TaMissingOrContradicted,
    string Notes);

internal static class TranslationPilotRunner
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
            Console.Error.WriteLine("TA_PILOT: missing Youdao credentials (YOUDAO_APP_KEY/AppID + YOUDAO_APP_SECRET/AppSecret).");
            return 2;
        }

        var youdaoMaxCalls = IntEnv("YOUDAO_MAX_CALLS", 40);
        var pangramMaxCalls = IntEnv("PANGRAM_MAX_CALLS", 0);
        var pangramKey = Environment.GetEnvironmentVariable("PANGRAM_API_KEY");
        var pangramEnabled = pangramMaxCalls > 0 && !string.IsNullOrWhiteSpace(pangramKey);

        using var youdaoHttp = new HttpClient();
        using var judgeHttp = new HttpClient();
        using var pangramHttp = new HttpClient();
        var youdao = new YoudaoTranslationClient(youdaoHttp, youdaoKey!, youdaoSecret!, youdaoUrl, TimeSpan.FromSeconds(30));
        var judge = new SemanticEvalJudge(judgeHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));
        var pangram = pangramEnabled
            ? new PangramWritingSignalClient(pangramHttp, pangramKey!, TimeSpan.FromSeconds(45))
            : null;

        Console.WriteLine(
            $"TA pilot: cases={cases.Count} youdaoMax={youdaoMaxCalls} pangram={(pangramEnabled ? $"on(max {pangramMaxCalls})" : "off")} model={config.Model}");

        var rows = new List<PilotRow>();
        var judgeCalls = 0;
        var pangramCalls = 0;

        foreach (var sample in cases)
        {
            var request = sample.ToRewriteRequest();
            var mustKeep = sample.MustKeep;
            var mustNotClaim = sample.MustNotClaim;

            // --- T0: production-baseline rewrite (engine v0, internal Sapling gate, no translation).
            var t0Result = await provider.RewriteAsync(Guid.NewGuid(), request, CancellationToken.None);
            var t0Text = RewritePayload.TryParse(t0Result.ResultJson)?.RewrittenText ?? string.Empty;
            var t0HasOutput = !string.IsNullOrWhiteSpace(t0Text);

            if (!t0HasOutput)
            {
                rows.Add(EmptyT0Row(sample, request.Tone, t0Result.ErrorCode));
                Console.WriteLine($"{sample.Id}: T0 produced no output ({t0Result.ErrorCode ?? "unknown"}); skipped TA.");
                continue;
            }

            var t0FactDet = FactExpectationChecker.Check(t0Text, mustKeep).Passed;
            var t0ForbidDet = ForbiddenClaimScreen.Check(t0Text, mustNotClaim).Violations.Count;
            var t0Sem = await judge.VerifyAsync(t0Text, mustKeep, mustNotClaim, CancellationToken.None);
            judgeCalls++;

            // --- TA: mask -> Youdao en->zh-CHS->en -> unmask -> gate on FINAL text.
            var ledger = FactLedgerExtractor.Extract(request);
            var masked = AnchorMasker.Mask(t0Text, ledger, mustKeep, mustNotClaim);

            var taText = string.Empty;
            var taTranslated = false;
            var sentinelPass = false;
            string fallbackReason;

            if (youdao.CallCount + 2 > youdaoMaxCalls)
            {
                fallbackReason = "youdao_budget_exhausted";
            }
            else
            {
                var toZh = await youdao.TranslateAsync(masked.MaskedText, "en", "zh-CHS", CancellationToken.None);
                if (!toZh.Success)
                {
                    fallbackReason = toZh.ErrorCode ?? "youdao_en_zh_failed";
                }
                else
                {
                    var backEn = await youdao.TranslateAsync(toZh.Text, "zh-CHS", "en", CancellationToken.None);
                    if (!backEn.Success)
                    {
                        fallbackReason = backEn.ErrorCode ?? "youdao_zh_en_failed";
                    }
                    else
                    {
                        taTranslated = true;
                        var unmask = AnchorMasker.Unmask(backEn.Text, masked.Map);
                        taText = unmask.Restored;
                        sentinelPass = unmask.IntegrityOk;
                        fallbackReason = unmask.IntegrityOk ? string.Empty : "sentinel_broken";
                        if (!unmask.IntegrityOk)
                        {
                            Console.WriteLine(
                                $"  [sentinel] {sample.Id} mapCount={masked.Map.Count} "
                                + $"missing=[{string.Join(",", unmask.Missing)}] residual={unmask.Residual}");
                        }

                        if (Environment.GetEnvironmentVariable("TA_PILOT_DEBUG") is "1" or "true")
                        {
                            Console.WriteLine($"  [debug-masked] {masked.MaskedText.Replace("\n", "\\n", StringComparison.Ordinal)}");
                            Console.WriteLine($"  [debug-backen] {backEn.Text.Replace("\n", "\\n", StringComparison.Ordinal)}");
                        }
                    }
                }
            }

            // --- Gate TA on the FINAL text. Semantic judge is authoritative (deterministic recorded
            // alongside). TA passes only if sentinels held AND facts/boundary/meaning all hold.
            var taFactDet = false;
            var taForbidDet = 0;
            var taFactSem = false;
            var taForbidSem = 0;
            var taMeaningChanged = false;
            IReadOnlyList<string> taLost = Array.Empty<string>();

            if (taTranslated && sentinelPass)
            {
                taFactDet = FactExpectationChecker.Check(taText, mustKeep).Passed;
                taForbidDet = ForbiddenClaimScreen.Check(taText, mustNotClaim).Violations.Count;
                var taSem = await judge.VerifyAsync(taText, mustKeep, mustNotClaim, CancellationToken.None);
                judgeCalls++;
                if (taSem.Error is not null)
                {
                    fallbackReason = $"judge_{taSem.Error}";
                }
                else
                {
                    taFactSem = taSem.FactsReallyPass;
                    taForbidSem = taSem.RealForbidden;
                    taMeaningChanged = taSem.MeaningChanged;
                    taLost = taSem.Facts
                        .Where(f => f.Status is "missing" or "contradicted")
                        .Select(f => $"{f.Status}:{f.Fact}")
                        .ToList();

                    if (!taFactSem)
                    {
                        fallbackReason = "ta_facts_drifted";
                    }
                    else if (taForbidSem > 0)
                    {
                        fallbackReason = "ta_forbidden_violation";
                    }
                    else if (taMeaningChanged)
                    {
                        fallbackReason = "ta_meaning_changed";
                    }
                }
            }

            var taGatePass = taTranslated && sentinelPass && taFactSem && taForbidSem == 0 && !taMeaningChanged
                && string.IsNullOrEmpty(fallbackReason);
            var chosen = taGatePass ? "TA" : "T0";

            // --- Pangram: read T0 once; read TA only when it is a distinct gate-passing output.
            int? pangramT0 = null;
            int? pangramTa = null;
            if (pangram is not null)
            {
                if (pangramCalls < pangramMaxCalls)
                {
                    pangramT0 = await MeasureAsync(pangram, t0Text);
                    pangramCalls++;
                }

                if (taGatePass && !string.Equals(taText, t0Text, StringComparison.Ordinal))
                {
                    if (pangramCalls < pangramMaxCalls)
                    {
                        pangramTa = await MeasureAsync(pangram, taText);
                        pangramCalls++;
                    }
                }
                else
                {
                    pangramTa = pangramT0; // fallback -> TA == T0, no extra call
                }
            }

            int? delta = pangramT0.HasValue && pangramTa.HasValue ? pangramTa.Value - pangramT0.Value : null;

            rows.Add(new PilotRow(
                sample.Id, sample.CaseNumber, sample.Category, request.Tone,
                t0HasOutput, t0Text,
                t0FactDet, t0Sem.Error is null && t0Sem.FactsReallyPass, t0ForbidDet, t0Sem.RealForbidden,
                masked.AnchorCount, sentinelPass,
                taTranslated, taText,
                taFactDet, taFactSem, taForbidDet, taForbidSem, taMeaningChanged,
                taGatePass,
                pangramT0, pangramTa, delta,
                chosen, taGatePass ? string.Empty : fallbackReason,
                taLost,
                Notes(taGatePass, fallbackReason, delta)));

            Console.WriteLine(
                $"{sample.Id}: anchors={masked.AnchorCount} sentinel={(sentinelPass ? "ok" : "BROKEN")} "
                + $"taGate={(taGatePass ? "pass" : "fallback:" + fallbackReason)} "
                + $"pangram T0={Fmt(pangramT0)} TA={Fmt(pangramTa)} delta={Fmt(delta)}");
        }

        var summary = PilotSummaryData.Create(
            startedAt, DateTimeOffset.UtcNow, config, rows,
            youdao.CallCount, pangramCalls, judgeCalls, modelCounter.CallCount, saplingCounter.CallCount,
            pangramEnabled);

        var stamp = startedAt.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(config.OutputDirectory);
        var jsonPath = Path.Combine(config.OutputDirectory, $"{stamp}-ta-translation-pilot.json");
        var mdPath = Path.Combine(config.OutputDirectory, $"{stamp}-ta-translation-pilot.md");
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(new { summary, rows }, jsonOptions));
        await File.WriteAllTextAsync(mdPath, PilotReport.Render(summary, rows));

        Console.WriteLine($"Wrote {jsonPath}");
        Console.WriteLine($"Wrote {mdPath}");
        Console.WriteLine(summary.OneLine());
        return 0;
    }

    private static async Task<int?> MeasureAsync(IWritingSignalClient client, string text)
    {
        var result = await client.MeasureAsync(text, CancellationToken.None);
        return result.Available ? result.AiLikePercent : null;
    }

    private static PilotRow EmptyT0Row(EvalCase sample, string tone, string? errorCode) => new(
        sample.Id, sample.CaseNumber, sample.Category, tone,
        false, string.Empty,
        false, false, 0, 0,
        0, false,
        false, string.Empty,
        false, false, 0, 0, false,
        false,
        null, null, null,
        "T0", $"t0_no_output:{errorCode ?? "unknown"}",
        Array.Empty<string>(),
        "T0 produced no output; TA not attempted.");

    private static string Notes(bool taGatePass, string fallbackReason, int? delta)
    {
        if (!taGatePass)
        {
            return $"TA fell back to T0 ({fallbackReason}); detection delta is 0 by construction.";
        }

        if (delta is null)
        {
            return "TA passed all gates; Pangram not measured.";
        }

        return delta < 0
            ? $"TA passed; Pangram dropped {Math.Abs(delta.Value)} pts vs T0."
            : delta > 0
                ? $"TA passed but Pangram rose {delta.Value} pts vs T0."
                : "TA passed; Pangram unchanged vs T0.";
    }

    private static string? FirstEnv(params string[] names) =>
        names.Select(Environment.GetEnvironmentVariable).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static int IntEnv(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var v) && v >= 0 ? v : fallback;

    private static string Fmt(int? v) => v?.ToString() ?? "-";
}

internal sealed record PilotSummaryData(
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    int Cases,
    int T0WithOutput,
    int T0FactPassSem,
    int TaFactPassSem,
    int SentinelPass,
    int TaGatePass,
    int TaFallback,
    IReadOnlyDictionary<string, int> FallbackReasons,
    int PangramPairs,
    int TaLower,
    int TaHigher,
    int TaEqual,
    int? MeanDeltaPangram,
    int? MedianDeltaPangram,
    bool PangramEnabled,
    int YoudaoCalls,
    int PangramCalls,
    int JudgeCalls,
    int ModelCalls,
    int SaplingCalls)
{
    public static PilotSummaryData Create(
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        EvalConfig config,
        IReadOnlyList<PilotRow> rows,
        int youdaoCalls,
        int pangramCalls,
        int judgeCalls,
        int modelCalls,
        int saplingCalls,
        bool pangramEnabled)
    {
        var withOutput = rows.Where(r => r.T0HasOutput).ToList();
        var pairs = withOutput
            .Where(r => r.TaGatePass && r.DeltaTaMinusT0.HasValue)
            .Select(r => r.DeltaTaMinusT0!.Value)
            .ToList();

        var fallbackReasons = withOutput
            .Where(r => !r.TaGatePass)
            .GroupBy(r => r.FallbackReason)
            .ToDictionary(g => g.Key, g => g.Count());

        return new PilotSummaryData(
            startedAt,
            finishedAt,
            rows.Count,
            withOutput.Count,
            rows.Count(r => r.T0FactPassSem),
            rows.Count(r => r.TaFactPassSem),
            rows.Count(r => r.SentinelPass),
            rows.Count(r => r.TaGatePass),
            withOutput.Count(r => !r.TaGatePass),
            fallbackReasons,
            pairs.Count,
            pairs.Count(d => d < 0),
            pairs.Count(d => d > 0),
            pairs.Count(d => d == 0),
            pairs.Count == 0 ? null : (int?)Math.Round(pairs.Average(), MidpointRounding.AwayFromZero),
            Median(pairs),
            pangramEnabled,
            youdaoCalls,
            pangramCalls,
            judgeCalls,
            modelCalls,
            saplingCalls);
    }

    private static int? Median(IReadOnlyList<int> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[mid]
            : (int)Math.Round((sorted[mid - 1] + sorted[mid]) / 2.0, MidpointRounding.AwayFromZero);
    }

    public string OneLine() =>
        $"TA pilot: T0out={T0WithOutput}/{Cases}, taFacts(sem)={TaFactPassSem}/{Cases}, sentinel={SentinelPass}/{Cases}, "
        + $"taGatePass={TaGatePass}/{Cases}, pangramPairs={PangramPairs} (lower={TaLower}/higher={TaHigher}/equal={TaEqual}), "
        + $"meanDelta={(MeanDeltaPangram?.ToString() ?? "n/a")}, youdao={YoudaoCalls}, pangram={PangramCalls}, judge={JudgeCalls}, model={ModelCalls}";
}

internal static class PilotReport
{
    public static string Render(PilotSummaryData s, IReadOnlyList<PilotRow> rows)
    {
        var lines = new List<string>
        {
            "# TA translation round-trip pilot (T0 vs Youdao en→zh-CHS→en)",
            "",
            "**Eval-only research pilot** (`plans/translation-roundtrip-pilot.md`). Not wired into the production engine.",
            "Lower Pangram = more human-like. TA = T0 rewrite, anchor-masked, round-tripped through Youdao, unmasked, then gated on the final text; falls back to T0 on any sentinel/fact/boundary/meaning drift.",
            "",
            $"Started: {s.StartedAt:O}  Finished: {s.FinishedAt:O}",
            "",
            "## Headline — does TA lower the detection reading vs T0?",
            "",
        };

        if (!s.PangramEnabled)
        {
            lines.Add("Pangram was **disabled** (PANGRAM_MAX_CALLS=0 or no key) — only the fact/boundary/sentinel gate ran. No detection delta measured.");
        }
        else if (s.PangramPairs == 0)
        {
            lines.Add("**No TA candidate passed the gate as a distinct output**, so there is no T0-vs-TA Pangram pair to compare. TA fell back to T0 in every case (detection delta 0 by construction).");
        }
        else
        {
            lines.Add($"Of **{s.PangramPairs}** gate-passing TA outputs measured against their T0:");
            lines.Add("");
            lines.Add($"- **TA lowered Pangram in {s.TaLower}/{s.PangramPairs}** cases (higher in {s.TaHigher}, unchanged in {s.TaEqual}).");
            lines.Add($"- Mean delta (TA − T0): **{Signed(s.MeanDeltaPangram)} pts**; median **{Signed(s.MedianDeltaPangram)} pts** (negative = TA more human-like).");
        }

        lines.Add("");
        lines.Add("## Fact / boundary safety (the gate that must hold before detection even counts)");
        lines.Add("");
        lines.Add($"- T0 with output: **{s.T0WithOutput}/{s.Cases}**");
        lines.Add($"- T0 facts pass (semantic judge): **{s.T0FactPassSem}/{s.Cases}**");
        lines.Add($"- TA facts pass (semantic judge): **{s.TaFactPassSem}/{s.Cases}**");
        lines.Add($"- Sentinel integrity held (anchors survived the round-trip): **{s.SentinelPass}/{s.Cases}**");
        lines.Add($"- TA passed the full gate (sentinel + facts + boundary + meaning): **{s.TaGatePass}/{s.Cases}**");
        lines.Add($"- TA fell back to T0: **{s.TaFallback}/{s.T0WithOutput}**" + (s.FallbackReasons.Count > 0
            ? " — " + string.Join(", ", s.FallbackReasons.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}×{kv.Value}"))
            : string.Empty));
        lines.Add("");
        lines.Add("## Cost / calls");
        lines.Add("");
        lines.Add($"Youdao: **{s.YoudaoCalls}** · Pangram: **{s.PangramCalls}** · DeepSeek judge: **{s.JudgeCalls}** · DeepSeek model (T0 rewrites): **{s.ModelCalls}** · Sapling (engine gate): **{s.SaplingCalls}**");
        lines.Add("");
        lines.Add("## Per-case");
        lines.Add("");
        lines.Add("| Case | Category | Anchors | Sentinel | TA gate | Pangram T0 | Pangram TA | Δ(TA−T0) | Chosen | Fallback / lost facts |");
        lines.Add("| --- | --- | ---: | :---: | :---: | ---: | ---: | ---: | :---: | --- |");
        foreach (var r in rows)
        {
            var detail = r.TaGatePass
                ? string.Empty
                : (r.FallbackReason + (r.TaMissingOrContradicted.Count > 0 ? " | " + string.Join("; ", r.TaMissingOrContradicted) : string.Empty));
            lines.Add(string.Join(" | ", new[]
            {
                r.CaseId,
                r.Category,
                r.AnchorCount.ToString(),
                r.SentinelPass ? "ok" : "broken",
                r.TaGatePass ? "pass" : "fallback",
                r.PangramT0?.ToString() ?? "-",
                r.PangramTa?.ToString() ?? "-",
                Signed(r.DeltaTaMinusT0),
                r.ChosenVariant,
                detail.Replace("|", "/", StringComparison.Ordinal),
            }));
        }

        lines.Add("");
        lines.Add("## Text comparison (T0 vs TA)");
        foreach (var r in rows.Where(r => r.T0HasOutput))
        {
            lines.Add("");
            lines.Add($"### {r.CaseId} — {r.Category} (chosen: {r.ChosenVariant})");
            lines.Add("");
            lines.Add("**T0 (baseline):**");
            lines.Add("");
            lines.Add("> " + r.T0Text.Replace("\n", "\n> ", StringComparison.Ordinal));
            lines.Add("");
            if (r.TaTranslated)
            {
                lines.Add($"**TA (round-trip, sentinel {(r.SentinelPass ? "ok" : "broken")}, gate {(r.TaGatePass ? "pass" : "fallback")}):**");
                lines.Add("");
                lines.Add("> " + r.TaText.Replace("\n", "\n> ", StringComparison.Ordinal));
            }
            else
            {
                lines.Add($"**TA:** not produced ({r.FallbackReason}).");
            }
        }

        lines.Add("");
        return string.Join("\n", lines) + "\n";
    }

    private static string Signed(int? v) => v is null ? "-" : v.Value > 0 ? $"+{v.Value}" : v.Value.ToString();
}
