# Hero Passport — MCP Interoperability Profile

**Status:** Accepted v3.1  
**Snapshot:** 2026-08-11  
**Goal:** maximize portable local use while retaining exact Hero Passport semantics

Exact wire behavior: `WIRE-CONTRACT.md`. Exact project binding: `PROJECT-IDENTITY.md`.

---

## 1. Portability definition

```text
portable runtime  = hero-passport mcp
portable contract = HP-MCP/2
host differences  = launch/configuration/qualification only
```

No Codex/Cursor/JetBrains-specific business DLLs.

A host is not “supported” merely because a config snippet can be written.

---

## 2. Protocol baseline

```text
preferred semantics: MCP 2026-07-28
ProtocolVersion: null/unset
compatibility qualification: 2026-07-28 + 2025-11-25
```

Application state uses explicit `questId` + SQLite, never connection/session identity.

Core requirement is Tools only.

---

## 3. Schema subset

Use a conservative cross-host profile:

```text
object roots
closed properties
required fields
closed enums
bounded strings/integers/arrays
boolean
small nested objects
simple interoperable patterns/format annotations when useful
```

Avoid advanced combinators/recursive/external schemas unless a future requirement proves them necessary.

Runtime validation is explicit server code; C# SDK generated schema/DataAnnotations do not enforce arguments.

---

## 4. Tool names

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

Static/deterministic explicit inventory.

---

## 5. Success compatibility representation — v3.1

Canonical machine data:

```text
structuredContent + outputSchema
```

Backward compatibility:

```text
exactly one TextContent containing minified serialized JSON
semantically equal to structuredContent
```

`displayText` is a human-facing field inside that result object.

This replaces the older idea of returning an unrelated human-only fallback string, because MCP 2026 recommends serialized JSON TextContent when structured content is returned.

---

## 6. Error compatibility representation

Expected tool/validation/business failures:

```text
isError=true
one safe TextContent
structuredContent absent
```

This remains actionable for models while avoiding conflict with the success output schema.

Protocol/framing/unknown-tool errors remain MCP errors.

---

## 7. Annotation compatibility

```text
start      idempotent=false
finish     idempotent=true
list       idempotent=true
card       idempotent=true
```

Start is only retry-safe while a matching open `QuestDedupKeyV1` declaration exists; the same arguments after completion may intentionally create another quest.

Do not advertise stronger semantics merely because a particular host would benefit from auto-retry.

---

## 8. Tool list/cache behavior

Inventory is identical for every 0.1 local user and deterministic.

2026 cache metadata:

```text
cacheScope=public
initial ttlMs may be 300000
```

TTL is tuning/freshness policy, not HP-MCP semantics. Do not advertise listChanged while inventory is static.

If future authorization changes tool visibility per caller, public cache scope must be re-reviewed.

---

## 9. Project-bound stdio profile

Portable project correctness requires project-bound launch.

Host mechanism:

```text
host-native cwd/project-level server config
or explicit hero-passport mcp --project-root <path>
```

After start, `project-identity/1` performs the same Git-aware normalization for every host:

```text
Git common-dir anchor
whole repo default scope
explicit repo-relative subproject scope when requested
linked worktree convergence
submodule/nested-repo separation
```

A globally shared stdio process without a trusted project binding cannot infer per-project state from model text; we document that limitation instead of adding `workspacePath` to tools.

---

## 10. Host support tiers

### Qualified

Passes release-defined smoke/E2E including:

```text
server startup/project binding
tools/list exact contract
start/list/finish/card
result compatibility representation
error representation
persistence across process restart
```

Codex is first automated Qualified reference target.

### Documented / protocol-compatible

Official host docs support the required stdio/Tools shape, but current Hero Passport release has not completed the full qualification gate.

Candidates:

```text
VS Code
JetBrains AI Assistant
Zed
Cursor
Claude Code
ChatGPT private tunnel path as a distinct deployment profile
```

### Unsupported/unknown

Known inability to provide required Tools/project binding/result behavior, or no release evidence.

---

## 11. Host configuration differences

These remain documentation-only concerns:

```text
Codex       mcp_servers.* / cwd
VS Code     mcp.json / servers / cwd + workspace variables
JetBrains   mcpServers / Working directory / project-global scope
Zed         context_servers / command-args-env or remote URL
Cursor      host-specific MCP config
Claude Code host-specific MCP config
```

Current official docs are rechecked at RC because these formats evolve independently of HP-MCP.

A future `integration show` command may print current snippets but does not mutate host config by default.

---

## 12. Features not required for portability

```text
Resources
Prompts
Roots
Sampling
MCP Logging
MRTR/elicitation
Tasks
MCP Apps
OAuth
legacy SSE
```

Optional enhancements may be revisited, but Tools remain the core lifecycle baseline.

---

## 13. Transport policy

0.1:

```text
stdio only
```

Future URL consumer:

```text
Streamable HTTP through a separate deployment/security design
```

Never implement a new legacy SSE path.

Private OpenAI remote access can use Secure MCP Tunnel to the local server, so it does not force a Hero Passport HTTP listener into 0.1.

---

## 14. Client metadata

Client info can support bounded diagnostics/interop investigation.

Never:

```text
authentication
hero selection
project selection
reward/Trust/Risk
persistent identity by default
```

---

## 15. Compatibility test matrix

Automated:

```text
actual tool/schema/result snapshots
exact annotations
2026-07-28 path
2025-11-25 path
structuredContent == parsed JSON TextContent
business error has no structuredContent
MCP Inspector
Codex E2E
stdio stdout purity
project identity binding vectors
```

Release smoke:

```text
VS Code
JetBrains
Zed
Cursor
Claude Code
```

Each integration page records host/version/OS/date/binding/result/caveat.

---

## 16. Host-neutral AgentEvals

```text
meaningful work -> start + finish
parallel distinct declarations -> distinct quests
same normalized open declaration -> reuse same quest
case-different declaration -> distinct quest
same declaration after finish/new work cycle -> new quest
lost questId -> list active quests
finish retry -> same reward
small factual request -> no quest
privacy adversarial request -> no forbidden fields
```

Recovery uses explicit list/ID, not fuzzy task matching.

---

## 17. Revisit triggers

Focused interop review before:

```text
fifth tool
advanced schema constructs
dynamic/per-user tool inventory
new MCP feature dependency
Streamable HTTP/OAuth
global multi-project server
public Registry publication
separate public API
```
