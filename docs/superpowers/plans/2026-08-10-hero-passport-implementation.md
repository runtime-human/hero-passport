# Hero Passport Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver Hero Passport `0.1.0`: a cross-platform, local-first Codex-compatible stdio MCP server with deterministic RPG progression, retry-safe SQLite persistence, strict privacy boundaries, operator CLI/doctor, compact localized status output, and agent-evaluated lifecycle behavior.

**Architecture:** Build a modular monolith with a pure Domain, typed Application use cases/ports, Infrastructure EF Core/SQLite/config/path adapters, and one App executable that hosts CLI + explicit four-tool MCP + presentation. Database work is short/synchronous through `IDbContextFactory`; MCP is narrow, typed and stateless at protocol level; Codex behavior is validated with dedicated agent evals. Blazor is outside this plan and begins at 0.2.0.

**Tech Stack:** C# 14, .NET SDK 10.0.302 / `net10.0`, ModelContextProtocol 2.0.0, EF Core SQLite 10.0.10, SQLitePCLRaw.bundle_e_sqlite3 3.0.5, System.CommandLine 2.0.10, xUnit.net v3 3.2.2, Microsoft Testing Platform/current pinned .NET tooling.

## Global Constraints

- Use only stable package versions accepted in `docs/DEPENDENCIES.md`; a preview needs an ADR.
- Package versions live in Central Package Management; CI/release uses lock files/locked restore.
- Target exactly `net10.0` / C# 14 for MVP projects.
- Domain has no EF/MCP/CLI/JSON/filesystem/localization dependency.
- Application has no MCP SDK, EF implementation or localized `displayText` rendering.
- App presentation renders localized text from typed Application results.
- MCP exposes exactly four explicitly registered tools in canonical order; no assembly-wide scanning.
- MCP input objects reject additional properties; no arbitrary metadata/context bags.
- Do not accept/persist source code, file contents, diffs, raw logs, prompts/chat history, secrets, environment dumps or full workspace paths.
- Stable local state (hero/project/locale/presentation) is resolved locally, not repeated in MCP calls.
- `hero-passport mcp` stdout is protocol only; diagnostics go to stderr/local logging.
- Use `IDbContextFactory<HeroPassportDbContext>` with short-lived contexts.
- SQLite database operations are synchronous/short; do not wrap them in `Task.Run`.
- Required SQLite state: WAL, `synchronous=FULL`, foreign keys ON; initial busy timeout policy 5s subject to release validation.
- Use EF migrations from first schema; no `EnsureCreated` product path and no custom migration mutex.
- A completed quest can have at most one XP ledger event (`UNIQUE quest_id`).
- Repeated finish returns persisted original outcome and never recalculates new rules.
- Use built-in `TimeProvider`, `Guid.CreateVersion7()` and `System.Text.Json` unless a documented gap appears.
- Persist canonical skill/trait/rule keys; localization is presentation-only.
- Russian labels: `scope_control=Контроль`, clean-scope bonus=`Бонус за контроль`, scope violation=`Выход за задачу`.
- Storage tests use real temporary file-backed SQLite.
- MCP name/description/schema/instruction changes require relevant agent evals.
- Keep normative docs consistent in the same PR as architecture/contract/rule/storage changes.

---

## Planned repository structure

```text
global.json
Directory.Build.props
Directory.Packages.props
.editorconfig
.gitignore
HeroPassport.slnx

src/
  HeroPassport.Domain/
    Heroes/
    Projects/
    Quests/
    Rewards/
    Skills/
    Traits/
    Shared/
  HeroPassport.Application/
    Abstractions/
    Contracts/
    Heroes/
    Projects/
    Quests/
      StartQuest/
      FinishQuest/
      GetCurrentQuest/
    Cards/GetHeroCard/
    Initialization/
    Diagnostics/
    Export/
  HeroPassport.Infrastructure/
    Persistence/
      Entities/
      Configurations/
      Migrations/
      Stores/
      Queries/
    Configuration/
    Paths/
    Projects/
    Diagnostics/
    Export/
  HeroPassport.App/
    Hosting/
    Cli/
    Mcp/Tools/
    Presentation/Localization/

tests/
  HeroPassport.Domain.Tests/
  HeroPassport.Application.Tests/
  HeroPassport.Infrastructure.Tests/
  HeroPassport.App.Tests/
  HeroPassport.Architecture.Tests/
  HeroPassport.AgentEvals/
```

