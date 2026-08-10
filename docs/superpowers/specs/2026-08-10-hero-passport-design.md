# Hero Passport Design Specification

> **Status:** Accepted for implementation planning  
> **Date:** 2026-08-10  
> **Basis:** project requirements + supplied technical report + current official documentation snapshot in `docs/REFERENCES.md`

## Goal

Build Hero Passport as a local-first RPG passport/state layer for AI coding agents: a meaningful task becomes a quest, the agent starts it once, works normally, finishes it once, receives a compact deterministic RPG result, and persists progression locally. Codex local stdio MCP is the first integration target; dashboard is deliberately later.

## Product shape

```text
Hero Passport
  = local stdio MCP server
  + deterministic versioned RPG domain engine
  + SQLite state/history
  + local CLI operations
  + compact end-of-session displayText
  + local Blazor dashboard after MVP
```

Hero Passport is an entertaining companion/passport first, not an enterprise monitoring dashboard. The MVP must feel lightweight and game-like while retaining rigorous persistence and rule auditability.

## Non-goals before minimal MVP

```text
achievements system
artifacts/inventory
runtime plugins/external DLL loading
HTTP/remote MCP
MCP Apps or MCP Tasks
cloud/team/auth
LLM judge
self-evolution
continuous editor telemetry
per-keystroke/per-line/per-diff XP
source/diff/raw-log collection
public REST API
full trace capture
```

## Architecture

Use a modular monolith with explicit inward dependencies:

```text
HeroPassport.Domain
  <- HeroPassport.Application
      <- HeroPassport.Infrastructure
          <- HeroPassport.App

HeroPassport.Web (later)
  -> Application
  -> Infrastructure only in startup/composition
```

### Domain

Pure deterministic state/rules. No EF, MCP, CLI, filesystem or web framework dependencies.

Owns:

```text
IDs/value objects
quest/result state
QuestQualityFlags
RewardCalculator
LevelCalculator
SkillKeyNormalizer / SkillXpAllocator
TrustRiskCalculator
Trait policies
RuleVersions
state transition invariants
```

### Application

Owns use cases, ports, contracts, validation and presentation projections:

```text
StartQuestHandler
FinishQuestHandler
GetCurrentQuestHandler
GetHeroCardHandler
InitializeApplicationHandler
ExportHandler
read models
IHeroStore / IProjectStore / IQuestStore / IUnitOfWork
IProjectIdentityResolver / IAppDataPaths
```

Transport-neutral external DTOs live under `Application.Contracts` initially; no separate Contracts assembly until a real independently versioned consumer exists.

### Infrastructure

Owns:

```text
EF Core DbContext/mappings
SQLite migrations/connection setup
WAL/foreign key/runtime native checks
stores/read projections
app-data paths
project fingerprint resolver
JSON export writer
local optional logging
```

### App

One executable, two primary modes:

```text
normal CLI
MCP stdio protocol mode
```

Owns Generic Host composition, System.CommandLine and thin MCP SDK adapters. MCP mode reserves stdout for protocol bytes only.

### Web later

Local Blazor dashboard starts only after release `0.1.0`. Read-model driven, loopback by default, no direct DbContext in components.

## Current stable technology baseline

As of 2026-08-10:

```text
C#                                        14
.NET SDK                                  10.0.302
.NET runtime / ASP.NET Core               10.0.10
ModelContextProtocol                       2.0.0
MCP revision                              2026-07-28
EF Core / EF Core SQLite                  10.0.10
SQLitePCLRaw.bundle_e_sqlite3              3.0.5
native SQLite via bundle                  >= 3.53.4
System.CommandLine                         2.0.10
xunit.v3                                   3.2.2
xunit.runner.visualstudio                  3.1.5 compatibility/private
Microsoft Testing Platform                selected through .NET 10 test config
```

Preview dependencies are excluded unless a separate ADR demonstrates necessity.

## MCP contract

Exactly four MVP tools in deterministic order:

```text
hero.start_quest
hero.finish_quest
hero.current_quest
hero.get_card
```

Lifecycle:

```text
start once -> normal work -> finish once
```

`current_quest` is recovery, `get_card` is a query. There is no per-step logging tool.

### Important contract decisions

- Explicit `questId` is the application handle across calls.
- `finish_quest` does not resend immutable `questType`; server loads it from the quest.
- `workspacePath` is absent. Local process resolves project; Codex `cwd` can be configured locally where needed.
- Unknown/generic metadata bags are absent.
- Inputs are bounded and reject unsupported values.
- Compact output is default.
- Tool errors use tool-result error semantics for valid `tools/call` requests.
- Retried finish returns stored original reward, never recalculates under newer rules.

## Domain rules v1.0.0

### XP

Base:

```text
planning 30
research 40
coding 60
review 50
debugging 70
documentation 40
maintenance 40
```

Result multipliers in integer permille:

```text
success 1000
partial 600
failed 200
blocked 300
abandoned 0
```

Bonuses:

```text
tests mentioned +10
clean scope +10
clear summary >= 40 chars +10
no user corrections +5
```

Penalties:

```text
scope violation -25 each
short summary -10
user correction -10 each
```

Formula:

```text
resultXp = floor(baseXp * permille / 1000)
finalXp = max(0, resultXp + bonuses + penalties)
```

Golden clean coding success = `95 XP`.

