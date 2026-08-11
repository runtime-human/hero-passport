# Hero Passport v3.2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to execute this plan task-by-task. Use superpowers:test-driven-development for every product-code task and superpowers:verification-before-completion before claiming a task complete.

**Goal:** Implement Hero Passport 0.1.0 as a local-first deterministic RPG companion with stdio HP-MCP/2, official Agent Skill, CLI, multi-Hero state, one open Quest per Hero+Project, safe request idempotency and atomic SQLite progression.

**Architecture:** C# 14 / .NET 10 modular monolith. Domain owns pure versioned game rules; Application owns semantic use cases; Infrastructure owns EF/SQLite/Git/filesystem/config; App owns CLI/MCP/presentation/localization. The Agent Skill is a portable orchestration package that calls MCP but never calculates game state.

**Tech Stack:** .NET SDK 10.0.302; `net10.0`; ModelContextProtocol 2.1.0; EF Core SQLite/Microsoft.Data.Sqlite 10.0.10; SQLitePCLRaw.bundle_e_sqlite3 3.0.5; actual SQLite runtime >=3.53.4; System.CommandLine 2.0.10; xunit.v3 3.2.2; .NET resource localization.

## Global constraints

Before each task read the relevant normative docs linked from `AGENTS.md`.

Never introduce:

```text
source/diff/raw-log ingestion
continuous telemetry
LLM judge
agent ownership/leases/heartbeats
cloud/sync backend
HTTP/OAuth
MCP Tasks for Quest lifecycle
MediatR/AutoMapper/Dapper/Polly/runtime plugin framework
```

Every persistence/concurrency claim uses real temporary file-backed SQLite. Do not use EF InMemory as evidence.

Every task follows:

```text
failing test -> observe failure -> minimal implementation -> observe pass -> refactor -> focused commit
```

Do not combine unrelated tasks into one implementation commit.

---

## Task 1 — Repository scaffold and dependency guardrails

**Create:**

```text
global.json
Directory.Build.props
Directory.Packages.props
HeroPassport.slnx
src/HeroPassport.Domain/HeroPassport.Domain.csproj
src/HeroPassport.Application/HeroPassport.Application.csproj
src/HeroPassport.Infrastructure/HeroPassport.Infrastructure.csproj
src/HeroPassport.App/HeroPassport.App.csproj
tests/HeroPassport.Domain.Tests/HeroPassport.Domain.Tests.csproj
tests/HeroPassport.Application.Tests/HeroPassport.Application.Tests.csproj
tests/HeroPassport.Infrastructure.Tests/HeroPassport.Infrastructure.Tests.csproj
tests/HeroPassport.App.Tests/HeroPassport.App.Tests.csproj
tests/HeroPassport.Architecture.Tests/HeroPassport.Architecture.Tests.csproj
tests/HeroPassport.Contract.Tests/HeroPassport.Contract.Tests.csproj
tests/HeroPassport.AgentEvals/HeroPassport.AgentEvals.csproj
```

**Tests first:** architecture project-reference tests proving Domain has no project dependency; Application references Domain only; Infrastructure references Application+Domain; App references Application+Infrastructure.

**Implementation:** central package versions exactly from `docs/DEPENDENCIES.md`; nullable enabled; warnings as errors for product projects; deterministic builds.

**Verify:**

```bash
dotnet --version
dotnet restore HeroPassport.slnx
dotnet build HeroPassport.slnx -c Release --no-restore
dotnet test tests/HeroPassport.Architecture.Tests/HeroPassport.Architecture.Tests.csproj -c Release --no-build
```

**Commit:** `build: scaffold hero passport v3.2 solution`

---

## Task 2 — Domain primitives, IDs and canonical enums

**Create under `src/HeroPassport.Domain/`:**

```text
Ids/HeroId.cs
Ids/ProjectId.cs
Ids/QuestId.cs
Ids/MutationRequestId.cs
Quests/QuestType.cs
Quests/QuestResult.cs
Quality/ExecutionStatus.cs
Quality/EvidenceKind.cs
Skills/SkillKey.cs
Heroes/RankKey.cs
Localization/LocaleKey.cs
Rules/RuleVersion.cs
Common/JsonSafeInteger.cs
```

**Tests:** UUIDv7 wrapper round-trips; closed enum parsing; JSON-safe ceiling arithmetic; equality/ordering rules.

Use server-generated entity IDs from `Guid.CreateVersion7()`. Request IDs are validated caller UUIDv7 values, never regenerated on retry.

