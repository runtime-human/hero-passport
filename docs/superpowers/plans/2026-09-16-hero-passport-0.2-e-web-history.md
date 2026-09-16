# Hero Passport 0.2-E Bounded Web History Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add authenticated, read-only, bounded current-Project Quest history at `/history` and `/history/{questId}` with no durable writes and no Web-owned persistence.

**Architecture:** Add immutable history read models to Application and two validated facade methods. Extend the existing `IHeroPassportStateStore` and `SqliteHeroPassportStateStore` with parameterized read-only SELECTs in a dedicated partial file; do not introduce a second persistence stack. Add a dedicated Web history service and two static-SSR GET pages that map only approved product facts, with real-process tests proving Project scoping, privacy, selector behavior, hard limit 25, and no writes.

**Tech Stack:** C# 14, .NET 10, ASP.NET Core 10 / Blazor static SSR, Microsoft.Data.Sqlite, xUnit v3, existing GitHub Actions qualification.

**Spec:** `docs/superpowers/specs/2026-09-16-hero-passport-0.2-e-web-history-design.md`

## Global Constraints

- Baseline is `main@d380efeea503af743b94572657726e7b8abdf1de`; implementation branch is `feat/0.2-e-web-history`.
- Routes are GET-only: `/history` and `/history/{questId}`; do not add Minimal API/REST endpoints or mutation forms.
- List is current Project only, deterministic `started_at_utc DESC, quest_id DESC`, hard limit 25, not caller configurable.
- Detail selector must be lowercase canonical UUIDv7; malformed selector returns 400 before store access.
- Missing and foreign-Project Quest return the same 404 behavior.
- History reads must not create Project rows, update settings, write receipts, mutate Quests/reports/progression, or call persistence commit hooks.
- Web must not reference EF Core, `Microsoft.Data.Sqlite`, or `HeroPassport.Infrastructure.Persistence` from Components/Services.
- No new schema/migration/index unless failing evidence proves it is necessary and the design is revised first.
- Do not surface reward decomposition, rank/trait/title progression, rule versions, workspace fingerprints, internal Project IDs, paths, request/receipt IDs, hashes, or secrets.
- Keep all existing Start/Finish/security/idempotency behavior unchanged.

---

### Task 1: Application history contracts and validated facade

**Files:**
- Create: `src/HeroPassport.Application/Runtime/HistoryModels.cs`
- Modify: `src/HeroPassport.Application/Runtime/IHeroPassportStateStore.cs`
- Modify: `src/HeroPassport.Application/Runtime/HeroPassportApplication.cs`
- Create: `tests/HeroPassport.Application.Tests/HistoryBehaviorTests.cs`
- Modify: test fake stores implementing `IHeroPassportStateStore` only as required to compile; non-history fakes get explicit `throw Unused()` implementations for the two new methods.

**Interfaces:**
- Produces:
```csharp
public sealed record ProjectQuestHistoryResult(
    string ProjectDisplayName,
    IReadOnlyList<ProjectQuestHistoryItem> Items);

public sealed record ProjectQuestHistoryItem(
    QuestId QuestId,
    string HeroName,
    string QuestType,
    string Title,
    string Status,
    string? Result,
    long? XpGained,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc);

public sealed record QuestHistoryDetailResult(
    QuestId QuestId,
    string HeroName,
    string ProjectDisplayName,
    string QuestType,
    string Title,
    string Goal,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    QuestHistoryReport? Report);

public sealed record QuestHistoryReport(
    string Result,
    string Summary,
    bool TestsMentioned,
    int ScopeViolations,
    int UserCorrections,
    string BuildStatus,
    string BuildEvidence,
    string TestsStatus,
    string TestsEvidence,
    long XpGained,
    IReadOnlyList<string> SkillsUsed);
```
- `IHeroPassportStateStore` additions:
```csharp
Task<ProjectQuestHistoryResult> GetProjectQuestHistoryAsync(
    ProjectBindingContext project,
    CancellationToken cancellationToken = default);

Task<QuestHistoryDetailResult?> GetQuestHistoryDetailAsync(
    QuestId questId,
    ProjectBindingContext project,
    CancellationToken cancellationToken = default);
```
- `HeroPassportApplication` additions validate Project binding before delegation:
```csharp
public Task<ProjectQuestHistoryResult> GetProjectQuestHistoryAsync(
    ProjectBindingContext project,
    CancellationToken cancellationToken = default) =>
    store.GetProjectQuestHistoryAsync(ValidateProject(project), cancellationToken);

public Task<QuestHistoryDetailResult?> GetQuestHistoryDetailAsync(
    QuestId questId,
    ProjectBindingContext project,
    CancellationToken cancellationToken = default) =>
    store.GetQuestHistoryDetailAsync(questId, ValidateProject(project), cancellationToken);
```

