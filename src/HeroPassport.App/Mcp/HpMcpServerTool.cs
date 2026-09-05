using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace HeroPassport.App.Mcp;

internal sealed class HpMcpServerTool(Tool protocolTool, HpMcpAdapter adapter) : McpServerTool
{
    public override Tool ProtocolTool { get; } = protocolTool;

    public override IReadOnlyList<object> Metadata { get; } = Array.Empty<object>();

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default) =>
        await adapter.InvokeAsync(ProtocolTool.Name, request.Params.Arguments, cancellationToken).ConfigureAwait(false);
}
