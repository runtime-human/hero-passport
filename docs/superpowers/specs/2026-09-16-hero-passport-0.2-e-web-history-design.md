# Hero Passport 0.2-E — bounded Web Project / Quest history

**Status:** design awaiting approval; no implementation yet  
**Issue:** #46  
**Baseline:** `main@d380efeea503af743b94572657726e7b8abdf1de`  
**Branch:** `feat/0.2-e-web-history`

## 1. Purpose

0.2-E adds the first dedicated browser history surface to `HeroPassport.Web`.

The slice is intentionally read-only and bounded. It exposes recent Quest history for the Web process's current Project and a detail page for one Quest, while preserving the existing architecture in which Application owns product contracts, Infrastructure owns persistence, and Web is only an authenticated presentation adapter.

The user-visible contract is:

1. open `/history` from the authenticated local Web UI;
2. see at most the 25 most recent Quests for the current Project, newest first;
3. open one Quest detail page through `/history/{questId}`;
4. see bounded Quest/report facts already persisted by Start/Finish;
5. return to the dashboard/history without any storage mutation.

0.2-E does not add history mutation, arbitrary search/filtering, pagination protocol, general REST, Web-owned SQL/EF access, or progression-detail screens that belong to later roadmap slices.

## 2. Repository truth and constraints

The starting repository already has:

- ASP.NET Core 10 / Blazor Web App static SSR;
- the qualified 0.2-B local-browser session/authorization boundary;
- Start and Finish Web mutations qualified through 0.2-C/D;
- `HeroPassportApplication` as the Application facade;
- `IHeroPassportStateStore` as the Application persistence boundary;
- SQLite/EF Core 10 in Infrastructure;
- canonical persisted Quest history in `quest_sessions`, `quest_reports`, `quest_report_skills`, reward/trust/milestone history, and existing Project/Hero tables;
- `HeroPassportDataExport`, which already proves the database contains sufficient Quest/report history without a new schema;
- architecture tests forbidding Web persistence ownership and additional general HTTP surfaces;
- privacy rules forbidding raw source/diff/log/prompt/secrets/full paths/Git remotes from product presentation.

0.2-E must preserve all of these boundaries.

No migration or new persisted projection is planned. A schema change is allowed only if RED evidence proves the existing canonical history cannot satisfy the approved bounded contract; that would require a design revision before implementation continues.

## 3. Alternatives considered

### A. Selected: Application-owned bounded list + detail read use cases

Add explicit Application/store read contracts for current-Project Quest history and one current-Project Quest detail. Infrastructure performs purpose-built no-tracking queries; Web maps results into dedicated presentation models.

Advantages:

- preserves `Web -> Application -> Infrastructure` ownership;
- no direct DbContext/SQL in Web;
- no duplicate game rules;
- current Project binding is enforced below presentation;
- fixed list bound avoids premature paging/filter/search API design;
- no schema change;
- straightforward real-process no-write qualification.

### B. Reuse the portable export document in Web

`HeroPassportDataExport` can reconstruct rich history, but it is an offline portability boundary, not a product read model. Reusing it would couple Web to export-specific shape, path/file semantics, and more data than the UI needs. Rejected.

### C. Query SQLite directly from Web

Mechanically simple but violates the repository architecture and creates a second persistence adapter in presentation. Rejected.

### D. Add full paging/filter/search now

This would require query parameters, cursor/offset semantics, result-count policy, additional validation, larger UX scope and more performance/security design before there is evidence that 25 recent Quests is insufficient. Rejected for 0.2-E.

## 4. Application contracts

Add immutable Application result models dedicated to history. Names may be adjusted during implementation, but the semantic contract is fixed.

### Project history list

```text
ProjectQuestHistoryResult
  ProjectDisplayName
  Items[0..25]

ProjectQuestHistoryItem
  QuestId
  HeroName
  QuestType
  Title
  Status            # open | finished
  Result?           # null for open
  XpGained?         # null for open
  StartedAtUtc
  FinishedAtUtc?
```

Application entry point:

```text
Task<ProjectQuestHistoryResult> GetProjectQuestHistoryAsync(
    ProjectBindingContext project,
    CancellationToken cancellationToken = default)
```

The bound is not caller-controlled: Application/store always returns at most 25 rows.

### Quest detail

```text
QuestHistoryDetailResult
  QuestId
  HeroName
  ProjectDisplayName
  QuestType
  Title
  Goal
  Locale
  Status
  StartedAtUtc
  FinishedAtUtc?
  Report?

QuestHistoryReport
  Result
  Summary
  TestsMentioned
  ScopeViolations
  UserCorrections
  BuildStatus
  BuildEvidence
  TestsStatus
  TestsEvidence
  XpGained
  SkillsUsed[1..3]
```

