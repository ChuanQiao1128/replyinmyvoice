using System.Security.Claims;
using FluentAssertions;
using ReplyInMyVoice.Api;

namespace ReplyInMyVoice.Tests;

public sealed class ApiAuthEmailResolverTests
{
    [Fact]
    public void ResolveEmailFromClaims_prefers_email_before_other_email_claims()
    {
        var user = Principal(
            new Claim("email", "casey@example.com"),
            new Claim("verified_primary_email", "verified@example.com"),
            new Claim("emails", "array@example.com"),
            new Claim("preferred_username", "preferred@example.com"));

        AuthEmailResolver.ResolveEmailFromClaims(user).Should().Be("casey@example.com");
    }

    [Fact]
    public void ResolveEmailFromClaims_uses_verified_primary_email_before_emails()
    {
        var user = Principal(
            new Claim("verified_primary_email", "verified@example.com"),
            new Claim("emails", "array@example.com"),
            new Claim("preferred_username", "preferred@example.com"));

        AuthEmailResolver.ResolveEmailFromClaims(user).Should().Be("verified@example.com");
    }

    [Fact]
    public void ResolveEmailFromClaims_uses_first_non_empty_verified_primary_email_claim()
    {
        var user = Principal(
            new Claim("verified_primary_email", " "),
            new Claim("verified_primary_email", "verified@example.com"),
            new Claim("emails", "array@example.com"));

        AuthEmailResolver.ResolveEmailFromClaims(user).Should().Be("verified@example.com");
    }

    [Fact]
    public void ResolveEmailFromClaims_skips_onmicrosoft_preferred_username()
    {
        var user = Principal(
            new Claim("oid", "4be43284-c453-4307-b7e0-8475e847dd84"),
            new Claim(
                "preferred_username",
                "4be43284-c453-4307-b7e0-8475e847dd84@replyinmyvoicecustomers.onmicrosoft.com"));

        AuthEmailResolver.ResolveEmailFromClaims(user).Should().BeNull();
    }

    [Fact]
    public void ResolveEmailFromClaims_skips_preferred_username_when_local_part_matches_oid()
    {
        var user = Principal(
            new Claim("oid", "4be43284-c453-4307-b7e0-8475e847dd84"),
            new Claim("preferred_username", "4be43284-c453-4307-b7e0-8475e847dd84@example.com"));

        AuthEmailResolver.ResolveEmailFromClaims(user).Should().BeNull();
    }

    [Fact]
    public void ResolveEmailFromClaims_uses_real_preferred_username_when_earlier_claims_are_absent()
    {
        var user = Principal(
            new Claim("sub", "entra-subject-1"),
            new Claim("preferred_username", "someone@example.com"));

        AuthEmailResolver.ResolveEmailFromClaims(user).Should().Be("someone@example.com");
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Bearer"));
}
