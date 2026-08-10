# Hero Passport — MCP Contract

**Status:** Accepted for MVP  
**Baseline:** official MCP C# SDK 2.0.0 / protocol revision 2026-07-28  
**Transport:** stdio only in MVP

## 1. Design goals

The MCP surface exists to wrap the Hero Passport session lifecycle, not to expose the database or create a generic game API.

Goals:

- four tools only;
- deterministic tool order;
- concise descriptions and bounded JSON schemas;
- explicit idempotency;
- structured result for machines + `displayText` for humans;
- no source/diff/log payloads;
- no protocol-session dependency for application state;
- strict stdout discipline.

## 2. Tool registry order

The server returns tools in this fixed order:

```text
1. hero.start_quest
2. hero.finish_quest
3. hero.current_quest
4. hero.get_card
```

Do not rely on reflection/assembly scan order unless the SDK guarantees the required deterministic ordering. If necessary, register the tools explicitly in order.

## 3. Server instructions

Keep the first 512 characters self-contained because clients may truncate server instructions.

Recommended text:

```text
Hero Passport tracks local RPG progress for meaningful agent work. Call hero.start_quest once at the start and hero.finish_quest once after the work. Do not send source code, diffs, raw logs, secrets, environment variables, file contents, or full prompts/chat history. Show only displayText from Hero Passport results; never dump raw structured JSON. Use current_quest only for recovery and get_card only when status is requested.
```

## 4. Common contract conventions

### 4.1 Schema version

MVP accepts:

```json
"schemaVersion": "1.0"
```

Missing `schemaVersion` may default to `1.0` only while all supported clients are first-party/known. Once more than one schema is supported, callers must send it explicitly. Responses always include it.

Unsupported major version -> `HP101 unsupported_schema_version`.

### 4.2 Locale

Accepted MVP values:

```text
ru
en
```

Default: `ru` for the initial product profile. Locale affects presentation only.

### 4.3 Output mode

```text
compact  default
normal
verbose
```

MCP agent workflow should use `compact`. `verbose` is not intended for automatic routine calls.

### 4.4 IDs

JSON IDs are canonical lowercase UUID strings. The server generates UUIDv7 for new persistent records.

### 4.5 String bounds

MVP maximums:

```text
goal                  500 chars
summary              2000 chars
host.name               64 chars
host.type               64 chars
idempotencyKey         128 chars
project display name   160 chars
hero display name       80 chars
```

Reject oversized input; do not silently truncate data used for scoring/audit.

### 4.6 Unknown JSON fields

Input schemas use `additionalProperties: false` where supported. Contract evolution happens through additive versioned properties, not silently ignored arbitrary payloads.

## 5. `hero.start_quest`

### 5.1 Description

```text
Start a local RPG quest for the current meaningful agent task. Creates or returns an idempotent open quest and a compact hero card. Stores quest metadata only; never send source code, diffs, raw logs, secrets, or file contents.
```

### 5.2 Behavioral annotations

Conceptual tool annotations:

```text
readOnlyHint      false
destructiveHint   false
idempotentHint    true
openWorldHint     false
```

Annotations are hints, not authorization/security controls.

### 5.3 Input

```json
{
  "schemaVersion": "1.0",
  "heroId": "auto",
  "projectId": "auto",
  "questType": "coding",
  "goal": "Implement XpCalculator",
  "idempotencyKey": "optional-client-key",
  "host": {
    "name": "codex",
    "type": "coding-agent"
  },
  "outputMode": "compact",
  "locale": "ru"
}
```

`host` is informational and bounded. The MVP contract intentionally has **no workspacePath field**. The server already runs locally and resolves the working project from its execution environment/config. If a future host genuinely cannot provide an appropriate process working directory, add a privacy-reviewed path hint in a later schema rather than normalizing path disclosure now.

### 5.4 Idempotency

Preferred identity:

```text
(heroId, projectId, idempotencyKey)
```

When `idempotencyKey` is absent, the fallback rule is one active quest per `(heroId, projectId)` for the automatic agent workflow. A repeated call returns that open quest with `alreadyOpen = true` rather than creating a duplicate.

