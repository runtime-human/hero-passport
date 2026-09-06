# Hero Passport v3.2.1 — Consolidated Product and Architecture Specification

**Status:** Accepted pre-implementation correction baseline  
**Snapshot:** 2026-08-11  
**Dependency qualification refresh:** 2026-09-06  
**Target:** 0.1.0 local-first MVP  
**Supersedes:** conflicting v3.2 bootstrap, active-Hero mutation context, recovery, Finish replay, destructive MCP, SQLite connection-policy, schema-invariant, sync-readiness and implementation-order clauses.

v3.2.1 is a targeted architecture correction, not a product redesign.

## 1. Product thesis

Hero Passport is a local-first RPG companion for people working with AI coding agents.

0.1 remains MCP Core + official Hero Passport Agent Skill + CLI. Web remains 0.2.

Core owns authoritative state, validation, idempotency, deterministic game rules and persistence. The Skill owns model-driven lifecycle heuristics and presentation. The Skill never calculates or invents game facts.

Hero Passport is not source-code telemetry, employee monitoring, an LLM judge, an agent scheduler, an anti-cheat system or a cloud service.

## 2. Quest boundary

Core truth: **A Quest is an explicitly started durable unit of progression.**

Skill recommendation: **Prefer one coherent meaningful user goal per Quest.**

“Meaningful” is a Skill/model heuristic, not something the server can objectively prove.

A conversation may contain zero, one or several Quests. MCP connection/session state is never Quest identity.

One open Quest remains allowed per `(HeroId, ProjectId)`.

Linked Git worktrees share `ProjectId`, therefore 0.1 intentionally allows only one simultaneous open Quest for the same Hero across linked worktrees. Parallel independent same-Hero Quests in one logical repository are unsupported in 0.1. No `WorkContextId` is introduced.

A Quest belongs to its persisted Hero and Project, not to an AI-agent identity. No agent owner, lease, heartbeat or leader election exists.

## 3. Request/work identity

Caller mutation identities:

```text
bootstrapRequestId
createRequestId
startRequestId
finishRequestId
```

Server durable work/resource IDs include `heroId`, `projectId`, `questId`.

Current exposed IDs use canonical UUIDv7. Natural-language title/goal is never an idempotency key. MCP/JSON-RPC request IDs are transport-only.

## 4. Bootstrap and configuration

First-run creation is separated from preferences.

`hero.bootstrap` input:

```text
bootstrapRequestId
locale
heroName
presentationStyle
autoStartQuest
autoFinishQuest
```

Bootstrap atomically creates the initial Hero, makes it active, stores settings and persists a mutation receipt.

```text
same bootstrapRequestId + same canonical args -> exact replay
same bootstrapRequestId + changed args        -> HP135
fresh bootstrapRequestId after setup          -> HP002 setup_already_completed
```

After setup, `hero.configure` changes only locale, presentationStyle, autoStartQuest and autoFinishQuest. Applying identical complete preferences is a no-op success.

## 5. Runtime context

Add read-only `hero.get_context`, available before and after setup.

It returns product/contract/skill versions, setup/settings, active default Hero, current Project display data, all open Quests in that Project across Heroes, and rule versions.

This replaces `hero.list_active_quests` and is the Skill hydration/recovery/version-skew surface.

Read-only calls never create Project rows or mutate “last seen” bookkeeping.

## 6. Multiple Heroes and explicit Start ownership

Multiple Heroes remain in MVP.

`activeHeroId` is only the default Hero preference for forming new work. It is not hidden mutation ownership.

`hero.start_quest` requires explicit `heroId`:

```text
startRequestId
heroId
questType
title
goal
```

The Skill normally gets the active/default Hero from `hero.get_context`, then passes that ID explicitly.

Another host changing the global active Hero cannot silently retarget an already formed Start request. Switching active Hero never moves, closes or reassigns existing Quests.

## 7. Start idempotency scope and linearization

Project identity is resolved through `project-identity/1` before the database transaction. `ProjectId` is part of canonical idempotency scope even though it is not model-visible.

Start flow:

```text
validate request + SafeText
resolve ProjectId
BEGIN non-deferred Serializable writer
lookup receipt(start_quest, startRequestId)
  found:
    compare ProjectId + HeroId + explicit args under stored encoding version/hash
    same -> load ORIGINAL Quest and return replay
    different -> HP135
  absent:
    read typed app_settings
    validate setup
    validate explicit Hero exists and is not archived
    snapshot locale
    check open HeroId+ProjectId Quest -> HP133 if present
    insert Quest + receipt + projection update
COMMIT
```

