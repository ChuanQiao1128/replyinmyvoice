using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace ReplyInMyVoice.Tests;

public sealed class OpenApiV1ContractTests
{
    private const string InvalidKey = "invalid_key";
    private const string InvalidRequest = "invalid_request";
    private const string InputTooLong = "input_too_long";
    private const string IdempotencyConflict = "idempotency_conflict";
    private const string QuotaExhausted = "quota_exhausted";
    private const string ApiRequiresPaidPlan = "api_requires_paid_plan";
    private const string RewriteFailed = "rewrite_failed";
    private const string RateLimited = "rate_limited";
    private const string RateLimitUnavailable = "rate_limit_unavailable";
    private const string NotFound = "not_found";
    private const string PayloadTooLarge = "payload_too_large";
    private const string EngineUnavailable = "engine_unavailable";

    private static readonly V1Endpoint SubmitRewrite = new("POST", "/api/v1/rewrite", "post");
    private static readonly V1Endpoint GetRewriteResult = new("GET", "/api/v1/rewrite/{id}", "get");
    private static readonly V1Endpoint GetUsage = new("GET", "/api/v1/usage", "get");

    private static readonly string[] ProducedErrorCodes =
    [
        InvalidKey,
        InvalidRequest,
        InputTooLong,
        IdempotencyConflict,
        QuotaExhausted,
        ApiRequiresPaidPlan,
        RewriteFailed,
        RateLimited,
        RateLimitUnavailable,
        NotFound,
        PayloadTooLarge,
        EngineUnavailable,
    ];

    private static readonly int[] ProducedStatusCodes =
    [
        StatusCodes.Status200OK,
        StatusCodes.Status202Accepted,
        StatusCodes.Status400BadRequest,
        StatusCodes.Status401Unauthorized,
        StatusCodes.Status402PaymentRequired,
        StatusCodes.Status404NotFound,
        StatusCodes.Status409Conflict,
        StatusCodes.Status413PayloadTooLarge,
        StatusCodes.Status429TooManyRequests,
        StatusCodes.Status500InternalServerError,
        StatusCodes.Status503ServiceUnavailable,
    ];

    private static readonly ProducedContract[] ProducedContracts =
    [
        Status(SubmitRewrite, StatusCodes.Status202Accepted),
        Error(SubmitRewrite, StatusCodes.Status400BadRequest, InvalidRequest),
        Error(SubmitRewrite, StatusCodes.Status400BadRequest, InputTooLong),
        Error(SubmitRewrite, StatusCodes.Status401Unauthorized, InvalidKey),
        Error(SubmitRewrite, StatusCodes.Status402PaymentRequired, ApiRequiresPaidPlan),
        Error(SubmitRewrite, StatusCodes.Status402PaymentRequired, QuotaExhausted),
        Error(SubmitRewrite, StatusCodes.Status409Conflict, IdempotencyConflict),
        Error(SubmitRewrite, StatusCodes.Status413PayloadTooLarge, PayloadTooLarge),
        Error(SubmitRewrite, StatusCodes.Status429TooManyRequests, RateLimited),
        Error(SubmitRewrite, StatusCodes.Status500InternalServerError, RewriteFailed),
        Error(SubmitRewrite, StatusCodes.Status503ServiceUnavailable, RateLimitUnavailable),

        Status(GetRewriteResult, StatusCodes.Status200OK),
        Error(GetRewriteResult, StatusCodes.Status200OK, EngineUnavailable),
        Error(GetRewriteResult, StatusCodes.Status401Unauthorized, InvalidKey),
        Error(GetRewriteResult, StatusCodes.Status404NotFound, NotFound),
        Error(GetRewriteResult, StatusCodes.Status413PayloadTooLarge, PayloadTooLarge),
        Error(GetRewriteResult, StatusCodes.Status429TooManyRequests, RateLimited),
        Error(GetRewriteResult, StatusCodes.Status503ServiceUnavailable, RateLimitUnavailable),

        Status(GetUsage, StatusCodes.Status200OK),
        Error(GetUsage, StatusCodes.Status401Unauthorized, InvalidKey),
        Error(GetUsage, StatusCodes.Status413PayloadTooLarge, PayloadTooLarge),
        Error(GetUsage, StatusCodes.Status429TooManyRequests, RateLimited),
        Error(GetUsage, StatusCodes.Status503ServiceUnavailable, RateLimitUnavailable),
    ];

