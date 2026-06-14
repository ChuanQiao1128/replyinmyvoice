using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ReplyInMyVoice.Functions.Http;

public sealed class HttpHardeningMiddleware(
    IConfiguration configuration,
    ILogger<HttpHardeningMiddleware> logger) : IFunctionsWorkerMiddleware
{
    public const string CorrelationHeaderName = "X-Correlation-Id";
    public const long DefaultMaxRequestBodyBytes = 65_536;
    public const long DefaultStripeWebhookMaxRequestBodyBytes = 1_048_576;

    private const string GenericLimitSettingName = "HTTP_MAX_REQUEST_BODY_BYTES";
    private const string StripeWebhookLimitSettingName = "HTTP_MAX_REQUEST_BODY_BYTES_STRIPE_WEBHOOK";
    private const long MinimumRequestBodyBytes = 1_024;

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var http = context.GetHttpContext();
        if (http is null)
        {
            await next(context);
            return;
        }

        var correlationId = ResolveCorrelationId(http.Request);
        http.Items["CorrelationId"] = correlationId;
        http.Response.Headers[CorrelationHeaderName] = correlationId;

        var limit = ResolveBodyLimitBytes(configuration, context.FunctionDefinition.Name);
        if (await TryRejectOversizedBodyAsync(http, limit, context.CancellationToken))
        {
            return;
        }

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
        });
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!http.Response.HasStarted)
        {
            logger.LogError(ex, "HTTP function failed with an unhandled exception.");
            await WriteInternalErrorAsync(http, correlationId, context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HTTP function failed after the response started.");
            throw;
        }
    }

    public static string ResolveCorrelationId(HttpRequest request)
    {
        var raw = request.Headers[CorrelationHeaderName].ToString();
        if (raw.Length is >= 1 and <= 64 && raw.All(IsCorrelationIdChar))
        {
            return raw;
        }

        return Guid.NewGuid().ToString("D");
    }

    public static long ResolveBodyLimitBytes(IConfiguration config, string functionName)
    {
        return string.Equals(functionName, "StripeWebhook", StringComparison.Ordinal)
            ? ReadLimitBytes(config, StripeWebhookLimitSettingName, DefaultStripeWebhookMaxRequestBodyBytes)
            : ReadLimitBytes(config, GenericLimitSettingName, DefaultMaxRequestBodyBytes);
    }

    public static async Task<bool> TryRejectOversizedBodyAsync(
        HttpContext http,
        long limit,
        CancellationToken cancellationToken)
    {
        if (http.Request.ContentLength is { } contentLength && contentLength > limit)
        {
            await WritePayloadTooLargeAsync(http, limit, cancellationToken);
            return true;
        }

        if (!CanCarryBody(http.Request))
        {
            return false;
        }

        var memory = new MemoryStream();
        var buffer = new byte[8 * 1024];
        long copied = 0;

        while (copied <= limit)
        {
            var bytesToRead = (int)Math.Min(buffer.Length, limit + 1 - copied);
            if (bytesToRead <= 0)
            {
                break;
            }

            var read = await http.Request.Body.ReadAsync(
                buffer.AsMemory(0, bytesToRead),
                cancellationToken);
            if (read == 0)
            {
                break;
            }

            copied += read;
            if (copied > limit)
            {
                await WritePayloadTooLargeAsync(http, limit, cancellationToken);
                return true;
            }

            await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        memory.Position = 0;
        http.Request.Body = memory;
        return false;
    }

    public static string BuildPayloadTooLargeJson(long limit)
    {
        return JsonSerializer.Serialize(new
        {
            error = new
            {
                code = "payload_too_large",
                message = $"Request body must be {limit} bytes or smaller.",
            },
        });
    }

    public static string BuildInternalErrorJson(string correlationId)
    {
        return JsonSerializer.Serialize(new
        {
            error = new
            {
                code = FunctionHttpResults.DefaultErrorCode(StatusCodes.Status500InternalServerError),
                message = FunctionHttpResults.InternalErrorMessage,
                requestId = correlationId,
            },
        });
    }

    private static async Task WritePayloadTooLargeAsync(
        HttpContext http,
        long limit,
        CancellationToken cancellationToken)
    {
        http.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        http.Response.ContentType = "application/json";
        await http.Response.WriteAsync(BuildPayloadTooLargeJson(limit), cancellationToken);
    }

    private static async Task WriteInternalErrorAsync(
        HttpContext http,
        string correlationId,
        CancellationToken cancellationToken)
    {
        http.Response.StatusCode = StatusCodes.Status500InternalServerError;
        http.Response.ContentType = "application/json";
        http.Response.Headers[CorrelationHeaderName] = correlationId;
        await http.Response.WriteAsync(BuildInternalErrorJson(correlationId), cancellationToken);
    }

    private static bool CanCarryBody(HttpRequest request)
    {
        if (request.ContentLength > 0)
        {
            return true;
        }

        return request.ContentLength is null &&
            (HttpMethods.IsPost(request.Method) ||
                HttpMethods.IsPut(request.Method) ||
                HttpMethods.IsPatch(request.Method) ||
                HttpMethods.IsDelete(request.Method));
    }

    private static long ReadLimitBytes(IConfiguration config, string settingName, long fallback)
    {
        return long.TryParse(config[settingName], out var configured)
            ? Math.Max(MinimumRequestBodyBytes, configured)
            : fallback;
    }

    private static bool IsCorrelationIdChar(char value)
    {
        return value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-';
    }
}
