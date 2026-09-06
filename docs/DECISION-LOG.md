# Hero Passport — Decision Log

**Current baseline:** v3.2.1  
**Snapshot:** 2026-09-06

Focused contracts are normative for exact schemas/rules. This log records intent and supersession.

## Retained foundations

Keep:

- C# 14 / .NET 10 LTS modular monolith;
- Domain -> Application -> Infrastructure -> App boundaries;
- same-host SQLite/WAL, EF migrations, real SQLite qualification;
- `project-identity/1` based on canonical Git common-dir;
- local privacy deny-list;
- deterministic game engine, no LLM judge;
- stdio 0.1 MCP transport;
- explicit tool registration;
- immutable completed Quest outcomes;
- UUIDv7 application IDs and JSON-safe exposed integer ceiling;
- Agent Skill orchestration / MCP Core authority;
- Web deferred to 0.2.

## ADR-046 — Quest granularity is Skill policy

**Status:** Corrected v3.2.1.

Core definition: a Quest is an explicitly started durable progression unit.

Agent Skill heuristic: prefer one coherent meaningful user goal per Quest.

The server does not claim to objectively prove “meaningful”. A chat/session/connection is never Quest identity.

## ADR-047 — One open Quest per Hero+Project

**Status:** Retained v3.2.1; supersedes v3.1 max-16 policy.

Partial unique SQLite index is the durable backstop.

Linked worktrees share Project identity, therefore 0.1 intentionally does not support parallel independent same-Hero open Quests across linked worktrees of one repository. No WorkContext identity is added.

## ADR-048 — Retry identity separate from work identity

**Status:** Retained/expanded v3.2.1; supersedes `QuestDedupKeyV1`.

Caller request identities:

```text
bootstrapRequestId
createRequestId
startRequestId
finishRequestId
```

Server `questId` is durable Quest identity. Natural-language equality never defines retry/task identity.

## ADR-049 — Versioned mutation receipts

**Status:** Corrected v3.2.1.

Receipts atomically record operation/request ID, `args_encoding_version`, canonical hash, result identity/status and bound Project/Hero context as applicable.

Receipt IDs/context deliberately have no FK so minimal `target_deleted` receipts can survive permanent target deletion and prevent resurrection/reuse ambiguity.

## ADR-050 — Agent Skill is first-class MVP

**Status:** Retained.

Portable Skill owns lifecycle heuristics, recovery, bounded attestations and presentation. Core owns all durable invariants/game calculations.

## ADR-051 — Multiple Heroes retained; active Hero is only a default

**Status:** Corrected v3.2.1.

Multiple local Heroes remain.

Global `activeHeroId` is preference/default for **forming** new Start calls, not hidden mutation ownership. `hero.start_quest` requires explicit `heroId`.

Quest HeroId never changes after start.

## ADR-052 — Archive is MCP removal; permanent logical delete is CLI-only

**Status:** Corrected v3.2.1.

Archive/restore are reversible model-facing operations.

Permanent Hero delete is explicit CLI administration in 0.1. No model-readable confirmation string is treated as proof of human destructive intent. Future MCP destructive delete requires separately qualified human-confirmation semantics.

Deletion means irreversible removal from the active logical DB state; no forensic erasure claim for SQLite free pages/backups/snapshots/exports/media.

## ADR-053 — Trust + Strain retained as RPG stats

**Status:** Retained/clarified v3.2.1.

Risk remains retired. Trust/Strain are transparent `0..100` RPG stats derived from bounded Quest signals, not objective employee/productivity telemetry.

No time regeneration, XP multiplier or feature gating.

## ADR-054 — Bounded agent attestations, no surveillance verifier

**Status:** Terminology corrected v3.2.1.

Use `observed | reported | none`, but `observed` means the agent **asserts** direct observation. Hero Passport does not independently verify raw source/log evidence.

## ADR-055 — Reward engine v2

**Status:** Retained.

Transparent quest-type base + fixed bonuses/penalties, then outcome multiplier. Clean successful coding remains 95 XP. No time/token/line/diff/complexity multiplier.

## ADR-056 — Versioned progression + cosmetic RPG layers

**Status:** Retained; implementation reordered.

Hero/Skill thresholds, Rank, Trust/Strain, Streak, Traits/Titles remain MVP design. Streak/Traits/Titles are implemented after the first vertical product checkpoint and are schedule-cut candidates if needed.

## ADR-057 — RU/EN + crash-safe onboarding

**Status:** Corrected v3.2.1.

`ru-RU`/`en-US` remain MVP.

First-run resource creation uses `hero.bootstrap` + `bootstrapRequestId`; `hero.configure` becomes post-setup preferences only.

stdio remains protocol-pure.

## ADR-058 — HP-MCP/2 current surface, not permanent tool-count invariant

**Status:** Corrected v3.2.1.

Current ordered tools:

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

`hero.get_context` replaces active-Hero-only list-active recovery. `hero.delete` is CLI-only.

## ADR-059 — MCP C# SDK 2.1.0 + sessionless explicit handles

