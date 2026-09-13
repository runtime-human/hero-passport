# Hero Passport 0.2-C Web Start Quest Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the first authenticated Web mutation vertical: prepare, review, explicitly confirm, and start one Quest for the prepared Hero/current Project without weakening the 0.2-B browser security boundary.

**Architecture:** Keep Blazor static SSR. Add a pure Application preparation seam so Web can display exactly normalized Start values before mutation; keep `HeroPassportApplication.StartQuestAsync(...)` as mutation authority. Web owns only process-local prepared confirmation state, SSR form orchestration, bounded error mapping, and request-size/form hardening.

**Tech Stack:** C# 14, .NET 10 LTS, ASP.NET Core 10 Blazor static SSR, xUnit, SQLite via the existing Infrastructure adapter. No new NuGet packages.

**Spec:** `docs/superpowers/specs/2026-09-13-hero-passport-0.2-c-web-start-quest-design.md`

## Global Constraints

- Exact baseline: `main@ededcb5a44e822533dc3b00f28d322b5eca67d33`.
- Preserve `HeroPassport.Web -> Application + Infrastructure`; no direct persistence/DbContext access from Web presentation/services.
- Preserve 0.2-B loopback-only listener, `127.0.0.1` Host Filtering, browser session middleware, same-origin policy and `UseAntiforgery()`.
- Static SSR only: no Interactive Server/WebAssembly.
- No general REST/minimal-API product surface; `MapPost` remains allowed only for the existing bootstrap security endpoint.
- `StartQuestAsync` remains the only store-backed Start mutation authority.
- Dedicated form DTOs only; never bind Domain/Application records from request form data.
- Form names are unique: `StartQuestPrepare` and `StartQuestConfirm`.
- Start/confirm POST body ceiling: 8192 bytes before antiforgery/form parsing.
- `RazorComponentsServiceOptions`: collection size 16, recursion depth 4, error count 16, key size 128.
- Prepared confirmation store: process-local, max 8 entries, 10-minute TTL, cryptographic opaque handle >=128 bits.
- Confirmation handle is not authentication; session + same-origin + antiforgery remain required.
- Preserve one generated `MutationRequestId` from prepare through every retry/commit attempt.
- Do not put Quest title/goal into redirect/query URLs, ordinary logs, error bodies, or telemetry.
- Explicit non-goals: Finish Quest, history, Hero/settings management, new RPG rules, public/LAN/HTTPS/reverse-proxy hosting, Streamable HTTP MCP, broad UI redesign.

---

### Task 1: Add a pure Application Start preparation seam

**Files:**
- Modify: `src/HeroPassport.Application/Runtime/StartQuestModels.cs`
- Modify: `src/HeroPassport.Application/Runtime/HeroPassportApplication.cs`
- Test: `tests/HeroPassport.Application.Tests/StartQuestPreparationTests.cs`

**Interfaces:**
- Produces: `PreparedStartQuest` with `StartRequestId`, `HeroId`, normalized `QuestType`, normalized `Title`, normalized `Goal`.
- Produces: `HeroPassportApplication.PrepareStartQuest(StartQuestRequest request, ProjectBindingContext project)`.
- Existing `StartQuestAsync(StartQuestRequest, ProjectBindingContext, CancellationToken)` delegates validation to the same preparation method before constructing `StartQuestStoreCommand`.

- [ ] **Step 1: Write RED tests for normalization and no-store preparation**

Create `StartQuestPreparationTests.cs` that instantiates `HeroPassportApplication` with a spy `IHeroPassportStateStore` whose mutation methods throw if called. Cover:

```csharp
[Fact]
public void PrepareStartQuestNormalizesExactlyOnceWithoutStoreMutation()
{
    var requestId = MutationRequestId.New();
    var heroId = HeroId.New();
    var application = new HeroPassportApplication(new ThrowOnStoreAccess(), TimeProvider.System);
    var project = new ProjectBindingContext("Demo", new string('a', 64), "project-identity/1");

    var prepared = application.PrepareStartQuest(
        new StartQuestRequest(requestId, heroId, "coding", "  Build   parser  ", "  Ship   bounded   parser  "),
        project);

    Assert.Equal(requestId, prepared.StartRequestId);
    Assert.Equal(heroId, prepared.HeroId);
    Assert.Equal("coding", prepared.QuestType);
    Assert.Equal("Build parser", prepared.Title);
    Assert.Equal("Ship bounded parser", prepared.Goal);
}
```

