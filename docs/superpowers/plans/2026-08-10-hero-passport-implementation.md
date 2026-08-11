# Hero Passport v3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: use a task-by-task TDD execution workflow. Every task is independently reviewable. Do not implement later tasks early merely because a seam is obvious.

**Goal:** deliver Hero Passport `0.1.0`, a portable local-first stdio MCP server/CLI with HP-MCP/2, deterministic RPG progression, multi-agent-safe quest lifecycle, SQLite persistence, Codex reference qualification, contract compatibility tests and host integration documentation.

**Architecture:** modular monolith `Domain -> Application -> Infrastructure -> App`. Application owns transport-neutral semantics; MCP/CLI are thin adapters. Project/hero binding is local startup/application context, not model input. Multiple distinct open quests may coexist, while same logical work converges through a versioned logical key and DB uniqueness.

**Tech Stack:** C# 14, .NET SDK 10.0.302 / `net10.0`, official `ModelContextProtocol 2.0.0`, EF Core SQLite 10.0.10, `SQLitePCLRaw.bundle_e_sqlite3 3.0.5`, System.CommandLine 2.0.10, xUnit.net v3 3.2.2.

## Global constraints

- Use exact stable dependency baseline in `docs/DEPENDENCIES.md`.
- `McpServerOptions.ProtocolVersion` stays unset/null in the ordinary server.
- 0.1 runtime transport is stdio only; do not add `ModelContextProtocol.AspNetCore`.
- Exact HP-MCP/2 tool order: `hero.start_quest`, `hero.finish_quest`, `hero.list_active_quests`, `hero.get_card`.
- Register tool types explicitly; no assembly-wide discovery.
- Application correctness never depends on MCP sessions/connection identity.
- No model-facing source code, file content, diff, raw log, prompt/chat, secret, environment bag, workspace path or arbitrary metadata bag.
- Project binding uses `--project-root` or process cwd/Git-root discovery; MCP Roots are not a dependency.
- Hero binding uses local active/default state or startup `--hero`, never routine model input.
- Multiple distinct open quests allowed; same logical work converges using LogicalQuestKey V1.
- Maximum open quests per hero/project is 16 and must survive real SQLite concurrency tests.
- `FinishQuest` verifies bound hero/project context and creates at most one XP ledger event.
- Persistence uses real SQLite, `IDbContextFactory`, short synchronous DB calls, WAL/FULL/FKs, no `Task.Run` wrappers.
- Domain/Application do not render localized strings; App presentation owns `displayText`.
- `scope_control` RU = `Контроль`; clean scope = `Бонус за контроль`; violation = `Выход за задачу`.
- Keep documentation/ADR/contracts synchronized in the same PR for any semantic change.

---

# Planned repository structure

