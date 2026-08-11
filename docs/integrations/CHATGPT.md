# Hero Passport — ChatGPT / OpenAI Remote Integration

**Status:** Deployment documentation; not a 0.1 local-host qualification claim  
**Documentation verified:** 2026-08-11

## 1. Two different OpenAI paths

Do not conflate:

```text
local Codex host configuration
```

with:

```text
ChatGPT web / Responses remote MCP access
```

Local Codex surfaces use Codex MCP configuration; ChatGPT web/public remote access uses hosted/plugin/tunnel mechanisms.

---

## 2. Private Secure MCP Tunnel

OpenAI Secure MCP Tunnel provides a private outbound path from supported OpenAI products to an MCP server behind a firewall/NAT.

Relevant property for Hero Passport:

```text
tunnel-client can connect to the local target over stdio
```

Therefore Hero Passport 0.1 can remain:

```text
hero-passport mcp --project-root <project>
```

while the OpenAI tunnel-client handles remote connectivity.

No Hero Passport HTTP listener is required solely for this private integration.

## 3. Trust boundary

Tunnel/API credentials belong to OpenAI tunnel/platform configuration.

Do not:

```text
store tunnel API keys in Hero Passport config
persist them in SQLite
pass them through MCP tools
commit them in integration examples
```

Hero Passport still uses its local hero/project binding and local privacy rules.

## 4. Project binding

The local tunnel target should launch/connect to a project-bound Hero Passport instance:

```text
host/tunnel local process
  -> hero-passport mcp --project-root <path>
```

Remote ChatGPT/model requests do not receive the filesystem path as a Hero Passport tool parameter.

## 5. Public plugin/server distribution

OpenAI documentation distinguishes private Secure MCP Tunnel from public plugin distribution. A public ChatGPT plugin/server requires a stable publicly reachable HTTPS MCP endpoint.

That is a future hosted deployment profile with:

```text
Streamable HTTP
TLS
MCP authorization
principal/project/hero authorization
remote persistence/tenant isolation as needed
```

Do not expose the local SQLite app directly to the public internet.

## 6. Responses API

Where OpenAI supports MCP tools through the tunnel/remote MCP path, HP-MCP/2 remains the same semantic contract. The deployment layer changes; the four Hero Passport tools do not.

## 7. Support claim

Private tunnel integration is documented as an external deployment path. It is not part of the 0.1 Codex CLI qualification gate unless the project explicitly promotes it after an end-to-end tunnel test.

Record any later qualification with:

```text
OpenAI product/surface
organization/workspace context
tunnel-client version/config
target transport = stdio
project binding
exact four tools
start/list/finish/card
verified date
```
