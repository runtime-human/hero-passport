# Hero Passport

> Local-first RPG companion for people working with AI coding agents.

Hero Passport turns meaningful agent-assisted project work into persistent XP, Skills, Levels, Ranks, Trust/Strain, Streaks, Traits and Titles — without collecting source code or requiring a cloud account.

## Experience

```text
Agent Skill hydrates Hero Passport context
        ↓
meaningful project work begins
        ↓
Skill starts/resumes an explicit durable Quest
        ↓
agent works normally
        ↓
Skill finishes when the goal is genuinely done
        ↓
Core calculates deterministic progression
        ↓
compact RPG result
```

“One coherent meaningful goal per Quest” is a conservative Skill heuristic. Core truth is simpler: a Quest is an explicitly started durable progression unit.

Typical start:

```text
⚔ Добавить first-run onboarding
```

Canonical clean coding finish is **95 XP** before any future rule-version change.

## v3.2.1 implementation baseline — 6 September 2026

```text
C# 14 / .NET 10 LTS / SDK 10.0.302
ModelContextProtocol 2.2.0
MCP semantics 2026-07-28; qualification path 2025-11-25
EF Core SQLite / Microsoft.Data.Sqlite 10.0.10
SQLitePCLRaw.bundle_e_sqlite3 3.0.5
qualified actual SQLite runtime >= 3.53.4
System.CommandLine 2.0.10
xunit.v3 3.2.2
```

The v3.2.1 product/architecture contract remains the 11 August 2026 baseline; dependency qualification is refreshed independently as implementation proceeds.

0.1 ships MCP Core + Agent Skill + CLI. Local Web UI is 0.2.

## Durable identity and retries

Hero Passport separates retry intent from game/work identity:

```text
bootstrapRequestId  caller bootstrap retry identity
createRequestId     caller Hero-create retry identity
startRequestId      caller Start retry identity
finishRequestId     caller Finish retry identity
questId             server-generated durable Quest identity
```

Natural-language goal text is never an idempotency key.

Start also carries explicit `heroId`; global active Hero is only a default preference. Process-bound `ProjectId` participates in canonical Start retry scope.

A Quest belongs to persisted Hero + Project, not Codex/Claude/another agent.

## Recovery

`hero.get_context` hydrates a fresh/restarted Skill with:

```text
setup/settings
Core/Skill/contract versions
active default Hero
current Project
all open Quests in that Project across Heroes
rule versions
```

Exactly one Quest may be open per Hero+Project. Linked Git worktrees share one Project identity, so 0.1 deliberately does not support parallel independent same-Hero Quests across linked worktrees.

## HP-MCP/2 v3.2.1

Current explicit tool order:

```text
hero.bootstrap
hero.configure
hero.get_context
hero.create
hero.list
hero.activate
hero.archive
hero.restore
hero.start_quest
hero.finish_quest
hero.get_card
```

The current count is a contract snapshot, not a forever architectural invariant.

Permanent Hero delete is **CLI-only** in 0.1; model-facing removal uses reversible archive/restore.

Exact schemas/results/errors: [`docs/WIRE-CONTRACT.md`](docs/WIRE-CONTRACT.md).

## Game rules

Core calculates progression deterministically from validated **bounded agent attestations**. The agent never chooses its XP/game deltas.

`observed` means the agent asserts it directly ran/saw the referenced result; Hero Passport does not independently inspect raw evidence.

Outcome multipliers:

```text
success    100%
partial     60%
blocked     30%
failed      10%
abandoned    0%
```

Full numeric rules: [`docs/ENGINE-SPEC.md`](docs/ENGINE-SPEC.md).

## Trust + Strain

Trust/Strain are transparent RPG stats derived from bounded Quest signals, not objective employee/productivity telemetry.

Both are `0..100`, Quest-driven only, have no passive clock regeneration, do not multiply XP and do not gate product functionality.

## Heroes

Multiple local Heroes remain in 0.1:

```text
create / list / activate / archive / restore
CLI permanent logical delete
```

Active Hero is the default for forming a new Start request. Start itself carries explicit HeroId; existing Quests never change owner.

## SQLite reliability

Supported writable profile:

```text
same-host local filesystem
WAL
synchronous=FULL
foreign_keys=ON
trusted_schema=OFF
Cache=Default
Pooling=True
Default Timeout=5
```

Critical DB state is protected by physical CHECK/FK/index invariants, versioned mutation receipts, non-deferred writer transactions and crash/recovery tests using real file-backed SQLite.

## Privacy

Hero Passport intentionally does not request/persist routine:

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

Quest title/goal/summary are still potentially sensitive local project metadata.

Permanent CLI delete is logical irreversible removal from the active Hero Passport database; Hero Passport does not claim forensic erasure from backups, filesystem snapshots or storage media.

## Local-first, sync-conscious

0.1 requires no account/cloud backend and implements no sync.

UUIDv7, immutable completed outcomes and rebuildable projections are **sync-conscious seams**, not a claim that cross-device Project identity, deletion, conflicts or causality are solved.

## Implementation strategy

Before implementing the complete RPG layers, prove a risk-first vertical slice:

```text
SQLite/migrations/connection policy
project identity
bootstrap/get_context
minimal Start
minimal Finish/base XP
real MCP adapter
minimal Agent Skill
Codex E2E + restart/retry/race/crash
```

Then add full reward/Skills/levels/Rank/Trust-Strain/Streak/Traits/Titles/localization/admin/release qualification.

Implementation plan:

- `docs/superpowers/plans/2026-08-11-hero-passport-v3.2.1-implementation.md`

## Documentation

Start at [`docs/README.md`](docs/README.md). The architecture PR is documentation-only and does **not** claim product build/test success before implementation exists.
