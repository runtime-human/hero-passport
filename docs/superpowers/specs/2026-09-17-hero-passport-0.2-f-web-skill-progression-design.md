# Hero Passport 0.2-F Web Skill Progression Design

**Status:** Accepted for implementation  
**Date:** 2026-09-17  
**Base:** `main@5d643ce2f621e7f40d0bcacfc79dd898aae6327c`

## Goal

Add an authenticated, read-only Web Skill progression surface over the existing deterministic Hero Passport progression state without expanding the compact MCP/Hero Card contract, introducing a second persistence stack, or changing the Skill economy.

## Scope

0.2-F adds one routable static-SSR page:

```text
GET /skills
```

The page presents the active Hero's complete canonical ten-Skill progression and the same Hero's current-Project contribution for each Skill.

Canonical Skill order is fixed and shared across the slice:

```text
coding
testing_awareness
scope_control
documentation
tool_use
planning
research
debugging
review
maintenance
```

Every canonical Skill is rendered, including zero-XP Skills. No paging, filtering, detail route, mutation, Skill equipment, history graph, localization redesign, Rank/Traits/Titles expansion, settings/Hero management, schema change, migration, index, REST endpoint, interactive render mode, or new browser authorization mechanism is part of 0.2-F.

## Architecture

The existing dependency direction remains authoritative:

```text
Domain <- Application <- Infrastructure <- Web
```

The Web route uses a dedicated service and presentation model:

```text
GET /skills
-> existing 0.2-B local browser session middleware
-> HeroPassportSkillProgressionService
-> GetRuntimeContextAsync(currentProject)
-> setup + active-Hero gate
-> HeroPassportApplication.GetSkillProgressionAsync(activeHeroId, currentProject)
-> IHeroPassportStateStore.GetSkillProgressionAsync(...)
-> SqliteHeroPassportStateStore.SkillProgression
-> bounded Web view model
-> Blazor static SSR
```

`HeroCardResult` remains unchanged. Its existing `TopSkills` contract remains a compact top-three projection for MCP/dashboard consumers. Web does not broaden that contract to satisfy this page.

## Domain progression semantics

`skill-progression/2.0.0` thresholds and level-cap behavior do not change.

The current `NextLevelXpRequired(level, version)` value is the width of the current level band, not remaining XP. The Domain therefore gains the same explicit level-band progress primitive already present for Hero progression:

```csharp
SkillProgressionRules.LevelXp(long totalXp, int level, string ruleVersion)
```

Semantics:

```text
LevelXp = totalXp - threshold_for_current_level
```

At Skill Level 10, XP continues accumulating. `IsLevelCapped=true`, `NextLevelXpRequired=null`, and `LevelXp` remains the XP accumulated above the Level-10 threshold. No rule version changes because thresholds, XP allocation, level mapping and externally persisted game outcomes are unchanged; this is an additional deterministic read helper.

## Application read contract

Add a dedicated Application model instead of altering Hero Card:

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

Application exposes:

```csharp
Task<SkillProgressionResult> GetSkillProgressionAsync(
    HeroId heroId,
    ProjectBindingContext project,
    CancellationToken cancellationToken = default);
```

Application validates the Project binding exactly as the existing card/history use cases do and delegates to the store. It owns no SQL and performs no Web presentation mapping.

## Persistence/read semantics

Use the existing `SqliteHeroPassportStateStore` and `Microsoft.Data.Sqlite` connection policy. EF Core remains schema/migration authority; no second read framework is introduced.

The operation is observational only and uses one short deferred read transaction so Hero identity, Project resolution, global Skill totals and Project Skill totals come from one SQLite snapshot. The transaction executes SELECT statements only.

Global Skill XP comes from the rebuildable `hero_skills` projection for the selected Hero.

Current-Project Skill XP is derived from canonical Quest/report history:

```sql
SELECT report_skill.skill_key, SUM(report_skill.xp_gained)
FROM quest_sessions AS quest
JOIN quest_reports AS report ON report.quest_id = quest.id
JOIN quest_report_skills AS report_skill ON report_skill.quest_report_id = report.id
WHERE quest.hero_id = $hero
  AND quest.project_id = $project
GROUP BY report_skill.skill_key;
```

Project resolution uses the validated workspace fingerprint only to resolve the existing internal Project row. Once resolved, subsequent queries use internal `project_id`. If the Project has never been persisted, the result still returns the validated Project display name and ten zero-XP Project Skill snapshots without creating a Project row.

Only canonical catalog keys are emitted. Infrastructure materializes all ten rows in canonical order and fills absent persisted totals with zero. XP/level display values are derived using the current `HeroPassportVersions.CurrentRules.SkillProgression` rule version; Web never calculates progression rules.

No INSERT, UPDATE, DELETE, receipt operation, projection refresh, Project creation, commit observer or migration is allowed in the read path.

## Setup and Hero semantics

