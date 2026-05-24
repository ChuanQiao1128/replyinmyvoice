using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Functions.Auth;

namespace ReplyInMyVoice.Tests;

public sealed class FunctionAuthResolverTests
{
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

    private static HttpRequest CreateRequest(ClaimsPrincipal? user = null)
    {
        var context = new DefaultHttpContext();
        if (user is not null)
        {
            context.User = user;
        }

        return context.Request;
    }

    private static IConfiguration BuildConfiguration(
        Dictionary<string, string?>? values = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();
}
