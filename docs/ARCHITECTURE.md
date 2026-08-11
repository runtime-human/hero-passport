# Hero Passport — Architecture

**Status:** Accepted architecture v3  
**Snapshot:** 2026-08-11  
**Target:** 0.1.0 Portable Local MCP Core  
**Style:** modular monolith with transport-neutral application semantics and thin adapters

## 1. Executive decision

Hero Passport is a **local RPG application with MCP/CLI adapters**, not an MCP platform and not a Codex plugin.

The durable product core is:

```text
logical agent work
  -> explicit quest
  -> deterministic reward
  -> durable local progression
  -> portable typed results
```

MCP is the preferred agent integration. Codex is the first qualified reference host. Other compatible hosts use the same HP-MCP/2 semantics without host-specific business code.

---

## 2. Architectural priorities

In order:

1. correctness/determinism;
2. idempotency/data integrity;
3. local privacy;
4. portable protocol semantics;
5. small model context footprint;
6. explicit project/hero binding;
7. testability/evaluability;
8. cross-platform behavior;
9. migration/release safety;
10. performance;
11. extensibility.

Extensibility remains last: universal abstractions are allowed only where multiple concrete consumers already prove the boundary.

---

## 3. System topology

```text
                 MCP-capable hosts
      Codex / VS Code / JetBrains / Zed / ...
                         |
                    MCP stdio
                         |
              +----------v----------+
              |   HeroPassport.App  |
              | MCP / CLI / Present |
              +----------+----------+
                         |
              +----------v-----------+
              | HeroPassport.Application
              | commands/queries/ports |
              +-----+-------------+---+
                    |             |
          +---------v----+  +-----v----------------+
          | Domain       |  | Infrastructure       |
          | game policy  |  | EF/SQLite/config/fs  |
          +--------------+  +----------+------------+
                                       |
                                    SQLite
```

Later:

```text
Browser -> HeroPassport.Web -> Application -> Infrastructure -> same SQLite
```

Future URL-based deployment:

```text
MCP host -> Streamable HTTP adapter -> same Application
```

HTTP does not become a second product core.

---

## 4. Project structure

0.1.0:

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

No standalone Contracts assembly initially. `HeroPassport.Application.Contracts` remains transport-neutral and is the extraction seam if a real external .NET consumer later justifies separate version ownership.

---

## 5. Dependency graph

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

Web -> Application
Web composition -> Infrastructure
```

Rules:

- Domain references no other product project.
- Application references Domain only.
- Infrastructure references Application + Domain.
- App references Application + Infrastructure and adapter packages.
- Web components do not reference DbContext/Infrastructure directly.
- Contract tests inspect project/namespace boundaries.

---

## 6. Feature-first organization

Domain:

```text
Heroes/
Projects/
Quests/
Rewards/
Skills/
Traits/
Shared/
```

Application:

```text
Abstractions/
Contracts/
Context/
Heroes/
Projects/
Quests/
  StartQuest/
  FinishQuest/
  ListActiveQuests/
Cards/GetHeroCard/
Initialization/
Diagnostics/
Export/
```

Infrastructure:

```text
Persistence/
  Entities/
  Configurations/
  Migrations/
  Stores/
  Queries/
Paths/
Projects/
Configuration/
Diagnostics/
Export/
```

App:

```text
Hosting/
Cli/
Mcp/
  Tools/
  HeroPassportMcpManifest.cs
  McpOperationContextResolver.cs
Presentation/
  HeroTextRenderer.cs
  Localization/
Diagnostics/
```

Avoid `Helpers`, `Utils`, `Managers`, generic `Repository<T>`.

---

## 7. Domain boundary

Domain owns:

```text
typed IDs/value objects
quest/result keys
quest state transitions
logical-key canonicalization policy/value type boundary
reward/levels
quality flags
skills
Trust/Risk
traits
rule versions
```

Domain never owns:

```text
EF/SQLite
MCP/JSON-RPC
CLI/HTTP
filesystem/config
localized text
logging
DateTime.UtcNow
client/host identities
```

Use integer score arithmetic. Time/IDs are passed in or provided by Application via built-in `TimeProvider` and UUIDv7 generation policy.

---

## 8. Application boundary

Application owns semantic use cases:

```text
StartQuestHandler
FinishQuestHandler
ListActiveQuestsHandler
GetHeroCardHandler
InitializeApplicationHandler
GetDiagnosticsHandler
ExportDataHandler
```

It also owns ports and context types:

```text
HeroOperationContext
IHeroStore
IQuestStore
IProjectStore
IHeroReadStore
IProjectBindingResolver
IHeroBindingResolver
IApplicationInitializer
IAppDataPaths
```

### 8.1 `HeroOperationContext`

Each scoped command/query receives resolved:

```text
HeroId
ProjectId
InvocationOrigin
```

InvocationOrigin is diagnostic adapter context only. It is never a reward/auth signal.

### 8.2 Expected failures

Use a small in-house typed result/error representation with stable HP codes. Do not add a third-party Result library merely to wrap four use cases.

---

## 9. Presentation boundary

Application returns typed data. App presentation renders RU/EN compact/normal text.

```text
FinishQuestResult
    ↓