```text
HeroPassport.slnx
global.json
Directory.Build.props
Directory.Packages.props
.editorconfig
.gitignore

src/
  HeroPassport.Domain/
    Heroes/
    Projects/
    Quests/
      Quest.cs
      QuestId.cs
      QuestType.cs
      QuestResult.cs
      QuestStatus.cs
      LogicalQuestKey.cs
      LogicalQuestKeyV1.cs
    Rewards/
    Skills/
    Traits/
    Shared/

  HeroPassport.Application/
    Abstractions/
      IHeroStore.cs
      IQuestStore.cs
      IProjectStore.cs
      IHeroReadStore.cs
      IProjectBindingResolver.cs
      IHeroBindingResolver.cs
      IUnitOfWork.cs
    Context/
      HeroOperationContext.cs
      InvocationOrigin.cs
      InvocationSurface.cs
    Contracts/
      HeroError.cs
      HeroResult.cs
    Quests/
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
      SqliteUnitOfWork.cs
    Paths/
      AppDataPaths.cs
    Projects/
      ProjectBindingResolver.cs
      ProjectIdentityV1.cs
    Heroes/
      HeroBindingResolver.cs
    Configuration/
      HeroPassportOptions.cs
      HeroPassportOptionsValidator.cs
    Diagnostics/
    Export/

  HeroPassport.App/
    Program.cs
    Hosting/
      ServiceRegistration.cs
    Cli/
      RootCommandFactory.cs
      Commands/
    Mcp/
      HeroPassportMcpManifest.cs
      HeroPassportServerInstructions.cs
      McpOperationContextResolver.cs
      Tools/
        StartQuestTool.cs
        FinishQuestTool.cs
        ListActiveQuestsTool.cs
        GetCardTool.cs
    Presentation/
      HeroTextRenderer.cs
      Localization/

contracts/
  mcp/hp-mcp-2/           # generated after Task 10

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

## Task 1 — Reproducible .NET foundation (`0.0.1`)

**Files**

- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `.editorconfig`
- Create/modify: `.gitignore`
- Create: `HeroPassport.slnx`
- Create: four source `.csproj` files (Domain/Application/Infrastructure/App)
- Create: seven test/eval `.csproj` files listed above

**Interfaces produced**

No product API yet; this task produces build/dependency boundaries only.

**Steps**

- [ ] **1. Pin SDK.** Create `global.json` with SDK `10.0.302`, `rollForward: disable`, `allowPrerelease: false`, and Microsoft Testing Platform runner configuration supported by the SDK/xUnit baseline.
- [ ] **2. Centralize build rules.** `Directory.Build.props`: `net10.0`, C# 14, nullable enabled, implicit usings, deterministic build, latest stable analysis, warnings policy selected so the empty scaffold builds cleanly.
- [ ] **3. Centralize package versions.** `Directory.Packages.props` contains exactly the accepted package baseline and no MCP ASP.NET package.
- [ ] **4. Create project reference graph.** Domain none; Application -> Domain; Infrastructure -> Application+Domain; App -> Application+Infrastructure. Test projects reference only their intended layer(s).
- [ ] **5. Add package lock configuration** and generate committed `packages.lock.json` for package-using projects.
- [ ] **6. Add architecture smoke test** asserting project references have the intended direction from loaded assemblies/project metadata.
- [ ] **7. Verify.** Run:

```bash
dotnet --version
dotnet restore --use-lock-file
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

Expected: all pass, SDK exactly 10.0.302.

- [ ] **8. Commit:** `build: establish reproducible net10 foundation`.

---

## Task 2 — Domain vocabulary, operation context and stable error contract (`0.0.2`)

**Files**

- Create Domain ID/enums under `Heroes/`, `Projects/`, `Quests/`.
- Create: `src/HeroPassport.Domain/Quests/LogicalQuestKey.cs`
- Create: `src/HeroPassport.Domain/Quests/LogicalQuestKeyV1.cs`
- Create: `src/HeroPassport.Application/Context/HeroOperationContext.cs`
- Create: `InvocationOrigin.cs`, `InvocationSurface.cs`
- Create: `src/HeroPassport.Application/Contracts/HeroError.cs`
- Create: `HeroResult.cs`
- Tests: `HeroPassport.Domain.Tests/Quests/LogicalQuestKeyV1Tests.cs`
- Tests: `HeroPassport.Application.Tests/Contracts/HeroErrorTests.cs`

**Interfaces produced**

```csharp
public readonly record struct HeroId(Guid Value);
public readonly record struct ProjectId(Guid Value);
public readonly record struct QuestId(Guid Value);

public enum QuestType { Planning, Research, Coding, Review, Debugging, Documentation, Maintenance }
public enum QuestResult { Success, Partial, Failed, Blocked, Abandoned }

public readonly record struct LogicalQuestKey(int Version, ImmutableArray<byte> Hash);

public static class LogicalQuestKeyV1
{
    public const int Version = 1;
    public static LogicalQuestKey Create(QuestType type, string validatedGoal);
    public static string CanonicalizeGoal(string validatedGoal);
}

public sealed record HeroOperationContext(HeroId HeroId, ProjectId ProjectId, InvocationOrigin Origin);
public sealed record InvocationOrigin(InvocationSurface Surface, string? ClientName = null, string? ClientVersion = null);
```

**Steps**

