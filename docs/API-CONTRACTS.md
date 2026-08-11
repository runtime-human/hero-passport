# Hero Passport — API Contracts

**Status:** Accepted architecture v3  
**Snapshot:** 2026-08-11  
**Scope:** transport-neutral application semantics and public contract rules

## 1. Contract taxonomy

Hero Passport has deliberately separate contract layers:

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

The layers may share concepts but must not share transport-specific types.

### 1.1 Domain contract

Domain types express game invariants only: IDs, quest/result keys, reward calculations, skills, traits, Trust/Risk and rule versions.

They do not contain:

```text
MCP SDK attributes
JSON-RPC concerns
CLI parser types
HTTP status codes
localized text
EF entities
host/client configuration
```

### 1.2 Application semantic contract

Application commands/queries are the canonical meaning of product operations. They are the interface that MCP, CLI and future Web call.

Canonical operations for 0.1.0:

```text
StartQuest
FinishQuest
ListActiveQuests
GetHeroCard
InitializeApplication
GetDiagnostics
ExportData
```

The first four map to MCP; administration/diagnostics/export do not.

### 1.3 Adapter contract

Each adapter optimizes for its consumer:

- MCP: minimal model-facing schemas and bounded output;
- CLI: human output plus stable `--json` where scripting is useful;
- Web: typed read models, not parsed text.

Do not force one universal DTO across all adapters.

---

## 2. Why MCP DTOs are not Application DTOs

A model should not choose stable local state on every call. Application operations need resolved hero/project context, but HP-MCP does not expose those identifiers as routine model inputs.

Example semantic flow:

```text
MCP StartQuestTool
  input: questType + goal
        ↓
McpOperationContextResolver
  resolves HeroOperationContext
        ↓
StartQuestCommand
  context + questType + goal
        ↓
StartQuestResult
        ↓
MCP output projection + text renderer
```

This keeps transport minimization and application correctness separate.

---

## 3. `HeroOperationContext`

Application handlers that operate on scoped hero/project state receive a transport-neutral context.

Conceptual contract:

```csharp
public sealed record HeroOperationContext(
    HeroId HeroId,
    ProjectId ProjectId,
    InvocationOrigin Origin);
```

`InvocationOrigin` is diagnostic context, conceptually:

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
web          # 0.2+
mcp_http     # future
```

Rules:

1. Hero/project IDs are resolved by trusted local binding/application state, not model input.
2. Client name/version is untrusted metadata.
3. Client metadata is not authentication or authorization.
4. Client metadata is not reward input.
5. Raw MCP client metadata is not persisted by default.
6. Domain never sees InvocationOrigin unless a future explicit domain requirement is approved; normal rules must remain client-neutral.

Avoid naming this type `ExecutionContext` because .NET already has `System.Threading.ExecutionContext`.

---

## 4. Project binding contract

`IProjectBindingResolver` turns launch/environment context into a project identity without exposing local path to the model.

Local stdio resolution order:

```text
1. explicit --project-root <path>
2. process working directory
3. Git repository root discovered from that starting directory when available
4. starting directory itself as fallback identity root
```

Implementation detail: Git-root discovery may logically occur before persisting the final project identity even though `--project-root`/cwd defines the starting point.

Persisted project identity remains:

```text
ProjectId
DisplayName
WorkspaceFingerprint
ProjectIdentityVersion
```

Absolute path is transient local adapter input.

### 4.1 Portable limitation

MCP 2026 does not provide a mandatory cross-host current-workspace primitive, and Roots are deprecated. Therefore a single global process cannot reliably infer a different project for each stateless request.

Supported local profile is **project-bound launch**. A host should either:

- launch Hero Passport with an appropriate cwd; or
- pass `--project-root` in server arguments.

Do not reintroduce `workspacePath` into tool schemas to compensate for weak host configuration.

---

## 5. Hero binding contract

0.1.0 resolves one active/default hero from local product state. A host may optionally pin a hero through a local launch option such as:

```text
hero-passport mcp --hero Nova
```

The model still does not send `heroId` on every call.

Client and hero identity are independent:

```text
Codex != Nova
Claude != a hero
same hero may be used from several MCP clients
one client may be configured against different heroes in different server entries
```

---

## 6. Quest concurrency and logical identity

### 6.1 Multiple active quests

Hero Passport permits several distinct open quests for the same hero/project. This is required for parallel agents, IDE + terminal workflows and deliberate workstream separation.

Application policy v1:

```text
MaxOpenQuestsPerHeroProject = 16
```

The cap is an operational/token-safety policy and may be changed by a future product version with explicit release notes; it is not a storage-engine limitation.

### 6.2 Logical quest key v1

A repeated start for the same logical work item converges intentionally to one open quest.

Canonicalization for key calculation:

```text
quest type canonical key
+
Unicode NFC(goal)
-> trim leading/trailing whitespace
-> collapse Unicode whitespace runs to one ASCII space
-> invariant case normalization
```

Then:

```text
LogicalQuestKeyV1 = SHA-256(UTF-8(canonicalQuestType + "\n" + canonicalGoal))
```

Persist the key and key-version.

Original bounded goal text is stored unchanged apart from validated normalization policy needed for persistence safety; it is not replaced by canonical lowercase text.

### 6.3 Intentional convergence semantics

Two clients that start the exact same logical work item for the same hero/project receive the same open `questId`. This is intentional handoff/deduplication behavior and prevents duplicate XP for duplicated agent starts.

If a user truly wants two competing attempts on identical wording, 0.1.0 requires distinct goal wording. A dedicated Attempt model is deferred until there is evidence for it; do not add an `attemptId`/`startKey` to HP-MCP preemptively.

---

## 7. StartQuest semantic contract

Conceptual input after context resolution:

```text
HeroOperationContext context
QuestType questType
string goal
```

Result:

```text
QuestId
AlreadyOpen
HeroCardReadModel
```

Algorithm:

```text
validate
resolve context
calculate LogicalQuestKeyV1
read active matching logical key
  -> found: return same quest, AlreadyOpen=true
