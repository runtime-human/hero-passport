# Hero Passport v3.2 — Consolidated Product and Architecture Specification

**Status:** Accepted design baseline  
**Snapshot:** 2026-08-11  
**Target:** 0.1.0 local-first MVP  
**Supersedes:** v3/v3.1 quest lifecycle, deduplication, Hero binding, Trust/Risk, MCP inventory, and orchestration decisions

This document is the consolidated semantic baseline. Exact MCP fields are normative in `WIRE-CONTRACT.md`; exact game math in `ENGINE-SPEC.md`; SQLite behavior in `PERSISTENCE-RELIABILITY.md`; project identity in `PROJECT-IDENTITY.md`; agent orchestration in `AGENT-SKILL.md`.

## 1. Product thesis

Hero Passport is a local-first RPG companion for people working with AI coding agents. It converts meaningful agent-assisted work into durable progression without becoming employee monitoring, source-code telemetry, an agent scheduler, or an LLM evaluator.

Primary experience:

```text
meaningful user goal
        ↓
Hero Passport Agent Skill recognizes a Quest boundary
        ↓
hero.start_quest
        ↓
normal agent work
        ↓
Skill recognizes completion and reports bounded facts
        ↓
hero.finish_quest
        ↓
deterministic local game engine
        ↓
XP / Skills / Levels / Rank / Trust / Strain / unlocks
        ↓
compact RPG result
```

The intended UX is ambient. Users normally do not manually start or finish Quests. Manual start/finish/abandon remain explicit overrides and recovery tools.

## 2. MVP product boundary

0.1 ships three first-class components:

1. **Hero Passport MCP Core** — authoritative state, validation, deterministic rules and local persistence.
2. **Hero Passport Agent Skill** — lifecycle orchestration, bounded fact reporting and presentation.
3. **CLI** — onboarding, diagnostics, administration, recovery and local scripting.

Local Web UI is deferred to 0.2.

The Skill may decide when meaningful work begins/ends, generate start metadata, carry `questId`, recover an open Quest, report bounded facts and render canonical results. It must never choose XP, Trust/Strain deltas, Levels, Ranks, Traits or Titles. The Core owns all game truth.

## 3. Quest semantics

One **meaningful goal** is one Quest. A chat may contain zero, one or several Quests. MCP connection/session lifetime is never Quest identity.

Typical Quest work includes implementation, debugging, testing, refactoring, review, documentation, project-specific research and architecture/design that produces a concrete result.

Casual conversation, a short factual explanation, a one-line syntax question or clarification before work begins normally does not create a Quest.

### 3.1 Conservative automatic start

The Skill starts only when it is confident meaningful project work is beginning. If uncertain, it does not ask “start a quest?”; it waits until work becomes clear. The user may explicitly request a start at any time.

Default start presentation is one compact title line, for example:

```text
⚔ Добавить first-run onboarding
```

### 3.2 Conservative automatic finish

The Skill finishes only when the current goal is genuinely complete and it is ready to present the final work result. Waiting for user input, verification still in progress, or known remaining work keeps the Quest open.

Open Quests do not expire because of inactivity, process restart or agent restart.

### 3.3 Goal switch

If a completed goal is followed by a new independent goal, finish the old Quest and start the new one automatically.

If the user explicitly switches away before completion:

```text
useful completed subset exists -> partial
no useful completed result     -> abandoned
then start the new Quest
```

If intent is ambiguous, do not silently close anything.

### 3.4 Recovery and handoff

Exactly one open Quest may exist for a `(HeroId, ProjectId)` pair.

After restart/handoff:

- clearly matching work resumes the same `questId`;
- a different or ambiguous goal surfaces recovery choices instead of guessing.

A Quest belongs to Hero + Project/work, not to an AI agent identity. Different agents may continue the same Quest.

## 4. Start identity and idempotency

`QuestDedupKeyV1` and goal-derived open deduplication are retired.

Start has two separate identities:

```text
startRequestId = caller-generated UUIDv7 for one start intent/retry sequence
questId        = server-generated UUIDv7 for the durable Quest
```

Required behavior:

```text
same startRequestId + same canonical args
  -> same persisted start outcome / same questId

same startRequestId + different canonical args
  -> HP135 idempotency_conflict

new startRequestId + already-open Quest for Hero+Project
  -> HP133 active_quest_exists

new startRequestId after completion
  -> may create a new Quest even with identical title/goal text
```

