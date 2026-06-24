using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using ReplyInMyVoice.Infrastructure.Providers;
using Xunit.Abstractions;

namespace ReplyInMyVoice.Tests;

/// <summary>
/// Live, opt-in proof that the em-dash fix is a CODE guarantee, not a prompt hope.
/// For each draft it calls the real model twice: once with no punctuation rule, once
/// with the production "do not use em dashes" rule, counting em dashes in each RAW
/// reply. Then it runs both through the real <see cref="RewriteOutputStyle"/> and
/// asserts ZERO em dashes remain. In practice the model emits em dashes anyway from
/// time to time (even with no em dashes in the input and even with the rule present);
/// the code is what removes every one.
///
/// This is a representative probe (the production system prompt lives in
/// OpenAiCompatibleRewriteModelClient; the load-bearing guarantee is unit-tested in
/// RewriteOutputStyleTests). It runs only when RUN_LIVE_ENGINE_TESTS=1 and a
/// DeepSeek/OpenAI key is present, so it never runs (or costs) in CI.
///
/// Run locally with:
///   set -a; . ./.env.local; set +a; RUN_LIVE_ENGINE_TESTS=1 \
///   dotnet test backend-dotnet/ReplyInMyVoice.sln -c Release \
///     --filter FullyQualifiedName~EngineEmDashLive --logger "console;verbosity=detailed"
/// </summary>
public class EngineEmDashLiveTests(ITestOutputHelper output)
{
    private const string BasePrompt =
        """
        You write send-ready email replies from provided facts.
        Return JSON only with rewrittenText.
        Preserve names, dates, money, counts, conditions, policies, and negative constraints.
        Format the reply as a send-ready email with a short greeting, two to four short paragraphs separated by blank lines, and a brief sign-off.
        Avoid stock filler like "thank you for reaching out".
        """;

    private const string NoEmDashLine =
        "Write with plain punctuation. Do not use em dashes (long dashes); use commas, periods, parentheses, or separate sentences instead.";

    private static readonly string[] Drafts =
    {
        // Plain rough drafts (no em dashes in the input).
        "tell the team the website redesign is slipping to june 13 because the design assets were late, give them two options (launch june 6 with reduced scope, or full scope june 13), recommend the first one, need a decision before friday's client call",
        "reply to a recruiter about a senior pm role at a fintech startup, comp range 160k to 185k, im happy where i am but open to a quick chat wednesday or thursday afternoon, ask whether its remote or hybrid and how big the team is",
        // Em-dash-laden inputs: the realistic case where a user pastes AI-generated
        // copy stuffed with em dashes. This is where the model is most likely to copy
        // them straight into the reply.
        "Hi Mara — thank you so much for reaching out — we truly appreciate your patience. Your refund — a prorated amount on the $89 annual plan — will be processed to the original card within 5 to 7 business days. If you have any questions — anything at all — just reply here, and we will be more than happy to help.",
        "Dear Daniel — I hope you're feeling better — your makeup quiz is Tuesday at lunch in Room 204, just bring a pen. The essay deadline has moved — from Monday to Wednesday — and office hours are Thursday from 3 to 4. Please don't hesitate to reach out — I'm here to help.",
        "Quick update — the launch is delayed to June 13 — the design assets came in late. We have two paths forward — Option A is a reduced-scope launch on June 6, and Option B — the full scope — moves to June 13. My recommendation — and I feel strongly about this — is Option A. Let me know before Friday's call — it would really help.",
    };

    [Fact]
    public async Task Live_engine_output_has_no_em_dashes_even_when_the_model_emits_them()
    {
        var apiKey = Env("DEEPSEEK_API_KEY") ?? Env("OPENAI_API_KEY");
        var baseUrl = Env("OPENAI_BASE_URL") ?? "https://api.deepseek.com";
        var model = Env("OPENAI_MODEL") ?? "deepseek-chat";

        if (Env("RUN_LIVE_ENGINE_TESTS") != "1" || string.IsNullOrWhiteSpace(apiKey))
        {
            output.WriteLine("SKIPPED: set RUN_LIVE_ENGINE_TESTS=1 and DEEPSEEK_API_KEY/OPENAI_API_KEY to run.");
            return;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var totalRawWithoutRule = 0;
        var totalRawWithRule = 0;

        output.WriteLine("draft |    condition | em dashes (input / raw model) | after RewriteOutputStyle");
        output.WriteLine("------+--------------+-------------------------------+-------------------------");

        for (var i = 0; i < Drafts.Length; i++)
        {
            foreach (var (label, system) in new[]
                     {
                         ("no rule    ", BasePrompt),
                         ("with rule  ", BasePrompt + "\n" + NoEmDashLine),
                     })
            {
                var raw = await GenerateAsync(http, baseUrl, model, system, Drafts[i]);
                var rawCount = CountEmDashes(raw);
                var cleaned = RewriteOutputStyle.Apply(raw);
                var cleanCount = CountEmDashes(cleaned);

                if (label.StartsWith("no rule")) totalRawWithoutRule += rawCount; else totalRawWithRule += rawCount;

                var inputDashes = CountEmDashes(Drafts[i]);
                output.WriteLine($"  {i + 1,2}  | {label} | input={inputDashes,2}  raw={rawCount,2}            | {cleanCount,5}");
                if (label.StartsWith("no rule"))
                {
                    output.WriteLine($"        raw reply: {Preview(raw)}");
                    output.WriteLine($"        cleaned  : {Preview(cleaned)}");
                }

                // The hard guarantee: the engine's normalizer leaves zero em dashes.
                cleanCount.Should().Be(0, "RewriteOutputStyle must strip every em dash from the reply");
            }
        }

        output.WriteLine("");
        output.WriteLine($"TOTAL raw em dashes the model emitted  WITHOUT the rule: {totalRawWithoutRule}");
        output.WriteLine($"TOTAL raw em dashes the model emitted  WITH    the rule: {totalRawWithRule}");
        output.WriteLine("TOTAL em dashes in final output (both conditions)       : 0");
    }

    private static async Task<string> GenerateAsync(
        HttpClient http, string baseUrl, string model, string system, string draft)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["temperature"] = 0.7,
            ["response_format"] = new { type = "json_object" },
            ["messages"] = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = $"Rough draft reply:\n{draft}\n\nReturn JSON only: {{ \"rewrittenText\": \"...\" }}" },
            },
        };
        if (baseUrl.Contains("deepseek", StringComparison.OrdinalIgnoreCase))
        {
            payload["thinking"] = new { type = "disabled" };
            payload["max_tokens"] = 2800;
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, ChatUri(baseUrl))
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        using var resp = await http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        using var inner = JsonDocument.Parse(content);
        return inner.RootElement.TryGetProperty("rewrittenText", out var rt) ? rt.GetString() ?? "" : content;
    }

    private static Uri ChatUri(string baseUrl)
    {
        var n = baseUrl.Trim().TrimEnd('/');
        if (n.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)) return new Uri(n);
        if (n.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) return new Uri($"{n}/chat/completions");
        return new Uri($"{n}/v1/chat/completions");
    }

    private static int CountEmDashes(string text) => text.Count(c => c is '—' or '―');

    private static string Preview(string text)
    {
        var oneLine = text.Replace("\r", " ").Replace("\n", " ⏎ ");
        return oneLine.Length <= 220 ? oneLine : oneLine[..220] + "…";
    }

    private static string? Env(string name)
    {
        var v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }
}