### Levels

Hero starts at level 1. Required XP for next level:

```text
xpToNext(L) = 100 + 50 * (L - 1)
```

Total threshold:

```text
threshold(L) = (L - 1) * (25 * L + 50)
```

Total XP is the source of truth; level progress is derived.

### Skills

Normalize aliases to canonical keys, de-duplicate, preserve first occurrence, keep at most three.

Allocation:

```text
1 skill: 100
2: 60/40
3: 50/30/20
```

Use cumulative-floor integer allocation. For 95 XP across three skills the exact deltas are `47/29/19`, conserving the total.

### Trust/risk

Initial:

```text
trust 50
risk 20
```

Clamp `0..100`. Rules are exactly specified in `docs/ENGINE-SPEC.md` and versioned separately.

### Traits

Initial behaviors only:

```text
precise_executor  active after 5 qualifying clean successes
test_scout        active after 5 qualifying tested coding/debugging successes
quest_finisher    active after 10 success/partial finishes
```

Traits do not award XP and are not achievements.

## Persistence

One SQLite DB. Core tables:

```text
heroes
projects
hero_project_stats
quest_sessions
quest_reports
quest_report_skills
skills
hero_skills
traits
hero_traits
xp_events
app_settings
```

### Integrity

- `xp_events.quest_id` unique for quest reward.
- One short atomic finish transaction updates immutable history and projections together.
- Retry of completed quest is read-only.
- No DB transaction stays open across agent work.
- Partial unique constraints protect active/idempotent start behavior where appropriate.
- Real SQLite tests cover concurrency/rollback.

### SQLite mode

```text
foreign_keys = ON
journal_mode = WAL
bounded busy timeout
no Cache=Shared optimization with WAL
```

Native SQLite version is checked at runtime/integration release gate.

## Privacy/security

The MCP schema/database intentionally have no fields for:

```text
source code
file contents
diffs/patches
raw terminal/build/test logs
full prompts/chat history
API keys/secrets
environment-variable bags
full workspace path
arbitrary binary attachments
```

Project identity persists a display name plus versioned SHA-256 fingerprint; path use is transient/local.

Logs do not record request/response bodies by default. MCP stdout is protocol only; stderr/local explicit sink is diagnostic output.

Remote networking, auth and at-rest encryption are not claimed by MVP.

## Codex integration

Preferred current native setup:

```bash
hero-passport init
codex mcp add hero-passport -- hero-passport mcp
codex mcp list
```

Do not mutate Codex config from Hero Passport. Root consumer `AGENTS.md` instructions should be concise because Codex project instructions have a bounded combined context budget.

First E2E acceptance is Codex CLI in the current repo/workspace. Explicit local `mcp_servers.hero-passport.cwd` is a documented fallback for host setups that launch the MCP server from another working directory.

## Test architecture

```text
Domain.Tests          pure formulas/goldens/boundaries
Application.Tests     use cases with fake ports/time
Infrastructure.Tests  real SQLite temp-file migrations/constraints/races
App.Tests             CLI + MCP adapter + process stdout guard
Architecture.Tests    dependency/privacy/contract fitness functions
```

Primary gates:

```bash
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

Full implementation PRs run Windows/Linux/macOS CI.

## Packaging

First target: .NET tool. Self-contained per-RID follows after core validation. Single-file waits for explicit native SQLite packaging/extraction tests.

## Release architecture

Milestones `0.0.1` through `0.0.10` build the foundation/domain/storage/CLI/MCP/Codex loop. `0.1.0-rc.1` is hardening; `0.1.0` is minimal MVP. `0.2.0` is the first local dashboard release.

See `docs/ROADMAP.md` and the implementation plan.

## Key deviations/corrections from supplied technical report

The report remains the product/technical starting point, but the August 10 verification changes several details:

1. **MCP SDK:** stable official C# SDK v2.0.0 is now the baseline and implements MCP `2026-07-28`.
2. **Protocol architecture:** explicit application handles/SQLite state align with the new stateless protocol direction; no hidden transport session dependency.
3. **Tool catalog:** deterministic fixed ordering is explicitly required for cache/prompt-cache stability.
4. **SQLite:** interim `SQLitePCLRaw 2.1.12` is superseded by stable `3.0.5`, with native SQLite `>=3.53.4`.
5. **Contracts assembly:** deferred until a real independent boundary exists; contracts stay in Application for MVP.
6. **Time:** use built-in `TimeProvider`, not a custom clock abstraction.
7. **Codex config:** use current native `codex mcp add/list`; no custom config installer in MVP.
8. **Workspace privacy:** no `workspacePath` in MCP schema or cleartext path persistence.
9. **Finish schema:** no repeated `questType`; immutable quest data loads by `questId`.
10. **Idempotency:** completed retry returns the original persisted outcome rather than rerunning current scoring rules.
11. **Level curve/trait thresholds/concurrency details:** fully specified rather than left implicit.
12. **Roadmap:** product gate is a real `0.1.0` MVP before `0.2.0` dashboard instead of many internal tail versions.

## Design acceptance criteria

The design is internally complete when every requirement maps to a canonical document and every implementation milestone has a corresponding test/acceptance gate. Implementation may not silently change formulas, contract schemas, privacy boundaries, storage semantics or dependency baseline without updating the appropriate specification and decision log.
