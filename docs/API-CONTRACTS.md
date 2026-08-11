# Hero Passport — API Contracts

**Status:** Accepted architecture v3.1  
**Snapshot:** 2026-08-11  
**Scope:** transport-neutral application semantics, error/versioning conventions

Exact project-binding behavior lives in `PROJECT-IDENTITY.md`. Exact SQLite transaction/recovery behavior lives in `PERSISTENCE-RELIABILITY.md`. Exact HP-MCP fields/validation/results live in `WIRE-CONTRACT.md`.

---

## 1. Contract taxonomy

```text
Domain model
    ↓
Application semantic contracts
    ↓
Adapter contracts
    ├── HP-MCP/2
    ├── CLI / --json
    └── Web read models (0.2+)
```

Do not force one DTO across these layers.

Domain types contain game invariants only and never MCP/CLI/HTTP/EF/localization concerns.

Application commands/queries are the canonical meaning of product operations.

0.1 semantic operations:

```text
StartQuest
FinishQuest
ListActiveQuests
GetHeroCard
InitializeApplication
GetDiagnostics
ExportData
```

Only the first four map to MCP.

---

## 2. Application operation context

Scoped operations receive:

```csharp
public sealed record HeroOperationContext(
    HeroId HeroId,
    ProjectId ProjectId,
    InvocationOrigin Origin);
```

Conceptual diagnostic origin:

```csharp
public sealed record InvocationOrigin(
    InvocationSurface Surface,
    string? ClientName,
    string? ClientVersion);
```

Initial surfaces:

```text
mcp_stdio
cli
web
future mcp_http
```

Hard rules:

```text
client identity != hero identity
client identity != authentication identity
client metadata != authorization
client metadata != reward input
```

Raw client metadata is not persisted by default.

---

## 3. Project binding contract

Application receives a resolved `ProjectId`; it does not resolve filesystem paths.

Infrastructure implements `project-identity/1` exactly as specified in `PROJECT-IDENTITY.md`.

Key semantics:

```text
--project-root else cwd
Git default -> whole repository
Git anchor -> canonical git-common-dir
linked worktrees -> same project
explicit monorepo subdirectory -> explicit repo-relative scope
submodule/nested repository -> separate by default
standalone directory -> path-based local identity
```

No full path appears in routine MCP DTOs or persistence.

Errors:

```text
HP310 invalid_project_binding
HP311 git_repository_unavailable
HP312 git_required_for_repository_binding
HP313 bare_repository_unsupported
```

---

## 4. Hero binding contract

Normal operations use locally resolved active/default hero state.

A local startup selector such as:

```text
--hero <name-or-id>
```

may bind a process without asking the model to choose a hero each call.

Ambiguous selector fails; it never chooses arbitrarily.

Hero binding and MCP client name are independent concepts.

---

## 5. Safe text boundary

MCP `goal`/`summary` pass through `SafeTextV1` before Application persists/uses them. Exact algorithm is in `WIRE-CONTRACT.md`.

Core guarantees:

```text
valid Unicode scalars only
NFC
single-line normalized whitespace
no dangerous control/bidi formatting characters
scalar-aware length bounds
```

The stored value is the normalized safe form.

Application reward summary length is measured over this normalized scalar sequence, not raw transport bytes or UTF-16 `string.Length`.

---

## 6. Quest start deduplication — v3.1 correction

`LogicalQuestKeyV1` is retired before public release.

Use:

```text
QuestDedupKeyV1
```

Algorithm:

```text
SHA-256(UTF8(canonicalQuestType + "\n" + SafeTextV1(goal)))
```

Case is preserved.

Reason: a natural-language hash cannot establish semantic identity, and case folding can incorrectly merge case-sensitive code identifiers.

The key means only:

> same normalized start declaration while an equivalent quest is currently open.

It is not fuzzy matching and not permanent global idempotency.

Persistence uniqueness:

```text
(hero_id, project_id, dedup_key_version, dedup_key)
WHERE status='open'
```

Multiple different declarations may be active simultaneously, up to 16 per hero/project.

---

## 7. StartQuest semantic contract

Conceptual input:

```csharp
StartQuestCommand(
    HeroOperationContext Context,
    QuestType QuestType,
    string Goal);
```

Application semantics:

```text
validate normalized goal/type
compute dedup key
immediate writer transaction
matching open key -> same quest, AlreadyOpen=true
else active count >=16 -> HP133
else create new quest, AlreadyOpen=false
```

The same arguments may create a new quest after the previous one has been finished. Therefore Application provides open-request deduplication, not lifecycle-global idempotency.

---

## 8. FinishQuest semantic contract

Conceptual input:

```csharp
FinishQuestCommand(
    HeroOperationContext Context,
    QuestId QuestId,
    QuestResult Result,
    string Summary,
    QuestMetrics Metrics,
    IReadOnlyList<SkillKey> SkillsUsed);
```

Semantics:

```text
immediate writer transaction
quest missing -> HP130
hero/project mismatch -> HP134
already finished -> return original persisted outcome
otherwise calculate deterministic rule versions
persist report + XP event + aggregate changes atomically
commit
```

A retry for the same finished `questId` never reruns current reward rules.

---

