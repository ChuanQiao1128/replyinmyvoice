using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using ReplyInMyVoice.Application.UseCases.Rewrite;
using ReplyInMyVoice.Functions.Http;

namespace ReplyInMyVoice.Tests;

public sealed class V1ErrorContractTests
{
    [Fact]
    public void V1_error_catalog_codes_are_documented_in_public_openapi()
    {
        using var document = LoadPublicOpenApi();
        var documentedResponses = CollectDocumentedErrorResponses(document.RootElement);
        var documentedCodes = documentedResponses.Select(x => x.Code).ToHashSet(StringComparer.Ordinal);

        var catalogCodes = EnumerateCatalogCodes().ToArray();
        var catalogErrorTuples = EnumerateCatalogErrors()
            .Select(x => new DocumentedErrorResponse(x.Code, x.StatusCode))
            .Distinct()
            .OrderBy(x => x.StatusCode)
            .ThenBy(x => x.Code, StringComparer.Ordinal)
            .ToArray();

        var missingCodes = catalogCodes
            .Where(x => !documentedCodes.Contains(x))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var missingTuples = catalogErrorTuples
            .Where(x => !documentedResponses.Contains(x))
            .ToArray();

        var failure = BuildMissingCatalogFailure(missingCodes, missingTuples, documentedResponses);

        failure.Should().BeNull();
    }

    [Fact]
    public void V1_endpoint_error_responses_are_documented_in_public_openapi()
    {
        using var document = LoadPublicOpenApi();
        var root = document.RootElement;
        var v1Operations = EnumerateV1Operations(root).ToArray();
        var documentedResponses = CollectDocumentedErrorResponses(root);

        var requiredForEveryV1Operation = new[]
        {
            new DocumentedErrorResponse(
                FunctionHttpResults.DefaultErrorCode(StatusCodes.Status413PayloadTooLarge),
                StatusCodes.Status413PayloadTooLarge),
            new DocumentedErrorResponse(
                V1ErrorCatalog.RewriteFailed.Code,
                V1ErrorCatalog.RewriteFailed.StatusCode),
            new DocumentedErrorResponse(
                V1ErrorCatalog.RateLimitUnavailable.Code,
                V1ErrorCatalog.RateLimitUnavailable.StatusCode),
        };

        var missingByOperation = v1Operations
            .SelectMany(operation => requiredForEveryV1Operation
                .Where(required => !OperationDocumentsError(root, operation, required))
                .Select(required => $"{operation.Method.ToUpperInvariant()} {operation.Path}: {required.StatusCode} {required.Code}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var targetedRequirements = new[]
        {
            new TargetedRequirement(
                "/api/v1/rewrite",
                "post",
                new DocumentedErrorResponse(
                    V1ErrorCatalog.ApiRequiresPaidPlan.Code,
                    V1ErrorCatalog.ApiRequiresPaidPlan.StatusCode)),
            new TargetedRequirement(
                "/api/v1/usage",
                "get",
                new DocumentedErrorResponse(
                    V1ErrorCatalog.ApiRequiresPaidPlan.Code,
                    V1ErrorCatalog.ApiRequiresPaidPlan.StatusCode)),
            new TargetedRequirement(
                "/api/v1/rewrite/{id}",
                "get",
                new DocumentedErrorResponse(
                    V1ErrorCatalog.NotFound.Code,
                    V1ErrorCatalog.NotFound.StatusCode)),
        };
        var missingTargeted = targetedRequirements
            .Where(x => !OperationDocumentsError(root, new V1Operation(x.Path, x.Method), x.Expected))
            .Select(x => $"{x.Method.ToUpperInvariant()} {x.Path}: {x.Expected.StatusCode} {x.Expected.Code}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        var failure = BuildEndpointFailure(
            missingByOperation,
            missingTargeted,
            documentedResponses);

        failure.Should().BeNull();
    }

    private static JsonDocument LoadPublicOpenApi()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "public", "openapi.json");
            if (File.Exists(candidate))
            {
                return JsonDocument.Parse(File.ReadAllText(candidate));
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Could not find public/openapi.json from the test output directory.");
    }

    private static IEnumerable<string> EnumerateCatalogCodes()
    {
        return typeof(V1ErrorCatalog)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(x => x is { IsLiteral: true, IsInitOnly: false } &&
                        x.FieldType == typeof(string) &&
                        x.Name.EndsWith("Code", StringComparison.Ordinal))
            .Select(x => (string)x.GetRawConstantValue()!)
            .Distinct(StringComparer.Ordinal);
    }

    private static IEnumerable<V1ErrorCatalog.V1Error> EnumerateCatalogErrors()
    {
        return typeof(V1ErrorCatalog)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(x => x.FieldType == typeof(V1ErrorCatalog.V1Error))
            .Select(x => (V1ErrorCatalog.V1Error)x.GetValue(null)!);
    }

    private static HashSet<DocumentedErrorResponse> CollectDocumentedErrorResponses(JsonElement root)
    {
        var documentedResponses = new HashSet<DocumentedErrorResponse>();
        if (!root.TryGetProperty("paths", out var paths))
        {
            return documentedResponses;
        }

        foreach (var path in paths.EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (!operation.Value.TryGetProperty("responses", out var responses))
                {
                    continue;
                }

                foreach (var response in responses.EnumerateObject())
                {
                    if (!int.TryParse(response.Name, out var statusCode))
                    {
                        continue;
                    }

                    foreach (var code in CollectDocumentedCodes(root, response.Value))
                    {
                        documentedResponses.Add(new DocumentedErrorResponse(code, statusCode));
                    }
                }
            }
        }

        return documentedResponses;
    }

