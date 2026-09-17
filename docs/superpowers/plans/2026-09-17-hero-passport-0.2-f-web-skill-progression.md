# Hero Passport 0.2-F Web Skill Progression Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an authenticated, read-only `/skills` static-SSR surface showing all ten canonical Skills for the active Hero, with global Hero progression and current-Project contribution, without changing Hero Card/MCP contracts or game economy.

**Architecture:** Add one Domain read helper (`SkillProgressionRules.LevelXp`), one dedicated Application/store read contract, one parameterized `Microsoft.Data.Sqlite` read implementation in the existing store, and one Web service/page. Reuse the existing 0.2-B session boundary and 0.2-E read-only/static-SSR patterns. Keep Hero Card top-three semantics unchanged.

**Tech Stack:** C# 14, .NET 10, ASP.NET Core 10 Blazor static SSR, Microsoft.Data.Sqlite 10, SQLite WAL, xUnit v3.

**Spec:** `docs/superpowers/specs/2026-09-17-hero-passport-0.2-f-web-skill-progression-design.md`

## Global Constraints

- Dependency direction remains `Domain <- Application <- Infrastructure <- Web`.
- `/skills` is authenticated GET-only static SSR; no Interactive Server/WebAssembly.
- Existing `HeroCardResult`, MCP tool schemas and wire contracts remain unchanged.
- Canonical Skill order is exactly: `coding`, `testing_awareness`, `scope_control`, `documentation`, `tool_use`, `planning`, `research`, `debugging`, `review`, `maintenance`.
- All ten canonical Skills are returned, including zero-XP rows.
- Hero/global XP comes from `hero_skills`; current-Project XP is derived from canonical `quest_report_skills` joined through current-Project Quest history.
- Unseen Project reads must not create a Project row.
- No schema, migration, index, receipt write, projection write, settings write or new persistence stack.
- Multi-query store read uses one short deferred SELECT-only SQLite transaction.
- Domain/Application own progression semantics; Web does not calculate thresholds or level progress.
- Skill Level 10 stays capped for display while XP continues accumulating.
- Existing local browser session middleware must return 401 before product reads when unauthenticated.
- Web models/rendered HTML must exclude workspace fingerprint, internal ProjectId, local paths, request/receipt IDs, hashes, rule-version internals, source/diff/raw logs/prompts/secrets.
- Exact-head Linux CI and Windows Server 2025/macOS 15 release-platform qualification are mandatory before merge.

---

### Task 1: Add canonical Skill level-band progress

**Files:**
- Modify: `tests/HeroPassport.Domain.Tests/SkillProgressionTests.cs`
- Modify: `src/HeroPassport.Domain/Engine/SkillProgressionRules.cs`

**Interfaces:**
- Produces: `public static long LevelXp(long totalXp, int level, string ruleVersion)`.
- Existing `Level`, `Apply`, `NextLevelXpRequired`, thresholds and `RuleVersion = "skill-progression/2.0.0"` remain unchanged.

- [ ] **Step 1: Write failing Domain tests**

Add a theory proving current-level XP:

```csharp
[Theory]
[InlineData(0, 1, 0)]
[InlineData(49, 1, 49)]
[InlineData(50, 2, 0)]
[InlineData(95, 2, 45)]
[InlineData(124, 2, 74)]
[InlineData(125, 3, 0)]
[InlineData(1349, 9, 249)]
[InlineData(1350, 10, 0)]
[InlineData(6350, 10, 5000)]
public void LevelXpReturnsXpInsideCurrentSkillLevel(long totalXp, int level, long expected)
{
    Assert.Equal(expected, SkillProgressionRules.LevelXp(totalXp, level, "skill-progression/2.0.0"));
}
```

Add invalid-input assertions:

```csharp
Assert.Throws<ArgumentException>(() => SkillProgressionRules.LevelXp(0, 1, "skill-progression/1.0.0"));
Assert.Throws<ArgumentOutOfRangeException>(() => SkillProgressionRules.LevelXp(-1, 1, "skill-progression/2.0.0"));
Assert.Throws<ArgumentOutOfRangeException>(() => SkillProgressionRules.LevelXp(0, 0, "skill-progression/2.0.0"));
Assert.Throws<ArgumentOutOfRangeException>(() => SkillProgressionRules.LevelXp(0, 11, "skill-progression/2.0.0"));
Assert.Throws<ArgumentOutOfRangeException>(() => SkillProgressionRules.LevelXp(49, 2, "skill-progression/2.0.0"));
```

