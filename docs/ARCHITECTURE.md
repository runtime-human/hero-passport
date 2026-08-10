# Hero Passport — Architecture

**Status:** Accepted  
**Baseline:** 2026-08-10  
**Architecture style:** local-first modular monolith with ports/adapters at infrastructure boundaries

## 1. Executive decision

Hero Passport is one local product with one authoritative SQLite database and one deterministic domain model. It is not a distributed system and must not be designed like one before a real remote requirement exists.

The runtime MVP is a single executable (`hero-passport`) that can operate as a normal CLI or as an MCP stdio server. A separate local Blazor host is introduced later for dashboard UX, sharing Application use cases/read models rather than duplicating rules.

## 2. Quality attributes, in priority order

1. **Correctness/determinism** — the same inputs and rule version produce the same game result.
2. **Privacy/local ownership** — no source-code telemetry; state lives locally.
3. **MCP correctness** — protocol-safe stdout and compact stable schemas.
4. **Idempotency/integrity** — retries cannot double-award progress.
5. **Token efficiency** — tiny tool surface and concise descriptions/results.
6. **Cross-platform behavior** — Windows/Linux/macOS.
7. **Testability** — pure rules, real SQLite tests, process-level MCP checks.
8. **Evolvability** — rule/schema versioning and explicit module boundaries.
9. **Performance** — important but naturally satisfied by the small local workload.

## 3. Solution structure

```text
src/
  HeroPassport.Domain/
  HeroPassport.Application/
  HeroPassport.Infrastructure/
  HeroPassport.App/
  HeroPassport.Web/              # created only in dashboard phase

tests/
  HeroPassport.Domain.Tests/
  HeroPassport.Application.Tests/
  HeroPassport.Infrastructure.Tests/
  HeroPassport.App.Tests/
  HeroPassport.Architecture.Tests/
```

### 3.1 Why no standalone Contracts project initially

MVP has one executable consumer of application contracts. MCP request/response records live under `HeroPassport.Application.Contracts` and remain transport-neutral. A separate `HeroPassport.Contracts` assembly is extracted only when a second independently versioned host/package actually needs it.

## 4. Compile-time dependency rule

```text
HeroPassport.Domain
        ^
        |
HeroPassport.Application
        ^             ^
        |             |
HeroPassport.Infrastructure
        ^
        |
HeroPassport.App

HeroPassport.Web (later) -> Application
Web composition root      -> Infrastructure
```

Rules:

- Domain references no product project and no infrastructure package.
- Application references Domain only.
- Infrastructure references Application + Domain.
- App references Application + Infrastructure and is the composition root.
- Web references Application; startup may wire Infrastructure, but Razor components do not know `DbContext`.
- Architecture tests enforce this graph.

## 5. Feature-first organization

Inside Domain/Application, group by capability rather than generic technical buckets:

```text
HeroPassport.Domain/
  Heroes/
  Quests/
  Rewards/
  Skills/
  Traits/
  Projects/
  Shared/

HeroPassport.Application/
  Heroes/
  Quests/
    StartQuest/
    FinishQuest/
    GetCurrentQuest/
  Cards/
    GetCard/
  Projects/
  Export/
  Contracts/
  Abstractions/
```

Avoid catch-all `Services`, `Helpers`, `Managers`, `Utils` folders.

## 6. Project responsibilities

### 6.1 Domain

Owns pure policy/invariants:

- hero/quest/project identity types;
- quest/result/skill canonical keys;
- quest state transitions;
- quality flags;
- XP/level calculations;
- reward breakdown;
- skill allocation;
- trust/risk updates;
- trait progression;
- rule-version constants.

Domain knows nothing about EF Core, SQLite, filesystem, console, JSON, MCP, CLI, ASP.NET Core or environment variables. Scoring uses deterministic integer arithmetic.

### 6.2 Application

Owns use cases and ports:

```text
StartQuestHandler
FinishQuestHandler
GetCurrentQuestHandler
GetHeroCardHandler
InitializeApplicationHandler
ExportHandler/read query
```

and abstractions such as:

```csharp
public interface IHeroStore;
public interface IProjectStore;
public interface IQuestStore;
public interface IUnitOfWork;
public interface IProjectIdentityResolver;
public interface IAppDataPaths;
```

Use capability-oriented stores/ports; do not create a generic repository per entity.

Application also owns transport-neutral request/response contracts, validation/error mapping, output projections/read models and `TimeProvider` consumption.

### 6.3 Infrastructure

