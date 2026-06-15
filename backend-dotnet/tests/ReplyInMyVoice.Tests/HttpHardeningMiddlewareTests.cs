using System.Collections;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ReplyInMyVoice.Functions.Http;

namespace ReplyInMyVoice.Tests;

public sealed class HttpHardeningMiddlewareTests
{
    [Fact]
    public async Task Invoke_HttpTriggerException_ReturnsInternalErrorEnvelopeWithCorrelationId()
    {
        const string correlationId = "req-http-failure";
        const string exceptionMessage = "sensitive storage failure details";
        var http = CreateHttpContext();
        http.Request.Headers[HttpHardeningMiddleware.CorrelationHeaderName] = correlationId;
        var context = new TestFunctionContext("SubmitRewrite", http);
        var middleware = CreateMiddleware();

        var act = () => middleware.Invoke(
            context,
            _ => throw new InvalidOperationException(exceptionMessage));

        await act.Should().NotThrowAsync();

        http.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        http.Response.ContentType.Should().Be("application/json");
        http.Response.Headers[HttpHardeningMiddleware.CorrelationHeaderName].ToString().Should().Be(correlationId);

        using var body = ReadResponseJson(http);
        var error = body.RootElement.GetProperty("error");
        error.GetProperty("code").GetString().Should().Be("internal_error");
        error.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        error.GetProperty("requestId").GetString().Should().Be(correlationId);
        body.RootElement.ToString().Should().NotContain(exceptionMessage);
    }

    [Fact]
    public async Task Invoke_NonHttpTriggerException_Propagates()
    {
        var failure = new InvalidOperationException("queue payload was invalid");
        var context = new TestFunctionContext("ProcessRewriteJob");
        var middleware = CreateMiddleware();

        var act = () => middleware.Invoke(
            context,
            _ => throw failure);

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Should().BeSameAs(failure);
    }

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

    [Fact]
    public void InternalErrorJson_MatchesFunctionHttpResultsCodedEnvelope()
    {
        const string correlationId = "req-internal-error";
        using var middlewareJson = JsonDocument.Parse(HttpHardeningMiddleware.BuildInternalErrorJson(correlationId));
        var result = FunctionHttpResults.InternalError(correlationId)
            .Should()
            .BeOfType<ObjectResult>()
            .Subject;
        using var resultJson = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));

        middlewareJson.RootElement.EnumerateObject().Select(x => x.Name).Should().Equal("error");
        resultJson.RootElement.EnumerateObject().Select(x => x.Name).Should().Equal("error");

        var middlewareError = middlewareJson.RootElement.GetProperty("error");
        var resultError = resultJson.RootElement.GetProperty("error");
        resultError.GetProperty("code").GetString().Should().Be(middlewareError.GetProperty("code").GetString());
        resultError.GetProperty("message").GetString().Should().Be(middlewareError.GetProperty("message").GetString());
        resultError.GetProperty("requestId").GetString().Should().Be(correlationId);
        middlewareError.GetProperty("requestId").GetString().Should().Be(correlationId);
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

    private static HttpHardeningMiddleware CreateMiddleware() =>
        new(new ConfigurationBuilder().Build(), NullLogger<HttpHardeningMiddleware>.Instance);

    private sealed class TestFunctionContext : FunctionContext
    {
        private const string HttpContextKey = "HttpRequestContext";
        private readonly TestFunctionDefinition _definition;
        private readonly TestInvocationFeatures _features = new();

        public TestFunctionContext(string functionName, HttpContext? http = null)
        {
            _definition = new TestFunctionDefinition(functionName);
            if (http is not null)
            {
                Items[HttpContextKey] = http;
            }
        }

        public override string InvocationId { get; } = Guid.NewGuid().ToString("D");
        public override string FunctionId { get; } = Guid.NewGuid().ToString("D");
        public override TraceContext TraceContext => null!;
        public override BindingContext BindingContext => null!;
        public override RetryContext RetryContext => null!;
        public override IServiceProvider InstanceServices { get; set; } = EmptyServiceProvider.Instance;
        public override FunctionDefinition FunctionDefinition => _definition;
        public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();
        public override IInvocationFeatures Features => _features;
    }

    private sealed class TestFunctionDefinition(string name) : FunctionDefinition
    {
        public override ImmutableArray<FunctionParameter> Parameters { get; } = [];
        public override string PathToAssembly { get; } = string.Empty;
        public override string EntryPoint { get; } = string.Empty;
        public override string Id { get; } = Guid.NewGuid().ToString("D");
        public override string Name { get; } = name;
        public override IImmutableDictionary<string, BindingMetadata> InputBindings { get; } =
            ImmutableDictionary<string, BindingMetadata>.Empty;
        public override IImmutableDictionary<string, BindingMetadata> OutputBindings { get; } =
            ImmutableDictionary<string, BindingMetadata>.Empty;
    }

    private sealed class TestInvocationFeatures : IInvocationFeatures
    {
        private readonly Dictionary<Type, object> _features = [];

        public void Set<T>(T instance)
        {
            if (instance is null)
            {
                _features.Remove(typeof(T));
                return;
            }

            _features[typeof(T)] = instance;
        }

        public T? Get<T>() =>
            _features.TryGetValue(typeof(T), out var feature)
                ? (T)feature
                : default;

        public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => _features.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static EmptyServiceProvider Instance { get; } = new();

        public object? GetService(Type serviceType) => null;
    }
}
