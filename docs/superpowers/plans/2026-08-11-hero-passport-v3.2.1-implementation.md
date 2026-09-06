# Hero Passport v3.2.1 Implementation Plan

> **Execution policy:** Use `superpowers:test-driven-development` for every product-code task, `superpowers:systematic-debugging` for unexpected failures, and `superpowers:verification-before-completion` before claiming a task complete. Prefer `superpowers:subagent-driven-development` or `superpowers:executing-plans` when executing this plan.

**Goal:** Implement Hero Passport 0.1.0 as a local-first deterministic RPG companion using stdio HP-MCP/2, official Agent Skill, CLI, multiple Heroes, explicit mutation identity, one open Quest per Hero+Project and crash-safe SQLite progression.

**Architecture:** C# 14 / .NET 10 modular monolith. Domain owns pure versioned game rules; Application owns semantic use cases; Infrastructure owns EF/SQLite/Git/filesystem/config/doctor; App owns MCP/CLI/localization/presentation. Agent Skill is a portable orchestration package and never calculates authoritative game state.

**Tech baseline:** .NET SDK 10.0.302; `net10.0`; ModelContextProtocol 2.2.0; EF Core SQLite/Microsoft.Data.Sqlite 10.0.10; SQLitePCLRaw.bundle_e_sqlite3 3.0.5; qualified actual SQLite >=3.53.4; System.CommandLine 2.0.10; xunit.v3 3.2.2.

**Dependency qualification refresh:** 2026-09-06. The HP-MCP/2 application contract remains v3.2.1; the official MCP SDK implementation baseline was refreshed and requalified independently.

## Global constraints

Before each task read `AGENTS.md` plus relevant normative docs.

Never introduce:

```text
source/diff/raw-log ingestion
continuous telemetry
LLM judge
agent owner/lease/heartbeat
cloud/sync backend
HTTP/OAuth
MCP Tasks for Quest lifecycle
MediatR/AutoMapper/Dapper/Polly/runtime plugin framework
```

SQLite correctness claims require real temporary file-backed SQLite. EF InMemory is not evidence for WAL, locking, CHECK/FK/partial-index, migrations, pooling, crash or backup behavior.

Every code task follows:

```text
write focused failing test
run and observe expected failure
implement minimum change
run and observe pass
refactor without widening scope
run focused + impacted tests
focused commit
```

Do not implement later RPG tasks before the vertical checkpoint unless a prior task strictly requires a seam.

---

# Phase A — Risk-first executable product slice

## Task 0 — Freeze dependency and contract availability

**Purpose:** prove the architecture baseline can actually restore before scaffolding the rest.

**Create/update:**

```text
global.json
Directory.Packages.props
```

**Requirements:**

```text
SDK 10.0.302
ModelContextProtocol 2.2.0
Microsoft.EntityFrameworkCore.Sqlite 10.0.10
Microsoft.Data.Sqlite 10.0.10
SQLitePCLRaw.bundle_e_sqlite3 3.0.5
System.CommandLine 2.0.10
xunit.v3 3.2.2
```

**Evidence:** run real package restore against configured feeds. If the pinned ModelContextProtocol version cannot restore, stop and investigate package/feed identity; do not silently downgrade the architecture.

**Verify:**

```bash
dotnet --version
dotnet restore
```

**Commit:** `build: freeze v3.2.1 dependency baseline`

---

## Task 1 — Solution scaffold and architectural dependency guards

**Create:**

```text
HeroPassport.slnx
Directory.Build.props
src/HeroPassport.Domain/HeroPassport.Domain.csproj
src/HeroPassport.Application/HeroPassport.Application.csproj
src/HeroPassport.Infrastructure/HeroPassport.Infrastructure.csproj
src/HeroPassport.App/HeroPassport.App.csproj
tests/HeroPassport.Domain.Tests/HeroPassport.Domain.Tests.csproj
tests/HeroPassport.Application.Tests/HeroPassport.Application.Tests.csproj
tests/HeroPassport.Infrastructure.Tests/HeroPassport.Infrastructure.Tests.csproj
tests/HeroPassport.App.Tests/HeroPassport.App.Tests.csproj
tests/HeroPassport.Architecture.Tests/HeroPassport.Architecture.Tests.csproj
tests/HeroPassport.Contract.Tests/HeroPassport.Contract.Tests.csproj
tests/HeroPassport.AgentEvals/HeroPassport.AgentEvals.csproj
```

**Tests first:** architecture tests enforce:

```text
Domain references no project
Application -> Domain only
Infrastructure -> Application + Domain
App -> Application + Infrastructure
Application has no MCP SDK reference
Domain has no EF/MCP/localization/CLI/Git/filesystem packages
```

