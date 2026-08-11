# Hero Passport — Architecture Decision Log

**Snapshot:** 2026-08-11  
**Current architecture:** v3

This document records decisions that implementation must not silently reverse. A superseded decision remains historically visible but is not normative.

---

## ADR-001 — C# 14 / .NET 10

**Status:** Accepted.  
Use C# 14 / .NET 10 as one stack for domain, CLI, MCP, persistence and later Blazor.

---

## ADR-002 — Modular monolith

**Status:** Accepted.  
One local application/storage core; no microservices, message bus or runtime module platform before a real distribution requirement exists.

---

## ADR-003 — Layer direction

**Status:** Accepted.

```text
Domain <- Application <- Infrastructure <- App
Web later -> Application; Infrastructure only at composition
```

Transport/UI/persistence types do not leak inward.

---

## ADR-004 — No separate Contracts assembly initially

**Status:** Accepted.  
Transport-neutral contracts live in Application. Extract a separately versioned Contracts/Client package only when a real independent .NET consumer exists.

---

## ADR-005 — Official MCP C# SDK

**Status:** Accepted.  
Use stable `ModelContextProtocol` 2.0.0 baseline; do not hand-roll MCP/JSON-RPC.

---

## ADR-006 — Four-tool MCP surface

**Status:** Accepted, revised by ADR-023.

Tool count remains exactly four in 0.1, but the v2 singular recovery tool has been replaced.

---

## ADR-007 — Explicit MCP registration

**Status:** Accepted.  
No assembly-wide tool discovery. Exact inventory is code-reviewable and snapshot-tested.

---

## ADR-008 — MCP is not CLI mirror

**Status:** Accepted.  
Administration, diagnostics, export and rich history stay CLI/Web unless a model-facing need is independently justified.

---

## ADR-009 — Deterministic RPG engine

**Status:** Accepted.  
Integer arithmetic, versioned rules, golden vectors; no LLM judge in MVP.

---

## ADR-010 — Presentation outside Domain/Application

**Status:** Accepted.  
App `HeroTextRenderer` owns RU/EN status text; typed data remains canonical inward.

---

## ADR-011 — SQLite + EF Core migrations

**Status:** Accepted.  
EF Core SQLite for local persistence/migrations; real SQLite integration tests; no product `EnsureCreated`.

---

## ADR-012 — Direct native SQLite bundle pin

**Status:** Accepted.  
Pin `SQLitePCLRaw.bundle_e_sqlite3` and verify actual runtime `sqlite_version()`.

---

## ADR-013 — Short synchronous SQLite operations

**Status:** Accepted.  
Microsoft.Data.Sqlite provides no true async I/O; use short synchronous DB segments, not `Task.Run` fake async.

---

## ADR-014 — `IDbContextFactory`

**Status:** Accepted.  
One short-lived DbContext per operation; no process/Blazor-circuit lifetime context.

---

## ADR-015 — WAL + FULL durability

**Status:** Accepted.  
Low write volume justifies prioritizing progression durability. Verify effective PRAGMAs.

---

## ADR-016 — EF migration locking only

**Status:** Accepted.  
Use EF provider migration locking (`__EFMigrationsLock` on SQLite); no parallel custom mutex system.

---

## ADR-017 — Platform-correct local data paths

**Status:** Accepted.  
Windows non-roaming LocalApplicationData, macOS Application Support, Linux XDG; `HERO_PASSPORT_HOME` for test isolation.

---

## ADR-018 — Dependency minimalism

**Status:** Accepted.  
No baseline MediatR, AutoMapper, Dapper, Polly, Serilog/NLog, Spectre.Console, OTel exporters or plugin/CQRS frameworks without measured need.

---

## ADR-019 — Codex as reference qualification host

**Status:** Accepted but reframed by ADR-022.  
Codex remains the first automated host E2E; it does not define Hero Passport semantics.

---

## ADR-020 — Agent evaluations

**Status:** Accepted.  
Unit/protocol tests are insufficient for model tool-selection behavior. Maintain host-neutral eval scenarios with Codex runner first.

---

## ADR-021 — `current_quest` and one-open-quest model

**Status:** Superseded by ADR-023.  
Architecture v2 used one active quest per hero/project and singular recovery. Multi-client analysis showed this creates artificial conflicts for parallel workstreams.

---

## ADR-022 — Universal semantics, host-specific binding

**Status:** Accepted v3.

Hero Passport standardizes:

```text
Domain semantics
Application commands/results/errors
HP-MCP contract
```

It does not standardize third-party config file syntax. Host integration differences are documentation/configuration adapters, not runtime business adapters.

Reason: Codex, VS Code, JetBrains, Zed, Cursor and Claude expose MCP through different config surfaces while sharing protocol concepts.

