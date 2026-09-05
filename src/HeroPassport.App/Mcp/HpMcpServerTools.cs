using ModelContextProtocol.Server;

namespace HeroPassport.App.Mcp;

public static class HpMcpServerTools
{
    public static IReadOnlyList<McpServerTool> Create(HpMcpAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        return HpMcpToolCatalog.ProtocolTools.Select(tool => (McpServerTool)new HpMcpServerTool(tool, adapter)).ToArray();
    }
}
