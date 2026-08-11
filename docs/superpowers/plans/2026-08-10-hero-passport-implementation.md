# Hero Passport v3.1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver Hero Passport `0.1.0`, a portable local-first stdio MCP server/CLI with HP-MCP/2, deterministic RPG progression, project-identity/1, race/crash-safe SQLite persistence, exact wire contracts and Codex reference qualification.

**Architecture:** Modular monolith `Domain -> Application -> Infrastructure -> App`. Application owns transport-neutral semantics; MCP/CLI are adapters. Project/hero binding is local process/application context, not model input. Read-modify-write SQLite operations acquire immediate writer intent before invariant reads. HP-MCP success returns canonical structured content plus equivalent JSON TextContent.

**Tech Stack:** C# 14, .NET SDK 10.0.302 / `net10.0`, official `ModelContextProtocol 2.0.0`, EF Core SQLite 10.0.10, `SQLitePCLRaw.bundle_e_sqlite3 3.0.5`, System.CommandLine 2.0.10, xUnit.net v3 3.2.2.

## Global Constraints

- Normative precedence is `docs/README.md`; read the relevant deep dive before coding.
- Exact project identity: `docs/PROJECT-IDENTITY.md`.
- Exact DB transaction/crash/backup behavior: `docs/PERSISTENCE-RELIABILITY.md`.
- Exact HP-MCP fields/validation/results: `docs/WIRE-CONTRACT.md`.
- SDK pinned to 10.0.302; stable packages only; Central Package Management + lock files.
- `McpServerOptions.ProtocolVersion` remains unset/null.
- 0.1 runtime transport is stdio only; no `ModelContextProtocol.AspNetCore`.
- Exact tool order: `hero.start_quest`, `hero.finish_quest`, `hero.list_active_quests`, `hero.get_card`.
- `start_quest` MCP idempotent hint is false; finish/list/card are true.
- MCP success: structured object + one semantically equal minified JSON TextContent.
- MCP business/validation error: `isError=true`, one safe TextContent, no structuredContent.
- Tool arguments receive explicit runtime validation; generated schema/DataAnnotations are not validation.
- Model `goal`/`summary` use SafeTextV1; scalar-aware bounds.
- Active retry identity uses `QuestDedupKeyV1`, case preserved; `LogicalQuestKeyV1` must not be introduced.
- Up to 16 active quests per hero/project; count=15 race must end exactly at 16.
- All DB read-modify-write use a non-deferred Serializable transaction before invariant reads; selected provider path must prove immediate writer semantics.
- Writable DB supported profile is same-host local filesystem, WAL/FULL/FKs, default timeout 5s.
- Actual loaded SQLite must qualify `>=3.51.3` for supported WAL runtime.
- Never raw-`File.Copy` a live DB; never delete WAL/SHM for recovery.
- Git project identity uses canonical `git-common-dir`; linked worktrees share identity; no remote URL/path persistence.
- No source/file/diff/raw-log/prompt/secret/environment/workspace-path model data.
- Domain/Application do not render localization; App presentation owns `displayText`.
- RPG rules remain `reward/1.0.0`, `trust-risk/1.0.0`, `traits/1.0.0`; clean coding golden = 95 XP.

---

## Planned Repository Structure

```text
HeroPassport.slnx
global.json
Directory.Build.props
Directory.Packages.props
.editorconfig

src/
  HeroPassport.Domain/
    Heroes/
    Projects/
    Quests/
    Rewards/
    Skills/
    Traits/

  HeroPassport.Application/
    Abstractions/
    Context/
    Contracts/
    Validation/
    Quests/
      Dedup/
      StartQuest/
      FinishQuest/
      ListActiveQuests/
    Cards/GetHeroCard/
    Initialization/
    Diagnostics/
    Export/

  HeroPassport.Infrastructure/
    Persistence/
      HeroPassportDbContext.cs
      Entities/
      Configurations/
      Migrations/
      Stores/
      Queries/
      SqliteWriteUnitOfWork.cs
      SqliteBackupService.cs
    Paths/
    Projects/
      GitRepositoryProbe.cs
      ProjectBindingResolver.cs
      ProjectIdentityV1.cs
      ProjectIdentitySaltStore.cs
    Heroes/
    Configuration/
    Diagnostics/

  HeroPassport.App/
    Program.cs
    Hosting/
    Cli/
    Mcp/
      HeroPassportMcpManifest.cs
      HeroPassportServerInstructions.cs
      McpOperationContextResolver.cs
      Validation/
      Results/
      Tools/
    Presentation/

contracts/mcp/hp-mcp-2/

tests/
  HeroPassport.Domain.Tests/
  HeroPassport.Application.Tests/
  HeroPassport.Infrastructure.Tests/
  HeroPassport.App.Tests/
  HeroPassport.Architecture.Tests/
  HeroPassport.Contract.Tests/
  HeroPassport.AgentEvals/
```