**Verify:** Domain test project only.

**Commit:** `feat: add domain primitives and canonical keys`

---

## Task 3 — SafeTextV1

**Create:**

```text
src/HeroPassport.Application/Text/SafeTextV1.cs
src/HeroPassport.Application/Text/SafeTextResult.cs
tests/HeroPassport.Application.Tests/Text/SafeTextV1Tests.cs
```

SafeText is an input-boundary/Application concern; Domain receives canonical text.

**Tests first:** NFC; Unicode scalar count; supplementary-plane emoji counts once; trim/collapse Unicode whitespace; reject unpaired surrogates; reject C0/C1 non-whitespace controls; reject documented bidi controls; title/name/goal/summary bounds.

**Verify:** exact vectors from `WIRE-CONTRACT.md`.

**Commit:** `feat: add safe text canonicalization`

---

## Task 4 — Deterministic reward and Skill allocation engine

**Create under Domain:**

```text
Rules/Reward/RewardRulesV2.cs
Rules/Reward/RewardInput.cs
Rules/Reward/RewardBreakdown.cs
Rules/Skills/SkillAllocationRulesV1.cs
Rules/Skills/SkillAllocation.cs
```

**Tests first:** all `ENGINE-SPEC.md` reward goldens; outcome floor arithmetic; penalty caps; abandoned=0; observed-tests condition; 1/2/3 Skill exact conservation; 95 -> 47/29/19 for three Skills.

No doubles/decimals.

**Commit:** `feat: add deterministic reward and skill allocation rules`

---

## Task 5 — Hero/Skill levels, Rank, Trust/Strain, Streak and unlocks

**Create:**

```text
Rules/Progression/HeroProgressionV2.cs
Rules/Progression/SkillProgressionV2.cs
Rules/Progression/RankRulesV1.cs
Rules/TrustStrain/TrustStrainRulesV1.cs
Rules/Streak/StreakRulesV1.cs
Rules/Unlocks/UnlockRulesV2.cs
Rules/Unlocks/TraitKey.cs
Rules/Unlocks/TitleKey.cs
Rules/Unlocks/UnlockResult.cs
```

**Tests first:** every exact threshold edge; level caps with XP accumulation; rank boundaries; Trust/Strain component composition/caps/clamp; abandoned neutral; streak reset/increment; every Trait/Title unlock exact threshold and monotonicity; active Title deterministic priority.

**Commit:** `feat: add hero progression trust strain and unlock rules`

---

## Task 6 — Configuration, localization and presentation primitives

**Create in Application/App:**

```text
src/HeroPassport.Application/Configuration/HeroPassportSettings.cs
src/HeroPassport.Application/Configuration/PresentationStyle.cs
src/HeroPassport.App/Presentation/HeroTextRenderer.cs
src/HeroPassport.App/Presentation/Resources/HeroPassportMessages.resx
src/HeroPassport.App/Presentation/Resources/HeroPassportMessages.ru-RU.resx
src/HeroPassport.App/Presentation/Resources/HeroPassportMessages.en-US.resx
```

If invariant/default resources make an additional neutral `.resx` unnecessary, keep exactly two culture resources plus a typed key catalog; do not duplicate semantics in three files without need.

**Tests:** RU/EN key completeness; placeholder parity; start banner bounds; reward component mapping; Trust/Strain labels; milestone flavor selection deterministic; no localized strings in Domain.

**Commit:** `feat: add configuration localization and presentation`

---

## Task 7 — Project identity v1

Implement `docs/PROJECT-IDENTITY.md` without changing its contract.

**Create:**

```text
src/HeroPassport.Application/Projects/ProjectIdentity.cs
src/HeroPassport.Application/Projects/IProjectIdentityResolver.cs
src/HeroPassport.Infrastructure/Projects/GitProjectIdentityResolver.cs
src/HeroPassport.Infrastructure/Projects/GitCommandRunner.cs
src/HeroPassport.Infrastructure/Projects/ProjectFingerprint.cs
tests/HeroPassport.Infrastructure.Tests/Projects/ProjectIdentityTests.cs
```

**Tests:** ordinary repo/nested cwd; linked worktrees same identity; explicit monorepo scope; submodule/nested repo separate; bare repo rejected; Git trust failure no standalone fallback; standalone non-Git; path/remote never persisted/output.

Use argument-list process execution, no shell interpolation, and scrub Git redirection env variables per spec.

