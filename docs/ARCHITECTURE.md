# Hero Passport — Architecture

**Status:** Accepted architecture v2  
**Baseline:** 2026-08-10  
**Target:** 0.1.0 local-first Codex-first MVP  
**Architecture style:** modular monolith with explicit ports/adapters at process, persistence and presentation boundaries

## 1. Executive decision

Hero Passport is a **local application with an MCP adapter**, not an MCP platform.

Its durable value is the stateful deterministic RPG model:

```text
agent task
  -> explicit quest
  -> deterministic reward
  -> durable local progression
  -> compact status
```

MCP is the preferred coding-agent integration because the `questId` state handle belongs naturally in an agent workflow. CLI is the preferred administration/diagnostics surface. Blazor becomes the local visual read surface after the core loop is stable.

The product remains one modular monolith and one authoritative local SQLite store through MVP.

---

## 2. Architectural priorities

In strict order:

1. **Correctness and determinism**
2. **Local privacy/data ownership**
3. **Idempotency and storage integrity**
4. **Protocol correctness**
5. **Tiny agent-context footprint**
6. **Clear code boundaries for agentic development**
7. **Cross-platform behavior**
8. **Testability/evaluability**
9. **Upgrade/migration safety**
10. **Performance**
11. **Extensibility**

Performance is intentionally below correctness because the workload is tiny. Extensibility is last because premature extension mechanisms are the easiest way to destroy the first ten properties.

---

## 3. High-level system

```text
                    +----------------------+
                    |  Codex / MCP client  |
                    +----------+-----------+
                               |
                         stdio MCP
                               |
                    +----------v-----------+
                    |  HeroPassport.App    |
                    | MCP / CLI / Present. |
                    +----------+-----------+
                               |
                    +----------v-----------+
                    | HeroPassport.Application
                    | use cases + ports     |
                    +----------+-----------+
                               |
                  +------------+------------+
                  |                         |
        +---------v---------+     +---------v-----------+
        | HeroPassport.Domain|     | Infrastructure      |
        | pure game policy   |     | EF/SQLite/filesystem|
        +--------------------+     +---------+-----------+
                                             |
                                      +------v------+
                                      |   SQLite    |
                                      +-------------+
```

Later:

```text
Browser -> HeroPassport.Web -> Application read models -> Infrastructure -> same SQLite
```

---

## 4. Project structure

### 4.1 MVP

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
  HeroPassport.AgentEvals/            # non-blocking/nightly/manual harness
```

### 4.2 Post-MVP

```text
src/
  HeroPassport.Web/
```

No `HeroPassport.Contracts` assembly initially. Transport-neutral Application DTOs already form a clean extraction seam if a separately versioned host/package later needs them.

No `Common`, `SharedKernel`, `BuildingBlocks` assembly until actual cross-project reuse appears.

---

## 5. Dependency graph

Allowed product references:

```text
Domain
  ^
  |
Application
  ^
  |
Infrastructure
  ^
  |
App

Web (later) -> Application
Web composition startup -> Infrastructure
```

More precisely:

- Domain references no product project and no infrastructure package.
- Application references Domain.
- Infrastructure references Application + Domain.
- App references Application + Infrastructure + MCP/CLI hosting packages.
- Web references Application; its composition root may reference Infrastructure.
- Razor components never reference Infrastructure types directly.

This is enforced by architecture tests.

---

## 6. Feature-first source organization

### Domain

```text
HeroPassport.Domain/
  Heroes/
  Projects/
  Quests/
  Rewards/
  Skills/
  Traits/
  Shared/
```

### Application

```text
HeroPassport.Application/
  Abstractions/
  Contracts/
  Heroes/
  Projects/
  Quests/
    StartQuest/
    FinishQuest/
    GetCurrentQuest/
  Cards/
    GetHeroCard/
  Initialization/
  Export/
  Diagnostics/
```

### Infrastructure

```text
HeroPassport.Infrastructure/
  Persistence/
    Entities/
    Configurations/
    Migrations/
    Stores/
    Queries/
  Paths/
  Projects/
  Configuration/
  Export/
  Diagnostics/
```

### App

```text
HeroPassport.App/
  Hosting/
  Cli/
  Mcp/
    Tools/
    HeroPassportMcpManifest.cs
  Presentation/
    HeroTextRenderer.cs
    Localization/
  Diagnostics/
