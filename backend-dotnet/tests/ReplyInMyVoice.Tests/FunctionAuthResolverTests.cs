using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Functions.Http;

namespace ReplyInMyVoice.Tests;

public sealed class FunctionAuthResolverTests
{
    private const string TestJwtIssuer =
        "https://replyinmyvoicecustomers.ciamlogin.com/614ea821-6ef3-43e2-8613-d4b13fae115d/v2.0";
    private const string TestJwtAudience = "api://1ecb5f62-22b8-4e5a-8139-b2c4f15c3f32";
    private const string TestJwtScope = $"{TestJwtAudience}/access_as_user";

    [Fact]
    public async Task ResolveUserAsync_reads_email_from_entra_emails_claim()
    {
        var request = CreateRequest(
            new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "entra-subject-1"),
                new Claim("emails", "teacher@example.com"),
            ], "Bearer")));
        var configuration = BuildConfiguration();

        var user = await FunctionAuthResolver.ResolveUserAsync(request, configuration);

        user.Should().NotBeNull();
        user!.ExternalAuthUserId.Should().Be("entra-subject-1");
        user.Email.Should().Be("teacher@example.com");
    }

    [Fact]
    public async Task ResolveUserAsync_falls_back_to_preferred_username_email()
    {
        var request = CreateRequest(
            new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("oid", "entra-object-1"),
                new Claim("preferred_username", "owner@example.com"),
            ], "Bearer")));
        var configuration = BuildConfiguration();

        var user = await FunctionAuthResolver.ResolveUserAsync(request, configuration);

        user.Should().NotBeNull();
        user!.ExternalAuthUserId.Should().Be("entra-object-1");
        user.Email.Should().Be("owner@example.com");
    }

    [Fact]
    public async Task ResolveUserAsync_prefers_oid_over_sub_for_stable_cross_token_key()
    {
        // Regression guard for the Entra pairwise-`sub` bug: the ID token (aud = frontend client)
        // and the access token (aud = api://<API client id>) carry DIFFERENT `sub` values for the
        // same human, but the SAME `oid`. The user key must be `oid` so the two tokens resolve to
        // one AppUser. With both claims present, `oid` must win over `sub`.
        var request = CreateRequest(
            new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "access-token-pairwise-sub"),
                new Claim("oid", "stable-object-id"),
            ], "Bearer")));

        var user = await FunctionAuthResolver.ResolveUserAsync(request, BuildConfiguration());

        user.Should().NotBeNull();
        user!.ExternalAuthUserId.Should().Be("stable-object-id");
    }

    [Fact]
    public async Task ResolveUserAsync_reads_oid_from_inbound_mapped_objectidentifier_claim()
    {
        // JwtSecurityTokenHandler claim mapping renames `oid` to this long URI. The resolver must
        // still find the object id (and still prefer it over `sub`) whether or not mapping is on.
        var request = CreateRequest(
            new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "access-token-pairwise-sub"),
                new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", "mapped-object-id"),
            ], "Bearer")));

        var user = await FunctionAuthResolver.ResolveUserAsync(request, BuildConfiguration());

        user.Should().NotBeNull();
        user!.ExternalAuthUserId.Should().Be("mapped-object-id");
    }

    [Fact]
    public void HasRequiredScopeOrRole_accepts_inbound_mapped_scope_claim()
    {
        // JwtSecurityTokenHandler maps Entra's raw `scp` claim to this URI claim type by default.
        // The live Functions auth path must treat it the same as raw `scp`.
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(
                "http://schemas.microsoft.com/identity/claims/scope",
                "access_as_user"),
        ], "Bearer"));
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["NEXT_PUBLIC_ENTRA_API_SCOPE"] =
                "api://1ecb5f62-22b8-4e5a-8139-b2c4f15c3f32/access_as_user",
        });

        FunctionAuthResolver.HasRequiredScopeOrRole(principal, configuration).Should().BeTrue();
    }

    [Fact]
    public void ResolveValidIssuers_accepts_ciam_metadata_issuer_for_alias_authority()
    {
        // Entra External ID may publish endpoints under a tenant subdomain alias while the
        // discovery document's issuer uses the canonical tenant-id host. Token validation must
        // trust the metadata issuer fetched from the configured authority.
        var issuers = FunctionAuthResolver.ResolveValidIssuers(
            "https://replyinmyvoicecustomers.ciamlogin.com/614ea821-6ef3-43e2-8613-d4b13fae115d/v2.0",
            "https://614ea821-6ef3-43e2-8613-d4b13fae115d.ciamlogin.com/614ea821-6ef3-43e2-8613-d4b13fae115d/v2.0");

        issuers.Should().Contain(
            "https://614ea821-6ef3-43e2-8613-d4b13fae115d.ciamlogin.com/614ea821-6ef3-43e2-8613-d4b13fae115d/v2.0");
    }

    [Fact]
    public void ResolveAudiences_accepts_api_uri_and_bare_api_client_id_from_scope()
    {
        var audiences = FunctionAuthResolver.ResolveAudiences(BuildConfiguration(new Dictionary<string, string?>
        {
            ["NEXT_PUBLIC_ENTRA_API_SCOPE"] =
                "api://1ecb5f62-22b8-4e5a-8139-b2c4f15c3f32/access_as_user",
        }));

        audiences.Should().Contain("api://1ecb5f62-22b8-4e5a-8139-b2c4f15c3f32");
        audiences.Should().Contain("1ecb5f62-22b8-4e5a-8139-b2c4f15c3f32");
    }

    [Fact]
    public async Task ResolveUserAsync_rejects_header_identity_unless_enabled()
    {
        var request = CreateRequest();
        request.Headers["X-External-User-Id"] = "spoofed";

        var user = await FunctionAuthResolver.ResolveUserAsync(request, BuildConfiguration());

        user.Should().BeNull();
    }

    [Fact]
    public async Task ResolveUserAsync_allows_header_identity_for_local_smoke_tests()
    {
        var request = CreateRequest();
        request.Headers["X-External-User-Id"] = "local-user";
        request.Headers["X-User-Email"] = "local@example.com";

        var user = await FunctionAuthResolver.ResolveUserAsync(
            request,
            BuildConfiguration(new Dictionary<string, string?>
            {
                ["ALLOW_HEADER_AUTH"] = "true",
            }));

        user.Should().NotBeNull();
        user!.ExternalAuthUserId.Should().Be("local-user");
        user.Email.Should().Be("local@example.com");
    }

    [Theory]
    [InlineData("ASPNETCORE_ENVIRONMENT")]
    [InlineData("AZURE_FUNCTIONS_ENVIRONMENT")]
    public async Task ResolveUserAsync_HeaderAuthIgnoredInProduction(string environmentKey)
    {
        var request = CreateRequest();
        request.Headers["X-Test-User-Id"] = "test-user";
        request.Headers["X-External-User-Id"] = "external-user";
        request.Headers["X-User-Email"] = "local@example.com";

        var user = await FunctionAuthResolver.ResolveUserAsync(
            request,
            BuildConfiguration(new Dictionary<string, string?>
            {
                ["ALLOW_HEADER_AUTH"] = "true",
                [environmentKey] = "Production",
            }));

        user.Should().BeNull();
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public async Task ResolveUserAsync_allows_header_identity_outside_production(string environmentName)
    {
        var request = CreateRequest();
        request.Headers["X-External-User-Id"] = "local-user";
        request.Headers["X-User-Email"] = "local@example.com";

        var user = await FunctionAuthResolver.ResolveUserAsync(
            request,
            BuildConfiguration(new Dictionary<string, string?>
            {
                ["ALLOW_HEADER_AUTH"] = "true",
                ["AZURE_FUNCTIONS_ENVIRONMENT"] = environmentName,
            }));

        user.Should().NotBeNull();
        user!.ExternalAuthUserId.Should().Be("local-user");
        user.Email.Should().Be("local@example.com");
    }

    [Fact]
    public async Task ResolveUserResultAsync_returns_no_token_when_bearer_header_is_missing()
    {
        var result = await FunctionAuthResolver.ResolveUserResultAsync(
            CreateRequest(),
            BuildConfiguration());

        result.User.Should().BeNull();
        result.Reason.Should().Be(AuthFailureReason.NoToken);
    }

    [Fact]
    public async Task ResolveUserResultAsync_returns_invalid_when_bearer_validation_cannot_run()
    {
        var request = CreateRequest();
        request.Headers.Authorization = "Bearer malformed-token";

        var result = await FunctionAuthResolver.ResolveUserResultAsync(
            request,
            BuildConfiguration());

        result.User.Should().BeNull();
        result.Reason.Should().Be(AuthFailureReason.Invalid);
    }

    [Fact]
    public void ClassifyTokenFailure_maps_security_token_expiry_to_expired()
    {
        FunctionAuthResolver
            .ClassifyTokenFailure(new SecurityTokenExpiredException("expired"))
            .Should()
            .Be(AuthFailureReason.Expired);
    }

    [Fact]
    public async Task ResolveUserAsync_accepts_valid_signed_token()
    {
        using var keySet = JwtTestKeySet.Create();
        var token = CreateSignedJwt(
            keySet.PrivateSigningKey,
            externalAuthUserId: "entra-object-from-jwt");
        var request = CreateBearerRequest(token);
        var configurationManager = CreateStaticConfigurationManager(keySet.PublicValidationKey);

        var user = await FunctionAuthResolver.ResolveUserAsync(
            request,
            BuildJwtValidationConfiguration(),
            configurationManagerOverride: configurationManager);

        user.Should().NotBeNull();
        user!.ExternalAuthUserId.Should().Be("entra-object-from-jwt");
    }

    [Fact]
    public async Task ResolveUserAsync_rejects_token_signed_with_wrong_key()
    {
        var keyId = Guid.NewGuid().ToString("N");
        using var tokenKeySet = JwtTestKeySet.Create(keyId);
        using var publishedKeySet = JwtTestKeySet.Create(keyId);
        var token = CreateSignedJwt(
            tokenKeySet.PrivateSigningKey,
            externalAuthUserId: "entra-object-from-jwt");
        var request = CreateBearerRequest(token);
        var configurationManager = CreateStaticConfigurationManager(publishedKeySet.PublicValidationKey);

        var user = await FunctionAuthResolver.ResolveUserAsync(
            request,
            BuildJwtValidationConfiguration(),
            configurationManagerOverride: configurationManager);

        user.Should().BeNull();
    }

    [Fact]
    public async Task ResolveUserAsync_rejects_token_with_wrong_audience()
    {
        using var keySet = JwtTestKeySet.Create();
        var token = CreateSignedJwt(
            keySet.PrivateSigningKey,
            audience: "api://wrong-audience",
            externalAuthUserId: "entra-object-from-jwt");
        var request = CreateBearerRequest(token);
        var configurationManager = CreateStaticConfigurationManager(keySet.PublicValidationKey);

        var user = await FunctionAuthResolver.ResolveUserAsync(
            request,
            BuildJwtValidationConfiguration(),
            configurationManagerOverride: configurationManager);

        user.Should().BeNull();
    }

    [Fact]
    public async Task ResolveUserAsync_rejects_expired_token()
    {
        using var keySet = JwtTestKeySet.Create();
        var now = DateTime.UtcNow;
        var token = CreateSignedJwt(
            keySet.PrivateSigningKey,
            notBefore: now.AddMinutes(-20),
            expires: now.AddMinutes(-10),
            externalAuthUserId: "entra-object-from-jwt");
        var request = CreateBearerRequest(token);
        var configurationManager = CreateStaticConfigurationManager(keySet.PublicValidationKey);

        var user = await FunctionAuthResolver.ResolveUserAsync(
            request,
            BuildJwtValidationConfiguration(),
            configurationManagerOverride: configurationManager);

        user.Should().BeNull();
    }

    [Theory]
    [InlineData(true, "Bearer error=\"invalid_token\"")]
    [InlineData(false, "Bearer")]
    public async Task Unauthorized_sets_www_authenticate_header(bool invalidToken, string expectedHeader)
    {
        var context = await ExecuteResultAsync(FunctionHttpResults.Unauthorized(
            "Authentication required",
            invalidToken));

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.Headers.WWWAuthenticate.ToString().Should().Be(expectedHeader);
    }

    [Fact]
    public async Task Rewrite_unauthorized_logs_auth_subtype_without_token_or_email()
    {
        var request = CreateRequest();
        request.Headers.Authorization = "Bearer sample-token-value";
        request.Headers["X-User-Email"] = "person@example.com";
        var logger = new CapturingLogger<RewriteHttpFunctions>();
        var functions = new RewriteHttpFunctions(
            BuildConfiguration(),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            logger);

        var result = await functions.CreateRewriteAttempt(request, CancellationToken.None);

        result.Should().NotBeNull();
        logger.Messages.Should().ContainSingle(message =>
            message.Contains("auth.unauthorized subtype=Invalid", StringComparison.Ordinal));
        logger.Messages.Should().OnlyContain(message =>
            !message.Contains("sample-token-value", StringComparison.Ordinal) &&
            !message.Contains("person@example.com", StringComparison.Ordinal));
    }

    private static HttpRequest CreateRequest(ClaimsPrincipal? user = null)
    {
        var context = new DefaultHttpContext();
        if (user is not null)
        {
            context.User = user;
        }

        return context.Request;
    }

    private static HttpRequest CreateBearerRequest(string token)
    {
        var request = CreateRequest();
        request.Headers.Authorization = $"Bearer {token}";
        return request;
    }

    private static async Task<DefaultHttpContext> ExecuteResultAsync(IActionResult result)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddLogging()
                .AddMvcCore()
                .Services
                .BuildServiceProvider(),
        };

        await result.ExecuteResultAsync(new ActionContext
        {
            HttpContext = context,
        });

        return context;
    }

    private static IConfiguration BuildConfiguration(
        Dictionary<string, string?>? values = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();

    private static IConfiguration BuildJwtValidationConfiguration() =>
        BuildConfiguration(new Dictionary<string, string?>
        {
            ["ENTRA_AUTHORITY"] = TestJwtIssuer,
            ["ENTRA_API_AUDIENCE"] = TestJwtAudience,
            ["ENTRA_API_SCOPE"] = TestJwtScope,
        });

    private static IConfigurationManager<OpenIdConnectConfiguration> CreateStaticConfigurationManager(
        SecurityKey signingKey)
    {
        var configuration = new OpenIdConnectConfiguration
        {
            Issuer = TestJwtIssuer,
        };
        configuration.SigningKeys.Add(signingKey);

        return new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
    }

    private static string CreateSignedJwt(
        SecurityKey signingKey,
        string issuer = TestJwtIssuer,
        string audience = TestJwtAudience,
        string externalAuthUserId = "entra-object-from-jwt",
        DateTime? notBefore = null,
        DateTime? expires = null)
    {
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim("oid", externalAuthUserId),
                new Claim("scp", "access_as_user"),
            ]),
            Issuer = issuer,
            Audience = audience,
            NotBefore = notBefore ?? now.AddMinutes(-1),
            Expires = expires ?? now.AddMinutes(10),
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256),
        };

        return new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor);
    }

    private sealed class JwtTestKeySet : IDisposable
    {
        private readonly RSA privateRsa;
        private readonly RSA publicRsa;

        private JwtTestKeySet(RSA privateRsa, RSA publicRsa, string keyId)
        {
            this.privateRsa = privateRsa;
            this.publicRsa = publicRsa;
            PrivateSigningKey = new RsaSecurityKey(this.privateRsa)
            {
                KeyId = keyId,
            };
            PublicValidationKey = new RsaSecurityKey(this.publicRsa)
            {
                KeyId = keyId,
            };
        }

        public RsaSecurityKey PrivateSigningKey { get; }

        public RsaSecurityKey PublicValidationKey { get; }

        public static JwtTestKeySet Create(string? keyId = null)
        {
            var privateRsa = RSA.Create(2048);
            var publicRsa = RSA.Create();
            publicRsa.ImportParameters(privateRsa.ExportParameters(includePrivateParameters: false));

            return new JwtTestKeySet(privateRsa, publicRsa, keyId ?? Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            privateRsa.Dispose();
            publicRsa.Dispose();
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
