# Hero Passport v3.2 — Consolidated Product and Architecture Specification

**Status:** Accepted design baseline  
**Snapshot:** 2026-08-11  
**Target:** 0.1.0 local-first MVP  
**Supersedes:** v3/v3.1 quest lifecycle, deduplication, Hero binding, Trust/Risk, MCP inventory, and orchestration decisions

## 1. Product thesis

Hero Passport is a local-first RPG companion for people working with AI coding agents. It turns meaningful agent-assisted work into durable progression without becoming employee monitoring, source-code telemetry, an agent scheduler, or an LLM evaluator.

Primary experience:

```text
user asks agent to do meaningful work
        ↓
Hero Passport Agent Skill recognizes a quest boundary
        ↓
hero.start_quest
        ↓
normal agent work
        ↓
Skill recognizes completion and collects bounded facts
        ↓
hero.finish_quest
        ↓
deterministic local game engine
        ↓
XP / skills / level / rank / Trust / Strain / unlocks
        ↓
compact RPG result
```

The intended UX is ambient: users normally do not manually start or finish quests. Manual start/finish/abandon controls remain explicit overrides and recovery tools.

## 2. Product boundary

### 2.1 MVP components

0.1 ships three first-class components:

1. **Hero Passport MCP Core** — authoritative state, invariants, deterministic game rules and local persistence.
2. **Hero Passport Agent Skill** — lifecycle orchestration, bounded fact reporting and presentation policy.
3. **CLI** — onboarding, diagnostics, administration, recovery and scriptable local operations.

A local Web UI is deferred to 0.2.

### 2.2 Skill/Core responsibility split

The Skill may:

- decide whether meaningful work is beginning;
- generate `startRequestId`, `title` and `goal`;
- call MCP tools;
- carry `questId` between calls;
- recover an open quest;
- recognize goal changes and completion;
- report bounded facts it observed;
- lightly adapt curated flavor text;
- render the canonical result attractively.

The Skill must not:

- choose XP amounts;
- choose Trust/Strain deltas;
- invent level/rank/skill progress;
- unlock Traits/Titles on its own;
- rewrite a persisted outcome.

The Core owns all game truth.

## 3. Quest model

### 3.1 Unit of play

One **meaningful goal** is one Quest. A chat session may contain zero, one, or several quests. An MCP connection/session is never a Quest boundary.

Examples that usually start a Quest:

- implementing a feature;
- fixing a bug;
- refactoring;
- debugging/testing;
- architecture/design work that produces a concrete project result;
- code review;
- documentation work;
- project-specific research required to make a decision or implementation.

Examples that normally do not:

- casual conversation;
- a short factual explanation;
- a one-line syntax question;
- clarifying discussion before meaningful work begins.

### 3.2 Conservative automatic start

The Skill starts automatically only when it is confident meaningful project work is beginning. If uncertain, it does not interrupt the user with “start a quest?”; it waits until the interaction clearly transitions to work. A user can explicitly request a start at any time.

Start presentation is intentionally tiny, for example:

```text
⚔ Исправить повторный запуск Quest
```

The title is generated for the current task; it is not a fixed phrase.

### 3.3 Conservative automatic finish

The Skill finishes automatically only when it is confident the current goal is done and it is about to present the final result. Asking the user for a project decision, waiting for input, or saying more work remains keeps the Quest open.

Open quests do not expire from inactivity or agent restart.

### 3.4 Goal switch

If the current goal is completed and the user clearly moves to another independent goal:

1. finish the completed Quest;
2. start the new Quest automatically.

If the user explicitly switches away before completion:

- useful completed subset exists -> finish old Quest as `partial`;
- no useful result exists -> `abandoned`;
- then start the new Quest.

If intent is ambiguous, do not silently close anything.

### 3.5 Recovery

Exactly one open Quest may exist for a `(HeroId, ProjectId)` pair.

After restart/handoff:

- if new work clearly matches the open Quest, resume it automatically and carry the same `questId`;
- otherwise expose continue / finish / abandon instead of guessing.

A Quest belongs to the Hero and project/work, not to an AI-agent identity. Different agents may continue the same Quest.

## 4. Start identity and idempotency