## 9. ListActiveQuests semantic contract

Input is operation context only.

Output:

```text
0..16 active quests for exact HeroId+ProjectId
deterministic order: StartedAtUtc DESC, QuestId ASC
```

Empty is success.

This is the recovery/handoff operation when an agent/process lost or did not inherit an explicit `questId`.

Do not add fuzzy search to recover a quest in 0.1.

---

## 10. GetHeroCard semantic contract

Returns:

```text
global hero progression
current bound project's compact projection
```

It does not return:

```text
workspace path
workspace fingerprint
project internal ID
raw history
source/log data
```

Detailed history is CLI/Web scope.

---

## 11. Error model

Expected failures use typed values rather than exceptions as normal control flow.

Conceptual model:

```csharp
public sealed record HeroError(
    string Code,
    ErrorCategory Category,
    Retryability Retryability,
    string MessageKey,
    IReadOnlyDictionary<string, string>? SafeDetails = null);
```

Do not put paths, SQL, request bodies or secrets in `SafeDetails`.

Current important codes:

```text
HP100 invalid_request
HP110 invalid_quest_type
HP111 invalid_result
HP112 invalid_skill
HP120 invalid_metrics
HP130 quest_not_found
HP133 active_quest_limit
HP134 quest_context_mismatch

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

Retired before public release:

```text
HP131 no_open_quest
HP132 quest_conflict
```

Different active quests are normal; empty active list is normal.

---

## 12. Error adapter mapping

### MCP

Valid tool invocation with bad field/business state:

```text
CallToolResult.IsError = true
one safe TextContent
no structuredContent
```

Malformed protocol request/unknown tool remains MCP protocol error.

### CLI

Human mode:

```text
stderr + nonzero exit
```

Script mode where supported:

```json
{
  "ok": false,
  "error": {
    "code": "HP133",
    "category": "conflict",
    "retryability": "after_user_action"
  }
}
```

### Web

Typed Application error -> typed UI state. Razor does not parse MCP error strings.

---

## 13. IDs

Internal IDs use typed wrappers and UUIDv7 generation (`Guid.CreateVersion7()`).

HP-MCP wire requirements are exact in `WIRE-CONTRACT.md`: lowercase canonical UUIDv7 text and explicit runtime validation.

No prefixed string IDs in 0.1.

---

## 14. Time

Application uses injected .NET `TimeProvider`.

Persistence stores UTC values.

HP-MCP producer formatting is fixed by `WIRE-CONTRACT.md` to millisecond UTC `...fffZ` for deterministic interoperability.

Domain does not call `DateTime.UtcNow`.

---

## 15. Numeric range

Long-lived integers exposed to HP-MCP must remain within JSON's widely interoperable exact-integer range:

```text
0 .. 9_007_199_254_740_991
```

Use checked arithmetic and fail safely rather than overflow/wrap.

Quest-local counters keep tighter documented limits.

---

## 16. Application DTOs are not MCP DTOs

MCP intentionally omits stable local state such as:

```text
HeroId
ProjectId
workspacePath
locale
presentation mode
clientName/clientVersion as routine input
```

Example flow:

```text
MCP StartQuestTool
  questType + goal
      ↓
McpOperationContextResolver
      ↓
HeroOperationContext
      ↓
StartQuestCommand
      ↓
StartQuestResult
      ↓
MCP output projection
```

CLI and Web may expose different data according to their consumer needs.

No `HeroPassport.Contracts` assembly is required until a real separately versioned .NET consumer appears.

---

## 17. Public machine contract version axes

Keep separate:

```text
Hero Passport 0.1.0        product version
MCP 2026-07-28             negotiated protocol revision
HP-MCP/2                   model-facing tool contract epoch
configVersion 1            config schema
EF migration id            persistence schema
project-identity/1         local project identity
QuestDedupKey V1           retry/dedup algorithm
SafeTextV1                 model text normalization
reward/1.0.0               XP rules
trust-risk/1.0.0           Trust/Risk rules
traits/1.0.0               trait rules
```

Do not send a caller-chosen `schemaVersion` in each MCP request.

---

## 18. Contract evolution

Breaking changes include:

```text
tool rename/removal
new required input
semantic meaning change
identifier format change
removing accepted enum value
new side effect
changing success/error meaning
incompatible bound narrowing
```

Potentially additive machine changes still require snapshot + interop review because HP-MCP advertises closed schemas.

New tool inventory always requires tool-selection/eval review even if “additive”.

RPG rule version changes are different from transport contract changes and never rewrite persisted historical rewards.

---

## 19. Contract snapshots

Actual registered implementation generates authoritative snapshots under:

```text
contracts/mcp/hp-mcp-2/
```

Tests compare:

- exact tool inventory/order;
- schemas/annotations;
- field bounds/enums;
- success structured/text semantic equivalence;
- error result shape;
- forbidden field absence.

Do not maintain a second hand-written schema implementation.

---

## 20. Normative references

- `PROJECT-IDENTITY.md`
- `PERSISTENCE-RELIABILITY.md`
- `WIRE-CONTRACT.md`
- `MCP-CONTRACT.md`
- `DATA-MODEL.md`
- `ENGINE-SPEC.md`
- `TESTING-QUALITY.md`