The Web service first calls `GetRuntimeContextAsync(currentProject)`.

```text
setup incomplete -> SetupRequired page state; no Skill read call
setup complete + no active Hero -> invalid product state; fail boundedly
setup complete + active Hero -> call GetSkillProgressionAsync(activeHeroId,...)
```

The selected Hero is the current active Hero because 0.2-F is a read/dashboard progression surface, not Hero management. Archived/non-active Hero browsing remains outside this slice.

The store still validates that the explicit Hero exists. It does not silently substitute the current active Hero.

## Web presentation

`HeroPassportSkillProgressionService` maps Application results to Web-only view models. Web models contain only:

```text
Hero display name
Project display name
Skill key
Hero XP / level / capped / level XP / level-band requirement
Project XP / level / capped / level XP / level-band requirement
```

They never contain workspace fingerprint, internal ProjectId, local paths, request IDs, receipt IDs, hashes, rule-version internals, SQL metadata, raw logs, source/diff data or secrets.

`/skills` is static SSR and GET-only. It reuses the existing local session middleware; no antiforgery or confirmation state applies because there is no mutation.

The page should make the two scopes explicit: `Hero` means all persisted progression for the active Hero; `Project` means XP contributed by completed Quests in the current Project for that Hero.

For uncapped Skills, progress is presented as `LevelXp / NextLevelXpRequired`. For capped Skills, show `Level 10 · capped` and total XP; do not fabricate a next-level target.

## Security and privacy

The existing 0.2-B browser boundary remains authoritative. An unauthenticated request to `/skills` must return 401 before Application/store product reads.

No new selector is exposed, so there is no new IDOR/404 distinction problem. The route has no route parameters.

The page remains local metadata and inherits the existing product warning that displayed Quest/Hero data can be sensitive. It does not add source surveillance or telemetry.

## Read consistency and provider constraints

Microsoft.Data.Sqlite 10 supports deferred transactions through `BeginTransaction(deferred: true)`. The implementation uses a short SELECT-only deferred transaction to maintain a consistent multi-query snapshot and commits/ends it without attempting a read-to-write upgrade.

SQLite has no true asynchronous I/O in Microsoft.Data.Sqlite; existing async Application/store signatures are retained for architectural consistency and cancellation-compatible contracts, not because the provider makes the underlying SQLite I/O asynchronous.

Official references:

- https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions
- https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlite.sqliteconnection.begintransaction?view=msdata-sqlite-10.0.0
- https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async
- https://learn.microsoft.com/en-us/aspnet/core/blazor/project-structure?view=aspnetcore-10.0

## Testing and acceptance

### Domain

Prove `SkillProgressionRules.LevelXp` at level starts, inside a level, threshold crossings, Level 10 cap and invalid inputs. Existing threshold, cap and JSON-safe tests stay green.

### Application/store contract

Prove Application validates Project binding and delegates the explicit HeroId. The new read contract must not modify existing Hero Card/MCP contracts.

### Real SQLite

Use file-backed SQLite to prove:

- exactly ten canonical Skill rows are returned in canonical order;
- zero-XP Skills are included;
- Hero/global totals include contributions from multiple Projects;
- Project totals include only completed Quest report Skill deltas for the selected Hero/current Project;
- another Hero's Project Skill XP is excluded;
- unseen Project returns ten zero Project totals without creating a Project row;
- progression values use Domain rules correctly;
- read transaction performs no durable mutation.

No query-plan/index migration is added without measured evidence.

### Web

Prove:

- setup-required state short-circuits before Skill read;
- configured active Hero maps all ten rows into privacy-bounded view models;
- `/skills` renders through static SSR and is linked from the dashboard/history navigation surface;
- unauthenticated `/skills` returns 401;
- authenticated `/skills` returns 200;
- rendered page includes all ten Skills and both Hero/Project scopes;
- no privacy deny-list values are rendered;
- before/after product-table snapshot is identical for the GET.

### Release gate

Before merge:

1. focused Domain/Application/Infrastructure/Web tests GREEN;
2. full Linux `ci` GREEN on the exact PR head;
3. Windows Server 2025 + macOS 15 `release-platform` GREEN on the exact PR head;
4. canonical docs synchronized with implemented behavior;
5. independent review on the exact qualified head;
6. no unresolved applicable findings;
7. merge guarded by expected head SHA;
8. post-merge `main` push CI GREEN.

## Explicit non-goals

0.2-F does not add:

```text
Skill mutation/equipment
Skill detail/history routes
charts or time-series projections
Rank/Traits/Titles detail
Hero/settings management
new localization system
MCP tool/field changes
public REST/API
Interactive Server/WebAssembly
new auth/session mechanism
schema/migration/index
new rule version
CQRS/read-repository split
```

A read-store/CQRS split becomes an ADR candidate only if subsequent independent read slices continue to cause systematic `IHeroPassportStateStore`/test-double churn. It is not introduced speculatively in 0.2-F.
