using ModelContextProtocol.Server;
using System.Collections;

namespace HeroPassport.App.Mcp;

public sealed class OrderedMcpServerToolCollection : McpServerPrimitiveCollection<McpServerTool>
{
    private readonly IReadOnlyList<McpServerTool> _orderedTools;

    public OrderedMcpServerToolCollection(IEnumerable<McpServerTool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        var ordered = tools.ToArray();
        foreach (var tool in ordered)
        {
            Add(tool);
        }

        _orderedTools = ordered;
    }

    public override ICollection<string> PrimitiveNames =>
        _orderedTools.Select(static tool => tool.ProtocolTool.Name).ToArray();

    public override McpServerTool[] ToArray() => [.. _orderedTools];

    public override void CopyTo(McpServerTool[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        for (var index = 0; index < _orderedTools.Count; index++)
        {
            array[arrayIndex + index] = _orderedTools[index];
        }
    }

    public override IEnumerator<McpServerTool> GetEnumerator() =>
        _orderedTools.GetEnumerator();
}
