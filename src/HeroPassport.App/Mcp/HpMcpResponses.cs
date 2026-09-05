using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace HeroPassport.App.Mcp;

public static class HpMcpResponses
{
    public static CallToolResult Success(JsonElement structuredContent)
    {
        var clone = structuredContent.Clone();
        return new CallToolResult
        {
            StructuredContent = clone,
            Content = [new TextContentBlock { Text = clone.GetRawText() }],
            IsError = false,
        };
    }

    public static CallToolResult Error(string code, string message)
    {
        var safe = JsonSerializer.Serialize(new { code, message });
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = safe }],
            IsError = true,
        };
    }
}
