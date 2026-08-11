# Hero Passport — MCP Contract

**Status:** Accepted HP-MCP/2 v3.1 overview  
**Snapshot:** 2026-08-11  
**SDK baseline:** official C# `ModelContextProtocol 2.0.0`  
**Preferred semantics:** MCP `2026-07-28`  
**0.1.0 transport:** stdio

Exact field/validation/result rules are normative in [`WIRE-CONTRACT.md`](WIRE-CONTRACT.md). This file describes the compact protocol surface and workflow.

---

## 1. Tool inventory

Exactly four explicitly registered tools in this stable order:

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

No assembly-wide scanning, dynamic toolsets or host-specific aliases.

Tool list is static in 0.1.0 and deterministic. Public cache scope is appropriate for the local unprivileged inventory; TTL remains implementation freshness policy and must be tested rather than treated as HP-MCP semantics.

---

## 2. Protocol revision policy

Design against MCP `2026-07-28` semantics while leaving:

```text
McpServerOptions.ProtocolVersion = null / unset
```

so the official SDK can negotiate its supported revisions.

Release tests cover:

```text
2026-07-28
2025-11-25 compatibility path
```

Application correctness never depends on:

```text
Mcp-Session-Id
connection identity
McpServer instance lifetime
initialize-session state
in-memory per-client workflow state
```

SQLite plus explicit `questId` is the state model.

---

## 3. Required/unused MCP features

Required core feature:

```text
Tools
```

0.1 does not depend on:

```text
Resources
Prompts
Roots
Sampling
MCP Logging
Tasks
MCP Apps
MRTR/elicitation
subscriptions
notifications/tools/list_changed
```

Roots/Sampling/Logging are not project/product correctness mechanisms. Tasks are unnecessary for short local calls.

---

## 4. Tool annotations — v3.1

| Tool | readOnly | destructive | idempotent | openWorld |
|---|---:|---:|---:|---:|
| `hero.start_quest` | false | false | **false** | false |
| `hero.finish_quest` | false | false | true | false |
| `hero.list_active_quests` | true | false | true | false |
| `hero.get_card` | true | false | true | false |

`hero.start_quest` is deliberately not annotated idempotent. While a matching normalized start declaration remains open, a retry returns that quest. After it has been finished, the same arguments may correctly create a new quest/history row.

`hero.finish_quest` is idempotent because a specific `questId` can be completed only once and every retry returns the original persisted outcome.

Annotations are hints, not authorization/security controls.

---

## 5. Input schemas

All input roots are objects and every object layer sets:

```json
"additionalProperties": false
```

Schemas stay shallow and use a conservative JSON Schema subset.

No-param tools use:

```json
{
  "type": "object",
  "additionalProperties": false
}
```

The official SDK-generated schema is **not runtime validation**. Tool boundaries validate explicitly according to `WIRE-CONTRACT.md`.

Forbidden routine MCP input concepts:

```text
heroId
projectId
workspacePath
locale
outputMode
schemaVersion
clientName/clientVersion
source/file contents
diff/patch
raw logs
prompts/chat history
secrets/tokens
environment bags
arbitrary metadata/context/payload bags
```

---

## 6. Result representation — v3.1 correction

Successful tool result:

```text
structuredContent = canonical result object
content            = exactly one TextContent containing minified JSON
                     semantically equal to structuredContent
displayText        = required human-facing field inside that object
```

This follows MCP's backward-compatibility guidance for tools that return structured content.

Do not return only a compact status string in TextContent while returning a different structured object.

Tool execution/validation/business error:

```text
isError=true
content = one safe actionable TextContent
structuredContent absent
```

Protocol/framing/unknown-tool errors remain SDK-level MCP protocol errors.

---

## 7. Server instructions

First 512 characters remain self-contained for the reference Codex path.

Canonical semantics:

```text
Use Hero Passport for meaningful coding, debugging, review, planning, research or documentation work. Start one quest for each work item, keep its questId, and finish that quest once. Several distinct quests may be active in one project. If you lose a questId, list active quests. Never send source code, diffs, raw logs, prompts, secrets, environment values or workspace paths.
```

Instructions are guidance, not a security boundary.

---

# 8. `hero.start_quest`

## Purpose

Start one quest for one normalized local work declaration.

Input:

```json
{
  "questType": "coding",
  "goal": "Implement XpCalculator"
}
```

Boundary rules:

```text
questType -> canonical closed enum
goal      -> SafeTextV1, 1..500 Unicode scalar values
```

Semantics:

```text
resolve local HeroOperationContext
normalize SafeTextV1 goal
compute QuestDedupKeyV1 (case preserved)
enter immediate writer transaction
same open dedup declaration -> existing quest, alreadyOpen=true
otherwise active count >=16 -> HP133
otherwise create quest -> alreadyOpen=false
```

A different declaration may coexist. The same arguments after the old quest is completed may create a new quest.

Success fields:

```text
questId
alreadyOpen
hero { name, level, levelXp, levelXpRequired, trust, risk }
displayText
```

Goal is not echoed in the start result.

---

# 9. `hero.finish_quest`

Input:

```json
{
  "questId": "0198c123-4567-7abc-8def-0123456789ab",
  "result": "success",
  "summary": "Implemented XP calculation and deterministic tests.",
  "metrics": {
    "testsMentioned": true,
    "scopeViolations": 0,
    "userCorrections": 0,
    "buildStatus": "passed",
    "testsStatus": "passed"
  },
  "skillsUsed": ["coding", "testing_awareness", "scope_control"]
}
```

Boundary highlights:

```text
questId      canonical lowercase UUIDv7
summary      SafeTextV1, 1..2000 scalars
counters     0..20
skillsUsed   1..3 canonical skills only, ordered primary->secondary->tertiary
testsStatus != not_run requires testsMentioned=true
```

Before reward:

```text
unknown quest -> HP130
wrong bound hero/project -> HP134
already finished -> original persisted outcome, alreadyFinished=true
```

New finish is one immediate writer transaction that atomically commits report, XP ledger, hero/skill/trait/project updates and finished state.

Success fields:

```text
questId
alreadyFinished
reward { xpGained, levelBefore, levelAfter, leveledUp, totalXp, levelXp, levelXpRequired }
trust
risk
skillXp[]
traitsUnlocked[]
displayText
```

---

# 10. `hero.list_active_quests`

Input:

```json
{}
```

Returns only the locally bound hero/project's active quests.

Success fields:

```text
quests[] { questId, questType, goal, startedAtUtc }
displayText
```

Rules:

```text
0..16 entries
startedAtUtc DESC
QuestId ASC tie-break
empty list is success
displayText does not echo arbitrary goal text
```

This is the explicit recovery/handoff path after restart or lost `questId`; do not use fuzzy semantic matching.

---

# 11. `hero.get_card`

Input:

```json
{}
```

Returns global hero progression plus a bounded projection of the currently bound project.

Success shape:

```text
hero {
  name, level, totalXp, levelXp, levelXpRequired,
  trust, risk,
  topSkills[], traits[]
}
project {
  displayName,
  questsFinished,
  questsSucceeded,
  totalXpEarned
}
displayText
```

No project internal ID, fingerprint or path is exposed.

---

## 12. Wire canonicalization

Exact rules are in `WIRE-CONTRACT.md`.

Important summary:

```text
SafeTextV1       NFC + safe single-line whitespace + scalar-aware bounds
QuestDedupKeyV1  quest type + case-preserved SafeText goal
UUID             canonical lowercase UUIDv7
Timestamp        YYYY-MM-DDTHH:mm:ss.fffZ
Long JSON int    <= 9_007_199_254_740_991
Enums            lower_snake_case closed keys
Current nulls    none
```

---

## 13. Error mapping

Examples:

```text
[HP100] Invalid Hero Passport tool input.
[HP130] Quest not found.
[HP133] Active quest limit reached. Finish an active quest and retry.
[HP134] Quest belongs to a different locally bound hero/project.
[HP202] Local database is busy. Retry shortly.
[HP203] Hero Passport storage is full. Free local disk space and retry.
[HP206] Hero Passport database failed an integrity check. Run doctor.
```

Never expose stack traces, raw SQL, connection strings, absolute paths, request dumps or secrets.

---

## 14. Snapshots and compatibility evidence

Implementation generates canonical snapshots under:

```text
contracts/mcp/hp-mcp-2/
```

They cover exact tools, order, annotations, input/output schemas, success/error result goldens and deterministic serialization.

Required release evidence:

```text
2026-07-28 protocol path
2025-11-25 compatibility path
MCP Inspector
Codex E2E
host smoke matrix from integrations/README.md
structuredContent == parsed compatibility JSON TextContent
```

`WIRE-CONTRACT.md` contains the exhaustive validation vectors.