---

## ADR-023 — HP-MCP/2 and multiple active logical quests

**Status:** Accepted v3.

0.1 tools:

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

Allow multiple distinct open quests per hero/project. Repeated same logical work converges using versioned deterministic logical key. Active count is bounded to 16.

Reasons:

- multiple local agents may work in one repository;
- singular current quest is ambiguous;
- explicit list is better reconnect/handoff behavior;
- logical dedupe preserves retry safety and prevents duplicated XP.

This change happens before 0.1 public contract; no compatibility alias is required yet.

---

## ADR-024 — Unpinned MCP protocol negotiation

**Status:** Accepted v3.

Design against MCP `2026-07-28`, but leave ordinary `McpServerOptions.ProtocolVersion` null/unset.

Reason: official C# SDK v2 supports both current per-request metadata protocol and supported initialize-era revisions. Pinning `2026-07-28` would reject older initialize clients unnecessarily.

Compatibility tests cover 2026-07-28 and 2025-11-25.

---

## ADR-025 — Session-independent application state

**Status:** Accepted v3.

Application state is SQLite + explicit `questId`, never MCP session/connection identity.

For stdio 0.1 this is a semantic invariant. For future HTTP, set the C# SDK HTTP transport stateless mode explicitly.

Do not misuse `Stateless` as a generic cross-transport setting in code/docs.

---

## ADR-026 — Project-bound launch, no Roots dependency

**Status:** Accepted v3.

Project identity is resolved from local launch binding:

```text
--project-root or host cwd -> Git root/fallback -> fingerprint
```

MCP Roots are deprecated in 2026 and not consistently available. Do not put `workspacePath` into model-facing tools.

A global process without project binding is not guaranteed project-aware operation.

---

## ADR-027 — `HeroOperationContext`

**Status:** Accepted v3.

Application handlers receive resolved HeroId + ProjectId + InvocationOrigin. Client name/version is diagnostic metadata only and is not auth, hero identity or reward signal.

This isolates transport binding from business DTOs.

---

## ADR-028 — No second public API for hypothetical integrations

**Status:** Accepted v3.

Use:

```text
AI integrations -> MCP
shell/scripts -> CLI/--json
local Web -> Application
```

Do not add REST/GraphQL/gRPC without a concrete non-MCP consumer and dedicated security/versioning design.

---

## ADR-029 — Conservative MCP interoperability schema profile

**Status:** Accepted v3.

Use shallow object-root JSON Schema with closed properties, enums and bounds; avoid advanced combinators/external refs unless required.

Reason: protocol permits more than we need; portability benefits from a smaller shared subset.

---

## ADR-030 — Tool-list cache metadata is explicit but TTL is policy

**Status:** Accepted v3.

Static list uses public cache scope. Initial implementation policy may use 300000ms TTL. TTL is a freshness tuning constant, not HP-MCP semantic versioning.

Do not advertise dynamic list changes while inventory is static.

---

## ADR-031 — Streamable HTTP is trigger-based

**Status:** Accepted v3.

`ModelContextProtocol.AspNetCore` and own HTTP listener are deferred. Add only for a concrete URL-based deployment requirement.

When added:

```text
Streamable HTTP only
explicit stateless HTTP mode
project binding not inferred from server cwd
Origin/Host/auth security profile
```

Do not implement new legacy SSE.

---

## ADR-032 — Secure MCP Tunnel is an external private OpenAI path

**Status:** Accepted as documentation/deployment option.

OpenAI Secure MCP Tunnel can forward to local stdio. Therefore private OpenAI remote access does not force Hero Passport to ship HTTP in 0.1.

Tunnel credentials/permissions remain OpenAI configuration, not Hero Passport state.

---

## ADR-033 — MCP Registry is distribution metadata only

**Status:** Accepted/deferred publication.

Registry is preview at the snapshot date. Do not make runtime depend on it. If publication is chosen, use stable NuGet/package identity and Registry ownership rules.

---

## ADR-034 — Support tiers

**Status:** Accepted v3.

Host claims are:

```text
Qualified
Documented / protocol-compatible
Unsupported
```

Codex is first Qualified release host. Config documentation alone is not evidence of full support.

---

## ADR-035 — Contract snapshots from implementation

**Status:** Accepted v3.

Generate/commit MCP manifest/schema snapshots from actual SDK registration once implementation exists. Do not hand-maintain a duplicate schema source before generator exists.

Any snapshot change receives compatibility/privacy/eval review.

---

## Decision-change rule

A PR that changes a public contract, persistence invariant, privacy boundary, deployment trust model or deterministic rule must update this log and the corresponding normative specification in the same change.
