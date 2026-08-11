# Hero Passport v3 Integration/API Architecture Design

> **Status:** Accepted for architecture PR after official-doc contradiction review on 2026-08-11.

## Goal

Define one implementation-ready Hero Passport architecture that:

- remains a small local-first RPG product;
- works naturally with Codex first but is not Codex-specific;
- exposes one portable four-tool HP-MCP/2 contract;
- supports multiple parallel agents/workstreams safely;
- separates semantic Application contracts from MCP/CLI/Web adapters;
- preserves privacy and deterministic progression;
- keeps HTTP/hosted complexity outside 0.1 while leaving a clean future seam;
- prevents coding-time ambiguity through explicit versioning, binding, concurrency, error and compatibility rules.

## Architecture

```text
MCP hosts
  Codex / VS Code / JetBrains / Zed / Cursor / Claude / ...
                         |
                    stdio 0.1
                         |
                 HeroPassport.App
             MCP + CLI + Presentation
                         |
               HeroPassport.Application
           semantic commands/queries/ports
                 /                \
                /                  \
       HeroPassport.Domain     Infrastructure
       deterministic policy     EF/SQLite/config/fs
                                      |
                                   SQLite

0.2: Web -> Application -> Infrastructure
future: Streamable HTTP adapter -> same Application
```

MCP is an adapter, not the product's internal API.

---

## 1. Product contract

Hero Passport turns meaningful AI-agent work into persistent RPG progression:

```text
start logical quest
-> work normally
-> finish explicit quest
-> deterministic reward
-> local durable progression
-> compact status
```

Product values:

```text
entertainment/companion first
local-first
privacy by data minimization
deterministic rules
small model-context footprint
portable MCP semantics
multi-agent safe
```

0.1.0 is the **Portable Local MCP Core**.

Codex is the first Qualified reference host, not a special code path.

---

## 2. Layer boundaries

### Domain

Owns:

```text
Hero/Project/Quest typed vocabulary
state transitions
LogicalQuestKey canonical policy/value
XP/levels
skills
quality flags
Trust/Risk
traits
rule versions
```

Must not reference:

```text
MCP
EF/SQLite
CLI/HTTP
filesystem/config
localization
client metadata
```

### Application

Owns:

```text
HeroOperationContext
StartQuestHandler
FinishQuestHandler
ListActiveQuestsHandler
GetHeroCardHandler
initialization/diagnostics/export use cases
ports
semantic Result/Error types
read models
```

Application is the canonical semantic API.

It must not expose MCP SDK types, HTTP objects, CLI parser types, EF entities or localized text.

### Infrastructure

Owns:

```text
DbContext/migrations
stores/read queries
SQLite transaction/concurrency behavior
project binding/fingerprinting
platform paths
file config
safe export
local diagnostics adapters
```

### App

Owns:

```text
Generic Host/DI
MCP stdio
System.CommandLine
operation-context resolver
presentation/localization
stdout/stderr policy
```

### Web — 0.2

Consumes Application/read models. Components never use DbContext directly.

---

## 3. Why no universal external DTO

MCP, CLI and Web have different consumers.

MCP needs bounded/token-small model-facing contracts.

CLI may expose operator details in `--json`.

Web needs rich typed projections.

Therefore:

```text
Application command/result != MCP request/result != CLI JSON != Web view model
```

Mappings are explicit at adapters.

No standalone `HeroPassport.Contracts` assembly until a real independently versioned .NET consumer exists.

---

## 4. HeroOperationContext

Scoped operations receive:

```text
HeroId
ProjectId
InvocationOrigin
```

InvocationOrigin may contain:

```text
surface = mcp_stdio | cli | web | future mcp_http
optional bounded client name/version for diagnostics
```

Rules:

```text
client info is untrusted
client != hero
client != principal/auth identity
client != project
client metadata never changes reward
raw client metadata not persisted by default
```

Do not name the type `ExecutionContext` because of the .NET BCL type.

---

## 5. Project binding

Project is a trusted local adapter context, not model input.

0.1 local stdio resolution:

```text
explicit --project-root, if provided
else process working directory
then normalize/discover Git root
else use starting directory
-> ProjectIdentityV1
```

Persist:

```text
ProjectId
DisplayName
WorkspaceFingerprint
ProjectIdentityVersion
```

Do not persist or return full path by default.

Hosts use their native cwd/project-level mechanism where available. `--project-root` is the portable fallback.

Do not use MCP Roots: deprecated in 2026 and not uniformly implemented.

A globally shared process without a reliable project binding is outside the project-aware 0.1 profile; do not infer workspace from tool text/client brand.

---

## 6. Hero binding

Default/active hero is local product state.

Optional adapter binding:

```text
hero-passport mcp --hero <selector>
```

No per-tool heroId.

One hero can be used by many clients. One client can run different server entries bound to different heroes.

---

## 7. HP-MCP/2

Exact stable inventory:

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

Registered explicitly in this order.

No assembly scanning/dynamic inventory.

### Input policy

```text
closed object roots
additionalProperties:false
bounded strings/integers/arrays
closed enums
shallow schema
no metadata/context/payload bag
```