- [ ] **Step 2: Verify RED**

Run:

```text
dotnet test tests/HeroPassport.Domain.Tests/HeroPassport.Domain.Tests.csproj --filter FullyQualifiedName~SkillProgressionTests
```

Expected: compile/test failure because `SkillProgressionRules.LevelXp` does not exist.

- [ ] **Step 3: Implement the minimal Domain helper**

Add to `SkillProgressionRules`:

```csharp
public static long LevelXp(long totalXp, int level, string ruleVersion)
{
    RequireVersion(ruleVersion);
    JsonSafeInteger.Require(totalXp);
    RequireLevel(level);

    var threshold = LevelThresholds[level - 1];
    ArgumentOutOfRangeException.ThrowIfLessThan(totalXp, threshold);
    return checked(totalXp - threshold);
}
```

Do not change thresholds, rule version or persisted result shapes in this task.

- [ ] **Step 4: Verify GREEN**

Run the same focused Domain test command and require success.

- [ ] **Step 5: Commit**

```text
feat(domain): expose Skill level-band progress
```

---

### Task 2: Add the dedicated Application Skill progression contract

**Files:**
- Create: `src/HeroPassport.Application/Runtime/SkillProgressionModels.cs`
- Modify: `src/HeroPassport.Application/Runtime/IHeroPassportStateStore.cs`
- Modify: `src/HeroPassport.Application/Runtime/HeroPassportApplication.cs`
- Create: `tests/HeroPassport.Application.Tests/SkillProgressionReadContractTests.cs`
- Modify: every existing test fake implementing `IHeroPassportStateStore` only as required to compile, using explicit `throw new InvalidOperationException("Unused in this test.")` stubs unless the fake is the subject of this task.

**Interfaces:**
- Produces:

```csharp
public sealed record SkillProgressionSnapshot(
    string SkillKey,
    long Xp,
    int Level,
    bool IsLevelCapped,
    long LevelXp,
    long? NextLevelXpRequired);

public sealed record SkillProgressionRow(
    string SkillKey,
    SkillProgressionSnapshot Hero,
    SkillProgressionSnapshot Project);

public sealed record SkillProgressionResult(
    string HeroName,
    string ProjectDisplayName,
    IReadOnlyList<SkillProgressionRow> Skills);
```

- Store method:

```csharp
Task<SkillProgressionResult> GetSkillProgressionAsync(
    HeroId heroId,
    ProjectBindingContext project,
    CancellationToken cancellationToken = default);
```

- Application facade method has the same signature and calls `ValidateProject(project)` before delegation.

- [ ] **Step 1: Write failing Application tests**

Use a recording fake store to prove:

```text
GetSkillProgressionAsync delegates the exact HeroId;
Project display name is SafeText-normalized before delegation;
invalid fingerprint or identity version throws HP310 before store access.
```

The test should assert the fake store call count remains zero for invalid Project binding.

- [ ] **Step 2: Verify RED**

Run:

```text
dotnet test tests/HeroPassport.Application.Tests/HeroPassport.Application.Tests.csproj --filter FullyQualifiedName~SkillProgressionReadContractTests
```

Expected: compile failure because the new models/facade/store member do not exist.

- [ ] **Step 3: Add models, store contract and facade**

Implement only the signatures above and delegation through existing `ValidateProject`.

- [ ] **Step 4: Restore unrelated test fakes to compile**

Add only unused stubs required by the expanded interface. Do not introduce a new CQRS/read-store abstraction in 0.2-F.

- [ ] **Step 5: Verify GREEN**

Run the focused Application tests plus:

```text
dotnet test tests/HeroPassport.Application.Tests/HeroPassport.Application.Tests.csproj
```

- [ ] **Step 6: Commit**

```text
feat(application): add Skill progression read contract
```

---

### Task 3: Implement the read-only SQLite projection

**Files:**
- Create: `src/HeroPassport.Infrastructure/Persistence/SqliteHeroPassportStateStore.SkillProgression.cs`
- Create: `tests/HeroPassport.Infrastructure.Tests/SkillProgressionReadTests.cs`

**Interfaces:**
- Implements `IHeroPassportStateStore.GetSkillProgressionAsync`.
- Consumes `SkillProgressionRules.Level`, `LevelXp`, `NextLevelXpRequired`, `HeroPassportVersions.CurrentRules.SkillProgression`.

- [ ] **Step 1: Write a real-file SQLite RED test for ten canonical rows**

