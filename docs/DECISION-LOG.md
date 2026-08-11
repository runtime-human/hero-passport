# Hero Passport — Architecture Decision Log

**Snapshot:** 2026-08-11  
**Current architecture:** v3.1

A superseded decision stays historically visible but is not normative. Detailed algorithms live in the corresponding deep-dive specification.

---

## ADR-001 — C# 14 / .NET 10

**Status:** Accepted.  
Use C# 14 / .NET 10 for Domain, Application, Infrastructure, CLI/MCP and later Blazor.

## ADR-002 — Modular monolith

**Status:** Accepted.  
One local application/state core. No microservices/message bus/runtime module platform without a real distribution requirement.

## ADR-003 — Layer direction

**Status:** Accepted.

```text
Domain <- Application <- Infrastructure <- App
Web later -> Application; Infrastructure only at composition
```

Transport/UI/persistence types do not leak inward.

## ADR-004 — No separate Contracts assembly initially

**Status:** Accepted.  
Transport-neutral contracts live in Application. Extract a separately versioned Client/Contracts package only for a real independent .NET consumer.

## ADR-005 — Official MCP C# SDK

**Status:** Accepted.  
Use stable official `ModelContextProtocol 2.0.0`; do not hand-roll MCP/JSON-RPC.

## ADR-006 — Four-tool MCP surface

**Status:** Accepted, revised by ADR-023.  
Tool count remains four in 0.1.

## ADR-007 — Explicit MCP registration

**Status:** Accepted.  
No assembly-wide discovery. Exact tool inventory is code-reviewable and snapshot-tested.

## ADR-008 — MCP is not a CLI mirror

**Status:** Accepted.  
Administration/diagnostics/export/rich history remain CLI/Web unless a model-facing need is independently justified.

## ADR-009 — Deterministic RPG engine

**Status:** Accepted.  
Integer/versioned rules/goldens; no LLM judge in MVP.

## ADR-010 — Presentation outside Domain/Application

**Status:** Accepted.  
App presentation owns RU/EN text. Typed values are authoritative.

## ADR-011 — SQLite + EF Core migrations

**Status:** Accepted.  
Real SQLite, EF migrations from day one, no product `EnsureCreated`.

## ADR-012 — Direct native SQLite bundle pin

**Status:** Accepted, strengthened by ADR-039.  
Pin `SQLitePCLRaw.bundle_e_sqlite3`; always verify actual loaded `sqlite_version()`.

## ADR-013 — Short synchronous SQLite segments

**Status:** Accepted.  
Microsoft.Data.Sqlite has no true SQLite async I/O; no `Task.Run` fake async.

## ADR-014 — `IDbContextFactory`

**Status:** Accepted.  
Short-lived context per unit of work.

## ADR-015 — WAL + FULL durability

**Status:** Accepted.  
Low write volume justifies stronger progression durability. Effective PRAGMAs are verified.

## ADR-016 — EF migration locking only

**Status:** Accepted.  
Use provider migration locking; no parallel custom migration mutex.

## ADR-017 — Platform-correct local data paths

**Status:** Accepted.  
Windows LocalApplicationData, macOS Application Support, Linux XDG; `HERO_PASSPORT_HOME` for isolated dev/tests.

## ADR-018 — Dependency minimalism

**Status:** Accepted.  
No baseline MediatR/AutoMapper/Dapper/Polly/Serilog/Spectre/OTel exporters/plugin/CQRS frameworks without measured need.

## ADR-019 — Codex reference qualification host

**Status:** Accepted, reframed by ADR-022.  
Codex is first automated host E2E, not the source of product semantics.

## ADR-020 — Agent evaluations

**Status:** Accepted.  
Deterministic tests do not prove LLM tool-selection behavior; maintain host-neutral eval scenarios.

## ADR-021 — Singular current quest / one-open-per-project

**Status:** Superseded by ADR-023.  
This architecture-v2 constraint caused artificial parallel-agent conflicts.

## ADR-022 — Universal semantics, host-specific binding/config

**Status:** Accepted v3.  
Standardize Domain/Application/HP-MCP semantics. Do not invent one universal third-party config file or host-specific runtime business adapters.

## ADR-023 — HP-MCP/2 multiple active quests

