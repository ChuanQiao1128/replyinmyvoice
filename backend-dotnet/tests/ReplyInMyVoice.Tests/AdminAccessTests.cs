using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Functions.Functions;

namespace ReplyInMyVoice.Tests;

public sealed class AdminAccessTests
{
    [Fact]
    public async Task AdminPing_NonAdminForbidden()
    {
        var function = new AdminHttpFunctions(BuildConfiguration(
            "admin-owner-oid, owner@example.com"));
        var request = CreateRequest("regular-user-oid", "regular@example.com");

        var result = await function.Ping(request, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task AdminPing_AdminAllowed_when_email_matches()
    {
        var function = new AdminHttpFunctions(BuildConfiguration(
            "admin-owner-oid, Owner@Example.com"));
        var request = CreateRequest("different-oid", "owner@example.com");

        var result = await function.Ping(request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be((int)HttpStatusCode.OK);
        okResult.Value.Should().BeEquivalentTo(new { ok = true });
    }

    [Fact]
    public async Task AdminPing_AdminAllowed_when_oid_matches()
    {
        var function = new AdminHttpFunctions(BuildConfiguration(
            "ADMIN-OWNER-OID, owner@example.com"));
        var request = CreateRequest("admin-owner-oid", "different@example.com");

        var result = await function.Ping(request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be((int)HttpStatusCode.OK);
        okResult.Value.Should().BeEquivalentTo(new { ok = true });
    }

    private static HttpRequest CreateRequest(string oid, string email)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("oid", oid),
                new Claim("email", email),
            ], "Bearer")),
        };

        return context.Request;
    }

    private static IConfiguration BuildConfiguration(string adminEmails) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ADMIN_EMAILS"] = adminEmails,
            })
            .Build();
}
