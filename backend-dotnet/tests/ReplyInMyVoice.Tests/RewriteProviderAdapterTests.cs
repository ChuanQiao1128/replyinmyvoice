using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.RewriteEngine;
using ReplyInMyVoice.Infrastructure.Providers;

namespace ReplyInMyVoice.Tests;

public sealed class RewriteProviderAdapterTests
{
    [Fact]
    public async Task OpenAiCompatibleRewriteModelClient_posts_json_chat_completion_to_deepseek_base_url()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var httpClient = new HttpClient(new RecordingHttpHandler(async request =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(
                """
                {
                  "choices": [
                    {
                      "message": {
                        "content": "{\"rewrittenText\":\"Hi Jordan, I can send this today.\"}"
                      }
                    }
                  ]
                }
                """);
        }));
        var client = new OpenAiCompatibleRewriteModelClient(
            httpClient,
            "deepseek-test-key",
            "deepseek-v4-pro",
            "https://api.deepseek.com",
            TimeSpan.FromSeconds(5));

        var result = await client.GenerateCandidateAsync(ModelRequest(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CandidateText.Should().Be("Hi Jordan, I can send this today.");
        capturedRequest!.RequestUri!.ToString().Should().Be("https://api.deepseek.com/v1/chat/completions");
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Authorization.Parameter.Should().Be("deepseek-test-key");
        using var body = JsonDocument.Parse(capturedBody!);
        body.RootElement.GetProperty("model").GetString().Should().Be("deepseek-v4-pro");
        body.RootElement.GetProperty("response_format").GetProperty("type").GetString().Should().Be("json_object");
        body.RootElement.GetProperty("thinking").GetProperty("type").GetString().Should().Be("disabled");
    }

    [Fact]
    public async Task SaplingWritingSignalClient_posts_aidetect_request_and_uses_mean_of_scorable_sentences()
    {
        string? capturedBody = null;
        var httpClient = new HttpClient(new RecordingHttpHandler(async request =>
        {
            request.RequestUri!.ToString().Should().Be("https://api.sapling.ai/api/v1/aidetect");
            capturedBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(
                """
                {
                  "score": 1.0,
                  "sentence_scores": [
                    { "sentence": "1.", "score": 1.0 },
                    { "sentence": "The base arrived cracked.", "score": 0.0 },
                    { "sentence": "I have flagged it with the team.", "score": 0.0 },
                    { "sentence": "I will follow up by Friday.", "score": 0.0 },
                    { "sentence": "Sorry for the hassle.", "score": 1.0 }
                  ]
                }
                """);
        }));
        var client = new SaplingWritingSignalClient(httpClient, "sapling-test-key", TimeSpan.FromSeconds(5));

        var result = await client.MeasureAsync("...", CancellationToken.None);

        result.Available.Should().BeTrue();
        // Scorable sentence scores are [0, 0, 0, 100] (the "1." list marker is excluded).
        // MEAN = 25. The previous MEDIAN would have returned 0 here, collapsing the signal to
        // the floor and hiding the one canned sentence — the exact failure this metric fixes.
        result.AiLikePercent.Should().Be(25);
        using var body = JsonDocument.Parse(capturedBody!);
        body.RootElement.GetProperty("key").GetString().Should().Be("sapling-test-key");
        body.RootElement.GetProperty("sent_scores").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("score_string").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task SaplingWritingSignalClient_does_not_let_one_outlier_sentence_pin_a_multi_sentence_email_to_100()
    {
        var httpClient = new HttpClient(new RecordingHttpHandler(_ =>
            Task.FromResult(JsonResponse(
                """
                {
                  "score": 1.0,
                  "sentence_scores": [
                    { "sentence": "Invoice INV-8842 covers 62 users at $186.00.", "score": 0.0 },
                    { "sentence": "The renewal date is April 30.", "score": 0.0 },
                    { "sentence": "The credit applies to the next cycle.", "score": 0.0 },
                    { "sentence": "Please let me know how you would like to proceed.", "score": 1.0 }
                  ]
                }
                """))));
        var client = new SaplingWritingSignalClient(httpClient, "sapling-test-key", TimeSpan.FromSeconds(5));

        var result = await client.MeasureAsync("...", CancellationToken.None);

        // One boilerplate sentence (100%) among three concrete ones (0%) yields mean 25, not
        // the raw document score of 100 — so a fact-complete email is not failed on a single
        // stock closer (the original outlier-domination bug the old raw-overall score had).
        result.AiLikePercent.Should().Be(25);
    }

    private static RewriteModelRequest ModelRequest()
    {
        var request = new RewriteRequest(
            "Jordan asked for the details.",
            "Tell Jordan I can send this today.",
            "Client",
            "Reply.",
            null,
            "Preserve today.",
            "warm");
        var analysis = RewriteInputAnalyzer.Analyze(request);
        return new RewriteModelRequest(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            request,
            analysis,
            FactLedgerExtractor.Extract(request),
            RewriteStrategy.FactsFirstReconstruct,
            []);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
}

internal sealed class RecordingHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        handler(request);
}