**Status:** Accepted v3, dedup wording refined by ADR-037.  
0.1 tools:

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

Multiple distinct active quests per hero/project, bounded at 16. Singular `current_quest` is removed before public release.

## ADR-024 — Unpinned MCP protocol negotiation

**Status:** Accepted v3.  
Design against `2026-07-28`; leave ordinary `McpServerOptions.ProtocolVersion` null/unset. Qualification includes 2026-07-28 and 2025-11-25 paths.

## ADR-025 — Session-independent Application state

**Status:** Accepted v3.  
SQLite + explicit `questId`, never MCP connection/session identity. Future HTTP configures stateless transport deliberately.

## ADR-026 — Project-bound launch, no MCP Roots dependency

**Status:** Accepted v3, replaced in detail by ADR-036.  
Project is local launch/application state; never routine `workspacePath` MCP input.

## ADR-027 — `HeroOperationContext`

**Status:** Accepted v3.  
Application receives HeroId + ProjectId + diagnostic InvocationOrigin. Client metadata is not auth/hero/reward identity.

## ADR-028 — No second public API for hypothetical integrations

**Status:** Accepted v3.

```text
AI hosts -> MCP
shell/scripts -> CLI/--json
local Web -> Application
```

REST/GraphQL/gRPC needs a concrete consumer and separate design.

## ADR-029 — Conservative MCP schema profile

**Status:** Accepted v3.  
Use shallow closed object schemas/enums/bounds instead of advanced JSON Schema features without need.

## ADR-030 — Tool-list cache metadata explicit; TTL policy only

**Status:** Accepted v3.  
Static local list can use public cache scope. TTL is tuning/freshness policy, not HP-MCP semantic versioning.

## ADR-031 — Streamable HTTP trigger-based

**Status:** Accepted v3.  
Own HTTP listener / `ModelContextProtocol.AspNetCore` deferred until a concrete URL deployment requirement. No new legacy SSE.

## ADR-032 — Secure MCP Tunnel external private OpenAI path

**Status:** Accepted deployment option.  
Private OpenAI remote access can forward to local stdio without forcing Hero Passport HTTP into 0.1.

## ADR-033 — MCP Registry distribution metadata only

**Status:** Accepted/deferred publication.  
No runtime dependency on preview Registry.

## ADR-034 — Host support tiers

**Status:** Accepted v3.

```text
Qualified
Documented/protocol-compatible
Unsupported/unknown
```

Config documentation alone is not qualification evidence.

## ADR-035 — Contract snapshots from implementation

**Status:** Accepted v3.  
Generate tool/schema/result snapshots from actual SDK registration; do not hand-maintain duplicate executable schemas.

---

## ADR-036 — `project-identity/1`: Git common-dir + explicit repo scope

**Status:** Accepted v3.1.  
**Details:** `PROJECT-IDENTITY.md`.

For a Git worktree:

```text
anchor = canonical absolute git-common-dir
scope  = . by default
scope  = explicit repo-relative --project-root subdirectory when deliberately selected
```

Consequences:

- linked worktrees share project identity;
- nested cwd does not split a repository;
- monorepo is one project by default;
- explicit monorepo subdirectory can be separate;
- submodule/nested repo is separate by default;
- no remote URL/branch/HEAD identity.

Reason: `$GIT_COMMON_DIR` is the standard Git metadata shared by linked worktrees; current filesystem cwd is not a stable product boundary.

---

## ADR-037 — `QuestDedupKeyV1`, not semantic LogicalQuestKey

**Status:** Accepted v3.1; supersedes the active part of ADR-023 referring to semantic logical-key matching.  
**Details:** `WIRE-CONTRACT.md`.

Retire before public release:

```text
LogicalQuestKeyV1
logical_key
logical_key_version
```

Use:

```text
QuestDedupKeyV1
dedup_key
dedup_key_version
```

Algorithm hashes canonical quest type plus SafeTextV1 normalized goal **with case preserved**.

Reason:

- natural-language hashing does not prove semantic identity;
- case folding can merge distinct code identifiers;
- retry deduplication needs conservative equality, not fuzzy semantics;
- handoff/recovery uses explicit active-quest listing/questId.

---

## ADR-038 — All read-modify-write DB use cases acquire writer intent first

