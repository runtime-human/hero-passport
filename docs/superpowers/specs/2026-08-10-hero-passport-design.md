# Hero Passport Architecture v2 — consolidated design

**Status:** Accepted design for implementation  
**Date:** 2026-08-10  
**Target:** Hero Passport 0.1.0

## 1. Goal

Build a cross-platform local-first RPG passport for AI coding agents that integrates first with Codex through a tiny stdio MCP surface, stores durable progression in SQLite, calculates all game progress deterministically, and never needs source code/cloud telemetry for the core loop.

## 2. Research basis

The design was produced through three passes:

1. extract architecture practices from mature open MCP servers/apps;
2. reject patterns that solve a scale/distribution problem Hero Passport does not have;
3. verify surviving choices against current official MCP/Codex/.NET/EF/SQLite/package documentation on 2026-08-10.

Key repositories studied are documented in `docs/ECOSYSTEM-BENCHMARK.md`: GitHub MCP Server, Sentry MCP, DBHub, Context7, Playwright MCP/CLI, ToolHive and official MCP SDK/reference repositories.

The resulting design intentionally combines practices rather than cloning one project.

## 3. Product loop

```text
Codex decides the task is meaningful
 -> hero.start_quest(questType, goal)
 -> questId
 -> Codex works normally
 -> hero.finish_quest(questId, result, compact summary/metrics/skills)
 -> deterministic reward persisted once
 -> structured result + compact displayText
```

`hero.current_quest` restores workflow context; `hero.get_card` reads hero progress.

No other MCP tool in 0.1.0.

## 4. Why MCP is appropriate here

Playwright/Context7 show that CLI + Skills is often better than large MCP inventories. Hero Passport remains a good MCP fit because:

- only four tools are advertised;
- an explicit durable `questId` is useful to agent reasoning;
- start/finish are semantically typed operations rather than generic shell commands;
- output is tiny.

Administration/diagnostics/export/full history stay CLI/dashboard.

## 5. Architecture

```text
HeroPassport.Domain
        ^
        |
HeroPassport.Application
        ^
        |
HeroPassport.Infrastructure
        ^
        |
HeroPassport.App

HeroPassport.Web (0.2) -> Application
```

### Domain

Pure deterministic game rules/state transitions. No EF/MCP/JSON/filesystem/localization.

### Application

Typed use cases and ports; resolves/coordinates state and returns typed result data. Uses `TimeProvider`. No MCP SDK and no human rendering.

### Infrastructure

EF Core/SQLite, migrations, stores, config/path resolution, project fingerprint, export, diagnostics.

### App

Composition root, System.CommandLine, official MCP stdio adapters and presentation/localization.

### Web later

Blazor read-focused host over Application read models, using Infrastructure only in composition root.

## 6. MCP design

Canonical stable order:

```text
hero.start_quest
hero.finish_quest
hero.current_quest
hero.get_card
```

Register four adapter types explicitly. No assembly-wide scanning.

Input design:

```text
start: questType, goal
finish: questId, result, summary, metrics, skillsUsed
current: empty strict object
card: empty strict object
```

Stable local state is not model input:

```text
active hero
project identity
locale
presentation mode
data/config paths
rule versions
```

All input schemas use JSON Schema 2020-12-style strict object semantics with `additionalProperties:false`, closed enums and bounded text/counters.

All outputs are typed through `outputSchema`/structured content and include one compact `displayText`, not duplicate status fields.

Annotations accurately express read-only/idempotent/open-world semantics; Tasks are forbidden.

## 7. MCP workflow guidance

Static server instructions carry cross-tool guidance. For Codex, first 512 characters are self-contained.

Semantic instruction:

```text
Use Hero Passport for meaningful coding/debugging/review/planning/research/docs tasks. Start one quest, keep questId, finish it once. Never send code, diffs, raw logs, prompts, secrets, environment or workspace paths. Show compact displayText in final output.
```

AGENTS.md may repeat this briefly for project context; per-response `agentHint` is removed.

## 8. Compatibility

MCP 2026-07-28 is the protocol baseline. Application correctness does not rely on protocol session state.

After 0.1.0:

- tool names/schemas/descriptions are compatibility artifacts;
- rename uses temporary deprecated alias if unavoidable;
- breaking semantic changes require explicit contract evolution;
- adding a fifth/sixth tool requires review/eval; >6 triggers a dedicated tool-surface architecture review.

## 9. RPG engine

Rule identifiers:

```text
reward/1.0.0
trust-risk/1.0.0
traits/1.0.0
```

Clean coding golden = 95 XP.

Everything uses deterministic integer arithmetic. Historical completion stores its original breakdown/rule versions; retry never reruns newer rules.

Skill XP allocation always sums exactly to quest XP.

Only three fully specified traits in MVP.

No achievements/items/streak engine/LLM judge.

## 10. Presentation

Typed engine/use-case result and human output are separate.

```text
Domain/Application -> numeric/canonical keys
App HeroTextRenderer -> RU/EN compact/normal text
```

This avoids coupling reward tests to punctuation and lets Web use typed data directly.

