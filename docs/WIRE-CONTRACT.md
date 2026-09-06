# Hero Passport — HP-MCP/2 v3.2.1 Wire Contract

**Status:** Accepted normative deep dive  
**Contract snapshot:** 2026-08-11  
**Contract epoch:** `HP-MCP/2`  
**SDK implementation baseline:** official C# `ModelContextProtocol 2.2.0`  
**SDK qualification refresh:** 2026-09-06  
**Preferred MCP semantics:** `2026-07-28`; release qualification also covers `2025-11-25`

This file is the field/schema/result source of truth for the model-facing contract.

## 1. Protocol rules

Hero Passport leaves protocol negotiation to the official SDK and never makes application correctness depend on MCP sessions/connections.

Successful calls return canonical `structuredContent` plus one deterministic serialized JSON `TextContent` compatibility block that is semantically equal to it. JSON whitespace/minification is not business semantics.

Expected validation/business failures return `isError=true`, one safe TextContent and no structuredContent.

Protocol/framing/unknown-tool errors remain SDK-level MCP/JSON-RPC errors.

## 2. Current tool inventory/order

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

The order is static/explicit for this contract snapshot. The number of tools is not a permanent architecture invariant.

No assembly-wide scanning, dynamic aliases or host-specific tool names.

Removed from model-facing MCP in v3.2.1:

```text
hero.delete
hero.list_active_quests
```

Permanent deletion is CLI-only. Recovery/settings hydration use `hero.get_context`.

## 3. Annotation matrix

Annotations are hints, never security controls.

| Tool | readOnly | destructive | idempotent | openWorld |
|---|---:|---:|---:|---:|
|`hero.bootstrap`|false|false|true|false|
|`hero.configure`|false|false|true|false|
|`hero.get_context`|true|false|true|false|
|`hero.create`|false|false|true|false|
|`hero.list`|true|false|true|false|
|`hero.activate`|false|false|true|false|
|`hero.archive`|false|false|true|false|
|`hero.restore`|false|false|true|false|
|`hero.start_quest`|false|false|true|false|
|`hero.finish_quest`|false|false|true|false|
|`hero.get_card`|true|false|true|false|

`bootstrap`, `create`, `start_quest` and `finish_quest` use caller-generated request identities. Same request identity with changed canonical scope/arguments is rejected.

## 4. JSON Schema profile

Use closed shallow schemas:

```text
object root
properties + required
additionalProperties:false at every object layer
closed enums
integer min/max
array min/max/unique
simple patterns
```

Runtime validation is authoritative. No current success field is emitted as null; optional meaning is represented by documented field absence/empty arrays.

## 5. SafeTextV1

Stored/model-returned user/model text:

1. rejects invalid Unicode scalars/unpaired surrogates;
2. rejects non-whitespace C0/C1 controls including NUL/DEL;
3. rejects bidi controls U+061C, U+200E/F, U+202A..E, U+2066..9;
4. normalizes NFC;
5. trims Unicode whitespace;
6. collapses whitespace runs to ASCII space;
7. counts Unicode scalars after normalization;
8. enforces field bounds.

Bounds:

```text
Hero name    1..64 scalars
Quest title  1..120 scalars
Quest goal   1..500 scalars
summary      1..2000 scalars
displayText  tool-specific, max 4000 scalars
```

## 6. IDs, timestamps, numeric ceiling

Current exposed application IDs/request IDs use lowercase canonical UUIDv7. Server-generated IDs use `.NET Guid.CreateVersion7()`.

Timestamps emitted by HP-MCP use `YYYY-MM-DDTHH:mm:ss.fffZ`.

Long-lived nonnegative JSON integers are bounded by `0..9_007_199_254_740_991`; checked arithmetic fails before overflow.

## 7. Canonical enums

Quest type:

```text
planning
research
coding
review
debugging
documentation
maintenance
```

Quest result:

```text
success
partial
blocked
failed
abandoned
```

Build/test status:

```text
not_run
passed
failed
unknown
```

Attestation/evidence provenance:

```text
observed
reported
none
```

Presentation style:

```text
rpg_engineering
classic_rpg
minimal
```

MVP locale:

```text
ru-RU
en-US
```

Canonical Skills are the ten keys in `ENGINE-SPEC.md`.

## 8. Attestation consistency

These are bounded agent attestations, not independently verified evidence.

```text
status = not_run -> evidence MUST be none
status = passed | failed -> evidence MUST be observed | reported
status = unknown -> evidence MAY be observed | reported | none

testsStatus != not_run -> testsMentioned MUST be true
```

Only `testsStatus=passed && testsEvidence=observed` satisfies the observed-tests reward bonus.

`observed` means the agent asserts it directly ran/saw the result.

## 9. Mutation request identity

Caller request IDs:

```text
bootstrapRequestId
createRequestId
startRequestId
finishRequestId
```

For every persisted receipt:

```text
same request ID + same canonical scope/args -> persisted replay
same request ID + changed canonical scope/args -> HP135 idempotency_conflict
```

Canonical mutation hashing is defined in `DATA-MODEL.md` and persists `args_encoding_version`.

