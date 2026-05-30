using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Infrastructure.Providers;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class RewriteCostTrackingTests
{
    private const decimal InputRatePer1K = 0.0100m;
    private const decimal OutputRatePer1K = 0.0200m;

    [Fact]
    public async Task RewriteWritesCostLog()
    {
        ConfigureRates();
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var reservedAttempt = await ReserveAttemptAsync(fixture, user.Id, "idem-cost-log");
        var processor = new RewriteJobProcessor(
            fixture.CreateContext,
            CreateOpenAiBackedProvider(promptTokens: 150, completionTokens: 50));

        await processor.ProcessAsync(new RewriteJob(reservedAttempt.AttemptId), CancellationToken.None);

        await using var db = fixture.CreateContext();
        var log = await db.RewriteCostLogs.Include(x => x.ProviderCalls).SingleAsync();
        var providerCall = log.ProviderCalls.Single();

        log.UserId.Should().Be(user.Id);
        log.RequestId.Should().Be(reservedAttempt.AttemptId.ToString());
        log.OpenAiInputTokens.Should().Be(150);
        log.OpenAiOutputTokens.Should().Be(50);
        log.OpenAiCostUsd.Should().BeGreaterThan(0);
        log.TotalEstimatedCostUsd.Should().Be(log.OpenAiCostUsd);
        providerCall.Model.Should().Be("deepseek-v4-pro");
        providerCall.InputTokens.Should().Be(150);
        providerCall.OutputTokens.Should().Be(50);
        providerCall.EstimatedCostUsd.Should().Be(log.OpenAiCostUsd);
        providerCall.LatencyMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task CostComputedFromRates()
    {
        ConfigureRates();
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var reservedAttempt = await ReserveAttemptAsync(fixture, user.Id, "idem-cost-rates");
        var processor = new RewriteJobProcessor(
            fixture.CreateContext,
            CreateOpenAiBackedProvider(promptTokens: 250, completionTokens: 125));

        await processor.ProcessAsync(new RewriteJob(reservedAttempt.AttemptId), CancellationToken.None);

        await using var db = fixture.CreateContext();
        var log = await db.RewriteCostLogs.Include(x => x.ProviderCalls).SingleAsync();
        var providerCall = log.ProviderCalls.Single();
        var expectedCost = (250m / 1000m * InputRatePer1K) + (125m / 1000m * OutputRatePer1K);

        log.OpenAiCostUsd.Should().Be(expectedCost);
        log.TotalEstimatedCostUsd.Should().Be(expectedCost);
        providerCall.EstimatedCostUsd.Should().Be(expectedCost);
    }

    private static void ConfigureRates()
    {
        Environment.SetEnvironmentVariable("REWRITE_COST_INPUT_PER_1K", InputRatePer1K.ToString("0.0000"));
        Environment.SetEnvironmentVariable("REWRITE_COST_OUTPUT_PER_1K", OutputRatePer1K.ToString("0.0000"));
    }

    private static async Task<ReserveRewriteResult> ReserveAttemptAsync(
        DbFixture fixture,
        Guid userId,
        string idempotencyKey)
    {
        var quota = new QuotaService(fixture.CreateContext);
        return await quota.ReserveAsync(
            userId,
            idempotencyKey,
            $"hash-{idempotencyKey}",
            """
            {
              "messageToReplyTo": "Jordan asked for the details.",
              "roughDraftReply": "Tell Jordan I can send this today.",
              "audience": "Client",
              "purpose": "Reply.",
              "factsToPreserve": "Preserve today.",
              "tone": "warm"
            }
            """,
            "free:lifetime",
            3,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(10));
    }

    private static IRewriteProvider CreateOpenAiBackedProvider(int promptTokens, int completionTokens)
    {
        var httpClient = new HttpClient(new RecordingHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                    {
                      "choices": [
                        {
                          "message": {
                            "content": "{\"rewrittenText\":\"Hi Jordan, I can send this today.\"}"
                          }
                        }
                      ],
                      "usage": {
                        "prompt_tokens": {{promptTokens}},
                        "completion_tokens": {{completionTokens}},
                        "total_tokens": {{promptTokens + completionTokens}}
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"),
            })));
        var client = new OpenAiCompatibleRewriteModelClient(
            httpClient,
            "test-key",
            "deepseek-v4-pro",
            "https://api.deepseek.com",
            TimeSpan.FromSeconds(5));
        return new OpenAiBackedRewriteProvider(client);
    }

    private sealed class OpenAiBackedRewriteProvider(IRewriteModelClient modelClient) : IRewriteProvider
    {
        public async Task<RewriteProviderResult> RewriteAsync(
            Guid attemptId,
            RewriteRequest request,
            CancellationToken cancellationToken)
        {
            var modelRequest = new RewriteModelRequest(
                attemptId,
                request,
                ReplyInMyVoice.Domain.RewriteEngine.RewriteInputAnalyzer.Analyze(request),
                ReplyInMyVoice.Domain.RewriteEngine.FactLedgerExtractor.Extract(request),
                ReplyInMyVoice.Domain.RewriteEngine.RewriteStrategy.FactsFirstReconstruct,
                []);
            var result = await modelClient.GenerateCandidateAsync(modelRequest, cancellationToken);
            return result.Success
                ? new RewriteProviderResult(
                    $$"""{"rewrittenText":{{System.Text.Json.JsonSerializer.Serialize(result.CandidateText)}},"changeSummary":[],"riskNotes":[]}""",
                    true,
                    null)
                : new RewriteProviderResult(null, false, result.ErrorCode);
        }
    }
}
