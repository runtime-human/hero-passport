# Hero Passport — MCP ecosystem benchmark

**Status:** Accepted research baseline  
**Snapshot date:** 2026-08-10  
**Purpose:** derive production patterns for Hero Passport from mature open MCP servers, coding-agent integrations, local-first applications and the official MCP SDK/specification.  
**License policy for this analysis:** licensing is intentionally not used as an architectural ranking criterion.

## 1. Why this document exists

Hero Passport should not copy a single MCP project. The useful patterns are distributed across projects with very different workloads:

- GitHub MCP has a very large tool inventory and strong configuration/backward-compatibility practices;
- Sentry MCP is optimized for human-in-the-loop coding agents and has explicit agent evaluations;
- DBHub demonstrates a deliberately tiny, token-efficient MCP surface with progressive disclosure;
- Context7 and Playwright show when CLI + Skills is superior to MCP for coding agents;
- ToolHive demonstrates production governance/configuration patterns, but also shows how quickly a server can become a platform;
- the official MCP C# SDK and protocol specification define the compatibility boundary we must not improvise around.

The goal is therefore **pattern extraction followed by rejection/adoption**, not imitation.

## 2. Method: three passes

### Pass A — architecture extraction

For every project, identify:

1. tool-surface size and discovery strategy;
2. state model;
3. transport model;
4. configuration and security boundaries;
5. testing/evaluation approach;
6. CLI/UI relationship;
7. backward compatibility;
8. token/context strategy.

### Pass B — Hero Passport fitness test

Every extracted pattern is tested against Hero Passport constraints:

```text
local-first
4-tool MVP
Codex-first
stdio-first
no secrets required by server
no source/diff/raw-log ingestion
short requests
persistent domain state in SQLite
very low token overhead
deterministic RPG rules
single-user local product
```

Patterns are rejected if they solve a scale/security/distribution problem that Hero Passport does not yet have.

### Pass C — specification/library verification

Surviving patterns are checked against current primary documentation as of 2026-08-10:

- MCP 2026-07-28;
- official MCP C# SDK 2.0.0;
- current Codex MCP/config documentation;
- .NET 10 / EF Core 10 / Microsoft.Data.Sqlite documentation;
- SQLite WAL/durability documentation;
- current stable NuGet releases.

Only after this pass does a pattern become an architecture decision.

---

## 3. GitHub MCP Server

Repository: `github/github-mcp-server`.

### What it does well

GitHub MCP has a huge potential operation set, so it exposes toolsets and individual-tool allow-lists. Its documentation explicitly explains that reducing enabled tools helps tool choice and context size. Invalid individual tool names fail local-server startup. Read-only mode takes precedence over requested write tools. Renamed tools preserve old names as compatibility aliases.

These are production-grade lessons:

- the advertised inventory is part of the product contract;
- configuration errors should fail early rather than silently widen behavior;
- tool names become compatibility surface;
- a server should expose the smallest useful inventory;
- security filters must override convenience configuration.

### What Hero Passport adopts

1. **Exact static tool allow-list.** The expected tool set is a constant and is verified at startup/tests.
2. **Fail-closed registration.** Startup/tests fail if the set differs from the canonical four tools.
3. **Tool-name compatibility policy.** After 0.1.0, a rename requires a compatibility alias for at least one minor release unless an explicit breaking version is declared.
4. **Inventory size as a quality metric.** Adding a fifth tool requires an architecture decision, token-budget measurement and agent-eval evidence.

### What Hero Passport rejects

- toolsets;
- dynamic tool discovery;
- runtime enable/disable tool configuration;
- dozens of capability groups.

Those mechanisms exist because GitHub MCP has a large surface. With four tools, they would make Hero Passport harder to reason about while providing no real context saving.

### Additional caution learned from GitHub MCP

Large configuration surfaces create their own correctness bugs. GitHub MCP has had issues where documented filtering semantics diverged between execution modes. Hero Passport avoids this entire class by making the MVP inventory fixed and compile-time registered.

---

## 4. Sentry MCP

Repository: `getsentry/sentry-mcp`.

### What it does well

Sentry explicitly optimizes its MCP server for **human-in-the-loop coding agents**, not generic API coverage. Its repository distinguishes three verification layers:

```text
unit tests
evaluations
manual/agent testing
```

This distinction is important. Protocol/unit tests can prove that a tool works, but cannot prove that a model chooses the intended tool at the intended time.

### What Hero Passport adopts

A dedicated **agent-evaluation layer** in addition to deterministic tests.

Representative scenarios:

```text
meaningful coding task
  -> start once
  -> finish once

simple factual/read-only question
  -> no quest by default

finish retry
  -> same persisted outcome
  -> no second XP event

existing open quest
  -> current/start behavior is predictable

malicious/oversized context
  -> never send/store forbidden fields
```

Agent evals are not release correctness substitutes. They detect workflow/tool-description regressions and are initially manual/nightly because model behavior can vary.

### What Hero Passport rejects

- an embedded LLM agent inside the MCP server;
- a meta-tool that hides the whole tool surface behind one agent tool;
- remote-service middleware architecture.

Hero Passport has only four operations and a deterministic engine. Adding another model inside the server would make rewards non-deterministic, add cost and expand the privacy boundary.

---

## 5. DBHub

Repository: `bytebase/dbhub`.

### What it does well

DBHub describes itself as local-development-first and token-efficient, with only two primary MCP tools. Database exploration uses progressive disclosure rather than flooding the model with schema context. It also puts guardrails such as query timeout and read-only behavior close to the capability boundary.

### What Hero Passport adopts

1. **Tiny tool surface is a feature, not a limitation.**
2. **Progressive disclosure for future history.** If quest history is ever exposed through MCP, return summary pages/handles first rather than full history.
3. **CLI/dashboard can be richer than MCP.** MCP is not required to expose every local product capability.
4. **Guardrails belong at the boundary.** Strict schema limits and storage/privacy constraints are enforced in code, not only written in AGENTS.md.

### What Hero Passport rejects

- custom MCP tools from user configuration;
- arbitrary query execution;
- dynamic capability creation.

Hero Passport rules must remain canonical and deterministic.

---

## 6. Context7

Repository: `upstash/context7`.

### What it does well

Context7 supports both MCP and CLI + Skills. Its skill-based mode explicitly guides coding agents through concise CLI commands rather than requiring every capability to live permanently in MCP context.

### What Hero Passport adopts

**MCP and CLI are equal product adapters over the same Application core.**

Use MCP where the agent benefits from a typed stateful workflow:

```text
start quest -> questId -> finish quest
```

Use CLI for:

- administration;
- diagnostics;
- export;
- data-path inspection;
- configuration inspection;
- human history views;
- future maintenance operations.

This prevents the MCP surface from becoming a mirror of the entire CLI.

### What Hero Passport rejects

No feature should be duplicated as an MCP tool merely because a CLI command exists.

---

## 7. Playwright MCP and Playwright CLI

Repository: `microsoft/playwright-mcp` plus the companion Playwright CLI.

### What it does well

The project documentation explicitly notes that modern coding agents can benefit from CLI + Skills because large MCP schemas and verbose tool results consume context. This is a useful counterexample to the assumption that “more MCP” is automatically more agent-native.

### Hero Passport conclusion

Hero Passport **is still a good MCP fit**, but only because its MCP surface is intentionally tiny and its persistent quest handle is useful to the model.

The lesson is a permanent architecture guardrail:

> If a future feature can be invoked naturally through shell/CLI and does not need to be part of the model's normal reasoning loop, prefer CLI/dashboard over another always-advertised MCP tool.

Examples that stay outside MCP by default:

```text
doctor
export
reset/delete
database maintenance
configuration editing
full history browsing
dashboard launch
```

---

## 8. ToolHive

Repository: `stacklok/toolhive`.

### What it does well

ToolHive has a versioned configuration contract, explicit runtime/security boundaries, a strong architecture documentation set and separation among gateway, registry, runtime and UI concerns.

### What Hero Passport adopts

1. **Configuration is a versioned contract.** `configVersion` starts at 1.
2. **Import/export/config changes are validated, never “best effort”.** Unknown/invalid values fail with actionable diagnostics.
3. **Architecture docs change with architecture.** New concepts require matching docs/ADR updates.
4. **Security boundaries are explicit.** Permissions and data-flow assumptions are documented rather than inferred from implementation.

### What Hero Passport rejects

Almost all of ToolHive's runtime topology:

```text
gateway
proxy/middleware chain
registry
Kubernetes operator
container runtime abstraction
OIDC/OAuth
remote aggregation
semantic tool search
OpenTelemetry exporter baseline
```

Hero Passport is a local application, not an MCP management platform.

---

## 9. Official MCP reference servers

Repository: `modelcontextprotocol/servers`.

The official project describes reference implementations as examples/educational material rather than production templates. They are useful for protocol idioms and annotations, but Hero Passport should not treat their project layout, operational behavior or old feature usage as canonical production architecture.

Important consequence: protocol correctness comes from the current specification and official C# SDK first, then production repositories, then reference examples.