Replay never re-resolves current active Hero or locale.

## 8. Finish identity and semantic conflict

`hero.finish_quest` requires `finishRequestId`, `questId`, result, summary, metrics and skillsUsed.

The canonical finish payload is versioned/fingerprinted.

```text
same finishRequestId + same payload -> replay
same finishRequestId + changed payload -> HP135
fresh finishRequestId + already finished + equivalent payload -> original result, alreadyFinalized=true
fresh finishRequestId + already finished + different payload -> HP136 quest_already_finalized_conflict
```

The first committed finalization is immutable. A conflict is surfaced but never overwrites history.

Correct guarantee remains **at-most-once committed progression per Quest**.

## 9. Bounded attestations

Finish inputs are bounded agent attestations/reported signals, not independent verification.

`observed` means the agent asserts it directly ran/saw the referenced result. Hero Passport does not ingest raw evidence, source, diffs or logs to verify the claim.

The RPG engine is deterministic given validated canonical attestations.

## 10. Current MCP surface

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

Current order is a contract snapshot; the number 11 is not a permanent invariant.

Removed from MCP: `hero.delete`, `hero.list_active_quests`.

## 11. Permanent delete

Archive/restore remain reversible MCP operations.

Permanent Hero delete is CLI-only in 0.1. A model-readable confirmation name is not proof of human destructive intent, and Hero Passport does not require cross-host MRTR qualification solely for this rare administration path.

Normative wording:

> Permanent Hero delete irreversibly removes the Hero from the active Hero Passport logical database state. Hero Passport does not claim forensic secure erasure from storage media, filesystem snapshots, backups or previously exported copies.

## 12. Mutation receipts

Required logical fields:

```text
operation_key
request_id
args_encoding_version
args_hash
result_kind
result_entity_id nullable
project_id nullable
hero_id nullable
result_status active | target_deleted
effective_at_utc
```

Unique `(operation_key, request_id)`.

Receipt target/context IDs intentionally have no FK requirement so a minimal receipt can survive target deletion.

`mutation-args/1` is stable length-delimited binary encoding. JSON serializer output/whitespace is not the hash contract.

When permanent Hero deletion removes target data, related surviving receipts become `target_deleted`; a late retry may report previously-committed-then-deleted state but must never recreate the resource.

## 13. Canonical history and projections

Hero Passport is not event sourcing.

Canonical surviving history includes Quest/final report, XP events, reward/Trust-Strain/Skill delta rows, Trait/Title unlock rows, semantic milestones and rule versions.

Rebuildable mutable projections include Hero total XP cache, Trust/Strain cache, streak cache, hero_skills totals and hero_project_stats.

A release test rebuilds projections from canonical surviving history and compares public card/project read models.

Permanent Hero delete removes that Hero’s history as an explicit lifecycle/privacy exception.

## 14. Typed singleton settings

`app_settings` is exactly one typed row:

```text
id INTEGER PRIMARY KEY CHECK(id=1)
setup_completed bool CHECK
active_hero_id nullable FK heroes RESTRICT
locale closed enum
presentation_style closed enum
auto_start_quest bool
auto_finish_quest bool
project_identity_salt_v1 BLOB
config_version >= 1
```

Invariant:

```text
setup_completed=0 -> active_hero_id IS NULL
setup_completed=1 -> active_hero_id IS NOT NULL
```

Migration 0001 seeds an unconfigured singleton row.

## 15. Database constraints

Initial schema physically enforces closed Quest/result/evidence/status values, Trust/Strain range, nonnegative XP/counters, metric bounds, open/finished timestamp consistency and singleton settings.

Partial unique invariant remains:

```sql
CREATE UNIQUE INDEX ux_quest_sessions_one_open_per_hero_project
ON quest_sessions(hero_id, project_id)
WHERE status='open';
```

Hero-owned history/projections cascade on permanent Hero delete; Project references restrict; report children cascade; catalog references restrict; receipt context IDs have no FK.

Projects are not user-deletable in 0.1.

## 16. SQLite connection policy

Qualified effective profile:

```text
actual SQLite >= 3.53.4
journal_mode=WAL
synchronous=FULL
foreign_keys=ON
trusted_schema=OFF
Cache=Default
Pooling=True
Default Timeout=5 seconds
same-host local filesystem only
```