Application entry point:

```text
Task<QuestHistoryDetailResult?> GetQuestHistoryDetailAsync(
    QuestId questId,
    ProjectBindingContext project,
    CancellationToken cancellationToken = default)
```

`null` means no Quest is visible under this Project binding. It deliberately does not distinguish "missing globally" from "exists under another Project".

Application validates `ProjectBindingContext` with the same existing authority as other Project-scoped operations. Web must not copy workspace-fingerprint validation.

## 5. Store boundary and Project ownership

Extend `IHeroPassportStateStore` with read-only methods matching the Application use cases.

The store resolves the supplied `ProjectBindingContext` through existing Project identity semantics. Web never supplies or receives internal `project_id`.

History list query requirements:

```text
scope: current resolved Project only
limit: 25
order: started_at_utc DESC, quest_id DESC
deterministic tie-break: canonical QuestId DESC
include: open and finished Quests
```

Quest detail requirements:

```text
predicate: quest_id = requested QuestId AND project = current resolved Project
result: zero or one row
```

Project scope is part of the query, not a post-query Web authorization check.

A Quest belonging to another Project therefore produces the same Application `null` result as a nonexistent Quest.

## 6. Read-only persistence implementation

Infrastructure may use EF Core LINQ or carefully parameterized existing persistence infrastructure. The preferred implementation is EF Core no-tracking projection because the use case is read-only and the result is a dedicated immutable Application model.

Requirements:

- `AsNoTracking()` or equivalent no-tracking query behavior;
- project-filtered SQL generated by Infrastructure;
- projection only of fields required by the approved contract;
- no entity mutation, `SaveChanges`, Project creation, `last seen` update, receipt write or projection rebuild;
- no lazy loading;
- no unbounded child collections;
- report Skills ordered by persisted ordinal and bounded to the canonical 1..3 contract;
- list uses one bounded query shape and must not introduce N+1 per Quest;
- detail may use a bounded split/second query for report Skills if that is clearer and still constant-query-count.

Official EF Core guidance used by this design: read-only queries should avoid tracking overhead when no updates are saved, and efficient queries should project only required data.

Reference:
https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying

## 7. Routes and static SSR behavior

Add authenticated static-SSR GET pages only:

```text
GET /history
GET /history/{questId}
```

No form, antiforgery token, POST endpoint, query filter or client-side state is needed in this slice.

The existing local session middleware remains the authorization gate before product reads.

### History list

`/history` renders:

- current Project display name;
- zero-state when no Quests exist for this Project;
- at most 25 Quest rows;
- each row links only to `/history/{canonicalQuestId}`;
- newest-first order supplied by Application/store, not reinterpreted in Web.

Each row shows only:

```text
started time
finished time when present
Hero display name
Quest type
Quest title
status
result when finished
XP gained when finished
```

### Quest detail selector

Web parses the route selector before Application/store access.

Behavior:

```text
malformed / noncanonical / non-UUIDv7 questId -> 400
canonical UUIDv7 not visible in current Project -> 404
visible Quest -> 200
```

The selector is not authorization. Current-Project ownership is enforced again by the Application/store query.

ASP.NET Core 10 static SSR security guidance treats route/query/form input as untrusted and requires server-side validation before database use. The design follows that rule.

Reference:
https://learn.microsoft.com/en-us/aspnet/core/blazor/security/static-server-side-rendering?view=aspnetcore-10.0

## 8. Web presentation boundary

Web introduces dedicated presentation view models rather than passing Application records directly to Razor components.

The Web history service/orchestrator is allowed to:

- obtain the existing current `ProjectBindingContext`;
- call Application history methods;
- parse the route QuestId;
- map Application results to bounded display models;
- map invalid/missing results to bounded HTTP status behavior.

It is not allowed to:

- reference EF Core/SQLite;
- know internal Project IDs;
- execute SQL;
- calculate XP/reward/progression/rank/traits/titles;
- derive ownership from active-Hero preference;
- mutate settings/project/Quest state;
- expose persistence metadata.

## 9. Detail presentation

For an open Quest, detail renders:

```text
Hero
Project
Quest type
Title
Goal
Started
Status = open
```

No fake Finish/report values are synthesized.

For a finished Quest, detail additionally renders:

```text
Finished
Result
Summary
XP gained
Tests mentioned
Scope violations
User corrections
Build status / evidence
Tests status / evidence
Skills used (1..3)
```

0.2-E deliberately does not render:

- reward component decomposition;
- before/after Hero XP/level/rank;
- Trust/Strain component decomposition;
- streak change;
- milestone/trait/title detail;
- full Skill progression snapshots;
- rule-version internals.

Those facts remain persisted and may be surfaced by later dedicated progression/detail slices. Keeping them out of 0.2-E prevents this history slice from absorbing subsequent roadmap items.

## 10. Privacy surface

The following values must never appear in history Application/Web DTOs or rendered product HTML:

```text
WorkspaceFingerprint
IdentityVersion
internal ProjectId
full repository/workspace path
Git remote URL
mutation request IDs
receipt IDs
args hashes
installation salt
bootstrap/session capabilities
SQLite path
raw source/diff/log/prompt/evidence payload
```

Allowed display values are already bounded product facts:

```text
Hero display name
Project display name
Quest title/type/goal
Quest locale/status/result/summary
bounded attestation values
Skill keys
XP gained
UTC timestamps
canonical QuestId in the detail route/link
```

Error responses must not reveal whether a canonical QuestId exists under another Project.

## 11. No-write invariant

History GETs are observational only.

Acceptance must prove that repeated list/detail requests do not change durable state. At minimum, before/after assertions cover counts or canonical snapshots for:

```text
projects
quest_sessions
quest_reports
xp_events / progression history
mutation_receipts
app_settings/settings_version
```

If the existing store currently creates a Project row merely by resolving Project binding, history must not reuse that mutating path. A dedicated read-only Project lookup must return an empty history / not-found detail when the Project has never been persisted.

This is a hard requirement: visiting `/history` in a valid repository with no Hero Passport Project row must not create that row.

## 12. Empty/uninitialized/setup behavior

The browser security/session boundary remains unchanged.

Product behavior:

- storage not initialized / setup incomplete: follow the existing bounded setup-required Web behavior rather than initializing storage;
- valid setup but current Project has never been persisted: `/history` returns 200 with an empty history and safe current Project display name; detail returns 404;
- persisted Project with no Quests: empty history;
- open Quests are included alongside finished Quests.

No history read may implicitly bootstrap, configure, create a Hero, create a Project, or start a Quest.

## 13. Time and ordering semantics

Persistence UTC timestamps remain authoritative facts.

0.2-E does not introduce timezone preference storage. Razor may display the canonical UTC value in a readable invariant/localized presentation, but ordering always uses persisted UTC values in Infrastructure.

Stable ordering is:

```text
started_at_utc DESC
QuestId DESC
```

The second key exists only for deterministic order if timestamps collide. Web does not reorder results.

## 14. Performance bounds

The list has a hard maximum of 25 rows and no user-controlled expansion.

Expected database behavior:

- no full in-memory table load before `Take(25)`;
- Project predicate is applied in SQL;
- ordering and `Take(25)` are applied in SQL;
- only display fields are projected;
- constant query count;
- detail returns at most one Quest/report plus at most three Skill rows.

No new index is planned initially because this is a local single-user database and the slice is deliberately bounded. If query-plan evidence shows the existing Project/Quest indexes are insufficient, index work requires explicit evidence and a focused schema decision rather than speculative migration churn.

## 15. Navigation

Add a `History` entry point to the authenticated dashboard.

Do not redesign dashboard navigation in 0.2-E. The minimal change is one clear link/button to `/history` placed with the existing Start/Finish product actions.

History detail provides a link back to `/history`; history provides a link back to `/`.

## 16. Error and status mapping

Use bounded browser responses:

```text
401  existing session authorization failure
400  malformed/noncanonical/non-v7 route QuestId
404  canonical QuestId not visible in current Project
500  unexpected internal failure through existing safe error handling
```

No new 409/410/429 semantics are needed because this slice has no pending state or mutation.

Error pages/messages must not echo workspace fingerprints, paths, internal IDs, SQL, exception text or foreign-Project existence.

## 17. Tests — RED to GREEN

Implementation follows TDD.

### Application tests

Add RED tests proving:

- Application validates Project binding before store history calls;
- list delegates to the bounded history read contract;
- detail preserves canonical Quest selector and Project binding;
- Web is not required to know internal Project identity.

### Infrastructure integration tests

Using real SQLite, prove:

- history returns only current-Project Quests across Heroes;
- newest-first deterministic order;
- exactly the newest 25 of >25 rows;
- open + finished rows map correctly;
- detail returns report/attestation/Skills for finished Quest;
- detail returns open shape without synthetic report;
- foreign-Project Quest returns null;
- unknown Quest returns null;
- unseen Project lookup does not create Project storage;
- repeated reads create no writes;
- query result contains no forbidden persistence/private material.

### Web unit/static tests

Prove:

- exact routes exist;
- history Web models contain only approved fields;
- no EF/SQLite/persistence references;
- no hidden mutation forms or POST handlers;
- canonical QuestId parse occurs before history detail Application call;
- malformed selector -> 400;
- canonical unavailable selector -> 404;
- dashboard exposes the History entry point.

### Real-process Web acceptance

Against real Kestrel + SQLite:

1. anonymous `/history` -> existing 401 behavior;
2. authenticated empty current Project -> 200 empty history with no Project row created;
3. create Quests in two Projects and multiple Heroes;
4. `/history` exposes only current Project rows;
5. >25 current-Project Quests -> only newest 25 rendered;
6. open and finished rows render bounded facts;
7. valid current-Project detail -> 200;
8. malformed detail selector -> 400;
9. canonical missing selector -> 404;
10. canonical foreign-Project selector -> indistinguishable 404;
11. rendered pages contain no fingerprint/path/request/receipt/hash leakage;
12. before/after durable counts/snapshots prove GET requests did not mutate storage.

Existing Start/Finish/security/idempotency/process tests must remain GREEN.

## 18. Architecture guards

Existing architecture tests remain authoritative and should be extended only where they materially prevent regression.

At minimum prove:

- Web still has no EF Core/SQLite package/reference or persistence namespace ownership;
- history is GET-only and does not add a general API/REST route;
- Application models do not reference Infrastructure types;
- Infrastructure implements the store read methods;
- no Web DTO exposes `WorkspaceFingerprint`, internal Project IDs, request IDs or args hashes.

## 19. Documentation changes after implementation

After real implementation qualification, update only docs whose repository truth changed:

- `docs/ROADMAP.md` — mark bounded Project/Quest history implemented and remove it from remaining slices;
- `docs/ARCHITECTURE.md` — add the read-only history flow;
- `docs/SECURITY-PRIVACY.md` — document history selector/scoping/no-write/privacy behavior;
- `docs/TESTING-QUALITY.md` — add list/detail/no-write/foreign-Project acceptance;
- `docs/DEPLOYMENT-MODES.md` only if route inventory is maintained there.

No broad docs churn.

## 20. Explicit non-goals

0.2-E does not add:

- arbitrary pagination/cursors;
- search/filter/sort controls;
- project switching;
- Hero management;
- settings mutation;
- Skill progression detail;
- Rank/Traits/Titles detail;
- reward/trust component detail;
- timeline visualization/charts;
- export/download from Web;
- deletion/editing/reopen Quest actions;
- public/LAN/reverse-proxy hosting;
- Identity/OAuth/accounts;
- Streamable HTTP MCP;
- REST/GraphQL API;
- Interactive Server or WebAssembly.

## 21. Official framework references

The implementation must be checked against current official documentation rather than memory.

ASP.NET Core 10 static SSR security / untrusted binding and server-side validation:
https://learn.microsoft.com/en-us/aspnet/core/blazor/security/static-server-side-rendering?view=aspnetcore-10.0

ASP.NET Core 10 Blazor project/routable page structure:
https://learn.microsoft.com/en-us/aspnet/core/blazor/project-structure?view=aspnetcore-10.0

ASP.NET Core 10 release notes, including static-SSR `NavigationManager.NotFound` behavior:
https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0

EF Core efficient querying / no-tracking read guidance:
https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying

## 22. Completion gate

#46 is complete only when all of the following are true on one exact implementation head:

- approved design remains satisfied or all deviations are explicitly documented and approved;
- no schema change unless separately justified by RED evidence;
- fixed 25-row current-Project list and current-Project detail implemented through Application/store contracts;
- no-write invariant independently proven;
- foreign-Project and missing Quest are indistinguishable at Web detail boundary;
- privacy-surface tests are GREEN;
- existing Start/Finish/browser-security suites are GREEN;
- architecture suite is GREEN;
- full Linux `ci` is GREEN;
- Windows Server 2025 and macOS 15 `release-platform` jobs are GREEN including packaged E2E;
- final independent exact-head review has no unresolved findings;
- PR merges through repository governance;
- issue #46 closes through merge;
- post-merge push CI on the resulting exact `main` SHA is fully GREEN.

Only after those conditions does canonical roadmap wording change from planned history to implemented history.
