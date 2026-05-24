using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Infrastructure.Providers;

public sealed class OpenAiRewriteProvider(
    HttpClient httpClient,
    string apiKey,
    string model,
    string baseUrl,
    TimeSpan timeout) : IRewriteProvider
{
    public async Task<RewriteProviderResult> RewriteAsync(Guid attemptId, RewriteRequest rewriteRequest, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var payload = new
        {
            model,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = "Return strict JSON for a concise, natural reply rewrite. Include rewrittenText, changeSummary, and riskNotes." },
                new { role = "user", content = BuildPrompt(attemptId, rewriteRequest) }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri(baseUrl));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            using var response = await httpClient.SendAsync(request, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new RewriteProviderResult(null, false, "openai_failed");
            }

            var json = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
            {
                return new RewriteProviderResult(null, false, "openai_empty");
            }

            JsonDocument.Parse(content);
            return new RewriteProviderResult(content, true, null);
        }
        catch (OperationCanceledException)
        {
            return new RewriteProviderResult(null, false, "openai_timeout");
        }
        catch (JsonException)
        {
            return new RewriteProviderResult(null, false, "openai_json_parse_failed");
        }
        catch (HttpRequestException)
        {
            return new RewriteProviderResult(null, false, "openai_network_failed");
        }
    }

    private static Uri BuildChatCompletionsUri(string configuredBaseUrl)
    {
        var normalizedBaseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? "https://api.openai.com/v1"
            : configuredBaseUrl.Trim();

        if (!normalizedBaseUrl.EndsWith("/", StringComparison.Ordinal))
        {
            normalizedBaseUrl += "/";
        }

        return new Uri(new Uri(normalizedBaseUrl), "chat/completions");
    }

    private static string BuildPrompt(Guid attemptId, RewriteRequest request) =>
        $"""
        Rewrite attempt: {attemptId}
        Tone: {request.Tone}
        Message to reply to:
        {request.MessageToReplyTo}

        Rough draft reply:
        {request.RoughDraftReply}

        Audience:
        {request.Audience}

        Purpose:
        {request.Purpose}

        What actually happened:
        {request.WhatHappened}

        Facts to preserve:
        {request.FactsToPreserve}

        Return JSON only with these fields: rewrittenText string, changeSummary string array, riskNotes string array.
        Do not invent names, dates, prices, timelines, discounts, policies, promises, or outcomes.
        """;
}
