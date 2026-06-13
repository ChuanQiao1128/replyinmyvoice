using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.RewriteEngine;
using ReplyInMyVoice.Infrastructure;
using ReplyInMyVoice.Infrastructure.Providers;
using ReplyInMyVoice.Infrastructure.Resilience;

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
    public async Task OpenAiCompatibleRewriteModelClient_retries_429_response_through_registered_named_http_client()
    {
        var attemptCount = 0;
        var attemptTimes = new List<DateTimeOffset>();
        var handler = new RecordingHttpHandler(_ =>
        {
            attemptCount++;
            attemptTimes.Add(DateTimeOffset.UtcNow);

            if (attemptCount == 1)
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
                return Task.FromResult(response);
            }

            return Task.FromResult(JsonResponse(
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
                """));
        });
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENAI_BASE_URL"] = "https://api.deepseek.com",
                ["DEEPSEEK_API_KEY"] = "deepseek-test-key",
                ["OPENAI_MODEL_MID_WRITER"] = "deepseek-v4-pro",
                ["SAPLING_API_KEY"] = "sapling-test-key",
            })
            .Build();

        services.AddReplyInMyVoiceInfrastructure(configuration, "Testing");
        services
            .AddHttpClient(nameof(OpenAiCompatibleRewriteModelClient))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<IRewriteModelClient>()
            .GenerateCandidateAsync(ModelRequest(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CandidateText.Should().Be("Hi Jordan, I can send this today.");
        attemptCount.Should().Be(2);
        attemptTimes[1].Should().BeOnOrAfter(attemptTimes[0].AddMilliseconds(500));
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

    [Fact]
    public async Task Open_model_circuit_fails_fast_with_existing_error_code()
    {
        var innerHandlerInvocations = 0;
        using var provider = BuildProviderWithProviderCircuit(
            nameof(OpenAiCompatibleRewriteModelClient),
            _ =>
            {
                innerHandlerInvocations++;
                return Task.FromResult(JsonResponse(
                    """
                    {
                      "choices": [
                        {
                          "message": {
                            "content": "{\"rewrittenText\":\"This should not be used.\"}"
                          }
                        }
                      ]
                    }
                    """));
            });
        ForceOpenCircuit(provider, nameof(OpenAiCompatibleRewriteModelClient));

        var stopwatch = Stopwatch.StartNew();
        var result = await provider.GetRequiredService<IRewriteModelClient>()
            .GenerateCandidateAsync(ModelRequest(), CancellationToken.None);

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("model_network_failed");
        innerHandlerInvocations.Should().Be(0);
    }

    [Fact]
    public async Task Open_sapling_circuit_fails_fast_with_unavailable_signal()
    {
        var innerHandlerInvocations = 0;
        using var provider = BuildProviderWithProviderCircuit(
            nameof(SaplingWritingSignalClient),
            _ =>
            {
                innerHandlerInvocations++;
                return Task.FromResult(JsonResponse("""{"score":0.0}"""));
            });
        ForceOpenCircuit(provider, nameof(SaplingWritingSignalClient));

        var result = await provider.GetRequiredService<IWritingSignalClient>()
            .MeasureAsync("Please reply to Jordan today.", CancellationToken.None);

        result.Available.Should().BeFalse();
        innerHandlerInvocations.Should().Be(0);
    }

    private static ServiceProvider BuildProviderWithProviderCircuit(
        string clientName,
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENAI_BASE_URL"] = "https://api.deepseek.com",
                ["DEEPSEEK_API_KEY"] = "deepseek-test-key",
                ["OPENAI_MODEL_MID_WRITER"] = "deepseek-v4-pro",
                ["SAPLING_API_KEY"] = "sapling-test-key",
                ["PROVIDER_CIRCUIT_MIN_SAMPLES"] = "1",
                ["PROVIDER_CIRCUIT_FAILURE_RATIO"] = "1.0",
            })
            .Build();

        services.AddReplyInMyVoiceInfrastructure(configuration, "Testing");
        services
            .AddHttpClient(clientName)
            .ConfigurePrimaryHttpMessageHandler(() => new RecordingHttpHandler(handler));
        return services.BuildServiceProvider();
    }

    private static void ForceOpenCircuit(ServiceProvider provider, string providerName)
    {
        var breaker = provider.GetRequiredService<ProviderCircuitBreakerRegistry>().GetOrAdd(providerName);
        breaker.Record(breaker.Acquire(), success: false);
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