- [ ] **1. Write logical-key goldens** for Unicode NFC equivalence, trim, Unicode whitespace collapse, invariant case normalization, type difference and real goal difference.
- [ ] **2. Verify tests fail** before implementation.
- [ ] **3. Implement canonicalization.** Normalize `NormalizationForm.FormC`; trim; treat Unicode whitespace via `char.IsWhiteSpace`/Rune-aware helper; collapse to ASCII space; apply invariant case normalization; hash UTF-8 of canonical quest key + newline + canonical goal with SHA-256.
- [ ] **4. Ensure no original goal mutation.** Test `Create` does not require/persist canonical text as the display/history text.
- [ ] **5. Implement typed IDs/enums/context.** Avoid a type called `ExecutionContext`.
- [ ] **6. Implement error model** preserving Code/Category/Retryability/MessageKey/SafeDetails semantics from `API-CONTRACTS.md`.
- [ ] **7. Add negative tests** showing client metadata does not enter domain APIs/reward types.
- [ ] **8. Run Domain + Application tests.**
- [ ] **9. Commit:** `feat: add v3 domain and operation contracts`.

---

## Task 3 — Deterministic reward and level engine (`0.0.3`)

**Files**

- Create: `Domain/Rewards/RewardRulesV1.cs`
- Create: `RewardCalculator.cs`, `RewardBreakdown.cs`, `QuestQualityFlags.cs`
- Create: `Domain/Heroes/LevelCurveV1.cs`
- Tests: focused reward/level golden files

**Interfaces**

```csharp
public static class RewardRulesV1 { public const string Version = "1.0.0"; }
public sealed class RewardCalculator
{
    public RewardBreakdown Calculate(QuestType type, QuestResult result, QuestQualityFlags flags, int scopeViolations, int userCorrections);
}
```

**Steps**

- [ ] **1. Write failing golden:** coding success + tests + clean scope + clear summary + no corrections = 95 XP.
- [ ] **2. Add boundary tests** for every base type, multiplier, penalty, min zero and summary threshold defined in `ENGINE-SPEC.md`.
- [ ] **3. Implement integer permille arithmetic only.** No `double`/locale-sensitive calculations.
- [ ] **4. Implement level threshold/progress functions** exactly from `ENGINE-SPEC.md` with boundary tests.
- [ ] **5. Run all Domain tests.**
- [ ] **6. Commit:** `feat: add deterministic reward and level rules`.

---

## Task 4 — Skills, Trust/Risk, traits and localization keys (`0.0.4`)

**Files**

- Domain skill normalizer/allocation types
- Trust/Risk rules
- initial trait progression
- tests for all three

**Steps**

- [ ] **1. Write failing skill normalization tests** for canonical aliases/unknowns/duplicates/max-3 behavior.
- [ ] **2. Write allocation conservation tests** for 1/2/3 skills and odd XP totals; assert sum exactly equals reward XP.
- [ ] **3. Implement cumulative-floor allocation** documented in engine spec.
- [ ] **4. Write/implement Trust/Risk rules** with clamp tests.
- [ ] **5. Write/implement three initial trait policies** with unlock monotonicity tests.
- [ ] **6. Verify persisted keys are canonical English keys only.** Localization is not added to Domain.
- [ ] **7. Commit:** `feat: add skills trust risk and traits`.

---

## Task 5 — Transport-neutral Application lifecycle (`0.0.5`)

**Files**

- `Application/Abstractions/IHeroStore.cs`
- `IQuestStore.cs`, `IProjectStore.cs`, `IHeroReadStore.cs`, `IUnitOfWork.cs`
- `Application/Quests/StartQuest/*`
- `FinishQuest/*`
- `ListActiveQuests/*`
- `Cards/GetHeroCard/*`
- Application tests with in-memory fakes (not EF InMemory)

**Key interfaces**

```csharp
public sealed record StartQuestCommand(HeroOperationContext Context, QuestType QuestType, string Goal);
public sealed record StartQuestResult(QuestId QuestId, bool AlreadyOpen, HeroCardReadModel Hero);

public sealed record FinishQuestCommand(
    HeroOperationContext Context,
    QuestId QuestId,
    QuestResult Result,
    string Summary,
    QuestMetrics Metrics,
    IReadOnlyList<string> SkillsUsed);

public sealed record ListActiveQuestsQuery(HeroOperationContext Context);
public sealed record ListActiveQuestsResult(IReadOnlyList<ActiveQuestReadModel> Quests);
```

