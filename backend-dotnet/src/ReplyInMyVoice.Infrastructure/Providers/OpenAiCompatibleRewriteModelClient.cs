using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ReplyInMyVoice.Domain.RewriteEngine;

namespace ReplyInMyVoice.Infrastructure.Providers;

public sealed class OpenAiCompatibleRewriteModelClient(
    HttpClient httpClient,
    string apiKey,
    string model,
    string baseUrl,
    TimeSpan timeout) : IRewriteModelClient
{
    public async Task<RewriteModelResult> GenerateCandidateAsync(
        RewriteModelRequest request,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri(baseUrl));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(BuildPayload(request)),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var response = await httpClient.SendAsync(httpRequest, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new RewriteModelResult(null, false, $"model_http_{(int)response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            using var doc = JsonDocument.Parse(json);
            var rawContent = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(rawContent))
            {
                return new RewriteModelResult(null, false, "model_empty");
            }

            var candidate = ExtractCandidateText(rawContent);
            return string.IsNullOrWhiteSpace(candidate)
                ? new RewriteModelResult(null, false, "model_candidate_missing")
                : new RewriteModelResult(candidate.Trim(), true, null);
        }
        catch (OperationCanceledException)
        {
            return new RewriteModelResult(null, false, "model_timeout");
        }
        catch (JsonException)
        {
            return new RewriteModelResult(null, false, "model_json_parse_failed");
        }
        catch (HttpRequestException)
        {
            return new RewriteModelResult(null, false, "model_network_failed");
        }
    }

    private object BuildPayload(RewriteModelRequest request)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["temperature"] = 0.4,
            ["response_format"] = new { type = "json_object" },
            ["messages"] = new object[]
            {
                new
                {
                    role = "system",
                    content = """
                    You write send-ready email replies from provided facts.
                    Return JSON only with rewrittenText.
                    Preserve names, dates, money, counts, conditions, policies, and negative constraints.
                    Preserve uncertainty: do not change may, might, seems, appears, likely, or looks like into definite claims such as is, will, confirmed, or due to.
                    Carry reviewed Amount, DateOrDeadline, and Count values into the reply exactly.
                    In payment, invoice, refund, return, membership, renewal, or transfer cases, include the original paid amount and original purchase/start date when provided.
                    If the facts name included benefits, requested days, or available options, state those explicitly before asking for assets or a decision.
                    Do not add professional-advice redirects such as "ask your accountant/lawyer/doctor" unless the source facts explicitly provide that next step.
                    Prefer concise, concrete wording over generic email templates, especially when the rough draft is short.
                    Avoid stock phrases like "thank you for reaching out", "I completely understand", "looking forward", and generic signoffs unless the draft used them.
                    Do not add unsupported judgment labels such as dismissive, careless, rude, negligent, or unprofessional.
                    Do not invent promises, discounts, timelines, policies, people, or outcomes.
                    Do not mention quality gates, scores, external scoring tools, or internal strategy names.
                    """,
                },
                new
                {
                    role = "user",
                    content = BuildPrompt(request),
                },
            },
        };

        if (IsDeepSeekBaseUrl(baseUrl))
        {
            payload["thinking"] = new { type = "disabled" };
            payload["max_tokens"] = 2800;
        }

        return payload;
    }

    private static string BuildPrompt(RewriteModelRequest request)
    {
        var facts = request.FactLedger.Facts
            .Select(fact => $"- [{fact.Importance}] {fact.Category}: {fact.Text}")
            .ToArray();

        return $$"""
        Attempt: {{request.AttemptId}}
        Tone: {{request.UserRequest.Tone}}
        Strategy: {{request.Strategy}}
        Scenario: {{request.InputAnalysis.Scenario}}
        Strategy instructions:
        {{StrategyInstructions(request.Strategy)}}

        Message to reply to:
        {{request.UserRequest.MessageToReplyTo}}

        Rough draft reply:
        {{request.UserRequest.RoughDraftReply}}

        Audience:
        {{request.UserRequest.Audience}}

        Purpose:
        {{request.UserRequest.Purpose}}

        What actually happened:
        {{request.UserRequest.WhatHappened}}

        Facts to preserve:
        {{request.UserRequest.FactsToPreserve}}

        Reviewed facts:
        {{string.Join("\n", facts)}}

        Prior failed attempts:
        {{AttemptHistoryText(request.AttemptHistory)}}

        Return JSON only:
        {
          "rewrittenText": "..."
        }
        """;
    }

    private static string StrategyInstructions(RewriteStrategy strategy) =>
        strategy switch
        {
            RewriteStrategy.MinimalPolish =>
                "Keep the reply close to the draft, but remove stiff wording and preserve every concrete fact.",
            RewriteStrategy.TargetedSentenceRepair =>
                "Repair the previous failure by shortening generic phrasing, using concrete plain language, and keeping all facts. Do not simply split the prior candidate into short paragraphs.",
            RewriteStrategy.FactsFirstReconstruct =>
                "Rebuild the reply from the reviewed facts instead of paraphrasing line by line. Use natural paragraphs and include the requested next step.",
            RewriteStrategy.FullStructureRewrite =>
                "Group related facts into send-ready paragraphs: acknowledge, explain status, give options or constraints, then ask for the next step when needed.",
            RewriteStrategy.SupportPolicyOptionsRewrite =>
                "Preserve policy limits, uncertainty, eligibility conditions, IDs, invoice totals, original paid amounts, start or purchase dates, and no-change-without-confirmation constraints. Make options clear without promising approval.",
            RewriteStrategy.QuoteListSafeRewrite =>
                "Keep list or quoted-thread boundaries clear. Answer items in a stable structure without detached list markers.",
            RewriteStrategy.MessyThreadCleanupRewrite =>
                "Separate old quoted context from the current reply. Clarify what changed without leaking messy thread fragments.",
            RewriteStrategy.StrongModelRestructure =>
                "Do a final concise facts-first restructure. Use plain direct wording, remove stock email phrases, and preserve every constraint.",
            _ => "Return a safe quality failure by not producing a candidate.",
        };

    private static string AttemptHistoryText(IReadOnlyList<RewriteAttemptHistoryItem> history)
    {
        if (history.Count == 0)
        {
            return "None.";
        }

        return string.Join(
            "\n\n",
            history.Select(item =>
                $$"""
                Attempt {{item.AttemptNo}} failed.
                Strategy: {{item.Strategy}}
                Failure tags: {{string.Join(", ", item.FailureKinds)}}
                Failure analysis: {{item.FailureAnalysis}}
                Failed candidate:
                {{item.CandidateText}}
                """));
    }

    private static string? ExtractCandidateText(string rawContent)
    {
        using var contentDoc = JsonDocument.Parse(rawContent);
        var root = contentDoc.RootElement;
        foreach (var propertyName in new[]
                 {
                     "rewrittenText",
                     "rewritten_text",
                     "final_email",
                     "finalEmail",
                     "candidate",
                     "candidateText",
                 })
        {
            if (root.TryGetProperty(propertyName, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
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

        if (normalized.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri($"{normalized}/chat/completions");
        }

        return new Uri($"{normalized}/v1/chat/completions");
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
}