RU canonical presentation includes:

```text
scope_control = Контроль
clean scope bonus = Бонус за контроль
scope violation = Выход за задачу
```

## 11. Persistence

SQLite + EF Core.

Operational baseline:

```text
IDbContextFactory<HeroPassportDbContext>
one short-lived context/unit of work
synchronous DB operations
Mode=ReadWriteCreate
Cache=Default
Foreign Keys=True
Pooling=True
Default Timeout=5 (validate before release)
WAL
synchronous=FULL
```

Do not use `Task.Run` around DB calls or long-lived ambient DbContext.

## 12. Storage model

Core tables:

```text
heroes
projects
hero_project_stats
quest_sessions
quest_reports
quest_report_skills
skills
hero_skills
traits
hero_traits
xp_events
app_settings
```

Critical integrity:

```text
quest report one-to-one with quest
xp_events.quest_id UNIQUE
at most one active quest per hero/project slot
finish transaction atomic
```

No full workspace path/code/diff/raw log/prompt/env metadata columns.

## 13. Migrations

EF migrations from first schema. Never `EnsureCreated` in product path.

Use EF Core built-in database-wide migration locking; SQLite uses `__EFMigrationsLock`.

No custom migration mutex/file lock.

`doctor` diagnoses suspicious abandoned migration lock; normal startup does not blindly delete it.

CI checks pending model changes and upgrade/fresh DB fixtures.

## 14. App data/config

Windows:

```text
%LOCALAPPDATA%\HeroPassport
```

not roaming `%APPDATA%`.

macOS:

```text
~/Library/Application Support/HeroPassport
```

Linux respects XDG data/config/state roots.

Tests/dev use `HERO_PASSPORT_HOME`.

`config.json` v1 is small/strict and contains only local presentation/diagnostics policy. Active hero is product state in SQLite.

## 15. Privacy/security

Contract-first minimization:

```text
no source code
no diffs
no raw logs
no prompts/chat history
no secrets/API keys
no env dump
no workspace path
no arbitrary metadata JSON
```

Goal/summary are bounded untrusted data and never become server instructions/tool definitions.

MVP requires no network and no elevation.

MCP stdout is protocol only; logs go stderr/local file under safe-field policy.

## 16. Libraries

Accept stable minimal stack:

```text
ModelContextProtocol 2.0.0
EF Core SQLite 10.0.10
EF Core Design 10.0.10 dev/private
SQLitePCLRaw.bundle_e_sqlite3 3.0.5
System.CommandLine 2.0.10
xunit.v3 3.2.2
built-in Host/DI/Logging/Options/TimeProvider/System.Text.Json/UUIDv7
```

Reject/defer without demonstrated need:

```text
MediatR
FluentValidation
AutoMapper
Dapper
Polly
Serilog/NLog
Spectre.Console baseline
OpenTelemetry exporters
Testcontainers
runtime plugins
generic repository/CQRS framework
```

See `DEPENDENCIES.md` for rationale and re-evaluation conditions.

## 17. Codex integration

Codex owns its configuration.

Preferred:

```bash
codex mcp add hero-passport -- hero-passport mcp
codex mcp list
```

Project config may set `mcp_servers.hero-passport.cwd` and host-side `enabled_tools` based on current official Codex docs.

Hero Passport does not edit Codex TOML in MVP.

## 18. Quality strategy

Three layers:

```text
deterministic tests -> rules/storage correctness
protocol/process tests -> actual MCP/CLI contract
agent evals -> Codex chooses lifecycle/tools correctly
```

Storage tests use real file-backed SQLite/WAL.

MCP tests inspect real advertised catalog, schemas, annotations, output conformance and stdout.

Codex E2E is required for 0.1.0.

Agent eval corpus includes meaningful vs trivial tasks, recovery, conflict, retries and privacy-adversarial scenarios.

## 19. Delivery

```text
0.0.1 foundation
0.0.2 domain rules
0.0.3 application lifecycle
0.0.4 config/paths/presentation
0.0.5 SQLite/migrations
0.0.6 transaction/idempotency
0.0.7 CLI/doctor
0.0.8 MCP
0.0.9 Codex E2E/evals
0.1.0-rc.1 hardening
0.1.0 MVP
0.2.0 Blazor dashboard
```

No dashboard before MCP/core quality gates.

## 20. Implementation success condition

The architecture is successful if a coding agent can implement one feature by loading only the relevant feature folder/spec plus a small set of contracts/tests, rather than understanding a generic framework/platform.

That means choosing the simplest explicit implementation at every layer and adding abstraction only where this design identifies a real boundary.

## 21. Normative detail

This consolidated design is an index/summary. If implementation detail is needed, the specialized documents are normative:

```text
ARCHITECTURE.md
MCP-CONTRACT.md
ENGINE-SPEC.md
DATA-MODEL.md
CONFIGURATION.md
SECURITY-PRIVACY.md
TESTING-QUALITY.md
DEPENDENCIES.md
integrations/CODEX.md
DECISION-LOG.md
```

If the summary ever conflicts with a detailed spec, fix the summary before coding against it.