Enable nullable; deterministic builds; warnings-as-errors for product projects.

**Verify:** restore/build + architecture tests.

**Commit:** `build: scaffold hero passport v3.2.1 solution`

---

## Task 2 — SQLite bootstrap, migration 0001 and connection policy

**Create:** Infrastructure DbContext/factory, EF configurations, initial migration, connection initializer/qualifier, temporary DB fixture tests.

**Schema first tests:** database rejects direct invalid writes for:

```text
second app_settings row
setup=true with null active Hero
invalid Quest status/result/status/evidence
Trust/Strain outside 0..100
negative counters/XP
invalid scope/correction bounds
open Quest with finished_at
finished Quest without finished_at
invalid FK targets
second open Quest same Hero+Project
```

**Migration 0001:** implement typed singleton `app_settings`, heroes/projects/stats/receipts/Quest/report/component/Skill/unlock/milestone/XP tables and reviewed CHECK/FK/partial-index policy from `DATA-MODEL.md`.

**Connection policy:**

```text
Mode=ReadWriteCreate
Cache=Default
Foreign Keys=True
Pooling=True
Default Timeout=5
```

On every opened product connection:

```sql
PRAGMA synchronous=FULL;
PRAGMA trusted_schema=OFF;
```

Initialization establishes/verifies WAL and actual SQLite >=3.53.4.

**Tests:** fresh connection, pooled reopen, clear pool, child process all show required effective pragmas; `Cache=Shared` absent.

**Migration-lock seam:** add non-destructive diagnostics abstraction for `__EFMigrationsLock`; repair implementation can remain Task 16.

**Verify:** Infrastructure tests on file-backed SQLite.

**Commit:** `feat(storage): establish v3.2.1 sqlite invariants`

---

## Task 3 — Safe primitives and `project-identity/1`

**Domain/Application primitives:** typed UUIDv7 IDs, canonical enums, JSON-safe integer guard, SafeTextV1, injected `TimeProvider` boundary.

**Project identity:** implement official Git resolver contract exactly from `PROJECT-IDENTITY.md`:

```text
explicit project root else cwd
Git common-dir anchor
linked worktrees share Project
submodule/nested repo separate
no remote/full persisted path
safe.directory never weakened
Git redirection env scrubbed
standalone fallback only when no Git context
```

**Tests:** SafeText hostile vectors, UUID canonical form, Git repo/worktree/submodule/bare/safe-directory cases, salted fingerprint stability for same installation.

**Verify:** Domain + Infrastructure focused tests.

**Commit:** `feat(core): add safe primitives and project identity`

---

## Task 4 — Bootstrap, typed settings, minimal Hero and `GetRuntimeContext`

**Application use cases:**

```text
BootstrapApplication
ConfigureApplication
GetRuntimeContext
CreateHero minimally as needed for multi-Hero tests
ActivateHero minimally
```

**Bootstrap receipt:** operation `bootstrap`, `bootstrapRequestId`, `args_encoding_version=mutation-args/1`, canonical hash and result Hero ID.

**Tests first:**

```text
fresh bootstrap creates exactly one Hero + setup
same ID/same args replay
same ID/changed args HP135
fresh ID after setup HP002
two concurrent bootstraps -> one setup
crash before commit -> no partial setup
crash after commit before response -> replay same Hero
configure before setup HP001
configure post-setup allowlist/no-op
```

**Runtime context:** return versions/setup/settings/active default Hero/current Project display data/open Quests across all Heroes/rule versions. Before setup it is still available.

**Read-only invariant:** get_context must not create/update Project rows or any bookkeeping.

**Verify:** Application + file-backed SQLite concurrency/crash tests.

**Commit:** `feat(onboarding): add crash-safe bootstrap and runtime context`

---

## Task 5 — Minimal StartQuest with explicit Hero ownership

**Implement:** `StartQuest` Application use case and persistence path with only fields necessary for a durable open Quest.

Input:

```text
startRequestId
heroId
questType
title
goal
resolved ProjectId
```

**Canonical hash scope:** ProjectId + HeroId + questType + SafeText title + SafeText goal using `mutation-args/1`.

**Writer sequence:** receipt lookup -> setup/Hero validation -> locale snapshot -> one-open check -> insert Project if needed -> Quest + receipt + stats -> commit.

Never read active Hero to choose ownership.

**Tests first:**

```text
same ID/same Project/Hero/args replay
same ID/different Hero HP135
same ID/different Project HP135
active Hero switch racing explicit Start(A) still creates A Quest
replay after active Hero change returns A Quest
replay after locale change returns original locale
two fresh Starts same Hero+Project -> one open, one HP133
different Heroes same Project allowed
same Hero different Projects allowed
linked worktrees same Hero -> second independent Start HP133
crash before/after commit
```

