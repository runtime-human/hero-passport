# Hero Passport — Ecosystem Benchmark

**Status:** Architecture research baseline v3  
**Snapshot:** 2026-08-11  
**Purpose:** record which MCP/ecosystem patterns were adopted, rejected or deferred

## 1. Method

Three-pass review:

1. inspect mature/open MCP servers/clients and current host products;
2. separate domain-relevant engineering practices from scale/platform machinery;
3. verify adopted protocol/library assumptions against current official specifications/SDK docs.

License compatibility was intentionally excluded from architectural ranking for this research phase, per project instruction. This does not waive license obligations for any future code reuse.

---

## 2. GitHub MCP Server

Useful patterns:

```text
tool-surface governance
allow/deny/toolset concepts for large inventories
fail-closed configuration
compatibility awareness for published tool names
production-level security boundaries
```

Adopt:

```text
explicit tool inventory
contract drift tests
support/compatibility discipline
fail closed on invalid configuration/binding
```

Reject for Hero Passport 0.1:

```text
dynamic toolsets
large discovery/search layer
gateway-like governance machinery
```

Reason: Hero Passport has four tools. Dynamic selection infrastructure would add complexity and prompt variability without reducing an already-small surface.

---

## 3. Sentry MCP

Useful patterns:

```text
separate deterministic tests from model/agent evaluations
careful tool metadata/workflow design
production MCP behavior tested as an agent interaction problem
```

Adopt:

```text
HeroPassport.AgentEvals
host-neutral scenario definitions
model call-sequence assertions
privacy/tool-selection evals
```

This is one of the most important adopted practices because a perfect server can still provide bad UX if the model invokes it incorrectly.

---

## 4. DBHub

Useful patterns:

```text
small token-efficient tool surface
progressive disclosure
separation of product capability from model-facing capability
```

Adopt:

- four tools only;
- no history/export/doctor/admin MCP mirror;
- bounded recovery result;
- tools focus on workflow state, not analytics dump.

---

## 5. Context7

Useful pattern: MCP and CLI/skill-style paths can coexist for different consumers.

Adopt:

```text
MCP = model reasoning loop
CLI = operator/admin/script boundary
```

Do not force CLI commands into MCP tools.

---

## 6. Playwright MCP / CLI

Useful lesson: large tool schemas/results consume agent context; CLI/skills may be more efficient for tasks that do not need model-visible structured tools.

Adopt:

- tiny HP-MCP surface;
- concise tool descriptions/instructions;
- no step logging;
- keep maintenance/admin in CLI.

Hero Passport still benefits from MCP because explicit `questId` state is naturally threaded through the agent workflow and the surface is tiny.

---

## 7. ToolHive

Useful patterns:

```text
versioned config
deployment/security boundaries
explicit validation
management-plane discipline
```

Adopt concepts, reject platform machinery:

```text
NO gateway
NO registry runtime
NO Kubernetes/operator
NO container orchestration layer
NO generic OAuth proxy in local MVP
```

ToolHive is a good reference for what becomes necessary at platform scale and therefore what Hero Passport should not prebuild.

---

## 8. Official C# MCP SDK/reference architecture

Adopt directly:

```text
stable official SDK 2.0
protocol version negotiation rather than hand-roll
explicit state handles compatible with 2026 stateless model
structured output/output schemas
cache metadata
future official ASP.NET transport only when HTTP exists
```

Important v3 correction:

```text
Do not pin ProtocolVersion=2026-07-28 for the ordinary portable server.
```

The stable SDK supports multiple revisions when unpinned, so strict pinning would reduce compatibility for no Hero Passport requirement.

---

## 9. Host product comparison

### Codex

Strengths relevant to Hero Passport:

```text
stdio + Streamable HTTP
project-scoped config
stdio cwd
server instructions
fine-grained tool allow-list
shared config across local Codex host surfaces
```

Use as reference automated qualification host.

### VS Code

Relevant:

```text
workspace/user mcp.json
stdio cwd
workspace variables
remote HTTP
sandboxing on supported OSes
```

Strong project-bound local fit.

### JetBrains AI Assistant / Junie

Relevant:

```text
stdio + Streamable HTTP
Working directory
project/global MCP level
MCP tools passed to Junie
```

Strong project-bound local fit.

### Zed

Relevant:

```text
local command/args/env
remote URL/OAuth
Tools/Prompts support
MCP forwarding to external ACP agents
```

Because custom local config does not present the same cwd field as some other hosts, `--project-root` is an important portable fallback.

### Cursor

Official docs expose stdio and Streamable HTTP plus legacy SSE/OAuth. Use protocol-compatible documentation but recheck current product behavior during release smoke because host docs/products evolve quickly.

### Claude Code

Official docs expose local stdio and remote HTTP/OAuth. Project/user/local scopes differ from other hosts, reinforcing that configuration is not the portable API.

---

## 10. Multi-client contradiction discovered

Architecture v2 constrained one open quest per hero/project:

```text
hero + project -> single current quest
```

This is incompatible with realistic parallel-agent workflows:

```text
Codex coding task
+ JetBrains/Junie review task
+ terminal Claude docs task
```

Adopt v3:

```text
multiple distinct open logical quests
same logical work converges
list_active_quests recovery tool
```

This is a product/domain improvement caused by integration analysis, not a transport hack.

---

## 11. Workspace/project-binding contradiction discovered

A stdio host may provide cwd/project-level launch, but configuration mechanisms differ and MCP Roots are deprecated in the 2026 line.

Adopt:

```text
project-bound process profile
host cwd when available
--project-root portable startup fallback
no workspacePath in MCP payload
```

Reject:

```text
client-name-specific project inference
goal-text path inference
dependence on Roots
a global multi-project stdio process without an explicit binding channel
```

---

## 12. HTTP contradiction discovered

“Support Streamable HTTP” is not enough to make a remote service correct. HTTP loses the natural per-process project cwd and introduces network trust/auth.

Adopt deployment profiles:

```text
local stdio
private OpenAI tunnel to local stdio
future project-scoped HTTP
future public multi-tenant HTTP as separate architecture
```

Reject “same local server, just add URL” thinking.

---

## 13. Adopt/reject matrix

| Pattern | Decision | Reason |
|---|---|---|
| explicit four tools | Adopt | smallest portable surface |
| deterministic order | Adopt | caching/prompt stability |
| structured output | Adopt | typed machine contract |
| conservative schema subset | Adopt | cross-host robustness |
| explicit application handle | Adopt | stateless/reconnect-safe |
| multi-active workstreams | Adopt | real multi-agent workflows |
| logical same-task dedupe | Adopt | retry/handoff + no duplicate XP |
| protocol hard pin | Reject | needlessly drops SDK-compatible clients |
| host-specific runtime adapters | Reject | MCP already standardizes runtime |
| Roots for project binding | Reject | deprecated/inconsistent |
| dynamic toolsets | Reject | four-tool product |
| Resources/Prompts required | Reject | reduces common baseline |
| Tasks | Reject | operations are short |
| MCP Apps core dependency | Defer | presentation enhancement only |
| Streamable HTTP 0.1 | Reject/defer | no current need; new security boundary |
| legacy SSE | Reject | deprecated direction |
| public REST API | Reject | duplicate external API without consumer |
| MCP Registry runtime dependency | Reject | distribution only, Registry preview |
| Registry publication | Defer | re-evaluate package identity/maturity |
| AgentEvals | Adopt | tests model behavior, not only server code |

---

## 14. Review triggers

Repeat ecosystem/official-doc review before:

```text
MCP spec/SDK major revision
fifth tool
HTTP/OAuth
public hosted deployment
MCP Apps/Resources/Prompts reliance
registry publication
major host moved to Qualified tier
separate public API
plugin/runtime extension architecture
```

The goal is not continuous trend-chasing. Review when an external change crosses a Hero Passport boundary.