Request identity and Quest creation are persisted atomically. Natural-language similarity is never retry identity.

Create Hero and permanent Delete Hero use the same architectural pattern with their own caller request IDs and minimal durable mutation receipts.

## 5. Quest start data

Model-facing start data:

```text
startRequestId
questType
title
goal
```

`title` is short human-readable task naming. `goal` is the precise technical goal. Both are SafeText-normalized.

Exact bounds are in `WIRE-CONTRACT.md`; current values are title `1..120` and goal `1..500` Unicode scalars.

The Quest snapshots its effective locale at start so presentation remains stable even if global preferences change later.

## 6. Finish report and provenance

The agent reports bounded facts, never a quality score or XP:

```text
result
summary
skillsUsed[1..3]
metrics.testsMentioned
metrics.scopeViolations
metrics.userCorrections
metrics.buildStatus
metrics.buildEvidence
metrics.testsStatus
metrics.testsEvidence
```

Outcome:

```text
success
partial
blocked
failed
abandoned
```

Execution status:

```text
not_run
passed
failed
unknown
```

Evidence:

```text
observed   # agent directly ran/saw the result
reported   # user or another source stated it
none       # no supporting observation/report
```

Cross-field validation is explicit. Only directly observed passed tests qualify for the testing XP bonus.

Hero Passport does not ingest source code, diffs or raw test/build logs to independently prove these facts.

## 7. Reward engine

Game calculation is deterministic, versioned and integer-only.

Quest-type base XP:

| Type | Base XP |
|---|---:|
| planning | 30 |
| research | 40 |
| coding | 60 |
| review | 50 |
| debugging | 70 |
| documentation | 40 |
| maintenance | 40 |

`reward/2.0.0` bonuses/penalties:

```text
observed tests passed       +10
clean scope                 +10
clear summary               +10
no user corrections          +5
scope violation              -5 each, cap -15
user correction              -5 each, cap -15
```

Outcome multiplier:

```text
success    ×1.00
partial    ×0.60
blocked    ×0.30
failed     ×0.10
abandoned  ×0.00
```

Formula:

```text
rawXp   = max(0, baseXp + bonuses - penalties)
questXp = floor(rawXp * outcomePermille / 1000)
```

Canonical clean coding golden remains:

```text
60 + 10 + 10 + 10 + 5 = 95 XP
```

No elapsed-time, token, line-count, diff-size or agent-reported complexity multiplier exists.

Exact goldens and rule versions are normative in `ENGINE-SPEC.md`.

## 8. Skill progression

The agent selects 1–3 actually used canonical Skills in primary/secondary/tertiary order. Hero Passport distributes final Quest XP deterministically:

```text
1 skill  -> 100%
2 skills -> 60/40
3 skills -> 50/30/20
```

Cumulative-floor allocation must conserve XP exactly. Calling Hero Passport itself never qualifies as `tool_use` XP.

Each Skill has independent XP and level progression, separate from Hero Level.

## 9. Hero Level and Rank

Hero XP and each Skill XP use separate soft-increasing, versioned threshold tables. They are static game content, not hidden adaptive formulas.

Rank is a large cosmetic milestone derived from Hero Level and never gates features or multiplies XP.

Initial rank keys/English labels:

```text
Code Squire
Code Knight
Senior Warrior
Staff Paladin
Principal Warlord
Legendary Architect
```

Milestone flavor is curated RPG/engineering writing. Humor is reserved for meaningful Level/Rank/Title/Trait/Streak events rather than every Quest.

## 10. Trust and Strain

`Risk` is retired and replaced by `Strain`.

```text
Trust  0..100  # demonstrated reliability
Strain 0..100  # accumulated technical friction/turbulence
```

They change only from Quest/game events; no time-based regeneration exists. They never directly multiply XP or lock product functionality in 0.1.

Exact `trust-strain/1.0.0` composition is normative in `ENGINE-SPEC.md`. Current exact outcome components are:

```text
success    Trust +1, Strain -1
partial    Trust +0, Strain +1
blocked    Trust +0, Strain +0
failed     Trust +0, Strain +2
abandoned  Trust +0, Strain +0
```

Additional deterministic quality components include clean successful completion, observed passed tests, scope violations and user corrections, with per-Quest caps specified in the engine contract.

