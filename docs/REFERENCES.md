# Hero Passport — Official Reference Baseline

**Snapshot:** 2026-08-11

This file records the external primary documentation used for architecture decisions. Implementation should recheck these sources when upgrading dependencies or changing protocol/deployment behavior.

## 1. MCP protocol

### Final MCP 2026-07-28 release

- https://blog.modelcontextprotocol.io/posts/2026-07-28/

Architecture-relevant points:

```text
stateless protocol core
initialize/initialized retired for 2026 wire era
Mcp-Session-Id removed
per-request protocol/client metadata
explicit application state handles recommended
cacheable/deterministic list results
Tasks/extensions framework
Roots/Sampling/Logging and legacy HTTP+SSE deprecation direction
```

### Stateless explicit handles

- https://modelcontextprotocol.io/seps/2567-sessionless-mcp

Used for `questId` state-handle design.

### List caching

- https://modelcontextprotocol.io/seps/2549-TTL-for-list-results

Used for explicit `ttlMs`/`cacheScope` policy. TTL remains implementation freshness policy rather than HP-MCP semantic contract.

### Tools specification

- https://modelcontextprotocol.io/specification/2026-07-28/server/tools

Used for deterministic tool list, JSON Schema/output schema/structured content and annotations guidance.

### Transport/security

- https://modelcontextprotocol.io/specification/2026-07-28/basic/transports

Used for stdio/Streamable HTTP and future Origin/security requirements.

### Authorization

- https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization

Used only for future HTTP deployment boundaries. Local stdio does not adopt HTTP OAuth semantics.

---

## 2. Official MCP C# SDK

### SDK v2 documentation

- https://csharp.sdk.modelcontextprotocol.io/v2/

### `McpServerOptions.ProtocolVersion`

- https://csharp.sdk.modelcontextprotocol.io/api/ModelContextProtocol.Server.McpServerOptions.html

Key verified behavior:

```text
supported values include 2024-11-05, 2025-03-26, 2025-06-18, 2025-11-25, 2026-07-28
null supports negotiation across the SDK-supported eras
pin 2026-07-28 rejects initialize handshakes
pin older revision rejects 2026 per-request metadata
```

This is why Hero Passport does not hard-pin protocol version.

### Stateless/stateful mode

- https://csharp.sdk.modelcontextprotocol.io/v2/concepts/stateless/stateless.html

Used for compatibility design and future HTTP transport configuration. Architecture carefully distinguishes session-independent application semantics from transport-specific `Stateless` options.

### Transports

- https://csharp.sdk.modelcontextprotocol.io/v2/concepts/transports/transports.html

Used for future loopback/Host/Origin security guidance.

---

## 3. OpenAI / Codex

### Codex MCP

- https://developers.openai.com/codex/mcp/

Verified:

```text
local stdio support
Streamable HTTP support
server instructions
first 512 instruction characters should be self-contained
shared Codex host config across desktop/CLI/IDE extension
ChatGPT web uses hosted/plugin MCP path rather than local Codex config
```

### Codex configuration reference

- https://developers.openai.com/codex/config-reference/

Verified `mcp_servers.<id>` options including:

```text
command
args
cwd
enabled_tools/disabled_tools
url
HTTP auth/header settings
startup/tool timeouts
```

### Secure MCP Tunnel

- https://developers.openai.com/api/docs/guides/secure-mcp-tunnels

Verified:

```text
private MCP reachable without inbound public listener
tunnel-client can reach local MCP over stdio or HTTP
supported OpenAI surfaces include ChatGPT/Codex/Responses paths documented there
private tunnel is not public plugin submission
```

---

## 4. Other MCP hosts

### VS Code

- https://code.visualstudio.com/docs/agent-customization/mcp-servers
- https://code.visualstudio.com/docs/agents/reference/mcp-configuration

Verified:

```text
workspace/user mcp.json
"servers"
stdio command/args/cwd/env
workspace variables
remote HTTP
```

### JetBrains AI Assistant 2026.2

- https://www.jetbrains.com/help/ai-assistant/mcp.html

Verified:

```text
STDIO
Streamable HTTP
legacy SSE compatibility
mcpServers JSON
Working directory
project/global server level
```

### Zed

- https://zed.dev/docs/ai/mcp

Verified:

```text
Tools/Prompts support
local command/args/env
remote URL/headers/OAuth
context_servers configuration
external-agent forwarding via ACP where applicable
```

### Cursor

- https://docs.cursor.com/context/model-context-protocol

Official page documents stdio, Streamable HTTP and OAuth. Because the indexed page snapshot may lag current product details, release qualification must recheck current docs/product before claiming Qualified status.

### Claude Code

- https://docs.anthropic.com/en/docs/claude-code/mcp

Official documentation describes local stdio and remote HTTP/OAuth configuration. As with any fast-moving host, recheck current docs/product during RC smoke qualification.

### ACP distinction

JetBrains/Zed documentation distinguishes ACP external agents from MCP tools. Hero Passport remains an MCP server, not an ACP agent.

---

## 5. .NET / SQLite

### .NET 10

- https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview

### System.CommandLine

- https://learn.microsoft.com/en-us/dotnet/standard/commandline/

### NuGet Central Package Management

- https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management

### EF Core SQLite limitations/migrations

- https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations

Used for SQLite provider limitations and EF migration-lock behavior.

### Microsoft.Data.Sqlite async limitation

- https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async

Used for intentional short synchronous SQLite DB segments.

### Microsoft.Data.Sqlite errors/timeout

- https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/database-errors

Used for busy/locked timeout behavior.

### SQLite WAL / PRAGMA

- https://www.sqlite.org/wal.html
- https://www.sqlite.org/pragma.html

Used for WAL/durability policy.

### Platform paths

- https://learn.microsoft.com/en-us/dotnet/api/system.environment.specialfolder
- https://specifications.freedesktop.org/basedir/latest/

---

## 6. Testing

### xUnit.net v3

- https://xunit.net/docs/getting-started/v3/getting-started
- https://xunit.net/docs/getting-started/v3/microsoft-testing-platform

### MCP Inspector

- https://github.com/modelcontextprotocol/inspector

Used as protocol/manual release smoke evidence, not as the only automated contract test.

---

## 7. MCP Registry

- https://modelcontextprotocol.io/registry/about
- https://modelcontextprotocol.io/registry/package-types

Registry is preview at this snapshot. It supports NuGet package metadata/ownership verification. Hero Passport does not depend on Registry at runtime.

---

## 8. Source hierarchy

For protocol/library behavior:

```text
final official specification
> official SDK documentation/source
> official host documentation
> repository implementation evidence
> third-party analysis
```

When official docs disagree or are temporarily stale across a release transition, do not silently choose whichever text is convenient. Record the ambiguity, write an interoperability test and prefer behavior proven by the stable released SDK/spec combination.

## 9. Revalidation triggers

Recheck primary sources before:

```text
MCP SDK upgrade
new MCP spec revision
new HTTP deployment
new auth mode
new Registry publication
host moved to Qualified tier
EF/SQLite upgrade
packaging/native SQLite strategy change
```
