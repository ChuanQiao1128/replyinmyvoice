using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using ReplyInMyVoice.Functions.Http;

namespace ReplyInMyVoice.Tests;

public sealed class FunctionHttpResultsTests
{
    [Fact]
    public void Problem_WithErrorCode_EmitsFrozenCodedEnvelope()
    {
        var result = FunctionHttpResults.Problem(
                "Invalid request",
                "Draft must be at least 10 characters.",
                400,
                "invalid_request")
            .Should()
            .BeOfType<ObjectResult>()
            .Subject;

        result.StatusCode.Should().Be(400);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        json.RootElement.EnumerateObject().Select(x => x.Name).Should().Equal("error");
        var error = json.RootElement.GetProperty("error");
        error.GetProperty("code").GetString().Should().Be("invalid_request");
        error.GetProperty("message").GetString().Should().Be("Draft must be at least 10 characters.");
        json.RootElement.TryGetProperty("title", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("status", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("detail", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(400, "invalid_request")]
    [InlineData(401, "unauthorized")]
    [InlineData(402, "payment_required")]
    [InlineData(404, "not_found")]
    [InlineData(409, "conflict")]
    [InlineData(500, "internal_error")]
    public void Problem_WithoutErrorCode_EmitsProblemDetailsWithAdditiveDefaultCode(
        int statusCode,
        string expectedCode)
    {
        var result = FunctionHttpResults.Problem("Problem title", "Problem detail", statusCode)
            .Should()
            .BeOfType<ObjectResult>()
            .Subject;

        result.StatusCode.Should().Be(statusCode);
        var problem = result.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Problem title");
        problem.Detail.Should().Be("Problem detail");
        problem.Status.Should().Be(statusCode);
        problem.Extensions["code"].Should().Be(expectedCode);
    }
}