```

Avoid generic folders such as `Helpers`, `Utils`, `Managers`, `Services` unless the name is genuinely domain-specific.

---

## 7. Domain responsibilities

Domain owns **only deterministic business policy and invariants**.

It contains:

- typed IDs/value objects/enums;
- hero and quest state transitions;
- reward calculation;
- level curve;
- skill normalization/distribution policy;
- quality flags;
- Trust/Risk calculation;
- trait progression;
- rule versions;
- invariant checks.

Domain does not contain:

```text
EF Core
SQLite
MCP attributes/SDK types
CLI types
JSON serialization annotations
console output
localized display text
filesystem paths
configuration readers
logging
DateTime.UtcNow
```

All score calculations use integer arithmetic.

### 7.1 Time

Domain operations receive required timestamps or a value already resolved by Application. Application uses injected .NET `TimeProvider`.

Do not create a custom `IClock` while `TimeProvider` satisfies the requirement.

### 7.2 IDs

Generated identifiers use built-in UUIDv7 (`Guid.CreateVersion7()`). JSON external form is canonical lowercase GUID text unless a future explicit contract revision changes it.

---

## 8. Application responsibilities

Application owns use cases and product ports.

Canonical command/query handlers:

```text
StartQuestHandler
FinishQuestHandler
GetCurrentQuestHandler
GetHeroCardHandler
InitializeApplicationHandler
GetDiagnosticsHandler
ExportDataHandler
```

Application:

- coordinates Domain;
- validates use-case semantics;
- resolves active hero/project through ports;
- defines transaction intent;
- returns typed use-case results/read models;
- never returns an MCP SDK type;
- never renders localized text.

### 8.1 Explicit result model

Expected business failures should not require exceptions for ordinary control flow.

Conceptual shape:

```csharp
public sealed record HeroError(string Code, string Message);

public readonly record struct HeroResult<T>(T? Value, HeroError? Error)
{
    public bool IsSuccess => Error is null;
}
```

The exact implementation can be refined during Task 2, but the semantics are fixed:

- expected validation/not-found/conflict -> typed failure;
- infrastructure exceptions -> translated at boundary to stable HP2xx/HP9xx errors;
- programmer defects are not swallowed as “validation”.

Do not add a third-party Result library for this tiny shape.

### 8.2 Application ports

Ports are capability-specific, for example:

```text
IHeroStore
IQuestStore
IProjectStore
IHeroReadStore
IProjectIdentityResolver
IActiveHeroProvider
IAppDataPaths
IApplicationInitializer
```

Avoid `IRepository<T>`.

---

## 9. Presentation boundary

This is a deliberate correction from architecture v1.

**Domain and Application do not own `displayText`.**

Typed result:

```text
FinishQuestResult
  reward
  hero projection
  skill changes
  trait changes
```

Presentation adapter:

```text
HeroTextRenderer
  -> compact RU
  -> compact EN
  -> normal RU/EN
```

Benefits:

- rules are locale-independent;
- MCP and CLI can share rendering without contaminating use cases;
- Blazor can ignore text renderer and use typed data directly;
- golden RPG tests do not fail because punctuation changed;
- presentation goldens can be tested separately;
- token budgets can be enforced at App boundary.

Canonical RU terminology remains:

```text
scope_control        -> Контроль
Clean scope bonus    -> Бонус за контроль
Scope violation      -> Выход за задачу
```

---

## 10. MCP adapter architecture

MVP uses official `ModelContextProtocol` 2.0.0 and stdio.

### 10.1 Explicit tool registration

Exactly four dedicated adapter types:

```text
StartQuestTool
FinishQuestTool
CurrentQuestTool
GetCardTool
```

Register explicitly in the composition root through official SDK generic/type registration APIs.

Do **not** use assembly-wide discovery such as a catch-all `WithToolsFromAssembly()` path.

Reasons:

- fixed inventory is visible in code review;
- no accidental public tool exposure;
- deterministic `tools/list` ordering is straightforward;
- startup/tool-manifest tests can compare exact inventory;
- less reflection/dynamic discovery pressure;
- future trimming/AOT experimentation remains easier.

### 10.2 Thin adapters

Each tool:

```text
SDK input DTO
 -> boundary validation/mapping
 -> Application handler
 -> Presentation renderer
 -> typed MCP result
