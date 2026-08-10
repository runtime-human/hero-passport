# Hero Passport Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver Hero Passport `0.1.0`: a cross-platform, local-first Codex-compatible stdio MCP server with deterministic RPG progression, retry-safe SQLite persistence, CLI operations, compact status output, and no source-code telemetry.

**Architecture:** Implement a modular monolith with a pure Domain project, Application use cases/ports/contracts, Infrastructure EF Core/SQLite adapters, and a single App executable that hosts CLI and MCP stdio. Build from rules outward: goldens first, then application lifecycle, real SQLite integrity, CLI, MCP, Codex E2E and release hardening. Dashboard is explicitly outside this plan and starts at `0.2.0`.

**Tech Stack:** C# 14, .NET SDK 10.0.302 / net10.0, ModelContextProtocol 2.0.0, EF Core SQLite 10.0.10, SQLitePCLRaw.bundle_e_sqlite3 3.0.5, System.CommandLine 2.0.10, xUnit.net v3 3.2.2, Microsoft Testing Platform.

## Global Constraints

- Target exactly `net10.0` and C# 14 for MVP projects.
- Use stable package versions approved in `docs/ARCHITECTURE.md`; no preview dependency without an ADR.
- `hero-passport mcp` stdout is MCP protocol only; diagnostics go to stderr or explicit local log sink.
- MCP MVP exposes only `hero.start_quest`, `hero.finish_quest`, `hero.current_quest`, `hero.get_card`, in deterministic order.
- Do not accept/persist source code, file contents, diffs, raw logs, full prompts/chat history, secrets, environment bags, or full workspace paths.
- Use deterministic integer rule calculations and persist rule versions.
- A completed quest can produce at most one reward XP ledger event.
- Repeated `finish_quest` returns the original persisted outcome; it never recalculates under newer rules.
- Use injected `TimeProvider` in behavior code.
- Persist canonical skill/trait/rule keys; localization is presentation-only.
- Russian `scope_control` label is `Контроль`; clean-scope bonus is `Бонус за контроль`; scope violation is `Выход за задачу`.
- Use real SQLite for persistence tests, not EF Core InMemory.
- Keep architecture/docs updated in the same change when a contract/rule/storage/privacy decision changes.

---

## Planned repository structure

```text
global.json
Directory.Build.props
Directory.Packages.props
NuGet.Config                    # only if audit/source policy needs repository config
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
    Cards/GetCard/
    Export/
    Initialization/
  HeroPassport.Infrastructure/
    Persistence/
      Entities/
      Configurations/
      Migrations/
      Stores/
      Queries/
    Paths/
    Projects/
    Export/
  HeroPassport.App/
    Cli/
    Mcp/
    Hosting/

tests/
  HeroPassport.Domain.Tests/
  HeroPassport.Application.Tests/
  HeroPassport.Infrastructure.Tests/
  HeroPassport.App.Tests/
  HeroPassport.Architecture.Tests/
  fixtures/
    goldens/
    db/

.github/workflows/ci.yml
```

---

### Task 1: Reproducible .NET 10 foundation (`0.0.1`)

**Files:**
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `.editorconfig`
- Create: `.gitignore`
- Create: `HeroPassport.slnx`
- Create: all five runtime/test `.csproj` skeletons except `HeroPassport.Web`
- Create: `.github/workflows/ci.yml`
- Generate/commit: `packages.lock.json` files after first restore

**Interfaces:**
- Produces the compile-time dependency graph consumed by every later task.
- Produces a single repository-wide package version source.

- [ ] **Step 1: Pin the SDK and test runner**

Create `global.json`:

```json
{
  "sdk": {
    "version": "10.0.302",
    "rollForward": "disable",
    "allowPrerelease": false
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

- [ ] **Step 2: Define repository build policy**

Create `Directory.Build.props` with:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AnalysisLevel>latest</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <Deterministic>true</Deterministic>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
  <PropertyGroup Condition="'$(CI)' == 'true'">
    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
  </PropertyGroup>
</Project>
```

If a specific third-party package emits a build-blocking warning, suppress that warning narrowly in the consuming project with a documented reason; do not disable warnings globally.

- [ ] **Step 3: Define Central Package Management**

Create `Directory.Packages.props` with approved stable versions:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.10" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.10" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.10" />
    <PackageVersion Include="ModelContextProtocol" Version="2.0.0" />
    <PackageVersion Include="SQLitePCLRaw.bundle_e_sqlite3" Version="3.0.5" />
    <PackageVersion Include="System.CommandLine" Version="2.0.10" />
    <PackageVersion Include="xunit.v3" Version="3.2.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>