If a caller attempts to start a materially different quest while an active quest exists and supplies a different explicit idempotency key, return a compact `HP132 quest_conflict` rather than guessing which quest to replace.

### 5.5 Response

```json
{
  "schemaVersion": "1.0",
  "questId": "019...",
  "alreadyOpen": false,
  "hero": {
    "heroId": "019...",
    "name": "Nova",
    "level": 1,
    "totalXp": 0,
    "levelXp": 0,
    "nextLevelXp": 100,
    "trust": 50,
    "risk": 20
  },
  "statusText": "🧭 Квест начат · Nova ур.1 · XP 0/100",
  "displayText": "🧭 Квест начат: Implement XpCalculator",
  "agentHint": "Show displayText only. Do not print raw Hero Passport JSON."
}
```

## 6. `hero.finish_quest`

### 6.1 Description

```text
Finish a local Hero Passport quest and deterministically apply XP, skills, traits, trust and risk. Idempotent: retrying a finished quest returns the original persisted outcome and never grants rewards twice.
```

### 6.2 Annotations

```text
readOnlyHint      false
destructiveHint   false
idempotentHint    true
openWorldHint     false
```

### 6.3 Input

```json
{
  "schemaVersion": "1.0",
  "questId": "019...",
  "result": "success",
  "summary": "Implemented XpCalculator with xUnit v3 tests and verified the requested scope.",
  "metrics": {
    "testsMentioned": true,
    "scopeViolations": 0,
    "userCorrections": 0,
    "buildStatus": "passed",
    "testsStatus": "passed"
  },
  "skillsUsed": ["coding", "scope_control", "testing_awareness"],
  "outputMode": "compact",
  "locale": "ru"
}
```

Do **not** repeat `questType` in the finish request. The quest type is immutable state established at start and loaded by `questId`; accepting it again creates conflict ambiguity.

### 6.4 Result values

```text
success
partial
failed
blocked
abandoned
```

### 6.5 Build/tests statuses

```text
passed
failed
not_run
unknown
```

These statuses are reported metadata in rule `1.0.0`. `testsMentioned` controls the current test bonus; `testsStatus` is persisted for future rule evolution and transparency. A future rule may distinguish tests actually passed from merely mentioned, but that is a versioned scoring change.

### 6.6 Skills

Input array max length: 3 after normalization. Duplicate aliases collapse to one canonical key.

Supported initial canonical keys:

```text
planning
research
coding
review
debugging
documentation
maintenance
testing_awareness
scope_control
tool_use
```

Unknown keys are ignored with a warning field only in normal/verbose output; they do not create database skill rows dynamically.

### 6.7 Response

```json
{
  "schemaVersion": "1.0",
  "questId": "019...",
  "alreadyFinished": false,
  "rewardRuleVersion": "1.0.0",
  "xpGained": 95,
  "reward": {
    "baseXp": 60,
    "resultMultiplierPermille": 1000,
    "resultXp": 60,
    "bonuses": [
      { "key": "tests_mentioned", "xp": 10 },
      { "key": "clean_scope_bonus", "xp": 10 },
      { "key": "clear_summary", "xp": 10 },
      { "key": "no_user_corrections", "xp": 5 }
    ],
    "penalties": [],
    "finalXp": 95
  },
  "hero": {
    "name": "Nova",
    "level": 1,
    "totalXp": 95,
    "levelXp": 95,
    "nextLevelXp": 100,
    "trust": 51,
    "risk": 19
  },
  "skillChanges": [
    { "key": "coding", "xpGained": 47 },
    { "key": "scope_control", "xpGained": 29 },
    { "key": "testing_awareness", "xpGained": 19 }
  ],
  "statusText": "✨ +95 XP · Nova ур.1 · XP 95/100 · Доверие 51 · Риск 19",
  "displayText": "## Hero Passport\n\n✨ Квест завершён: +95 XP\nNova · ур.1 · XP 95/100\nДоверие 51 · Риск 19\nНавыки: Кодинг +47, Контроль +29, Тесты +19\nСледующее: ур.2 через 5 XP",
  "agentHint": "Show displayText only. Do not print raw Hero Passport JSON."
}
```

### 6.8 Retry behavior

