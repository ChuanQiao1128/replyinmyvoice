using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Account;
using ReplyInMyVoice.Application.UseCases.Promo;
using ReplyInMyVoice.Functions.Auth;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class PromoHttpFunctions(
    IConfiguration configuration,
    RedeemPromoHandler redeemPromoHandler,
    GetPromoStatusHandler getPromoStatusHandler,
    GetAccountSummaryHandler getAccountSummaryHandler)
{
    private const string ProxySecretHeader = "X-RIMV-Proxy-Secret";
    private const string ClientIpHeader = "X-Client-IP";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Function("RedeemPromoCode")]
    public async Task<IActionResult> RedeemPromoCode(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "promo/redeem")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var authUser = await FunctionAuthResolver.ResolveUserAsync(request, configuration, cancellationToken);
        if (authUser is null)
        {
            return Error("authentication_required", StatusCodes.Status401Unauthorized);
        }

        PromoRedeemRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<PromoRedeemRequest>(
                request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return Error("invalid_request", StatusCodes.Status400BadRequest);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Code))
        {
            return Error("invalid_request", StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(body.TurnstileToken))
        {
            return Error("invalid_captcha", StatusCodes.Status403Forbidden);
        }

        try
        {
            var clientIp = ResolveTrustedClientIp(request);
            if (clientIp.ServerConfigError)
            {
                return Error("server_config", StatusCodes.Status500InternalServerError);
            }

            var result = await redeemPromoHandler.HandleAsync(
                new RedeemPromoCommand(
                    authUser.ExternalAuthUserId,
                    authUser.Email,
                    body.Code,
                    clientIp.IpHash,
                    DateTimeOffset.UtcNow),
                cancellationToken);

            return await MapRedeemResultAsync(result, authUser, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Error("server_error", StatusCodes.Status500InternalServerError);
        }
    }

    [Function("GetPromoStatus")]
    public async Task<IActionResult> GetPromoStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "promo/status")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var authUser = await FunctionAuthResolver.ResolveUserAsync(request, configuration, cancellationToken);
        if (authUser is null)
        {
            return Error("authentication_required", StatusCodes.Status401Unauthorized);
        }

        try
        {
            var status = await getPromoStatusHandler.HandleAsync(
                new GetPromoStatusQuery(
                    authUser.ExternalAuthUserId,
                    authUser.Email,
                    DateTimeOffset.UtcNow),
                cancellationToken);

            return new OkObjectResult(new PromoStatusResponse(
                status.HasRedeemed,
                status.Eligible,
                status.TrialRemaining,
                status.TrialExpiresAt));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Error("server_error", StatusCodes.Status500InternalServerError);
        }
    }

    private async Task<IActionResult> MapRedeemResultAsync(
        PromoRedeemResultDto result,
        FunctionAuthUser authUser,
        CancellationToken cancellationToken)
    {
        switch (result.Kind)
        {
            case PromoRedeemResultKind.Success:
                var account = await getAccountSummaryHandler.HandleAsync(
                    new GetAccountSummaryQuery(authUser.ExternalAuthUserId, authUser.Email),
                    cancellationToken);
                return new OkObjectResult(new PromoRedeemResponse(
                    result.CreditsGranted,
                    account.Usage.Remaining,
                    result.ExpiresAt,
                    AlreadyRedeemed: false));

            case PromoRedeemResultKind.InvalidCode:
                return Error("invalid_code", StatusCodes.Status422UnprocessableEntity);

            case PromoRedeemResultKind.Expired:
                return Error("code_expired", StatusCodes.Status422UnprocessableEntity);

            case PromoRedeemResultKind.AlreadyRedeemed:
                return Error("already_redeemed", StatusCodes.Status409Conflict);

            case PromoRedeemResultKind.CapReached:
                return Error("code_exhausted", StatusCodes.Status409Conflict);

            case PromoRedeemResultKind.IpVelocityBlocked:
                return Error("ip_velocity", StatusCodes.Status429TooManyRequests);

            case PromoRedeemResultKind.ServerConfig:
                return Error("server_config", StatusCodes.Status500InternalServerError);

            default:
                return Error("server_error", StatusCodes.Status500InternalServerError);
        }
    }

    private PromoClientIpResolution ResolveTrustedClientIp(HttpRequest request)
    {
        var isProduction = IsProductionEnvironment();
        var expectedSecret = configuration["PROMO_PROXY_SHARED_SECRET"];
        if (isProduction && string.IsNullOrWhiteSpace(expectedSecret))
        {
            return PromoClientIpResolution.ServerConfig();
        }

        var candidateIp = request.Headers[ClientIpHeader].ToString();
        if (string.IsNullOrWhiteSpace(candidateIp))
        {
            return isProduction
                ? PromoClientIpResolution.ServerConfig()
                : PromoClientIpResolution.Skip();
        }

        var suppliedSecret = request.Headers[ProxySecretHeader].ToString();
        if (string.IsNullOrWhiteSpace(expectedSecret) ||
            string.IsNullOrWhiteSpace(suppliedSecret) ||
            !SecretsMatch(expectedSecret, suppliedSecret))
        {
            return isProduction
                ? PromoClientIpResolution.ServerConfig()
                : PromoClientIpResolution.Skip();
        }

        var normalizedIp = NormalizeTrustedClientIp(candidateIp);
        if (normalizedIp is null)
        {
            return isProduction
                ? PromoClientIpResolution.ServerConfig()
                : PromoClientIpResolution.Skip();
        }

        var salt = configuration["PROMO_IP_HASH_SALT"];
        if (string.IsNullOrWhiteSpace(salt))
        {
            return PromoClientIpResolution.ServerConfig();
        }

        return PromoClientIpResolution.WithHash(HashIp(normalizedIp, salt));
    }

    private bool IsProductionEnvironment()
    {
        var environmentName = configuration["DOTNET_ENVIRONMENT"]
            ?? configuration["ASPNETCORE_ENVIRONMENT"]
            ?? configuration["AZURE_FUNCTIONS_ENVIRONMENT"];
        return string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeTrustedClientIp(string? trustedClientIp)
    {
        var trimmed = trustedClientIp?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return IPAddress.TryParse(trimmed, out var parsedIp)
            ? parsedIp.ToString()
            : null;
    }

    private static string HashIp(string trustedClientIp, string salt)
    {
        var payload = Encoding.UTF8.GetBytes($"{salt}:{trustedClientIp}");
        var hash = SHA256.HashData(payload);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool SecretsMatch(string expectedSecret, string suppliedSecret)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSecret);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedSecret);
        return expectedBytes.Length == suppliedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }

    private static ObjectResult Error(string error, int statusCode) =>
        new(new PromoErrorResponse(error))
        {
            StatusCode = statusCode,
        };

    private sealed record PromoRedeemRequest(string? Code, string? TurnstileToken);

    private sealed record PromoRedeemResponse(
        int CreditsGranted,
        int TotalRemaining,
        DateTimeOffset? ExpiresAt,
        bool AlreadyRedeemed);

    private sealed record PromoStatusResponse(
        bool HasRedeemed,
        bool Eligible,
        int TrialRemaining,
        DateTimeOffset? TrialExpiresAt);

    private sealed record PromoErrorResponse(string Error);

    private sealed record PromoClientIpResolution(bool ServerConfigError, string? IpHash)
    {
        public static PromoClientIpResolution ServerConfig() => new(true, null);

        public static PromoClientIpResolution Skip() => new(false, null);

        public static PromoClientIpResolution WithHash(string ipHash) => new(false, ipHash);
    }
}
