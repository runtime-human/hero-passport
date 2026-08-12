using ModelContextProtocol.Server;
using System.Collections;

namespace HeroPassport.App.Mcp;

public sealed class OrderedMcpServerToolCollection : McpServerPrimitiveCollection<McpServerTool>
{
    private readonly McpServerTool[] _orderedTools;

    public OrderedMcpServerToolCollection(IEnumerable<McpServerTool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        _orderedTools = tools.ToArray();
        foreach (var tool in _orderedTools)
        {
            Add(tool);
        }
    }

    public override ICollection<string> PrimitiveNames =>
        _orderedTools.Select(static tool => tool.ProtocolTool.Name).ToArray();

    public override McpServerTool[] ToArray() => [.. _orderedTools];

    public override void CopyTo(McpServerTool[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        for (var index = 0; index < _orderedTools.Length; index++)
        {
            array[arrayIndex + index] = _orderedTools[index];
        }
    }

    public override IEnumerator<McpServerTool> GetEnumerator() =>
        ((IEnumerable<McpServerTool>)_orderedTools).GetEnumerator();
}