**Commit:** `feat: implement project identity v1`

---

## Task 8 — EF model and migration 0001

**Create:**

```text
src/HeroPassport.Infrastructure/Persistence/HeroPassportDbContext.cs
src/HeroPassport.Infrastructure/Persistence/HeroPassportDbContextFactory.cs
src/HeroPassport.Infrastructure/Persistence/Entities/*
src/HeroPassport.Infrastructure/Persistence/Configurations/*
src/HeroPassport.Infrastructure/Persistence/Migrations/*
tests/HeroPassport.Infrastructure.Tests/Persistence/SchemaTests.cs
```

Model exactly `DATA-MODEL.md`, including:

```text
mutation_receipts unique(operation_key, request_id)
quest_sessions partial unique(hero_id, project_id) WHERE status='open'
quest_reports unique quest_id
xp_events unique quest_id
```

Use EF migration `0001_InitialV32` naming convention accepted by tooling.

**Tests first:** create fresh file DB via migrations; inspect unique/partial indexes/FKs; no forbidden privacy columns; UUID/time/value constraints represented where SQLite/EF supports them and Application validation covers the rest.

**Commands:**

```bash
dotnet tool restore
dotnet ef migrations add 0001_InitialV32 --project src/HeroPassport.Infrastructure --startup-project src/HeroPassport.App
dotnet test tests/HeroPassport.Infrastructure.Tests/HeroPassport.Infrastructure.Tests.csproj -c Release
```

If the repository chooses a local tool manifest for `dotnet-ef`, create/pin it in this task.

**Commit:** `feat: add initial sqlite persistence schema`

---

## Task 9 — SQLite initialization, runtime qualification and doctor primitives

**Create:**

```text
src/HeroPassport.Infrastructure/Persistence/SqliteDatabaseInitializer.cs
src/HeroPassport.Infrastructure/Persistence/SqliteRuntimeDiagnostics.cs
src/HeroPassport.Infrastructure/Persistence/SqliteWriterTransaction.cs
src/HeroPassport.Application/Diagnostics/DoctorResult.cs
```

**Tests:** actual `sqlite_version() >= 3.53.4`; WAL/FULL/foreign_keys; local file path; non-deferred Serializable writer qualification; busy timeout mapping; unsupported old-runtime fixture if injectable/feasible; no Cache=Shared.

Do not claim `BEGIN IMMEDIATE` until the integration test proves the selected provider behavior.

**Commit:** `feat: qualify sqlite runtime and writer transactions`

---

## Task 10 — First-run setup and Hero lifecycle use cases

**Create Application commands/handlers:**

```text
Configuration/ConfigureApplication.cs
Heroes/CreateHero.cs
Heroes/ListHeroes.cs
Heroes/ActivateHero.cs
Heroes/ArchiveHero.cs
Heroes/RestoreHero.cs
Heroes/DeleteHero.cs
```

Create corresponding Infrastructure stores/queries.

**Tests first:** HP001 setup gate; initial Hero creation atomically with setup; post-setup config cannot create/rename Hero; create request replay/mismatch; create does not activate; activate future-default only; archive/restore idempotent; archive/delete reject active Hero and any open-Quest Hero; permanent delete exact-name confirmation; delete request late retry; deleted history removed while minimal receipt remains.

**Commit:** `feat: implement onboarding and hero lifecycle`

---

## Task 11 — StartQuest use case and one-open invariant

**Create:**

```text
src/HeroPassport.Application/Quests/StartQuest.cs
src/HeroPassport.Application/Quests/StartQuestCommand.cs
src/HeroPassport.Application/Quests/StartQuestResult.cs
src/HeroPassport.Application/Mutations/MutationArgumentEncoderV1.cs
src/HeroPassport.Infrastructure/Quests/StartQuestStore.cs
```

**Tests first:** same request ID/same args replay; same ID/different args HP135; different request while open HP133; new request after finish can reuse same text; title/goal normalized; locale snapshot; Hero+Project owner captured.

**Concurrency integration:** two different starts same Hero+Project -> exactly one Quest and one HP133; same request concurrently -> one Quest/replay.

**Crash tests:** child process before commit and after commit-before-response.

**Commit:** `feat: implement idempotent quest start`

---

## Task 12 — FinishQuest atomic progression

**Create:**

