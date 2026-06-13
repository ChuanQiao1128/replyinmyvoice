using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Functions.Http;

namespace ReplyInMyVoice.Tests;

public sealed class HttpHardeningMiddlewareTests
{
    [Fact]
    public async Task OversizedContentLength_Returns413WithPayloadTooLargeEnvelope()
    {
        var http = CreateHttpContext();
        http.Request.Method = HttpMethods.Post;
        http.Request.ContentLength = HttpHardeningMiddleware.DefaultMaxRequestBodyBytes + 1;

        var rejected = await HttpHardeningMiddleware.TryRejectOversizedBodyAsync(
            http,
            HttpHardeningMiddleware.DefaultMaxRequestBodyBytes,
            CancellationToken.None);

        rejected.Should().BeTrue();
        http.Response.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        http.Response.ContentType.Should().Be("application/json");
        var body = ReadResponseJson(http);
        body.RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be("payload_too_large");
        body.RootElement.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ContentLengthAtLimit_PassesThroughAndPreservesBodyBytes()
    {
        var bytes = Enumerable.Repeat((byte)'a', (int)HttpHardeningMiddleware.DefaultMaxRequestBodyBytes).ToArray();
        var http = CreateHttpContext(bytes);
        http.Request.Method = HttpMethods.Post;
        http.Request.ContentLength = bytes.Length;

        var rejected = await HttpHardeningMiddleware.TryRejectOversizedBodyAsync(
            http,
            HttpHardeningMiddleware.DefaultMaxRequestBodyBytes,
            CancellationToken.None);

        rejected.Should().BeFalse();
        http.Request.Body.CanSeek.Should().BeTrue();
        http.Request.Body.Position.Should().Be(0);
        (await ReadRequestBytesAsync(http)).Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public async Task MissingContentLength_BodyOverLimit_Returns413()
    {
        var http = CreateHttpContext(Enumerable.Repeat((byte)'b', 70_000).ToArray());
        http.Request.Method = HttpMethods.Post;
        http.Request.ContentLength = null;

        var rejected = await HttpHardeningMiddleware.TryRejectOversizedBodyAsync(
            http,
            HttpHardeningMiddleware.DefaultMaxRequestBodyBytes,
            CancellationToken.None);

        rejected.Should().BeTrue();
        http.Response.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        var body = ReadResponseJson(http);
        body.RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be("payload_too_large");
    }

    [Fact]
    public async Task MissingContentLength_BodyUnderLimit_PassesThroughIntact()
    {
        var bytes = Encoding.UTF8.GetBytes("""{"draft":"Please send the update tomorrow."}""");
        var http = CreateHttpContext(bytes);
        http.Request.Method = HttpMethods.Post;
        http.Request.ContentLength = null;

        var rejected = await HttpHardeningMiddleware.TryRejectOversizedBodyAsync(
            http,
            HttpHardeningMiddleware.DefaultMaxRequestBodyBytes,
            CancellationToken.None);

        rejected.Should().BeFalse();
        http.Request.Body.Position.Should().Be(0);
        (await ReadRequestBytesAsync(http)).Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public void ResolveCorrelationId_ValidHeader_IsReturnedVerbatim()
    {
        var http = CreateHttpContext();
        http.Request.Headers[HttpHardeningMiddleware.CorrelationHeaderName] = "req-abc.123_X";

        var correlationId = HttpHardeningMiddleware.ResolveCorrelationId(http.Request);

        correlationId.Should().Be("req-abc.123_X");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("bad\nvalue")]
    [InlineData("spaces here")]
    public void ResolveCorrelationId_MissingTooLongOrUnsafeHeader_GeneratesGuid(string? headerValue)
    {
        var http = CreateHttpContext();
        if (headerValue is not null)
        {
            http.Request.Headers[HttpHardeningMiddleware.CorrelationHeaderName] = headerValue;
        }

        var correlationId = HttpHardeningMiddleware.ResolveCorrelationId(http.Request);

        Guid.TryParseExact(correlationId, "D", out _).Should().BeTrue();
    }

    [Fact]
    public void ResolveBodyLimitBytes_DefaultsAndStripeOverrideAndEnvOverrides()
    {
        var emptyConfig = new ConfigurationBuilder().Build();

        HttpHardeningMiddleware.ResolveBodyLimitBytes(emptyConfig, "SubmitRewrite")
            .Should()
            .Be(HttpHardeningMiddleware.DefaultMaxRequestBodyBytes);
        HttpHardeningMiddleware.ResolveBodyLimitBytes(emptyConfig, "StripeWebhook")
            .Should()
            .Be(HttpHardeningMiddleware.DefaultStripeWebhookMaxRequestBodyBytes);

        var overrideConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HTTP_MAX_REQUEST_BODY_BYTES"] = "131072",
                ["HTTP_MAX_REQUEST_BODY_BYTES_STRIPE_WEBHOOK"] = "2097152",
            })
            .Build();

        HttpHardeningMiddleware.ResolveBodyLimitBytes(overrideConfig, "SubmitRewrite").Should().Be(131_072);
        HttpHardeningMiddleware.ResolveBodyLimitBytes(overrideConfig, "StripeWebhook").Should().Be(2_097_152);

        var lowConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HTTP_MAX_REQUEST_BODY_BYTES"] = "12",
                ["HTTP_MAX_REQUEST_BODY_BYTES_STRIPE_WEBHOOK"] = "64",
            })
            .Build();

        HttpHardeningMiddleware.ResolveBodyLimitBytes(lowConfig, "SubmitRewrite").Should().Be(1_024);
        HttpHardeningMiddleware.ResolveBodyLimitBytes(lowConfig, "StripeWebhook").Should().Be(1_024);
    }

    [Fact]
    public void PayloadTooLargeJson_MatchesFunctionHttpResultsCodedEnvelope()
    {
        using var middlewareJson = JsonDocument.Parse(
            HttpHardeningMiddleware.BuildPayloadTooLargeJson(HttpHardeningMiddleware.DefaultMaxRequestBodyBytes));
        var result = FunctionHttpResults.PayloadTooLarge(HttpHardeningMiddleware.DefaultMaxRequestBodyBytes)
            .Should()
            .BeOfType<ObjectResult>()
            .Subject;
        using var resultJson = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));

        middlewareJson.RootElement.EnumerateObject().Select(x => x.Name).Should().Equal("error");
        resultJson.RootElement.EnumerateObject().Select(x => x.Name).Should().Equal("error");
        resultJson.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should()
            .Be(middlewareJson.RootElement.GetProperty("error").GetProperty("code").GetString());
        resultJson.RootElement.GetProperty("error").GetProperty("message").GetString()
            .Should()
            .Be(middlewareJson.RootElement.GetProperty("error").GetProperty("message").GetString());
    }

    private static DefaultHttpContext CreateHttpContext(byte[]? body = null)
    {
        var http = new DefaultHttpContext();
        http.Request.Body = new MemoryStream(body ?? []);
        http.Response.Body = new MemoryStream();
        return http;
    }

    private static JsonDocument ReadResponseJson(HttpContext http)
    {
        http.Response.Body.Position = 0;
        return JsonDocument.Parse(http.Response.Body);
    }

    private static async Task<byte[]> ReadRequestBytesAsync(HttpContext http)
    {
        using var copy = new MemoryStream();
        await http.Request.Body.CopyToAsync(copy);
        return copy.ToArray();
    }
}