```

No EF query in tool classes.
No reward calculation in tool classes.
No static mutable server session.

### 10.3 Server instructions

Static concise server-wide workflow instructions are registered where supported by the SDK/host path. For Codex, first 512 characters carry complete essential workflow/privacy guidance.

### 10.4 MCP features deliberately not used

```text
HTTP transport
OAuth
MCP Apps
Tasks
resources
prompts
roots
sampling
MCP logging
subscriptions
server-initiated workflow state
```

MCP 2026-07-28's stateless model strengthens the explicit `questId` design.

---

## 11. CLI architecture

System.CommandLine 2.0.10.

CLI is an adapter over Application, not a second implementation.

Initial command families:

```text
hero-passport init
hero-passport mcp
hero-passport doctor
hero-passport card
hero-passport quest current
hero-passport export
hero-passport data path
```

Existing/product roadmap may add agent/project management commands incrementally, but destructive/reset operations are not needed to prove the first MCP loop.

CLI rules:

- human concise stdout by default;
- `--json` only on script-relevant commands;
- diagnostics/errors to stderr;
- no decorative library dependency in MVP;
- MCP mode bypasses normal CLI output completely.

---

## 12. Infrastructure persistence architecture

EF Core SQLite 10.0.10 + explicitly pinned SQLitePCLRaw bundle.

### 12.1 DbContext lifetime

Use `IDbContextFactory<HeroPassportDbContext>`.

One command/query -> one context -> dispose.

Why:

- stdio/CLI are not HTTP request scopes;
- future Blazor circuits must not share DbContext;
- Microsoft.Data.Sqlite objects are not thread-safe;
- unit-of-work lifetime stays obvious.

No globally scoped/long-lived DbContext.

### 12.2 Synchronous SQLite I/O

Microsoft.Data.Sqlite documents that SQLite does not support asynchronous I/O and its async ADO.NET methods execute synchronously.

Therefore persistence implementation uses synchronous SQLite/EF calls for actual database work.

This is not permission to block for long tasks: transactions/queries must be short and bounded.

MCP/Application entrypoints may still be asynchronous where required by SDK composition, but the database segment is explicitly synchronous.

Do not add `Task.Run` around SQLite calls; that merely moves blocking work to another thread and complicates cancellation/transactions.

### 12.3 Connection policy

Build connection strings via `SqliteConnectionStringBuilder`.

Baseline:

```text
Mode=ReadWriteCreate
Cache=Default
Foreign Keys=True
Pooling=True
Default Timeout=5
```

`Cache=Shared` is not used with WAL; Microsoft documentation discourages the combination.

### 12.4 Journal/durability

Required operational state:

```text
PRAGMA journal_mode = WAL;
PRAGMA synchronous = FULL;
PRAGMA foreign_keys = ON;
```

`FULL` trades some commit throughput for power-loss durability. Hero Passport writes are tiny/low-frequency; preserving earned progression is more valuable than maximizing writes/sec.

PRAGMA state is verified in Infrastructure tests and `doctor`.

### 12.5 Busy behavior

Microsoft.Data.Sqlite retries busy/locked operations until command timeout. Application does not stack a Polly retry policy on top.

After configured timeout, translate relevant SQLite busy/locked errors to:

```text
HP202 database_busy
```

with remediation guidance, no raw SQL/path leakage.

---

## 13. Transaction model

No transaction spans agent work.

### Start

Short transaction:

```text
resolve active state
validate active-quest uniqueness
insert/open quest or return matching existing quest
commit
```

### Finish

One short atomic transaction:

```text
load quest + hero + project state
if finished -> read persisted original outcome and exit
normalize skills
calculate reward in memory
calculate Trust/Risk/traits in memory
insert quest report
insert unique XP event
update hero totals
update skills
update traits
update project projection
mark quest completed
commit
```

Database uniqueness constraints are the final idempotency barrier.

If any write fails, none of the progression mutation commits.

---

## 14. Migration architecture

EF migrations from the first schema. Never use `EnsureCreated` as product schema management.

### 14.1 No custom migration lock

Important correction: EF Core 9+ already protects migrations with a database-wide lock. SQLite uses `__EFMigrationsLock`.

Do not introduce a second file mutex/lock table around migrations.

### 14.2 Abandoned lock handling

A process killed during migration can leave the SQLite migration lock abandoned.

Policy:

- normal startup does not blindly delete it;
- `doctor` detects suspicious condition/version/migration state;
- recovery is explicit and documented;
- migration CI verifies pending-model state before release.

### 14.3 Migration review

Every migration PR includes:

```text
migration code
model snapshot diff
upgrade test from previous released DB
fresh database test
backup/restore consideration
SQLite rebuild-operation review
```

Destructive migration is not allowed in MVP without explicit ADR/data migration plan.

---

## 15. Data-path architecture

Paths are platform-correct; see `CONFIGURATION.md`.

Baseline:

```text
Windows: %LOCALAPPDATA%\HeroPassport
macOS:   ~/Library/Application Support/HeroPassport
Linux:   XDG_DATA_HOME / XDG_CONFIG_HOME / XDG_STATE_HOME
```

Tests/dev can use:

```text
HERO_PASSPORT_HOME
```

No full application/workspace path appears in MCP contracts.

---

## 16. Project identity

Resolve project locally:

```text
Git repository root
  else process working directory