</Project>
```

Add `Microsoft.NET.Test.Sdk` only if the selected local/IDE compatibility path demonstrably requires it; verify the current stable version at implementation time before pinning because it changes independently from xUnit.

- [ ] **Step 4: Create projects and references**

Project references must be exactly:

```text
Application -> Domain
Infrastructure -> Application, Domain
App -> Application, Infrastructure
Domain.Tests -> Domain
Application.Tests -> Application, Domain
Infrastructure.Tests -> Infrastructure, Application, Domain
App.Tests -> App, Application
Architecture.Tests -> all runtime assemblies only as needed for inspection
```

Do not add Infrastructure -> App or Domain -> anything.

- [ ] **Step 5: Add baseline smoke tests**

Create one passing test in each test project proving the runner discovers it. In `HeroPassport.Architecture.Tests`, add an initial project-reference graph test that will become stricter later.

- [ ] **Step 6: Run clean restore/build/test**

Run:

```bash
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

Expected: all projects compile and five test projects are discovered.

- [ ] **Step 7: Commit lock files, then prove locked restore**

Run:

```bash
dotnet restore --locked-mode
```

Expected: success with no lock-file mutation.

- [ ] **Step 8: Add cross-platform CI**

`ci.yml` matrix: `ubuntu-latest`, `windows-latest`, `macos-latest`; setup .NET from `global.json`; run locked restore, Release build, tests. Add `dotnet format --verify-no-changes --no-restore` after formatting configuration is stable.

- [ ] **Step 9: Commit**

```bash
git add .
git commit -m "build: bootstrap reproducible .NET 10 solution"
```

---

### Task 2: Canonical domain types, contracts, versions and validation (`0.0.2`)

**Files:**
- Create: `src/HeroPassport.Domain/Shared/HeroId.cs`, `ProjectId.cs`, `QuestId.cs`, `XpEventId.cs`
- Create: `src/HeroPassport.Domain/Quests/QuestType.cs`, `QuestResult.cs`, `QuestStatus.cs`, `QuestQualityFlags.cs`
- Create: `src/HeroPassport.Domain/Rewards/RuleVersions.cs`
- Create: `src/HeroPassport.Domain/Skills/SkillKeys.cs`
- Create: `src/HeroPassport.Domain/Traits/TraitKeys.cs`
- Create: `src/HeroPassport.Application/Contracts/Common/*`
- Create: request/response records under each four use-case folder
- Test: `tests/HeroPassport.Domain.Tests/Shared/*`
- Test: `tests/HeroPassport.Application.Tests/Contracts/*`

**Interfaces:**
- Produces `HeroId`, `ProjectId`, `QuestId`, `QuestType`, `QuestResult`, `QuestQualityFlags`, `RuleVersions`.
- Produces transport-neutral `StartQuestRequest/Response`, `FinishQuestRequest/Response`, `CurrentQuestRequest/Response`, `GetHeroCardRequest/Response`.

- [ ] **Step 1: Write ID tests first**

Assert generated IDs use UUIDv7 and parse canonical GUID strings. Suggested API:

```csharp
public readonly record struct QuestId(Guid Value)
{
    public static QuestId New(TimeProvider timeProvider);
    public static bool TryParse(string? text, out QuestId id);
    public override string ToString();
}
```

Implementation may call `Guid.CreateVersion7(timeProvider.GetUtcNow())` through the supported runtime overload/appropriate timestamp conversion; keep generation deterministic under fake time where possible.

- [ ] **Step 2: Define canonical quest/result/status models**

Use enums or constrained value types with explicit serializer mapping; external keys must be exactly:

```text
planning research coding review debugging documentation maintenance
success partial failed blocked abandoned
open completed
```

Tests reject unknown values at the Application boundary.

- [ ] **Step 3: Implement `QuestQualityFlags` and `RuleVersions`**

```csharp
public sealed record QuestQualityFlags(
    bool HasTestsMentioned,
    bool HasCleanScope,
    bool HasClearSummary,
    bool HasNoUserCorrections,
    bool HasBuildPassed,
    bool HasTestsPassed);

public static class RuleVersions
{
    public const string Reward = "1.0.0";
    public const string Level = "1.0.0";
    public const string Skill = "1.0.0";
    public const string TrustRisk = "1.0.0";
    public const string Trait = "1.0.0";
    public const string ProjectIdentity = "1.0.0";
}
```

- [ ] **Step 4: Define bounded common contract types**

Add `OutputMode` (`compact`, `normal`, `verbose`), `Locale` (`ru`, `en`), build/test status values (`passed`, `failed`, `not_run`, `unknown`) and error DTOs.

Centralize bounds:

```csharp
public static class ContractLimits
{
    public const int GoalMaxLength = 500;
    public const int SummaryMaxLength = 2000;
    public const int HostNameMaxLength = 64;
    public const int HostTypeMaxLength = 64;
    public const int IdempotencyKeyMaxLength = 128;
    public const int HeroNameMaxLength = 80;
    public const int ProjectNameMaxLength = 160;
    public const int MetricCounterMax = 100;
    public const int SkillsUsedMax = 3;
}
```