## 11. Streak, Traits and Titles

Success Streak:

- increments only on `success`;
- resets on `partial`, `blocked`, `failed`, `abandoned`;
- grants no XP multiplier;
- may unlock cosmetic milestones.

Traits are permanent collected cosmetic characteristics/badges. Titles are cosmetic labels; one active Title is selected automatically by deterministic priority. Manual title equipment is deferred.

Unlock sources are hybrid:

- Hero-level milestones;
- Skill-level milestones;
- Success Streak milestones;
- rare behavioral conditions.

No Trait/Title provides a mechanical advantage in 0.1.

## 12. Hero model

A Hero progresses globally across projects. Each project keeps a separate compact statistics/history projection for that Hero.

Multiple local Heroes are supported:

```text
create
list
activate
archive
restore
permanent delete
```

One globally active Hero is the default owner of **new** Quests. A Quest captures its `heroId` at start and never changes owner if the active Hero later changes.

Archive is reversible. Permanent delete is explicit and irreversible. A Hero with any open Quest cannot be archived or deleted. The globally active Hero must first be replaced by another active Hero before archive/delete.

Permanent delete removes the Hero’s local game/history data but keeps the minimal non-content mutation receipt needed to make a late delete retry safe.

## 13. Project model

Hero progression is global. Per `(Hero, Project)` projections include at minimum:

```text
quests started/finished/succeeded
XP earned in project
success rate
last Quest
recent history
Skill contribution statistics
```

`PROJECT-IDENTITY.md` remains normative: Git common-dir based identity, linked worktrees share identity, deliberate monorepo scopes are explicit, submodules/nested repositories are separate by default, and no full workspace path or Git remote URL is persisted.

## 14. First-run onboarding and localization

First-run setup is persisted application state.

Short onboarding:

1. language;
2. initial Hero name;
3. presentation style (`rpg_engineering` default);
4. automatic start preference;
5. automatic finish preference and confirmation.

CLI uses `hero-passport init` interactively.

MCP stdio never prints wizard prompts into protocol stdout. Until setup is complete, `hero.configure` is allowed and gameplay/Hero tools return `HP001 setup_required`; the Agent Skill may conduct onboarding conversationally and submit the completed settings.

0.1 ships `ru-RU` and `en-US`. Domain/Application expose semantic keys and values; App/Skill render localized text. Missing resource keys are test failures.

## 15. HP-MCP/2 v3.2 surface

Static deterministic order:

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

No host-specific aliases and no assembly-wide scanning.

Annotations are hints, never security controls. Exact annotation/schema/result/error contracts are normative in `WIRE-CONTRACT.md`.

Permanent delete is explicitly destructive and requires target confirmation in the server contract even if a host also has its own confirmation UI.

## 16. MCP protocol policy

Baseline SDK: official C# `ModelContextProtocol 2.1.0`.

Preferred semantics: MCP `2026-07-28`, with release qualification against `2025-11-25` compatibility behavior supported by the official SDK.

Application correctness never depends on MCP sessions/connections. `questId` is an explicit durable application handle carried between calls.

Canonical `structuredContent` is authoritative. The result also carries exactly one minified JSON TextContent semantically equal to structured content for compatibility.

MCP Tasks are not used for Hero Quest lifecycle: actual coding-agent work occurs outside Hero Passport calls, while Core operations are short local reads/mutations.

## 17. Local persistence and concurrency

SQLite remains authoritative local state through EF Core/Microsoft.Data.Sqlite.

Supported profile:

```text
same-host local filesystem
SQLite runtime >= 3.53.4
WAL
synchronous=FULL
foreign_keys=ON
Cache=Default
Default Timeout=5
IDbContextFactory
short non-deferred Serializable writer transactions
```

Every read-modify-write mutation acquires writer intent before invariant reads.

Start atomically checks request replay/mismatch, checks one-open Hero+Project invariant, inserts Quest + mutation receipt and updates projections.

Finish atomically commits report, XP ledger, Hero/Skill/Trust/Strain/Streak/Trait/Title/project updates and finished state.

Durable backstops include:

```text
UNIQUE mutation receipt operation+request ID
partial UNIQUE open Quest per hero_id+project_id
UNIQUE quest_reports.quest_id
UNIQUE xp_events.quest_id
```