Owns adapters:

- EF Core `DbContext` + mappings;
- SQLite connection/migrations/transactions;
- app-data paths and best-effort permissions;
- local project/workspace resolver + fingerprint;
- JSON export writer;
- optional local file logging;
- store/query implementations.

No XP/trust/trait decisions live in Infrastructure.

### 6.4 App

Owns executable concerns:

- .NET Generic Host/DI;
- System.CommandLine;
- MCP stdio server;
- thin MCP tool adapters;
- stdout/stderr separation;
- CLI exit codes;
- `doctor` diagnostics.

MCP tool methods map SDK inputs -> Application and Application outputs -> MCP results. They never query EF directly.

### 6.5 Web — post-MVP

Local ASP.NET Core/Blazor Web App. Read-only first. Components consume Application read models/services, never `HeroPassportDbContext`. Any future write uses the same Application command path as CLI/MCP.

## 7. Runtime topology

### MCP

```text
Codex / MCP client
  -> stdio JSON-RPC
  -> HeroPassport.App MCP adapter
  -> Application handler
  -> Domain rules
  -> Infrastructure / EF Core
  -> SQLite
```

Application persistence, not hidden MCP transport state, carries progression between calls.

### CLI

```text
Terminal
  -> System.CommandLine
  -> Application handler/query
  -> Infrastructure
  -> SQLite
```

Normal CLI may use Spectre.Console later. MCP mode must not share decorative stdout output.

### Dashboard later

```text
Browser on loopback
  -> HeroPassport.Web
  -> Application read models
  -> Infrastructure
  -> same SQLite database
```

## 8. Start/finish flows

### 8.1 Start quest

```text
validate contract + schemaVersion
 -> resolve hero
 -> resolve project locally
 -> check explicit idempotency/open quest
 -> return existing quest on retry
 -> otherwise insert Open quest in short transaction
 -> update project projection only if newly created
 -> return compact card/displayText
```

No database transaction stays open during the agent's actual work.

### 8.2 Finish quest

```text
validate contract
 -> load quest + hero/project state
 -> if completed: return persisted original outcome
 -> normalize <=3 skills
 -> build QuestQualityFlags
 -> calculate RewardBreakdown(explicit rule version)
 -> calculate level/skill/trait/trust-risk deltas in memory
 -> atomically persist report + XP ledger + all projections + completion
 -> return compact status/displayText
```

Historical retry never reruns a completed quest under newer rules.

## 9. IDs and time

Use `.NET Guid.CreateVersion7()` for generated persistent IDs. Render externally as lowercase canonical GUID strings.

Persist UTC `DateTime` and enforce UTC at the boundary. SQLite has no native `DateTimeOffset` semantics worth pretending otherwise for this model.

Use injected `TimeProvider` in Application/Domain behavior; avoid direct `DateTime.UtcNow` calls there.

## 10. Project identity/privacy

`projectId = auto` resolves:

1. explicit valid persisted project ID when supplied;
2. local Git repository root when detectable;
3. local current working directory as fallback.

Persist:

```text
project_id
project_display_name
workspace_fingerprint = SHA-256(normalized local root identity + versioned app-local identity salt/namespace)
identity_version
```

Do not persist a cleartext full path by default. The resolver may transiently use it locally.

The fingerprint reduces accidental disclosure; it is not a credential or anonymity primitive against a local attacker.

For Codex setups where the MCP process does not start in the intended workspace, use local `mcp_servers.hero-passport.cwd` configuration rather than a model-visible `workspacePath` argument.

## 11. Version architecture

Persist relevant versions with history:

```text
schema_version
reward_rule_version
level_rule_version
skill_rule_version
trust_risk_rule_version
trait_rule_version
project_identity_version
```

Rule versions describe behavior, not NuGet versions. Historical rewards are immutable; new rules apply prospectively unless an explicit future recalculation feature is designed.

## 12. Error architecture

Expected compact product errors:

```text
HP100 invalid_request
HP101 unsupported_schema_version
HP110 hero_not_found
HP120 project_not_resolved
HP130 quest_not_found
HP131 no_open_quest
HP132 quest_conflict
HP140 unsupported_quest_type
HP141 unsupported_result

HP200 storage_unavailable
HP201 migration_failed
HP202 database_busy
HP210 app_data_unavailable

HP900 internal_error
```

Business/tool failures from a valid MCP call use MCP tool-error result semantics (`isError`). Protocol framing/JSON-RPC errors stay protocol errors handled by the SDK.

