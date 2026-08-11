# Hero Passport — Architecture

**Status:** Accepted architecture v3.1  
**Snapshot:** 2026-08-11  
**Target:** 0.1.0 portable local stdio MCP + CLI  
**Style:** modular monolith with explicit semantic/adapter/persistence boundaries

Deep-dive normative sources:

- [`PROJECT-IDENTITY.md`](PROJECT-IDENTITY.md)
- [`PERSISTENCE-RELIABILITY.md`](PERSISTENCE-RELIABILITY.md)
- [`WIRE-CONTRACT.md`](WIRE-CONTRACT.md)

---

## 1. Executive decision

Hero Passport is a **local deterministic RPG application with an MCP adapter**, not a Codex plugin, MCP gateway or universal agent platform.

```text
agent work
  -> explicit quest
  -> deterministic local rules
  -> durable SQLite progression
  -> typed status
```

MCP is the portable model-facing surface. CLI owns administration/diagnostics. Blazor becomes the visual local read surface in 0.2+.

---

## 2. Architectural priorities

In order:

1. correctness/determinism;
2. local privacy/data ownership;
3. persistence atomicity/crash safety;
4. protocol/wire correctness;
5. multi-agent concurrency safety;
6. tiny bounded agent context;
7. project identity consistency;
8. cross-host/platform interoperability;
9. testability/qualification;
10. upgrade/migration safety;
11. performance;
12. extensibility.

Extensibility remains last: new frameworks/transports/extensions need a demonstrated requirement.

---

## 3. Runtime architecture

```text
Codex / VS Code / JetBrains / Zed / Cursor / Claude / MCP host
                               |
                         stdio HP-MCP/2
                               |
                    +----------v----------+
                    | HeroPassport.App    |
                    | MCP / CLI / Present.|
                    +----------+----------+
                               |
                    +----------v----------+
                    | Application         |
                    | semantic use cases  |
                    +-----+----------+----+
                          |          |
                     Domain       Ports
                                     |
                              Infrastructure
                              EF/SQLite/Git/FS
                                     |
                                   SQLite
```

Later:

```text
Browser -> HeroPassport.Web -> Application/read models -> same local store
```

---

## 4. Project structure

0.1:

```text
src/
  HeroPassport.Domain/
  HeroPassport.Application/
  HeroPassport.Infrastructure/
  HeroPassport.App/

tests/
  HeroPassport.Domain.Tests/
  HeroPassport.Application.Tests/
  HeroPassport.Infrastructure.Tests/
  HeroPassport.App.Tests/
  HeroPassport.Architecture.Tests/
  HeroPassport.Contract.Tests/
  HeroPassport.AgentEvals/
```

0.2+:

```text
src/HeroPassport.Web/
```

No separate Contracts assembly until a real separately versioned .NET consumer exists.

---

## 5. Dependency direction

```text
Domain
  ^
Application
  ^
Infrastructure
  ^
App

Web -> Application
Web composition -> Infrastructure
```

Rules:

- Domain references no product project/infrastructure package.
- Application references Domain only.
- Infrastructure references Application + Domain.
- App references Application + Infrastructure + MCP/CLI hosting packages.
- Razor never references DbContext/Infrastructure types directly.

Architecture tests enforce these rules.

---

## 6. Domain boundary

Domain owns deterministic game policy:

```text
typed IDs/enums
reward/level rules
skills
Trust/Risk
traits
rule versions
pure invariant calculations
```

Domain does not own:

```text
EF/SQLite
Git/filesystem
MCP/CLI/HTTP
JSON serialization
localization
configuration
logging
DateTime.UtcNow
```

Time is supplied by Application from injected `TimeProvider`.

---

## 7. Application boundary

Canonical scoped use cases:

```text
StartQuest
FinishQuest
ListActiveQuests
GetHeroCard
```

Administration:

```text
InitializeApplication
GetDiagnostics
ExportData
```

Application receives a resolved:

```text
HeroOperationContext
  HeroId
  ProjectId
  InvocationOrigin
```

`InvocationOrigin` is diagnostics only. Client name/version cannot affect hero identity, auth, reward or game rules.

Application returns typed values/errors, never MCP SDK types or localized strings.

---

## 8. Adapter DTO boundary

MCP DTOs are deliberately smaller than Application commands.

```text
MCP start: questType + goal
        ↓
McpOperationContextResolver
        ↓
HeroOperationContext + typed StartQuestCommand
```

Stable local state is not repeated by the model:

```text
hero
project
locale
presentation
workspace path
```