---

### Task 1: Reproducible .NET foundation

**Files:**
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `.editorconfig`
- Create: `.gitignore`
- Create: `HeroPassport.slnx`
- Create project files under `src/` and `tests/`
- Create: CI workflow(s) under `.github/workflows/`

**Interfaces:**
- Produces the project graph and dependency/package policy every later task uses.

- [ ] **Step 1: Create the solution/project skeleton with no business implementation.**

Use `net10.0`, nullable enabled, deterministic build, analyzers/code style enabled. Add only project references permitted by `docs/ARCHITECTURE.md`.

- [ ] **Step 2: Pin SDK/package versions.**

`global.json` pins SDK 10.0.302 with prerelease disabled. `Directory.Packages.props` owns all package versions from `docs/DEPENDENCIES.md`.

- [ ] **Step 3: Configure lock/audit behavior and restore.**

Generate/commit lock files as appropriate for the chosen PackageReference layout. Verify the exact NuGet audit properties/warning IDs using the pinned SDK instead of guessing them from older SDK docs.

- [ ] **Step 4: Add architecture smoke test.**

Write a failing test that discovers project references and asserts Domain has no product dependency and Application references only Domain; implement only enough project structure to pass.

- [ ] **Step 5: Run foundation verification.**

```bash
dotnet --info
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet format --verify-no-changes
```

Also run locked restore once lock files exist.

- [ ] **Step 6: Commit.**

```text
build: bootstrap reproducible .NET 10 solution
```

**Acceptance:** clean clone with pinned SDK restores/builds/tests; no preview dependency; project graph matches architecture.

---

### Task 2: Domain vocabulary and typed rule model

**Files:**
- Create: `src/HeroPassport.Domain/Quests/QuestType.cs`
- Create: `src/HeroPassport.Domain/Quests/QuestResult.cs`
- Create typed IDs/value records under relevant feature folders
- Create: `src/HeroPassport.Domain/Rewards/QuestQualityFlags.cs`
- Create: `src/HeroPassport.Domain/Rewards/RewardBreakdown.cs`
- Create: `src/HeroPassport.Domain/Rewards/RuleVersions.cs`
- Create matching tests in `HeroPassport.Domain.Tests`

**Interfaces:**
- Produces canonical quest/result/ID/rule types used by every engine/Application task.

- [ ] **Step 1: Write failing tests for canonical quest/result values and UUIDv7-generated ID round-trip.**
- [ ] **Step 2: Implement minimal typed values/enums without serialization/infrastructure attributes.**
- [ ] **Step 3: Write failing tests for `QuestQualityFlags` derivation inputs/boundaries.**
- [ ] **Step 4: Implement the pure quality flag model and `RuleVersions` constants.**
- [ ] **Step 5: Run Domain tests and architecture tests.**
- [ ] **Step 6: Commit.**

```text
feat: define Hero Passport domain vocabulary
```

**Acceptance:** Domain contains no localization/EF/MCP/JSON/filesystem APIs.

---

### Task 3: Reward and level engine

**Files:**
- Create: `src/HeroPassport.Domain/Rewards/RewardCalculator.cs`
- Create: `src/HeroPassport.Domain/Heroes/LevelCurve.cs`
- Create golden/boundary tests.

**Interfaces:**
- `RewardCalculator.Calculate(...) -> RewardBreakdown`
- `LevelCurve.FromTotalXp(long/int totalXp) -> level projection`