`QuestDedupKeyV1` and goal-derived open deduplication are retired.

Start uses two separate identities:

```text
startRequestId = caller-generated UUIDv7 identifying one start intent/request
questId        = server-generated UUIDv7 identifying the durable Quest
```

Required behavior:

```text
same startRequestId + same canonical start arguments
  -> return same persisted start outcome / same questId

same startRequestId + different canonical arguments
  -> HP135 idempotency_conflict

new startRequestId while an open Quest already exists for Hero+Project
  -> HP133 active_quest_exists

new startRequestId after previous Quest finished
  -> may create a new Quest even with identical title/goal
```

Persist the request identity beyond Quest completion. Recording request identity and creating the Quest occur in one atomic write transaction.

This separates retry identity from work identity and makes `hero.start_quest` genuinely idempotent for repeated identical arguments.

## 5. Quest start data

Required model-facing start data:

```text
startRequestId
questType
title
goal
```

`title` is concise human-readable task naming. `goal` is the precise technical goal. Both use SafeText normalization with separate bounds.

Recommended bounds:

```text
title  1..120 Unicode scalars
goal   1..500 Unicode scalars
```

Quest effective locale is snapped at start for deterministic presentation of that Quest; game state itself stores semantic keys/data rather than translated text.

## 6. Finish report and provenance

The agent sends bounded facts, never a quality score or XP.

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

Outcome enum:

```text
success
partial
blocked
failed
abandoned
```

Status enum:

```text
not_run
passed
failed
unknown
```

Evidence enum:

```text
observed   # agent directly ran/saw the result
reported   # user or another source stated it
none       # no evidence / not applicable
```

Cross-field validation is explicit. For example `testsStatus=passed|failed` cannot have `testsEvidence=none`; a testing reward that claims execution requires `observed` evidence.

The product does not ingest raw test/build logs to prove these claims.

## 7. Reward engine

Game calculation is deterministic, versioned and integer-only.

### 7.1 Base XP

Canonical quest-type base XP remains:

| Type | Base XP |
|---|---:|
| planning | 30 |
| research | 40 |
| coding | 60 |
| review | 50 |
| debugging | 70 |
| documentation | 40 |
| maintenance | 40 |

### 7.2 Bonuses and penalties

`reward/2.0.0` initial policy:

```text
observed tests passed       +10
clean scope                 +10
clear summary               +10
no user corrections          +5
scope violation              -5 each, cap -15
user correction              -5 each, cap -15
```

The canonical clean coding golden remains 95 XP before the success multiplier:

```text
60 base + 10 tests + 10 clean scope + 10 summary + 5 no corrections = 95
```

### 7.3 Outcome multiplier

Applied after bonuses/penalties:

```text
success    ×1.00
partial    ×0.60
blocked    ×0.30
failed     ×0.10
abandoned  ×0.00
```

Formula:

```text
rawXp = max(0, baseXp + bonuses - penalties)
questXp = floor(rawXp * outcomePermille / 1000)
```

No elapsed-time, token, line-count, diff-size or agent-reported complexity multiplier exists.

### 7.4 Skill XP

The agent selects 1–3 actually used canonical skills in primary/secondary/tertiary order. The server distributes the final Quest XP deterministically:

```text
1 skill  -> 100%
2 skills -> 60/40
3 skills -> 50/30/20
```

Use cumulative-floor allocation so allocations always sum exactly to `questXp`.

Using Hero Passport itself never qualifies as `tool_use` XP.

## 8. Hero progression

Hero XP and each Skill XP have separate soft-increasing progression tables. Tables are versioned data, not hidden adaptive formulas. Exact thresholds are game content and golden-tested.

Ranks are large cosmetic milestones and never gate features or multiply XP. Initial editorial direction:

```text
Code Squire
Senior Warrior
Staff Paladin
Principal Warlord
Legendary Architect
```

Exact level thresholds are versioned content.

Rank/level/title/trait milestones may include short curated RPG/engineering flavor lines. Humor is reserved for meaningful milestones rather than every Quest.

## 9. Trust and Strain

`Risk` is retired and replaced by `Strain`.

```text
Trust  0..100  # demonstrated reliability
Strain 0..100  # accumulated technical friction/turbulence
```