---

### Task 1: Reproducible .NET foundation (`0.0.1`)

**Files:**
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `.editorconfig`
- Create: `.gitignore`
- Create: `HeroPassport.slnx`
- Create: four source `.csproj` files
- Create: seven test/eval `.csproj` files
- Test: `tests/HeroPassport.Architecture.Tests/ProjectReferenceTests.cs`

**Interfaces:**
- Produces the fixed project/reference/package graph used by every later task.

- [ ] **Step 1: Pin SDK.** Create `global.json` with exact SDK `10.0.302`, `rollForward: disable`, `allowPrerelease: false`.
- [ ] **Step 2: Add centralized build properties.** Target `net10.0`, C# 14, nullable/implicit usings, deterministic build, warnings policy.
- [ ] **Step 3: Add Central Package Management.** Pin exactly the approved stable packages; do not include MCP ASP.NET package.
- [ ] **Step 4: Create project graph.** Domain no product refs; Application -> Domain; Infrastructure -> Application+Domain; App -> Application+Infrastructure.
- [ ] **Step 5: Create failing architecture test** that detects a forbidden reverse project reference.
- [ ] **Step 6: Configure package lock files** and restore once with lock generation.
- [ ] **Step 7: Verify foundation.** Run:

```bash
dotnet --version
dotnet restore --use-lock-file
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

Expected SDK: `10.0.302`; build/tests pass for scaffold.

- [ ] **Step 8: Commit.** `build: establish reproducible net10 foundation`

---

### Task 2: Domain vocabulary and deterministic IDs (`0.0.2`)

**Files:**
- Create: `src/HeroPassport.Domain/Heroes/HeroId.cs`
- Create: `src/HeroPassport.Domain/Projects/ProjectId.cs`
- Create: `src/HeroPassport.Domain/Quests/QuestId.cs`
- Create: `QuestType.cs`, `QuestResult.cs`, `QuestStatus.cs`
- Create: `src/HeroPassport.Domain/Shared/JsonSafeInteger.cs`
- Test: `tests/HeroPassport.Domain.Tests/Shared/IdAndRangeTests.cs`

**Interfaces:**

```csharp
public readonly record struct HeroId(Guid Value);
public readonly record struct ProjectId(Guid Value);
public readonly record struct QuestId(Guid Value);

public enum QuestType { Planning, Research, Coding, Review, Debugging, Documentation, Maintenance }
public enum QuestResult { Success, Partial, Failed, Blocked, Abandoned }
public enum QuestStatus { Open, Finished }

public static class JsonSafeInteger
{
    public const long Max = 9_007_199_254_740_991L;
}
```

- [ ] **Step 1: Write failing typed-ID tests** proving HeroId/ProjectId/QuestId cannot be interchanged by API type.
- [ ] **Step 2: Write UUIDv7 generation/round-trip test** around the chosen ID factory using `Guid.CreateVersion7()`.
- [ ] **Step 3: Add enum key tests** for the exact canonical values needed by later adapters.
- [ ] **Step 4: Add JSON-safe integer bound tests** for Max, Max+1 rejection and checked overflow behavior.
- [ ] **Step 5: Implement the minimum types** without JSON/MCP/EF attributes in Domain.
- [ ] **Step 6: Run Domain tests.**
- [ ] **Step 7: Commit.** `feat: add typed domain vocabulary`

---

### Task 3: SafeTextV1, QuestDedupKeyV1, operation context and error contract (`0.0.3`)

**Files:**
- Create: `src/HeroPassport.Application/Validation/SafeTextV1.cs`
- Create: `src/HeroPassport.Application/Quests/Dedup/QuestDedupKey.cs`
- Create: `QuestDedupKeyV1.cs`
- Create: `src/HeroPassport.Application/Context/HeroOperationContext.cs`
- Create: `InvocationOrigin.cs`, `InvocationSurface.cs`
- Create: `src/HeroPassport.Application/Contracts/HeroError.cs`, `HeroResult.cs`
- Test: `tests/HeroPassport.Application.Tests/Validation/SafeTextV1Tests.cs`
- Test: `tests/HeroPassport.Application.Tests/Quests/QuestDedupKeyV1Tests.cs`

**Interfaces:**

```csharp
public readonly record struct SafeText(string Value, int ScalarLength);

