# Hero Passport — MCP Contract

**Status:** Accepted HP-MCP/2 design  
**Snapshot:** 2026-08-11  
**SDK baseline:** official C# `ModelContextProtocol 2.0.0`  
**Preferred semantics:** MCP `2026-07-28`  
**0.1.0 transport:** stdio

## 1. Contract objective

HP-MCP/2 is a deliberately small, portable model-facing contract. It is designed to work through any conforming host that can run a local stdio MCP server and honor the basic tools feature.

Canonical tool inventory and order:

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

The inventory is compile-time explicit and static in 0.1.0.

No assembly-wide discovery. No dynamic toolsets. No host-specific tool names.

---

## 2. Protocol revision strategy

Hero Passport is designed against the current `2026-07-28` MCP semantics but does not hard-pin the server to that single revision.

Implementation policy:

```text
McpServerOptions.ProtocolVersion = null / unset
```

Rationale: official C# SDK v2 can negotiate its supported initialize-era protocol revisions and the `2026-07-28` per-request metadata era. Pinning the server to `2026-07-28` would deliberately reject initialize-era clients; pinning an older version would reject 2026-style requests.

Tests must exercise at least:

```text
2026-07-28
2025-11-25 compatibility path
```

### 2.1 Application state is protocol-session independent

Hero Passport correctness never depends on:

```text
Mcp-Session-Id
client connection identity
McpServer instance lifetime
in-memory per-client dictionaries
initialize-session state
```

Stateful workflow uses an explicit `questId` handle:

```text
start_quest -> questId
                 ↓
            finish_quest
```

This remains correct across process reconnects because SQLite is authoritative.

### 2.2 `Stateless` terminology

Do not write code that assumes a generic `Stateless` property exists on every transport configuration.

For the 0.1.0 stdio process, the invariant is simply: **no application correctness depends on protocol sessions**.

When Streamable HTTP is implemented later, configure the C# SDK HTTP transport explicitly in stateless mode rather than relying on defaults.

---

## 3. MCP feature profile

Required baseline capability:

```text
Tools
```

Optional client features are not required for the core lifecycle.

0.1.0 does not depend on:

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

Roots, Sampling and MCP Logging are deprecated in the 2026 protocol line; project binding is therefore not designed around Roots.

Tasks are inappropriate because all Hero Passport tools are short local operations.

---

## 4. Tool-list behavior and caching

Tool order is deterministic and identical for every 0.1 local invocation.

For 2026-era `tools/list` responses:

```text
cacheScope = public
```

The initial implementation may use a five-minute `ttlMs` (300000) to match a conservative static-list policy, but **TTL is implementation freshness policy, not an HP-MCP semantic guarantee**. It may be tuned after interoperability evidence without changing HP-MCP/2.

Do not advertise dynamic list changes while the inventory is static.

If future authorization makes tool visibility user-specific, `cacheScope=public` must be re-reviewed before such behavior ships.

---

## 5. Interoperable JSON Schema profile

The protocol supports full JSON Schema 2020-12, but Hero Passport intentionally uses a conservative subset to maximize host/tool-parser compatibility.

Use:

```text
object root
properties
required
additionalProperties:false
string minLength/maxLength
enum
integer minimum/maximum
boolean
array minItems/maxItems/uniqueItems
small nested objects
```

Avoid unless a real requirement appears:

```text
oneOf/anyOf/allOf
if/then/else
recursive schemas
external $ref
patternProperties
very deep nesting
non-object input roots
```

No-param tools use an empty closed object:

```json
{
  "type": "object",
  "additionalProperties": false
}
```

Output schemas remain object-root even though 2026 structured content can represent any JSON value.

---

## 6. Common result representation

Successful tools return:

1. canonical typed machine data in `structuredContent`;
2. one concise human/legacy text representation rendered by App presentation.

Do not duplicate the entire structured JSON into text.

Do not create redundant fields such as:

```text
statusText + displayText + message + summaryText
```