- [ ] **Step 1: Write the 95-XP golden as a failing test.**

Expected calculation exactly:

```text
60 + 10 + 10 + 10 + 5 = 95
```

- [ ] **Step 2: Add failing tests for each quest type/result/bonus/penalty and zero floor.**
- [ ] **Step 3: Implement integer-permille reward calculation.**
- [ ] **Step 4: Add before/at/after threshold tests for levels 1-6 and formula consistency.**
- [ ] **Step 5: Implement `LevelCurve`.**
- [ ] **Step 6: Run complete Domain suite and commit.**

```text
feat: implement deterministic reward and level rules
```

**Acceptance:** no floating-point reward arithmetic; all goldens exact.

---

### Task 4: Skills, Trust/Risk and traits

**Files:**
- Create: `Skills/SkillKeys.cs`
- Create: `Skills/SkillKeyNormalizer.cs`
- Create: `Skills/SkillXpDistributor.cs`
- Create: `Heroes/TrustRiskCalculator.cs`
- Create trait rules/types under `Traits/`
- Add exhaustive tests.

**Interfaces:**
- Normalizer maps documented alias -> canonical key or typed failure.
- Distributor accepts XP + 1..3 canonical skills and returns exact-sum allocations.
- Trust/Risk and trait policies return typed deltas/progress.

- [ ] **Step 1: Write failing canonical/alias/unknown/duplicate skill tests.**
- [ ] **Step 2: Implement deterministic normalizer.**
- [ ] **Step 3: Write failing distribution goldens including `95 -> 47/29/19`.**
- [ ] **Step 4: Implement cumulative-floor distribution and assert sum invariant.**
- [ ] **Step 5: Write Trust/Risk boundary/clamp tests and implement v1.**
- [ ] **Step 6: Write three trait threshold/permanent-unlock tests and implement v1.**
- [ ] **Step 7: Run all Domain tests and commit.**

```text
feat: add skills trust risk and traits rules
```

---

### Task 5: Application contracts, results and lifecycle

**Files:**
- Create: `Application/Contracts/*`
- Create: `Application/Abstractions/*`
- Create: `StartQuestHandler.cs`
- Create: `FinishQuestHandler.cs`
- Create: `GetCurrentQuestHandler.cs`
- Create: `GetHeroCardHandler.cs`
- Create Application fake-store tests.

**Interfaces:**

Application contracts intentionally do not mirror the old MCP-v1 DTO baggage. Conceptual requests:

```text
StartQuestCommand(QuestType, Goal)
FinishQuestCommand(QuestId, Result, Summary, Metrics, SkillsUsed)
GetCurrentQuestQuery()
GetHeroCardQuery()
```

Use a tiny project-owned typed result/error abstraction; do not add a Result NuGet package.

- [ ] **Step 1: Write failing handler tests with in-memory fake ports and fake `TimeProvider`.**
- [ ] **Step 2: Define capability-specific ports (`IQuestStore`, `IHeroStore`, `IProjectStore`, identity/active hero/read ports).**
- [ ] **Step 3: Implement `StartQuestHandler` matching-retry/conflict semantics.**
- [ ] **Step 4: Implement `FinishQuestHandler` orchestration against transaction/store abstraction and pure Domain rules.**
- [ ] **Step 5: Implement current/card read handlers.**
- [ ] **Step 6: Architecture-test no MCP/EF references and no `displayText` field in Application results.**
- [ ] **Step 7: Commit.**

```text
feat: implement application quest lifecycle
```

**Acceptance:** Application lifecycle is fully testable without EF/MCP.

---

### Task 6: Platform paths, strict configuration and presentation

**Files:**
- Create Infrastructure path/config adapters
- Create App `Presentation/HeroTextRenderer.cs`
- Create RU/EN localization resources/mappings
- Add config/path/presentation tests.

