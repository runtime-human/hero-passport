# Hero Passport — Roadmap

**Status:** Accepted implementation sequence  
**Baseline:** 2026-08-10  
**Target:** minimal public-quality local MVP before dashboard expansion

## 1. Delivery principle

The critical path is:

```text
reproducible repo
 -> deterministic domain
 -> application lifecycle
 -> real SQLite/idempotency
 -> CLI/local operations
 -> MCP stdio
 -> Codex end-to-end
 -> security/status/release hardening
 -> dashboard
```

Each milestone leaves `main` buildable and testable. Version numbers are checkpoints, not permission to merge broken half-slices.

## 2. Milestone map

| Milestone | Version | User/engineering outcome |
|---|---:|---|
| M0 Foundation | 0.0.1 | reproducible .NET 10 solution + CI/test skeleton |
| M1 Domain contracts | 0.0.2 | stable IDs/enums/contracts/quality flags/rule versions |
| M2 Reward engine | 0.0.3 | XP + levels + golden fixtures |
| M3 Progression | 0.0.4 | skills + trust/risk + traits + localization |
| M4 Application lifecycle | 0.0.5 | start/finish/current/card use cases behind ports |
| M5 SQLite | 0.0.6 | EF migrations, WAL-safe current native baseline, seed/read models |
| M6 Integrity | 0.0.7 | atomic finish + retry/concurrency defenses |
| M7 CLI/local ops | 0.0.8 | init/status/doctor/export/data path |
| M8 MCP stdio | 0.0.9 | four tools + stdout guard + official SDK tests |
| M9 Codex experience | 0.0.10 | native Codex setup + full two-call flow |
| M10 Hardening | 0.1.0-rc.1 | privacy/package/matrix/release qualification |
| M11 Minimal MVP | 0.1.0 | tagged Codex-first local-first release |
| M12 Dashboard | 0.2.0 | local Blazor read dashboard |

The source report's longer `0.0.11/12/13` sequence is intentionally collapsed around a clearer `0.1.0` product gate: the shippable artifact is the safe two-call loop, not an arbitrary count of internal increments.

## 3. M0 — Foundation (`0.0.1`)

### Deliverables

```text
solution file
global.json
Directory.Build.props
Directory.Packages.props
.editorconfig
.gitignore
src/ project skeleton
tests/ project skeleton
package lock files
GitHub Actions CI
```

Baseline:

```text
.NET SDK                         10.0.302 exact
net10.0 / C# 14
Microsoft Testing Platform      selected in global.json
xunit.v3                        3.2.2
```

### Gate

```bash
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
```

pass on Windows, Linux and macOS.

## 4. M1 — Domain/contracts (`0.0.2`)

Build:

- UUIDv7 typed identity wrappers/record structs;
- quest type/result/build/test status models;
- canonical skill/trait keys;
- `QuestQualityFlags`;
- `RuleVersions`;
- Application contracts for four use cases;
- schema/output-mode/locale/string-bound/error models;
- architecture dependency tests.

Gate: no EF/MCP/CLI types in Domain/Application contracts and contract serialization fixtures are stable.

## 5. M2 — Reward/level engine (`0.0.3`)

Build:

- base XP map;
- integer permille multipliers;
- bonuses/penalties;
- immutable `RewardBreakdown`;
- level curve/read model;
- `coding-success-clean-95` golden.

Gate: full rule matrix, boundaries and culture-determinism tests pass.

## 6. M3 — Progression (`0.0.4`)

Build:

- `SkillKeyNormalizer`;
- cumulative-floor skill allocation;
- trust/risk engine;
- three initial trait policies;
- localized labels/status formatter separated from numeric rules.

Gate: standard fixture yields exactly:

```text
XP +95
skills 47 / 29 / 19 for coding / scope_control / testing_awareness
trust 50 -> 51
risk 20 -> 19
```

and RU labels include `Контроль`, `Бонус за контроль`, `Выход за задачу`.

## 7. M4 — Application lifecycle (`0.0.5`)

Build:

- application ports/stores/unit-of-work;
- `StartQuestHandler`;
- `FinishQuestHandler`;
- `GetCurrentQuestHandler`;
- `GetHeroCardHandler`;
- initialization/export orchestration;
- project identity abstraction;
- `TimeProvider` injection;
- display/status projections.

Use fake test ports first so lifecycle correctness is independent of EF/MCP.

Gate: all four use cases and retry branches run in Application tests.

## 8. M5 — SQLite (`0.0.6`)

Build:

- EF Core 10.0.10 mappings;
- initial migration;
- `foreign_keys=ON` + WAL initialization;
- `SQLitePCLRaw.bundle_e_sqlite3 3.0.5` direct pin;
- runtime native SQLite floor `>= 3.53.4`;
- app-data path adapter;
- project identity/fingerprint resolver;
- default hero/canonical skill/trait seeding;
- storage/query implementations;
- real temp-file SQLite tests.

