# AGENTS.md — Hero Passport

## Mission

Build Hero Passport as a local-first RPG companion for people working with AI coding agents.

0.1 is **MCP Core + official Agent Skill + CLI**. Web is 0.2.

Do not turn the product into source-code telemetry, employee monitoring, an LLM judge, an agent scheduler or a cloud service.

## Read before coding

Always read:

```text
docs/PRODUCT-SPEC.md
docs/ARCHITECTURE.md
```

Then the focused contract:

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
Implementation plan     docs/superpowers/plans/2026-08-11-hero-passport-v3.2.1-implementation.md
```

`docs/README.md` defines precedence.

## Core architecture

```text
Domain <- Application <- Infrastructure <- App
```

Agent Skill is a portable orchestration package outside authoritative game logic.

No separate Contracts assembly in 0.1.

## Critical v3.2.1 invariants

```text
Core Quest = explicitly started durable progression unit
one coherent meaningful goal = Skill heuristic, not server truth
one open Quest per Hero + Project
linked worktrees share Project -> same-Hero parallel independent Quests unsupported in 0.1
Quest owner fixed at start
Quest is not owned by an AI agent
active Hero is a default preference, not hidden Start ownership
Start carries explicit heroId
bootstrapRequestId/createRequestId/startRequestId/finishRequestId are retry identities
questId is durable work identity
natural-language goal is never idempotency identity
Finish conflicting finalization is HP136; history never overwritten
at-most-once committed progression per Quest
```

## Game authority

Agent sends **bounded attestations/reported signals**, not XP or a quality score.

`observed` means the agent asserts it directly saw/ran a result; Hero Passport does not independently verify raw evidence.

Core calculates:

```text
XP
Skill XP
Hero/Skill levels
Rank
Trust/Strain
Streak
Traits/Titles
semantic milestones
```

Canonical clean coding golden for `reward/2.0.0` is **95 XP**.

Trust/Strain are RPG stats, not objective productivity telemetry.

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

Quest title/goal/summary can still be sensitive local project metadata; do not imply SafeText redacts secrets.

## MCP

Official C# SDK baseline: `ModelContextProtocol 2.1.0`.

Preferred semantics: MCP `2026-07-28`, with `2025-11-25` compatibility qualification.

Application correctness never depends on MCP session/connection state.

Current tool inventory/order is normative in `WIRE-CONTRACT.md`; register explicitly.

`hero.delete` is not model-facing in 0.1; permanent logical delete is CLI-only.

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
trusted_schema=OFF
Cache=Default
IDbContextFactory
```

WAL/runtime are initialization/qualification concerns. Connection-scoped policy (`FULL`, foreign keys, trusted_schema) must hold on every product connection, including pooled/new-process paths.

All invariant read-modify-write operations acquire writer intent before invariant reads.

No custom global writer mutex. No Polly retry layer. Never delete WAL/SHM for recovery.

EF SQLite abandoned `__EFMigrationsLock` is diagnosed by doctor and repaired only via explicit safe administration, never silently at ordinary startup.

## Development method

Follow the v3.2.1 **risk-first** implementation plan.

Use TDD for every product-code task:

```text
write failing test
run and observe failure
minimal implementation
run and observe pass
refactor
focused commit
```

Use real file-backed SQLite for persistence/concurrency/crash claims.

Do not claim build/tests pass without executing and observing the exact commands.

## Pre-code checkpoint

Before full RPG expansion, prove the real vertical loop:

```text
scaffold/dependency restore
SQLite/migrations/connection policy
project identity
bootstrap/get_context
minimal Start
minimal Finish/base XP
real MCP adapter
minimal packaged Skill
Codex E2E + restart/retry/race/crash
```

Only then expand the complete RPG layers.

## Extensibility rule

Do not add MediatR, AutoMapper, Dapper, runtime plugin systems, event buses, HTTP/OAuth, cloud sync, CRDT/event sourcing, MCP Tasks, source ingestion or another framework because it might be useful later.

Future sync language is **sync-conscious**, not sync-ready: current local data choices do not solve cross-device identity/conflicts/deletion/causality.