If the quest is already completed, return the **stored original response projection** based on its persisted report/reward event:

```text
alreadyFinished = true
xpGained = original amount
no new XP event
no new skill XP
no trust/risk mutation
no new trait progression
```

Do not recalculate the historical quest under the current rule version.

## 7. `hero.current_quest`

### 7.1 Description

```text
Read the current open Hero Passport quest for the resolved hero/project. Use only to recover task context; this tool does not change game state.
```

### 7.2 Annotations

```text
readOnlyHint      true
destructiveHint   false
idempotentHint    true
openWorldHint     false
```

### 7.3 Input

```json
{
  "schemaVersion": "1.0",
  "heroId": "auto",
  "projectId": "auto",
  "outputMode": "compact",
  "locale": "ru"
}
```

### 7.4 Output

When open:

```json
{
  "schemaVersion": "1.0",
  "hasOpenQuest": true,
  "quest": {
    "questId": "019...",
    "questType": "coding",
    "goal": "Implement XpCalculator",
    "startedAtUtc": "2026-08-10T17:00:00Z"
  },
  "displayText": "🧭 Текущий квест: Implement XpCalculator"
}
```

When none exists, this is a successful read, not an error:

```json
{
  "schemaVersion": "1.0",
  "hasOpenQuest": false,
  "quest": null,
  "displayText": "Открытого квеста нет."
}
```

## 8. `hero.get_card`

### 8.1 Description

```text
Read the compact local RPG card for a Hero Passport hero, including level, XP, trust/risk and top skills/traits. Does not change state.
```

### 8.2 Input

```json
{
  "schemaVersion": "1.0",
  "heroId": "auto",
  "projectId": "auto",
  "outputMode": "compact",
  "locale": "ru"
}
```

### 8.3 Output budget

Compact card returns:

- identity + level XP;
- trust/risk;
- at most 3 top skills;
- at most 3 active/unlocked trait summaries;
- project-specific stats only when `projectId` resolves;
- `displayText`.

Never return unbounded quest history from `get_card`.

## 9. Tool error result

For a valid MCP `tools/call` that cannot complete the requested domain operation, return tool-error semantics with a compact structured error:

```json
{
  "schemaVersion": "1.0",
  "error": {
    "code": "HP130",
    "key": "quest_not_found",
    "message": "Quest was not found."
  },
  "displayText": "Hero Passport: квест не найден."
}
```

Protocol framing/invalid JSON-RPC errors remain protocol errors handled by the SDK.

Do not include stack traces, SQL, local paths or request dumps in MCP error content.

## 10. Stdout contract

In `hero-passport mcp` mode:

```text
stdout = MCP transport only
stderr = diagnostics/logging only
exit code != 0 = fatal startup/runtime failure
```

Forbidden on stdout:

- banner/version text;
- Spectre rendering;
- EF migration logs;
- normal `Console.WriteLine` diagnostics;
- stack traces;
- "server started" messages.

A process integration test must start the executable and fail if any non-protocol bytes precede/follow MCP messages.

## 11. Token/prompt-cache policy

- Four tools, no dynamic tool set in MVP.
- Deterministic order.
- Tool descriptions should remain roughly <= 500 characters.
- Prefer field descriptions that state decision-relevant semantics, not marketing text.
- Bound lists and strings.
- Do not duplicate immutable state in later request schemas (`questType` is not resent at finish).
- Compact response is default.
- Do not add `hero.log_step`.

Any proposal for a fifth MVP tool must show why an existing tool/read model cannot satisfy the use case and estimate tool-context/token impact.

## 12. MCP 2026-07-28 implications

Hero Passport treats the protocol as stateless with respect to application continuity. Quest IDs are explicit application handles and SQLite stores persistent state.

For stdio, use the stable official C# SDK transport and allow its backward compatibility behavior. Do not implement custom initialize/session logic.

MCP Apps and MCP Tasks are extension packages in C# SDK v2 and are explicitly out of MVP. Logging/sampling/roots legacy server-initiated patterns are not dependencies of the design.

If/when Streamable HTTP is added later, begin stateless and require a new security/auth threat model; do not simply expose the local stdio tool server on a network port.