Gate: fresh migration/seed/reopen/WAL/FK/native-version tests pass.

## 9. M6 — Integrity/idempotency (`0.0.7`)

Build:

- active/idempotency uniqueness;
- unique XP event per quest;
- atomic finish transaction;
- persisted reward breakdown/outcome replay;
- concurrent start/finish tests;
- bounded DB busy handling;
- rollback tests.

Gate: repeated/concurrent finish attempts can never produce two reward ledger events for one quest.

## 10. M7 — CLI/local operations (`0.0.8`)

Required commands:

```text
hero-passport init
hero-passport status
hero-passport doctor
hero-passport export --format json
hero-passport data path
hero-passport --version
```

`doctor` reports:

```text
app/runtime version
data path/access
DB open/migration state
native SQLite version
WAL/foreign-key state
default hero state
```

Gate: init/re-init/status/export/doctor work in clean isolated Windows/Linux/macOS tests.

## 11. M8 — MCP stdio (`0.0.9`)

Build:

- stable `ModelContextProtocol 2.0.0`;
- `hero-passport mcp`;
- exactly four tools in deterministic order;
- compact descriptions/schemas/annotations;
- thin Application adapters;
- tool-error mapping;
- official SDK client/server tests;
- child-process stdout guard.

Gate: complete protocol exchange succeeds with zero non-protocol stdout bytes.

## 12. M9 — Codex experience (`0.0.10`)

Build/validate:

- current official `codex mcp add hero-passport -- hero-passport mcp` path;
- `codex mcp list` verification;
- consumer `AGENTS.md` snippet;
- current-workspace project resolution;
- explicit local `mcp_servers.hero-passport.cwd` troubleshooting path where a host needs it;
- compact final `displayText` flow;
- retry + restart persistence.

Gate: installed package, not IDE-only source wiring, completes:

```text
start -> normal work -> finish -> displayText -> repeat finish safely -> get card
```

## 13. M10 — MVP hardening (`0.1.0-rc.1`)

Build/validate:

- dependency vulnerability audit;
- privacy sentinel tests;
- export manifest/version;
- analyzer/format gate;
- locked restore;
- cross-platform package/install smoke;
- final install/troubleshooting docs;
- changelog/release notes;
- release qualification checklist/script.

Gate: all success criteria from `PRODUCT-SPEC.md` pass.

## 14. M11 — Minimal MVP (`0.1.0`)

Release only; no scope growth.

Artifacts:

- .NET tool package/selected stable install path;
- source tag/release notes;
- tested Codex stdio setup;
- data/export documentation;
- deterministic local progression;
- no dashboard dependency.

## 15. M12 — Dashboard (`0.2.0`)

Only after `0.1.0` validates the state model.

Local Blazor/ASP.NET Core, loopback-only default, read-only first:

```text
Hero card
XP/level progress
Trust / Risk
Top skills
Trait progress
Last reward
Recent quests
Project stats
```

Rules:

- Application read models only;
- no `DbContext` injection into Razor components;
- no reward logic in UI;
- no hidden cloud/auth/team work.

## 16. Deferred unscheduled candidates

Evaluate only after real use:

```text
card v2 / richer RPG presentation
safe import/restore
new explicitly versioned rules/traits
MCP resources/prompts when they reduce friction
achievements as separate post-MVP module
artifacts/items
self-evolution experiments
MCP Apps / MCP Tasks
HTTP/remote MCP with auth/security architecture
cloud/team sync
```

A candidate enters the roadmap only with product outcome, schema/migration impact, threat/privacy impact and test plan.

## 17. Sequencing constraints

- Domain rules before EF persistence logic.
- Application lifecycle before MCP adapters.
- SQLite integrity before claiming idempotency.
- MCP/status loop before dashboard.
- No HTTP merely because SDK supports it.
- No runtime plugins for speculative extensibility.
- No custom Codex config writer while native Codex management works.
- No achievements conflated with traits.

## 18. Safe parallel work

After M1:

```text
A: domain rule/golden implementation
B: CLI command shell/help without product logic
C: docs/fixtures/integration instructions
```

After M4:

```text
A: SQLite mappings/migrations
B: MCP adapter tests against fake Application
C: packaging/CI hardening
```

Merge order still follows the critical path and keeps main green.

## 19. Roadmap completion test

The MVP roadmap is complete only when an external user can install Hero Passport, configure current Codex tooling, complete the two-call quest loop, retry safely, restart without losing state, export their data, and inspect compact progression without source/diff/log leakage.
