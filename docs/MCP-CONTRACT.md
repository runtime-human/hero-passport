# Hero Passport — MCP contract

**Status:** Accepted MVP contract  
**Protocol target:** MCP `2026-07-28`  
**SDK:** official C# SDK `ModelContextProtocol 2.0.0`  
**Transport:** stdio only in 0.1.0  
**Contract revision:** HP-MCP/1

## 1. Design rules

Hero Passport MCP is intentionally small. It exposes the minimum stateful agent workflow and nothing else.

Canonical tool order:

```text
hero.start_quest
hero.finish_quest
hero.current_quest
hero.get_card
```

This order is stable. MCP 2026-07-28 recommends deterministic list ordering because clients can cache tool catalogs and prompt caches are more effective when the inventory is stable.

The tool set is compile-time registered explicitly. Do not use assembly-wide scanning for MVP.

The MCP surface is **not** a mirror of CLI/dashboard features.

---

## 2. Why the contract changed from the first architecture draft

The first draft repeated application/host concerns inside every tool request/response (`schemaVersion`, `heroId`, `projectId`, `locale`, `outputMode`, `agentHint`). This is unnecessary token overhead and creates choices that the model should not make repeatedly.

The final contract removes them.

### Removed from normal tool input

```text
schemaVersion
heroId
projectId
workspacePath
locale
outputMode
```

### Removed from normal tool output

```text
schemaVersion
agentHint
statusText (duplicate of displayText)
```

Reasons:

- MCP already has a protocol/schema contract;
- active hero and project are local application context;
- workspace path is resolved locally;
- locale/presentation are local config;
- workflow guidance belongs in server instructions + AGENTS/skill guidance;
- duplicate human text wastes tokens.

Breaking tool changes after 0.1.0 use compatibility evolution, not a caller-supplied `schemaVersion` switch in every call.

RPG/database rule versions remain persisted because historical interpretation is a different concern.

---

## 3. MCP 2026-07-28 state model

The protocol core is stateless. Hero Passport application state is explicit.

Flow:

```text
hero.start_quest
  -> returns questId

agent works normally

hero.finish_quest(questId, ...)
  -> finishes that explicit quest
```

No correctness depends on:

```text
Mcp-Session-Id
initialize/initialized hidden state
client connection identity
in-memory server session cache
```

If Codex reconnects/restarts, `questId` plus SQLite state is enough.

`hero.current_quest` is a recovery/convenience operation, not hidden session emulation.

---

## 4. Server instructions

Codex supports MCP server instructions and specifically recommends putting cross-tool workflows/constraints there. The first 512 characters should be self-contained.

Canonical semantic content:

```text
Use Hero Passport for meaningful coding, debugging, review, planning, research, or documentation work. Start one quest before the work, keep the returned questId, and finish that quest once when done. Do not send source code, diffs, raw logs, prompts, secrets, environment variables, or workspace paths. Show the returned displayText briefly in the final response.
```

Implementation may shorten wording but must preserve these semantics.

Important:

- instructions guide models;
- instructions are not a security boundary;
- forbidden data is also absent from schemas and blocked by validation/logging/storage design.

---

## 5. JSON Schema policy

Tool schemas target full JSON Schema 2020-12 semantics supported by MCP 2026-07-28.

Rules:

1. Root input is always an object.
2. `additionalProperties: false` on every tool input object.
3. No generic `metadata`, `context`, `extra`, `payload` bag.
4. Enums are closed.
5. Text and collection lengths are bounded.
6. Integer counters are bounded and non-negative.
7. Output uses `outputSchema` and canonical `structuredContent`.
8. Server validates semantic conditions not expressible cleanly in schema.
9. External `$ref` is not used.
10. Tool schema complexity is intentionally shallow.

---

## 6. Tool annotations

MCP annotations are hints, not enforcement. Hero Passport sets them accurately for client UX/retry decisions while keeping real guarantees in server code.

| Tool | readOnly | destructive | idempotent | openWorld | taskSupport |
|---|---:|---:|---:|---:|---|
| `hero.start_quest` | false | false | true* | false | forbidden |
| `hero.finish_quest` | false | false | true | false | forbidden |
| `hero.current_quest` | true | false | true | false | forbidden |
| `hero.get_card` | true | false | true | false | forbidden |