```

Persist:

```text
project_id
project_display_name
workspace_fingerprint
identity_version
```

Fingerprint is a privacy-oriented identity key, not a cryptographic access credential.

Do not persist absolute workspace path by default.

Codex can set an MCP stdio server's `cwd`; that is the host-owned mechanism when project pinning is required.

---

## 17. Configuration architecture

Config and product state remain separate.

`config.json` v1 contains presentation/diagnostics preference only.

Application state such as active hero lives in SQLite.

Precedence:

```text
explicit CLI option
> documented HERO_PASSPORT_* env
> config.json
> defaults
```

Unknown config fields are rejected.

No generic “configuration dictionary” is propagated into Domain/Application.

---

## 18. Error architecture

Three categories.

### Expected application errors

Typed stable HP codes:

```text
HP100..HP199 contract/domain/use-case
HP200..HP299 persistence/filesystem
HP300..HP399 configuration
```

### Unexpected defects

```text
HP900 internal_error
```

Unexpected exception details are diagnostic-only.

### Mapping rule

```text
Domain/Application expected failure
 -> HeroResult failure
 -> MCP isError / CLI nonzero exit

Infrastructure exception
 -> infrastructure translator
 -> stable HP2xx or HP900

Protocol/framing error
 -> official MCP SDK protocol handling
