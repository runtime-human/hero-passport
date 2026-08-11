# Hero Passport — HP-MCP/2 Wire Contract Deep Dive

**Status:** Accepted normative deep-dive  
**Snapshot:** 2026-08-11  
**Contract epoch:** `HP-MCP/2`  
**Preferred MCP semantics:** `2026-07-28` with SDK compatibility negotiation

This document is the field-by-field wire source of truth for HP-MCP/2. It supersedes looser examples in earlier architecture documents where those examples disagree with the exact rules here.

---

## 1. Audit outcome

The field-by-field audit found five changes that must be made before implementation:

1. successful structured tool results must follow MCP's backward-compatibility guidance by returning the **same serialized JSON in TextContent** rather than an unrelated compact status string;
2. `hero.start_quest` is retry-safe while a matching quest remains open, but is **not globally idempotent across lifecycle state changes**, so `idempotentHint=false`;
3. the old `LogicalQuestKeyV1` name/semantics overclaimed semantic identity and case folding could merge case-sensitive code goals; it becomes **`QuestDedupKeyV1`**, preserving case;
4. generated JSON Schema is not runtime validation in the official C# SDK, so every boundary rule requires explicit server validation;
5. text, IDs, timestamps, integer ranges, arrays and error results need canonical wire rules rather than relying on host/library defaults.

---

## 2. Canonical tool inventory

Exactly, in this stable order:

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

No host-specific aliases.
No fifth administration tool.
No assembly-wide tool scanning.

---

## 3. Protocol compatibility

The ordinary server leaves:

```text
McpServerOptions.ProtocolVersion = null / unset
```

The official C# SDK is responsible for negotiating the supported MCP revision.

Required qualification paths:

```text
2026-07-28
2025-11-25 compatibility path
```

HP-MCP/2 uses only capability commonality needed by its tools. Optional MCP features are not correctness dependencies.

---

## 4. Result representation — corrected contract

### 4.1 Success

Every successful HP-MCP/2 call returns:

```text
structuredContent = canonical machine result object
content            = exactly one TextContent whose text is minified JSON
                     semantically equal to structuredContent
isError            = false or omitted according to SDK wire behavior
```

MCP Tools guidance says that a tool returning `structuredContent` SHOULD also return the serialized JSON in TextContent for backward compatibility.

Therefore the former pattern:

```text
structuredContent = rich object
TextContent        = only "✨ +95 XP ..."
```

is superseded.

Human presentation remains available as the required `displayText` field **inside** the structured object. Older/text-only clients therefore still receive the compact human status inside the serialized JSON without the server violating the backward-compatibility recommendation.

### 4.2 Equality rule

The TextContent JSON and `structuredContent` must deserialize to semantically equal JSON values.

Tests compare parsed JSON equality, not whitespace/property-order textual identity.

Use compact/minified serialization for TextContent.

### 4.3 Error

A normal tool/business/validation error returns:

```text
isError = true
content = exactly one safe TextContent
structuredContent = absent
```

Example:

```text
[HP133] Active quest limit reached. Finish an active quest and retry.
```

Why no structured error object: each tool advertises a success `outputSchema`; returning a differently shaped structured error risks violating the advertised output schema. HP-MCP/2 intentionally avoids a success/error union schema to keep interoperability simple.

Protocol/framing/unknown-tool errors remain MCP protocol errors handled by the SDK.

---

## 5. C# SDK validation boundary

The official C# SDK explicitly states that tool arguments are untrusted and that data annotations can influence generated JSON Schema **without enforcing runtime validation**.

Therefore:

```text
JSON Schema = advertised machine contract
explicit validator = runtime authority
Application/Domain = semantic authority
```

Do not treat `[Required]`, `[MaxLength]`, enum serialization or generated schemas as security/correctness enforcement by themselves.

Each tool validates and maps input before calling Application.

---

## 6. JSON Schema profile

HP-MCP/2 remains conservative even though MCP 2026 permits richer output schemas and arbitrary JSON structured values.

Use:

```text
object root
properties
required
additionalProperties:false
string minLength/maxLength
string pattern when simple and interoperable
enum
boolean
integer minimum/maximum
array minItems/maxItems/uniqueItems
small nested objects
format only as annotation/help, never sole validation
```