**Status:** Retained v3.2.1.

Official `ModelContextProtocol 2.1.0` remains baseline. Preferred MCP is `2026-07-28`; qualify `2025-11-25` compatibility.

Application state uses explicit ordinary handles/arguments, never protocol session lifetime.

Real package restore/build is Task-1 evidence.

## ADR-060 — SQLite runtime floor 3.53.4

**Status:** Retained.

Doctor/release checks the actual loaded runtime >=3.53.4.

## ADR-061 — Local-first, sync-conscious

**Status:** Corrected v3.2.1.

0.1 has no account/cloud/sync. Current identity/history choices avoid some future blockers but **do not claim sync-ready semantics**.

Cross-device Project identity, deletion/tombstones, causality and conflicts require future ADRs. No CRDT/event sourcing now.

## ADR-062 — Quest belongs to work/Hero+Project, not agent

**Status:** Retained.

No agent owner/lease/heartbeat/leader election. Multiple agents may continue the same Quest handle. Hero Passport serializes only its own state.

## ADR-063 — Prior-art gate

**Status:** Retained process rule.

For nontrivial mechanisms: compare strong prior art, verify current official stack behavior, adapt minimally, encode claims in tests. NeuroArxiv influenced research process only.

## ADR-064 — Runtime context is explicit and project-wide

**Status:** Accepted v3.2.1.

`hero.get_context` exposes setup/settings/compatibility/default Hero and all current-Project open Quests across Heroes.

This solves restart hydration and inactive-Hero recovery. Read-only context calls perform no durable bookkeeping writes.

## ADR-065 — Finish has explicit mutation identity and conflict detection

**Status:** Accepted v3.2.1.

`finishRequestId` distinguishes request retry from Quest identity.

Same request/changed payload -> HP135. New request against already finalized Quest with different canonical payload -> HP136. First committed outcome remains immutable; no leases/overwrite.

## ADR-066 — Initial schema enforces critical invariants physically

**Status:** Accepted v3.2.1.

Migration 0001 contains typed singleton `app_settings`, closed-enum/range/status-time CHECKs, reviewed FK actions and partial unique open-Quest backstop.

## ADR-067 — Connection-scoped SQLite durability/security policy

**Status:** Accepted v3.2.1.

WAL/runtime are database initialization/qualification concerns. Every product connection enforces/qualifies foreign keys, `synchronous=FULL` and `trusted_schema=OFF`, including pool/new-process tests.

## ADR-068 — EF migration abandoned-lock recovery is explicit

**Status:** Accepted v3.2.1.

Doctor detects suspicious `__EFMigrationsLock`. Ordinary startup never silently clears it. Explicit repair has process-stopped/safety/integrity preconditions and child-process tests.

## ADR-069 — Canonical history supports projection rebuild

**Status:** Accepted v3.2.1.

Completed reports/events/deltas/unlocks are canonical surviving history; Hero totals/Skill totals/streak/project stats are rebuildable projections. This is repair/migration insurance, not event sourcing.

## ADR-070 — Flavor is presentation

**Status:** Accepted v3.2.1.

Domain emits semantic milestone events only. Deterministic hash/mod flavor selection is removed from game truth; curated wording may evolve without changing progression.

## ADR-071 — Risk-first implementation

**Status:** Accepted v3.2.1.

Before all RPG layers, prove SQLite/project identity/bootstrap/get_context/minimal Start+Finish/real MCP/minimal Skill/Codex E2E with restart/retry/race/crash.

Only then implement full reward/progression/RPG cosmetics/localization/admin/broader qualification.

## ADR-072 — Reward component keys are canonical history

**Status:** Accepted v3.2.1 clarification.

Persisted `quest_reward_components.component_key` values are immutable semantic history, not implementation-private names or localized presentation labels.

`reward/2.0.0` uses the fixed catalog and ordering defined by `REWARD-COMPONENTS.md`: observed-tests, clean-scope, clear-summary and no-user-corrections bonuses followed by aggregated scope-violation and user-correction penalties. Inactive/zero-delta components are omitted and persisted ordinals are dense after filtering.

Base XP and the outcome multiplier remain report fields rather than synthetic component rows. Penalty categories persist one capped aggregate row each rather than one row per violation/correction.

Future reward versions may change the catalog only under a new `reward_rule_version`; existing completed Quest rows are never reinterpreted or relabeled in storage. Localization such as Russian “Бонус за контроль” remains presentation only.

## Historical/superseded terms

These may appear only in explicit history/supersession notes:

```text
QuestDedupKeyV1
up to 16 open Quests per Hero+Project
Risk as active Hero stat
first-run initialHeroName through hero.configure
Start ownership resolved from mutable global active Hero
hero.list_active_quests as recovery contract
hero.delete as 0.1 MCP tool
Finish idempotency solely by questId with silent conflicting-payload replay
sync-ready claim
hash-mod deterministic flavor line
ModelContextProtocol 2.0.0 baseline
SQLite runtime floor 3.51.3
```