The wire DTO may expose `displayText` inside structured content if useful to clients, but it has exactly one canonical human-facing value.

All human text is bounded.

---

## 7. Tool annotations

Annotations are truthful hints, not security controls.

| Tool | readOnly | destructive | idempotent | openWorld | Tasks |
|---|---:|---:|---:|---:|---|
| `hero.start_quest` | false | false | true* | false | unsupported |
| `hero.finish_quest` | false | false | true | false | unsupported |
| `hero.list_active_quests` | true | false | true | false | unsupported |
| `hero.get_card` | true | false | true | false | unsupported |

`start_quest` is idempotent with respect to one open logical work item: matching hero/project/LogicalQuestKey returns the same open quest.

---

## 8. Server instructions

Server-wide instructions contain the cross-tool lifecycle, not each tool response.

The first 512 characters must remain self-contained for Codex interoperability and should convey approximately:

```text
Use Hero Passport for meaningful coding, debugging, review, planning, research or documentation work. Start one quest for each logical work item, keep its questId, and finish that quest once. Several distinct quests may be active in one project. If you lose a questId, list active quests. Never send source code, diffs, raw logs, prompts, secrets, environment values or workspace paths.
```

Instructions are model guidance, not a security boundary.

---

# 9. `hero.start_quest`

## Purpose

Create one open quest for the current logical work item, or return the already-open matching quest.

## Input

```json
{
  "questType": "coding",
  "goal": "Implement XpCalculator"
}
```