```text
src/HeroPassport.Application/Quests/FinishQuest.cs
src/HeroPassport.Application/Quests/FinishQuestCommand.cs
src/HeroPassport.Application/Quests/FinishQuestResult.cs
src/HeroPassport.Application/Quality/QuestMetrics.cs
src/HeroPassport.Infrastructure/Quests/FinishQuestStore.cs
```

**Tests first:** evidence cross-field validation; all engine inputs; successful atomic report/components/Skills/XP event/Hero/Trust-Strain/Streak/unlocks/project update; failed/blocked/partial/abandoned behavior; current active Hero switch does not change Quest owner; wrong Project -> HP134; already finished returns stored result and never current rule recalculation.

**Concurrency:** identical and conflicting concurrent Finish -> one committed immutable outcome/event.

**Crash:** before commit no partial progression; after commit-before-response retry returns stored result.

**Commit:** `feat: implement atomic quest finish progression`

---

## Task 13 — Read models: active Quest, Hero list/card and project stats

**Create:**

```text
src/HeroPassport.Application/Quests/ListActiveQuests.cs
src/HeroPassport.Application/Heroes/GetHeroCard.cs
src/HeroPassport.Application/Heroes/HeroCard.cs
src/HeroPassport.Infrastructure/Queries/ActiveQuestQuery.cs
src/HeroPassport.Infrastructure/Queries/HeroCardQuery.cs
```

**Tests:** active list cardinality 0..1; deterministic ordering where relevant; card fields per Wire contract; top Skills deterministic tie-break; active Quest optional; no project internal ID/fingerprint/path; success rate integer permille; archive list order.

**Commit:** `feat: add hero card and recovery read models`

---

## Task 14 — HP-MCP/2 stdio adapter

**Create:**

```text
src/HeroPassport.App/Mcp/HeroPassportMcpServer.cs
src/HeroPassport.App/Mcp/Tools/ConfigureTool.cs
src/HeroPassport.App/Mcp/Tools/CreateHeroTool.cs
src/HeroPassport.App/Mcp/Tools/ListHeroesTool.cs
src/HeroPassport.App/Mcp/Tools/ActivateHeroTool.cs
src/HeroPassport.App/Mcp/Tools/ArchiveHeroTool.cs
src/HeroPassport.App/Mcp/Tools/RestoreHeroTool.cs
src/HeroPassport.App/Mcp/Tools/DeleteHeroTool.cs
src/HeroPassport.App/Mcp/Tools/StartQuestTool.cs
src/HeroPassport.App/Mcp/Tools/FinishQuestTool.cs
src/HeroPassport.App/Mcp/Tools/ListActiveQuestsTool.cs
src/HeroPassport.App/Mcp/Tools/GetCardTool.cs
src/HeroPassport.App/Mcp/Validation/*
src/HeroPassport.App/Mcp/Serialization/*
```

**Contract tests first:** exact 11 tool names/order; exact annotations; closed schemas; SafeText/UUID/counter/enums; no forbidden inputs; success `structuredContent`; exactly one minified JSON TextContent semantically equal; expected errors `isError=true` and no structuredContent; setup gate; output bounds.

Generate implementation-derived snapshots under:

```text
contracts/mcp/hp-mcp-2/
```

Do not hand-maintain a second competing schema model.

**Protocol verification:** MCP Inspector; `2026-07-28` normal path; `2025-11-25` compatibility path; stdout frame purity.

**Commit:** `feat: add hp-mcp-2 stdio adapter`

---

## Task 15 — CLI surface

**Create:**

```text
src/HeroPassport.App/Cli/RootCommandFactory.cs
src/HeroPassport.App/Cli/Commands/InitCommand.cs
src/HeroPassport.App/Cli/Commands/McpCommand.cs
src/HeroPassport.App/Cli/Commands/DoctorCommand.cs
src/HeroPassport.App/Cli/Commands/CardCommand.cs
src/HeroPassport.App/Cli/Commands/HeroCommands.cs
src/HeroPassport.App/Cli/Commands/QuestCommands.cs
src/HeroPassport.App/Cli/Commands/ExportCommand.cs
src/HeroPassport.App/Cli/Commands/DataPathCommand.cs
```

Required UX:

```text
hero-passport init
hero-passport mcp [--project-root <path>]
hero-passport doctor
hero-passport card
hero-passport hero list|create|activate|archive|restore|delete
hero-passport quest list --active
hero-passport export
hero-passport data path
hero-passport --version
```

**Tests:** interactive onboarding through injectable console abstraction; non-interactive failure instead of prompt hang; safe stderr/exit codes; `--json` only on explicitly documented scriptable reads/actions; MCP command does not print CLI banners to stdout.