Avoid unless a future requirement proves necessary:

```text
oneOf
anyOf
allOf
if/then/else
recursive schemas
external $ref
patternProperties
dynamic references
polymorphic discriminators
non-object root outputs
```

Every nested object also sets:

```json
"additionalProperties": false
```

All current HP-MCP/2 output fields are required. HP-MCP/2 uses no `null` payload values.

Empty collection means “none”.

Future optional fields must be deliberately specified; do not emit `null` merely because a C# nullable property exists.

---

## 7. Safe model text — `SafeTextV1`

Model-supplied `goal` and `summary` are untrusted text and must be canonicalized before persistence/use.

### 7.1 Algorithm

For each input string:

1. enumerate valid Unicode scalar values; reject unpaired UTF-16 surrogates;
2. reject non-whitespace C0/C1 control characters, including NUL and DEL;
3. reject bidi formatting/control characters that can spoof rendered direction:
   - U+061C;
   - U+200E, U+200F;
   - U+202A..U+202E;
   - U+2066..U+2069;
4. normalize Unicode to NFC;
5. trim leading/trailing Unicode whitespace;
6. collapse each internal Unicode-whitespace run to one ASCII U+0020 space;
7. count Unicode scalar values after normalization;
8. enforce field-specific min/max bounds.

Whitespace control characters such as tab/newline may enter the validator but become a normal ASCII space; arbitrary control characters do not survive.

This produces compact single-line stored text and prevents terminal/log formatting surprises.

### 7.2 Why scalar count

JSON Schema `minLength`/`maxLength` are defined in terms of JSON string characters, not .NET UTF-16 code units. Runtime validation therefore uses Rune/scalar-aware counting rather than `string.Length`.

Tests include supplementary-plane characters/emoji to prove one scalar is not counted as two UTF-16 code units.

### 7.3 Bounds

```text
goal       1..500 Unicode scalar values after SafeTextV1
summary    1..2000 Unicode scalar values after SafeTextV1
displayText outputs have tool-specific bounds below
```

The normalized SafeText result is what is persisted. The product does not promise byte-for-byte preservation of original whitespace/control formatting.

---

## 8. `QuestDedupKeyV1` — correction to v3

The previous name `LogicalQuestKeyV1` suggested semantic task identity. A deterministic hash of natural-language text cannot establish semantic identity safely.

Case folding is particularly unsafe for coding goals:

```text
Fix UserId handling
Fix userId handling
```

may refer to different case-sensitive identifiers.

Therefore before public release:

```text
LogicalQuestKeyV1 -> QuestDedupKeyV1
logical_key       -> dedup_key
dedup key version -> 1
```

### 8.1 Algorithm

Input is already validated SafeTextV1 goal.

```text
canonicalQuestType = lower_snake_case enum key
canonicalGoal      = SafeTextV1 goal, CASE PRESERVED

QuestDedupKeyV1 = SHA-256(
  UTF8(canonicalQuestType + "\n" + canonicalGoal)
)
```

No lowercase/case-fold step.
No stemming.
No embeddings.
No punctuation removal.
No semantic model call.

### 8.2 Meaning

The key means:

> same hero + same project + same normalized start declaration while an equivalent quest remains open

It does **not** mean “these two descriptions are semantically the same task”.

A restart/handoff that reformulates the goal should use `hero.list_active_quests` to recover the explicit existing `questId` rather than relying on fuzzy deduplication.

---

## 9. UUID contract

All server-generated entity IDs exposed through HP-MCP/2 use UUIDv7 internally and canonical lowercase hyphenated text externally.

`questId` wire form:

```text
36 ASCII characters
xxxxxxxx-xxxx-7xxx-[89ab]xxx-xxxxxxxxxxxx
lowercase hex only
```

Input schema may include both:

```json
"format": "uuid"
```

and an anchored v7 ASCII pattern, but runtime validation is authoritative because JSON Schema `format` can be annotation-only depending on the validator.

Runtime rule:

- exact `D` representation;
- lowercase canonical round-trip;
- UUID version 7;
- RFC-compatible variant.

Malformed UUID is `HP100 invalid_request`, not quest-not-found.

A valid but unknown UUID is `HP130 quest_not_found`.

---

## 10. Timestamp contract