Correct claim: **at-most-once committed progression per Quest**, not “handler executes exactly once”.

No custom writer mutex, separate Polly retry layer, lease, heartbeat or agent-owner lock is introduced.

## 18. Multi-agent behavior

Different agents may resume/finish the same Quest when the work matches. Hero Passport does not coordinate their code edits and does not elect an owner/leader.

Two simultaneous Hero Passport mutations are resolved by SQLite/application invariants. A concurrent Finish can produce only one durable progression outcome; later retries return the persisted original result.

## 19. Local-first, sync-ready

0.1 has no account, cloud backend or sync dependency.

The data model keeps a future optional sync seam through UUIDv7 identities, immutable completion facts, explicit timestamps/rule versions and deliberate archive/delete semantics.

No CRDT/event-sourcing framework is added preemptively. Future sync requires a separate conflict/deletion/security design.

## 20. Privacy and evidence boundary

Hero Passport intentionally does not request/persist routine:

```text
source/file contents
diffs/patches
raw terminal/build/test logs
full prompts/chat transcripts
secrets/tokens/API keys
environment dumps
full workspace paths
Git remote URLs
continuous editor/activity telemetry
arbitrary metadata/context bags
```

Stored Quest history is compact: title/goal/summary, bounded statuses/evidence, immutable reward breakdown and progression deltas.

Hero Passport is a companion, not an independent auditor. A future verifier that reads code/logs requires a separate explicit privacy design.

## 21. Presentation

Core result data is semantic and deterministic. The Skill renders it and may lightly contextualize curated milestone flavor without changing any numeric/game fact.

Typical finish shape:

```text
+60 XP  Базовая награда
+10 XP  Тестирование
+10 XP  Бонус за контроль
+10 XP  Итоговый отчёт
 +5 XP  Без исправлений

↑ Coding             +47 XP
↑ Testing Awareness  +29 XP
↑ Scope Control      +19 XP
★ Level 7 → 8

XP       +95
Level    7 → 8
Trust   52 → 54
Strain  18 → 16
Streak       6 🔥
```

The exact 95-XP three-Skill cumulative-floor distribution is `47/29/19`.

## 22. Non-goals through 0.1

Do not add:

```text
Web dashboard
cloud account/sync
team/shared progression
continuous telemetry
source/diff ingestion
raw log collection
LLM judge
XP from time/tokens/lines
agent ownership/leases/heartbeats
MCP Tasks for Quest lifecycle
own Streamable HTTP/OAuth
runtime plugin framework
REST/GraphQL/gRPC
random loot/items economy
mechanical title/trait/rank bonuses
manual title equipment
```

## 23. Required implementation evidence

Before 0.1 release prove at minimum:

```text
SafeText and localization vectors
UUIDv7/timestamp/JSON-safe integer vectors
startRequestId retry + mismatch behavior
concurrent two-start race -> exactly one open Hero+Project Quest
concurrent Finish -> one committed progression event
crash before/after commit recovery
Hero switch ownership invariants
archive/delete open-Quest guards
outcome/XP goldens
Skill XP conservation
Trust/Strain vectors
Hero/Skill/Rank threshold vectors
Streak/unlock vectors
bounded evidence consistency
MCP schema/order/annotation snapshots
structuredContent == parsed compatibility TextContent
MCP 2026-07-28 + 2025-11-25 qualification
Agent Skill trigger/start/finish/recovery evals
CLI and MCP first-run onboarding
RU/EN localization completeness
SQLite version/WAL/backup/migration qualification
Codex reference E2E and cross-host smoke
```

## 24. Normative precedence

For implementation details use:

1. this consolidated design for cross-subsystem product semantics;
2. `WIRE-CONTRACT.md` for exact MCP wire behavior;
3. `PERSISTENCE-RELIABILITY.md` for SQLite/write/crash/backup behavior;
4. `PROJECT-IDENTITY.md` for project identity;
5. `ENGINE-SPEC.md` for exact deterministic game math/content tables;
6. `AGENT-SKILL.md` for agent orchestration;
7. overview/roadmap/reference docs only as summaries.

Any v3.1 statement about `QuestDedupKeyV1`, 16 concurrent open Quests, `Risk`, four-only MCP tools, MCP SDK 2.0.0, or session-shaped Quest identity is superseded and must not survive as an active requirement.
