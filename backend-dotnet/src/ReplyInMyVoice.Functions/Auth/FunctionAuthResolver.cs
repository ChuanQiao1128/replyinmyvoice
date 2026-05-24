using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace ReplyInMyVoice.Functions.Auth;

public static class FunctionAuthResolver
{
    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> ConfigurationManagers = new();

    public static async Task<FunctionAuthUser?> ResolveUserAsync(
        HttpRequest request,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var headerUserId = ResolveHeaderExternalUserId(request, configuration);
        if (!string.IsNullOrWhiteSpace(headerUserId))
        {
            return new FunctionAuthUser(headerUserId, ResolveHeaderEmail(request));
        }

        if (request.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            var principalUserId = ResolveUserIdFromClaims(request.HttpContext.User);
            if (!string.IsNullOrWhiteSpace(principalUserId))
            {
                return new FunctionAuthUser(principalUserId, ResolveEmail(request.HttpContext.User));
            }
        }

        var bearerToken = ResolveBearerToken(request);
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return null;
        }

        var principal = await ValidateBearerTokenAsync(bearerToken, configuration, cancellationToken);
        if (principal is null)
        {
            return null;
        }

        var externalUserId = ResolveUserIdFromClaims(principal);
        return string.IsNullOrWhiteSpace(externalUserId)
            ? null
            : new FunctionAuthUser(externalUserId, ResolveEmail(principal));
    }

    public static string? ResolveExternalUserId(HttpRequest request, IConfiguration configuration)
    {
        var headerUserId = ResolveHeaderExternalUserId(request, configuration);
        if (!string.IsNullOrWhiteSpace(headerUserId))
        {
            return headerUserId;
        }

        return request.HttpContext.User.Identity?.IsAuthenticated == true
            ? ResolveUserIdFromClaims(request.HttpContext.User)
            : null;
    }

    public static string? ResolveEmail(HttpRequest request)
    {
        return ResolveEmail(request.HttpContext.User);
    }

    private static string? ResolveHeaderEmail(HttpRequest request)
    {
        var headerEmail = request.Headers["X-User-Email"].ToString();
        return string.IsNullOrWhiteSpace(headerEmail) ? null : headerEmail;
    }

    private static string? ResolveHeaderExternalUserId(HttpRequest request, IConfiguration configuration)
    {
        if (!AllowHeaderAuth(configuration))
        {
            return null;
        }

        var testUserId = request.Headers["X-Test-User-Id"].ToString();
        if (!string.IsNullOrWhiteSpace(testUserId))
        {
            return testUserId;
        }

        var externalUserId = request.Headers["X-External-User-Id"].ToString();
        return string.IsNullOrWhiteSpace(externalUserId) ? null : externalUserId;
    }

    private static bool AllowHeaderAuth(IConfiguration configuration) =>
        string.Equals(configuration["ALLOW_HEADER_AUTH"], "true", StringComparison.OrdinalIgnoreCase);

    private static string? ResolveBearerToken(HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..].Trim();
        }

        return null;
    }

    private static async Task<ClaimsPrincipal?> ValidateBearerTokenAsync(
        string token,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var authority = ResolveAuthority(configuration);
        var audiences = ResolveAudiences(configuration);
        if (string.IsNullOrWhiteSpace(authority) || audiences.Count == 0)
        {
            return null;
        }

        var metadataAddress = $"{authority.TrimEnd('/')}/.well-known/openid-configuration";
        var manager = ConfigurationManagers.GetOrAdd(
            metadataAddress,
            key => new ConfigurationManager<OpenIdConnectConfiguration>(
                key,
                new OpenIdConnectConfigurationRetriever()));

        OpenIdConnectConfiguration openIdConfiguration;
        try
        {
            openIdConfiguration = await manager.GetConfigurationAsync(cancellationToken);
        }
        catch
        {
            return null;
        }

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = openIdConfiguration.SigningKeys,
            ValidateIssuer = true,
            ValidIssuers = ResolveIssuers(authority),
            ValidateAudience = true,
            ValidAudiences = audiences,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
        };

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(
                token,
                tokenValidationParameters,
                out _);

            return HasRequiredScopeOrRole(principal, configuration) ? principal : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveAuthority(IConfiguration configuration) =>
        configuration["ENTRA_AUTHORITY"] ??
        configuration["NEXT_PUBLIC_ENTRA_AUTHORITY"] ??
        configuration["AZURE_EXTERNAL_ID_AUTHORITY"];

    private static List<string> ResolveAudiences(IConfiguration configuration)
    {
        var audiences = new List<string>();
        AddIfPresent(audiences, configuration["ENTRA_API_AUDIENCE"]);
        AddIfPresent(audiences, configuration["NEXT_PUBLIC_ENTRA_API_AUDIENCE"]);
        AddIfPresent(audiences, configuration["AZURE_EXTERNAL_ID_API_AUDIENCE"]);
        AddIfPresent(audiences, configuration["NEXT_PUBLIC_ENTRA_CLIENT_ID"]);

        var scope = ResolveRequiredScope(configuration);
        var scopeAudience = ResolveAudienceFromScope(scope);
        AddIfPresent(audiences, scopeAudience);

        return audiences
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> ResolveIssuers(string authority)
    {
        var normalized = authority.TrimEnd('/');
        yield return normalized;
        yield return $"{normalized}/";
    }

    private static string? ResolveRequiredScope(IConfiguration configuration) =>
        configuration["ENTRA_API_SCOPE"] ??
        configuration["NEXT_PUBLIC_ENTRA_API_SCOPE"] ??
        configuration["AZURE_EXTERNAL_ID_API_SCOPE"];

    private static string? ResolveAudienceFromScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        var slashIndex = scope.LastIndexOf('/');
        return slashIndex > 0 ? scope[..slashIndex] : null;
    }

    private static bool HasRequiredScopeOrRole(ClaimsPrincipal principal, IConfiguration configuration)
    {
        var requiredScope = ResolveRequiredScope(configuration);
        if (string.IsNullOrWhiteSpace(requiredScope))
        {
            return true;
        }

        var allowedScopeValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            requiredScope,
        };
        var slashIndex = requiredScope.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < requiredScope.Length - 1)
        {
            allowedScopeValues.Add(requiredScope[(slashIndex + 1)..]);
        }

        return principal.Claims.Any(claim =>
            (claim.Type is "scp" or "roles" or ClaimTypes.Role) &&
            claim.Value
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(allowedScopeValues.Contains));
    }

    private static string? ResolveUserIdFromClaims(ClaimsPrincipal principal) =>
        principal.FindFirstValue("sub") ??
        principal.FindFirstValue("oid") ??
        principal.FindFirstValue(ClaimTypes.NameIdentifier);

    private static string? ResolveEmail(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Email) ??
        principal.FindFirstValue("email") ??
        principal.FindFirstValue("emails") ??
        principal.FindFirstValue("preferred_username");

    private static void AddIfPresent(ICollection<string> values, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add(value);
        }
    }
}

public sealed record FunctionAuthUser(string ExternalAuthUserId, string? Email);