## Input schema

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "questType": {
      "type": "string",
      "enum": [
        "planning",
        "research",
        "coding",
        "review",
        "debugging",
        "documentation",
        "maintenance"
      ]
    },
    "goal": {
      "type": "string",
      "minLength": 1,
      "maxLength": 500
    }
  },
  "required": ["questType", "goal"]
}
```

Whitespace-only goal is rejected semantically.

## Success structured content

```json
{
  "questId": "0198f2c8-8b61-7aa1-8c14-18a1e860a31a",
  "alreadyOpen": false,
  "hero": {
    "name": "Nova",
    "level": 1,
    "levelXp": 0,
    "levelXpRequired": 100,
    "trust": 50,
    "risk": 20
  },
  "displayText": "🧭 Квест начат · Nova ур.1 · XP 0/100"
}
```

## Semantics

```text
resolve HeroOperationContext
validate
calculate LogicalQuestKeyV1
matching open key exists -> return it, alreadyOpen=true
active quest count >= 16 -> HP133
otherwise insert open quest
```

A different goal/type no longer conflicts merely because another quest is open.

Concurrent identical starts converge through the database partial uniqueness rule.

Tool description should mention that repeated same-work starts return the open quest and that forbidden payloads must not be sent. Keep description concise.

---

# 10. `hero.finish_quest`

## Purpose

Complete an explicit quest, award deterministic progression exactly once and persist the entire outcome atomically.

## Input

```json
{
  "questId": "0198f2c8-8b61-7aa1-8c14-18a1e860a31a",
  "result": "success",
  "summary": "Implemented XP calculation and tests.",
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

## Schema constraints

```text
questId: canonical UUID text, exact validated parse
result: success | partial | failed | blocked | abandoned
summary: 1..2000 chars, non-whitespace
scopeViolations: 0..20
userCorrections: 0..20
buildStatus/testsStatus: not_run | passed | failed | unknown
skillsUsed: 1..3 unique strings, each <=64 chars, canonical/known alias only
additionalProperties:false at every object level
```

## Success structured content

```json
{
  "questId": "0198f2c8-8b61-7aa1-8c14-18a1e860a31a",
  "alreadyFinished": false,
  "reward": {
    "xpGained": 95,
    "levelBefore": 1,
    "levelAfter": 1,
    "leveledUp": false,
    "totalXp": 95,
    "levelXp": 95,
    "levelXpRequired": 100
  },
  "trust": 51,
  "risk": 19,
  "skillXp": [
    {"skill": "coding", "xpGained": 47},
    {"skill": "testing_awareness", "xpGained": 29},
    {"skill": "scope_control", "xpGained": 19}
  ],
  "traitsUnlocked": [],
  "displayText": "✨ +95 XP · Nova ур.1 · XP 95/100 · Доверие 51 · Риск 19"
}
```

## Context safety

Before finishing:

```text
quest exists?                         else HP130
quest HeroId == resolved HeroId?      else HP134
quest ProjectId == resolved ProjectId? else HP134
```

`questId` is not an authentication secret and cannot override local project/hero binding.

## Retry semantics

If already finished:

```text
alreadyFinished=true
return original persisted outcome
never rerun reward rules
never insert another xp_event
never mutate skills/traits/trust/risk again
```

---

# 11. `hero.list_active_quests`

## Purpose

Recover active work for the locally bound hero/project after reconnect, handoff or parallel-agent work.

## Input

```json
{}
```

## Success structured content

```json
{
  "quests": [
    {
      "questId": "0198f2c8-8b61-7aa1-8c14-18a1e860a31a",
      "questType": "coding",
      "goal": "Implement XpCalculator",
      "startedAtUtc": "2026-08-11T02:12:30Z"
    }
  ],
  "displayText": "🧭 Активных квестов: 1"
}
```

Semantics:

- returns only current HeroOperationContext hero/project;
- empty list is success;
- max 16 entries;
- deterministic ordering: `startedAtUtc DESC`, then `questId ASC`;
- `displayText` does not echo arbitrary goal text by default;
- structured goal is the previously stored bounded quest goal and must be treated as untrusted data by presentation layers.

This supersedes architecture-v2 `hero.current_quest` before the first public contract release.

---

# 12. `hero.get_card`

## Purpose

Read current hero progression for the locally resolved context.

## Input

```json
{}
```

## Success structured content

Conceptually:

```json
{
  "hero": {
    "name": "Nova",
    "level": 1,
    "totalXp": 95,
    "levelXp": 95,
    "levelXpRequired": 100,
    "trust": 51,
    "risk": 19,
    "topSkills": [],
    "traits": []
  },
  "displayText": "Nova · ур.1 · XP 95/100 · Доверие 51 · Риск 19"
}
```

Read-only and bounded. Detailed history belongs to CLI/Web, not this tool.

---

## 13. Error mapping

Valid `tools/call` requests that fail semantically return MCP tool-error semantics with a concise safe text representation and, when supported cleanly by SDK contract, structured safe error data.

Examples:

```text
[HP130] Quest not found.
[HP133] Active quest limit reached. Finish an existing quest before starting another.
[HP134] Quest belongs to a different locally bound hero/project context.
[HP202] Local database is busy. Retry after the competing local operation finishes.
```

Do not expose:

```text
stack trace
raw SQL
connection string
absolute workspace/database path
request dump
secrets
```

Protocol/framing/unknown-tool errors remain protocol errors handled by the MCP SDK.

---

## 14. Privacy/schema deny-list

Contract tests fail if any MCP input/output schema introduces a property whose normalized name matches or semantically represents:

```text
workspacePath
sourceCode
fileContent
diff
patch
rawLog
prompt
chatHistory
env/environment
secret/apiKey/token
metadata/context/payload/extra generic bag
```

Necessary typed fields must be reviewed explicitly rather than bypassing the deny-list through vague names.

---

## 15. Compatibility and contract snapshots

After tool registration exists, tests generate canonical snapshots from the actual SDK tool manifest/schemas. Documentation examples are explanatory; generated snapshots are executable drift gates.

Minimum compatibility matrix:

```text
MCP 2026-07-28      required qualified path
MCP 2025-11-25      required SDK compatibility path
MCP Inspector       required protocol smoke
Codex               required reference-host E2E
other hosts         release smoke according to integrations/README.md
```

A public tool rename after release requires compatibility strategy. The `current_quest -> list_active_quests` rename occurs now specifically because no public 0.1.0 contract exists yet.