They change only from game events; no time-based regeneration exists.

`trust-strain/1.0.0` is a deterministic event table. Initial policy direction:

```text
clean successful Quest      Trust +2, Strain -2
success with minor issues   Trust +1, Strain -1
partial                     Trust +0, Strain +1
blocked                     Trust +0, Strain +0/+1
failed                      Trust +0, Strain +2
scope violation             Trust -1, Strain +1 each
user correction             Trust -1, Strain +1 each
observed tests passed       may add Trust +1 within capped per-quest policy
abandoned                   Trust +0, Strain +0
```

The exact non-overlapping composition table must be fixed by golden fixtures before implementation. Trust/Strain do not directly multiply XP or lock features in 0.1.

## 10. Streak, Traits and Titles

Success Streak:

- increments only on `success`;
- breaks on `partial`, `blocked`, `failed`, `abandoned`;
- grants no XP multiplier;
- may unlock cosmetic milestones.

Traits are permanent collected characteristics/badges. Titles are cosmetic labels; exactly one active Title is selected automatically by deterministic priority. Manual title equipment is deferred.

Unlock sources are deliberately hybrid:

- Hero-level milestones;
- Skill-level milestones;
- success streak/milestones;
- rare behavioral conditions.

No Trait/Title provides mechanical advantage in 0.1.

## 11. Hero model

One Hero can progress globally across many projects, while each project keeps its own compact statistics/history projection.

Multiple Heroes are supported locally:

```text
create
list
activate
archive
restore
permanent delete
```

There is one globally active Hero used as the default for **new** quests. A Quest captures its `heroId` at start and never changes owner if the active Hero changes later.

Archived Heroes are hidden from ordinary selection but recoverable. Permanent deletion is explicit and irreversible. A Hero with any open Quest cannot be archived or permanently deleted until that Quest is finished or abandoned.

## 12. Project model

Hero progression is global. Per `(Hero, Project)` projections include at minimum:

```text
quests started/finished/succeeded
XP earned in project
success rate (derived)
last quest
recent history
skill contribution statistics
```

Project identity remains `project-identity/1`: Git common-dir based for repositories, linked worktrees share identity, submodules/nested repositories remain separate by default, and no remote URL/full workspace path is persisted.

## 13. First-run onboarding and localization

First-run setup is explicit persisted application state.

Default short wizard:

1. language;
2. initial Hero name;
3. presentation style (`rpg_engineering` default);
4. automatic start/finish preferences;
5. confirm and create.

CLI uses `hero-passport init` interactively.

MCP stdio never prints wizard prompts into stdout. If setup is incomplete, gameplay mutations fail with `HP001 setup_required`; the Agent Skill conducts conversational onboarding and calls `hero.configure` with validated setup values.

0.1 ships `ru-RU` and `en-US`. Domain/Application return semantic keys and values; localization/presentation lives in App/Skill adapters. Missing localization keys fail tests rather than silently changing game semantics.

## 14. MCP surface — HP-MCP/2 v3.2

Static deterministic tool order:

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

High-level annotation intent:

```text
configure      mutation, non-destructive, idempotent for same complete settings
create         additive mutation, idempotent only when caller request identity is provided; otherwise false
list           read-only
activate       mutation, idempotent
archive        mutation, non-destructive, idempotent
restore        mutation, non-destructive, idempotent
delete         destructive, idempotent only after canonical target semantics are fixed
start_quest    additive mutation, idempotent via required startRequestId
finish_quest   mutation, idempotent via questId persisted outcome
list_active    read-only
get_card       read-only
```

Exact annotation matrix belongs in `WIRE-CONTRACT.md`; annotations are hints, never security controls.

For destructive permanent delete, the tool contract must require an explicit confirmation token/string derived from the target identity (for example `confirmHeroName`) so accidental model invocation is rejected server-side. Host confirmation UX may still exist independently.

## 15. MCP protocol policy

Baseline SDK: official C# `ModelContextProtocol 2.1.0`.

Preferred semantics: MCP `2026-07-28`, with qualification for `2025-11-25` compatibility handled by the official SDK. Application correctness never depends on protocol sessions/connections.