- [ ] **Step 5: Define four request shapes exactly as `MCP-CONTRACT.md`**

Critical assertions:

- `StartQuestRequest` has no workspace path/raw metadata bag.
- `FinishQuestRequest` has no quest type.
- `skillsUsed` max 3 at normalized boundary.
- unknown fields are rejected by JSON schema/SDK path where supported.

- [ ] **Step 6: Add schema/privacy reflection tests**

Fail if public MCP-facing request properties match prohibited names/categories such as `Code`, `Diff`, `RawLog`, `Environment`, `WorkspacePath`, `FileContents`, `Prompt`, `ChatHistory`, `Metadata` generic dictionary.

- [ ] **Step 7: Run focused tests**

```bash
dotnet test tests/HeroPassport.Domain.Tests -c Release
dotnet test tests/HeroPassport.Application.Tests -c Release
```

Expected: parsing/serialization/bounds/privacy tests pass.

- [ ] **Step 8: Commit**

```bash
git add src tests
git commit -m "feat: define Hero Passport domain and tool contracts"
```

---

### Task 3: Reward and level engine with golden compatibility (`0.0.3`)

**Files:**
- Create: `src/HeroPassport.Domain/Rewards/RewardCalculator.cs`
- Create: `RewardBreakdown.cs`, `RewardAdjustment.cs`, `RewardRulesV1.cs`
- Create: `src/HeroPassport.Domain/Heroes/LevelCalculator.cs`, `LevelProgress.cs`
- Create: goldens under `tests/fixtures/goldens/reward/` and `levels/`
- Test: `tests/HeroPassport.Domain.Tests/Rewards/*`, `Heroes/LevelCalculatorTests.cs`

**Interfaces:**

```csharp
public sealed record RewardInput(
    QuestType QuestType,
    QuestResult Result,
    QuestQualityFlags Quality,
    int ScopeViolations,
    int UserCorrections);

public interface IRewardCalculator
{
    RewardBreakdown Calculate(RewardInput input);
}

public static LevelProgress LevelCalculator.FromTotalXp(long totalXp);
```

- [ ] **Step 1: Write the standard 95 XP failing test**

```csharp
[Fact]
public void Clean_successful_coding_quest_awards_95_xp()
{
    var input = Fixtures.CleanSuccessfulCoding;
    var result = RewardCalculatorV1.Instance.Calculate(input);
    Assert.Equal(95, result.FinalXp);
    Assert.Equal(60, result.ResultXp);
}
```

Expected before implementation: compile/fail because calculator does not exist.

- [ ] **Step 2: Implement base map and permille multiplier**

Exact maps from `ENGINE-SPEC.md`; use checked integer arithmetic and `Math.Max(0, ...)` only after adjustments.

- [ ] **Step 3: Add one test per bonus/penalty and combination boundaries**

Test counter values `0`, `1`, `100`; reject values outside contract before domain calculation or throw a domain argument exception if invoked directly.

- [ ] **Step 4: Persist/generate deterministic adjustment order**

Bonus order:

```text
tests_mentioned, clean_scope_bonus, clear_summary, no_user_corrections
```

Penalty order:

```text
scope_violation, unclear_summary, user_correction
```

- [ ] **Step 5: Write level boundary tests**

Assert:

```text
0 -> L1 0/100
99 -> L1 99/100
100 -> L2 0/150
249 -> L2 149/150
250 -> L3 0/200
449 -> L3 199/200
450 -> L4 0/250
```

- [ ] **Step 6: Implement level formulas**

```text
xpToNext(L) = 100 + 50*(L-1)
threshold(L) = (L-1)*(25*L+50)
```

Use checked `long`; derive, do not depend on persisted level.

- [ ] **Step 7: Add culture/determinism replay test**

Run same fixture under `ru-RU`, `en-US`, invariant culture and assert identical numeric/canonical result.

- [ ] **Step 8: Write golden JSON fixtures and snapshot test**

At minimum:

```text
coding-success-clean-95.json
failed-with-violations.json
zero-clamped.json
levels-boundaries.json
```

- [ ] **Step 9: Run tests and commit**

```bash
dotnet test tests/HeroPassport.Domain.Tests -c Release
git add src tests
git commit -m "feat: add deterministic reward and level engine"
```

---

### Task 4: Skills, trust/risk, traits and localized presentation (`0.0.4`)