**Interfaces:**
- `IAppDataPaths.Current`
- typed config v1
- renderer consumes typed Application result/read model and locale/presentation mode.

- [ ] **Step 1: Write path tests for Windows LocalApplicationData, Linux XDG, macOS Application Support and `HERO_PASSPORT_HOME`.**
- [ ] **Step 2: Implement pure path resolver + explicit directory initializer separation.**
- [ ] **Step 3: Write config parser/validation tests: defaults, precedence, malformed JSON, unknown property, unsupported version.**
- [ ] **Step 4: Implement strict config v1 with `System.Text.Json`/Options validation.**
- [ ] **Step 5: Write presentation goldens including canonical RU terminology and compact status size limits.**
- [ ] **Step 6: Implement `HeroTextRenderer`; keep Domain/Application text-free.**
- [ ] **Step 7: Commit.**

```text
feat: add platform config paths and presentation
```

---

### Task 7: EF Core SQLite schema, migration 0001 and initialization

**Files:**
- Create `HeroPassportDbContext`
- Create persistence entities/configurations
- Create migration 0001
- Create initializer/native-version/PRAGMA checks
- Create real file-backed SQLite tests.

**Interfaces:**
- Infrastructure implements Application persistence/initialization ports.
- Uses `IDbContextFactory<HeroPassportDbContext>`.

- [ ] **Step 1: Write failing migration/schema tests against temp-file SQLite.**

Assert tables, FKs, unique constraints and active-quest uniqueness design.

- [ ] **Step 2: Configure `IDbContextFactory` and explicit EF entity configurations.**
- [ ] **Step 3: Scaffold/review migration 0001; add localized SQLite SQL only where exact partial-index semantics require it.**
- [ ] **Step 4: Implement initialization with `SELECT sqlite_version()`, migrations and required PRAGMA verification.**
- [ ] **Step 5: Assert WAL + FULL + FK and accepted native SQLite minimum.**
- [ ] **Step 6: Prove product initialization uses migrations, not `EnsureCreated`.**
- [ ] **Step 7: Verify no custom migration mutex; document/test EF lock diagnostics path.**
- [ ] **Step 8: Run Infrastructure suite and commit.**

```text
feat: add SQLite schema migrations and initialization
```

---

### Task 8: Transactional stores and race-safe idempotency

**Files:**
- Create EF store/query implementations
- Create transaction/coordinator implementation as required by Application contract
- Create project identity resolver/fingerprint
- Add failure-injection and concurrency integration tests.

**Interfaces:**
- Short synchronous DB operations only.
- Finish writes all progression atomically.

- [ ] **Step 1: Write a failing successful-finish integration test proving all report/XP/skill/trait/project mutations.**
- [ ] **Step 2: Implement stores/transaction with one short-lived context/unit of work.**
- [ ] **Step 3: Add injected-failure tests at report/event/hero/skill/trait stages; assert rollback.**
- [ ] **Step 4: Add two-finisher concurrency test; assert exactly one XP event and one hero XP change.**
- [ ] **Step 5: Implement uniqueness-race recovery by reading canonical completed outcome—not recalculating/retrying mutation.**
- [ ] **Step 6: Add busy-timeout behavior test and stable `HP202` translation.**
- [ ] **Step 7: Add WAL reader-during-writer smoke.**
- [ ] **Step 8: Commit.**

```text
feat: make quest persistence atomic and retry safe
```

---

### Task 9: CLI, init and doctor

**Files:**
- Create `App/Cli/*`
- Create `App/Hosting/*`
- Create diagnostics Application/Infrastructure implementation
- Add process tests.

**Interfaces:**

Initial commands:

```text
init
mcp
doctor
card
quest current
export
data path
--help
--version
```