Also assert invalid quest type/text/project throw the same existing `HP110`/`HP300`/`HP310` semantics as `StartQuestAsync` and that preparation never calls `IHeroPassportStateStore`.

- [ ] **Step 2: Run the focused Application test and verify RED**

Run:

```bash
dotnet test tests/HeroPassport.Application.Tests/HeroPassport.Application.Tests.csproj --filter FullyQualifiedName~StartQuestPreparationTests
```

Expected: compile/test failure because `PreparedStartQuest` / `PrepareStartQuest` do not exist.

- [ ] **Step 3: Add the minimal model and refactor validation**

Add to `StartQuestModels.cs`:

```csharp
public sealed record PreparedStartQuest(
    MutationRequestId StartRequestId,
    HeroId HeroId,
    string QuestType,
    string Title,
    string Goal);
```

Refactor `HeroPassportApplication`:

```csharp
public PreparedStartQuest PrepareStartQuest(
    StartQuestRequest request,
    ProjectBindingContext project)
{
    ArgumentNullException.ThrowIfNull(request);
    _ = ValidateProject(project);
    return new PreparedStartQuest(
        request.StartRequestId,
        request.HeroId,
        RequireQuestType(request.QuestType),
        NormalizeRequestText(request.Title, 1, 120, "title"),
        NormalizeRequestText(request.Goal, 1, 500, "goal"));
}
```

Change `StartQuestAsync` to call `PrepareStartQuest`, validate the Project once for the store command, and use the prepared values. Do not introduce a second validation implementation.

- [ ] **Step 4: Run focused + existing Start Application tests**

```bash
dotnet test tests/HeroPassport.Application.Tests/HeroPassport.Application.Tests.csproj --filter "FullyQualifiedName~StartQuest"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/HeroPassport.Application/Runtime/StartQuestModels.cs src/HeroPassport.Application/Runtime/HeroPassportApplication.cs tests/HeroPassport.Application.Tests/StartQuestPreparationTests.cs
git commit -m "feat(application): add start quest preparation seam"
```

---

### Task 2: Add bounded process-local pending confirmation state

**Files:**
- Create: `src/HeroPassport.Web/Services/PendingStartQuestStore.cs`
- Test: `tests/HeroPassport.Web.Tests/PendingStartQuestStoreTests.cs`

**Interfaces:**
- Consumes: `PreparedStartQuest` from Task 1.
- Produces: Web-owned `PendingStartQuest`/`PendingStartQuestResult` presentation records.
- Produces a concurrency-safe store with insert/lookup/claim/complete/release semantics.

- [ ] **Step 1: Write RED unit tests for TTL/capacity/concurrency**

Cover:

```text
Insert -> handle is non-empty bounded base64url and lookup returns exact prepared state
9th insert with 8 live entries evicts oldest pending entry only
entry older than 10 minutes returns expired/stale and is removed
malformed/unknown handle never throws and never returns a command
first TryClaim(handle) succeeds; concurrent second claim reports Busy
Complete(handle, result) makes duplicate confirm return same safe committed result without a second Application call
ReleaseAfterRetryableFailure(handle) returns committing -> pending without generating a new StartRequestId
```

Use a deterministic `TimeProvider` test double rather than wall-clock sleeps.

- [ ] **Step 2: Run focused Web unit test and verify RED**

```bash
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj --filter FullyQualifiedName~PendingStartQuestStoreTests
```

Expected: compile failure because the store does not exist.

- [ ] **Step 3: Implement minimal store**

Use `RandomNumberGenerator.GetBytes(16)` and base64url encoding for handles. Store at most 8 entries. Use a private lock around a dictionary/list and explicit states:

```csharp
internal enum PendingStartQuestState { Pending, Committing, Committed }
```

Do not store browser-session secrets or persist entries outside process memory.