- [ ] **Step 1: Write RED Application tests**

Create `HistoryBehaviorTests.cs` with a recording fake store. Assert an invalid fingerprint returns `HP310` and leaves both history-call counters at zero; assert a valid list/detail call receives the normalized Project display name and exact `QuestId`.

```csharp
[Fact]
public async Task HistoryValidatesProjectBeforeStoreAccess()
{
    var store = new RecordingStateStore();
    var app = new HeroPassportApplication(store, TimeProvider.System);
    var invalid = new ProjectBindingContext("Project", "bad", "project-identity/1");

    var error = await Assert.ThrowsAsync<HeroPassportException>(() =>
        app.GetProjectQuestHistoryAsync(invalid, TestContext.Current.CancellationToken));

    Assert.Equal("HP310", error.Code);
    Assert.Equal(0, store.ListHistoryCalls);
    Assert.Equal(0, store.DetailHistoryCalls);
}
```

- [ ] **Step 2: Run RED test**

Run:
```bash
dotnet test tests/HeroPassport.Application.Tests/HeroPassport.Application.Tests.csproj --filter HistoryBehaviorTests
```
Expected: FAIL because history models/methods do not exist.

- [ ] **Step 3: Add Application models, store methods, facade methods, and compile-only fake stubs**

Use the signatures above exactly. Find fake implementations with:
```bash
rg -l "IHeroPassportStateStore" tests
```
and add these stubs wherever history is not under test:
```csharp
public Task<ProjectQuestHistoryResult> GetProjectQuestHistoryAsync(ProjectBindingContext project, CancellationToken cancellationToken = default) => throw Unused();
public Task<QuestHistoryDetailResult?> GetQuestHistoryDetailAsync(QuestId questId, ProjectBindingContext project, CancellationToken cancellationToken = default) => throw Unused();
```
Use each fake's existing exception helper where named differently.

- [ ] **Step 4: Run Application suite**

```bash
dotnet test tests/HeroPassport.Application.Tests/HeroPassport.Application.Tests.csproj
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/HeroPassport.Application tests/HeroPassport.Application.Tests tests/HeroPassport.Web.Tests
git commit -m "feat(application): add bounded quest history reads"
```

---

### Task 2: SQLite current-Project history reads and no-write invariant

**Files:**
- Create: `src/HeroPassport.Infrastructure/Persistence/SqliteHeroPassportStateStore.History.cs`
- Create: `tests/HeroPassport.Infrastructure.Tests/QuestHistoryReadTests.cs`

**Interfaces:** Consumes Task 1 models/methods. Produces the concrete history implementation in the existing `SqliteHeroPassportStateStore` partial class.

The list query is one parameterized SELECT and must apply Project scope/order/limit in SQL:
```sql
SELECT
  q.id, h.name, q.quest_type, q.title, q.status,
  r.result, r.xp_gained, q.started_at_utc, q.finished_at_utc
FROM projects p
JOIN quest_sessions q ON q.project_id = p.id
JOIN heroes h ON h.id = q.hero_id
LEFT JOIN quest_reports r ON r.quest_id = q.id
WHERE p.workspace_fingerprint = $fingerprint
ORDER BY q.started_at_utc DESC, q.id DESC
LIMIT 25;
```
Project display name is read separately with a single SELECT; if the Project row does not exist, return `new ProjectQuestHistoryResult(project.DisplayName, [])` without INSERT/UPDATE.