**Files:**
- Create: `src/HeroPassport.Domain/Skills/SkillKeyNormalizer.cs`, `SkillXpAllocator.cs`
- Create: `src/HeroPassport.Domain/Heroes/TrustRiskCalculator.cs`
- Create: `src/HeroPassport.Domain/Traits/TraitEvaluator.cs`, trait policy definitions
- Create: `src/HeroPassport.Application/Cards/HeroTextFormatter.cs`
- Create: `src/HeroPassport.Application/Contracts/Common/LocalizationCatalog.cs` or resource-backed equivalent
- Test: corresponding Domain/Application test files
- Goldens: `tests/fixtures/goldens/skills/`, `trust-risk/`, `mcp/`

**Interfaces:**

```csharp
public static IReadOnlyList<string> SkillKeyNormalizer.Normalize(IEnumerable<string> raw);
public static IReadOnlyList<SkillXpChange> SkillXpAllocator.Allocate(int xp, IReadOnlyList<string> skills);
public static TrustRiskChange TrustRiskCalculator.Calculate(int trustBefore, int riskBefore, QuestResult result, int scopeViolations, int userCorrections);
public static IReadOnlyList<TraitProgressChange> TraitEvaluator.Evaluate(TraitEvaluationInput input);
```

- [ ] **Step 1: Write alias/duplicate/unknown skill tests**

Required mappings include:

```text
code, implementation -> coding
tests, test -> testing_awareness
scope, control -> scope_control
docs, doc -> documentation
tools, tool -> tool_use
plan -> planning
reviewing -> review
debug -> debugging
maintain -> maintenance
```

Unknown keys are dropped; duplicates collapse preserving first canonical order; max 3.

- [ ] **Step 2: Write allocation tests before implementation**

For `95` XP and three skills assert exact `[47,29,19]` and sum conservation. Also test totals `0,1,2,3,5`.

Implement cumulative-floor boundaries, never floating point.

- [ ] **Step 3: Implement trust/risk exact v1 rules**

Use the table in `ENGINE-SPEC.md`; clamp after total delta. Golden default clean success: trust `50->51`, risk `20->19`.

- [ ] **Step 4: Implement exactly three v1 traits**

```text
precise_executor threshold 5
test_scout threshold 5
quest_finisher threshold 10
```

State transitions `locked -> active` only. No XP award.

- [ ] **Step 5: Add RU/EN presentation catalog**

At minimum assert RU:

```text
scope_control -> Контроль
clean_scope_bonus -> Бонус за контроль
scope_violation -> Выход за задачу
```

Localization cannot change persisted keys or numeric results.

- [ ] **Step 6: Implement status/card formatter as Application presentation logic**

Golden clean coding status must include:

```text
✨ +95 XP · Nova ур.1 · XP 95/100 · Доверие 51 · Риск 19
```

and skill display `Кодинг +47, Контроль +29, Тесты +19` for the standard fixture.

- [ ] **Step 7: Run tests and commit**

```bash
dotnet test tests/HeroPassport.Domain.Tests -c Release
dotnet test tests/HeroPassport.Application.Tests -c Release
git add src tests
git commit -m "feat: add progression rules and localized hero status"
```

---

### Task 5: Application use-case lifecycle behind ports (`0.0.5`)

**Files:**
- Create: `src/HeroPassport.Application/Abstractions/IHeroStore.cs`, `IProjectStore.cs`, `IQuestStore.cs`, `IUnitOfWork.cs`, `IProjectIdentityResolver.cs`, `IAppDataPaths.cs`
- Create handler folders/files for Start/Finish/Current/Card/Initialization/Export
- Create read models under feature folders
- Test with focused fake stores under `tests/HeroPassport.Application.Tests/Fakes/`

**Interfaces:**

Use capability-specific methods, for example:

```csharp
public interface IQuestStore
{
    Task<QuestSnapshot?> GetAsync(QuestId id, CancellationToken ct);
    Task<QuestSnapshot?> GetOpenAsync(HeroId heroId, ProjectId projectId, CancellationToken ct);
    Task<QuestSnapshot?> GetByIdempotencyKeyAsync(HeroId heroId, ProjectId projectId, string key, CancellationToken ct);
    Task AddAsync(NewQuest quest, CancellationToken ct);
    Task<CompletedQuestOutcome?> GetCompletedOutcomeAsync(QuestId id, CancellationToken ct);
}
```

`IUnitOfWork` should expose a single transaction callback or begin/commit abstraction sufficient for Infrastructure; do not expose EF transactions/types.

- [ ] **Step 1: Implement fake stores and failing start idempotency tests**

Tests:

```text
new start creates one quest
same explicit key returns same quest
fallback automatic repeat returns open quest
conflicting explicit different key while open -> HP132
retry does not increment starts twice
```

- [ ] **Step 2: Implement `StartQuestHandler` minimally**

Resolution order: hero -> project -> idempotency/open check -> create. Use `TimeProvider` and typed IDs. No database-specific behavior.

- [ ] **Step 3: Write finish tests before handler**

Tests:

```text
open success calculates + applies reward
already completed returns stored original outcome with alreadyFinished=true
retry payload differences do not change persisted outcome
unknown quest -> HP130
skills normalized before allocation
all projections represented in a single application state change/transaction request
```

- [ ] **Step 4: Implement `FinishQuestHandler`**

Load immutable quest type from stored quest; never trust a second quest type from caller (there is no such field). Build quality flags from summary/metrics. Invoke versioned calculators. Persist outcome via transaction port.

- [ ] **Step 5: Implement Current/Card queries**

Bound card output to top 3 skills/traits and no unbounded history.

- [ ] **Step 6: Implement initialization contract**

Idempotent initialization creates logical default hero/catalog state through ports; physical migrations remain Infrastructure responsibility.

- [ ] **Step 7: Implement export model builder**

Build versioned logical export DTO with no path/config/log data.

- [ ] **Step 8: Run Application suite and commit**

```bash
dotnet test tests/HeroPassport.Application.Tests -c Release
git add src tests
git commit -m "feat: implement quest application lifecycle"
```

---

### Task 6: EF Core SQLite persistence, migrations and project identity (`0.0.6`)

**Files:**
- Create: `src/HeroPassport.Infrastructure/Persistence/HeroPassportDbContext.cs`
- Create persistence entities/configurations for tables in `DATA-MODEL.md`
- Create first EF migration under `Persistence/Migrations/`
- Create store implementations
- Create `Paths/PlatformAppDataPaths.cs`
- Create `Projects/GitProjectIdentityResolver.cs`
- Create database initializer
- Test: `tests/HeroPassport.Infrastructure.Tests/Persistence/*`, `Projects/*`, `Paths/*`

**Interfaces:** Implements Task 5 ports.

- [ ] **Step 1: Add EF/SQLite dependencies and verify resolved graph**

Infrastructure references centrally pinned:

```text
Microsoft.EntityFrameworkCore 10.0.10
Microsoft.EntityFrameworkCore.Sqlite 10.0.10
SQLitePCLRaw.bundle_e_sqlite3 3.0.5
```

Add `Microsoft.EntityFrameworkCore.Design 10.0.10` as private development dependency in the correct startup/design project arrangement.

Run dependency listing/audit and confirm no prerelease package.

- [ ] **Step 2: Write real SQLite migration smoke test first**

Use unique temp-file DB, run migrations, reopen connection/context, assert all required tables exist and foreign keys work.

- [ ] **Step 3: Implement entities/mappings exactly from `DATA-MODEL.md`**

Include max lengths, required fields, foreign keys, unique `xp_events.quest_id`, project fingerprint uniqueness, trust/risk checks where EF migration supports them cleanly.

- [ ] **Step 4: Add exact active/idempotency indexes**

Use EF fluent indexes when sufficient; otherwise fixed SQLite migration SQL for partial unique indexes. Add sequential and race tests.

- [ ] **Step 5: Configure SQLite connection initialization**

Verify:

```text
PRAGMA foreign_keys = ON
PRAGMA journal_mode = WAL
bounded busy timeout
Cache=Shared not used
```

Add integration assertions, not comments only.

- [ ] **Step 6: Assert native SQLite floor**

Query `SELECT sqlite_version();`; parse semantic version; assert `>= 3.53.4` in integration/release test.

- [ ] **Step 7: Implement app-data paths**

Use platform per-user app-data APIs/home conventions behind `IAppDataPaths`. Tests must override root and never write real user directories.

- [ ] **Step 8: Implement project resolver**

Detect Git root from local process context when possible; fallback current directory. Persist only display name + versioned SHA-256 fingerprint, never full path. Add Windows/Unix separator/case normalization tests appropriate to filesystem semantics.

- [ ] **Step 9: Implement idempotent seed initializer**

Repeated init yields one default `Nova`, one row per canonical skill/trait, no duplicate projections.

- [ ] **Step 10: Implement stores/read projections**

Use short-lived DbContext and `AsNoTracking` for reads. No EF entity escapes Infrastructure.

- [ ] **Step 11: Run storage suite and commit**

```bash
dotnet test tests/HeroPassport.Infrastructure.Tests -c Release
git add src tests
git commit -m "feat: add SQLite persistence and migrations"
```

---

### Task 7: Transactional integrity and concurrency hardening (`0.0.7`)

**Files:**
- Modify: Infrastructure unit-of-work/finish persistence path
- Modify: quest/XP mappings and store queries as required
- Add: integration race/rollback fixtures
- Test: `tests/HeroPassport.Infrastructure.Tests/Persistence/QuestConcurrencyTests.cs`, `FinishTransactionTests.cs`

**Interfaces:** Strengthens existing Application ports; avoid introducing EF concepts upward.

- [ ] **Step 1: Write rollback test with injected failure**