Request IDs are scoped by operation kind and are not authentication credentials.

# 10. `hero.bootstrap`

Purpose: crash-safe first-run creation.

Input:

```json
{
  "bootstrapRequestId": "019...",
  "locale": "ru-RU",
  "heroName": "Nova",
  "presentationStyle": "rpg_engineering",
  "autoStartQuest": true,
  "autoFinishQuest": true
}
```

All fields required.

Runtime:

```text
same receipt + same args -> replay
same receipt + changed args -> HP135
no receipt + setup already complete -> HP002 setup_already_completed
otherwise atomically create initial Hero, make active, persist settings + receipt
```

Success:

```text
setupCompleted: true
hero { heroId, name }
settings { locale, presentationStyle, autoStartQuest, autoFinishQuest }
replayed: bool
displayText
```

# 11. `hero.configure`

Purpose: post-setup preference changes only.

Input:

```json
{
  "locale": "ru-RU",
  "presentationStyle": "rpg_engineering",
  "autoStartQuest": true,
  "autoFinishQuest": true
}
```

No Hero name/resource creation field exists.

Before setup: HP001. Repeating identical complete settings is a no-op success.

Success:

```text
settings { locale, presentationStyle, autoStartQuest, autoFinishQuest }
changed: bool
displayText
```

# 12. `hero.get_context`

Read-only hydration/recovery/version surface. Available before and after setup.

Input: closed empty object.

It resolves the invocation-bound Project identity but **must not create/update durable Project state** merely because this read occurs.

Success shape:

```text
productVersion
contractVersion = HP-MCP/2
skillContractVersion = hero-passport-skill/1
setupCompleted
settings? { locale, presentationStyle, autoStartQuest, autoFinishQuest }
activeHero? { heroId, name }
project { displayName }
openQuests[] {
  questId, heroId, heroName,
  questType, title, goal,
  startedAtUtc, locale
}
ruleVersions { ... }
displayText
```

`openQuests` spans all Heroes for the current Project, ordered by `startedAtUtc ASC, questId ASC`. At most one row per Hero exists by DB invariant; total cardinality is `0..N`.

Before setup, optional settings/activeHero are absent as documented and `setupCompleted=false`.

# 13. `hero.create`

Input:

```json
{
  "createRequestId": "019...",
  "name": "CodeMage"
}
```

Creation is retry-safe by receipt and does not automatically activate the Hero.

Success:

```text
hero { heroId, name, level, rankKey, trust, strain, archived }
replayed: bool
displayText
```

If the Hero was later permanently deleted and a stale create request is retried, the surviving receipt may return a safe previously-committed-then-deleted outcome; it never recreates the Hero.

# 14. `hero.list`

Input: closed empty object.

Read-only; never mutates Project/app state.

Success:

```text
heroes[] {
  heroId, name, archived, active,
  totalXp, level, rankKey, trust, strain
}
displayText
```

Order: active first, then non-archived, then createdAtUtc ASC, then heroId ASC.

# 15. `hero.activate`

Input:

```json
{"heroId":"019..."}
```

Target must exist and not be archived. Repeating activation is no-op success.

Activation only changes the default Hero preference for future Start formation. It never moves/closes/reassigns open Quests.

# 16. `hero.archive`

Input: `{"heroId":"019..."}`.

Guards:

- Hero exists;
- no open Quest owned by that Hero in any Project;
- Hero is not current active default (activate another first).

Repeated archive is success.

# 17. `hero.restore`

Input: `{"heroId":"019..."}`.

Repeated restore is success. Restore does not activate automatically.

# 18. Permanent delete is not an MCP tool

0.1 permanent logical Hero deletion is CLI-only.

MCP exposes reversible archive/restore. Future reintroduction of model-controlled destructive delete requires a separately qualified human-confirmation design (for example MRTR) and a new contract revision.

# 19. `hero.start_quest`

Input:

```json
{
  "startRequestId": "019...",
  "heroId": "019...",
  "questType": "coding",
  "title": "Добавить onboarding",
  "goal": "Добавить first-run onboarding для CLI и Skill без нарушения stdio."
}
```

All fields required.

ProjectId is invocation-bound and omitted from model-visible input, but is part of canonical request scope/hash.

Semantics:

```text
validate + SafeText
resolve ProjectId
BEGIN writer
lookup receipt(start_quest, startRequestId)
  found:
    same ProjectId + HeroId + canonical args under stored encoding -> original Quest replay
    changed context/args -> HP135
  absent:
    require setup complete
    validate explicit Hero exists/not archived
    snapshot current locale
    open Quest for explicit HeroId+ProjectId? -> HP133
    insert Quest + receipt + projection update
COMMIT
```

Current active Hero is never re-read to decide ownership. A replay after active-Hero/locale changes returns the original Quest.

Success:

```text
quest { questId, heroId, questType, title, goal, startedAtUtc, locale }
hero { heroId, name, level, rankKey }
replayed: bool
displayText
```

# 20. `hero.finish_quest`

Input:

```json
{
  "finishRequestId": "019...",
  "questId": "019...",
  "result": "success",
  "summary": "Added first-run setup and recovery.",
  "metrics": {
    "testsMentioned": true,
    "scopeViolations": 0,
    "userCorrections": 0,
    "buildStatus": "passed",
    "buildEvidence": "observed",
    "testsStatus": "passed",
    "testsEvidence": "observed"
  },
  "skillsUsed": ["coding", "testing_awareness", "scope_control"]
}
```

`skillsUsed`: 1..3 unique canonical keys ordered primary/secondary/tertiary.

Finish resolves invocation-bound ProjectId and loads Quest by questId. Current active Hero never replaces persisted ownership.

Semantics:

```text
canonicalize finish payload + hash
BEGIN writer
receipt(finish_quest, finishRequestId)?
  same args -> persisted replay
  changed args -> HP135
load Quest + verify ProjectId
if Quest already finalized:
  payload equals persisted finalization hash
    -> persist/accept this request receipt and return original result, alreadyFinalized=true
  payload differs
    -> HP136 quest_already_finalized_conflict
else:
  calculate current deterministic rules once
  atomically persist report/components/Skill deltas/XP event/unlocks/projections
  persist finalization hash + finish receipt
  mark Quest finished
COMMIT
```

No overwrite of finalized history.

Success shape:

```text
questId
result
replayed
alreadyFinalized
reward { baseXp, bonusXp, penaltyXp, rawXp, outcomePermille, xpGained, rewardRuleVersion, components[] }
heroProgress {
  heroId, totalXpBefore, totalXpAfter,
  levelBefore, levelAfter,
  isLevelCapped,
  levelXp,
  nextLevelXpRequired?   # omitted at Hero cap
  rankBefore, rankAfter
}
trustStrain { trustBefore, trustAfter, strainBefore, strainAfter, components[], ruleVersion }
streak { before, after, ruleVersion }
skillProgress[] {
  skillKey, xpGained, xpAfter,
  levelBefore, levelAfter,
  isLevelCapped,
  nextLevelXpRequired?  # omitted at Skill cap
}
traitsUnlocked[]
titlesUnlocked[]
activeTitle?
milestones[] { eventKey, semanticKey }
displayText
```

`activeTitle` is omitted when no Title is active. Flavor prose/key selection is presentation, not authoritative engine output.

# 21. `hero.get_card`

Input:

```json
{"heroId":"019..."}
```

Read-only. Does not rely on current active default after receiving explicit heroId.

Success:

```text
hero {
  heroId, name,
  totalXp, level, isLevelCapped, levelXp, nextLevelXpRequired?,
  rankKey, activeTitle?,
  trust, strain, successStreak,
  topSkills[] { skillKey, xp, level, isLevelCapped, nextLevelXpRequired? },
  traits[], titles[]
}
project {
  displayName,
  questsStarted, questsFinished, questsSucceeded,
  totalXpEarned,
  successRatePermille,
  topSkills[]
}
displayText
```

`nextLevelXpRequired` is omitted when the corresponding Hero/Skill level is capped. `activeTitle` is omitted when no Title is active.

No project internal ID/fingerprint/path is exposed.

## 22. Setup gate

Before setup:

```text
hero.get_context -> allowed
hero.bootstrap   -> allowed
all other HP-MCP tools -> HP001 setup_required
```

After setup, a fresh bootstrap request -> HP002 setup_already_completed.

## 23. Errors

Stable application errors include:

```text
HP001 setup_required
HP002 setup_already_completed
HP100 invalid_request
HP110 invalid_quest_type
HP111 invalid_result
HP112 invalid_skill
HP120 invalid_metrics
HP130 quest_not_found
HP133 active_quest_exists
HP134 quest_context_mismatch
HP135 idempotency_conflict
HP136 quest_already_finalized_conflict
HP140 hero_not_found
HP141 hero_archived
HP143 hero_has_open_quest
HP145 active_hero_protected
HP200 storage_unavailable
HP202 database_busy
HP203 storage_full
HP204 storage_read_only
HP205 storage_io_error
HP206 database_corrupt
HP207 storage_constraint
HP208 unsupported_sqlite_version
HP210 app_data_unavailable
HP211 unsupported_storage_location
HP300 invalid_configuration
HP310 invalid_project_binding
HP311 git_repository_unavailable
HP312 git_required_for_repository_binding
HP313 bare_repository_unsupported
HP900 internal_error
```

Do not expose raw SQL, stack traces, absolute paths, request dumps, prompts or secrets.

## 24. Contract snapshots

Implementation-generated snapshots under `contracts/mcp/hp-mcp-2/` cover:

```text
current exact order/annotations
input/output schemas
SafeText/UUID/time/integer rules
bootstrap replay/mismatch
get_context pre/post setup
Start Project/Hero idempotency scope
Finish request replay + HP136 semantic conflict
one-open Hero+Project behavior
level-cap optional-field semantics
structuredContent compatibility TextContent semantic equality
read-only no-write behavior
forbidden-field absence
2026-07-28 + 2025-11-25 qualification
```