The detail query must include Project predicate in SQL:
```sql
SELECT
  q.id, h.name, p.display_name, q.quest_type, q.title, q.goal, q.status,
  q.started_at_utc, q.finished_at_utc,
  r.id, r.result, r.summary, r.tests_mentioned, r.scope_violations,
  r.user_corrections, r.build_status, r.build_evidence,
  r.tests_status, r.tests_evidence, r.xp_gained
FROM quest_sessions q
JOIN projects p ON p.id = q.project_id
JOIN heroes h ON h.id = q.hero_id
LEFT JOIN quest_reports r ON r.quest_id = q.id
WHERE q.id = $questId AND p.workspace_fingerprint = $fingerprint
LIMIT 1;
```
If a report exists, load skills with one bounded second query:
```sql
SELECT skill_key
FROM quest_report_skills
WHERE quest_report_id = $reportId
ORDER BY ordinal
LIMIT 3;
```
Parse timestamps with the same exact UTC format already used by `SqliteHeroPassportStateStore.Context.cs`.

- [ ] **Step 1: Write RED real-SQL tests**

`QuestHistoryReadTests.cs` must cover: unseen Project returns empty and Project count stays zero; current-Project-only rows across multiple Heroes; >25 rows returns exactly newest 25; open row has null result/xp/finished; finished detail maps summary/attestations/xp/skills; missing and foreign Project detail return null; repeated list/detail reads leave snapshots of `projects`, `quest_sessions`, `quest_reports`, `xp_events`, `mutation_receipts`, and `app_settings` unchanged.

Use existing public Application/Store mutation APIs to seed canonical data where practical. For the >25 ordering fixture, direct SQL seed is acceptable only inside Infrastructure tests and must satisfy schema constraints.

- [ ] **Step 2: Run RED Infrastructure tests**

```bash
dotnet test tests/HeroPassport.Infrastructure.Tests/HeroPassport.Infrastructure.Tests.csproj --filter QuestHistoryReadTests
```
Expected: FAIL because the SQLite history methods are not implemented.

- [ ] **Step 3: Implement `SqliteHeroPassportStateStore.History.cs`**

Use `HeroPassportDatabase.OpenConnectionAsync`, existing `Command(...)`, parameter binding, and pure reads only. Do not begin a write transaction, call `ExecuteAsync`, `ObserveCommitBoundary`, or any INSERT/UPDATE/DELETE.

- [ ] **Step 4: Run Infrastructure tests**

```bash
dotnet test tests/HeroPassport.Infrastructure.Tests/HeroPassport.Infrastructure.Tests.csproj --filter QuestHistoryReadTests
dotnet test tests/HeroPassport.Infrastructure.Tests/HeroPassport.Infrastructure.Tests.csproj
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/HeroPassport.Infrastructure/Persistence/SqliteHeroPassportStateStore.History.cs tests/HeroPassport.Infrastructure.Tests/QuestHistoryReadTests.cs
git commit -m "feat(infrastructure): read bounded project quest history"
```

---

### Task 3: Web history service and privacy-bounded view models

**Files:**
- Create: `src/HeroPassport.Web/Services/HeroPassportHistoryService.cs`
- Modify: `src/HeroPassport.Web/Program.cs`
- Create: `tests/HeroPassport.Web.Tests/HistoryServiceTests.cs`

**Interfaces:**
```csharp
public enum QuestHistoryPageStatus { Ready, Invalid, NotFound }

public sealed record HeroPassportHistoryListViewModel(
    string ProjectDisplayName,
    IReadOnlyList<HeroPassportHistoryItemViewModel> Items);

public sealed record HeroPassportHistoryItemViewModel(
    string QuestId,
    string HeroName,
    string QuestType,
    string Title,
    string Status,
    string? Result,
    long? XpGained,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc);

public sealed record HeroPassportQuestHistoryViewModel(
    string HeroName,
    string ProjectDisplayName,
    string QuestType,
    string Title,
    string Goal,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    HeroPassportQuestHistoryReportViewModel? Report);
```
The report view model contains only result, summary, four attestation status/evidence/count facts, XP gained, and `IReadOnlyList<string> SkillsUsed`.

`HeroPassportHistoryService.LoadListAsync()` calls Application directly. `LoadDetailAsync(string routeQuestId)` must parse `QuestId.Parse` inside a `try/catch (FormatException)` before any Application call. Invalid -> `Invalid`; Application null -> `NotFound`; otherwise `Ready`.

- [ ] **Step 1: Write RED service tests**

Use a fake `IHeroPassportStateStore`/Application. Assert malformed route returns Invalid and history store call count remains zero. Assert canonical unavailable returns NotFound. Assert Ready mapping excludes Project binding internals and preserves item order from Application.

- [ ] **Step 2: Run RED Web service tests**