Arrange an open quest, force an exception after creating a reward ledger candidate but before completion, then assert after rollback:

```text
quest still open
no xp_event
hero XP unchanged
skill XP unchanged
trust/risk unchanged
trait progress unchanged
project finished/xp stats unchanged
```

- [ ] **Step 2: Make finish one transaction**

Persist report, report skills, unique XP event, hero/skills/traits/project projections and quest completion under one SQLite transaction.

- [ ] **Step 3: Write concurrent finish race test**

Open two contexts/process-like tasks against same temp-file DB and attempt finish for one quest. Assert exactly one `xp_events` row and one final outcome.

- [ ] **Step 4: Convert race loser into idempotent success**

When known uniqueness/state conflict indicates another writer finished first, reload completed outcome and return it; do not broad-catch every database exception.

- [ ] **Step 5: Write/start concurrent idempotency race**

Equivalent starts yield one active quest and one starts projection increment.

- [ ] **Step 6: Stress repeat**

Run race scenario in a bounded loop (for example 100 iterations in a dedicated integration fact/collection) without flaky timing assumptions. If environment makes high iteration too slow for every CI run, keep a smaller PR count and run 100 in release qualification.

- [ ] **Step 7: Commit**

```bash
dotnet test tests/HeroPassport.Infrastructure.Tests -c Release
git add src tests
git commit -m "fix: guarantee retry-safe quest progression"
```

---

### Task 8: CLI initialization, diagnostics and export (`0.0.8`)

**Files:**
- Create: `src/HeroPassport.App/Program.cs`
- Create: `Hosting/ServiceRegistration.cs`
- Create commands under `Cli/Commands/`
- Create: `Infrastructure/Export/JsonExportWriter.cs`
- Test: `tests/HeroPassport.App.Tests/Cli/*`, `Infrastructure.Tests/Export/*`

**Interfaces:**

Required CLI:

```text
hero-passport init
hero-passport status
hero-passport doctor
hero-passport export --format json
hero-passport data path
hero-passport --version
```

- [ ] **Step 1: Build command tree with System.CommandLine**

Handlers call Application services; command classes do not query DbContext directly.

- [ ] **Step 2: Write `init` process/handler tests**

First run migrates/seeds; second run exits success without duplicates. Normal CLI stdout may show data location/status.

- [ ] **Step 3: Implement `doctor` structured checks**

Checks:

```text
app version
runtime version
data directory access
database open/current migration
native SQLite version
WAL mode
foreign_keys setting
default hero existence
```

Return nonzero exit if a required correctness condition fails; warnings (for example optional permission concerns) are distinguishable from fatal errors.

- [ ] **Step 4: Implement logical JSON export**

Use version manifest from spec, atomic temp-file + move/replace. No raw DB copy as default backup.

- [ ] **Step 5: Write privacy sentinel export/log tests**

Assert local full paths/environment sentinels are absent. Allowed goal/summary text remains user product data and is exported intentionally.

- [ ] **Step 6: Ensure CLI `--help` has no DB side effects**

No migration/database open required to render help/version.

- [ ] **Step 7: Commit**

```bash
dotnet test tests/HeroPassport.App.Tests -c Release
dotnet test tests/HeroPassport.Infrastructure.Tests -c Release
git add src tests
git commit -m "feat: add Hero Passport CLI operations"
```

---

### Task 9: Official MCP C# SDK stdio host and four tools (`0.0.9`)

**Files:**
- Add App dependency: `ModelContextProtocol`
- Create: `src/HeroPassport.App/Mcp/McpHost.cs`
- Create: `Mcp/HeroTools.cs` or four explicit tool adapter classes
- Create: `Mcp/McpResultMapper.cs`
- Create: `Mcp/McpServerInstructions.cs`
- Test: `tests/HeroPassport.App.Tests/Mcp/*`

**Interfaces:** Thin adapters call Task 5 Application handlers.

- [ ] **Step 1: Write deterministic tool catalog test before implementation**

Using official SDK client/server APIs where feasible, list tools and assert exact order/names:

```text
hero.start_quest
hero.finish_quest
hero.current_quest
hero.get_card
```

- [ ] **Step 2: Configure stable official MCP stdio server**

Use `ModelContextProtocol 2.0.0` hosting/DI APIs from current official docs. Do not add AspNetCore/Tasks/Apps packages.

- [ ] **Step 3: Register concise server instructions**

First 512 chars must contain lifecycle + privacy + displayText guidance from `MCP-CONTRACT.md`.

- [ ] **Step 4: Implement four tool schemas/adapters**

Use bounded request records and descriptions from canonical contract. Mark read/mutation/idempotent hints where current SDK supports annotations. Treat annotations as metadata, not security.

- [ ] **Step 5: Implement MCP tool error mapping**