public static class SafeTextV1
{
    public static HeroResult<SafeText> NormalizeGoal(string input);
    public static HeroResult<SafeText> NormalizeSummary(string input);
}

public readonly record struct QuestDedupKey(int Version, ImmutableArray<byte> Hash);

public static class QuestDedupKeyV1
{
    public const int Version = 1;
    public static QuestDedupKey Create(QuestType type, SafeText goal);
}

public sealed record HeroOperationContext(HeroId HeroId, ProjectId ProjectId, InvocationOrigin Origin);
```

- [ ] **Step 1: Write SafeText failing vectors** from `WIRE-CONTRACT.md`: NFC, emoji scalar count, whitespace collapse, controls/bidi rejection, 500/501 and 2000/2001 boundaries.
- [ ] **Step 2: Verify `.Length` cannot satisfy emoji boundary tests.** Expected failing evidence before Rune-aware implementation.
- [ ] **Step 3: Implement SafeTextV1** using `Rune`/valid scalar enumeration, NFC, trimming and whitespace collapse; never persist raw unnormalized model text.
- [ ] **Step 4: Write dedup goldens** proving whitespace/NFC equivalence but **case difference and punctuation difference produce different keys**.
- [ ] **Step 5: Implement QuestDedupKeyV1** as SHA-256 of canonical quest key + newline + case-preserved SafeText goal.
- [ ] **Step 6: Add stale-name test** rejecting production type name `LogicalQuestKeyV1`.
- [ ] **Step 7: Implement operation context and typed error model** with Code/Category/Retryability/MessageKey/SafeDetails.
- [ ] **Step 8: Assert InvocationOrigin does not enter Domain reward APIs.**
- [ ] **Step 9: Run Application + Domain tests.**
- [ ] **Step 10: Commit.** `feat: add safe text dedup and operation contracts`

---

### Task 4: Deterministic reward and level engine (`0.0.4`)

**Files:**
- Create: `src/HeroPassport.Domain/Rewards/RewardRulesV1.cs`
- Create: `RewardCalculator.cs`, `RewardBreakdown.cs`, `QuestQualityFlags.cs`
- Create: `src/HeroPassport.Domain/Heroes/LevelCurveV1.cs`
- Test: `tests/HeroPassport.Domain.Tests/Rewards/RewardCalculatorTests.cs`
- Test: `tests/HeroPassport.Domain.Tests/Heroes/LevelCurveV1Tests.cs`

**Interfaces:**

```csharp
public sealed record QuestQualityFlags(
    bool HasTestsMentioned,
    bool HasCleanScope,
    bool HasClearSummary,
    bool HasNoUserCorrections,
    bool HasBuildPassed,
    bool HasTestsPassed);