- [ ] **Step 4: Run focused tests**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/HeroPassport.Web/Services/PendingStartQuestStore.cs tests/HeroPassport.Web.Tests/PendingStartQuestStoreTests.cs
git commit -m "feat(web): add pending start quest store"
```

---

### Task 3: Add Start Quest orchestration service with stable ownership/idempotency

**Files:**
- Create: `src/HeroPassport.Web/Services/HeroPassportStartQuestService.cs`
- Test: `tests/HeroPassport.Web.Tests/StartQuestServiceTests.cs`

**Interfaces:**
- Consumes: `HeroPassportApplication`, process `ProjectBindingContext`, `PendingStartQuestStore`.
- Produces:

```csharp
Task<StartQuestPageModel> LoadAsync(CancellationToken cancellationToken = default);
Task<PrepareStartQuestWebResult> PrepareAsync(StartQuestForm input, CancellationToken cancellationToken = default);
Task<ConfirmStartQuestWebResult> LoadConfirmationAsync(string handle, CancellationToken cancellationToken = default);
Task<CommitStartQuestWebResult> CommitAsync(string handle, CancellationToken cancellationToken = default);
```

- Web input DTO contains only `QuestType`, `Title`, `Goal`.
- Prepared state captures Hero ID/name and Project display name at prepare time.

- [ ] **Step 1: Write RED service tests**

Cover setup incomplete, active Hero missing, existing current-Hero open Quest, normalized prepare result, no mutation during prepare, active-Hero preference changes after prepare do not retarget ownership, `HP133` maps to safe conflict, `HP140`/`HP141` map to stale-Hero conflict, duplicate committed handle returns the same safe success result.

Use a fake `IHeroPassportStateStore` or existing test runtime pattern; assert the exact `HeroId` and `StartRequestId` reaching the mutation.

- [ ] **Step 2: Verify RED**

```bash
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj --filter FullyQualifiedName~StartQuestServiceTests
```

- [ ] **Step 3: Implement orchestration only**

`PrepareAsync`:

```text
GetRuntimeContextAsync(project)
require setup + ActiveHero
reject if context.OpenQuests contains ActiveHero.HeroId
requestId = MutationRequestId.New()
prepared = application.PrepareStartQuest(...)
store.Insert(prepared, activeHero.Name, context.Project.DisplayName)
return opaque handle
```

`CommitAsync` atomically claims the pending entry and constructs a `StartQuestRequest` from the stored prepared values. It must not read `ActiveHero` to choose ownership. Call only existing `StartQuestAsync`. Map known `HeroPassportException.Code` values to bounded Web statuses/messages; never render raw exception messages.

- [ ] **Step 4: Verify GREEN**

Run the focused tests plus existing dashboard tests.

- [ ] **Step 5: Commit**

```bash
git add src/HeroPassport.Web/Services/HeroPassportStartQuestService.cs tests/HeroPassport.Web.Tests/StartQuestServiceTests.cs
git commit -m "feat(web): orchestrate confirmed quest start"
```

---

### Task 4: Add pre-antiforgery mutation request bounds

**Files:**
- Create: `src/HeroPassport.Web/Security/MutationRequestBoundaryMiddleware.cs`
- Modify: `src/HeroPassport.Web/Program.cs`
- Test: `tests/HeroPassport.Web.Tests/MutationRequestBoundaryTests.cs`

**Interfaces:**
- Applies only to POST requests for `/quests/start` and `/quests/start/confirm/*`.
- Rejects non-`application/x-www-form-urlencoded` as 415.
- Rejects `Content-Length > 8192` as 413.
- Before body read sets `IHttpMaxRequestBodySizeFeature.MaxRequestBodySize = 8192` and a bounded `IFormFeature`/`FormOptions`.
- Existing bootstrap keeps its dedicated 1024-byte settings.

- [ ] **Step 1: Write RED middleware tests**

Use `DefaultHttpContext` or real-process tests to prove:

```text
multipart/form-data -> 415 before next delegate
known Content-Length 8193 -> 413 before next delegate
valid small urlencoded request reaches next delegate
non-POST and unrelated routes are unchanged
bootstrap route is not intercepted by this middleware
```

- [ ] **Step 2: Verify RED**

```bash
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj --filter FullyQualifiedName~MutationRequestBoundaryTests
```

- [ ] **Step 3: Implement and wire before `UseAntiforgery()`**

Program order remains:

```csharp
app.UseHostFiltering();
app.UseMiddleware<BootstrapResponseHeadersMiddleware>();
app.UseMiddleware<LocalWebSessionMiddleware>();
app.UseMiddleware<MutationRequestBoundaryMiddleware>();
app.UseAntiforgery();
```

Tighten Razor form mapper:

```csharp
builder.Services.AddRazorComponents(options =>
{
    options.MaxFormMappingCollectionSize = 16;
    options.MaxFormMappingRecursionDepth = 4;
    options.MaxFormMappingErrorCount = 16;
    options.MaxFormMappingKeySize = 128;
});
```

Do not change bootstrap limits.

- [ ] **Step 4: Verify middleware + existing 0.2-B security tests**

```bash
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj --filter "FullyQualifiedName~MutationRequestBoundaryTests|FullyQualifiedName~BootstrapRequestBoundaryTests|FullyQualifiedName~LocalWebSecurity"
```

- [ ] **Step 5: Commit**

```bash
git add src/HeroPassport.Web/Security/MutationRequestBoundaryMiddleware.cs src/HeroPassport.Web/Program.cs tests/HeroPassport.Web.Tests/MutationRequestBoundaryTests.cs
git commit -m "security(web): bound quest mutation forms"
```

---

### Task 5: Add static-SSR Start and confirmation pages

**Files:**
- Create: `src/HeroPassport.Web/Components/Pages/StartQuest.razor`
- Create: `src/HeroPassport.Web/Components/Pages/ConfirmStartQuest.razor`
- Modify: `src/HeroPassport.Web/Components/Pages/Home.razor`
- Modify: `src/HeroPassport.Web/Components/_Imports.razor` only if needed for forms/DTO namespace.
- Modify: `src/HeroPassport.Web/Program.cs` to register Start services/store.
- Modify: `src/HeroPassport.Web/wwwroot/app.css`
- Test: `tests/HeroPassport.Web.Tests/StartQuestStaticSsrTests.cs`

**Interfaces:**
- `StartQuest.razor` route `/quests/start`, `FormName="StartQuestPrepare"`, dedicated nullable `StartQuestForm?` supplied from form.
- `ConfirmStartQuest.razor` route `/quests/start/confirm/{Handle}`, `FormName="StartQuestConfirm"`; confirm POST submits only framework form metadata (route supplies handle).
- No hidden title/goal/HeroId/requestId fields.

- [ ] **Step 1: Write RED source/SSR tests**

Assert source contains unique form names and dedicated DTO binding, and does not contain hidden title/goal/request IDs. Process test authenticated GET `/quests/start` should currently return 404.

- [ ] **Step 2: Verify RED**

Run focused Web tests.

- [ ] **Step 3: Implement Start page**

Use official static-SSR initialization pattern:

```csharp
[SupplyParameterFromForm(FormName = "StartQuestPrepare")]
private StartQuestForm? Input { get; set; }

protected override async Task OnInitializedAsync()
{
    Input ??= new();
    _page = await StartQuest.LoadAsync();
}
```

On submit, call `PrepareAsync`; successful prepare navigates only to `/quests/start/confirm/{opaqueHandle}`. Validation errors remain on page with bounded messages.

- [ ] **Step 4: Implement confirmation page**

Load prepared state by handle on GET/POST. Render safe Hero/Project/type/title/goal. On valid confirm submit call `CommitAsync`; success redirects to `/`. Expired/malformed handles render stale confirmation UI and never mutate.

- [ ] **Step 5: Add dashboard entry point**

Show a `Start Quest` link only when configured and the active Hero has no open Quest in current Project. Do not add Finish controls.

- [ ] **Step 6: Verify focused page tests**

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/HeroPassport.Web/Components src/HeroPassport.Web/Program.cs src/HeroPassport.Web/wwwroot/app.css tests/HeroPassport.Web.Tests/StartQuestStaticSsrTests.cs
git commit -m "feat(web): add confirmed start quest pages"
```

---

### Task 6: Prove the mutation vertical in real Web processes

**Files:**
- Create: `tests/HeroPassport.Web.Tests/StartQuestProcessTests.cs`
- Modify existing Web test helpers only when reuse clearly reduces duplicated process/bootstrap setup.

**Interfaces:**
- Reuse Testing-only deterministic bootstrap/session secrets and `--no-open-browser` from 0.2-B.
- Use real HTTP requests against `HeroPassport.Web` with a temporary Hero Passport data directory/project root.

- [ ] **Step 1: Add RED/acceptance process tests before changing production behavior further**

Cover end-to-end:

```text
unauthenticated GET /quests/start -> 401
GET after bootstrap -> 200 and no quest/project-stat write
prepare POST without antiforgery -> 400/no mutation
cross-site prepare POST -> 400/no mutation
oversized prepare -> 413/no mutation
multipart prepare -> 415/no mutation
valid prepare -> redirect to opaque confirmation URL; DB still has zero open Quest
confirmation HTML shows exact normalized Hero/Project/type/title/goal but no internal HeroId/fingerprint/requestId/session/bootstrap token
confirm POST -> redirect / and exactly one open Quest
GET / after confirm shows new Quest
re-post same confirmation -> no second Quest and bounded success/stale semantics
prepare Hero A, switch active Hero through CLI/store-supported surface, confirm -> Quest remains owned by Hero A
competing Start before confirm -> confirm surfaces safe HP133 conflict and does not duplicate
stale/unknown handle -> 410/no mutation
logs/errors/Location never contain title/goal except authenticated page body where intentionally rendered
```

Use HTML parsing already available in tests only if existing dependencies support it; otherwise use bounded string extraction for antiforgery hidden fields as current tests do. Do not add a parser dependency solely for this slice.

- [ ] **Step 2: Run process tests**

```bash
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj --filter FullyQualifiedName~StartQuestProcessTests
```

Fix only defects within #40 scope.

- [ ] **Step 3: Run all Web tests**

```bash
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj
```

Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add tests/HeroPassport.Web.Tests
git commit -m "test(web): qualify confirmed quest start"
```

---

### Task 7: Update architecture guards for a mutation-capable static-SSR Web adapter

**Files:**
- Modify: `tests/HeroPassport.Architecture.Tests/ProjectDependencyTests.cs`

**Interfaces:**
- Preserve no Web package additions and no direct persistence tokens in Components/Services.
- Preserve the rule that the only explicit `MapPost(` in Web source is `Security/BootstrapEndpoint.cs`.
- Preserve Identity/CORS/interactive/reverse-proxy prohibitions.
- Update misleading test naming from `WebReadOnlyPresentation...` to reflect that Web now has a bounded mutation path through Application.

- [ ] **Step 1: Add/adjust architecture assertions**

Add guards that Start components/services do not reference `HeroPassport.Infrastructure.Persistence`, `DbContext`, `SqliteConnection`, or create product `MapPost` endpoints. Assert both form names occur exactly once in their intended page sources.

- [ ] **Step 2: Run architecture tests**

```bash
dotnet test tests/HeroPassport.Architecture.Tests/HeroPassport.Architecture.Tests.csproj
```

- [ ] **Step 3: Commit**

```bash
git add tests/HeroPassport.Architecture.Tests/ProjectDependencyTests.cs
git commit -m "test(architecture): guard web mutation boundary"
```

---

### Task 8: Canonical docs and exact-head qualification

**Files:**
- Modify only if behavior is already GREEN: `docs/ARCHITECTURE.md`, `docs/DEPLOYMENT-MODES.md`, `docs/TESTING-QUALITY.md`, `docs/ROADMAP.md`, `docs/SECURITY-PRIVACY.md` where wording is actually stale.

**Interfaces:**
- Docs must say 0.2-C introduces only Start Quest Web mutation and explicit confirmation.
- Do not claim Finish/history/settings/published Web packaging exists.

- [ ] **Step 1: Run full local/CI-equivalent test commands available in the execution environment**

At minimum:

```bash
dotnet build HeroPassport.slnx --no-restore
dotnet test tests/HeroPassport.Architecture.Tests/HeroPassport.Architecture.Tests.csproj --no-build
dotnet test tests/HeroPassport.Domain.Tests/HeroPassport.Domain.Tests.csproj --no-build
dotnet test tests/HeroPassport.Application.Tests/HeroPassport.Application.Tests.csproj --no-build
dotnet test tests/HeroPassport.Infrastructure.Tests/HeroPassport.Infrastructure.Tests.csproj --no-build
dotnet test tests/HeroPassport.Contract.Tests/HeroPassport.Contract.Tests.csproj --no-build
dotnet test tests/HeroPassport.App.Tests/HeroPassport.App.Tests.csproj --no-build
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj --no-build
```

If execution is GitHub-only, branch CI is authoritative; do not claim local execution.

- [ ] **Step 2: Update canonical docs narrowly**

Document only evidenced behavior and test coverage.

- [ ] **Step 3: Commit docs**

```bash
git add docs/ARCHITECTURE.md docs/DEPLOYMENT-MODES.md docs/TESTING-QUALITY.md docs/ROADMAP.md docs/SECURITY-PRIVACY.md
git commit -m "docs: record web start quest boundary"
```

- [ ] **Step 4: Create/update focused PR closing #40**

PR body must contain exact base/head SHA, RED run evidence, GREEN run evidence, security/privacy invariants, non-goals, and `Closes #40`.

- [ ] **Step 5: Qualify exact PR head**

Require on the exact current head:

```text
ci / build-test: success
release-platform matrix: success when triggered/required
no unresolved blocking review threads
PR mergeable
main unchanged from expected merge base or branch updated/requalified
```

- [ ] **Step 6: Merge only with exact-head guard after fresh verification**

After merge, verify new exact `main`, automatic closure of #40, and post-merge CI before declaring 0.2-C complete.
