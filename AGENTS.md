# AGENTS.md — Hero Passport

## Mission

Build Hero Passport as a local-first RPG companion for people working with AI coding agents.

0.1 is **MCP Core + official Agent Skill + CLI**. Web is 0.2.

Do not turn the product into source-code telemetry, employee monitoring, an LLM judge, an agent scheduler, or a cloud service by accident.

## Read before coding

Always read:

```text
docs/PRODUCT-SPEC.md
docs/ARCHITECTURE.md
```

Then the focused contract for the subsystem:

```text
MCP exact wire          docs/WIRE-CONTRACT.md
RPG rules               docs/ENGINE-SPEC.md
Agent orchestration     docs/AGENT-SKILL.md
SQLite/recovery         docs/PERSISTENCE-RELIABILITY.md
Data model              docs/DATA-MODEL.md
Project identity        docs/PROJECT-IDENTITY.md
Configuration/i18n      docs/CONFIGURATION.md
Security/privacy        docs/SECURITY-PRIVACY.md
Tests/release evidence  docs/TESTING-QUALITY.md
Implementation plan     docs/superpowers/plans/2026-08-11-hero-passport-v3.2-implementation.md
```

`docs/README.md` defines documentation precedence.

## Core architecture

```text
Domain
  ^
Application
  ^
Infrastructure
  ^
App (MCP stdio + CLI + presentation)
```

Agent Skill is a portable orchestration package outside Domain/Application game logic.

No separate Contracts assembly in 0.1.

## Critical v3.2 invariants

```text
one meaningful goal = one Quest
one open Quest per Hero + Project
Quest owner fixed at start
Quest is not owned by an AI agent
startRequestId identifies caller start intent/retry
questId identifies durable Quest
natural-language goal is never an idempotency key
Finish commits progression at most once
historical finish result is immutable
```

## Game authority

The agent reports bounded facts only.

Hero Passport Core calculates:

```text
XP
Skill XP
Hero/Skill levels
Rank
Trust/Strain
Streak
Traits/Titles
milestones
```

Never accept agent-supplied XP/quality score/game deltas.

Canonical clean coding golden for `reward/2.0.0` is **95 XP**.

## Privacy deny-list

Never add routine model-facing fields/storage/logging for:

```text
source/file contents
diffs/patches
raw terminal/build/test logs
full prompts/chat transcripts
secrets/tokens/environment dumps
full workspace paths
Git remote URLs
arbitrary metadata/context bags
```

Build/test provenance is `observed | reported | none`, not raw evidence storage.

## MCP

Official C# SDK baseline: `ModelContextProtocol 2.1.0`.

Preferred semantics: MCP `2026-07-28`, with `2025-11-25` compatibility qualification.

Application correctness never depends on MCP session/connection state.

Tool inventory/order is normative in `WIRE-CONTRACT.md`; register explicitly, never by broad assembly scan.

For stdio:

```text
stdout = MCP protocol only
stderr = safe diagnostics only
```

## SQLite

```text
EF Core / Microsoft.Data.Sqlite 10.0.10
SQLitePCLRaw.bundle_e_sqlite3 3.0.5
actual SQLite runtime >= 3.53.4
WAL
synchronous=FULL
foreign_keys=ON
IDbContextFactory
```

All read-modify-write operations acquire writer intent before invariant reads.

No custom global writer mutex. No Polly retry layer. Never delete WAL/SHM as recovery.

## Development method

Follow the accepted implementation plan.

Use TDD for product code:

```text
write failing test
run and observe failure
minimal implementation
run and observe pass
refactor
commit focused change
```

Use real file-backed SQLite for persistence/concurrency/crash claims.

Do not claim build/tests pass without running the exact commands and seeing successful output.

## Extensibility rule

Do not add MediatR, AutoMapper, Dapper, runtime plugin systems, event buses, HTTP/OAuth, cloud sync, CRDT/event sourcing, MCP Tasks, source ingestion, or another framework because it might be useful later.

A demonstrated product requirement comes first; then update architecture/ADR/tests.
