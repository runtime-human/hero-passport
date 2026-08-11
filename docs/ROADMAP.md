# Hero Passport — Roadmap

**Status:** Accepted architecture v3.1 roadmap  
**Snapshot:** 2026-08-11

## Guiding rule

Ship a correct portable local core before dashboard, HTTP, plugins or hosted features. Every milestone ends with executable evidence and must conform to the three deep-dive contracts.

---

## 0.0.1 — Reproducible foundation

```text
net10.0 projects
SDK 10.0.302
Central Package Management
lock files
xUnit v3 projects
architecture test skeleton
```

Gate: clean restore/build/test scaffold.

---

## 0.0.2 — Domain vocabulary

```text
typed Hero/Project/Quest IDs
quest/result/status keys
JSON-safe integer ceiling
pure domain boundaries
```

Gate: no MCP/EF/config leakage.

---

## 0.0.3 — SafeText/dedup/context/error contracts

```text
SafeTextV1
QuestDedupKeyV1 (case preserved)
HeroOperationContext
InvocationOrigin
HeroError/HeroResult
```

Gate:

```text
Unicode scalar/bidi/control goldens
case difference -> different dedup key
NFC/whitespace equivalence -> same dedup key
no active LogicalQuestKeyV1 type
```

---

## 0.0.4 — Reward/levels

Deterministic engine + 95-XP golden; checked numeric boundaries.

---

## 0.0.5 — Skills, Trust/Risk, traits

Canonical skills, exact ordered allocation, rule versions and unlock monotonicity.

---

## 0.0.6 — Application lifecycle

```text
StartQuest
FinishQuest
ListActiveQuests
GetHeroCard
```

Semantic gates:

```text
same normalized OPEN declaration -> same quest
same arguments after finished quest -> new quest allowed
case-different declaration -> distinct quest
distinct active quests coexist
16 active -> HP133
wrong bound context -> HP134
finished retry -> original persisted result
```

---

## 0.0.7 — Paths/config/project identity

```text
platform app-data roots
HERO_PASSPORT_HOME
configVersion 1
project-identity/1
Git common-dir anchor
linked-worktree convergence
explicit monorepo scope
submodule/nested-repo separation
Git trust/error handling
--hero binding
```

Gate: all `PROJECT-IDENTITY.md` vectors + no persisted path/remote URL.

---

## 0.0.8 — SQLite schema/migration 0001

```text
projects identity version/fingerprint
quest_sessions dedup_key + version
partial open dedup uniqueness
active query index
reports/report skills/traits/xp ledger
UNIQUE quest_reports.quest_id
UNIQUE xp_events.quest_id
```

Gate: real fresh SQLite schema/constraints/PRAGMAs.

---

## 0.0.9 — Persistence reliability

Implement `PERSISTENCE-RELIABILITY.md`:

```text
non-deferred Serializable write transaction before invariant reads
qualified BEGIN IMMEDIATE provider behavior
count=15 two-writer start race -> exactly 16
finish race -> one reward
busy/error mapping
child-process crash before/after commit
WAL recovery without manual file deletion
BackupDatabase + verification
actual SQLite >=3.51.3 qualification
same-host local writable storage profile
```

Gate: repeated real file-backed concurrency/crash/backup suite.

---

## 0.0.10 — CLI/presentation/doctor

```text
init
mcp --project-root --hero
doctor
card
quest list --active
export
data path
--version
RU/EN renderer
```

Doctor adds project/SQLite/WAL/migration/integrity qualification without destructive auto-repair.

---

## 0.0.11 — Exact HP-MCP/2 stdio

```text
four explicit tools
runtime validation
ProtocolVersion unset/null
start idempotent=false
finish/list/card idempotent=true
closed input/output schemas
structuredContent + equivalent JSON TextContent success
safe isError TextContent-only business errors
server instructions/cache metadata
contract snapshot generator
```

Gate: exhaustive `WIRE-CONTRACT.md` vectors.

---

## 0.0.12 — Protocol/process compatibility

```text
MCP 2026-07-28 path
MCP 2025-11-25 path
stdio stdout purity
MCP Inspector
compatibility JSON TextContent proof
```

---

## 0.0.13 — Codex reference qualification + AgentEvals

Codex fresh/restart/parallel/retry E2E + host-neutral model behavior scenarios.

Gate: Codex qualifies as reference host for RC.

---

## 0.0.14 — Cross-host qualification pack

Current official-doc recheck + release smoke for:

```text
VS Code
JetBrains
Zed
Cursor
Claude Code
ChatGPT private tunnel profile
```

Record Qualified vs Documented honestly. No host-specific runtime SDKs.

---

## 0.1.0-rc.1 — Fitness/packaging qualification

```text
locked restore/build/full deterministic tests
NuGet audit
architecture/privacy/stale-contract gates
fresh + previous DB migrations
real concurrency/crash/backup suite
actual sqlite_version per published artifact
Inspector + Codex E2E + AgentEvals
supported OS/RID smoke
cross-host evidence matrix
```

No feature expansion after RC except required gate fixes.

---

## 0.1.0 — Portable Local MCP Core

Definition:

```text
local stdio HP-MCP/2
transport-neutral Application
project-identity/1
multi-agent-safe retry/cap model
crash/race-safe SQLite
CLI/doctor
Codex Qualified
other hosts evidence-tiered
```

Dashboard not required.

---

## 0.1.1 — Integration/distribution polish

Evidence-driven only:

```text
integration show <host> snippet renderer
broader automated host smoke
packaging ergonomics
MCP Registry publication if appropriate
additional Qualified hosts
```

No API expansion for version-number optics.

---

## 0.2.0 — Local Blazor dashboard

```text
hero card
XP
Trust/Risk
skills/traits
recent + active quests
project stats
```

Uses Application/read models, no duplicated RPG rules or DbContext in Razor components.

---

## HTTP trigger milestone

Own Streamable HTTP enters only after a concrete URL consumer/deployment requirement and a new security/project-binding design.

Likely first profile:

```text
project-scoped Streamable HTTP
official ASP.NET MCP package
explicit stateless HTTP mode
loopback/private security profile
same HP-MCP semantics
```

No new legacy SSE.

---

## Hosted/public phase — separate architecture

Before public/team service, design:

```text
identity/OAuth
authorization
multi-tenant ownership
remote persistence
backup/retention
rate/abuse controls
public HTTPS
```

Do not expose local SQLite/binding architecture publicly as-is.

---

## Post-MVP deferred

```text
Achievements/items/artifacts
runtime plugin ABI
self-evolution
LLM judge
continuous telemetry
cloud sync/team dashboards
MCP Apps/Tasks
required Resources/Prompts
REST/GraphQL/gRPC
ACP agent
project relink/attempt model unless real usage demands it
```

Every deferred feature identifies a product problem and boundary before implementation.