Bootstrap Hero `Nova`, finish Quests that award a subset of Skills, call `GetSkillProgressionAsync`, and assert the result keys are exactly:

```csharp
[
    "coding", "testing_awareness", "scope_control", "documentation", "tool_use",
    "planning", "research", "debugging", "review", "maintenance"
]
```

Assert untouched Skills have Hero and Project XP `0`, Level `1`, LevelXp `0`, `IsLevelCapped=false`, `NextLevelXpRequired=50`.

- [ ] **Step 2: Verify RED**

Run:

```text
dotnet test tests/HeroPassport.Infrastructure.Tests/HeroPassport.Infrastructure.Tests.csproj --filter FullyQualifiedName~SkillProgressionReadTests
```

Expected: failure because the SQLite store method is not implemented.

- [ ] **Step 3: Add additional RED cases before production implementation**

Add real SQLite tests proving:

1. global Hero totals include finished Quest Skill XP from Project A and Project B;
2. Project A totals exclude Project B;
3. another Hero's Project A Skill XP is excluded;
4. unseen Project returns ten zero Project snapshots and does not create a `projects` row;
5. Level/LevelXp/cap values match `SkillProgressionRules`;
6. before/after snapshot of all canonical product tables is byte/row-equivalent for the read operation after test setup completes.

For the no-write assertion, capture all product tables listed by the canonical data model and exclude only SQLite/EF internal metadata tables.

- [ ] **Step 4: Implement the minimal read path**

Use one existing product connection and:

```csharp
using var transaction = connection.BeginTransaction(deferred: true);
```

Within that transaction:

1. load/setup-check the Hero explicitly by `heroId` and capture Hero name;
2. resolve existing Project by validated workspace fingerprint to internal `projectId` + persisted display name;
3. read `hero_skills` for global totals;
4. when `projectId != null`, aggregate `quest_report_skills.xp_gained` through `quest_sessions` + `quest_reports` for exact `hero_id` + `project_id`;
5. materialize all ten canonical keys in fixed order, filling missing totals with zero;
6. derive each scope's Level/LevelXp/cap/next-band requirement using Domain rules;
7. commit/end the SELECT-only transaction and return the bounded result.

All SQL must be parameterized. Do not call `ExecuteAsync`, mutation helpers or commit observers.

- [ ] **Step 5: Verify GREEN**

Run the focused Infrastructure tests, then the whole Infrastructure test project.

- [ ] **Step 6: Commit**

```text
feat(infrastructure): project Skill progression reads
```

---

### Task 4: Add the Web service and bounded view models

**Files:**
- Create: `src/HeroPassport.Web/Services/HeroPassportSkillProgressionService.cs`
- Create: `tests/HeroPassport.Web.Tests/SkillProgressionServiceTests.cs`
- Create or update a Web model-boundary/privacy test as needed without weakening existing history/dashboard assertions.

**Interfaces:**
- Produces:

```csharp
public enum SkillProgressionPageStatus
{
    Ready,
    SetupRequired,
    Invalid,
}

public sealed record LoadSkillProgressionWebResult(
    SkillProgressionPageStatus Status,
    HeroPassportSkillProgressionViewModel? Page = null);
```

- Web view model contains Hero name, Project display name and ten rows. Each scope contains XP, Level, IsLevelCapped, LevelXp and nullable NextLevelXpRequired.

- [ ] **Step 1: Write service RED tests**

Use a fake Application/store seam consistent with existing Web tests to prove:

```text
setup incomplete -> SetupRequired and no Skill progression read;
setup complete + active Hero -> exact HeroId passed to Application read;
ready result maps all ten rows and does not expose persistence/security fields.
```

If a configured runtime context somehow has no active Hero, return `Invalid`; do not guess a Hero.

- [ ] **Step 2: Verify RED**

Run:

```text
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj --filter FullyQualifiedName~SkillProgressionServiceTests
```

- [ ] **Step 3: Implement the minimal service**

Call `GetRuntimeContextAsync(_project)` first. Short-circuit setup. Require `context.ActiveHero` for configured state. Then call `GetSkillProgressionAsync(context.ActiveHero.HeroId, _project)` and map to Web-only records.

- [ ] **Step 4: Verify GREEN**

Run the focused Web service tests.

- [ ] **Step 5: Commit**

```text
feat(web): add Skill progression service
```

---

### Task 5: Add `/skills` static SSR and process acceptance