**Steps**

- [ ] **1. Write StartQuest tests**: new key creates, same key returns same, distinct key coexists, >=16 returns HP133.
- [ ] **2. Write List tests**: empty success, max 16, deterministic order, exact context filtering.
- [ ] **3. Write Finish tests**: unknown HP130, wrong hero/project HP134, finished returns original, skill validation, all result cases.
- [ ] **4. Implement ports narrowly.** Do not add `IRepository<T>` or MediatR.
- [ ] **5. Implement handlers** with deterministic domain calls and typed errors.
- [ ] **6. Assert InvocationOrigin does not affect reward.** Run same command with Codex/unknown origin and compare result.
- [ ] **7. Commit:** `feat: add transport neutral quest lifecycle`.

---

## Task 6 — Platform paths, config, project/hero binding and presentation (`0.0.6`)

**Files**

- `Infrastructure/Paths/AppDataPaths.cs`
- `Infrastructure/Configuration/HeroPassportOptions.cs`
- options validator/loader
- `Infrastructure/Projects/ProjectBindingResolver.cs`
- `ProjectIdentityV1.cs`
- `Infrastructure/Heroes/HeroBindingResolver.cs`
- `App/Presentation/HeroTextRenderer.cs`
- localization resources/maps
- corresponding tests

**Interfaces**

```csharp
public interface IProjectBindingResolver
{
    ProjectBinding Resolve(string? explicitProjectRoot, string processWorkingDirectory);
}

public interface IHeroBindingResolver
{
    HeroId Resolve(string? explicitSelector);
}
```

**Steps**

- [ ] **1. Write path tests** for Windows LocalApplicationData semantics, macOS Application Support and Linux XDG/fallback; use testable platform abstraction if needed without third-party package.
- [ ] **2. Write `HERO_PASSPORT_HOME` isolation tests.**
- [ ] **3. Implement strict config v1** with unknown-property/version rejection.
- [ ] **4. Write project resolver tests**: explicit root, cwd, nested Git repo root, non-Git fallback, spaces/unicode, invalid path -> HP310.
- [ ] **5. Implement versioned workspace fingerprint** without persisting absolute path.
- [ ] **6. Write hero selector tests** for default, explicit unique selector, unknown/ambiguous failure.
- [ ] **7. Write renderer goldens** for RU/EN compact/normal; include required RU labels.
- [ ] **8. Verify list-active human text does not echo goal by default.**
- [ ] **9. Commit:** `feat: add local binding config and presentation`.

---

## Task 7 — EF Core SQLite schema and migration 0001 (`0.0.7`)

**Files**

- `Infrastructure/Persistence/HeroPassportDbContext.cs`
- EF entities/configurations
- initial migration + model snapshot
- stores/queries skeleton
- Infrastructure integration tests

**Schema requirements**

`quest_sessions` includes:

```text
logical_key
logical_key_version
```

and partial unique open logical key, **not** one-open-per-project.

`xp_events.quest_id` unique.

**Steps**

- [ ] **1. Write migration/schema assertions first** against a temp file DB; expected failure because no migration exists.
- [ ] **2. Implement DbContext mappings** with explicit lengths/required/FKs/checks where SQLite supports/useful.
- [ ] **3. Generate initial EF migration.** Add SQLite-specific migration SQL for partial unique index if required.
- [ ] **4. Configure connection builder** with ReadWriteCreate, Default cache, FKs, pooling, 5s timeout.
- [ ] **5. Initialize/verify PRAGMAs:** WAL, FULL, foreign keys.
- [ ] **6. Query and assert actual `sqlite_version()`.**
- [ ] **7. Test fresh migration and seeds.**
- [ ] **8. Confirm no `EnsureCreated` product path.**
- [ ] **9. Commit:** `feat: add sqlite v3 persistence schema`.

---

## Task 8 — SQLite transaction and concurrency correctness (`0.0.8`)

**Files**

- `Infrastructure/Persistence/SqliteUnitOfWork.cs`
- store write implementations
- concurrency integration test fixtures