HeroTextRenderer
    ├── compact RU
    ├── compact EN
    └── normal RU/EN
```

Blazor later consumes typed read models directly.

Canonical RU labels remain:

```text
scope_control -> Контроль
Clean scope bonus -> Бонус за контроль
Scope violation -> Выход за задачу
```

---

## 10. MCP adapter

Official `ModelContextProtocol 2.0.0`.

Exact types:

```text
StartQuestTool
FinishQuestTool
ListActiveQuestsTool
GetCardTool
```

Register explicitly. No assembly-wide scanning.

Adapter pipeline:

```text
SDK request
 -> strict DTO/schema
 -> McpOperationContextResolver
 -> Application handler
 -> typed result/error
 -> Presentation renderer
 -> MCP structured/text result
```

No EF/reward logic in tools.

### 10.1 Protocol compatibility

Design to 2026 semantics while leaving `McpServerOptions.ProtocolVersion` unset. This allows official SDK negotiation with supported older protocol revisions.

Application state is explicit and session-independent.

For stdio 0.1, do not introduce transport-session state.

For future Streamable HTTP, configure the C# SDK HTTP transport stateless mode explicitly.

### 10.2 Feature usage

Required: Tools only.

Deferred/unsupported core dependencies:

```text
Resources
Prompts
Roots (deprecated)
Sampling (deprecated)
MCP Logging (deprecated)
MRTR
Tasks
Apps
subscriptions
```

---

## 11. Multi-agent quest architecture

Architecture v2 allowed one open quest per hero/project. That is superseded.

v3 supports:

```text
Hero + Project
  ├── coding quest A
  ├── review quest B
  └── docs quest C