**Commit:** `feat(quests): add explicit idempotent quest start`

---

## Task 6 — Minimal FinishQuest with base XP and semantic conflict detection

**Implement first playable finish:** only terminal outcome + base XP needed initially; create final report/XP event/finalization hash/finish receipt and update minimal Hero/project projection atomically.

Input includes `finishRequestId` and complete finalization payload shape even if advanced reward fields are initially neutral/defaulted according to contract fixtures.

**Tests first:**

```text
same finish ID/same payload replay
same finish ID/changed payload HP135
fresh finish ID/already finalized/equivalent -> original + alreadyFinalized
fresh finish ID/already finalized/different -> HP136
partial vs success race -> one commits, loser conflicts
UNIQUE report + XP event prevent double progression
active Hero switch cannot redirect XP
Project context mismatch
crash before commit -> no partial progression
crash after commit -> same request replays
```

Correct claim: at-most-once committed progression per Quest.

**Commit:** `feat(quests): add conflict-safe atomic quest finish`

---

## Task 7 — Real HP-MCP/2 adapter on official C# SDK

**Implement:** stdio MCP composition in App using `ModelContextProtocol 2.2.0`.

Current exact tool order:

```text
hero.bootstrap
hero.configure
hero.get_context
hero.create
hero.list
hero.activate
hero.archive
hero.restore
hero.start_quest
hero.finish_quest
hero.get_card
```

At this phase unimplemented later Hero admin behavior may exist only if required by current contract; do not fake success. Prefer implement the simple CRUD semantics necessary for the published tool snapshot before marking this task complete.

**Contract tests:** closed schemas, annotations, setup gate, explicit heroId Start, finishRequestId, HP135/HP136, structuredContent + one JSON TextContent semantic equality, level-cap schema optionality, no `hero.delete`/`hero.list_active_quests`.

**Transport tests:** stdout contains protocol frames only; safe diagnostics to stderr.

**Protocol qualification:** preferred 2026-07-28 plus 2025-11-25 compatibility supported by selected SDK. Qualify the real stdio subprocess, exact tool inventory/order and canonical server instructions.

**Commit:** `feat(mcp): expose hp-mcp-2 v3.2.1 tools`

---

## Task 8 — Minimal official Agent Skill package

**Create:**

```text
skills/hero-passport/SKILL.md
skills/hero-passport/references/lifecycle.md
skills/hero-passport/references/finish-attestations.md
skills/hero-passport/references/recovery.md
skills/hero-passport/references/presentation.md
```

**Implement only minimal vertical behavior:**

```text
get_context hydration
setup -> bootstrap
conservative meaningful-work Start heuristic
explicit default heroId Start
carry questId
conservative finish
finishRequestId retry behavior
HP133/HP135/HP136 handling
bounded attestation terminology
```

Declare `hero-passport-skill/1` compatibility metadata.

**Agent evals:** trigger/no-trigger, restart hydration, inactive-Hero recovery, active Hero changed elsewhere, ambiguous Start/Finish retries, HP136 no-overwrite, autoStart=false persistence.

**Commit:** `feat(skill): add minimal hero passport lifecycle skill`

---

## Task 9 — Packaged Codex vertical checkpoint

This is the architecture checkpoint. Do not start full RPG layers until it passes.

**Pack/install actual app + Skill into isolated test environment.**

E2E:

```text
fresh HERO_PASSPORT_HOME
bootstrap via Skill/MCP
real temporary Git repo
get_context
meaningful request -> Start explicit Hero
minimal work path
Finish -> base XP persisted
restart MCP process
get_context finds durable state/history
retry/collision vectors behave correctly
```

**Reliability qualification in packaged shape:**

```text
Start crash before/after commit
Finish crash before/after commit
concurrent Start race
conflicting concurrent Finish
fresh/pooled/new-process SQLite pragmas
linked-worktree one-open limitation
stdio purity
```

**Gate:** if this vertical path is not stable, debug/fix architecture-adapter/storage issues before adding more game features.

**Commit:** `test(e2e): qualify minimal hero passport vertical slice`

---

# Phase B — Complete deterministic RPG

## Task 10 — Full reward engine + Skill allocation

Implement `reward/2.0.0` and `skill-allocation/1.0.0` exactly from `ENGINE-SPEC.md`.

**Goldens:** clean coding 95 XP; partial 57; blocked 28; failed 9; abandoned 0; penalties/caps; integer-only arithmetic; Skill XP conservation 100 / 60-40 / 50-30-20.