CLI/Web have their own consumer-appropriate projections.

---

## 9. Presentation boundary

Application returns typed data.

```text
Typed result
  -> HeroTextRenderer
     compact RU/EN
     normal RU/EN
```

`displayText` is non-authoritative human presentation.

Web uses typed read models and never parses it.

Canonical RU terminology:

```text
scope_control -> Контроль
Clean scope bonus -> Бонус за контроль
Scope violation -> Выход за задачу
```

---

## 10. HP-MCP/2 adapter

Official C# SDK `ModelContextProtocol 2.0.0`.

Exact tools:

```text
StartQuestTool
FinishQuestTool
ListActiveQuestsTool
GetCardTool
```

Explicit registration only.

Pipeline:

```text
SDK request
 -> explicit WIRE-CONTRACT runtime validation
 -> context resolution
 -> Application handler
 -> typed success/error
 -> presentation
 -> exact CallToolResult
```

No EF/reward calculation in tool classes.

### Protocol

```text
preferred semantics 2026-07-28
ProtocolVersion unset/null
state independent of protocol sessions
```

Qualification covers 2026-07-28 and 2025-11-25.

### Success

```text
structuredContent = canonical result object
TextContent = minified JSON semantically equal to structuredContent
displayText = field inside result object
```

### Business/validation error

```text
isError=true
one safe TextContent
no structuredContent
```

### Annotations

```text
start      idempotent=false
finish     idempotent=true
list       idempotent=true
card       idempotent=true
```

The start operation is open-request retry-safe, not globally idempotent across a completed lifecycle.

---

## 11. SafeText and quest dedup architecture

Model-supplied goal/summary use `SafeTextV1` from `WIRE-CONTRACT.md` before persistence.

`LogicalQuestKeyV1` is retired before public release.

Use:

```text
QuestDedupKeyV1 = SHA-256(
  UTF8(questType + "\n" + SafeTextV1(goal))
)
```

Case is preserved.

This key means exact normalized retry declaration identity while open, not semantic natural-language equivalence.

Why:

- case-sensitive code identifiers must not collapse;
- fuzzy semantic matching creates false merges;
- handoff/restart already has explicit `list_active_quests` + `questId` recovery.

---

## 12. Multi-agent quest architecture

Multiple distinct open quests may coexist:

```text
Hero + Project
  ├── coding A
  ├── review B
  └── docs C
```

Application cap:

```text
16 open quests per hero/project
```

DB open uniqueness:

```text
(hero_id, project_id, dedup_key_version, dedup_key)
WHERE status='open'
```

A matching open start returns that quest. A different declaration creates a new one if cap permits. After a quest finishes, the same declaration can start a new quest.

---

## 13. Project identity architecture

`PROJECT-IDENTITY.md` is authoritative.

Launch input:

```text
explicit --project-root
else process cwd
```

Git-aware identity:

```text
Git anchor = canonical absolute git-common-dir
scope = . by default
scope = explicit repo-relative subdirectory only when --project-root intentionally selects it
```

Consequences:

- nested cwd in repo -> same project;
- linked worktrees -> same project;
- monorepo -> one project by default;
- explicit monorepo subproject -> separate scoped project;
- submodule/nested repo -> separate project;
- bare repo -> reject;
- Git trust failure -> reject, not standalone fallback;
- non-Git directory -> standalone path-based local identity.

Persist only display name, salted fingerprint and identity version. No full path/remote URL.

---

## 14. Hero binding

Default/active hero belongs to local product state.

Optional startup:

```text
--hero <selector>
```

binds a local process without adding routine hero selection to MCP calls.

No hard mapping between host brand and hero.

---

## 15. CLI architecture

System.CommandLine 2.0.10.

Initial commands:

```text
hero-passport init
hero-passport mcp [--project-root] [--hero]
hero-passport doctor
hero-passport card
hero-passport quest list --active
hero-passport export
hero-passport data path
hero-passport --version
```

CLI calls Application; it does not query DbContext directly.

`--json` exists only where scripts need stable machine output.

Hero Passport does not mutate third-party host config by default.

---

## 16. Persistence architecture

SQLite + EF Core with `IDbContextFactory`.

Operational state:

```text
WAL
synchronous=FULL
foreign_keys=ON
Default Timeout=5
Cache=Default
Pooling=True
```

Writable supported DB is local same-host filesystem.

Actual native SQLite is release/runtime-qualified with `sqlite_version()`; v3.1 supported WAL floor is `>=3.51.3`.

---

## 17. Write transaction architecture — resolved