```bash
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj --filter HistoryServiceTests
```
Expected: FAIL because service/models are absent.

- [ ] **Step 3: Implement service and register singleton**

In `Program.cs` add:
```csharp
builder.Services.AddSingleton<HeroPassportHistoryService>();
```
Do not add persistence references to the service.

- [ ] **Step 4: Run Web service tests and architecture tests**

```bash
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj --filter HistoryServiceTests
dotnet test tests/HeroPassport.Architecture.Tests/HeroPassport.Architecture.Tests.csproj
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/HeroPassport.Web/Services/HeroPassportHistoryService.cs src/HeroPassport.Web/Program.cs tests/HeroPassport.Web.Tests/HistoryServiceTests.cs
git commit -m "feat(web): add bounded history presentation service"
```

---

### Task 4: Static-SSR history list/detail pages and navigation

**Files:**
- Create: `src/HeroPassport.Web/Components/Pages/History.razor`
- Create: `src/HeroPassport.Web/Components/Pages/QuestHistory.razor`
- Modify: `src/HeroPassport.Web/Components/Pages/Home.razor`
- Create: `tests/HeroPassport.Web.Tests/HistoryStaticSsrTests.cs`

**Page contract:**

`History.razor`:
```razor
@page "/history"
@inject HeroPassportHistoryService History
```
Render Project display name, empty state, and at most the supplied items. Link each item with `href="/history/@item.QuestId"`. No `<EditForm>`, `[SupplyParameterFromForm]`, POST handler, hidden field, query/filter input, or client-side interactivity.

`QuestHistory.razor`:
```razor
@page "/history/{QuestId}"
@inject HeroPassportHistoryService History
@inject IHttpContextAccessor HttpContextAccessor
```
If existing pages set status via another established mechanism, follow that exact mechanism instead of introducing a new one. `OnParametersSetAsync` calls `History.LoadDetailAsync(QuestId)`. Invalid -> HTTP 400; NotFound -> HTTP 404; Ready -> render approved facts. Do not render raw QuestId as product metadata except navigation/route use.

`Home.razor` adds one authenticated/configured-project entry:
```razor
<p><a href="/history">Quest history</a></p>
```
inside the existing configured content; do not redesign navigation.

- [ ] **Step 1: Write RED static-source tests**

Assert exact two routes, no forms/POST surfaces, no forbidden privacy tokens (`WorkspaceFingerprint`, `ProjectId`, `RequestId`, `Receipt`, `ArgsHash`, `Sqlite`), service registration, and dashboard History link.

- [ ] **Step 2: Run RED static tests**

```bash
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj --filter HistoryStaticSsrTests
```
Expected: FAIL because pages/navigation do not exist.

- [ ] **Step 3: Implement pages using existing static-SSR status-code pattern**

Keep formatting simple and semantic (`main`, `header`, `section`, `ul`). Use `<time datetime="...">` for timestamps if consistent with current CSS; otherwise render invariant UTC text without adding timezone preference behavior.

- [ ] **Step 4: Run Web + architecture tests**

```bash
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj --filter HistoryStaticSsrTests
dotnet test tests/HeroPassport.Architecture.Tests/HeroPassport.Architecture.Tests.csproj
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/HeroPassport.Web/Components/Pages src/HeroPassport.Web/Components/Pages/Home.razor tests/HeroPassport.Web.Tests/HistoryStaticSsrTests.cs
git commit -m "feat(web): render bounded quest history"
```

---

### Task 5: Real-process history acceptance and durable no-write proof

**Files:**
- Create: `tests/HeroPassport.Web.Tests/HistoryProcessTests.cs`

**Acceptance:** Reuse the proven process harness patterns from `FinishQuestProcessTests.cs` / `LocalWebSecurityAcceptanceTests.cs`: isolated `HERO_PASSPORT_HOME`, explicit project root, deterministic Testing bootstrap/session, real Kestrel and SQLite.

- [ ] **Step 1: Write RED process tests**

At minimum create two tests:

1. `HistoryRequiresSessionAndUnseenProjectReadDoesNotPersistProject`
   - anonymous `/history` -> 401;
   - bootstrap browser session against initialized Hero Passport with current filesystem Project not yet in `projects`;
   - record `SELECT COUNT(*) FROM projects` before GET;
   - authenticated `/history` -> 200 empty state;
   - Project count unchanged.