**Steps**

- [ ] **1. Implement same-key race test** with two independent DbContexts/connections targeting the same file, synchronized to attempt start concurrently.
- [ ] **2. Verify naïve implementation fails or characterize behavior.** Do not skip failure evidence.
- [ ] **3. Implement write transaction/unique-race translation** so both calls return one persisted quest ID.
- [ ] **4. Implement cap race test** starting from 15 active quests with two distinct concurrent starts.
- [ ] **5. If ordinary EF transaction can permit 17**, localize an SQLite immediate/write-serialization path in Infrastructure; rerun until active count is exactly <=16 and one caller gets HP133.
- [ ] **6. Implement finish race test**; assert one report, one xp_event, one aggregate mutation, both callers converge to original persisted result.
- [ ] **7. Implement context mismatch integration test** using a valid UUID from another project/hero.
- [ ] **8. Implement busy timeout/error translation** without Polly/general retries.
- [ ] **9. Run concurrency suite repeatedly (e.g. 100 iterations for race fixtures) in CI-stable form.**
- [ ] **10. Commit:** `feat: harden sqlite quest concurrency`.

---

## Task 9 — CLI and doctor (`0.0.9`)

**Files**

- `App/Program.cs`
- `Hosting/ServiceRegistration.cs`
- `Cli/RootCommandFactory.cs`
- Commands for init/mcp/doctor/card/quest list/export/data path/version
- process tests

**Steps**

- [ ] **1. Write parser/help tests** including `mcp --project-root` and `--hero`.
- [ ] **2. Build Generic Host/DI composition** once; command handlers call Application/adapters, not DbContext directly.
- [ ] **3. Implement `init`.**
- [ ] **4. Implement `doctor` typed checks**: version/runtime/paths/config/hero/project binding when applicable/SQLite version/PRAGMAs/migrations/manifest/protocol policy.
- [ ] **5. Implement `card`, `quest list --active`, `export`, `data path`.**
- [ ] **6. Implement stable `--json` only for script-relevant commands and semantic HeroError representation.
- [ ] **7. Process-test stdout/stderr and temp HOME.**
- [ ] **8. Commit:** `feat: add cli and diagnostics`.

---

## Task 10 — HP-MCP/2 stdio and generated contract snapshots (`0.0.10`)

**Files**

- `App/Mcp/HeroPassportMcpManifest.cs`
- `HeroPassportServerInstructions.cs`
- `McpOperationContextResolver.cs`
- four tool classes
- Contract test generators/snapshots
- App process tests

**Interfaces/tools**

```text
StartQuestTool
FinishQuestTool
ListActiveQuestsTool
GetCardTool
```

**Steps**

- [ ] **1. Write failing manifest test** expecting exact names/order and exactly four tools.
- [ ] **2. Configure official MCP SDK** with stdio and `ProtocolVersion` left null/unset. Do not reference ASP.NET MCP package.
- [ ] **3. Register tool types explicitly** through official SDK type/generic APIs. No `WithToolsFromAssembly` catch-all.
- [ ] **4. Register server instructions** whose first 512 chars contain complete lifecycle/privacy semantics.
- [ ] **5. Implement strict input DTOs** and confirm generated schemas meet conservative profile/deny-list.
- [ ] **6. Implement `McpOperationContextResolver`** from startup project/hero binding plus bounded SDK client metadata where available.
- [ ] **7. Implement tool adapters**: map -> Application -> render -> structured/text MCP result; no EF/domain math in tools.
- [ ] **8. Set accurate annotations.** Tasks unsupported.
- [ ] **9. Set public list cache scope and initial 300000ms TTL through SDK-supported metadata only where protocol supports it. Do not hardcode behavior into HP-MCP DTOs.
- [ ] **10. Generate canonical contract snapshots** into `contracts/mcp/hp-mcp-2/` from actual manifest/schema.
- [ ] **11. Write stale/forbidden schema tests** including no `workspacePath`, `schemaVersion`, `heroId`, `projectId`, `agentHint`, generic metadata bags.
- [ ] **12. Spawn executable and assert MCP stdout protocol purity.**
- [ ] **13. Commit:** `feat: add hp-mcp-2 stdio contract`.