    [Fact]
    public void OpenApi_v1_documents_all_produced_status_and_error_tuples()
    {
        ProducedContracts
            .Where(contract => contract.ErrorCode is not null)
            .Select(contract => contract.ErrorCode)
            .Distinct()
            .Should()
            .BeEquivalentTo(ProducedErrorCodes);

        ProducedContracts
            .Select(contract => contract.StatusCode)
            .Distinct()
            .Should()
            .BeEquivalentTo(ProducedStatusCodes);

        using var spec = LoadOpenApiSpec();
        var documentedContracts = new[]
        {
            ExtractDocumentedContracts(spec.RootElement, SubmitRewrite),
            ExtractDocumentedContracts(spec.RootElement, GetRewriteResult),
            ExtractDocumentedContracts(spec.RootElement, GetUsage),
        };

        var missing = ProducedContracts
            .SelectMany(contract => MissingContractMessages(contract, documentedContracts.Single(x => x.Endpoint == contract.Endpoint)))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();

        missing.Should().BeEmpty(
            "Missing from spec: {0}",
            string.Join("; ", missing));
    }

    [Fact]
    public void OpenApi_v1_contract_pin_matches_literal_source_error_surface()
    {
        var sourceContracts = ExtractLiteralSourceErrorContracts()
            .OrderBy(contract => contract.Endpoint.Path, StringComparer.Ordinal)
            .ThenBy(contract => contract.Endpoint.Method, StringComparer.Ordinal)
            .ThenBy(contract => contract.StatusCode)
            .ThenBy(contract => contract.ErrorCode, StringComparer.Ordinal)
            .ToArray();

        var pinnedErrorContracts = ProducedContracts
            .Where(contract => contract.ErrorCode is not null)
            .OrderBy(contract => contract.Endpoint.Path, StringComparer.Ordinal)
            .ThenBy(contract => contract.Endpoint.Method, StringComparer.Ordinal)
            .ThenBy(contract => contract.StatusCode)
            .ThenBy(contract => contract.ErrorCode, StringComparer.Ordinal)
            .ToArray();

        sourceContracts.Should().BeEquivalentTo(
            pinnedErrorContracts,
            options => options.WithStrictOrdering(),
            "the pinned OpenAPI guard should track the v1 API error tuples emitted by the ASP.NET Core and Functions entry points");
    }

    private static JsonDocument LoadOpenApiSpec()
    {
        var openApiJson = File.ReadAllText(Path.Combine(FindRepoRoot(), "public", "openapi.json"));
        return JsonDocument.Parse(openApiJson);
    }

    private static DocumentedEndpointContracts ExtractDocumentedContracts(JsonElement root, V1Endpoint endpoint)
    {
        var responses = root
            .GetProperty("paths")
            .GetProperty(endpoint.Path)
            .GetProperty(endpoint.OpenApiMethod)
            .GetProperty("responses");

        var statuses = new HashSet<int>();
        var errorCodesByStatus = new Dictionary<int, HashSet<string>>();

        foreach (var response in responses.EnumerateObject())
        {
            var statusCode = int.Parse(response.Name);
            statuses.Add(statusCode);

            var errorCodes = new HashSet<string>(StringComparer.Ordinal);
            CollectDocumentedErrorCodes(root, response.Value, errorCodes);
            errorCodesByStatus[statusCode] = errorCodes;
        }

        return new DocumentedEndpointContracts(endpoint, statuses, errorCodesByStatus);
    }