Integrate full component rows into Finish atomic transaction.

**Commit:** `feat(game): implement reward and skill allocation`

---

## Task 11 — Hero/Skill progression, Rank and cap wire semantics

Implement threshold tables:

```text
hero-progression/2.0.0
skill-progression/2.0.0
rank/1.0.0
```

**Tests:** every threshold edge; multi-level jump; JSON-safe overflow; Hero L50/Skill L10 continue XP; `isLevelCapped`; `nextLevelXpRequired` absent at cap.

Update card/Finish output accordingly.

**Commit:** `feat(game): add hero skill progression and ranks`

---

## Task 12 — Trust/Strain RPG stats

Implement `trust-strain/1.0.0` exactly.

**Tests:** initial 50/20, outcome components, clean/tests positives, violation/correction caps, clamp, abandoned neutral, historical stored components.

Presentation/docs call these RPG stats, never verified productivity telemetry.

**Commit:** `feat(game): add trust strain progression`

---

## Task 13 — Streak, Traits, Titles and semantic milestones

Implement:

```text
streak/1.0.0
unlock/2.0.0
```

**Tests:** success/reset transitions, unlock thresholds/monotonicity, active Title priority, semantic milestone events.

Do **not** implement deterministic hash/mod flavor selection. Domain emits semantic events only.

**Commit:** `feat(game): add cosmetic milestones and unlocks`

---

## Task 14 — RU/EN localization and presentation

Implement App `.resx` or accepted .NET resource localization approach for:

```text
ru-RU
en-US
```

Domain/Application stay semantic.

Implement start banner, finish reward logs/card and current curated milestone flavor mapping.

**Tests:** key completeness, placeholder parity, locale snapshot, cap display, style variants, flavor cannot alter game values.

**Commit:** `feat(ui): add bilingual hero passport presentation`

---

# Phase C — Administration, recovery and release evidence

## Task 15 — Complete Hero administration + CLI logical delete

Finish create/list/activate/archive/restore semantics if not already complete.

Add explicit CLI permanent logical delete.

**Guards:** active default Hero cannot delete/archive as specified; any open Quest blocks archive/delete; delete is not an MCP tool.

**Delete transaction:** mark surviving relevant receipts `target_deleted`, remove Hero-owned canonical history/projections via reviewed cascade/explicit deletes, commit.

**Tests:** late create/start receipt after delete never resurrects; logical-delete wording/behavior makes no forensic-erasure claim.

**Commit:** `feat(cli): complete hero administration and logical delete`

---

## Task 16 — Doctor, migration-lock repair, projection rebuild, backup/export

Doctor reports:

```text
sqlite version
WAL/FULL/foreign_keys/trusted_schema
storage location support
migration state
__EFMigrationsLock suspicion
quick_check
foreign_key_check
```

Explicit migration-lock repair only after safety preconditions.

Implement projection rebuild and compare read models.

Live backup uses SQLite backup API + validation before publish.

Export remains explicit and privacy-bounded.

**Child-process tests:** kill-during-migration -> diagnose -> explicit repair -> migrate/integrity pass.

**Commit:** `feat(admin): add doctor repair rebuild and backup`

---

## Task 17 — Broader host qualification and 0.1 release gate

Codex remains reference E2E.

Validate current Skill/integration instructions against selected supported hosts using release-time official documentation; do not freeze stale host-specific config syntax in architecture.

Run complete:

```text
unit/application tests
real SQLite integration/concurrency/crash tests
migration/rebuild/backup tests
MCP contract snapshots/protocol qualification
Agent Skill evals
RU/EN localization tests
privacy/static architecture scans
packaged Codex E2E
cross-host smoke matrix
```

Record exact commands/outputs/versions. Do not claim release readiness from partial suites.

**Commit:** `test(release): qualify hero passport 0.1`

---

# Definition of implementation-ready architecture

Before Task 0 product code begins, v3.2.1 documentation must consistently specify:

```text
real MCP SDK 2.2.0 dependency gate
bootstrap request identity
pre/post setup tool gate
get_context hydration/recovery/version fields
explicit heroId Start
ProjectId included in Start idempotency scope
finishRequestId + HP136 conflict
versioned/tombstoned mutation receipts
one-open linked-worktree limitation
typed singleton app_settings
physical CHECK/FK/index schema
per-connection FULL/foreign_keys/trusted_schema
migration abandoned-lock recovery
logical CLI-only delete
level-cap wire semantics
semantic-only milestone engine output
sync-conscious wording
risk-first checkpoint
```

If these contracts change during implementation, stop the affected task, update the normative contract/ADR/tests first, then resume TDD.
