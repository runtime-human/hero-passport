# Hero Passport — Product Specification

**Status:** Accepted v3.2 product contract  
**Snapshot:** 2026-08-11  
**Target:** 0.1.0 local-first MVP

Normative consolidated design: [`superpowers/specs/2026-08-11-hero-passport-v3.2-design.md`](superpowers/specs/2026-08-11-hero-passport-v3.2-design.md).

## 1. Product

Hero Passport is a local-first RPG companion for people working with AI coding agents. It turns meaningful agent-assisted work into durable progression without collecting source code or requiring a cloud account.

It is **not** employee monitoring, code surveillance, an LLM quality judge, or an agent scheduler.

Primary loop:

```text
meaningful user goal
-> Hero Passport Agent Skill auto-starts a Quest
-> agent works normally
-> Skill auto-finishes when the goal is done
-> Core calculates deterministic progression
-> Skill renders compact RPG logs + summary
```

Manual start/finish/abandon remain explicit recovery/override actions, not the normal UX.

## 2. MVP surfaces

0.1 has three first-class surfaces:

```text
Hero Passport MCP Core
Hero Passport Agent Skill
CLI
```

Web UI is 0.2.

The Skill owns lifecycle recognition and presentation. The Core owns game truth, validation, persistence, idempotency and progression.

## 3. Quest semantics

One meaningful goal is one Quest. A chat may contain zero, one or several Quests. MCP connections/sessions are never Quest identity.

The Skill uses conservative automation:

- start only when meaningful project work is clearly beginning;
- do not ask “start a quest?” when uncertain;
- finish only when the current goal is genuinely complete and a final answer is about to be returned;
- no inactivity timeout;
- on restart, resume the existing Quest only when the goal clearly matches;
- on an explicit mid-work goal switch, old Quest becomes `partial` if useful work exists, otherwise `abandoned`;
- ambiguous switches do not silently mutate Quest state.

Exactly one Quest may be open for one `(HeroId, ProjectId)` pair.

A Quest belongs to a Hero + Project, never an agent brand/instance. Another agent may continue the same `questId`.

## 4. Start idempotency

Start requires caller-generated `startRequestId` and server-generated `questId`.

```text
same startRequestId + same canonical args -> same start outcome / same questId
same startRequestId + different canonical args -> HP135 idempotency_conflict
new startRequestId + open Quest for Hero+Project -> HP133 active_quest_exists
new startRequestId after completion -> may create a new Quest
```

Request identity is persisted atomically with Quest creation. Retry identity is never inferred from natural-language goal similarity.

## 5. Start/finish facts

Start:

```text
startRequestId
questType
title
goal
```

Finish:

```text
questId
result
summary
1..3 ordered canonical skills
bounded quality facts
build/test status + provenance
```

Evidence is one of:

```text
observed
reported
none
```

The agent never submits XP, quality score, Trust/Strain delta, level, rank, title or unlock decision.

## 6. Deterministic RPG progression

Canonical quest-type base XP:

| Type | XP |
|---|---:|
| planning | 30 |
| research | 40 |
| coding | 60 |
| review | 50 |
| debugging | 70 |
| documentation | 40 |
| maintenance | 40 |

Bonuses/penalties for `reward/2.0.0`:

```text
observed tests passed  +10
clean scope            +10
clear summary          +10
no user corrections     +5
scope violation         -5 each, max -15
user correction         -5 each, max -15
```

Outcome multipliers:

```text
success    100%
partial     60%
blocked     30%
failed      10%
abandoned    0%
```

The clean successful coding golden remains:

```text
60 + 10 + 10 + 10 + 5 = 95 XP
```

No XP depends on elapsed time, tokens, line counts, diff size or agent-reported complexity.

## 7. Skills, levels and rank

Skills are canonical stable keys:

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

Final Quest XP is distributed by server rule:

```text
1 skill  100%
2 skills 60/40
3 skills 50/30/20
```

Hero Level and every Skill Level have independent soft-increasing, versioned threshold tables.

Rank is a large cosmetic milestone, with RPG/engineering editorial direction such as:

```text
Code Squire
Senior Warrior
Staff Paladin
Principal Warlord
Legendary Architect
```

Ranks never gate product functionality or multiply XP.

## 8. Trust + Strain

`Trust` and `Strain` are bounded `0..100` behavioral game stats.

```text
Trust  = demonstrated reliability
Strain = accumulated technical friction/turbulence
```

They change only through deterministic Quest events. No passive time regeneration exists. They do not directly modify XP or gate functionality in 0.1.

`abandoned` is neutral and awards zero XP.

## 9. Streak, Traits and Titles

Success Streak increments on `success` and breaks on other outcomes. It grants no XP multiplier.

Traits are permanent collected cosmetic characteristics. Titles are cosmetic labels; one active Title is selected automatically by deterministic priority.

Unlocks may come from Hero levels, Skill levels, success streak milestones and rare behavioral conditions. No 0.1 unlock grants a mechanical advantage.

## 10. Heroes and projects

A Hero progresses globally across projects. Each Hero+Project has its own compact statistics/history projection.

Multiple Heroes are supported:

```text
create
list
activate
archive
restore
permanent delete
```

One Hero is globally active for **new** Quests. A Quest captures its Hero at start and never changes owner.

An open-Quest Hero cannot be archived or permanently deleted until the Quest is finished or abandoned.

## 11. Onboarding and localization

First-run setup is a five-step flow:

1. language;
2. initial Hero name;
3. presentation style (`rpg_engineering` default);
4. auto-start/auto-finish preferences;
5. confirmation.

CLI runs this through `hero-passport init`.

MCP stdio never mixes interactive prompts with protocol stdout. Until setup completes, gameplay mutations return `HP001 setup_required`; the Agent Skill can conduct conversational setup and submit it with `hero.configure`.

0.1 ships `ru-RU` and `en-US`. Canonical semantic keys are stored; localized strings are presentation only.

## 12. HP-MCP/2 v3.2

Static tool order:

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

Exact fields, schemas, annotations and results are normative in `WIRE-CONTRACT.md`.

Preferred MCP semantics are `2026-07-28`; official C# SDK baseline is `ModelContextProtocol 2.1.0`. The application never depends on MCP session state.

## 13. Persistence and privacy

SQLite is authoritative local state. Finish commits Quest report, XP ledger and all progression mutations atomically.

0.1 intentionally does not request or persist routine:

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

Stored Quest history is compact: title/goal/summary, bounded facts/provenance, outcome, immutable reward breakdown and progression delta.

## 14. Local-first, sync-ready

No account/cloud/sync is required or implemented in 0.1. IDs, immutable events, timestamps, rule versions, archive/delete semantics and persistence boundaries are designed so optional sync can be designed later without changing Hero Passport’s local-first identity.

No CRDT/event-sourcing framework is added preemptively.

## 15. Release definition

0.1 is accepted only with executable evidence for:

- start request idempotency and mismatch;
- one-open-Quest race safety;
- at-most-once committed finish progression;
- crash-before/after-commit recovery;
- SQLite runtime/WAL/backup qualification;
- Hero ownership/archive/delete invariants;
- reward/skill/Trust-Strain/streak/unlock goldens;
- RU/EN completeness;
- exact MCP tool/schema/result snapshots;
- Agent Skill start/finish/recovery evaluations;
- Codex reference E2E and cross-host smoke.

No implementation pass is claimed by this documentation-only architecture PR.
