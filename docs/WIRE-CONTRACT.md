# Hero Passport — HP-MCP/2 v3.2 Wire Contract

**Status:** Accepted normative deep dive  
**Snapshot:** 2026-08-11  
**Contract epoch:** `HP-MCP/2`  
**SDK baseline:** official C# `ModelContextProtocol 2.1.0`  
**Preferred MCP semantics:** `2026-07-28`; release qualification also covers `2025-11-25`

This file is the field/schema/result source of truth for the model-facing contract.

## 1. Protocol rules

Hero Passport leaves protocol negotiation to the official SDK and does not make application correctness depend on MCP sessions/connections.

`questId` and explicit mutation request IDs are ordinary application handles/identities.

Every successful call returns:

```text
structuredContent = canonical result JSON value
content            = exactly one TextContent containing minified JSON
                     semantically equal to structuredContent
isError            = false/omitted according to SDK serialization
```

Every expected validation/business failure returns:

```text
isError            = true
content            = exactly one safe actionable TextContent
structuredContent  = absent
```

Protocol/framing/unknown-tool errors remain SDK-level JSON-RPC/MCP errors.

## 2. Tool inventory and order

Exactly this static explicit order:

```text
hero.configure
hero.create
hero.list
hero.activate
hero.archive
hero.restore
hero.delete
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

No assembly-wide scanning, dynamic aliases or host-specific names.

## 3. Tool annotation matrix

Annotations are hints, not security controls.

| Tool | readOnly | destructive | idempotent | openWorld |
|---|---:|---:|---:|---:|
|`hero.configure`|false|false|true|false|
|`hero.create`|false|false|true|false|
|`hero.list`|true|false|true|false|
|`hero.activate`|false|false|true|false|
|`hero.archive`|false|false|true|false|
|`hero.restore`|false|false|true|false|
|`hero.delete`|false|true|true|false|
|`hero.start_quest`|false|false|true|false|
|`hero.finish_quest`|false|false|true|false|
|`hero.list_active_quests`|true|false|true|false|
|`hero.get_card`|true|false|true|false|

`create`, `delete` and `start_quest` are retry-safe because their schemas carry caller-generated request identities. Same request identity with changed canonical arguments is rejected.

## 4. JSON Schema profile

Use closed shallow object schemas:

```text
object root
properties + required
additionalProperties:false at every object layer
closed enums
integer min/max
array min/max/unique
simple patterns
```

Runtime validation is authoritative; generated schemas/data annotations are not sufficient enforcement.

No current success field is emitted as `null`; absence or empty arrays have explicitly documented meaning.

## 5. SafeTextV1

Model/user-facing stored text uses the existing SafeTextV1 algorithm:

1. reject invalid Unicode scalar sequences/unpaired surrogates;
2. reject non-whitespace C0/C1 controls including NUL/DEL;
3. reject bidi controls U+061C, U+200E/F, U+202A..E, U+2066..9;
4. normalize NFC;
5. trim Unicode whitespace;
6. collapse internal whitespace runs to ASCII space;
7. count Unicode scalar values after normalization;
8. enforce per-field bounds.

Bounds:

```text
Hero name    1..64 scalars
Quest title  1..120 scalars
Quest goal   1..500 scalars
summary      1..2000 scalars
displayText  tool-specific, max 4000 scalars
```

Wire schema lengths remain a compatibility hint; Rune/scalar-aware runtime validation is authority.

## 6. IDs, timestamps and numeric ceiling

Public entity/mutation IDs use canonical lowercase UUIDv7 text:

```text
xxxxxxxx-xxxx-7xxx-[89ab]xxx-xxxxxxxxxxxx
```

Server IDs are generated with .NET `Guid.CreateVersion7()`.

Timestamps produced by HP-MCP use:

```text
YYYY-MM-DDTHH:mm:ss.fffZ
```

Long-lived nonnegative JSON integers are bounded by:

```text
0 .. 9_007_199_254_740_991
```

Checked arithmetic must fail safely before exceeding that ceiling.

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

Evidence:

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

## 8. Evidence consistency

Runtime rules:

```text
status = not_run -> evidence MUST be none
status = passed | failed -> evidence MUST be observed | reported
status = unknown -> evidence MAY be observed | reported | none

