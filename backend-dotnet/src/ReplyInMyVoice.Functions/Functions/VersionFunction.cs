using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class VersionFunction(IConfiguration configuration)
{
    private const string Unknown = "unknown";

    [Function("Version")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "version")]
        HttpRequest request)
    {
        var commitSha = ReadMetadata("Build:CommitSha", "Build_CommitSha", "BUILD_COMMIT_SHA");
        var buildTimestamp = ReadMetadata("Build:Timestamp", "Build_Timestamp", "BUILD_TIMESTAMP");

        return new OkObjectResult(new VersionResponse(commitSha, buildTimestamp));
    }

    private string ReadMetadata(string primaryKey, string aliasKey, string environmentVariableName)
    {
        var primaryValue = configuration[primaryKey];
        if (!string.IsNullOrWhiteSpace(primaryValue))
        {
            return primaryValue;
        }

        var aliasValue = configuration[aliasKey];
        if (!string.IsNullOrWhiteSpace(aliasValue))
        {
            return aliasValue;
        }

        var environmentValue = Environment.GetEnvironmentVariable(environmentVariableName);
        return string.IsNullOrWhiteSpace(environmentValue) ? Unknown : environmentValue;
    }
}

public sealed record VersionResponse(
    [property: JsonPropertyName("commitSha")] string CommitSha,
    [property: JsonPropertyName("buildTimestamp")] string BuildTimestamp);