`* start_quest` idempotency is application-defined: retrying the same logical start when the matching active quest already exists returns it rather than creating another. A conflicting new goal/type while an incompatible quest is open returns a conflict instead of silently creating duplicate state.

`destructive=false` for start/finish because these operations add progression/history; they do not delete/overwrite arbitrary user data. The underlying hero aggregate changes, but the business effect is additive progression with an immutable event/report trail.

All four tools are `openWorld=false`: they operate only on local Hero Passport state and do not access arbitrary network/external entities.

Tasks are forbidden: operations are short local database calls and should complete within an ordinary tool request.

---

## 7. Common output philosophy

Every success result has a machine-readable typed object and one concise human-facing `displayText` value inside it.

`structuredContent` is canonical for machines.

Backward compatibility:

MCP recommends a TextContent representation when structured content is returned. During implementation, inspect actual C# SDK 2.0 behavior with MCP Inspector/Codex. Prefer the SDK-supported representation that:

- stays conformant;
- avoids doubling a large JSON payload unnecessarily;
- still gives clients without structured rendering a useful fallback.

Do **not** create two semantically equivalent fields such as `statusText` + `displayText`.

Result DTOs contain only fields needed to continue the workflow or render the compact status.

---

## 8. `hero.start_quest`

### Purpose

Start the current meaningful agent work as one RPG quest, or return the matching already-open quest for an idempotent retry.

### Description budget

Target <= 300 UTF-8-visible characters in English.

Suggested meaning:

```text
Start one local RPG quest for the current meaningful agent task. Returns an explicit questId and compact hero status. Repeated matching calls return the open quest. Never send code, diffs, raw logs, secrets or workspace paths.
```

### Input

```json
{
  "questType": "coding",
  "goal": "Implement XpCalculator"
}
```

### Input schema

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

Whitespace-only goal is rejected semantically even if JSON Schema `minLength` is satisfied.

### Success output

```json
{
  "questId": "0198...",
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

### Output constraints

- `questId`: canonical UUID string generated from UUIDv7 internally;
- `alreadyOpen`: boolean;
- `displayText`: <= 300 characters in compact mode;
- no goal echo unless there is a demonstrated UX need; avoid reflecting untrusted text by default.

### Semantics

Resolution sequence:

```text
validate input
resolve active/default hero locally
resolve project identity locally
check active quest for hero+project
  if matching logical quest -> return same questId, alreadyOpen=true
  if conflicting active quest -> HP132 quest_conflict
