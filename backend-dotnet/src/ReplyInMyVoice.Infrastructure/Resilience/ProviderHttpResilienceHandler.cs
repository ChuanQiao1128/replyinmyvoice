using System.Net;

namespace ReplyInMyVoice.Infrastructure.Resilience;

public sealed class ProviderHttpResilienceHandler(ProviderCircuitBreaker circuitBreaker) : DelegatingHandler
{
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(5);
    private const int MaxRetryAttempts = 3;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var snapshot = await BufferedHttpRequestSnapshot.CreateAsync(request, cancellationToken);
        var lease = circuitBreaker.Acquire();

        for (var attempt = 0; attempt <= MaxRetryAttempts; attempt++)
        {
            if (attempt > 0)
            {
                circuitBreaker.ThrowIfOpen();
            }

            try
            {
                using var attemptRequest = snapshot.CreateRequest();
                var response = await base.SendAsync(attemptRequest, cancellationToken);
                if (!IsTransientStatusCode(response.StatusCode))
                {
                    circuitBreaker.Record(lease, success: true);
                    return response;
                }

                if (attempt == MaxRetryAttempts || lease.IsProbe)
                {
                    circuitBreaker.Record(lease, success: false);
                    return response;
                }

                var delay = RetryDelay(attempt, response);
                response.Dispose();
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception exception) when (IsTransientException(exception, cancellationToken))
            {
                if (attempt == MaxRetryAttempts || lease.IsProbe)
                {
                    circuitBreaker.Record(lease, success: false);
                    throw;
                }

                await Task.Delay(RetryDelay(attempt, response: null), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                circuitBreaker.Record(lease, success: false);
                throw;
            }
        }

        throw new InvalidOperationException("Provider HTTP retry loop exited unexpectedly.");
    }

    private static TimeSpan RetryDelay(int attempt, HttpResponseMessage? response)
    {
        if (response?.Headers.RetryAfter?.Delta is { } retryAfterDelta &&
            retryAfterDelta > TimeSpan.Zero)
        {
            return retryAfterDelta + Jitter();
        }

        if (response?.Headers.RetryAfter?.Date is { } retryAfterDate)
        {
            var retryAfter = retryAfterDate - DateTimeOffset.UtcNow;
            if (retryAfter > TimeSpan.Zero)
            {
                return retryAfter + Jitter();
            }
        }

        var backoffMs = BaseRetryDelay.TotalMilliseconds * Math.Pow(2, attempt);
        var backoff = TimeSpan.FromMilliseconds(Math.Min(backoffMs, MaxRetryDelay.TotalMilliseconds));
        return backoff + Jitter();
    }

    private static TimeSpan Jitter() =>
        TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100));

    private static bool IsTransientStatusCode(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static bool IsTransientException(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException ||
        exception is TimeoutException ||
        exception is TaskCanceledException && !cancellationToken.IsCancellationRequested;

    private sealed record BufferedHttpRequestSnapshot(
        HttpMethod Method,
        Uri? RequestUri,
        Version Version,
        HttpVersionPolicy VersionPolicy,
        IReadOnlyList<KeyValuePair<string, IEnumerable<string>>> Headers,
        byte[]? Content,
        IReadOnlyList<KeyValuePair<string, IEnumerable<string>>> ContentHeaders)
    {
        public static async Task<BufferedHttpRequestSnapshot> CreateAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            byte[]? content = null;
            var contentHeaders = Array.Empty<KeyValuePair<string, IEnumerable<string>>>();
            if (request.Content is not null)
            {
                content = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                contentHeaders = request.Content.Headers.ToArray();
            }

            return new BufferedHttpRequestSnapshot(
                request.Method,
                request.RequestUri,
                request.Version,
                request.VersionPolicy,
                request.Headers.ToArray(),
                content,
                contentHeaders);
        }

        public HttpRequestMessage CreateRequest()
        {
            var request = new HttpRequestMessage(Method, RequestUri)
            {
                Version = Version,
                VersionPolicy = VersionPolicy,
            };

            foreach (var header in Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (Content is null)
            {
                return request;
            }

            request.Content = new ByteArrayContent(Content);
            foreach (var header in ContentHeaders)
            {
                request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return request;
        }
    }
}