```

- [ ] **Step 1: Write failing clean-coding golden.** Expected exactly 95 XP.
- [ ] **Step 2: Write every quest/result multiplier and penalty boundary test** from `ENGINE-SPEC.md`.
- [ ] **Step 3: Use normalized SafeText scalar length to derive clear-summary flag** at Application boundary; Domain receives the flag.
- [ ] **Step 4: Implement integer permille reward arithmetic only.**
- [ ] **Step 5: Implement level threshold/progress with checked arithmetic and JSON-safe ceiling.**
- [ ] **Step 6: Run Domain tests.**
- [ ] **Step 7: Commit.** `feat: add deterministic reward and levels`

---

### Task 5: Skills, Trust/Risk and traits (`0.0.5`)

**Files:**
- Create: Domain skill keys/allocation/TrustRisk/Trait rule types
- Test: focused Domain tests for each rule family

**Interfaces:**

```csharp
public static class SkillXpAllocator
{
    public static IReadOnlyList<SkillXpDelta> Allocate(long xp, IReadOnlyList<SkillKey> orderedSkills);
}
```

- [ ] **Step 1: Write canonical-skill and CLI-alias normalization tests.**
- [ ] **Step 2: Write 1/2/3-skill allocation conservation tests** including 95 -> 47/29/19 for three ordered skills.
- [ ] **Step 3: Implement cumulative-floor allocation** preserving input semantic order.
- [ ] **Step 4: Write/implement Trust/Risk transition/clamp tests.**
- [ ] **Step 5: Write/implement initial trait unlock monotonicity tests.**
- [ ] **Step 6: Verify no localized labels are Domain persistence keys.**
- [ ] **Step 7: Commit.** `feat: add skills trust risk and traits`

---

### Task 6: Transport-neutral quest lifecycle (`0.0.6`)

**Files:**
- Create narrow Application store/read ports under `Application/Abstractions/`
- Create `Application/Quests/StartQuest/*`
- Create `FinishQuest/*`
- Create `ListActiveQuests/*`
- Create `Cards/GetHeroCard/*`
- Test: Application handler tests using small fakes, not EF InMemory

**Interfaces:**

```csharp
public sealed record StartQuestCommand(HeroOperationContext Context, QuestType QuestType, SafeText Goal);
public sealed record StartQuestResult(QuestId QuestId, bool AlreadyOpen, HeroCardReadModel Hero);

public sealed record FinishQuestCommand(
    HeroOperationContext Context,
    QuestId QuestId,
    QuestResult Result,
    SafeText Summary,
    QuestMetrics Metrics,
    IReadOnlyList<SkillKey> SkillsUsed);
```

- [ ] **Step 1: Write Start tests:** new declaration, matching open retry, case-different distinct, different type distinct, 16 -> HP133.
- [ ] **Step 2: Add same-args-after-finish test.** Expected a **new** quest; this protects start's non-idempotent lifecycle semantics.
- [ ] **Step 3: Write Finish tests:** HP130, HP134 hero/project, already-finished original result, all result categories.
- [ ] **Step 4: Write metrics cross-field tests:** non-`not_run` testsStatus requires testsMentioned=true.
- [ ] **Step 5: Make MCP canonical skill semantics explicit in handler input; keep aliases outside MCP adapter path.**
- [ ] **Step 6: Write list/card ordering/filter/projection tests.**
- [ ] **Step 7: Implement narrow ports/handlers; no `IRepository<T>` or MediatR.**
- [ ] **Step 8: Run Application tests.**
- [ ] **Step 9: Commit.** `feat: add transport neutral quest lifecycle`

---

### Task 7: Platform paths, config and project/hero binding (`0.0.7`)

**Files:**
- Create: `Infrastructure/Paths/AppDataPaths.cs`
- Create: config options/loader/validator
- Create: `Infrastructure/Projects/GitRepositoryProbe.cs`
- Create: `ProjectBindingResolver.cs`, `ProjectIdentityV1.cs`, `ProjectIdentitySaltStore.cs`
- Create: `Infrastructure/Heroes/HeroBindingResolver.cs`
- Test: `Infrastructure.Tests/Projects/ProjectIdentityV1Tests.cs`

**Interfaces:**

```csharp
public interface IProjectBindingResolver
{
    ProjectBinding Resolve(string? explicitProjectRoot, string processWorkingDirectory);
}

internal sealed record GitProjectAnchor(string CommonDirectory, string WorktreeTopLevel, string Scope);
```

- [ ] **Step 1: Write platform data-path + `HERO_PASSPORT_HOME` tests.**
- [ ] **Step 2: Write normal Git identity tests** root/nested cwd/spaces/Unicode/dash-leading path.
- [ ] **Step 3: Create real linked worktree test.** Assert main+linked fingerprint equal because both use absolute `git-common-dir`.
- [ ] **Step 4: Create real monorepo scope tests.** Nested cwd no explicit root => whole repo; explicit services/a and services/b => distinct scoped identities.
- [ ] **Step 5: Create submodule/nested-repo tests** proving separate identities.
- [ ] **Step 6: Create Git safety/failure tests** HP311/312/313 and assert no `safe.directory` mutation.
- [ ] **Step 7: Implement Git probe with `ProcessStartInfo.ArgumentList`, `git -C`, sanitized Git location env variables, no shell/remotes/hooks.**
- [ ] **Step 8: Implement installation 32-byte random project identity salt + salted SHA-256 fingerprint.**
- [ ] **Step 9: Assert persisted project record excludes path/remote URL.**
- [ ] **Step 10: Implement strict config v1 and hero selector ambiguity handling.**
- [ ] **Step 11: Run Infrastructure project/config tests.**
- [ ] **Step 12: Commit.** `feat: add project identity and local binding`

---

### Task 8: EF Core SQLite schema and migration 0001 (`0.0.8`)

**Files:**
- Create: `Infrastructure/Persistence/HeroPassportDbContext.cs`
- Create EF entities/configurations
- Create migration `0001_*` + model snapshot
- Test: schema/migration integration tests

**Schema contract:**

```text
quest_sessions.dedup_key
quest_sessions.dedup_key_version
partial UNIQUE open dedup key
UNIQUE quest_reports.quest_id
UNIQUE xp_events.quest_id
projects.workspace_fingerprint UNIQUE
no path/remote URL column
```

- [ ] **Step 1: Write failing schema assertions** against a fresh temp file DB.
- [ ] **Step 2: Map bounded fields/FKs/check constraints** including JSON-safe maxima where practical.
- [ ] **Step 3: Generate migration 0001** and add SQLite partial unique index SQL if EF mapping requires it.
- [ ] **Step 4: Assert stale `logical_key` columns are absent.**
- [ ] **Step 5: Configure WAL/FULL/FKs through tested initialization path.**
- [ ] **Step 6: Assert actual `sqlite_version()` is queryable and release floor logic exists.**
- [ ] **Step 7: Test empty -> latest and foreign-key/index state.**
- [ ] **Step 8: Commit.** `feat: add sqlite schema and migration`

---

### Task 9: Writer transactions, races, crash recovery and backup (`0.0.9`)

**Files:**
- Create: `Infrastructure/Persistence/SqliteWriteUnitOfWork.cs`
- Create persistence stores/queries
- Create: `SqliteBackupService.cs`
- Create: storage exception translator
- Test: real SQLite concurrency/crash/backup suite

**Interface:**

```csharp
public interface IWriteUnitOfWork
{
    HeroResult<T> Execute<T>(Func<HeroPassportDbContext, T> operation);
}
```

Implementation must begin `Database.BeginTransaction(IsolationLevel.Serializable)` before invariant reads and prove selected provider immediate-writer behavior.

- [ ] **Step 1: Write provider-locking failing test** with two connections proving writer intent is acquired before invariant read sequence.
- [ ] **Step 2: Implement short non-deferred Serializable writer unit of work.** No raw independent `BEGIN` behind EF.
- [ ] **Step 3: Write same-dedup concurrent Start test** -> one row/same questId.
- [ ] **Step 4: Write count=15 two-distinct-writer test** -> final count exactly 16, other HP133.
- [ ] **Step 5: Write concurrent Finish test** -> one report, one xp_event, same result.
- [ ] **Step 6: Write busy timeout test** -> HP202 after provider bound, no Polly stack.
- [ ] **Step 7: Add child-process fault points** before commit and after commit-before-response.
- [ ] **Step 8: Prove crash before commit leaves no partial progression.**
- [ ] **Step 9: Prove crash after commit-before-response returns original result on retry.**
- [ ] **Step 10: Prove WAL recovery works without deleting/renaming WAL/SHM.**
- [ ] **Step 11: Add error mapping tests** HP203/204/205/206/207/208/211.
- [ ] **Step 12: Implement `BackupDatabase` backup + independent quick/FK/schema verification test; static/test gate rejects live DB `File.Copy`.**
- [ ] **Step 13: Test normal SQLite runtime floor >=3.51.3 and doctor failure path for a simulated unqualified version.**
- [ ] **Step 14: Commit.** `feat: harden sqlite concurrency crash and backup`

---

### Task 10: Presentation, CLI and doctor (`0.0.10`)

**Files:**
- Create: `App/Presentation/HeroTextRenderer.cs` + localization maps
- Create CLI root/commands
- Create diagnostics read model/doctor services
- Test: App process/renderer/doctor tests

- [ ] **Step 1: Write RU/EN presentation goldens** including `Контроль`, `Бонус за контроль`, `Выход за задачу`.
- [ ] **Step 2: Assert displayText never echoes goal/summary by default and remains within wire bounds.**
- [ ] **Step 3: Build CLI parser tree** for init/mcp/doctor/card/quest list/export/data path/version.
- [ ] **Step 4: Implement doctor checks** paths, Git binding diagnostics, DB open/version/PRAGMAs/migrations/quick_check/FK check/storage location.
- [ ] **Step 5: Ensure doctor has no destructive auto-repair path.**
- [ ] **Step 6: Process-test stdout/stderr and isolated `HERO_PASSPORT_HOME`.**
- [ ] **Step 7: Commit.** `feat: add cli presentation and diagnostics`

---

### Task 11: Exact HP-MCP/2 stdio contract (`0.0.11`)

**Files:**
- Create: `App/Mcp/HeroPassportMcpManifest.cs`
- Create: server instructions
- Create: `App/Mcp/Validation/*`
- Create: `App/Mcp/Results/McpToolResultFactory.cs`
- Create four tool classes
- Test: `HeroPassport.Contract.Tests/*`

**Core result helper:**

```csharp
internal static CallToolResult Success<T>(T dto)
{
    JsonElement structured = JsonSerializer.SerializeToElement(dto, JsonOptions);
    string json = JsonSerializer.Serialize(dto, JsonOptions);
    return new CallToolResult
    {
        StructuredContent = structured,
        Content = [new TextContentBlock { Text = json }],
        IsError = false
    };
}
```

Use the actual SDK 2.0 type names/properties verified at implementation time; preserve semantics even if exact constructors differ.

- [ ] **Step 1: Write failing manifest test** for exact four names/order and no fifth tool.
- [ ] **Step 2: Write annotation test** start idempotent=false; finish/list/card true; readOnly/destructive/openWorld exact.
- [ ] **Step 3: Build explicit runtime validators** for SafeText, canonical UUIDv7, enums, metrics, canonical-only ordered skills.
- [ ] **Step 4: Write test proving schema annotations alone are not relied on:** invoke adapter with invalid object and expect HP100/typed tool error.
- [ ] **Step 5: Implement output DTOs/schemas** exactly from `WIRE-CONTRACT.md`, every nested object closed and current fields required.
- [ ] **Step 6: Implement success result factory:** structured object + one minified JSON TextContent semantic equality.
- [ ] **Step 7: Implement error result factory:** isError=true + one safe TextContent + no structuredContent.
- [ ] **Step 8: Implement four thin tools** mapping validation/context/Application/presentation only.
- [ ] **Step 9: Add server instructions** with full first-512-character lifecycle/privacy semantics.
- [ ] **Step 10: Generate contract snapshots** under `contracts/mcp/hp-mcp-2/` from actual registration.
- [ ] **Step 11: Add stale-contract gate** for current_quest, LogicalQuestKey, start idempotent=true, human-only structured fallback, forbidden fields.
- [ ] **Step 12: Commit.** `feat: implement exact hp-mcp2 contract`

---

### Task 12: MCP protocol/process compatibility (`0.0.12`)

**Files:**
- Add protocol compatibility tests in App/Contract test projects
- Add process test harness
- Add Inspector qualification notes/scripts if repository policy permits

- [ ] **Step 1: Create 2026-07-28 official SDK client path test.**
- [ ] **Step 2: Create 2025-11-25 official SDK compatibility path test.** Both see equivalent four-tool semantics.
- [ ] **Step 3: Assert ordinary production server never sets concrete ProtocolVersion.**
- [ ] **Step 4: Process-test `hero-passport mcp --project-root <temp>` stdout for protocol framing only.**
- [ ] **Step 5: Verify 2025-era consumer can parse JSON TextContent even when structured rendering is ignored.**
- [ ] **Step 6: Run current official MCP Inspector lifecycle smoke** tools/list/start/list/finish/card/error.
- [ ] **Step 7: Record Inspector/version/evidence in release fixture.**
- [ ] **Step 8: Commit.** `test: qualify mcp protocol compatibility`

---

### Task 13: Codex reference E2E and AgentEvals (`0.0.13`)

**Files:**
- Create/update `tests/HeroPassport.AgentEvals/*`
- Add isolated Codex E2E harness/docs

- [ ] **Step 1: Verify current official Codex MCP config/CLI during implementation.** Do not copy stale syntax from memory.
- [ ] **Step 2: Register project-bound Hero Passport in isolated Codex environment.**
- [ ] **Step 3: Run lifecycle E2E** start -> work simulation -> list -> finish -> card -> restart -> durable read.
- [ ] **Step 4: Run same-open-declaration retry scenario** expecting reuse.
- [ ] **Step 5: Run same declaration after completed cycle scenario** expecting a new quest when new work is intentionally started.
- [ ] **Step 6: Run parallel distinct work scenario** expecting two quests.
- [ ] **Step 7: Build host-neutral AgentEval scenarios** meaningful work, trivial question, lost questId, privacy adversarial input, finish retry.
- [ ] **Step 8: Keep evals non-blocking until signal quality is demonstrated; record baseline.**
- [ ] **Step 9: Commit.** `test: add codex e2e and agent evals`

---

### Task 14: Cross-host qualification pack (`0.0.14`)

**Files:**
- Update `docs/integrations/*.md` with verified release evidence
- Optional test scripts only if a host exposes practical automation

- [ ] **Step 1: Re-check each host's current official MCP documentation** at RC time.
- [ ] **Step 2: Smoke VS Code** local stdio/project binding/tool lifecycle.
- [ ] **Step 3: Smoke JetBrains AI Assistant.**
- [ ] **Step 4: Smoke Zed.**
- [ ] **Step 5: Smoke Cursor.**
- [ ] **Step 6: Smoke Claude Code.**
- [ ] **Step 7: Record host/version/OS/transport/binding/results/date.**
- [ ] **Step 8: Promote only evidence-backed hosts to Qualified; leave others Documented.**
- [ ] **Step 9: Commit.** `docs: record host qualification matrix`

---

### Task 15: Architecture/privacy/dependency/package gates (`0.1.0-rc.1`)

**Files:**
- Expand `HeroPassport.Architecture.Tests`
- Add package/publish smoke scripts/config
- Update release docs

- [ ] **Step 1: Add layer-reference tests.**
- [ ] **Step 2: Add static gate against assembly-wide MCP discovery.**
- [ ] **Step 3: Add stale-contract scan** for retired/current-incorrect terms from `TESTING-QUALITY.md`.
- [ ] **Step 4: Add privacy schema/log/export deny-list tests.**
- [ ] **Step 5: Add dependency gate** Central Package Management/locked restore/NuGet vulnerability audit/no MCP ASP.NET package.
- [ ] **Step 6: Publish supported artifacts and run them on target OS/RID matrix.** Record actual `sqlite_version()` from each artifact.
- [ ] **Step 7: Verify every normal artifact qualifies SQLite >=3.51.3.**
- [ ] **Step 8: Run DB stress/crash/backup suite repeatedly enough to catch race regressions.**
- [ ] **Step 9: Capture startup latency/RSS/tool schema/result size budgets as release evidence, not speculative hard claims.**
- [ ] **Step 10: Commit.** `test: add release fitness gates`

---

### Task 16: `0.1.0` qualification and release

**Files:**
- Update `README.md`, `ROADMAP.md`, release notes/changelog if introduced
- Tag/package metadata after all gates succeed

- [ ] **Step 1: Clean locked restore.**

```bash
dotnet restore --locked-mode
```

- [ ] **Step 2: Release build.**

```bash
dotnet build --configuration Release --no-restore
```

- [ ] **Step 3: Deterministic test suites.**

```bash
dotnet test --configuration Release --no-build
```

- [ ] **Step 4: Run separate crash/concurrency/backup qualification suite** using real file DBs/child processes.
- [ ] **Step 5: Run contract/protocol/Inspector/Codex E2E qualification.**
- [ ] **Step 6: Review generated HP-MCP snapshots against `WIRE-CONTRACT.md`.**
- [ ] **Step 7: Review project-identity golden evidence including linked worktrees.**
- [ ] **Step 8: Verify published artifact SQLite versions/PRAGMAs/doctor.**
- [ ] **Step 9: Verify privacy search on artifacts/docs/log fixtures.**
- [ ] **Step 10: Record host qualification matrix and known caveats.**
- [ ] **Step 11: Update docs from evidence only; do not claim unexecuted support/tests.**
- [ ] **Step 12: Commit/tag release.** `chore: release hero passport 0.1.0`

---

## Plan self-review checklist

Before execution begins, verify:

```text
No LogicalQuestKeyV1 implementation task
No case-folded goal dedup
No start idempotent=true
No human-only TextContent success fallback
No reliance on DataAnnotations for runtime validation
No deferred read->write invariant transaction
No active DB File.Copy backup
No manual WAL/SHM deletion
No remote URL/path project identity
No SQLite runtime assumption without sqlite_version proof
No model workspacePath/heroId/projectId reintroduction
```

This plan is subordinate to normative architecture/deep-dive specs. If official SDK/provider behavior differs during implementation, stop the affected task, reproduce the discrepancy with a focused test, update the normative spec/ADR, then continue.
