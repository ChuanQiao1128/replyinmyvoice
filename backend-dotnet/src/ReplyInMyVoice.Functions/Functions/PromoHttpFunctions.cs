using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class PromoHttpFunctions(
    IConfiguration configuration,
    PromoService promoService,
    AccountService accountService)
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
            var result = await promoService.RedeemAsync(
                authUser.ExternalAuthUserId,
                authUser.Email,
                body.Code,
                ResolveTrustedClientIp(request),
                DateTimeOffset.UtcNow,
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
            var status = await promoService.GetStatusAsync(
                authUser.ExternalAuthUserId,
                authUser.Email,
                DateTimeOffset.UtcNow,
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
        PromoRedeemResult result,
        FunctionAuthUser authUser,
        CancellationToken cancellationToken)
    {
        switch (result.Kind)
        {
            case PromoRedeemResultKind.Success:
                var account = await accountService.GetOrCreateAccountSummaryAsync(
                    authUser.ExternalAuthUserId,
                    authUser.Email,
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

    private string? ResolveTrustedClientIp(HttpRequest request)
    {
        var candidateIp = request.Headers[ClientIpHeader].ToString();
        if (string.IsNullOrWhiteSpace(candidateIp))
        {
            return null;
        }

        var expectedSecret = configuration["PROMO_PROXY_SHARED_SECRET"];
        var suppliedSecret = request.Headers[ProxySecretHeader].ToString();
        if (string.IsNullOrWhiteSpace(expectedSecret) ||
            string.IsNullOrWhiteSpace(suppliedSecret) ||
            !SecretsMatch(expectedSecret, suppliedSecret))
        {
            return null;
        }

        return candidateIp;
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
}