**Files:**
- Create: `src/HeroPassport.Web/Components/Pages/Skills.razor`
- Modify: `src/HeroPassport.Web/Components/Pages/Home.razor`
- Modify: `src/HeroPassport.Web/Program.cs`
- Create: `tests/HeroPassport.Web.Tests/SkillProgressionStaticSsrTests.cs`
- Create: `tests/HeroPassport.Web.Tests/SkillProgressionProcessTests.cs`

**Interfaces:**
- Route: `@page "/skills"`.
- DI: singleton `HeroPassportSkillProgressionService`, matching existing Application/project singleton lifetime.

- [ ] **Step 1: Write static-source/SSR RED tests**

Prove the page:

```text
has @page "/skills";
injects HeroPassportSkillProgressionService;
renders setup-required state without product rows;
renders both Hero and Current project scope labels;
handles capped Skills without printing a fabricated next-level denominator;
does not use interactive render mode or a form.
```

Also assert Home includes an authenticated progression link to `/skills` next to the existing history/top-Skills surface.

- [ ] **Step 2: Verify RED**

Run the focused static SSR test class and require failure because the page/registration do not exist.

- [ ] **Step 3: Implement page, navigation and DI**

The page loads once through `HeroPassportSkillProgressionService.LoadAsync()`. For uncapped scopes render `Level {level} · {totalXp} XP · {levelXp} / {nextLevelXpRequired} level XP`. For capped scopes render `Level 10 · capped · {totalXp} XP`.

Do not put rule versions or internal IDs in HTML.

- [ ] **Step 4: Add real-process acceptance RED tests**

Follow the existing Kestrel/SQLite session bootstrap test harness and prove:

```text
GET /skills without session -> 401;
authenticated configured GET /skills -> 200;
all ten canonical keys appear in deterministic order;
Hero/global and current-Project values differ when fixture data spans Projects;
privacy deny-list values are absent;
unseen Project rendering does not add a Project row;
product-table snapshot before/after GET is identical.
```

- [ ] **Step 5: Run process tests and fix only product defects**

Run:

```text
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj --filter FullyQualifiedName~SkillProgressionProcessTests
```

Require GREEN.

- [ ] **Step 6: Run the entire Web test project**

```text
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj
```

Require GREEN with no Start/Finish/history/security regressions.

- [ ] **Step 7: Commit**

```text
feat(web): render Skill progression page
```

---

### Task 6: Canonical docs and exact-head qualification

**Files:**
- Modify: `docs/ROADMAP.md`
- Modify: `docs/ARCHITECTURE.md`
- Modify: `docs/SECURITY-PRIVACY.md`
- Modify: `docs/TESTING-QUALITY.md`
- Modify: `docs/DEPLOYMENT-MODES.md` only if its route inventory is otherwise stale.
- Keep: `docs/superpowers/specs/2026-09-17-hero-passport-0.2-f-web-skill-progression-design.md`
- Keep: this implementation plan.

- [ ] **Step 1: Synchronize canonical documentation**

Document 0.2-F as implemented read-only Skill progression, preserving explicit separation from the later Rank/Traits/Titles and management slices. State that Hero Card remains Top-3 and `/skills` is the complete Web read surface.

- [ ] **Step 2: Run stale-contract review**

Ensure no active document claims the current Web implementation stops at 0.2-E or that all Skill progression UI is future work.

- [ ] **Step 3: Run focused + full solution verification**

At minimum require:

```text
dotnet test tests/HeroPassport.Domain.Tests/HeroPassport.Domain.Tests.csproj
dotnet test tests/HeroPassport.Application.Tests/HeroPassport.Application.Tests.csproj
dotnet test tests/HeroPassport.Infrastructure.Tests/HeroPassport.Infrastructure.Tests.csproj
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj
dotnet test HeroPassport.slnx
```

Do not claim success without observed output.

- [ ] **Step 4: Open/update PR for issue #48**

PR body must describe implemented behavior and use `Closes #48` only once acceptance is actually present.

- [ ] **Step 5: Require exact-head CI**

The exact PR head must have GREEN Linux `ci` and Windows Server 2025/macOS 15 `release-platform` checks.

- [ ] **Step 6: Request independent exact-head review**

Request review only after the qualified SHA is stable. Resolve only findings applicable to that exact head; any fix creates a new head and requires qualification/review refresh.

- [ ] **Step 7: Merge with expected head SHA and verify post-merge**

Merge only when issue/PR metadata, checks and review are current. Verify `main` points to the expected merge commit and the post-merge push CI is GREEN before declaring 0.2-F complete.