Map known `HPxxx` application failures to a structured tool error with safe `displayText` and `isError`; do not turn valid tool failures into arbitrary JSON-RPC protocol exceptions.

- [ ] **Step 6: Write official-client call tests**

Call each tool against isolated temp data. Assert standard finish response includes 95 XP and no raw JSON recommendation beyond structured result fields.

- [ ] **Step 7: Write child-process stdout guard**

Launch built `hero-passport mcp`; perform protocol exchange; assert no BOM/banner/log/decorative text on stdout. Enable diagnostics and assert they appear only on stderr.

- [ ] **Step 8: Add negative stdout regression fixture**

Where test architecture permits, inject a startup logger/sentinel and prove the guard would fail if it were routed to stdout. This demonstrates the test is not vacuous.

- [ ] **Step 9: Commit**

```bash
dotnet test tests/HeroPassport.App.Tests -c Release
git add src tests
git commit -m "feat: expose Hero Passport over MCP stdio"
```

---

### Task 10: Codex integration and installed-tool E2E (`0.0.10`)

**Files:**
- Modify/validate: `docs/integrations/CODEX.md`
- Add: `tests/HeroPassport.App.Tests/EndToEnd/InstalledToolSmokeTests.cs` or external release script if Codex cannot be safely nested in normal test execution
- Add: `scripts/verify-mcp-smoke.ps1` and `scripts/verify-mcp-smoke.sh` if a portable scripted MCP client fixture is required
- Add/update consumer snippet docs

**Interfaces:** External user flow, no new product API unless a gap is demonstrated.

- [ ] **Step 1: Pack/install Hero Passport into an isolated tool path**

Use `dotnet pack` + `dotnet tool install --tool-path <temp> --add-source <local-package-source> ...`. Do not test only `dotnet run`.

- [ ] **Step 2: Validate native Codex registration syntax against current official docs**

Expected baseline:

```bash
codex mcp add hero-passport -- hero-passport mcp
codex mcp list
```

If official syntax changed after this plan date, update `REFERENCES.md`, `CODEX.md`, ADR and acceptance test before implementation; do not retain stale examples.

- [ ] **Step 3: Validate current-workspace project resolution**

Launch Codex CLI from a test/sample Git repo where manual/controlled E2E is feasible. Confirm project display/fingerprint separates it from a second repo.

- [ ] **Step 4: Validate explicit local `cwd` troubleshooting path**

Where config-based test isolation is feasible, set `mcp_servers.hero-passport.cwd` and verify project resolution without transmitting path in Hero Passport tool args/results.

- [ ] **Step 5: Run two-call lifecycle manually/automated**

Verify:

```text
start once
no per-step Hero Passport calls
finish once
displayText only in final UI guidance
repeat finish = no extra XP
restart server = state retained
get_card totals correct
```

- [ ] **Step 6: Document client-support claims narrowly**

Claim Codex CLI only after validated. Add desktop/IDE support only after explicit host tests; do not infer from shared config alone.

- [ ] **Step 7: Commit**

```bash
git add docs tests scripts
git commit -m "test: validate Codex Hero Passport workflow"
```

---

### Task 11: Architecture/privacy fitness functions and dependency audit

**Files:**
- Expand: `tests/HeroPassport.Architecture.Tests/*`
- Add CI audit/format steps
- Add optional policy script under `scripts/verify-dependencies.*`

**Interfaces:** build gates only.

- [ ] **Step 1: Enforce project/namespace dependency rules**

Fail if Domain references assemblies/namespaces from EF Core, ModelContextProtocol, System.CommandLine, ASP.NET Core, Infrastructure or App.

Fail if Application public contracts expose Infrastructure/EF types.

- [ ] **Step 2: Enforce prohibited contract field categories**

Reflection/schema scan fails on source/diff/raw-log/environment/path/prompt/chat-history generic ingestion properties.

- [ ] **Step 3: Enforce MCP surface count/order**

Architecture/App test asserts exactly four MVP tools.

- [ ] **Step 4: Add package vulnerability/audit gate**

Use current .NET/NuGet supported audit command/restore audit behavior and fail CI/release on the repository's agreed severity (at least high/critical for MVP). Verify no known vulnerability in resolved SQLite/native graph.

- [ ] **Step 5: Add no-prerelease dependency check**

Parse resolved dependency output/lock files and fail if package version contains prerelease identifiers without an allowlisted ADR-backed exception.

- [ ] **Step 6: Run full suite and commit**

```bash
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
dotnet format --verify-no-changes --no-restore
git add tests .github scripts
git commit -m "test: enforce Hero Passport architecture and privacy"
```

---

### Task 12: Release packaging and `0.1.0-rc.1` qualification

