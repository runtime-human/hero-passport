# Hero Passport — Product Specification

**Status:** Accepted v3.2.1 product contract  
**Snapshot:** 2026-08-11  
**Target:** 0.1.0 local-first MVP

Normative consolidated design: `superpowers/specs/2026-08-11-hero-passport-v3.2.1-design.md`.

## 1. Product

Hero Passport is a local-first RPG companion for people working with AI coding agents. It converts explicit durable Quests into deterministic progression without source-code surveillance or a cloud account.

0.1 surfaces:

```text
MCP Core
Agent Skill
CLI
```

Web is 0.2.

Core owns game truth. Skill owns model-driven lifecycle heuristics and rendering.

## 2. Quest UX

Core definition:

> A Quest is an explicitly started durable progression unit.

Skill recommendation:

> Use one coherent meaningful user goal per Quest.

The Skill starts conservatively when meaningful project work is clearly beginning and finishes conservatively when the goal is genuinely complete. Manual start/finish/abandon remain overrides/recovery tools.

A conversation can contain zero, one or several Quests. No inactivity timeout exists.

One Quest may be open per `(HeroId, ProjectId)`. Linked worktrees share Project identity, so same-Hero parallel independent worktree Quests are unsupported in 0.1.

Quest ownership is Hero+Project, never agent ownership.

## 3. Heroes

Multiple local Heroes remain in MVP.

`activeHero` is a user preference/default for forming a new Quest, not hidden ownership state. Start always carries explicit `heroId`.

Switching active Hero affects future default selection only and never moves existing Quests.

Hero archive/restore is reversible. Permanent logical delete is CLI-only in 0.1.

## 4. First run and runtime context

First-run setup uses `hero.bootstrap` with `bootstrapRequestId`, then preference changes use `hero.configure`.

`hero.get_context` hydrates a restarted Skill with versions, setup/settings, active default Hero, current Project and all open Quests in that Project across Heroes.

## 5. Start identity

Start input:

```text
startRequestId
heroId
questType
title
goal
```

`ProjectId` is implicit process-bound context but is included in canonical idempotency scope.

```text
same request + same bound Project/Hero/args -> original Quest replay
same request + changed context/args -> HP135
fresh request + open Quest for Hero+Project -> HP133
```

Natural-language equality is never deduplication.

## 6. Finish identity

Finish input:

```text
finishRequestId
questId
result
summary
skillsUsed[1..3]
bounded attestations
```

First committed finalization is immutable.

```text
same finish request/payload -> replay
same request/changed payload -> HP135
new request/already finalized/equivalent payload -> original result
new request/already finalized/different payload -> HP136
```

No leases or agent ownership are added.

## 7. Attestation/privacy boundary

Build/test/scope/correction fields are bounded agent attestations, not independent verification.

`observed` means the agent asserts it directly saw/ran the result; `reported` means supplied by a user/other source; `none` means no such evidence.

Hero Passport never requests routine source, diff, raw logs, prompts, secrets, environment dumps, full workspace paths or Git remotes.

Quest title/goal/summary can still contain project-sensitive metadata and are stored locally as bounded history.

## 8. RPG engine

Keep deterministic versioned XP, outcome multipliers, Skill XP/levels, Hero levels, Rank, Trust/Strain, Success Streak, Traits/Titles and semantic milestones.

Canonical clean successful coding golden remains 95 XP.

Outcome multipliers remain `1.00 / 0.60 / 0.30 / 0.10 / 0.00` for success/partial/blocked/failed/abandoned.

Trust/Strain are transparent RPG stats derived from bounded signals; they are not objective productivity telemetry and do not gate features or multiply XP.

Trait/Title/Streak implementation comes after the first working vertical slice.

## 9. Current MCP tools

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

Exact contract is `WIRE-CONTRACT.md`. Tool count is not a forever invariant.

Permanent delete remains available through explicit CLI administration, not MCP.

## 10. Local persistence

SQLite is authoritative local state.

Required qualified profile:

```text
actual SQLite >=3.53.4
WAL
synchronous=FULL
foreign_keys=ON
trusted_schema=OFF
same-host local filesystem
```

Finish atomically commits report, XP event and all progression changes.

Mutation receipts carry encoding version/context so late retries remain interpretable after upgrades/deletions.

## 11. Local-first, sync-conscious

0.1 has no account/cloud/sync.

The data model is sync-conscious, not sync-ready: UUIDv7 and immutable completion facts avoid some future blockers, but cross-device project identity, deletion, causality and conflict semantics are explicitly unsolved.

## 12. Release acceptance

0.1 requires executable evidence for bootstrap crash replay; Start context/idempotency/races; Finish conflict/idempotency/races; all-Hero Project recovery; SQLite connection policy across pooling/processes; DB CHECK/FK/index invariants; migration-lock diagnosis/repair; projection rebuild; level-cap wire semantics; MCP snapshots; Skill trigger/recovery/version-skew evals; Codex vertical E2E; privacy scans; and RU/EN completeness.

No product-code success is claimed by the architecture PR.
