using HeroPassport.App.Mcp;
using ModelContextProtocol.Server;
using System.Text.Json;
using Xunit;

namespace HeroPassport.App.Tests;

public sealed class McpSurfaceTests
{
    [Fact]
    public void ToolCatalogMatchesHpMcp2OrderAndAnnotations()
    {
        var endpoint = HeroPassportMcpEndpoint.CreateForMetadataTests();
        IReadOnlyList<McpServerTool> tools = HeroPassportMcpToolCatalog.Create(endpoint);

        string[] expectedNames =
        [
            "hero.bootstrap",
            "hero.configure",
            "hero.get_context",
            "hero.create",
            "hero.list",
            "hero.activate",
            "hero.archive",
            "hero.restore",
            "hero.start_quest",
            "hero.finish_quest",
            "hero.get_card",
        ];

        Assert.Equal(expectedNames, tools.Select(static tool => tool.ProtocolTool.Name));
        Assert.All(tools, static tool =>
        {
            Assert.False(tool.ProtocolTool.Annotations?.OpenWorldHint ?? true);
            Assert.False(tool.ProtocolTool.Annotations?.DestructiveHint ?? true);
            Assert.True(tool.ProtocolTool.Annotations?.IdempotentHint ?? false);
            Assert.NotNull(tool.ProtocolTool.OutputSchema);
            AssertClosedObjects(tool.ProtocolTool.InputSchema);
            AssertClosedObjects(tool.ProtocolTool.OutputSchema.Value);
        });

        var readOnlyNames = tools
            .Where(static tool => tool.ProtocolTool.Annotations?.ReadOnlyHint is true)
            .Select(static tool => tool.ProtocolTool.Name)
            .ToArray();

        Assert.Equal(["hero.get_context", "hero.list", "hero.get_card"], readOnlyNames);
    }

    private static void AssertClosedObjects(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (schema.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "object")
        {
            Assert.True(schema.TryGetProperty("additionalProperties", out var additionalProperties));
            Assert.Equal(JsonValueKind.False, additionalProperties.ValueKind);
        }

        foreach (var property in schema.EnumerateObject())
        {
            if (property.NameEquals("properties") && property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var nested in property.Value.EnumerateObject())
                {
                    AssertClosedObjects(nested.Value);
                }
            }
            else if (property.NameEquals("items"))
            {
                AssertClosedObjects(property.Value);
            }
        }
    }
}