**Files:**
- Modify App `.csproj` with tool package metadata
- Create: `CHANGELOG.md`
- Create: `docs/RELEASE-CHECKLIST.md`
- Create/update install/troubleshooting in README/docs
- Add release smoke scripts/workflow only as needed

**Interfaces:** ships the existing `hero-passport` command; no new gameplay scope.

- [ ] **Step 1: Configure .NET tool package**

Set stable command/tool metadata, package ID, version source and repository/license metadata. Ensure all runtime files/native SQLite assets required by the tool are included.

- [ ] **Step 2: Install package on all three supported OSes in CI/manual release matrix**

Validate `--version`, `init`, `doctor`, MCP smoke, start/finish/card/export.

- [ ] **Step 3: Run all 15 PRODUCT-SPEC MVP success criteria**

Record pass/fail in release checklist. Do not waive a P0 criterion to tag RC.

- [ ] **Step 4: Run concurrency/retry stress and privacy sentinels**

At release qualification count, not only minimal PR smoke.

- [ ] **Step 5: Validate package dependency graph/native SQLite floor after installation**

Do not assume build-time resolution equals packaged runtime behavior.

- [ ] **Step 6: Validate fresh + previous-schema migration path**

For the first RC, fresh DB only plus any internal pre-release fixture. From `0.1.0` onward, keep released DB fixtures and migrate forward.

- [ ] **Step 7: Commit RC metadata/docs**

```bash
git add src docs CHANGELOG.md .github scripts
git commit -m "chore: prepare Hero Passport 0.1.0-rc.1"
```

---

### Task 13: Release minimal MVP `0.1.0`

**Files:**
- Update: version source, `CHANGELOG.md`, README status, release notes
- No domain/protocol feature files should change unless fixing an RC blocker with tests.

**Interfaces:** frozen 1.0 MCP schema family and rule versions `1.0.0` for the initial release.

- [ ] **Step 1: Re-run clean release qualification from a clean checkout**

```bash
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
```

plus multi-OS package install/MCP E2E/audit/migration/export checks from Task 12.

- [ ] **Step 2: Confirm documentation and implementation agreement**

Compare actual tool schemas/formulas/table constraints/package versions/CLI commands against:

```text
PRODUCT-SPEC.md
ARCHITECTURE.md
MCP-CONTRACT.md
ENGINE-SPEC.md
DATA-MODEL.md
SECURITY-PRIVACY.md
TESTING-QUALITY.md
CODEX.md
DECISION-LOG.md
REFERENCES.md
```

Resolve any mismatch before release; do not label docs as aspirational if code differs.

- [ ] **Step 3: Freeze release notes and tag**

Commit version/docs, create signed/normal project tag per repository policy, publish .NET tool/release artifacts, then perform one post-publish installation smoke from the actual published artifact.

- [ ] **Step 4: Open the next milestone only after release**

Dashboard planning (`0.2.0`) begins as a separate design/spec/plan cycle. Do not add Web project in this MVP release commit merely as an empty future shell.

---

## Cross-task golden compatibility matrix

These values must remain locked through `0.1.0` unless their rule/schema version changes deliberately:

```text
schemaVersion                     1.0
rewardRuleVersion                 1.0.0
levelRuleVersion                  1.0.0
skillRuleVersion                  1.0.0
trustRiskRuleVersion              1.0.0
traitRuleVersion                  1.0.0
projectIdentityVersion            1.0.0

clean successful coding XP       95
3-skill allocation for 95        47 / 29 / 19
initial trust/risk                50 / 20
clean successful trust/risk      51 / 19
MVP tool count                    4
MVP transport                     stdio
MVP DB journal mode               WAL
native SQLite minimum             3.53.4
```

## Self-review against specification

### Coverage

- Product loop/scope: Tasks 5, 8, 9, 10, 12, 13.
- Deterministic RPG rules: Tasks 2–4.
- Persistence/privacy/integrity: Tasks 6–7, 11.
- Codex/MCP/token discipline: Tasks 9–10.
- Reproducible toolchain/dependencies: Tasks 1, 11–12.
- Cross-platform quality: Tasks 1, 8, 12–13.
- Dashboard: explicitly excluded and starts in a new post-MVP plan.

### Ambiguity resolutions

- No standalone Contracts assembly in MVP.
- No custom Codex config writer.
- No workspace path in MCP payload/DB.
- No `questType` in finish request.
- No skill-level formula in MVP; cumulative skill XP only.
- Exactly three initial trait policies.
- Logical JSON export before raw DB backup.
- SQLitePCLRaw `3.0.5`, not report-era `2.1.12`.

### Required implementation discipline

Each task is reviewable independently and must keep the repository green. An implementer should not jump ahead by introducing post-MVP infrastructure. When a current official dependency/tooling document has changed since 2026-08-10, update the reference/ADR/spec first, then implement against the newly approved baseline.