create quest
persist short transaction
return card projection
```

What constitutes “matching logical quest” is versioned application policy and is tested. Initial rule: normalized quest type + normalized goal exact match within the same hero/project active slot.

---

## 9. `hero.finish_quest`

### Purpose

Finish an explicit quest, calculate deterministic reward exactly once, persist the result atomically and return compact progression.

### Suggested description

```text
Finish an existing Hero Passport quest and award deterministic local RPG progress once. Retry-safe: a finished quest returns its original persisted outcome. Send only a short summary, counters and canonical skill keys—never code/diffs/raw logs/secrets.
```

### Input

```json
{
  "questId": "0198...",
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

### Input schema intent

```json
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "questId": {
      "type": "string",
      "minLength": 36,
      "maxLength": 36
    },
    "result": {
      "type": "string",
      "enum": ["success", "partial", "failed", "blocked", "abandoned"]
    },
    "summary": {
      "type": "string",
      "minLength": 1,
      "maxLength": 2000
    },
    "metrics": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "testsMentioned": { "type": "boolean" },
        "scopeViolations": { "type": "integer", "minimum": 0, "maximum": 20 },
        "userCorrections": { "type": "integer", "minimum": 0, "maximum": 20 },
        "buildStatus": {
          "type": "string",
          "enum": ["not_run", "passed", "failed", "unknown"]
        },
        "testsStatus": {
          "type": "string",
          "enum": ["not_run", "passed", "failed", "unknown"]
        }
      },
      "required": [
        "testsMentioned",
        "scopeViolations",
        "userCorrections",
        "buildStatus",
        "testsStatus"
      ]
    },
    "skillsUsed": {
      "type": "array",
      "minItems": 1,
      "maxItems": 3,
      "uniqueItems": true,
      "items": {
        "type": "string",
        "maxLength": 64
      }
    }
  },
  "required": ["questId", "result", "summary", "metrics", "skillsUsed"]
}
```

`skillsUsed` accepts documented canonical keys and a small documented alias set handled by `SkillKeyNormalizer`. Unknown values are rejected rather than persisted as invented skills.

### Success output

```json
{
  "questId": "0198...",
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
    { "skill": "coding", "xpGained": 47 },
    { "skill": "testing_awareness", "xpGained": 29 },
    { "skill": "scope_control", "xpGained": 19 }
  ],
  "traitsUnlocked": [],
  "displayText": "✨ +95 XP · Nova ур.1 · XP 95/100 · Доверие 51 · Риск 19"
}
```

Compact `displayText` target <= 600 characters; normal presentation may be richer but remains bounded and locally configured.

### Retry semantics

If quest is already finished:

```text
return alreadyFinished=true
return original persisted reward/report projection
DO NOT rerun reward rules
DO NOT insert another xp_event
DO NOT mutate skill XP
DO NOT mutate traits
DO NOT mutate trust/risk
```

This is required even if the current engine rule version differs from the one originally used.

---

## 10. `hero.current_quest`

### Purpose

Read the active quest for the locally resolved hero/project so a client can recover workflow context after reconnect/restart.

### Input

Recommended empty-object schema:

```json
{
  "type": "object",
  "additionalProperties": false
}
```

### Output when active

```json
{
  "hasActiveQuest": true,
  "quest": {
    "questId": "0198...",
    "questType": "coding",
    "goal": "Implement XpCalculator",
    "startedAtUtc": "2026-08-10T17:00:00Z"
  },
  "displayText": "🧭 Активный квест · coding · начат 17:00 UTC"
}
```

### Output when none

```json
{
  "hasActiveQuest": false,
  "quest": null,
  "displayText": "Активного квеста нет."
}
```

Privacy note: goal is user/agent-provided compact text and can be returned here because it is part of the quest record. It must still be bounded and never treated as trusted instructions by the server.

---

## 11. `hero.get_card`

### Purpose

Read the compact local hero progression card.

### Input

```json
{
  "type": "object",
  "additionalProperties": false
}
```

### Output

```json
{
  "hero": {
    "name": "Nova",
    "level": 1,
    "totalXp": 95,
    "levelXp": 95,
    "levelXpRequired": 100,
    "trust": 51,
    "risk": 19
  },
  "topSkills": [
    { "skill": "coding", "xp": 47 },
    { "skill": "scope_control", "xp": 19 }
  ],
  "traits": [],
  "displayText": "Nova · ур.1 · XP 95/100 · Доверие 51 · Риск 19"
}
```

Compact result target <= 800 characters serialized excluding MCP framing.

Do not expose full quest history in this tool.

---

## 12. Error contract

### MCP protocol errors

Malformed JSON-RPC/protocol framing/capability errors are handled as protocol errors by the SDK.

### Tool/business errors

Valid tool calls that fail business/application validation return MCP tool-error semantics (`isError = true`) with a compact stable Hero Passport error code.

Canonical codes:

```text
HP100 invalid_request
HP110 hero_not_found
HP120 project_not_resolved
HP130 quest_not_found
HP131 no_open_quest
HP132 quest_conflict
HP140 unsupported_quest_type
HP141 unsupported_result
HP142 unsupported_skill

HP200 storage_unavailable
HP201 migration_failed
HP202 database_busy
HP210 app_data_unavailable

HP300 unsupported_config_version
HP301 invalid_config
HP302 config_unavailable

HP900 internal_error
```

Error text:

- concise;
- no stack trace;
- no SQL;
- no full local path;
- no serialized request;
- no environment data;
- no secrets.

---

## 13. Tool registration manifest

Composition root owns one canonical manifest:

```text
HeroPassportMcpManifest
  ProtocolContract = "HP-MCP/1"
  Tools = [
    StartQuestTool,
    FinishQuestTool,
    CurrentQuestTool,
    GetCardTool
  ]
```

The implementation registers the four tool types explicitly through the official SDK generic/type APIs. No `WithToolsFromAssembly()`/assembly-wide registration in MVP.

Startup/test invariant:

```text
actual list names == canonical list names
actual order == canonical order
no duplicate names
all four have annotations
all four have output schemas
all input schemas reject additional properties
all task support is forbidden
```

---

## 14. Tool compatibility policy

After 0.1.0, tool names and schemas are public compatibility surface.

### Additive compatible change

Examples:

- optional output field;
- optional input field with safe default, only if it does not confuse tool selection;
- richer description without semantic change.

Requires schema golden update + agent eval.

### Tool rename

Preferred sequence:

```text
release N: new canonical name + deprecated old alias
release N+1: keep alias and document removal window
future breaking release: remove alias
```

Do not keep aliases forever if they increase tool inventory/context burden. The compatibility window is explicit.

### Breaking semantic change

Create a new contract/tool version only when unavoidable. Do not overload old names with incompatible meaning.

Because no product implementation exists before 0.1.0, the architecture phase may still simplify names/contracts without compatibility aliases.

---

## 15. Token budgets

Token efficiency is tested through serialized size budgets because tokenization varies by model.

Initial character/byte-oriented gates:

```text
each tool description              <= 300 chars
total 4-tool catalog JSON          target <= 10 KiB
start compact displayText           <= 300 chars
finish compact displayText          <= 600 chars
current compact displayText         <= 300 chars
card compact displayText            <= 800 chars
summary input                       <= 2000 chars
goal input                           <= 500 chars
skillsUsed                          <= 3 items
```

The exact catalog budget is validated after the official C# SDK generates the real schemas. If generated metadata exceeds the target, first simplify schemas/descriptions rather than hiding tools dynamically.

No `hero.log_step` or per-file/event telemetry tool exists.

---

## 16. Security properties

MCP contract has no fields capable of intentionally transporting:

```text
source code
file contents
diffs/patches
raw logs
raw prompts/chat history
API keys/secrets
environment map
workspace path
arbitrary metadata object
```

`goal` and `summary` are still untrusted text. They are:

- length bounded;
- stored as data;
- parameterized through EF/SQLite;
- never injected into tool descriptions/server instructions;
- not logged by default;
- escaped/rendered as ordinary text.

---

## 17. Testing contract

MCP tests must inspect the **actual advertised server**, not only DTO classes.

Required checks:

1. `tools/list` exact names/order.
2. Exact annotations.
3. JSON Schemas reject unknown fields.
4. Output schemas exist and actual structured results conform.
5. Empty-input tools reject non-empty unknown objects.
6. `finish_quest` retry is identical except retry indicator.
7. malformed IDs return tool error, not server crash.
8. no non-protocol stdout bytes.
9. descriptions/catalog stay inside size budget.
10. MCP Inspector smoke.
11. real Codex E2E.
12. agent workflow evals for tool-selection behavior.

---

## 18. Not in MVP MCP

Explicitly absent:

```text
resources
prompts
MCP Apps
Tasks
HTTP transport
OAuth
sampling
roots
MCP logging
subscriptions
notifications-dependent state
hero.get_history
hero.log_step
hero.track_file
hero.evaluate_quality
hero.get_achievements
admin/reset/delete tools
```

MCP 2026-07-28 deprecates roots/sampling/logging in favor of newer patterns; Hero Passport does not build new architecture on them.

## 19. Primary sources

- MCP 2026-07-28 release: https://blog.modelcontextprotocol.io/posts/2026-07-28/
- MCP Tools specification: https://modelcontextprotocol.io/specification/draft/server/tools
- MCP tool annotations guidance: https://blog.modelcontextprotocol.io/posts/2026-03-16-tool-annotations/
- official C# SDK API: https://csharp.sdk.modelcontextprotocol.io/
- official C# SDK repository: https://github.com/modelcontextprotocol/csharp-sdk
- OpenAI Codex MCP documentation: https://developers.openai.com/codex/mcp/