**Commit:** `feat: add hero passport cli`

---

## Task 16 — Logical export and physical backup adapter

**Create:**

```text
src/HeroPassport.Application/Export/ExportData.cs
src/HeroPassport.Infrastructure/Export/JsonExportWriter.cs
src/HeroPassport.Infrastructure/Persistence/SqliteBackupService.cs
```

**Tests:** export contains allowed game/history fields only; forbidden fields impossible; stable schema version; `BackupDatabase` snapshot opens independently; `quick_check`, `foreign_key_check`, migration metadata valid; never raw `File.Copy` active DB.

If physical backup is not exposed in 0.1 CLI, still implement only if required for migration/release safety; otherwise leave the tested port/service for the first migration that needs it rather than creating unused UI.

**Commit:** `feat: add safe export and sqlite backup support`

---

## Task 17 — Official Hero Passport Agent Skill

**Create:**

```text
skills/hero-passport/SKILL.md
skills/hero-passport/references/lifecycle.md
skills/hero-passport/references/finish-facts.md
skills/hero-passport/references/presentation.md
skills/hero-passport/references/recovery.md
```

Follow `docs/AGENT-SKILL.md` and the current Agent Skills specification.

`SKILL.md` contains valid YAML frontmatter with a precise trigger description and concise workflow; keep detail in references.

**Validation:**

```bash
skills-ref validate ./skills/hero-passport
```

when the reference validator is available in release tooling.

**AgentEvals first:** implement the scenario matrix from `TESTING-QUALITY.md`; score false start, premature finish, missed finish, wrong request-ID retry, provenance errors and self-reward mistakes.

The Skill must never encode duplicate XP formulas.

**Commit:** `feat: add hero passport agent skill`

---

## Task 18 — Integration qualification

Update executable examples in `docs/integrations/` only after testing current host versions.

Reference blocker: Codex.

For each claimed host:

```text
configure stdio MCP
install/map Skill if supported
verify project cwd/binding
complete onboarding
start/finish one Quest
restart/recover open Quest
inspect structured result
record tool confirmation UX
record limitations/version/date
```

**Tests/evidence:** packaged smoke notes or automated harness where practical; do not label a host Qualified from documentation reading alone.

**Commit:** `test: qualify hero passport host integrations`

---

## Task 19 — Full release qualification and packaging

Run the full matrix from `TESTING-QUALITY.md`.

Minimum commands, adjusted only if the repository tooling created above has a more precise wrapper:

```bash
dotnet restore HeroPassport.slnx
dotnet build HeroPassport.slnx -c Release --no-restore
dotnet test HeroPassport.slnx -c Release --no-build
dotnet publish src/HeroPassport.App/HeroPassport.App.csproj -c Release --no-build
```

Additionally run:

```text
SQLite runtime/pragmas doctor
concurrency suite
child-process crash suite
migration fixtures
physical backup verification
MCP Inspector + protocol compatibility
Agent Skill validator + AgentEvals
RU/EN resource completeness
privacy/schema/log scans
fresh packaged Codex E2E
cross-host smoke matrix
```

Record actual commands/output in release evidence. Do not claim pass for a step not run.

**Commit:** `test: complete hero passport 0.1 release qualification`

---

## Task 20 — v0.1 release readiness review

Before tagging:

1. compare implementation-derived MCP snapshots against `WIRE-CONTRACT.md`;
2. compare Domain rule goldens against `ENGINE-SPEC.md`;
3. search repository for retired active concepts (`QuestDedupKeyV1`, 16-open policy, `Risk`, MCP SDK 2.0.0, old reward rules);
4. run `dotnet list package --outdated` and verify any newer version from **official** vendor/package sources rather than auto-upgrading;
5. review all migrations/destructive FK paths;
6. verify README/integrations state only evidence-backed support claims;
7. run superpowers:verification-before-completion;
8. request code review before release/tag.

**Commit:** only if review produces documentation/config corrections; otherwise no synthetic commit is required.

## Plan completion criteria

Implementation is not complete until all accepted v3.2 behavior is represented by code **and** the subsystem-level evidence required by `TESTING-QUALITY.md` exists.

The intended execution order is vertical enough to expose architectural problems early: pure rules first, real persistence/invariants next, then MCP/CLI, then Skill/host qualification. Do not implement the Web 0.2 surface during this plan.