HP-MCP/2 only produces timestamps; current tools do not accept timestamps from the model.

Canonical output:

```text
YYYY-MM-DDTHH:mm:ss.fffZ
```

Properties:

- UTC only;
- uppercase `T` and `Z`;
- exactly three fractional-second digits;
- millisecond precision;
- no local offset;
- no `-00:00`;
- producer truncates higher precision to milliseconds rather than rounding across a second boundary.

This is a deliberately narrower producer profile of RFC 3339 for deterministic snapshots and compact output.

Schema may annotate:

```json
"format": "date-time"
```

but output tests enforce the canonical Hero Passport form.

Ordering uses the underlying persisted timestamp then quest UUID tie-breaker, not lexical assumptions about an arbitrary client-provided date string.

---

## 11. Integer interoperability

JSON permits numbers outside common binary64 exact-integer range, but RFC 8259 identifies the range below as interoperable across widespread IEEE-754 implementations:

```text
-(2^53 - 1) .. +(2^53 - 1)
```

HP-MCP/2 therefore constrains every integer value that could grow over product lifetime to:

```text
0 .. 9_007_199_254_740_991
```

This includes:

```text
totalXp
project totalXpEarned
skill XP
long-lived counters if ever exposed
```

Small request counters retain their much tighter bounds.

Persistence/domain checked arithmetic must prevent a value from exceeding the HP JSON-safe maximum.

This rule avoids a future state where a valid SQLite/.NET `long` value is silently rounded by JavaScript-like clients.

---

## 12. Closed enums

Canonical wire enums use lower_snake_case.

### Quest type

```text
planning
research
coding
review
debugging
documentation
maintenance
```

### Result

```text
success
partial
failed
blocked
abandoned
```

### Build/test status

```text
not_run
passed
failed
unknown
```

Definitions:

```text
not_run = positively known not to have been run
unknown = outcome unavailable/uncertain
passed  = self-reported passed
failed  = self-reported failed
```

These are compact agent-reported facts, not independent verification claims.

Unknown enum value -> `HP100 invalid_request` or the more specific stable application code when already defined.

---

## 13. Skills contract

MCP input advertises **canonical skills only**. Aliases exist for human CLI/import compatibility, not to make the model guess among multiple spellings.

Canonical HP-MCP/2 enum:

```text
coding
testing_awareness
scope_control
documentation
tool_use
planning
research
debugging
review
maintenance
```

`skillsUsed`:

```text
minItems = 1
maxItems = 3
uniqueItems = true
```

Array order is semantic:

```text
first  = primary/most relevant skill
second = secondary
third  = tertiary
```

The deterministic skill-XP weighting follows this order.

For a zero-XP abandoned/failed quest, declared skills may receive zero XP; do not invent minimum skill XP.

Unknown/alias MCP skill -> validation error. Application's `SkillKeyNormalizer` may still support aliases for non-MCP adapters.

---

## 14. Metrics consistency

Finish metrics:

```text
testsMentioned    boolean
scopeViolations   integer 0..20
userCorrections   integer 0..20
buildStatus       closed enum
testsStatus       closed enum
```

Semantic invariant:

```text
if testsStatus is passed | failed | unknown
then testsMentioned MUST be true
```

`testsStatus=not_run` may coexist with either value of `testsMentioned`:

- false = tests were not part of the reported work;
- true = tests were considered/mentioned but known not to have been run.

This retains the v1 RPG “testing awareness” signal without pretending it proves a passing test run.

The schema stays simple; this cross-field rule is enforced explicitly at runtime rather than via `if/then` JSON Schema.

---

## 15. Tool annotations — corrected matrix

| Tool | readOnly | destructive | idempotent | openWorld |
|---|---:|---:|---:|---:|
| `hero.start_quest` | false | false | **false** | false |
| `hero.finish_quest` | false | false | true | false |
| `hero.list_active_quests` | true | false | true | false |
| `hero.get_card` | true | false | true | false |

### Why start is not idempotent

While a matching dedup declaration is still open, retry returns that quest and causes no new effect.

However:

```text
start(args)
finish(quest)
start(the same args again)
```

legitimately creates a new quest/history row. Therefore “repeating the same arguments has no additional effect” is not globally true.

The correct description is:

```text
open-request retry-safe / deduplicated
```

not MCP `idempotentHint=true`.

### Why finish is idempotent

`questId` names a specific quest. Repeating finish for that same already-finished quest returns the original persisted outcome and causes no additional progression.

Annotations remain hints, not security boundaries.

---

# 16. `hero.start_quest`

## 16.1 Input object

```json
{
  "questType": "coding",
  "goal": "Implement XpCalculator"
}
```

Exact properties:

### `questType`

```text
type: string
required: yes
enum: canonical seven quest keys
```

### `goal`

```text
type: string
required: yes
schema minLength: 1
schema maxLength: 500
runtime: SafeTextV1, 1..500 scalars
```

No extra properties.

## 16.2 Start semantics

```text
validate
resolve HeroOperationContext locally
SafeTextV1(goal)
compute QuestDedupKeyV1
run immediate-writer Start transaction
matching open declaration -> same quest / alreadyOpen=true
otherwise cap check -> insert or HP133
```

## 16.3 Success object

Exact shape:

```json
{
  "questId": "0198c123-4567-7abc-8def-0123456789ab",
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

Bounds:

```text
hero.name        1..64 SafeText-compatible scalars
level            integer 1..JSON-safe max
a XP field       0..JSON-safe max
trust/risk       0..100
displayText      1..300 scalars
```

The goal is deliberately not echoed in start output.

---

# 17. `hero.finish_quest`

## 17.1 Input object

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

Exact field rules:

### `questId`

```text
required
canonical lowercase UUIDv7
```

### `result`

```text
required
closed result enum
```

### `summary`

```text
required
schema 1..2000 characters
runtime SafeTextV1 1..2000 scalars
```

### `metrics`

All five properties required. `additionalProperties:false`.

### `skillsUsed`

```text
required
1..3
unique
canonical enum only
order = relevance/weight order
```

No duplicate `questType` because it belongs to the persisted quest.

## 17.2 Success object

Exact shape:

```json
{
  "questId": "0198c123-4567-7abc-8def-0123456789ab",
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

Ordering:

```text
skillXp           = same semantic skill order as validated input/allocation
traitsUnlocked    = canonical trait key ascending
```

Bounds:

```text
skillXp items     1..3
traitsUnlocked    0..20
displayText       1..600 scalars
all long XP ints  0..JSON-safe max
trust/risk        0..100
```

A finished retry sets `alreadyFinished=true` and returns the original persisted reward projection; no recalculation.

---

# 18. `hero.list_active_quests`

## 18.1 Input

Exact empty object:

```json
{}
```

Input schema:

```json
{
  "type": "object",
  "additionalProperties": false
}
```

## 18.2 Success object

```json
{
  "quests": [
    {
      "questId": "0198c123-4567-7abc-8def-0123456789ab",
      "questType": "coding",
      "goal": "Implement XpCalculator",
      "startedAtUtc": "2026-08-11T04:31:12.345Z"
    }
  ],
  "displayText": "🧭 Активных квестов: 1"
}
```

Rules:

```text
quests             0..16
ordering           startedAtUtc DESC, then QuestId ASC using typed values
goal               persisted SafeTextV1, 1..500 scalars
startedAtUtc       canonical Hero timestamp
displayText        1..300 scalars
```

`displayText` does not echo arbitrary goal text.

An empty list is normal success.

---

# 19. `hero.get_card`

## 19.1 Input

Exact empty object.

## 19.2 Success object

The card includes global hero progress and a compact current-project projection because the operation context already resolves a project.

Exact shape:

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
    "topSkills": [
      { "skill": "coding", "xp": 47 }
    ],
    "traits": []
  },
  "project": {
    "displayName": "hero-passport",
    "questsFinished": 1,
    "questsSucceeded": 1,
    "totalXpEarned": 95
  },
  "displayText": "Nova · ур.1 · XP 95/100 · Доверие 51 · Риск 19"
}
```

No `projectId`, fingerprint or path is exposed.

Ordering/bounds:

```text
topSkills        0..5, xp DESC then canonical skill key ASC
traits           0..20, canonical trait key ASC
project name     1..128 SafeText-compatible scalars
counters/XP      0..JSON-safe max
displayText      1..600 scalars
```

Detailed history remains CLI/Web scope.

---

## 20. `displayText` contract

`displayText` is:

- human-facing;
- generated locally by App presentation;
- non-authoritative for machine logic;
- never parsed by Domain/Application;
- never used by Web as its data source;
- bounded;
- free of user goal/summary echo by default;
- included inside the machine object and therefore also inside compatibility JSON TextContent.

Machines must use typed fields, not parse `displayText`.

Presentation wording/punctuation may evolve without changing HP-MCP semantics as long as documented bounds and meaning remain compatible.

---

## 21. Output array determinism

Every array has explicit order:

```text
list_active_quests.quests
  startedAtUtc DESC
  QuestId ASC tie-break

