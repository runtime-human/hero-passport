using ModelContextProtocol.Server;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HeroPassport.App.Mcp;

public static class HeroPassportMcpToolCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static IReadOnlyList<McpServerTool> Create(HeroPassportMcpEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return
        [
            Create(endpoint, nameof(HeroPassportMcpEndpoint.BootstrapAsync), "hero.bootstrap", readOnly: false),
            Create(endpoint, nameof(HeroPassportMcpEndpoint.ConfigureAsync), "hero.configure", readOnly: false),
            Create(endpoint, nameof(HeroPassportMcpEndpoint.GetContextAsync), "hero.get_context", readOnly: true),
            Create(endpoint, nameof(HeroPassportMcpEndpoint.CreateHeroAsync), "hero.create", readOnly: false),
            Create(endpoint, nameof(HeroPassportMcpEndpoint.ListHeroesAsync), "hero.list", readOnly: true),
            Create(endpoint, nameof(HeroPassportMcpEndpoint.ActivateHeroAsync), "hero.activate", readOnly: false),
            Create(endpoint, nameof(HeroPassportMcpEndpoint.ArchiveHeroAsync), "hero.archive", readOnly: false),
            Create(endpoint, nameof(HeroPassportMcpEndpoint.RestoreHeroAsync), "hero.restore", readOnly: false),
            Create(endpoint, nameof(HeroPassportMcpEndpoint.StartQuestAsync), "hero.start_quest", readOnly: false),
            Create(endpoint, nameof(HeroPassportMcpEndpoint.FinishQuestAsync), "hero.finish_quest", readOnly: false),
            Create(endpoint, nameof(HeroPassportMcpEndpoint.GetCardAsync), "hero.get_card", readOnly: true),
        ];
    }

    private static McpServerTool Create(HeroPassportMcpEndpoint endpoint, string methodName, string toolName, bool readOnly)
    {
        var method = typeof(HeroPassportMcpEndpoint).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"MCP endpoint method '{methodName}' was not found.");

        return McpServerTool.Create(method, endpoint, new McpServerToolCreateOptions
        {
            Name = toolName,
            Title = toolName,
            SerializerOptions = SerializerOptions,
            UseStructuredContent = true,
            ReadOnly = readOnly,
            Destructive = false,
            Idempotent = true,
            OpenWorld = false,
        });
    }
}
