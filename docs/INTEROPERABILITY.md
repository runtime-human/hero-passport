# Hero Passport — MCP Interoperability Profile

**Status:** Accepted v3  
**Snapshot:** 2026-08-11  
**Goal:** maximize portable local use without weakening Hero Passport contracts to the least capable host

## 1. Portability definition

Hero Passport calls itself portable only when the **same server binary and HP-MCP/2 tool semantics** can be used by multiple MCP hosts. Portability does not mean host configuration files are identical.

```text
portable runtime = hero-passport mcp
portable contract = HP-MCP/2
host adapter      = configuration/instructions only
```

No host-specific runtime DLLs or business branches.

---

## 2. Protocol baseline

Preferred semantic baseline: MCP `2026-07-28`.

Server protocol version is not hard-pinned. Official C# SDK v2 compatibility negotiation is retained so older supported initialize-era clients can connect.

Application state is explicit (`questId`) and survives reconnect/restart independent of transport sessions.

0.1.0 does not require any extension beyond core Tools.

---

## 3. Interoperability JSON Schema subset

Although 2026 MCP accepts full JSON Schema 2020-12, Hero Passport constrains itself to a widely implemented subset:

```text
object inputs/outputs
closed properties
required
enum
bounded string/integer/array
boolean
one small level of nested objects
```

Avoid advanced combinators and recursive/external references. This is a deliberate compatibility policy, not a protocol limitation.

---

## 4. Tool/result compatibility

### Tool names

Stable names contain only portable MCP-safe characters:

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

### Machine output

Canonical machine data uses `structuredContent` and an `outputSchema`.

### Human fallback

Each result also has a short text representation. It is not a duplicate JSON serialization.

This gives newer clients structured data while keeping useful behavior on clients that primarily surface text content.

---

## 5. Tool list stability

Inventory is deterministic and compile-time static in 0.1.0.

For 2026 cache hints:

```text
cacheScope = public
initial ttlMs policy = 300000
```

The five-minute value is an implementation freshness default, not a semantic compatibility promise. A patch release may tune it if interop testing shows a better value.

Hero Passport does not use `notifications/tools/list_changed` while its list is static.

---

## 6. Project-bound local-server profile

Portable correctness requires a project-bound server launch because the 2026 protocol does not provide a mandatory current-workspace field and Roots are deprecated.

Supported mechanisms, in preference order:

```text
host cwd / project-level server config
explicit hero-passport mcp --project-root <path>
```

A host that launches one global process without a stable project binding cannot receive correct per-project progression automatically. That limitation must be documented rather than solved by putting local paths into model-visible tool arguments.

---

## 7. Host support tiers

### Qualified

A host is Qualified when the current release passes its defined smoke/E2E checklist, including start/list/finish/card and project binding.

Initial release gate:

```text
Codex CLI — automated Qualified reference host
```

### Documented / protocol-compatible

A host may have current official configuration documentation and be expected to work through stdio without yet being a release-blocking automated target.

Initial documented candidates:

```text
VS Code
JetBrains AI Assistant / Junie path
Zed Agent
Cursor
Claude Code
ChatGPT private tunnel path (different deployment profile)
```

Do not label these Qualified until smoke evidence is recorded for the release.

### Unsupported

A host is unsupported when it cannot provide required Tools + project-bound launch/endpoint semantics or when known implementation behavior breaks HP-MCP/2.

---

## 8. Current host configuration differences

These differences are intentionally isolated in documentation:

```text
Codex       TOML mcp_servers.*; cwd supported
VS Code     .vscode/mcp.json / profile mcp.json; servers; cwd + workspace variables
JetBrains   mcpServers JSON + explicit Working directory + project/global level
Zed         context_servers; local command/args/env or remote URL
Cursor      mcpServers; stdio / Streamable HTTP / legacy SSE
Claude Code CLI/project JSON configuration; stdio / HTTP; OAuth for remote
```

Hero Passport does not normalize these formats into its own runtime configuration.

A future `hero-passport integration show <host>` command may render examples from one internal descriptor, but it must only print; it must not mutate other applications' files by default.

---

## 9. Features deliberately not required for portability

```text
Resources
Prompts
Roots
Sampling
MCP Logging
Elicitation/MRTR
Tasks
MCP Apps
OAuth
legacy SSE
```

Some hosts support some of these, but requiring them would reduce the common compatibility baseline without helping the core quest lifecycle.

Prompts or Apps may later improve onboarding/presentation as optional enhancements; tools remain the functional baseline.

---

## 10. Transport policy

### 0.1.0

```text
stdio only
```

Why: every primary coding-host class we target supports a local subprocess path, and stdio avoids HTTP/auth/network configuration for the local-first product.

### Future Streamable HTTP

Only when a concrete consumer requires a URL-based server. New HTTP implementation uses official MCP Streamable HTTP; do not implement new legacy HTTP+SSE.

HTTP introduces separate project binding and security rules described in `DEPLOYMENT-MODES.md`.

### Private OpenAI remote access

OpenAI Secure MCP Tunnel can forward to a private local MCP server over stdio or HTTP. This gives a remote OpenAI integration path without forcing Hero Passport 0.1 to expose a listener.

---

## 11. Client metadata

MCP `2026-07-28` carries client information per request. It may be used for bounded diagnostics/compatibility investigation.

It must not be used as:

```text
authentication
hero identity
project identity
reward signal
Trust/Risk signal
```

Older-protocol client info is normalized by the adapter into the same diagnostic concept where available.

---

## 12. Compatibility test matrix

Automated 0.1 gates:

```text
actual SDK manifest/schema snapshot
tools/list deterministic order/cache metadata
2026-07-28 client path
2025-11-25 compatibility path
MCP Inspector
Codex CLI E2E
stdio stdout purity
project-root/cwd binding
multi-agent active-quest behavior
```

Release smoke matrix:

```text
VS Code
JetBrains AI Assistant
Zed
Cursor
Claude Code
```

Each integration page records:

```text
documentation verified date
smoke-tested release/date
configuration scope
known limitations
```

---

## 13. Host-neutral agent evals

Behavior scenarios describe an Agent, not Codex-specific behavior:

```text
meaningful task -> one start + one finish
parallel distinct tasks -> distinct quest IDs
same task repeated -> same open quest
lost ID -> list active quests
finish retry -> one reward
small factual question -> no needless quest
privacy adversarial request -> no forbidden payload
```

Runners can later execute the same scenario set against Codex and other agents without changing expected Hero Passport semantics.

---

## 14. Compatibility review triggers

Repeat a focused interoperability review before shipping any of:

```text
fifth MCP tool
advanced JSON Schema constructs
dynamic/per-user tool inventory
Resources/Prompts/Apps dependency
Streamable HTTP
OAuth
global multi-project server mode
public MCP Registry publication
separate public API
```

Portability is maintained by keeping these decisions explicit rather than accumulating optional protocol features accidentally.