finish.skillXp
  primary/secondary/tertiary input relevance order

finish.traitsUnlocked
  canonical trait key ASC

card.hero.topSkills
  XP DESC
  canonical skill key ASC tie-break

card.hero.traits
  canonical trait key ASC
```

Never depend on database incidental row order.

---

## 22. JSON object property ordering

Property ordering is kept stable in generated snapshots and serialized compatibility JSON for diffability/cache stability, but consumers must not treat JSON object member order as semantic.

Contract tests compare structural content and also snapshot canonical serialization produced by the implementation.

---

## 23. Error text contract

Tool errors use exactly one concise, safe TextContent block.

Pattern:

```text
[HPxxx] Human-readable remediation.
```

Examples:

```text
[HP100] Invalid Hero Passport tool input.
[HP130] Quest not found.
[HP133] Active quest limit reached. Finish an active quest and retry.
[HP134] Quest belongs to a different locally bound hero/project.
[HP202] Local database is busy. Retry shortly.
[HP203] Hero Passport storage is full. Free local disk space and retry.
[HP206] Hero Passport database failed an integrity check. Run doctor and restore from a known-good backup if needed.
```

No error content includes:

```text
stack trace
exception type unless harmless/needed
SQL
connection string
DB path
workspace path
request body dump
secret/token/environment
```

Retryability remains typed Application metadata for CLI/diagnostics; HP-MCP text conveys only the action needed by the model/user.

---

## 24. Input/output deny-list

Contract/generator tests fail on model-facing properties that represent:

```text
workspacePath
sourceCode
fileContent
diff
patch
rawLog
prompt
chatHistory
environment/env bag
secret
apiKey
token
generic metadata/context/payload/extra bag
clientName/clientVersion as routine tool input
```

`HeroOperationContext` remains adapter/application state and is never serialized into routine MCP arguments.

---

## 25. Breaking/additive policy after field audit

Treat all machine-visible schema changes as **contract changes requiring snapshot + compatibility review**.

Clearly breaking:

```text
tool rename/removal
new required input
removing accepted enum value
changing identifier format
changing field semantics
changing retry/side-effect meaning
making a former success case an error
narrowing an accepted bound incompatibly
```

Potentially additive but still reviewed:

```text
new optional output property
new enum value
new MCP tool
wider bound
new error code
```

A new optional output property is not automatically “free”: HP-MCP uses closed output schemas and some clients may cache/validate advertised schemas. Compatibility tests decide whether it can remain within HP-MCP/2 or requires a new epoch/transition strategy.

Human-only wording changes inside `displayText` are normally non-breaking when machine fields remain unchanged.

---

## 26. Contract snapshot contents

Generated from actual registered SDK tools:

```text
contracts/mcp/hp-mcp-2/
  tools-list.snapshot.json
  start-quest.input.schema.json
  start-quest.output.schema.json
  finish-quest.input.schema.json
  finish-quest.output.schema.json
  list-active-quests.input.schema.json
  list-active-quests.output.schema.json
  get-card.input.schema.json
  get-card.output.schema.json
  success-result-goldens/
  error-result-goldens/