- [ ] **Step 1: Write CLI parser/help/exit-code tests.**
- [ ] **Step 2: Build System.CommandLine tree and composition root without rich-console dependency.**
- [ ] **Step 3: Implement `init` over initializer.**
- [ ] **Step 4: Implement `doctor` typed diagnostic read model and safe human/JSON presentation.**
- [ ] **Step 5: Implement card/current/export/data-path commands using Application/read models.**
- [ ] **Step 6: Process-test stdout/stderr and isolated `HERO_PASSPORT_HOME`.**
- [ ] **Step 7: Commit.**

```text
feat: add Hero Passport CLI and doctor
```

---

### Task 10: Exact MCP stdio implementation

**Files:**
- Create `App/Mcp/HeroPassportMcpManifest.cs`
- Create four files under `App/Mcp/Tools/`
- Create MCP mapping/result DTOs if SDK requires transport-specific wrappers
- Add actual-server contract/process tests.

**Interfaces:**
- Exact HP-MCP/1 contract in `docs/MCP-CONTRACT.md`.

- [ ] **Step 1: Write a failing test that starts/inspects the server and expects exactly four names in exact order.**
- [ ] **Step 2: Register the four tool types explicitly using official SDK 2.0 supported APIs; do not call assembly-wide discovery.**
- [ ] **Step 3: Add concise server instructions with essential first-512-character workflow/privacy content.**
- [ ] **Step 4: Implement `StartQuestTool` thin mapping -> Application -> renderer.**
- [ ] **Step 5: Implement `FinishQuestTool` thin mapping -> Application -> renderer.**
- [ ] **Step 6: Implement `CurrentQuestTool` and `GetCardTool`.**
- [ ] **Step 7: Define/check input/output schemas, strict additional-property behavior and exact annotations/task support.**
- [ ] **Step 8: Validate actual structured-content/TextContent fallback behavior against SDK 2.0 + MCP Inspector; avoid unnecessary full JSON duplication while staying conformant.**
- [ ] **Step 9: Add catalog-size and description/displayText budget tests.**
- [ ] **Step 10: Add process stdout guard including startup/storage failure path.**
- [ ] **Step 11: Run MCP Inspector smoke and all App tests.**
- [ ] **Step 12: Commit.**

```text
feat: expose strict Hero Passport MCP stdio tools
```

---

### Task 11: Codex integration and agent evaluation harness

**Files:**
- Implement `tests/HeroPassport.AgentEvals/*`
- Update/finalize `docs/integrations/CODEX.md` with tested commands/config
- Add scripts/fixtures for isolated E2E if helpful.

**Interfaces:**
- Uses current official Codex CLI/config; does not mutate Codex config from product code.

- [ ] **Step 1: Record current Codex version/build and verify `codex mcp add/list` syntax from official docs.**
- [ ] **Step 2: Install/register local built Hero Passport through native Codex MCP flow.**
- [ ] **Step 3: Execute meaningful coding/debugging/planning evals and record tool call sequence.**
- [ ] **Step 4: Execute trivial-task eval expecting no unnecessary quest in the tested policy.**
- [ ] **Step 5: Test matching/conflicting open quest and reconnect/current recovery.**
- [ ] **Step 6: Test privacy-adversarial prompt; verify no forbidden content enters Hero Passport calls/logs/storage.**
- [ ] **Step 7: Test explicit Codex `mcp_servers.hero-passport.cwd` path; ensure workspace path remains outside MCP/database.**
- [ ] **Step 8: Restart client/server and prove durable state.**
- [ ] **Step 9: Tune only descriptions/server instructions if eval behavior is poor; rerun deterministic tests after every change.**
- [ ] **Step 10: Commit.**

```text
test: add Codex end to end agent evaluations
```

---

### Task 12: Architecture, privacy and dependency fitness gates

**Files:**
- Extend `HeroPassport.Architecture.Tests`
- Add log/privacy sentinel tests
- Add package/config checks as needed.

