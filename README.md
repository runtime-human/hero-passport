# Hero Passport

> Local-first RPG companion for people working with AI coding agents.

Hero Passport turns meaningful agent-assisted work into persistent XP, Skills, Levels, Ranks, Trust/Strain, Streaks, Traits and Titles — without collecting source code or requiring a cloud account.

## Experience

```text
You ask an AI agent to do meaningful project work
        ↓
Hero Passport Agent Skill starts/resumes a Quest automatically
        ↓
Agent works normally
        ↓
Skill finishes when the goal is genuinely done
        ↓
Hero Passport Core calculates deterministic progression
        ↓
Compact RPG logs + result card
```

Typical start:

```text
⚔ Добавить first-run onboarding
```

Typical clean coding finish:

```text
+60 XP  Базовая награда
+10 XP  Тестирование
+10 XP  Бонус за контроль
+10 XP  Итоговый отчёт
 +5 XP  Без исправлений

★ Level 7 → 8
XP      +95
Trust   52 → 54
Strain  18 → 16
```

## v3.2 architecture snapshot — 11 August 2026

```text
C# 14 / .NET 10 LTS / SDK 10.0.302
ModelContextProtocol 2.1.0
MCP semantics 2026-07-28; compatibility qualification 2025-11-25
EF Core SQLite / Microsoft.Data.Sqlite 10.0.10
SQLitePCLRaw.bundle_e_sqlite3 3.0.5
qualified actual SQLite runtime >= 3.53.4
System.CommandLine 2.0.10
xunit.v3 3.2.2
```

Runtime structure:

```text
AI agent
  ↕ Hero Passport Agent Skill
  ↕ HP-MCP/2 stdio
HeroPassport.App
  ↓
Application
  ↓
Domain
  ↕
Infrastructure -> SQLite
```

0.1 ships MCP Core + Agent Skill + CLI. Local Web UI is 0.2.

## Quest identity

Hero Passport v3.2 deliberately separates retry intent from work identity:

```text
startRequestId = caller-generated identity of one start intent/retry sequence
questId        = server-generated durable Quest identity
```

Exactly one Quest may be open for one Hero + Project.

Natural-language goal text is **not** an idempotency key. Repeating the same start request safely replays the same Quest; a fresh request can later create another Quest with identical wording.

A Quest belongs to the Hero + Project, not to Codex/Claude/another agent. Agents can hand the same `questId` to one another.

## HP-MCP/2 v3.2

Static explicit tool order:

```text
hero.configure
hero.create
hero.list
hero.activate
hero.archive
hero.restore
hero.delete
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

Exact schemas/results/errors: [`docs/WIRE-CONTRACT.md`](docs/WIRE-CONTRACT.md).

## Game rules

Game state is calculated locally and deterministically. The agent reports bounded facts and provenance; it never chooses its own reward.

Current outcome multipliers:

```text
success    100%
partial     60%
blocked     30%
failed      10%
abandoned    0%
```

Canonical clean coding golden remains **95 XP**.

Full rules: [`docs/ENGINE-SPEC.md`](docs/ENGINE-SPEC.md).

## Trust + Strain

```text
Trust  = demonstrated reliability
Strain = accumulated technical friction/turbulence
```

Both are `0..100`, deterministic and Quest-driven. No passive time regeneration, no harsh XP feedback loop and no product features locked behind them.

## Heroes

A Hero progresses globally across projects. Multiple Heroes are supported locally:

```text
create / list / activate / archive / restore / permanently delete
```

One Hero is globally active for **new** Quests. Existing Quests never change owner when the active Hero changes.

## Privacy

Hero Passport intentionally does not request or persist routine:

```text
source/file contents
diffs/patches
raw terminal/build/test logs
full prompts/chat transcripts
secrets/tokens/environment dumps
full workspace paths
Git remote URLs
continuous activity/heartbeat telemetry
```

This is a companion, not a work-surveillance product.

## Local-first

0.1 requires no account/cloud backend and uses same-host SQLite. The data model uses UUIDv7 and immutable completion facts so optional future sync can be designed later; sync itself is not part of MVP.

## Documentation

Start at [`docs/README.md`](docs/README.md).

Primary contracts:

- [`docs/PRODUCT-SPEC.md`](docs/PRODUCT-SPEC.md)
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)
- [`docs/WIRE-CONTRACT.md`](docs/WIRE-CONTRACT.md)
- [`docs/ENGINE-SPEC.md`](docs/ENGINE-SPEC.md)
- [`docs/AGENT-SKILL.md`](docs/AGENT-SKILL.md)
- [`docs/PERSISTENCE-RELIABILITY.md`](docs/PERSISTENCE-RELIABILITY.md)
- [`docs/PROJECT-IDENTITY.md`](docs/PROJECT-IDENTITY.md)
- [`docs/TESTING-QUALITY.md`](docs/TESTING-QUALITY.md)
- [`docs/ECOSYSTEM-BENCHMARK.md`](docs/ECOSYSTEM-BENCHMARK.md)

Implementation plan:

- `docs/superpowers/plans/2026-08-11-hero-passport-v3.2-implementation.md`

This architecture PR is documentation-only; it does **not** claim product build/test success before implementation exists.