testsStatus != not_run -> testsMentioned MUST be true
```

Only `testsStatus=passed && testsEvidence=observed` satisfies the reward engine’s observed-tests bonus.

## 9. Mutation request identity

Caller-generated request IDs are used for operations where automatic retry could otherwise duplicate a resource/destructive command:

```text
createRequestId
startRequestId
deleteRequestId
```

For each operation, within its documented scope:

```text
same request ID + same canonical arguments -> semantically equivalent persisted result
same request ID + changed canonical arguments -> HP135 idempotency_conflict
```

The server stores request identity, canonical argument fingerprint and enough result identity to answer a late retry atomically with the mutation.

Mutation request IDs are not shared across operation kinds.

# 10. `hero.configure`

Purpose: first-run setup and mutable user preferences only.

Input:

```json
{
  "locale": "ru-RU",
  "presentationStyle": "rpg_engineering",
  "autoStartQuest": true,
  "autoFinishQuest": true,
  "initialHeroName": "Nova"
}
```

Properties:

```text
locale             required closed enum
autoStartQuest     required bool
autoFinishQuest    required bool
presentationStyle  required closed enum
initialHeroName    optional SafeTextV1 1..64
```

Runtime:

- if setup is incomplete, `initialHeroName` is required and this call creates the initial Hero atomically with settings;
- after setup, `initialHeroName` MUST be absent; this tool cannot rename/create Heroes;
- repeated post-setup identical settings are a no-op success.

Success:

```json
{
  "setupCompleted": true,
  "settings": {
    "locale": "ru-RU",
    "presentationStyle": "rpg_engineering",
    "autoStartQuest": true,
    "autoFinishQuest": true
  },
  "activeHero": {"heroId":"019...","name":"Nova"},
  "displayText":"Hero Passport настроен."
}
```

# 11. `hero.create`

Input:

```json
{
  "createRequestId": "019...",
  "name": "CodeMage"
}
```

`createRequestId` is required canonical UUIDv7. `name` is SafeTextV1 1..64.

Success:

```text
hero { heroId, name, level, rank, trust, strain, archived }
replayed: bool
displayText
```

Creation does not automatically activate the Hero.

# 12. `hero.list`

Input: closed empty object.

Success:

```text
heroes[] {
  heroId, name, archived, active,
  level, rankKey, totalXp, trust, strain
}
displayText
```

Order:

```text
active first
then non-archived before archived
then createdAtUtc ASC
then heroId ASC
```

# 13. `hero.activate`

Input:

```json
{"heroId":"019..."}
```

Hero must exist and not be archived. Repeating activation is success with no extra effect. Existing open Quests owned by another Hero are not moved/closed.

Success:

```text
activeHero { heroId, name }
alreadyActive: bool
displayText
```

# 14. `hero.archive`

Input:

```json
{"heroId":"019..."}
```

Guards:

- Hero exists;
- no open Quest owned by this Hero in any project;
- Hero is not the globally active Hero; activate another Hero first.

Repeated archive of an already archived Hero is success.

Success:

```text
heroId
archived: true
alreadyArchived: bool
displayText
```

# 15. `hero.restore`

Input:

```json
{"heroId":"019..."}
```

Repeated restore is success.

Success:

```text
heroId
archived: false
alreadyRestored: bool
displayText
```

Restoring does not activate automatically.

# 16. `hero.delete`

Permanent local deletion.

Input:

```json
{
  "deleteRequestId": "019...",
  "heroId": "019...",
  "confirmHeroName": "OldMage"
}
```

Guards:

- exact current SafeText-normalized Hero name must equal `confirmHeroName`;
- Hero is not globally active;
- Hero has no open Quest in any project.

The delete transaction removes the Hero’s local game/history rows and stores only the minimal mutation receipt needed to return a safe late retry (`deleteRequestId`, canonical argument fingerprint, deleted HeroId, deletion timestamp). It does not retain deleted Quest/history content.

Success:

```text
heroId
deletedAtUtc
replayed: bool
displayText
```

# 17. `hero.start_quest`

Input:

```json
{
  "startRequestId": "019...",
  "questType": "coding",
  "title": "Добавить onboarding",
  "goal": "Добавить first-run onboarding для CLI и MCP Skill без нарушения stdio."
}
```

All fields required.

Semantics:

```text
validate + SafeText
resolve globally active Hero
resolve local ProjectId
snapshot current locale
BEGIN writer
existing start request?
  same canonical args -> return persisted start result, replayed=true
  changed args        -> HP135