- [ ] **Step 1: Assert project/layer dependency rules.**
- [ ] **Step 2: Assert MCP manifest count/order and no assembly-wide registration pattern.**
- [ ] **Step 3: Reflect/inspect MCP contracts for forbidden broad/privacy fields.**
- [ ] **Step 4: Assert every input is strict/bounded and every tool has output schema/annotations.**
- [ ] **Step 5: Capture logs while using secret/path/goal sentinels; assert forbidden values do not leak.**
- [ ] **Step 6: Verify CPM/no stray package versions and locked restore.**
- [ ] **Step 7: Run NuGet audit with pinned SDK and implement agreed release severity gate.**
- [ ] **Step 8: Commit.**

```text
test: enforce architecture privacy and dependency boundaries
```

---

### Task 13: 0.1.0-rc.1 packaging and release qualification

**Files:**
- Packaging metadata/scripts/workflow
- release/troubleshooting docs
- migration fixture data as appropriate
- changelog/version files selected during Task 1.

- [ ] **Step 1: Build/package the .NET tool and test clean install/uninstall/invoke.**
- [ ] **Step 2: Run Windows x64 qualification.**
- [ ] **Step 3: Run Linux x64 qualification.**
- [ ] **Step 4: Run macOS arm64 qualification before claiming support.**
- [ ] **Step 5: On every claimed platform verify native SQLite version, init, WAL/FULL/FK, CLI and MCP stdio.**
- [ ] **Step 6: Run fresh DB + previous-version upgrade migration tests and pending-model check.**
- [ ] **Step 7: Record startup/start/finish/current/card smoke timings; investigate obvious regressions without speculative optimization.**
- [ ] **Step 8: Run locked restore/audit, complete tests, Inspector, Codex E2E and agent eval review.**
- [ ] **Step 9: Search repository for superseded architecture-v1 terms and fix contradictions.**

Search at least:

```text
workspacePath
agentHint
statusText
outputMode
%APPDATA%
WithToolsFromAssembly
EnsureCreated
Task.Run
migration mutex
Achievements
```

- [ ] **Step 10: Commit/tag RC according to repository versioning policy.**

```text
chore: qualify Hero Passport 0.1.0 rc1
```

---

### Task 14: 0.1.0 release

**Files:** release notes/docs/version only unless an RC defect requires a separately reviewed fix.

- [ ] **Step 1: Confirm no unresolved P0/P1 defect or documentation contradiction from RC.**
- [ ] **Step 2: Re-run official MCP/Codex/package version check for changes since architecture snapshot; do not silently major-upgrade at the release boundary.**
- [ ] **Step 3: Re-run all release gates on final commit.**
- [ ] **Step 4: Publish/tag `0.1.0` through the repository's verified release path.**
- [ ] **Step 5: Record exact supported Codex/.NET/native SQLite/platform evidence in release notes.**

```text
chore: release Hero Passport 0.1.0
```

---

## Post-plan boundary

This implementation plan stops at `0.1.0`.

Do not add these while executing it:

```text
HeroPassport.Web dashboard (starts 0.2)
achievements/items
runtime plugins
HTTP/OAuth MCP
MCP Apps/Tasks
cloud/team/auth
LLM judge
continuous telemetry
source/diff ingestion
full MCP history/admin API
```

If a real blocker appears that seems to require one of them, write a focused ADR/design change instead of smuggling architecture expansion into an implementation task.

## Final self-review checklist

Before calling this plan implemented:

```text
[ ] Every PRODUCT-SPEC 0.1 success criterion has a test/evidence step.
[ ] Every MCP tool has exact implementation + actual-server tests.
[ ] 95-XP golden is stable.
[ ] Finish race/idempotency is proven on real SQLite.
[ ] No async-SQLite fiction or custom migration lock was reintroduced.
[ ] Platform paths match CONFIGURATION.md.
[ ] App renderer owns localized display text.
[ ] Dependency set still matches DEPENDENCIES.md.
[ ] Codex E2E and agent evals were actually run, not inferred.
[ ] Documentation describes shipped behavior.
```
