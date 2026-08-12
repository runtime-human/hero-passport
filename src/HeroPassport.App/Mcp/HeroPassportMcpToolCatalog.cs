using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace HeroPassport.App.Mcp;

public static class HeroPassportMcpToolCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    private static readonly AIJsonSchemaCreateOptions SchemaCreateOptions = new()
    {
        TransformSchemaNode = static (_, node) => CloseObjectSchema(node),
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
            SchemaCreateOptions = SchemaCreateOptions,
            UseStructuredContent = true,
            ReadOnly = readOnly,
            Destructive = false,
            Idempotent = true,
            OpenWorld = false,
        });
    }

    private static JsonNode CloseObjectSchema(JsonNode node)
    {
        if (node is JsonObject schema && IsObjectType(schema["type"]))
        {
            schema["additionalProperties"] = false;
        }

        return node;
    }

    private static bool IsObjectType(JsonNode? typeNode)
    {
        if (typeNode is JsonValue value && value.TryGetValue<string>(out var type))
        {
            return string.Equals(type, "object", StringComparison.Ordinal);
        }

        if (typeNode is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is JsonValue itemValue &&
                    itemValue.TryGetValue<string>(out var itemType) &&
                    string.Equals(itemType, "object", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
