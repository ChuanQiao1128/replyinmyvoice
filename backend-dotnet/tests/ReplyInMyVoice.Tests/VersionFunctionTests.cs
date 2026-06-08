using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Functions.Functions;

namespace ReplyInMyVoice.Tests;

public sealed class VersionFunctionTests
{
    [Fact]
    public void Run_returns_configured_build_metadata()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Build:CommitSha"] = "abc1234",
                ["Build:Timestamp"] = "2026-06-08T01:02:03Z",
            })
            .Build();
        var function = new VersionFunction(configuration);

        var result = function.Run(new DefaultHttpContext().Request);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        var body = ok.Value.Should().BeOfType<VersionResponse>().Subject;
        body.CommitSha.Should().Be("abc1234");
        body.BuildTimestamp.Should().Be("2026-06-08T01:02:03Z");
    }

    [Fact]
    public void Run_returns_unknown_when_metadata_is_not_configured()
    {
        var originalCommitSha = Environment.GetEnvironmentVariable("BUILD_COMMIT_SHA");
        var originalTimestamp = Environment.GetEnvironmentVariable("BUILD_TIMESTAMP");
        Environment.SetEnvironmentVariable("BUILD_COMMIT_SHA", null);
        Environment.SetEnvironmentVariable("BUILD_TIMESTAMP", null);

        try
        {
            var configuration = new ConfigurationBuilder().Build();
            var function = new VersionFunction(configuration);

            var result = function.Run(new DefaultHttpContext().Request);

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            ok.StatusCode.Should().Be(StatusCodes.Status200OK);
            var body = ok.Value.Should().BeOfType<VersionResponse>().Subject;
            body.CommitSha.Should().Be("unknown");
            body.BuildTimestamp.Should().Be("unknown");
        }
        finally
        {
            Environment.SetEnvironmentVariable("BUILD_COMMIT_SHA", originalCommitSha);
            Environment.SetEnvironmentVariable("BUILD_TIMESTAMP", originalTimestamp);
        }
    }

    [Fact]
    public void Run_reads_build_metadata_from_generated_json_loader()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rimv-version-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(
                Path.Combine(directory, "version.generated.json"),
                """
                {
                  "Build": {
                    "CommitSha": "deadbeefcafe",
                    "Timestamp": "2026-06-08T00:00:00Z"
                  }
                }
                """);
            var configuration = new ConfigurationBuilder()
                .SetBasePath(directory)
                .AddJsonFile("version.generated.json", optional: false, reloadOnChange: false)
                .Build();
            var function = new VersionFunction(configuration);

            var result = function.Run(new DefaultHttpContext().Request);

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            ok.StatusCode.Should().Be(StatusCodes.Status200OK);
            var body = ok.Value.Should().BeOfType<VersionResponse>().Subject;
            body.CommitSha.Should().Be("deadbeefcafe");
            body.BuildTimestamp.Should().Be("2026-06-08T00:00:00Z");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
