# Hero Passport — roadmap

**Status:** Accepted implementation sequence v2  
**Snapshot:** 2026-08-10  
**Goal:** reach a tested Codex-first local MVP before dashboard expansion

## 1. Roadmap philosophy

Versions are **working gates**, not arbitrary file-count milestones.

Each milestone must leave the repository buildable/testable and remove a specific implementation risk. Do not pull later product features into an earlier milestone merely because a convenient extension seam exists.

The order is deliberately:

```text
reproducible foundation
-> pure game rules
-> application lifecycle
-> persistence integrity
-> local CLI/diagnostics
-> MCP protocol
-> real Codex behavior
-> release hardening
-> dashboard
```

This sequence is derived from the current MCP ecosystem analysis and official platform behavior. It prioritizes tool-contract correctness and agent-evaluation before visual expansion.

---

## 2. 0.0.1 — reproducible foundation

### Deliverables

```text
global.json pinned .NET SDK
Directory.Build.props
Directory.Packages.props
NuGet lock-file policy
.editorconfig
.gitignore
HeroPassport.slnx
Domain/Application/Infrastructure/App projects
5 deterministic test projects + AgentEvals harness skeleton
CI baseline
```

### Dependency baseline

```text
.NET SDK 10.0.302
ModelContextProtocol 2.0.0
EF Core SQLite 10.0.10
SQLitePCLRaw.bundle_e_sqlite3 3.0.5
System.CommandLine 2.0.10
xunit.v3 3.2.2
```

No preview package.

### Gates

```text
dotnet restore --locked-mode
dotnet build -c Release
dotnet test -c Release
format/static analysis clean
NuGet audit policy visible
```

### Architecture proof

Architecture tests prove project dependency direction from the first commit that introduces projects.

---

## 3. 0.0.2 — domain vocabulary and rules

### Deliverables

```text
typed IDs
quest/result canonical types
skill canonical keys + normalizer
QuestQualityFlags
RewardBreakdown
XP formula/95-XP golden
level curve
skill XP distribution
Trust/Risk v1
3 traits v1
RuleVersions
```

### Important constraint

No localized text rendering in Domain.

No EF/MCP types.

### Gates

Boundary/golden tests defined in `ENGINE-SPEC.md` pass.

---

## 4. 0.0.3 — Application contracts and lifecycle

### Deliverables

```text
HeroResult<T>/stable errors
Application request/result records
StartQuestHandler
FinishQuestHandler
GetCurrentQuestHandler
GetHeroCardHandler
ports for stores/project identity/active hero/paths
TimeProvider integration
```

### Behavioral gates

```text
matching start retry -> same quest
conflicting open quest -> HP132
finish retry -> original persisted outcome abstraction
no Application reference to MCP SDK
no localized displayText in Application
```

Persistence is faked here to prove use-case semantics before EF complexity.

---

## 5. 0.0.4 — configuration, paths and presentation

### Deliverables

```text
platform-correct IAppDataPaths
HERO_PASSPORT_HOME isolation override
config.json v1 strict schema/options
HeroTextRenderer RU/EN
compact/normal local presentation
canonical RU terminology goldens
```

### Gates

```text
Windows LocalApplicationData mapping
Linux XDG mapping
macOS Application Support mapping
unknown config property rejected
presentation goldens independent from rule goldens
```

This milestone deliberately removes stable preferences from the model-facing MCP schema before MCP is implemented.

---

## 6. 0.0.5 — SQLite schema and initialization

### Deliverables

```text
HeroPassportDbContext
IDbContextFactory registration
entity configurations
migration 0001
initializer
canonical seeds
SQLite native-version check
WAL/FULL/FK setup verification
```

### Tables

```text
heroes
projects
hero_project_stats
quest_sessions
quest_reports
quest_report_skills
skills
hero_skills
traits
hero_traits
xp_events
app_settings
```

### Important implementation rules

```text
file-backed SQLite tests
sync DB I/O
no Task.Run wrapper
no EnsureCreated product path
no custom migration mutex
```

### Gates

Fresh DB migration/integrity/PRAGMA/version tests pass.

---

## 7. 0.0.6 — transactional stores and idempotency

### Deliverables

```text
EF store/query adapters
project fingerprint resolver
active hero provider
atomic start transaction
atomic finish transaction
UNIQUE xp_events.quest_id race handling
read models
```

### Concurrency gates

```text
two finishers -> exactly one XP event
retry returns canonical original report
read remains available during short WAL writer
bounded busy -> HP202
no partial reward after injected write failure
```

This milestone is the storage-correct core product without CLI/MCP polish.

---

## 8. 0.0.7 — CLI and doctor

### Deliverables

```text
hero-passport init
hero-passport doctor
hero-passport card
hero-passport quest current
hero-passport export
hero-passport data path
hero-passport mcp command dispatch stub/host
--version
--help
--json where script-useful
```

### Doctor baseline

```text
app/runtime/OS
data/config/state status
config validity
DB/native SQLite/migrations
WAL/FULL/FK
migration-lock diagnostics
seed/default hero
MCP manifest version/hash
```

### Gates

Process tests verify exit codes/stdout/stderr and test-home isolation.

No rich-console dependency required.

---

## 9. 0.0.8 — MCP stdio contract

### Deliverables

Official C# SDK 2.0.0 stdio host and exactly four explicit tool adapters:

```text
StartQuestTool
FinishQuestTool
CurrentQuestTool
GetCardTool
```

Plus:

```text
HeroPassportMcpManifest
server instructions
strict JSON schemas
output schemas
structuredContent
annotations
task support forbidden
presentation renderer integration
stdout isolation
```

### Explicit non-deliverables

```text
assembly-wide tool scanning
dynamic discovery/toolsets
HTTP/OAuth
Tasks
Apps
resources/prompts
admin/history MCP tools
```

### Gates

```text
tools/list exact 4 + exact order
schema/annotation goldens
catalog size budget
actual output validates outputSchema
negative input schema cases
stdout guard
MCP Inspector smoke
```

---

## 10. 0.0.9 — Codex integration and agent evals

### Deliverables

```text
current official Codex setup docs
native `codex mcp add` path
project `cwd` config example
host enabled_tools guidance
AGENTS snippet
Codex E2E scripts/checklist
AgentEvals corpus
```

### Eval corpus

At least 10 scenarios from `TESTING-QUALITY.md`.

### Gates

Real current Codex demonstrates:

```text
meaningful task -> start once -> finish once
trivial task -> no unnecessary quest in expected eval
reconnect/current recovery
no forbidden data sent
compact final display only
persistent state across restart
```

Tool description/server instruction changes are now evaluated, not guessed.

---

## 11. 0.1.0-rc.1 — release hardening

### Deliverables

```text
cross-platform package/install smoke
.NET tool package
Windows/Linux/macOS claimed-platform qualification
locked restore/audit policy
migration upgrade fixture
export schema v1 if export is included
README/install/troubleshooting
privacy/security review
performance smoke measurements
```

### Critical review pass

Re-read all normative docs and scan for stale architecture-v1 terminology:

```text
schemaVersion in every MCP call
locale/outputMode in every MCP call
agentHint/statusText
workspacePath wire/storage
%APPDATA% DB
async SQLite requirement
custom migration lock
Domain display text
runtime plugins/achievements in MVP
```

No unresolved contradiction proceeds to release.

---

## 12. 0.1.0 — minimal MVP

0.1.0 is complete only when:

- documented release gates pass;
- Codex E2E passes on recorded supported version;
- clean coding golden produces 95 XP;
- no retry can duplicate XP;
- state survives process restarts;
- exact 4-tool MCP contract is stable;
- app-data/config behavior is platform-correct;
- `doctor` diagnoses core setup/storage state;
- no source/diff/raw-log/cloud dependency exists;
- docs describe implementation as shipped, not aspirational.

At this point MCP tool/schema/name changes enter compatibility policy.

---

## 13. 0.2.0 — local dashboard

### Goal

Make existing progression visually enjoyable without rewriting backend/domain logic.

### Technical baseline

```text
HeroPassport.Web
ASP.NET Core / Blazor Web App .NET 10
Application read models
Infrastructure via composition root
IDbContextFactory pattern retained
```

### First screens

```text
hero card
XP/level progress
Trust/Risk
skills
traits
recent quest history
last reward breakdown
project stats
```

### Gates

```text
no DbContext injection into Razor components
no duplicated reward logic
no remote listen by default
HTML-safe rendering of untrusted goal/summary
same database/read models as CLI/MCP
```

---

## 14. Post-0.2 candidates — not commitments

Evaluate independently:

```text
portable import
richer traits
history filters/compare
hero profile customization
optional local evidence adapters
selective MCP resource if a real client workflow needs it
additional agent/client compatibility matrix
```

Require new design before:

```text
HTTP/remote MCP
OAuth
cloud sync
multi-user/team
MCP Apps
Tasks
runtime plugins
achievements/items
self-evolution
LLM judging
source/diff ingestion
```

---

## 15. Tool-growth threshold

If MCP inventory would exceed 6 tools, stop normal feature delivery and run a dedicated tool-surface review.

Questions:

```text
Can CLI/dashboard own the feature?
Can it be merged into an existing typed operation without semantic ambiguity?
Does it need to be advertised every session?
What does agent eval show?
What is catalog-size/token impact?
Would progressive disclosure/resources be better?
```

Only after measured need consider GitHub-MCP-like dynamic discovery/toolsets.

---

## 16. Dependency review cadence

Before each release candidate:

```text
check current stable .NET servicing SDK/runtime
ModelContextProtocol stable release/protocol changes
EF Core SQLite stable servicing
SQLitePCLRaw/native SQLite security baseline
System.CommandLine stable line
xUnit stable line
NuGet vulnerabilities
Codex official MCP/config changes
```

Do not auto-upgrade majors during release hardening. Update one dependency family at a time with corresponding tests/evals.

---

## 17. Documentation as a release artifact

Architecture-changing PRs must update the applicable documents in the same change:

```text
PRODUCT-SPEC
ARCHITECTURE
MCP-CONTRACT
ENGINE-SPEC
DATA-MODEL
CONFIGURATION
SECURITY-PRIVACY
TESTING-QUALITY
DEPENDENCIES
CODEX integration
DECISION-LOG
ROADMAP/implementation plan
```

Not every PR changes every file, but leaving a normative contradiction is a release failure.

---

## 18. PR slicing recommendation

After architecture PR #1 is merged, implementation should use focused PRs roughly aligned to milestones, not one enormous 0.1 PR.

Recommended:

```text
PR foundation
PR domain rules
PR application/config/presentation
PR SQLite/migrations
PR transactions/idempotency
PR CLI/doctor
PR MCP contract
PR Codex eval/E2E
PR RC/release hardening
```

Each PR is independently reviewable and test-complete.