`questId` is the explicit state handle carried across calls. MCP tool result structured data is authoritative; when `structuredContent` is returned, one JSON TextContent compatibility representation remains semantically equal to it.

MCP Tasks are not used for Hero Quest lifecycle because the actual work occurs outside the Hero Passport tool call and Hero Passport mutations are short local operations.

## 16. Local persistence and concurrency

SQLite remains authoritative local state using EF Core and Microsoft.Data.Sqlite.

Write policy:

```text
same-host local filesystem
WAL
synchronous=FULL
foreign_keys=ON
Cache=Default
Default Timeout=5
short-lived DbContext from IDbContextFactory
short non-deferred Serializable writer transactions
```

Start transaction:

```text
validate canonical request outside DB
BEGIN writer intent before invariant reads
lookup persisted startRequestId
  same args -> return persisted start outcome
  different args -> HP135
check open Hero+Project Quest
  exists -> HP133 active_quest_exists
insert start request + Quest + projection updates
COMMIT
```

Finish transaction atomically commits report, XP ledger, Hero/Skill/Trust/Strain/Trait/Title/Streak/project updates and finished Quest state. `UNIQUE quest_reports.quest_id` and `UNIQUE xp_events.quest_id` are durable backstops.

Concurrency semantics are **at-most-once committed progression per Quest**, not “handler executes exactly once”.

No custom writer mutex, Polly retry layer, lease, heartbeat or agent-owner lock is introduced.

## 17. Local-first, sync-ready

0.1 has no account/cloud/sync dependency. The data model is prepared for later optional sync by using:

- UUIDv7 entity identities;
- immutable completion/XP facts;
- explicit timestamps and rule/schema versions;
- public identity independent of SQLite rowid;
- explicit archive/delete semantics;
- deterministic conflict-sensitive operations.

No CRDT/event-sourcing framework is added preemptively. A future sync product requires its own conflict and deletion architecture.

## 18. Privacy and evidence boundary

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
arbitrary metadata/context payload bags
```

The system stores compact Quest title/goal/summary, bounded status/evidence facts, game results and progression deltas.

This is a companion, not an independent auditor. A future verifier that reads code/logs would require a separate explicit privacy design and is not part of 0.1.

## 19. Presentation

Core result data includes semantic reward/progression components. The Skill renders them; the MCP result also carries compact fallback `displayText`.

Typical finish presentation:

```text
+60 XP  Базовая награда
+10 XP  Тестирование
+10 XP  Бонус за контроль
+10 XP  Итоговый отчёт
 +5 XP  Без исправлений

↑ Coding             +48 XP
↑ Testing Awareness  +29 XP
↑ Scope Control      +18 XP
★ Level 7 → 8

XP       +95
Level    7 → 8
Trust   52 → 54
Strain  18 → 16
Streak       6 🔥
```

Milestone flavor may follow, for example a rank-up quip. The Skill may contextualize curated flavor but must not alter numeric facts.

## 20. Non-goals through 0.1

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

## 21. Required implementation evidence

Before 0.1 release prove at minimum:

```text
SafeText and localization vectors
UUIDv7/timestamp/JSON-safe integer vectors
startRequestId retry + mismatch behavior
concurrent two-start race -> exactly one open Hero+Project Quest
concurrent finish -> one committed progression event
crash before/after commit recovery
Hero switch ownership invariants
archive/delete open-Quest guards
outcome/XP golden vectors
skill XP conservation
Trust/Strain table vectors
level/skill/rank threshold vectors
streak/unlock vectors
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

## 22. Normative precedence

After v3.2 consolidation, active normative order is:

1. this consolidated design for product semantics;
2. `WIRE-CONTRACT.md` for exact MCP wire behavior;
3. `PERSISTENCE-RELIABILITY.md` for SQLite/write/crash/backup behavior;
4. `PROJECT-IDENTITY.md` for project identity;
5. `ENGINE-SPEC.md` for exact deterministic game rules;
6. `AGENT-SKILL.md` for agent orchestration behavior;
7. compact overview docs for summaries only.

Any v3.1 statement about `QuestDedupKeyV1`, 16 concurrent open quests, `Risk`, four-only MCP tools, or session-shaped Quest identity is superseded and must not survive as an active requirement.