open Quest for Hero+Project?
  yes -> HP133 active_quest_exists
insert request + Quest + project start projection
COMMIT
```

Success:

```text
quest {
  questId, questType, title, goal, startedAtUtc, locale
}
hero { heroId, name, level, rankKey }
replayed: bool
displayText
```

`displayText` is a tiny start banner and may include the safe title.

# 18. `hero.finish_quest`

Input:

```json
{
  "questId": "019...",
  "result": "success",
  "summary": "Added first-run setup and locale persistence.",
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

`skillsUsed`: 1..3, unique, canonical, semantically ordered.

Finish resolves the process-bound ProjectId and loads the Quest by `questId`. **Current active Hero does not replace Quest ownership.** The Quest’s persisted HeroId receives progression. A Quest from another project returns context mismatch.

If already finished, return its persisted original result with `replayed=true`; never recalculate under current rules.

New finish is one atomic writer transaction.

Success shape:

```text
questId
result
replayed
reward {
  baseXp, bonusXp, penaltyXp, rawXp,
  outcomePermille, xpGained, rewardRuleVersion,
  components[] { key, xpDelta }
}
heroProgress {
  heroId, totalXpBefore, totalXpAfter,
  levelBefore, levelAfter,
  levelXp, levelXpRequired,
  rankBefore, rankAfter
}
trustStrain {
  trustBefore, trustAfter,
  strainBefore, strainAfter,
  components[] { key, trustDelta, strainDelta },
  ruleVersion
}
streak { before, after, ruleVersion }
skillProgress[] {
  skillKey, xpGained, xpAfter,
  levelBefore, levelAfter
}
traitsUnlocked[]
titlesUnlocked[]
activeTitle
milestones[] { eventKey, flavorKey }
displayText
```

All arrays may be empty; no invented minimum progress is emitted.

# 19. `hero.list_active_quests`

Input: closed empty object.

Resolves the globally active Hero + process-bound Project.

Success:

```text
quests[] { questId, questType, title, goal, startedAtUtc, locale }
displayText
```

Cardinality is exactly `0..1` by Core invariant.

This is the recovery/handoff tool. It is not a polling/telemetry loop.

# 20. `hero.get_card`

Input: closed empty object.

Returns current globally active Hero plus current process-bound Project projection:

```text
hero {
  heroId, name,
  totalXp, level, levelXp, levelXpRequired,
  rankKey, activeTitle,
  trust, strain, successStreak,
  topSkills[] { skillKey, xp, level },
  traits[], titles[]
}
activeQuest? {
  questId, questType, title, startedAtUtc
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

`activeQuest` is omitted when none exists. No project internal ID/fingerprint/path is exposed.

## 21. Setup gate

Before setup completion:

```text
hero.configure -> allowed
read-only diagnostic/version surfaces outside this MCP tool set -> host/CLI specific
all other HP-MCP tools -> HP001 setup_required
```

After setup all tools follow normal guards.

## 22. Error codes

Stable v3.2 application errors include:

```text
HP001 setup_required
HP100 invalid_request
HP110 invalid_quest_type
HP111 invalid_result
HP112 invalid_skill
HP120 invalid_metrics
HP130 quest_not_found
HP133 active_quest_exists
HP134 quest_context_mismatch
HP135 idempotency_conflict
HP140 hero_not_found
HP141 hero_archived
HP143 hero_has_open_quest
HP145 active_hero_protected
HP146 hero_confirmation_mismatch
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

Do not include raw SQL, stack traces, absolute paths, request dumps, prompts or secrets in model-facing errors.

## 23. Contract snapshots

Implementation-generated snapshots under:

```text
contracts/mcp/hp-mcp-2/
```

must cover:

```text
exact tool order
annotations
input/output schemas
SafeText/UUID/time/integer rules
all success goldens
business error shape
request-id replay + mismatch
one-open Quest behavior
structuredContent == parsed compatibility TextContent
forbidden-field absence
2026-07-28 and 2025-11-25 qualification
```