```

Do not expose exception type names as API contract.

---

## 19. Logging and observability

Use built-in .NET logging abstractions.

MCP stdio invariant:

```text
stdout = protocol bytes only
stderr = diagnostics
```

By default do not log:

```text
goal
summary
workspace path
MCP argument bodies
environment variables
SQL parameter values
```

Safe diagnostic fields:

```text
error code
questId
hero/project opaque IDs
rule versions
migration ID
operation name
duration
SQLite error code (when useful)
```

OpenTelemetry exporters are post-MVP. No remote telemetry by default.

---

## 20. Security boundary

The server has no reason to read source files or the full process environment.

MVP process needs:

```text
read/write Hero Passport app-data dirs
read cwd/Git metadata enough to resolve project root/name
stdio to parent MCP client
```

It does not need:

```text
network access
API secrets
repo file contents
shell execution
child process execution for core workflow
browser access
system-wide write permissions
```

If Git project identity can be determined without invoking an external `git` process (e.g. directory discovery), prefer that. If a `git` process is used later, arguments/output are tightly bounded and documented.

---

## 21. Read-model architecture

Purpose-built read models:

```text
HeroCardReadModel
CurrentQuestReadModel
RecentQuestReadModel
ProjectStatsReadModel
DiagnosticsReadModel
DashboardSnapshotReadModel (0.2)
```

Infrastructure projects directly from EF queries with no tracking when mutation is not needed.

No EF entity crosses into MCP/CLI/Web.

No generic “return all entities” APIs.

---

## 22. Dashboard architecture (0.2)

Blazor Web App over the same Application/read-model layer.

For server-side Blazor, Microsoft recommends short-lived DbContext patterns because scoped contexts can be shared across a circuit and DbContext is not thread-safe. The already-selected `IDbContextFactory` model fits this naturally.

Dashboard v1 is local read-focused:

```text
hero card
XP progress
trust/risk
skills
traits
last reward
recent quests
project stats
```

No auth/cloud/team mode in 0.2 unless scope is deliberately changed.

---

## 23. MCP vs CLI vs Web capability policy

| Capability | MCP | CLI | Web later |
|---|---:|---:|---:|
| start/finish quest | yes | optional/manual | optional |
| current quest | yes | yes | yes |
| hero card | yes | yes | yes |
| doctor | no | **yes** | maybe summary |
| export | no | **yes** | later |
| data path | no | **yes** | no |
| full history | no | yes/paged later | **yes** |
| reset/delete | no | explicit CLI later | maybe guarded |
| configuration | no | yes | later |

This prevents “MCP API sprawl”.

---

## 24. Compatibility/version dimensions

Keep separate:

```text
application version              0.x.y
MCP protocol revision            2026-07-28
Hero MCP contract                HP-MCP/1
config schema                    1
EF migration history             migration IDs
reward rule version              e.g. reward/1.0.0
trust-risk rule version          trust-risk/1.0.0
trait rule version               traits/1.0.0
project identity version         identity/1
export schema version            export/1 (when introduced)
```

Do not use one global `schemaVersion` field to mean all of these things.

---

## 25. Testing architecture

### Deterministic tests

```text
Domain: formulas/invariants/goldens
Application: handler lifecycle/idempotency with fakes
Infrastructure: real temp-file SQLite
App: CLI/MCP process tests
Architecture: dependency/privacy/tool manifest
```

### Protocol tests

Advertise real server and inspect:

```text
names
order
descriptions
input/output schemas
annotations
taskSupport
actual structured results
stdout framing
```

### Agent evals

A separate harness validates behavioral use:

```text
does Codex start/finish at correct time?
does it avoid forbidden fields?
does tool-description change increase mistakes?
does it recover an active quest?
```

Agent evals are evidence for UX/tool-design changes, not a replacement for deterministic release gates.

---

## 26. Architecture fitness functions

Build/test should fail if practical when:

- Domain references EF/MCP/CLI/ASP.NET.
- Application references MCP SDK.
- App MCP tool count != 4.
- MCP tool order differs from manifest.
- assembly-wide MCP scanning is introduced.
- a tool input has `additionalProperties != false`.
- a tool gains arbitrary `metadata/context/payload` bag.
- an MCP DTO contains path/code/diff/log/secret fields.
- a Razor component references DbContext.
- a DbContext is registered/used as a long-lived singleton.
- persistence tests use EF InMemory instead of SQLite.
- MCP process emits non-protocol stdout.
- reward fixture changes without rule-version review.
- dependencies are versioned outside CPM.
- locked restore or audit fails in release CI.

---

## 27. Rejected architecture alternatives

### Microservices

Rejected. No network boundary, scale dimension or ownership split justifies them.

### Runtime plugin system

Rejected before post-MVP. It complicates security, migration, packaging and deterministic rules.

### Event bus/event sourcing framework

Rejected. The XP event ledger is an audit/integrity table, not a reason to make the application event-sourced.

### CQRS framework

Rejected. We use conceptually separate commands/read models without a framework.

### DDD aggregate ceremony everywhere

Rejected. Use domain concepts where they clarify invariants; do not create factories/specifications/domain events solely to match a template.

### MCP gateway

Rejected. Hero Passport is one server with four tools.

### HTTP MCP from day one

Rejected. Remote auth/network/security provides no MVP value.

---

## 28. Cheap future seams retained

Keep only low-cost seams:

- rule version objects/constants;
- skill normalizer;
- quality flags;
- typed Application result/error;
- storage ports;
- `IDbContextFactory`;
- read models;
- app-data path resolver;
- config version;
- MCP manifest;
- presentation renderer interface/implementation boundary;
- goldens/evals.

Deferred until a real feature requires them:

```text
feature flag system
module registry
plugin ABI
remote transport abstraction
cloud persistence abstraction
message bus
achievements module
artifacts/items module
MCP resources/prompts/apps/tasks
```

---

## 29. Architecture success criterion

A coding agent implementing `FinishQuest` should be able to load roughly:

```text
ENGINE-SPEC.md
MCP-CONTRACT.md relevant section
Application/Quests/FinishQuest/*
Domain/Rewards/*
Infrastructure store/transaction files
matching tests
```

without needing to understand the dashboard, CLI formatting, all EF entities, remote MCP, auth or plugin infrastructure.

If ordinary feature work requires loading the whole repository to understand side effects, the architecture has failed.

## 30. Related documents

- `ECOSYSTEM-BENCHMARK.md`
- `DEPENDENCIES.md`
- `MCP-CONTRACT.md`
- `DATA-MODEL.md`
- `CONFIGURATION.md`
- `SECURITY-PRIVACY.md`
- `TESTING-QUALITY.md`
- `ENGINE-SPEC.md`
- `ROADMAP.md`
- `DECISION-LOG.md`

## 31. Primary references

See `REFERENCES.md`. Critical architectural sources include current MCP 2026-07-28, official MCP C# SDK 2.0 docs, OpenAI Codex MCP/config docs, .NET/EF Core/Microsoft.Data.Sqlite docs, SQLite WAL/PRAGMA docs, and the source repositories analyzed in `ECOSYSTEM-BENCHMARK.md`.