2. `HistoryIsProjectScopedBoundedPrivateAndReadOnly`
   - seed canonical history for current Project and a foreign Project, including >25 current-project Quest rows, one finished report with Skills and one open Quest;
   - capture a durable snapshot/count tuple for `projects`, `quest_sessions`, `quest_reports`, `xp_events`, `mutation_receipts`, `app_settings` before GETs;
   - `/history` -> 200; exactly 25 current-Project history links; no foreign title; newest expected title present, oldest excluded;
   - current detail -> 200 and expected summary/attestation/skills;
   - malformed route -> 400;
   - canonical missing -> 404;
   - canonical foreign Project -> 404 with the same safe not-found presentation;
   - assert no workspace fingerprint, sandbox path, `request_id`, `args_hash`, `project_id`, or database path in rendered HTML;
   - after all GETs, durable snapshot/count tuple unchanged.

- [ ] **Step 2: Run RED process tests**

```bash
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj --filter HistoryProcessTests
```
Expected: FAIL until pages/service/read store are complete; if Tasks 1-4 already satisfy them, the tests should immediately PASS and serve as acceptance evidence rather than weakening assertions.

- [ ] **Step 3: Fix only acceptance-discovered correctness gaps**

No scope expansion. Any required production change must stay inside #46 contracts. If a schema/index/general API change appears necessary, stop and revise the design rather than adding it here.

- [ ] **Step 4: Run full local test matrix available in CI**

```bash
dotnet test tests/HeroPassport.Architecture.Tests/HeroPassport.Architecture.Tests.csproj
dotnet test tests/HeroPassport.Domain.Tests/HeroPassport.Domain.Tests.csproj
dotnet test tests/HeroPassport.Application.Tests/HeroPassport.Application.Tests.csproj
dotnet test tests/HeroPassport.Infrastructure.Tests/HeroPassport.Infrastructure.Tests.csproj
dotnet test tests/HeroPassport.Contract.Tests/HeroPassport.Contract.Tests.csproj
dotnet test tests/HeroPassport.App.Tests/HeroPassport.App.Tests.csproj
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj
```
Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add tests/HeroPassport.Web.Tests/HistoryProcessTests.cs
git commit -m "test(web): qualify bounded history reads"
```

---

### Task 6: Canonical docs, exact-head qualification, and review gate

**Files:**
- Modify: `docs/ROADMAP.md`
- Modify: `docs/ARCHITECTURE.md`
- Modify: `docs/SECURITY-PRIVACY.md`
- Modify: `docs/TESTING-QUALITY.md`
- Modify: `docs/DEPLOYMENT-MODES.md` only if its current Web route/surface description becomes stale.
- Modify: `docs/superpowers/specs/2026-09-16-hero-passport-0.2-e-web-history-design.md` status only after implementation is actually qualified.
- Update PR #47 body/title from design-only draft to implementation evidence.

- [ ] **Step 1: Update docs to repository truth only after Task 5 is GREEN**

Roadmap must move `bounded project/Quest history` from remaining to implemented. Architecture must state `Web -> Application -> IHeroPassportStateStore -> SqliteHeroPassportStateStore.History` and GET-only/no-write behavior. Security/privacy must document same-404 foreign/missing rule and forbidden fields. Testing must record fixed 25, selector, privacy and durable no-write acceptance.

- [ ] **Step 2: Run final exact-head GitHub CI**

Push the docs-final head and require the repository `build-test` workflow to complete successfully on that exact SHA.

- [ ] **Step 3: Require cross-platform qualification**

Require `release-platform` success on Windows Server 2025 and macOS 15, including publish and packaged vertical E2E, for the same exact head.

- [ ] **Step 4: Mark PR Ready and request final independent Codex review**

Comment:
```text
@codex review

Please review exact head <SHA> for #46. Focus on current-Project scoping, hard limit/order, missing-vs-foreign 404 equivalence, durable no-write behavior, privacy surface, UUIDv7 selector validation before persistence access, and ensuring Web owns no SQL/EF/game rules. Report only findings that still apply to this exact head.
```
Resolve only findings actually fixed and requalify if head changes.

- [ ] **Step 5: Merge only after all gates are current-head GREEN**

Merge with expected head SHA. Verify `main` points to the merge commit, #46 closes through PR metadata, and the post-merge push CI is fully GREEN before declaring 0.2-E complete.