---

## Task 11 — MCP revision compatibility and Inspector (`0.0.10` continued)

**Files**

- `HeroPassport.Contract.Tests/Protocol/*`
- release/test scripts as appropriate

**Steps**

- [ ] **1. Build an official C# SDK client test path using protocol `2026-07-28`.** Verify tools/start/list/finish/card.
- [ ] **2. Build/force a `2025-11-25` initialize-era compatibility path** using supported SDK test hooks/options and verify equivalent Hero Passport semantics.
- [ ] **3. Add assertion that product server configuration never sets `ProtocolVersion` to a concrete value.
- [ ] **4. Assert application outcome is unchanged across protocol eras for the same operation fixture.
- [ ] **5. Run current MCP Inspector** against packaged/built stdio server; record command/script in `TESTING-QUALITY.md`/developer docs if needed.
- [ ] **6. Verify 2026 cache metadata does not break older protocol serialization through SDK compatibility.
- [ ] **7. Commit:** `test: qualify mcp revision compatibility`.

---

## Task 12 — Codex qualification and host-neutral AgentEvals (`0.0.11`)

**Files**

- `tests/HeroPassport.AgentEvals/Scenarios/*`
- Codex runner/harness
- `docs/integrations/CODEX.md` evidence updates

**Steps**

- [ ] **1. Install/register built Hero Passport into current Codex using native config with explicit project binding.
- [ ] **2. E2E exact four-tool discovery.**
- [ ] **3. Run new start -> finish -> card scenario and assert DB/XP.
- [ ] **4. Run same-task duplicate start -> same quest ID.
- [ ] **5. Run distinct parallel tasks -> two IDs, list both.
- [ ] **6. Restart server/agent context -> list recovery -> finish selected quest.
- [ ] **7. Run finish retry -> exactly one XP event.
- [ ] **8. Create host-neutral eval scenario definitions** for meaningful work, tiny factual no-op, parallel/reuse/recovery/privacy/card.
- [ ] **9. Implement Codex runner** that captures tool sequence/args and DB outcome without depending on exact prose beyond bounded expected behavior.
- [ ] **10. Record Codex version/OS/date and mark tested release candidate Qualified only after pass.
- [ ] **11. Commit:** `test: qualify codex and agent lifecycle`.

---

## Task 13 — Cross-host integration smoke pack (`0.0.12`)

**Files**

- update `docs/integrations/*.md` with evidence blocks
- optional manual smoke checklist script/templates under `tests/HostSmoke/` or `docs/testing/`

**Steps**

- [ ] **1. Recheck current official docs** for VS Code, JetBrains, Zed, Cursor and Claude Code; update config examples if schemas changed.
- [ ] **2. Smoke VS Code** project binding + four-tool lifecycle on an available supported OS.
- [ ] **3. Smoke JetBrains** project-level Working directory + lifecycle.
- [ ] **4. Smoke Zed** `--project-root` local configuration + lifecycle.
- [ ] **5. Smoke Cursor** current documented local stdio config + lifecycle.
- [ ] **6. Smoke Claude Code** current native MCP config/scope + lifecycle.
- [ ] **7. Record failures/caveats honestly.** Only tested environments become Qualified; others stay Documented/protocol-compatible.
- [ ] **8. Do not add host-specific packages/runtime branches to make a smoke pass; fix standard MCP interop or document host limitation.
- [ ] **9. Commit:** `docs: record mcp host qualification evidence`.

---

## Task 14 — Architecture/privacy/dependency fitness gates (`0.0.13`)

**Files**

- Architecture/Contract tests
- CI workflow/config when repository CI is introduced

**Steps**