check active count
  -> >=16: HP133
insert new open quest atomically
return projection
```

Concurrent starts for the same logical key must converge through a database uniqueness constraint, not only an in-memory pre-check.

---

## 8. FinishQuest semantic contract

Input:

```text
HeroOperationContext context
QuestId
Result
Summary
QuestMetrics
SkillsUsed
```

Before reward calculation:

```text
load quest
quest exists?                     else HP130
quest hero/project == context?    else HP134
already finished?                 return original persisted outcome
```

Finish is one atomic transaction and creates at most one XP ledger event per quest.

Retry semantics are immutable: never rerun current reward rules for an already-completed quest.

---

## 9. ListActiveQuests semantic contract

Input: `HeroOperationContext` only.

Result contains active quests for that exact hero/project.

Ordering is deterministic:

```text
StartedAtUtc descending
then QuestId ascending
```

Maximum returned count is the same application cap (16), so the operation is always bounded.

No active quest is a successful empty result, not `HP131`.

The former singular `GetCurrentQuest/current_quest` contract is superseded before public release.

---

## 10. Hero card semantic contract

`GetHeroCard` is a read operation over locally resolved hero/project context. It returns typed hero progress. Presentation text is rendered in App.

No client/transport-specific game state is included.

---

## 11. Error model

Application expected failures use a stable semantic error:

```csharp
public sealed record HeroError(
    string Code,
    HeroErrorCategory Category,
    HeroRetryability Retryability,
    string MessageKey,
    IReadOnlyDictionary<string, string>? SafeDetails = null);
```

This is conceptual; implementation may use allocation-efficient concrete types while preserving semantics.

Categories:

```text
validation
not_found
conflict
storage
configuration
internal
```

Retryability:

```text
never
same_request
transient
after_user_action
```

Stable code families:

```text
HP100..199 application/contract/domain
HP200..299 storage/filesystem
HP300..399 configuration/binding
HP900 internal_error
```

Core relevant codes:

```text
HP100 invalid_request
HP101 unsupported_contract_input
HP120 project_not_resolved
HP130 quest_not_found
HP133 active_quest_limit
HP134 quest_context_mismatch
HP140 unsupported_quest_type
HP141 unsupported_result
HP200 storage_unavailable
HP201 migration_failed
HP202 database_busy
HP210 app_data_unavailable
HP300 invalid_configuration
HP310 invalid_project_binding
HP900 internal_error
```

`HP131 no_open_quest` and `HP132 quest_conflict` from architecture v2 are not required by HP-MCP/2 normal flow and are retired before public release. Empty active lists are success; different logical quests may coexist.

Adapters may add transport-level metadata but may not redefine semantic meaning of a code.

---

## 12. External JSON conventions

For all JSON-producing adapters unless a specific protocol mandates otherwise:

```text
property names: lowerCamelCase
canonical enum keys: lower_snake_case
UUID: canonical lowercase hyphenated form
UTC timestamp: RFC3339 / ISO 8601 with Z
numbers: integer where domain value is integer
absent optional property: omit unless null has distinct semantic meaning
```

No polymorphic type discriminators are introduced without a demonstrated need.

Arrays with semantic order define it explicitly. Sets are serialized in canonical order.

---

## 13. Version axes

Never conflate:

```text
Hero Passport release        0.1.0
MCP wire revision            negotiated (preferred semantics 2026-07-28)
Hero MCP contract epoch      HP-MCP/2
configuration schema         configVersion 1
EF migrations                migration IDs
reward rules                 RewardRules 1.0.0
Trust/Risk rules             TrustRiskRules 1.0.0
trait rules                  TraitRules 1.0.0
logical quest key            LogicalQuestKey V1
project identity             ProjectIdentity V1
```

No per-call generic `schemaVersion` field.

---

## 14. HP-MCP breaking-change rules

Breaking before/after release is judged by observable model/client contract, not internal class refactoring.

Breaking examples:

```text
tool removal/rename
new required input
removing an accepted enum value
changing semantics of an existing field
changing retry/idempotency meaning
making a previously successful ordinary case an error
changing explicit identifier format
```

Normally additive:

```text
new optional output field
new safe error code for a previously unspecified failure
clearer tool description with same semantics
new host configuration documentation
new local presentation format
```

Adding a new MCP tool is technically additive but still requires an explicit API/eval review because it changes the model's tool-selection surface and list-cache contract.

---

## 15. Contract snapshots

Once implementation exists, actual SDK-generated MCP schemas are the executable source of truth and committed snapshots guard drift.

Planned generated artifacts:

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
```

Do not hand-maintain duplicate schema files before the generator/manifest test exists. The first MCP implementation task generates them and the build compares canonicalized output thereafter.

Contract snapshot changes require review of:

```text
backward compatibility
privacy fields
model token impact
host interoperability
agent evals
release notes / HP-MCP epoch when applicable
```

---

## 16. Explicit non-goals

0.1.0 does not create a generic public REST API, GraphQL API, gRPC service, ACP agent interface or language-agnostic SDK merely for hypothetical integrations.

Use:

```text
AI agents/hosts       -> MCP
shell/CI automation   -> CLI / --json
Hero Passport Web     -> Application in-process
future .NET consumer  -> consider Contracts/Client package only when real
future remote service -> Streamable HTTP MCP deployment profile first
```

A second public service API requires a separate consumer, ADR and versioning/security model.