No fields for source/diff/raw logs/prompt/secrets/environment/workspace path.

### Output policy

Canonical machine result: `structuredContent` + output schema.

Human/legacy result: one concise text representation; never duplicate the entire JSON as text.

### Tool annotations

```text
start   readOnly=false destructive=false idempotent=true*  openWorld=false
finish  readOnly=false destructive=false idempotent=true   openWorld=false
list    readOnly=true  destructive=false idempotent=true   openWorld=false
card    readOnly=true  destructive=false idempotent=true   openWorld=false
```

`start` idempotency is logical-work idempotency.

---

## 8. MCP protocol versioning

Preferred semantic baseline:

```text
2026-07-28
```

But server option:

```text
McpServerOptions.ProtocolVersion = null/unset
```

Official C# SDK v2 then supports its documented set of initialize-era revisions plus 2026 behavior.

Tests require:

```text
2026-07-28
2025-11-25
```

Application correctness never depends on MCP session identity.

Do not describe stdio implementation as requiring a generic `Stateless=true` option. For future Streamable HTTP adapter, configure SDK HTTP transport explicitly in stateless mode.

---

## 9. MCP feature profile

Required:

```text
Tools
```

Not required for core:

```text
Resources
Prompts
Roots
Sampling
MCP Logging
MRTR
Tasks
Apps
```

Roots, Sampling and MCP Logging are deprecated in the 2026 line. Tasks are wrong for millisecond local operations.

Optional future Prompts/Apps must not become prerequisites for the quest lifecycle.

---

## 10. `tools/list` cache policy

Inventory is compile-time static and same for all local users.

2026 list metadata:

```text
cacheScope = public
initial ttlMs = 300000
```

The 5-minute TTL is an implementation freshness default, not HP-MCP semantics. It may be tuned after host evidence without HP-MCP epoch change.

No `listChanged` notification while inventory remains static.

---

## 11. Multi-active quest model

Architecture v2's single-current-quest model is removed before public release.

A hero/project may hold up to 16 distinct open quests.

Example:

```text
Nova + Project X
  q1 coding
  q2 review
  q3 documentation
```

This supports parallel agent/client work.

---

## 12. LogicalQuestKeyV1

Same logical work should converge even if two clients start it.

Canonical key input:

```text
canonical questType
+ "\n"
+ normalized goal
```

Goal normalization for key only:

```text
Unicode NFC
trim
collapse Unicode whitespace runs to single ASCII space
invariant case normalization
```

Hash:

```text
SHA-256 UTF-8 bytes
```

Persist hash + version.

Original validated goal remains available as bounded history text.

Database partial uniqueness:

```text
(hero_id, project_id, logical_key_version, logical_key)
WHERE status='open'
```

Same-key concurrent start -> one quest, other caller reloads winner.

Distinct keys -> coexist.

If user wants two deliberate identical-wording experiments, v1 requires distinct goal wording. Do not add attempt/idempotency fields until real demand.

---

## 13. Active cap

```text
MaxOpenQuestsPerHeroProject = 16
```

Application policy, not DB schema truth.

Real SQLite race test from count 15 with two distinct starts must prove <=16 after both calls.

SQLite serializes writers; choose a write transaction mode that actually preserves the cap. If ordinary EF transaction behavior is insufficient, localize SQLite-specific immediate transaction SQL in Infrastructure.

No distributed/global mutex.

---

## 14. StartQuest

MCP input:

```json
{"questType":"coding","goal":"Implement XpCalculator"}
```

Semantic algorithm:

```text
resolve HeroOperationContext
validate
compute logical key
find matching open quest
  found -> return same ID, alreadyOpen=true
count active
  >=16 -> HP133
insert
commit
```

Concurrent unique-key race is translated into success-reload, not leaked constraint error.

---

## 15. ListActiveQuests

MCP input:

```json
{}
```

Returns only bound hero/project active quests.

```text
max 16
startedAtUtc DESC, then questId ASC
empty -> successful []
```

It supersedes `current_quest` before 0.1 release.

Human display does not echo arbitrary goal text by default. Structured data includes bounded goal for recovery.

---

## 16. FinishQuest

MCP input contains only:

```text
questId
result
summary
bounded metrics
1..3 skills
```

Before mutation:

```text
quest exists else HP130
quest hero/project matches HeroOperationContext else HP134
if finished -> return original stored result
```

Atomic transaction:

```text
report
skill allocation snapshot
UNIQUE xp_event
hero aggregate
skills
traits
project stats
quest status
```

Repeated/concurrent finish produces one reward.

---

## 17. GetHeroCard

Read-only typed hero/project progress; no large history.

Presentation text is App-level.

---

## 18. Errors

Semantic error type contains:

```text
Code
Category
Retryability
MessageKey
SafeDetails
```

Core codes:

```text
HP100 invalid_request
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

Old HP131/HP132 normal paths are retired before public release.

MCP semantic failure -> tool error. CLI -> stderr/nonzero/stable JSON error where applicable. Protocol framing remains SDK error.

No stack/SQL/path leakage.

---

## 19. SQLite

EF Core 10.0.10 + pinned SQLitePCLRaw 3.0.5.

```text
IDbContextFactory
short synchronous DB calls
WAL
synchronous=FULL
foreign_keys=ON
Cache=Default
Pooling=True
Default Timeout=5
```

Tests query actual `sqlite_version()`.

Migrations from 0001. EF owns migration lock; no custom mutex.

---

## 20. Presentation

Domain/Application never render status strings.

App `HeroTextRenderer` owns RU/EN compact/normal output.

Russian canonical labels:

```text
scope_control -> Контроль
Clean scope bonus -> Бонус за контроль
Scope violation -> Выход за задачу
```

---

## 21. Integration model

No runtime `CodexAdapter`, `CursorAdapter`, etc.

One runtime:

```text
hero-passport mcp
```

Host docs/config differ:

```text
Codex TOML/cwd
VS Code servers/cwd/workspace variable
JetBrains mcpServers + Working directory
Zed context_servers + args fallback
Cursor mcpServers + binding fallback
Claude Code native MCP config + binding fallback
```

Support tiers:

```text
Qualified
Documented/protocol-compatible
Unsupported
```

Codex is first Qualified release host. Others require RC smoke evidence.

---

## 22. Deployment profiles

### A — local stdio 0.1

No network/auth. Project-bound local process + SQLite.

### B — OpenAI Secure MCP Tunnel

External tunnel can reach local stdio; no Hero Passport HTTP required.

### C — project-scoped Streamable HTTP future

Only on concrete URL/deployment need. Add `ModelContextProtocol.AspNetCore`, bind project explicitly, set HTTP stateless mode, validate Origin/Host and design auth for non-loopback.

### D — public/multi-tenant

Separate architecture: TLS/OAuth/principal authorization/tenant isolation/remote DB/backups/rate limits.

No new legacy SSE.

---

## 23. No second public API by default

```text
AI integrations -> MCP
shell/CI -> CLI/--json
Hero Passport Web -> Application in-process
```

No REST/GraphQL/gRPC until a concrete non-MCP remote consumer exists and gets its own ADR/security/version model.

---

## 24. Distribution

0.1 primary package: .NET tool `hero-passport`.

Later self-contained binaries only after native SQLite packaging tests.

MCP Registry is preview and distribution-only; no runtime dependency. If publication happens, choose stable package/server identity and follow NuGet `mcp-name` ownership requirements.

No per-host binary.

---

## 25. Contract snapshots

Once MCP implementation exists, generate actual SDK manifest/schema snapshots:

```text
contracts/mcp/hp-mcp-2/
```

Snapshots cover exact tool list/input/output schemas. They are generated, not hand-maintained.

Any diff receives compatibility/privacy/token/eval review.

---

## 26. Testing

Deterministic:

```text
Domain goldens
Application semantic tests
real SQLite migration/race tests
architecture tests
contract snapshots
process stdout tests
protocol 2026 + 2025-11-25
MCP Inspector
Codex E2E
```

Model behavior:

```text
host-neutral AgentEval scenarios
Codex runner first
same task reuse
parallel tasks
lost ID list recovery
privacy
finish retry
small-question no-op
```

RC host smoke:

```text
VS Code
JetBrains
Zed
Cursor
Claude Code
```

A failing unqualified host smoke can remain documented rather than block core release, but must not be marketed Qualified.

---

## 27. Version axes

```text
Product                 0.1.0
MCP protocol            negotiated; 2026 semantics preferred
HP-MCP                  2
config                   1
EF schema                migrations
RewardRules              1.0.0
TrustRiskRules           1.0.0
TraitRules               1.0.0
LogicalQuestKey          1
ProjectIdentity          1
```

Do not add generic per-call schemaVersion.

---

## 28. Dependency decisions

Accepted:

```text
ModelContextProtocol 2.0.0
EF Core SQLite/Design 10.0.10
SQLitePCLRaw.bundle_e_sqlite3 3.0.5
System.CommandLine 2.0.10
xUnit v3 3.2.2
built-in Host/DI/Logging/Options/TimeProvider/System.Text.Json/UUIDv7/SHA256
```

Deferred/rejected baseline:

```text
ModelContextProtocol.AspNetCore until HTTP
MediatR
FluentValidation
AutoMapper
Dapper
Polly
Serilog/NLog
Spectre.Console
OTel exporters
Testcontainers for SQLite
host-specific SDKs
REST/OpenAPI framework
runtime plugin framework
```

---

## 29. Documentation precedence

```text
PRODUCT-SPEC
ARCHITECTURE
API-CONTRACTS
MCP-CONTRACT
DATA/CONFIG/SECURITY specs
DECISION-LOG
ROADMAP/implementation plan
```

Implementation plan cannot override a normative contract.

---

## 30. Architecture review triggers

Mandatory focused review before:

```text
fifth MCP tool
new code/diff/log data ingestion
HTTP/OAuth
public hosting
Resources/Prompts/Apps as required core
MCP Registry publication
separate public API
new persistence backend
runtime plugins
team/multi-tenant mode
```

This design intentionally leaves extension points at semantic boundaries rather than prebuilding frameworks.