Database initialization/qualification establishes/verifies WAL and runtime version.

Every opened product connection enforces foreign keys, `PRAGMA synchronous=FULL` and `PRAGMA trusted_schema=OFF`. Do not assume one initialization connection configures future/pooled connections.

Tests cover fresh open, pooled reopen, pool clear and new process.

## 17. EF migration abandoned-lock recovery

Doctor inspects migration state and `__EFMigrationsLock` plus quick_check/foreign_key_check.

A suspicious abandoned EF migration lock is reported, never silently cleared during normal startup.

An explicit repair path is allowed only after competing Hero Passport processes are stopped and a fresh safety check succeeds.

Required test: kill during migration -> doctor diagnosis -> explicit repair -> migration succeeds.

## 18. Level-cap wire semantics

Hero Level 50 and Skill Level 10 remain display caps; XP continues accumulating.

Progress read models use:

```text
level
isLevelCapped
levelXp
nextLevelXpRequired?  # omitted when capped
```

No fake zero/infinite threshold.

## 19. Milestone flavor

The deterministic Domain emits semantic milestone events/keys only.

A hash-based flavor selector is removed from authoritative game rules. Curated/localized flavor is presentation and may evolve without changing historical XP/progression. Skill may lightly contextualize it without changing facts.

## 20. Local-first, sync-conscious

Replace “sync-ready” with **local-first, sync-conscious**.

The schema avoids obvious blockers but does not claim solved cross-device identity, causality, deletion or conflicts.

Future sync needs separate design for Project identity, device/origin, Hero/account namespace, open-Quest conflicts, tombstones, projection merge/rebuild, clocks, authentication and privacy.

No CRDT/event sourcing is added now.

## 21. Privacy metadata caveat

The strong deny-list remains, but bounded title, goal and summary can contain confidential project metadata. SafeText is Unicode/input hygiene, not semantic secret redaction.

## 22. Official dependency baseline

Keep the qualified implementation baseline:

```text
.NET SDK 10.0.302 / net10.0 / C# 14
ModelContextProtocol 2.2.0
EF Core SQLite / Microsoft.Data.Sqlite 10.0.10
SQLitePCLRaw.bundle_e_sqlite3 3.0.5
System.CommandLine 2.0.10
xunit.v3 3.2.2
```

`ModelContextProtocol 2.2.0` is an official stable C# SDK release published 2026-08-13 and was revalidated for the Task 7 adapter on 2026-09-06. Preferred MCP semantics remain `2026-07-28`, with `2025-11-25` compatibility qualification. Dependency refresh does not change the HP-MCP/2 application contract epoch.

## 23. RPG scope retained

Keep multiple Heroes, XP, Hero Level, Skill XP/Level, Rank, Trust/Strain, Success Streak, Traits, Titles and milestone flavor in 0.1 design.

Trust/Strain are RPG stats derived from bounded attestations, not objective productivity/reliability telemetry.

Traits/Titles/Streak move after the first playable vertical slice in implementation order; they are schedule-cut candidates, not architecture removals.

## 24. Risk-first implementation order

```text
0 freeze corrected contracts + dependency restore gate
1 scaffold/architecture guardrails
2 SQLite runtime + migration + connection policy
3 project-identity/1
4 bootstrap + typed settings + get_context + minimal Hero
5 minimal StartQuest
6 minimal FinishQuest with base XP only
7 real MCP adapter
8 minimal packaged Agent Skill
9 Codex E2E + restart/idempotency/concurrency/crash qualification

--- architecture checkpoint ---

10 full reward + Skill allocation
11 Hero/Skill progression + Rank
12 Trust/Strain
13 Streak/Traits/Titles
14 RU/EN localization/presentation
15 remaining Hero admin + CLI logical delete
16 backup/export/doctor + migration-lock repair
17 broader host qualification + release evidence
```

Every product-code task remains TDD-driven.

## 25. Readiness gate

Before the first product-code commit, active docs must agree on bootstrap replay, get_context, explicit heroId Start, Start Project/Hero hash scope, finishRequestId + HP136, receipt encoding/context/tombstone, all-Hero Project recovery, MCP delete removal/CLI logical delete, per-connection SQLite policy, trusted_schema=OFF, CHECK/FK/singleton schema, EF migration-lock recovery, level-cap wire shape, presentation-only flavor, sync-conscious wording and risk-first implementation order.
