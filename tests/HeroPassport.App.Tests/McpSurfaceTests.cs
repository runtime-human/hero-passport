using HeroPassport.App.Mcp;
using ModelContextProtocol.Server;
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
        });

        var readOnlyNames = tools
            .Where(static tool => tool.ProtocolTool.Annotations?.ReadOnlyHint is true)
            .Select(static tool => tool.ProtocolTool.Name)
            .ToArray();

        Assert.Equal(["hero.get_context", "hero.list", "hero.get_card"], readOnlyNames);
    }
}