    private static void CollectDocumentedErrorCodes(
        JsonElement root,
        JsonElement element,
        ISet<string> errorCodes)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("$ref", out var refElement) &&
                    refElement.ValueKind == JsonValueKind.String)
                {
                    CollectDocumentedErrorCodes(root, ResolveLocalReference(root, refElement.GetString()!), errorCodes);
                }

                if (element.TryGetProperty("code", out var codeElement) &&
                    codeElement.ValueKind == JsonValueKind.String)
                {
                    errorCodes.Add(codeElement.GetString()!);
                }

                foreach (var property in element.EnumerateObject())
                {
                    CollectDocumentedErrorCodes(root, property.Value, errorCodes);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectDocumentedErrorCodes(root, item, errorCodes);
                }

                break;
        }
    }

    private static JsonElement ResolveLocalReference(JsonElement root, string reference)
    {
        reference.StartsWith("#/", StringComparison.Ordinal)
            .Should()
            .BeTrue("OpenAPI contract tests only resolve local refs");

        var current = root;
        foreach (var segment in reference[2..].Split('/'))
        {
            current = current.GetProperty(segment);
        }

        return current;
    }

    private static IEnumerable<string> MissingContractMessages(
        ProducedContract produced,
        DocumentedEndpointContracts documented)
    {
        if (!documented.StatusCodes.Contains(produced.StatusCode))
        {
            yield return produced.ErrorCode is null
                ? $"status={produced.StatusCode} on {produced.Endpoint.DisplayName}"
                : $"error_code={produced.ErrorCode} with status={produced.StatusCode} on {produced.Endpoint.DisplayName}";
            yield break;
        }

        if (produced.ErrorCode is not null &&
            (!documented.ErrorCodesByStatus.TryGetValue(produced.StatusCode, out var documentedCodes) ||
             !documentedCodes.Contains(produced.ErrorCode)))
        {
            yield return $"error_code={produced.ErrorCode} with status={produced.StatusCode} on {produced.Endpoint.DisplayName}";
        }
    }

    private static IReadOnlyCollection<ProducedContract> ExtractLiteralSourceErrorContracts()
    {
        var root = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(
            root,
            "backend-dotnet",
            "src",
            "ReplyInMyVoice.Api",
            "Program.cs"));
        var functions = File.ReadAllText(Path.Combine(
            root,
            "backend-dotnet",
            "src",
            "ReplyInMyVoice.Functions",
            "Functions",
            "V1RewriteHttpFunctions.cs"));
        var functionHttpResults = File.ReadAllText(Path.Combine(
            root,
            "backend-dotnet",
            "src",
            "ReplyInMyVoice.Functions",
            "Http",
            "FunctionHttpResults.cs"));

        var contracts = new HashSet<ProducedContract>();

        AddAspNetCoreEndpointContracts(
            contracts,
            SubmitRewrite,
            SliceBetween(apiProgram, "app.MapPost(\"/api/v1/rewrite\"", "app.MapGet(\"/api/v1/rewrite/{id:guid}\""));
        AddAspNetCoreEndpointContracts(
            contracts,
            GetRewriteResult,
            SliceBetween(apiProgram, "app.MapGet(\"/api/v1/rewrite/{id:guid}\"", "app.MapGet(\"/api/v1/usage\""));
        AddAspNetCoreEndpointContracts(
            contracts,
            GetUsage,
            SliceBetween(apiProgram, "app.MapGet(\"/api/v1/usage\"", "app.MapGet(\"/api/rewrite-attempts/"));

        AddFunctionsSubmitContracts(
            contracts,
            SliceBetween(functions, "[Function(\"V1SubmitRewrite\")]", "[Function(\"V1GetRewriteResult\")]"));
        AddFunctionsStatusContracts(
            contracts,
            GetRewriteResult,
            SliceBetween(functions, "[Function(\"V1GetRewriteResult\")]", "[Function(\"V1GetUsage\")]"));
        AddFunctionsStatusContracts(
            contracts,
            GetUsage,
            SliceBetween(functions, "[Function(\"V1GetUsage\")]", "private static IActionResult MapRewriteResult"));

        if (apiProgram.Contains("\"engine_unavailable\"", StringComparison.Ordinal) &&
            functions.Contains("\"engine_unavailable\"", StringComparison.Ordinal))
        {
            contracts.Add(Error(GetRewriteResult, StatusCodes.Status200OK, EngineUnavailable));
        }

        if (functionHttpResults.Contains("\"payload_too_large\"", StringComparison.Ordinal) &&
            functionHttpResults.Contains("StatusCodes.Status413PayloadTooLarge", StringComparison.Ordinal))
        {
            contracts.Add(Error(SubmitRewrite, StatusCodes.Status413PayloadTooLarge, PayloadTooLarge));
            contracts.Add(Error(GetRewriteResult, StatusCodes.Status413PayloadTooLarge, PayloadTooLarge));
            contracts.Add(Error(GetUsage, StatusCodes.Status413PayloadTooLarge, PayloadTooLarge));
        }

        return contracts;
    }

    private static void AddAspNetCoreEndpointContracts(
        ISet<ProducedContract> contracts,
        V1Endpoint endpoint,
        string source)
    {
        AddMatches(
            contracts,
            endpoint,
            source,
            @"V1Error\(\s*""(?<code>[^""]+)""[\s\S]*?StatusCodes\.Status(?<status>\d+)");

        if (endpoint == SubmitRewrite && source.Contains("result.ErrorCode ?? \"quota_exhausted\"", StringComparison.Ordinal))
        {
            contracts.Add(Error(SubmitRewrite, StatusCodes.Status402PaymentRequired, QuotaExhausted));
        }
    }

    private static void AddFunctionsSubmitContracts(
        ISet<ProducedContract> contracts,
        string source)
    {
        AddMatches(
            contracts,
            SubmitRewrite,
            source,
            @"Error\(\s*""(?<code>[^""]+)""[\s\S]*?StatusCodes\.Status(?<status>\d+)");

        if (source.Contains("result.ErrorCode ?? \"quota_exhausted\"", StringComparison.Ordinal))
        {
            contracts.Add(Error(SubmitRewrite, StatusCodes.Status402PaymentRequired, QuotaExhausted));
        }
    }

    private static void AddFunctionsStatusContracts(
        ISet<ProducedContract> contracts,
        V1Endpoint endpoint,
        string source)
    {
        AddMatches(
            contracts,
            endpoint,
            source,
            @"FunctionHttpResults\.Problem\([\s\S]*?StatusCodes\.Status(?<status>\d+)[\s\S]*?""(?<code>[^""]+)""\s*\)");
    }

    private static void AddMatches(
        ISet<ProducedContract> contracts,
        V1Endpoint endpoint,
        string source,
        string pattern)
    {
        foreach (Match match in Regex.Matches(source, pattern, RegexOptions.CultureInvariant))
        {
            contracts.Add(Error(
                endpoint,
                int.Parse(match.Groups["status"].Value),
                match.Groups["code"].Value));
        }
    }

    private static string SliceBetween(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, $"source should contain {startMarker}");

        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start, $"source should contain {endMarker} after {startMarker}");

        return source[start..end];
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "public", "openapi.json")) &&
                Directory.Exists(Path.Combine(directory.FullName, "backend-dotnet", "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing public/openapi.json.");
    }

    private static ProducedContract Error(V1Endpoint endpoint, int statusCode, string errorCode) =>
        new(endpoint, statusCode, errorCode);

    private static ProducedContract Status(V1Endpoint endpoint, int statusCode) =>
        new(endpoint, statusCode, null);

    private sealed record V1Endpoint(string Method, string Path, string OpenApiMethod)
    {
        public string DisplayName => $"{Method} {Path}";
    }

    private sealed record ProducedContract(V1Endpoint Endpoint, int StatusCode, string? ErrorCode);

    private sealed record DocumentedEndpointContracts(
        V1Endpoint Endpoint,
        IReadOnlySet<int> StatusCodes,
        IReadOnlyDictionary<int, HashSet<string>> ErrorCodesByStatus);
}