---

## 10. Official MCP C# SDK

Repository: `modelcontextprotocol/csharp-sdk`.

### Adopted package boundary

For the local stdio MVP:

```text
ModelContextProtocol
```

is the correct package. It includes hosting/DI and references the low-level Core package. `ModelContextProtocol.AspNetCore` is deferred until a real HTTP MCP requirement exists.

### Registration strategy

Hero Passport uses **explicit generic/type registration** for the four tool adapter types and avoids assembly-wide tool scanning.

Reasons:

- exact inventory is visible in composition root;
- accidental `[McpServerTool]` methods cannot silently become public tools;
- deterministic order is easier to guarantee/test;
- fewer reflection/dynamic-discovery assumptions;
- future trimming/AOT experiments remain easier even though NativeAOT is not an MVP requirement.

---

## 11. Cross-project pattern matrix

| Pattern | GitHub MCP | Sentry | DBHub | Context7 | Playwright | ToolHive | Hero Passport |
|---|---|---|---|---|---|---|---|
| minimize tool inventory | strong | workflow-focused | very strong | strong | strongly motivated | dynamic at scale | **adopt fixed 4** |
| dynamic discovery | useful at scale | optional/meta | no | no | no | platform feature | **reject** |
| CLI beside MCP | yes | dev CLI | app/CLI | strong | strong | strong | **adopt** |
| agent evals | some integration tests | **strong** | limited | workflow tests | practical agent tests | platform tests | **adopt** |
| backward tool aliases | **strong** | evolving | limited | evolving | evolving | platform contracts | **adopt after 0.1** |
| remote HTTP/OAuth | yes | core | optional | remote | common | core | **defer** |
| progressive disclosure | toolsets | workflow | **strong** | two-stage | accessibility state | semantic discovery | **future history only** |
| versioned config | yes | env/config | TOML | setup config | config | **strong** | **adopt** |
| embedded/meta agent | no | yes | no | skills | skills | workflows | **reject** |
| rich gateway/runtime | no | middleware | gateway-lite | no | no | **core** | **reject** |

---

## 12. Final adopted MCP principles

The benchmark resolves to these Hero Passport rules:

1. **Four tools, fixed and explicit.**
2. **MCP is a narrow agent workflow adapter, not the product's full API.**
3. **CLI owns administration and maintenance.**
4. **Application/domain own behavior; transport adapters stay thin.**
5. **Tool schemas/descriptions/names/annotations are compatibility artifacts.**
6. **Use explicit state handles (`questId`), never hidden protocol session state.**
7. **Strict JSON schemas; no arbitrary metadata bags.**
8. **Typed structured results and output schemas.**
9. **Server instructions guide workflow but never enforce security.**
10. **Agent evaluations complement deterministic tests.**
11. **Tool additions require evidence, not enthusiasm.**
12. **No remote/platform architecture before a real requirement exists.**

---

## 13. Rejected “modernity theater”

The following are deliberately *not* added to make the project look modern:

- runtime plugin loading;
- dynamic MCP tool discovery;
- semantic tool router;
- gateway/middleware framework;
- MCP Tasks;
- MCP Apps;
- HTTP MCP;
- OAuth/OIDC;
- event bus;
- message broker;
- OpenTelemetry exporter;
- embedded LLM judge;
- generic repository;
- CQRS framework;
- internal mediator library;
- separate microservices.

Modern architecture here means **using current protocol semantics and precise boundaries with the smallest sufficient mechanism**.

## 14. Review trigger

Re-run this benchmark before any of these changes:

- MCP tool count grows beyond 6;
- HTTP/remote server becomes a real requirement;
- team/multi-user mode is introduced;
- a second database backend is proposed;
- external plugins are proposed;
- Hero Passport starts consuming remote APIs;
- MCP Apps or Tasks are considered;
- an agent/client other than coding agents becomes a primary target.

## 15. Primary sources

- MCP 2026-07-28 release: https://blog.modelcontextprotocol.io/posts/2026-07-28/
- MCP tool specification: https://modelcontextprotocol.io/specification/draft/server/tools
- MCP C# SDK: https://github.com/modelcontextprotocol/csharp-sdk
- GitHub MCP: https://github.com/github/github-mcp-server
- Sentry MCP: https://github.com/getsentry/sentry-mcp
- DBHub: https://github.com/bytebase/dbhub
- Context7: https://github.com/upstash/context7
- Playwright MCP: https://github.com/microsoft/playwright-mcp
- ToolHive: https://github.com/stacklok/toolhive
- MCP reference servers: https://github.com/modelcontextprotocol/servers