    private static bool OperationDocumentsError(
        JsonElement root,
        V1Operation operation,
        DocumentedErrorResponse expected)
    {
        if (!root.TryGetProperty("paths", out var paths) ||
            !paths.TryGetProperty(operation.Path, out var path) ||
            !path.TryGetProperty(operation.Method, out var method) ||
            !method.TryGetProperty("responses", out var responses) ||
            !responses.TryGetProperty(expected.StatusCode.ToString(), out var response))
        {
            return false;
        }

        return CollectDocumentedCodes(root, response).Contains(expected.Code, StringComparer.Ordinal);
    }

    private static IEnumerable<V1Operation> EnumerateV1Operations(JsonElement root)
    {
        if (!root.TryGetProperty("paths", out var paths))
        {
            yield break;
        }

        foreach (var path in paths.EnumerateObject())
        {
            if (!path.Name.StartsWith("/api/v1/", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var method in path.Value.EnumerateObject())
            {
                yield return new V1Operation(path.Name, method.Name);
            }
        }
    }

    private static IEnumerable<string> CollectDocumentedCodes(JsonElement root, JsonElement element)
    {
        if (TryResolveLocalRef(root, element, out var resolved))
        {
            foreach (var code in CollectDocumentedCodes(root, resolved))
            {
                yield return code;
            }
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("code") &&
                        property.Value.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(property.Value.GetString()))
                    {
                        yield return property.Value.GetString()!;
                    }
                    else if (property.NameEquals("code") &&
                             property.Value.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var code in CollectSchemaCodeValues(property.Value))
                        {
                            yield return code;
                        }
                    }

                    foreach (var code in CollectDocumentedCodes(root, property.Value))
                    {
                        yield return code;
                    }
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var code in CollectDocumentedCodes(root, item))
                    {
                        yield return code;
                    }
                }

                break;
        }
    }

    private static IEnumerable<string> CollectSchemaCodeValues(JsonElement codeSchema)
    {
        if (codeSchema.TryGetProperty("const", out var constant) &&
            constant.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(constant.GetString()))
        {
            yield return constant.GetString()!;
        }

        if (!codeSchema.TryGetProperty("enum", out var enumValues) ||
            enumValues.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var enumValue in enumValues.EnumerateArray())
        {
            if (enumValue.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(enumValue.GetString()))
            {
                yield return enumValue.GetString()!;
            }
        }
    }

    private static bool TryResolveLocalRef(JsonElement root, JsonElement element, out JsonElement resolved)
    {
        resolved = default;
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("$ref", out var refElement) ||
            refElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var refValue = refElement.GetString();
        if (string.IsNullOrWhiteSpace(refValue) ||
            !refValue.StartsWith("#/", StringComparison.Ordinal))
        {
            return false;
        }

        resolved = root;
        foreach (var rawSegment in refValue[2..].Split('/'))
        {
            var segment = rawSegment.Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
            if (!resolved.TryGetProperty(segment, out var next))
            {
                return false;
            }

            resolved = next;
        }

        return true;
    }

    private static string? BuildMissingCatalogFailure(
        IReadOnlyCollection<string> missingCodes,
        IReadOnlyCollection<DocumentedErrorResponse> missingTuples,
        IReadOnlyCollection<DocumentedErrorResponse> documentedResponses)
    {
        if (missingCodes.Count == 0 && missingTuples.Count == 0)
        {
            return null;
        }

        return string.Join(
            Environment.NewLine,
            new[]
            {
                "public/openapi.json is missing v1 error catalog documentation.",
                $"Missing catalog codes: {FormatCodes(missingCodes)}",
                $"Missing status/code pairs: {FormatResponses(missingTuples)}",
                $"Documented status/code pairs: {FormatResponses(documentedResponses)}",
            });
    }

    private static string? BuildEndpointFailure(
        IReadOnlyCollection<string> missingByOperation,
        IReadOnlyCollection<string> missingTargeted,
        IReadOnlyCollection<DocumentedErrorResponse> documentedResponses)
    {
        if (missingByOperation.Count == 0 && missingTargeted.Count == 0)
        {
            return null;
        }

        return string.Join(
            Environment.NewLine,
            new[]
            {
                "public/openapi.json is missing required v1 endpoint error responses.",
                $"Missing all-endpoint responses: {FormatStrings(missingByOperation)}",
                $"Missing targeted responses: {FormatStrings(missingTargeted)}",
                $"Documented status/code pairs: {FormatResponses(documentedResponses)}",
            });
    }

    private static string FormatCodes(IEnumerable<string> codes) =>
        FormatStrings(codes.Order(StringComparer.Ordinal));

    private static string FormatStrings(IEnumerable<string> values)
    {
        var formatted = values.ToArray();
        return formatted.Length == 0 ? "<none>" : string.Join(", ", formatted);
    }

    private static string FormatResponses(IEnumerable<DocumentedErrorResponse> responses)
    {
        var formatted = responses
            .OrderBy(x => x.StatusCode)
            .ThenBy(x => x.Code, StringComparer.Ordinal)
            .Select(x => $"{x.StatusCode} {x.Code}")
            .ToArray();
        return formatted.Length == 0 ? "<none>" : string.Join(", ", formatted);
    }

    private sealed record DocumentedErrorResponse(string Code, int StatusCode);

    private sealed record V1Operation(string Path, string Method);

    private sealed record TargetedRequirement(string Path, string Method, DocumentedErrorResponse Expected);
}