```

Snapshots verify:

- exact tool names/order;
- exact annotations;
- input/output schema closure;
- required fields;
- enum/bounds;
- UUID pattern/annotations;
- no `null` unions;
- success structured/text semantic equality;
- error lacks structuredContent;
- deterministic serialization;
- deny-list absence.

Do not hand-edit snapshots to match unintended generated output.

---

## 27. Required wire validation vectors

### Safe text

1. ordinary ASCII/Russian text;
2. NFC-equivalent strings normalize identically;
3. emoji counts as one scalar;
4. unpaired surrogate rejected;
5. NUL rejected;
6. DEL/C1 control rejected;
7. tab/newline/multiple whitespace collapse to one space;
8. bidi override/isolate controls rejected;
9. 500-scalar goal accepted, 501 rejected;
10. 2000-scalar summary accepted, 2001 rejected.

### Dedup key

11. exact SafeText declaration retry -> same key;
12. whitespace-equivalent declaration -> same key because SafeText normalizes it;
13. NFC-equivalent declaration -> same key;
14. case difference -> **different** key;
15. different quest type -> different key;
16. punctuation difference -> different key.

### UUID

17. canonical lowercase UUIDv7 accepted;
18. uppercase form rejected as noncanonical input;
19. UUIDv4 rejected;
20. malformed UUID -> HP100;
21. valid unknown v7 -> HP130.

### Metrics

22. testsStatus=passed + testsMentioned=false rejected;
23. testsStatus=failed + false rejected;
24. testsStatus=unknown + false rejected;
25. testsStatus=not_run + false accepted;
26. testsStatus=not_run + true accepted.

### Skills

27. canonical 1..3 accepted;
28. duplicate rejected;
29. alias such as `code` rejected by MCP validator;
30. fourth skill rejected;
31. input ordering preserved in skill-XP result.

### Output

32. every success validates against output schema;
33. TextContent parses to structuredContent-equal JSON;
34. every error has `isError=true`, one TextContent, no structuredContent;
35. timestamps exactly millisecond UTC form;
36. numeric values remain JSON-safe integers;
37. arrays follow documented deterministic order;
38. `displayText` remains within per-tool bounds and contains no raw goal/summary.

### Compatibility

39. 2026-07-28 client path;
40. 2025-11-25 client path sees usable serialized JSON TextContent;
41. MCP Inspector shows four valid tools/schemas;
42. reference Codex lifecycle succeeds with exact schemas.

---

## 28. Implementation guidance for official C# SDK

Because HP-MCP needs exact control of success/error representation, tools may return `CallToolResult` directly while using `OutputSchemaType`/explicit output schema and `UseStructuredContent=true` according to the official SDK API.

The adapter owns:

```text
runtime validation
Application mapping
presentation displayText
structured object serialization
matching JSON TextContent
safe error CallToolResult
```

Application never returns `CallToolResult` and never references MCP SDK types.

---

## 29. Revisit triggers

Reopen HP-MCP/2 wire design if:

- an official MCP revision removes/changes the structured+TextContent compatibility recommendation;
- real host evidence shows duplicated structured/text content creates unacceptable agent context cost and a negotiated alternative can remain conformant;
- output sizes approach defined budgets;
- a fifth tool is genuinely required;
- a new public consumer depends on exact schemas;
- model-supplied text needs multilingual RTL support requiring a refined bidi-control policy;
- a future attempt/workstream model changes start semantics.

---

## 30. Official references verified 2026-08-11

- MCP Tools current draft: https://modelcontextprotocol.io/specification/draft/server/tools
- MCP 2025-11-25 Tools: https://modelcontextprotocol.io/specification/2025-11-25/server/tools
- MCP 2026-07-28 release: https://blog.modelcontextprotocol.io/posts/2026-07-28/
- MCP 2026 RC schema changes: https://blog.modelcontextprotocol.io/posts/2026-07-28-release-candidate/
- MCP C# SDK `McpServerToolAttribute`: https://csharp.sdk.modelcontextprotocol.io/api/ModelContextProtocol.Server.McpServerToolAttribute.html
- MCP C# SDK `CallToolResult`: https://csharp.sdk.modelcontextprotocol.io/api/ModelContextProtocol.Protocol.CallToolResult.html
- MCP C# SDK `ToolAnnotations`: https://csharp.sdk.modelcontextprotocol.io/api/ModelContextProtocol.Protocol.ToolAnnotations.html
- JSON Schema 2020-12 validation: https://json-schema.org/draft/2020-12/json-schema-validation
- RFC 8259 JSON: https://www.rfc-editor.org/rfc/rfc8259
- RFC 3339 timestamps: https://www.rfc-editor.org/rfc/rfc3339