```

Bounded application cap: 16 active quests per hero/project.

### 11.1 Same logical work convergence

`LogicalQuestKeyV1` is derived deterministically from canonical quest type + canonicalized goal. It is persisted with a version.

Database enforces one open quest per:

```text
(hero_id, project_id, logical_key_version, logical_key)
WHERE status='open'
```

Thus concurrent identical starts converge without a distributed lock.

Different logical keys coexist.

### 11.2 Why no model-supplied idempotency key

A random/request key would require the model/host to manage infrastructure state and would not dedupe semantically duplicated starts across different clients. For 0.1, logical work convergence better matches the RPG model.

---

## 12. Project binding architecture

Project binding is not MCP business input.

Local stdio starting context:

```text
explicit --project-root
else process cwd
```

Infrastructure normalizes to Git root when present and produces persisted fingerprint/display name.

Reasons for explicit startup fallback:

- Codex supports stdio `cwd`;
- VS Code supports stdio `cwd` and workspace variables;
- JetBrains exposes Working directory/project scope;
- other hosts have different config shapes;
- MCP Roots are deprecated and cannot be the universal answer.

A host with one global process and no project-scoped binding is outside the 0.1 project-aware profile. We document the limitation rather than infer path from model text.

---

## 13. Hero binding architecture

Default active hero lives in product state. Optional local startup `--hero` pins the server adapter to a hero without exposing hero selection to every model call.

No hard mapping between client brands and heroes.

---

## 14. CLI architecture

System.CommandLine 2.0.10; CLI is an Application adapter.

```text
init
mcp [--project-root] [--hero]
doctor
card
quest list --active
export
data path
--version
```

`--json` exists only for commands with a script-consumer reason.

CLI does not mutate host application config by default.

A later `integration show <host>` command may print snippets from internal descriptors, but it remains presentation/documentation functionality.

---

## 15. Persistence architecture

EF Core SQLite 10.0.10 + SQLitePCLRaw bundle baseline.

Use `IDbContextFactory<HeroPassportDbContext>` and one short-lived context/unit of work.

Actual SQLite work is short and synchronous because Microsoft.Data.Sqlite does not provide true async I/O. Do not hide it in `Task.Run`.

Connection policy:

```text
ReadWriteCreate
Cache=Default
Foreign Keys=True
Pooling=True
Default Timeout=5 seconds
```

Operational PRAGMAs:

```sql
PRAGMA journal_mode=WAL;
PRAGMA synchronous=FULL;
PRAGMA foreign_keys=ON;
```

---

## 16. Transactions and races

No transaction spans agent work.

### Start transaction

```text
resolve context
compute logical key
lookup match
check count
insert open quest
commit
```

If concurrent insert hits logical-key uniqueness, reload and return the winning open quest as `alreadyOpen=true`.

If count cap races, Application/DB tests ensure the policy cannot explode unboundedly; a tiny transient overshoot must not be silently accepted if the chosen implementation claims a hard cap. Prefer serialization at write transaction/constraint logic where practical.

### Finish transaction

```text
load quest
validate bound context
if finished -> return persisted result
calculate deterministic changes
insert report
insert UNIQUE xp_event
update aggregates/skills/traits/project stats
mark quest finished
commit
```

Concurrent finish uniqueness is the final barrier.

---

## 17. Migration architecture

EF migrations from schema 0001. Never `EnsureCreated` for product persistence.

EF Core owns migration locking; SQLite provider uses `__EFMigrationsLock`. Do not add a second lock system.

Doctor diagnoses migration state; it does not blindly delete locks.

Every migration gets fresh-DB and previous-release upgrade tests.

---

## 18. Configuration and paths

Platform roots remain:

```text
Windows  %LOCALAPPDATA%\HeroPassport
macOS    ~/Library/Application Support/HeroPassport
Linux    XDG data/config/state roots
```

`HERO_PASSPORT_HOME` isolates tests/dev.

`config.json` stores local product preferences, not Codex/VS Code/Cursor configuration.

Configuration precedence:

```text
explicit CLI/startup option
> HERO_PASSPORT_* env
> config.json
> defaults
```

Project/hero startup binding is part of adapter resolution, not Domain.

---

## 19. Deployment profiles

### Local stdio — 0.1

One OS user, local SQLite, project-bound process. Primary profile.

### Local/private Streamable HTTP — future trigger

Same Application core, but explicit project binding and HTTP transport/security policy required. Never infer project from server cwd for a multi-project endpoint.

### Private OpenAI tunnel — external mechanism

OpenAI Secure MCP Tunnel can reach the local stdio process without Hero Passport exposing public inbound HTTP.

### Public/multi-tenant HTTP — separate phase

Requires authenticated principal, authorization of hero/project, tenant isolation, remote storage/backups/rate limits. Not an adapter toggle.

See `DEPLOYMENT-MODES.md`.

---

## 20. No second public API by default

Do not add REST, GraphQL or gRPC solely for “integration completeness”.

```text
AI host              -> MCP
shell/automation     -> CLI / --json
Hero Passport Web    -> Application
future remote agent  -> Streamable HTTP MCP
```

A separate public service API needs a real consumer and ADR.

---

## 21. Observability

Use `Microsoft.Extensions.Logging` and `System.Diagnostics` seams, local/off by default.

MCP stdout is protocol only. Diagnostics go stderr/local file when enabled.

Do not log request/response bodies, goal/summary by default, local paths, environment or client auth material.

InvocationOrigin may be logged in bounded normalized form for interop debugging but is not persisted product history by default.

---

## 22. Dependency policy

Approved baseline remains in `DEPENDENCIES.md`.

`ModelContextProtocol.AspNetCore` is deferred until own Streamable HTTP transport is implemented.

No MediatR/AutoMapper/Dapper/Polly/Serilog/OpenTelemetry exporter baseline without measured need.

---

## 23. Architecture fitness functions

Build/test must detect:

```text
forbidden project references
MCP tool inventory/order drift
current_quest stale contract reintroduction
advanced/unapproved schema constructs
forbidden privacy fields
ProtocolVersion accidental hard pin
session-dependent application state
workspacePath in MCP DTOs
raw clientInfo used for hero/auth/reward
one-open-quest-per-project stale constraint
missing logical key version/index
XP duplicate race
DbContext leakage to MCP/Web
non-protocol MCP stdout
```

---

## 24. Deferred seams

Keep only cheap useful seams:

```text
rule versions
logical quest key version
project identity version
SkillKeyNormalizer
QuestQualityFlags
read models
operation-context resolver ports
MCP manifest/contract snapshots
```

Do not prebuild:

```text
runtime plugin ABI
module registry
event bus
cloud abstraction
HTTP gateway
OAuth server
multi-tenant store
MCP Apps framework
```

Architecture v3 is successful if adding a new documented local MCP host requires configuration/testing/docs—not changes to Domain/Application semantics.
