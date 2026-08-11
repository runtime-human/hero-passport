# Hero Passport — Roadmap

**Status:** Accepted architecture v3 roadmap  
**Snapshot:** 2026-08-11

## Guiding rule

Ship a correct portable local core before dashboard, HTTP, plugins or hosted features. Each milestone ends with executable evidence and a coherent public/internal contract.

---

## 0.0.1 — Reproducible foundation

Deliver:

```text
net10.0 solution/projects
global.json SDK 10.0.302
Central Package Management
lock files
.editorconfig/analyzers
xUnit v3 test projects
architecture test skeleton
```

Gate: clean locked restore/build/test on supported dev OS.

---

## 0.0.2 — Domain vocabulary and v3 contract types

Deliver:

```text
typed IDs
quest/result/skill keys
Hero/Project/Quest domain state
HeroOperationContext Application type
stable HeroError model
LogicalQuestKeyV1 canonicalization + goldens
```

Gate: no infrastructure/MCP leakage; logical-key deterministic cross-platform tests.

---

## 0.0.3 — Reward/levels

Deliver deterministic reward engine and 95-XP golden.

Gate: all boundary/golden tests.

---

## 0.0.4 — Skills, Trust/Risk, traits

Deliver canonical normalization/allocation and initial trait progression.

Gate: exact XP conservation and rule version tests.

---

## 0.0.5 — Application lifecycle

Deliver:

```text
StartQuestHandler
FinishQuestHandler
ListActiveQuestsHandler
GetHeroCardHandler
binding ports
read/result contracts
```

Semantic gates:

```text
same logical task -> same open quest
distinct task -> distinct open quest
empty list -> success
wrong context -> HP134
finished retry -> stored result
```

---

## 0.0.6 — Configuration, paths and presentation

Deliver:

```text
platform app data paths
HERO_PASSPORT_HOME
configVersion 1
--project-root binding model
--hero binding model
HeroTextRenderer RU/EN
```

Gate: no path in MCP/Application public result, isolated test roots, presentation goldens.

---

## 0.0.7 — SQLite schema/migrations

Deliver migration 0001 matching architecture v3:

```text
heroes/projects/stats
quest_sessions with logical_key + version
partial open logical-key uniqueness
active query index
reports/skills/traits/xp ledger
UNIQUE xp_events.quest_id
```

Configure/verify WAL, FULL, FKs, native SQLite.

Gate: fresh DB + constraints + query projection tests.

---

## 0.0.8 — Concurrency and idempotency hardening

Real file-backed SQLite race tests:

```text
same-key concurrent start convergence
different-key concurrent starts at cap 15 -> <=16
finish race -> one XP event
context mismatch
DB busy mapping
rollback
```

Choose SQLite transaction mode from evidence. No distributed/custom global mutex.

Gate: deterministic race suite repeatedly passes.

---

## 0.0.9 — CLI/operator surface

Deliver:

```text
init
mcp --project-root --hero
doctor
card
quest list --active
export
data path
--version
```

Gate: parser/help/exit-code/process/stdout/stderr tests.

---

## 0.0.10 — HP-MCP/2 stdio

Deliver exact four tools:

```text
start
finish
list_active_quests
get_card
```

Plus:

```text
explicit registration
ProtocolVersion unset/null
session-independent app semantics
server instructions
strict interop schemas
output schemas/structured content
annotations
public list cache scope
contract snapshot generator
stdout guard
```

Gate:

```text
2026-07-28 client path
2025-11-25 compatibility path
MCP Inspector
schema snapshots
```

---

## 0.0.11 — Codex reference qualification + AgentEvals

Deliver:

```text
Codex current official config example
project binding via cwd/project config or --project-root
exact enabled tool allow-list
E2E fresh/restart/multi-quest/retry
host-neutral eval scenarios with Codex runner
```

Gate: Codex becomes Qualified reference host for RC.

---

## 0.0.12 — Interoperability documentation pack

Document current configuration for:

```text
VS Code
JetBrains AI Assistant
Zed
Cursor
Claude Code
ChatGPT private tunnel
```

Perform release smoke where environment access permits and record Qualified vs Documented status honestly.

No host-specific runtime libraries.

---

## 0.0.13 — Architecture/privacy/dependency gates

Deliver fail-fast checks for:

```text
layer references
forbidden MCP fields
stale current_quest contract
accidental ProtocolVersion pin
assembly scanning
session-dependent state
one-open-quest stale constraint
unapproved dependencies
privacy/log leakage
```

---

## 0.1.0-rc.1 — Qualification

Run:

```text
locked restore/build/full tests
NuGet audit
fresh + previous DB migration
native SQLite check
process MCP smoke
Inspector
Codex E2E
AgentEvals
pack/install/update tests
OS matrix
host smoke matrix documentation
```

No new feature scope after RC unless required to fix a gate.

---

## 0.1.0 — Portable Local MCP Core

Definition:

```text
local stdio MCP
HP-MCP/2
multi-agent-safe active quest model
transport-neutral Application
SQLite durability
CLI/operator support
Codex Qualified
other host integration docs with evidence tier
```

No dashboard required.

---

## 0.1.1 — Integration/distribution polish

Possible only from evidence after 0.1:

```text
integration show <host> snippet renderer
broader automated host smoke where practical
packaging ergonomics
MCP Registry publication if preview maturity/identity are acceptable
additional Qualified hosts
```

No API expansion merely to gain version number.

---

## 0.2.0 — Local Blazor dashboard

Add `HeroPassport.Web` over Application/read models.

Initial UI:

```text
hero card
XP progress
Trust/Risk
skills
traits
recent quests
active quests
project stats
```

No DbContext in Razor components. No separate duplicated business rules.

---

## HTTP trigger milestone — not preassigned as automatic scope

Own Streamable HTTP enters roadmap only when a concrete requirement satisfies `DEPLOYMENT-MODES.md` trigger criteria.

Likely first form if needed:

```text
project-scoped stateless Streamable HTTP
ModelContextProtocol.AspNetCore
loopback/private security profile
same HP-MCP semantics
```

Do not implement legacy SSE.

---

## Hosted/public phase — separate architecture

If public ChatGPT/plugin/team SaaS becomes a goal, first design:

```text
identity/OAuth
authorization
multi-tenant data ownership
remote persistence
backup/retention
rate/abuse controls
public HTTPS deployment
```

Do not assume local SQLite/project-binding architecture can simply be exposed publicly.

---

## Post-MVP explicitly deferred

```text
Achievements module
items/artifacts
runtime plugin ABI
self-evolution
LLM judge
continuous telemetry
cloud sync
team dashboards
MCP Apps UI
Tasks
Resources/Prompts as required core behavior
REST/GraphQL/gRPC public API
ACP agent
```

Every proposed deferred feature must identify which product problem it solves and which existing boundary it crosses before implementation begins.