**Status:** Accepted v3.1.  
**Details:** `PERSISTENCE-RELIABILITY.md`.

`StartQuest` and `FinishQuest` begin a non-deferred Serializable transaction before reading mutable invariants.

For selected Microsoft.Data.Sqlite 10.0.10 this is qualified as `BEGIN IMMEDIATE` behavior.

Reason:

- SQLite has one writer;
- early writer acquisition makes `count -> insert` and `check finished -> mutate` race reasoning simple;
- count=15 + two starts ends exactly at 16 without custom mutex;
- short local writes make serialization acceptable.

Provider upgrades must re-prove this behavior.

---

## ADR-039 — SQLite WAL runtime safety floor

**Status:** Accepted v3.1.  
**Details:** `PERSISTENCE-RELIABILITY.md`.

Normal supported WAL runtime requires:

```text
sqlite_version() >= 3.51.3
```

Reason: SQLite documents a rare WAL-reset corruption race through 3.51.2, fixed in 3.51.3+ (plus selected older backports not included in our primary qualification matrix).

Actual loaded native SQLite is tested per artifact; NuGet version alone is not proof.

---

## ADR-040 — Live DB backup uses SQLite backup API

**Status:** Accepted v3.1.  
**Details:** `PERSISTENCE-RELIABILITY.md`.

Never use raw `File.Copy` for an active database.

Use `SqliteConnection.BackupDatabase`, independently open/verify the destination, then publish it. Never manually delete/rename WAL/SHM for recovery.

Reason: WAL/hot journals are part of live SQLite state and naive file copying can produce an inconsistent backup.

---

## ADR-041 — HP-MCP structured success mirrors JSON into TextContent

**Status:** Accepted v3.1.  
**Details:** `WIRE-CONTRACT.md`.

Success:

```text
structuredContent = typed result
one TextContent = minified JSON semantically equal to structuredContent
displayText = human field inside result
```

Tool/business error:

```text
isError=true
one safe TextContent
no structuredContent
```

Reason: MCP `2026-07-28` recommends serialized JSON TextContent for backward compatibility when structured content is returned; structured errors would also complicate success output-schema conformance.

---

## ADR-042 — `hero.start_quest` idempotent hint is false

**Status:** Accepted v3.1; supersedes v3 annotation matrix.  
**Details:** `WIRE-CONTRACT.md`.

`start_quest` is retry/dedup-safe while a matching normalized declaration remains open, but:

```text
start(args)
finish
start(args)
```

legitimately creates a new quest. Therefore MCP `idempotentHint=true` would overstate behavior.

`finish_quest`, `list_active_quests` and `get_card` remain idempotent.

---

## ADR-043 — Explicit `SafeTextV1` runtime validation

**Status:** Accepted v3.1.  
**Details:** `WIRE-CONTRACT.md`.

`goal`/`summary` are Unicode-scalar validated, NFC-normalized, single-line whitespace-normalized, bounded and stripped of prohibited control/bidi-formatting characters before persistence.

Reason:

- model text is untrusted;
- `.NET string.Length` is UTF-16 code units, not the intended wire character metric;
- generated C# SDK/DataAnnotation schema does not enforce runtime validation;
- compact single-line persistence avoids terminal/log formatting hazards.

---

## ADR-044 — HP-MCP canonical IDs/time/numeric profile

**Status:** Accepted v3.1.  
**Details:** `WIRE-CONTRACT.md`.

```text
UUID       canonical lowercase UUIDv7
Timestamp  YYYY-MM-DDTHH:mm:ss.fffZ
JSON long-lived integer max 9_007_199_254_740_991
null       absent from current HP-MCP fields
```

Reason: deterministic snapshots and broad cross-language JSON interoperability.

---

## ADR-045 — MCP skill input is canonical-only and ordered

**Status:** Accepted v3.1.  
**Details:** `WIRE-CONTRACT.md` / `ENGINE-SPEC.md`.

HP-MCP `skillsUsed` accepts canonical keys only, 1..3, ordered primary->secondary->tertiary. This ordering drives v1 skill-XP weighting.

Aliases remain possible for human CLI/import adapters but are not advertised to the model.

---

## Decision-change rule

A PR changing a public contract, project identity, persistence invariant, privacy/deployment trust model or deterministic rule must update this log and its normative spec in the same change.