Unexpected exceptions never expose stack traces, SQL, local paths or request dumps in normal MCP output.

## 13. Concurrency model

Assumptions: a few local processes, short requests, no distributed coordination.

- SQLite WAL enabled.
- One SQLite writer at a time.
- Reads are short/no-tracking when possible.
- Write use cases are short transactions.
- Bounded busy timeout.
- Application retries only known transient failures and relies on idempotency/constraints.
- No long-lived transaction spans agent work.

Correctness uses explicit quest status, unique constraints and transaction semantics, not SQL Server-style database-generated `rowversion` (not available in SQLite).

## 14. Persistence strategy

EF Core handles mapping/migrations/ordinary queries. SQLite-specific SQL in migrations is acceptable for exact constraints/indexes/PRAGMA-related setup when EF APIs are insufficient, but stays localized in Infrastructure.

Use migrations from the first schema. `EnsureCreated` is not a long-term/product strategy.

The database is authoritative; caches are disposable optimizations only.

## 15. Read models

Purpose-built DTOs:

```text
HeroCardReadModel
CurrentQuestReadModel
RecentQuestReadModel
ProjectStatsReadModel
DashboardSnapshotReadModel  # added with dashboard
```

Infrastructure projects directly to them and uses `AsNoTracking` where appropriate. EF entities never leak into MCP/CLI/Web.

## 16. Observability

Use normal .NET logging/System.Diagnostics abstractions, local/off by default.

Rules:

- MCP stdout = protocol only;
- diagnostics = stderr or explicit local file;
- no request/response body logging by default;
- no source/diff/prompt fields exist in the product contract;
- correlate with request/quest IDs;
- OpenTelemetry exporter is post-MVP opt-in.

The MCP 2026-07-28 direction deprecates MCP-specific logging in favor of stderr/normal observability; Hero Passport does not depend on the legacy facility.

## 17. Dependency baseline

Approved stable baseline on 2026-08-10:

```text
.NET SDK                                    10.0.302
.NET runtime / ASP.NET Core                 10.0.10
ModelContextProtocol                         2.0.0
Microsoft.EntityFrameworkCore               10.0.10
Microsoft.EntityFrameworkCore.Sqlite        10.0.10
Microsoft.EntityFrameworkCore.Design        10.0.10 (private/dev)
SQLitePCLRaw.bundle_e_sqlite3                3.0.5
native SQLite via bundle                    >= 3.53.4
System.CommandLine                           2.0.10
xunit.v3                                     3.2.2
xunit.runner.visualstudio                    3.1.5 (private compatibility)
```

`SQLitePCLRaw 3.0.5` supersedes the interim `2.1.12` recommendation from the source report because it is the newer stable major release by the architecture snapshot date and depends on a newer safe native SQLite baseline.

Use stable packages only unless an ADR approves an unavoidable preview.

## 18. Reproducible build

Planned `global.json` baseline:

```json
{
  "sdk": {
    "version": "10.0.302",
    "rollForward": "disable",
    "allowPrerelease": false
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

Use Central Package Management and committed NuGet lock files; CI/release restores in locked mode.

## 19. Packaging

Delivery order:

1. source/dev invocation;
2. .NET tool (`hero-passport` command);
3. optional framework-dependent packaging;
4. validated per-RID self-contained binaries;
5. single-file only after native SQLite packaging/extraction/upgrade tests.

Single-file is not a P0 feature.

## 20. Deferred seams and YAGNI

Cheap seams retained:

```text
RuleVersions
SkillKeyNormalizer
QuestQualityFlags
read models
Application ports
migration/golden-test discipline
feature flag skeleton only after first real flag exists
```

Explicitly deferred:

```text
module registry
external plugin ABI/runtime DLL loading
achievements/artifacts
HTTP API/MCP
cloud storage abstraction
MCP Apps/Tasks
LLM judge/self-evolution
```

## 21. Architecture fitness functions

Build/test should fail when practical if:

- Domain references EF/MCP/CLI/ASP.NET namespaces;
- Application contracts expose Infrastructure/EF types;
- a future Razor component references `HeroPassportDbContext`;
- MCP process emits non-protocol stdout;
- a package version bypasses central management;
- reward goldens change without explicit rule review;
- prohibited raw-data properties enter contracts;
- native SQLite falls below the approved floor;
- dependency audit finds a disallowed known vulnerability.

Success means the common quest path is understandable and testable by reading one feature slice, not the entire repository.
