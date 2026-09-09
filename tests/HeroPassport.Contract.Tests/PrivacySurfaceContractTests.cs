using HeroPassport.App.Mcp;
using System.Text.Json;
using Xunit;

namespace HeroPassport.Contract.Tests;

public sealed class PrivacySurfaceContractTests
{
    private static readonly string[] ForbiddenWireFragments =
    [
        "sourcecode",
        "filecontent",
        "diff",
        "patch",
        "rawlog",
        "prompt",
        "chattranscript",
        "secret",
        "apikey",
        "environment",
        "path",
        "remoteurl",
        "gitremote",
    ];

    [Fact]
    public void McpSchemasDoNotExposeForbiddenRawEvidenceSecretsOrLocalIdentityMaterial()
    {
        var propertyNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var tool in HpMcpToolCatalog.ProtocolTools)
        {
            CollectSchemaPropertyNames(tool.InputSchema, propertyNames);
            if (tool.OutputSchema is JsonElement outputSchema)
            {
                CollectSchemaPropertyNames(outputSchema, propertyNames);
            }
        }

        Assert.Contains("title", propertyNames);
        Assert.Contains("goal", propertyNames);
        Assert.Contains("summary", propertyNames);
        Assert.Contains("displayName", propertyNames);

        var violations = propertyNames
            .Where(IsForbiddenWireProperty)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    private static bool IsForbiddenWireProperty(string propertyName)
    {
        var normalized = new string(propertyName
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

        return ForbiddenWireFragments.Any(normalized.Contains);
    }

    private static void CollectSchemaPropertyNames(JsonElement element, ISet<string> propertyNames)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in properties.EnumerateObject())
                {
                    propertyNames.Add(property.Name);
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                CollectSchemaPropertyNames(property.Value, propertyNames);
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectSchemaPropertyNames(item, propertyNames);
            }
        }
    }
}
