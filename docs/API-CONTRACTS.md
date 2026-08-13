# Hero Passport — API Contracts

**Status:** Accepted application semantics v3.2.1  
**Snapshot:** 2026-08-11

Exact model-facing MCP fields are normative in `WIRE-CONTRACT.md`; persistence/project identity are separate focused contracts.

## 1. Layers

```text
Domain rules
  ↓
Application commands/queries
  ↓
adapters
  ├─ HP-MCP/2
  ├─ CLI / --json
  └─ Web read models (0.2)
```

Do not force one DTO across layers.

## 2. Application operations

MCP-facing semantic use cases:

```text
BootstrapApplication
ConfigureApplication
GetRuntimeContext
CreateHero
ListHeroes
ActivateHero
ArchiveHero
RestoreHero
StartQuest
FinishQuest
GetHeroCard
```

CLI/admin also owns:

```text
DeleteHeroPermanently
GetDiagnostics
RepairMigrationLock
ExportData
BackupData
GetDataPath
```

Permanent logical delete is not model-facing in 0.1.

## 3. Operation context

Project-bound operations receive a resolved `ProjectId` from `project-identity/1`.

`activeHeroId` is a default preference only. New Start receives explicit `heroId`; existing Quest operations always use persisted Quest ownership.

Client/agent identity is diagnostic only and never Hero identity, authorization, ownership or reward input.

## 4. Mutation request identity

```text
BootstrapApplication(bootstrapRequestId, settings + heroName)
CreateHero(createRequestId, name)
StartQuest(startRequestId, heroId, questType, title, goal, Context(ProjectId))
FinishQuest(finishRequestId, questId, result, summary, attestations, skills, Context(ProjectId))
```

Same request ID + same canonical operation scope/arguments -> persisted replay.

Same request ID + changed scope/arguments -> `HP135 idempotency_conflict`.

Canonical hashing persists `args_encoding_version` and never depends on JSON serializer formatting.

## 5. Bootstrap

```text
receipt replay/mismatch check inside writer
fresh request + setup already complete -> HP002
otherwise create initial Hero + settings + active preference + receipt atomically
```

Crash-after-commit retry therefore converges.

Configure is preference-only after setup.

## 6. Runtime context

`GetRuntimeContext` is read-only and works before/after setup.

Returns versions/setup/settings/default Hero/current Project display data/open Quests across **all Heroes for that Project**/rule versions.

It must not create/update durable Project rows or bookkeeping simply because the read occurs.

## 7. StartQuest

Conceptual input:

```text
Context(ProjectId + invocation origin)
startRequestId
heroId
questType
title
goal
```

ProjectId + explicit HeroId are part of the idempotency scope.

```text
resolve ProjectId
canonicalize
writer transaction
receipt replay/mismatch
validate setup/explicit Hero
snapshot locale
one-open Hero+Project check
create Quest + receipt + projection atomically
```

No current-active-Hero lookup determines ownership.

`HP133` means an open Quest already occupies that Hero+Project slot.

## 8. FinishQuest

Conceptual input:

```text
Context(ProjectId + invocation origin)
finishRequestId
questId
result
summary
bounded attestations/provenance
ordered Skills
```

```text
writer transaction
finish receipt replay/mismatch
load Quest + verify Project binding
already finalized + equivalent payload -> original result / alreadyFinalized
already finalized + different payload -> HP136
otherwise run current deterministic rules exactly once
commit final report/ledger/receipt/all progression atomically
```

Current active Hero is irrelevant to loaded Quest ownership.

## 9. Hero management

Create does not auto-activate.

Activate changes only the default Hero preference for future Start formation.

Archive/Restore are reversible idempotent state setters. Archive rejects active default Hero and any Hero with open Quest(s).

Permanent logical delete is explicit CLI administration. It rejects active/open-Quest Heroes, marks relevant surviving receipts `target_deleted`, deletes Hero-owned history/projections and does not claim forensic storage erasure.

## 10. Hero card

`GetHeroCard(heroId, Context(ProjectId))` uses explicit HeroId.

Returns global Hero progression plus bounded current-project projection.

At Hero/Skill cap, `isLevelCapped=true` and `nextLevelXpRequired` is absent.

No path/fingerprint/internal ProjectId/raw history/source/log data.

## 11. Attestations

Build/test/scope/correction inputs are bounded agent attestations/reported signals.

`observed` means caller agent asserts direct observation; it is not independent Core verification.

## 12. Errors

Application uses typed safe errors:

```text
Code
Category
Retryability
MessageKey
SafeDetails? allowlisted only
```

Important codes are in `WIRE-CONTRACT.md`, including HP001/HP002/HP133/HP135/HP136.

Never include SQL, absolute paths, request bodies, source, prompts, secrets or environment data in model-facing details.

## 13. IDs/time/integer range

```text
IDs       typed UUIDv7 wrappers
Time      injected .NET TimeProvider, UTC persistence
JSON ints 0..9_007_199_254_740_991 where exposed/long-lived
```

Domain never reads wall clock directly.

## 14. Version axes

Independent versions include:

```text
Hero Passport product
MCP protocol revision
HP-MCP/2
hero-passport-skill/1
config schema
EF migration
project-identity/1
SafeTextV1
mutation-args/1
reward/2.0.0
hero-progression/2.0.0
skill-progression/2.0.0
skill-allocation/1.0.0
trust-strain/1.0.0
streak/1.0.0
unlock/2.0.0
rank/1.0.0
```

Game balance changes never rewrite historical completed Quest results.