- [ ] **1. Layer-reference tests** for Domain/Application restrictions.
- [ ] **2. Static/reference test for no assembly-wide MCP discovery.**
- [ ] **3. Stale-v2 scan** rejects active `hero.current_quest`, `CurrentQuestTool`, `GetCurrentQuestHandler` and one-open-per-project constraint outside clearly historical docs.
- [ ] **4. Protocol policy test** rejects concrete `ProtocolVersion` assignment in production server config.
- [ ] **5. Privacy schema/log/export deny-list tests.
- [ ] **6. Dependency gate** rejects unapproved direct package versions/out-of-CPM package refs and runs NuGet vulnerability audit.
- [ ] **7. HTTP dependency gate** verifies no `ModelContextProtocol.AspNetCore` in 0.1 project graph.
- [ ] **8. Native SQLite version/PRAGMA doctor fixture.
- [ ] **9. Commit:** `test: enforce architecture and privacy contracts`.

---

## Task 15 — `0.1.0-rc.1` release qualification

**Files**

- release notes/changelog if established
- package metadata/tool packaging
- CI/release scripts
- qualification evidence docs

**Steps**

- [ ] **1. Pack/install the .NET tool into a clean isolated environment.
- [ ] **2. Run locked restore/build/full tests + audit.
- [ ] **3. Run fresh DB and previous-version/migration fixture suite.
- [ ] **4. Run MCP 2026 + 2025 compatibility tests and contract snapshot check.
- [ ] **5. Run MCP Inspector.
- [ ] **6. Run Codex E2E and AgentEvals.
- [ ] **7. Run packaging matrix on Windows/Linux/macOS; include paths with spaces/unicode and actual native SQLite load.
- [ ] **8. Run/record other host smoke matrix according to available environments.
- [ ] **9. Verify docs have no active contradictions/stale v2 normative references.
- [ ] **10. Freeze feature scope; only fix release blockers after RC.
- [ ] **11. Commit/tag RC following repository release policy.

---

## Task 16 — `0.1.0` Portable Local MCP Core

**Release definition**

```text
one portable dotnet tool
local stdio HP-MCP/2
four static tools
multi-agent-safe quest lifecycle
SQLite durable progression
CLI/doctor
Codex Qualified
other hosts honestly tiered
no source/diff/raw-log ingestion
no own HTTP/public API
```

**Steps**

- [ ] **1. Resolve every RC-blocking defect without scope expansion.
- [ ] **2. Re-run all release qualification gates.
- [ ] **3. Confirm public docs/version axes: Product 0.1.0, HP-MCP/2, rule/key versions and negotiated MCP policy.
- [ ] **4. Confirm NuGet package/readme metadata and installation instructions.
- [ ] **5. Publish release only after artifact/install smoke succeeds from the published package.
- [ ] **6. Record final host qualification matrix and known limitations.

---

# Post-0.1 plans — not implementation tasks in this plan

## 0.1.1 Integration/distribution polish

Only evidence-driven:

```text
integration show <host> snippet renderer
broader automated host smoke
MCP Registry publication if preview maturity/package identity are acceptable
additional Qualified hosts
```

## 0.2.0 Blazor dashboard

Separate design/spec/plan. Uses Application/read models; no DbContext in components.

## Streamable HTTP

Separate design/spec/plan only after `DEPLOYMENT-MODES.md` trigger. Add ASP.NET MCP package then, configure explicit stateless HTTP mode, project/auth binding and network security. Do not add legacy SSE.

## Public/multi-tenant

Separate product architecture: OAuth/principal authorization/tenant isolation/remote storage/backups/rate limits. Local SQLite schema is not assumed to be hosted tenancy architecture.

---

# Final plan self-review checklist

Before executing Task 1, verify these plan invariants against normative docs:

```text
[ ] no current_quest implementation task
[ ] no single-open-quest constraint
[ ] no ProtocolVersion=2026-07-28 pin
[ ] no Roots dependency
[ ] no workspacePath MCP input
[ ] no ASP.NET MCP in 0.1 dependencies
[ ] no per-host runtime adapter
[ ] same-key start race tested
[ ] active-cap race tested
[ ] finish race/context mismatch tested
[ ] contract snapshots generated from actual SDK
[ ] both protocol eras tested
[ ] Codex qualification distinct from other host support claims
```

If any implementation discovery contradicts an official stable SDK/spec behavior, stop that task, document the concrete discrepancy, update the relevant normative spec/ADR first, then resume with one coherent contract rather than adding a compatibility hack silently.