All read-modify-write operations begin a short non-deferred Serializable transaction **before invariant reads**.

Selected Microsoft.Data.Sqlite 10.0.10 is qualified to translate this path to `BEGIN IMMEDIATE`.

### Start

```text
validate/context/dedup key outside DB
BEGIN writer
same-key lookup
active count
insert if <16
COMMIT
```

Count=15 + two distinct concurrent starts must end with exactly 16; one receives HP133.

### Finish

```text
BEGIN writer
load/context check
already-finished readback or
report + xp_event + hero + skills + traits + project + finished state
COMMIT
```

Concurrent finish yields one report/event/progression mutation; later caller returns persisted original result.

No custom writer mutex and no external retry framework.

---

## 18. Read architecture

Card/list use bounded read queries and no writer transaction.

If a later read needs several SQL statements from one snapshot, use a short read transaction only.

No long-lived read transaction, analytics scan or UI circuit holding SQLite state open.

---

## 19. Crash/recovery architecture

SQLite owns WAL/journal recovery.

Never manually delete/rename:

```text
-wal
-shm
rollback journals
```

Crash before commit -> no partial progression.

Crash after commit but before response -> retry using explicit `questId`, return persisted finished outcome.

Release tests use real child-process termination points.

---

## 20. Backup architecture

Logical export != physical backup.

Live backup uses SQLite/Microsoft.Data.Sqlite `BackupDatabase`, then independently verifies backup integrity/schema.

Raw `File.Copy` of an active SQLite database is forbidden.

Restore/replace requires a separate explicit workflow; do not overwrite an open DB file.

---

## 21. Migration architecture

EF migrations from day one; never `EnsureCreated` for product schema.

Use EF provider migration lock only. Do not create a second migration mutex.

Upgrade gates:

```text
empty -> latest
previous-release fixture -> latest
model snapshot/pending-change check
SQLite rebuild/destructive review
backup/recovery consideration
```

Doctor detects suspicious lock state; no blind deletion.

---

## 22. Error architecture

Application expected errors use stable HP codes.

New deep-dive families include:

```text
HP203 storage_full
HP204 storage_read_only
HP205 storage_io_error
HP206 database_corrupt
HP208 unsupported_sqlite_version
HP211 unsupported_storage_location
HP311 git_repository_unavailable
HP312 git_required_for_repository_binding
HP313 bare_repository_unsupported
```

Unexpected details stay in safe local diagnostics, not MCP text.

---

## 23. Privacy/security boundary

Never intentionally request/persist ordinary model-facing:

```text
source/file contents
diffs/patches
raw terminal/build/test logs
full prompts/chat history
API keys/secrets/tokens
environment dumps
full workspace paths
Git remote URLs
generic metadata/context bags
```

Git probing is local/read-only, shell-free and does not weaken `safe.directory` protections.

`questId` is an identifier, not a credential; finish validates bound HeroId+ProjectId.

---

## 24. Deferred MCP/platform features

Through 0.1:

```text
our own Streamable HTTP/OAuth
MCP Apps/Tasks
runtime plugins
REST/GraphQL/gRPC
cloud/team mode
source/diff ingestion
continuous telemetry
LLM judge
```

Resources/Prompts may be reconsidered only for a demonstrated model-facing value and may never replace the four-tool core lifecycle.

---

## 25. Deployment profiles

```text
0.1 local project-bound stdio
external private OpenAI Secure MCP Tunnel -> local stdio
future project-bound Streamable HTTP after concrete requirement
future public/multi-tenant service as separate auth/storage architecture
```

Do not treat HTTP as a transport flag over the local architecture.

---

## 26. Release qualification

0.1 requires evidence for:

```text
project identity worktree/monorepo/submodule vectors
SafeText/dedup/UUID/timestamp wire vectors
BEGIN IMMEDIATE provider behavior
start cap race -> exactly 16
finish race -> one event
crash before/after commit
live backup consistency
actual SQLite version floor
MCP 2026 + 2025 compatibility
structured == compatibility TextContent JSON
MCP Inspector
Codex E2E
cross-host RC smoke
```

Unit tests alone are not release qualification.

---

## 27. Extensibility rule

Adopt a new abstraction only when a real consumer/requirement exists.

Examples needing new ADR/design before implementation:

```text
HTTP authorization/tenancy
attempt/workstream model
project relink across repository moves
cross-machine shared state
fifth MCP tool
new rule version with historical implications
runtime plugin/module system
```

Modernity means precise current contracts and evidence, not maximum framework count.
