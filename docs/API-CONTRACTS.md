# Hero Passport — API Contracts

**Status:** Accepted application semantics v3.2  
**Snapshot:** 2026-08-11

Exact MCP fields are in `WIRE-CONTRACT.md`; persistence and project binding are separate normative contracts.

## 1. Contract layers

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

```text
ConfigureApplication
CreateHero
ListHeroes
ActivateHero
ArchiveHero
RestoreHero
DeleteHero
StartQuest
FinishQuest
ListActiveQuests
GetHeroCard
InitializeApplication
GetDiagnostics
ExportData
```

First eleven map to HP-MCP/2 except `InitializeApplication/GetDiagnostics/ExportData`, which are CLI/admin in 0.1.

## 3. Context

Project-bound operations receive a resolved ProjectId. The resolver is Infrastructure and follows `project-identity/1`.

New Quest creation resolves the global active Hero. Existing Quest operations use the persisted Quest Hero; switching active Hero never transfers an open/finished Quest.

Client/agent identity is diagnostic only and never Hero identity, ownership, authorization or reward input.

## 4. Request identity

Resource/destructive operations that need safe retry carry caller request identity:

```text
CreateHero(createRequestId, name)
StartQuest(startRequestId, questType, title, goal)
DeleteHero(deleteRequestId, heroId, confirmHeroName)
```

Same request ID + same canonical args returns semantically equivalent persisted result. Same request ID + changed canonical args is `HP135 idempotency_conflict`.

## 5. StartQuest

Conceptual input:

```text
Context(ProjectId + invocation origin)
startRequestId
questType
title
goal
```

Semantics:

```text
resolve active Hero
canonicalize
writer transaction
request replay/mismatch check
one-open Hero+Project check
create Quest + receipt atomically
```

`HP133 active_quest_exists` means the caller must recover/finish/abandon the existing Quest, never silently replace it.

## 6. FinishQuest

Conceptual input:

```text
Context(ProjectId + invocation origin)
questId
result
summary
bounded metrics/provenance
ordered Skills
```

Semantics:

```text
writer transaction
load Quest by ID
verify Project binding
if finished -> persisted original result
otherwise run deterministic versioned rules once
commit report/ledger/all progression atomically
```

Current active Hero is not consulted for ownership of the loaded Quest.

## 7. Hero management

Create does not auto-activate.

Activate chooses the default owner of future Quests.

Archive/Restore are reversible and idempotent state setters. Archive rejects globally active Hero and any Hero with an open Quest.

Permanent Delete is destructive, request-idempotent, requires exact target-name confirmation, rejects active/open-Quest Heroes, and deletes that Hero’s local game/history data in one transaction.

## 8. ListActiveQuests

Returns `0..1` open Quest for current active Hero + current bound Project. Empty is success.

This is explicit recovery/handoff, not fuzzy semantic search.

## 9. Hero card

Returns global active Hero progression plus bounded current-project projection and optional active Quest.

No path/fingerprint/project internal ID/raw history/source/log data.

## 10. Errors

Application uses typed safe errors:

```text
Code
Category
Retryability
MessageKey
SafeDetails? allowlisted only
```

Important codes are enumerated in `WIRE-CONTRACT.md`.

Never put SQL, absolute paths, request bodies, source, prompts, secrets or environment values in model-facing details.

## 11. Time/IDs/integer range

```text
IDs       typed UUIDv7 wrappers
Time      injected .NET TimeProvider, UTC persistence
JSON ints 0..9_007_199_254_740_991 where long-lived/exposed
```

Domain never calls wall clock directly.

## 12. Version axes

Keep independent:

```text
Hero Passport product version
MCP negotiated protocol revision
HP-MCP/2 contract epoch
config schema version
EF migration id
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

Game balance changes never rewrite persisted historical Quest results.
