# Hero Passport Documentation

**Current architecture:** v3.2.1  
**Snapshot:** 2026-09-06

## Start here

```text
PRODUCT-SPEC.md     product behavior and MVP scope
ARCHITECTURE.md     runtime/system boundaries
AGENT-SKILL.md      ambient orchestration policy
```

## Normative precedence

When documents overlap, use this order for the relevant topic:

1. `superpowers/specs/2026-08-11-hero-passport-v3.2.1-design.md` — consolidated accepted correction baseline;
2. `WIRE-CONTRACT.md` — exact HP-MCP/2 fields, schemas, tool order, results/errors;
3. `PERSISTENCE-RELIABILITY.md` — SQLite connections, transactions, crash/migration/backup;
4. `DATA-MODEL.md` — schema, CHECK/FK/index/projection invariants;
5. `PROJECT-IDENTITY.md` — Git/filesystem project identity;
6. `ENGINE-SPEC.md` — deterministic game rules;
7. `REWARD-COMPONENTS.md` — persisted reward component keys/order for versioned historical reports;
8. `AGENT-SKILL.md` — Skill trigger/lifecycle/recovery/presentation behavior;
9. subsystem overview docs;
10. roadmap/integration/reference material.

For `quest_reward_components.component_key` and its ordinal/delta semantics, `REWARD-COMPONENTS.md` is the focused contract and takes precedence over generic table descriptions.

The v3.2 consolidated design and implementation plan are superseded by v3.2.1. Older v3/v3.1 material is historical only.

## Current contracts

- [`PRODUCT-SPEC.md`](PRODUCT-SPEC.md)
- [`ARCHITECTURE.md`](ARCHITECTURE.md)
- [`WIRE-CONTRACT.md`](WIRE-CONTRACT.md)
- [`ENGINE-SPEC.md`](ENGINE-SPEC.md)
- [`REWARD-COMPONENTS.md`](REWARD-COMPONENTS.md)
- [`AGENT-SKILL.md`](AGENT-SKILL.md)
- [`DATA-MODEL.md`](DATA-MODEL.md)
- [`PERSISTENCE-RELIABILITY.md`](PERSISTENCE-RELIABILITY.md)
- [`PROJECT-IDENTITY.md`](PROJECT-IDENTITY.md)
- [`CONFIGURATION.md`](CONFIGURATION.md)
- [`SECURITY-PRIVACY.md`](SECURITY-PRIVACY.md)
- [`TESTING-QUALITY.md`](TESTING-QUALITY.md)

## Current design and execution plans

- `superpowers/specs/2026-08-11-hero-passport-v3.2.1-design.md`
- `superpowers/plans/2026-08-11-hero-passport-v3.2.1-implementation.md`
- [`MVP-PR-PLAN.md`](MVP-PR-PLAN.md) — reviewable PR sequence and merge discipline for 0.1

## Key v3.2.1 corrections

```text
first-run hero.configure        -> hero.bootstrap + preference-only configure
hidden active-Hero Start owner  -> explicit heroId on hero.start_quest
list_active_quests              -> project-wide hero.get_context
Finish without request identity -> finishRequestId + semantic finalization conflict
MCP permanent delete            -> CLI-only logical permanent delete
sync-ready claim                -> local-first, sync-conscious
flavor hash as game truth       -> semantic milestones + presentation flavor
```

Still retired from earlier versions:

```text
Risk                    -> Strain
QuestDedupKeyV1         -> explicit mutation request IDs
max 16 open Quests      -> one open Quest per Hero+Project
4-tool-only MCP surface -> explicit current HP-MCP/2 inventory
```

## Documentation maintenance rule

When a decision changes:

1. update the authoritative deep dive;
2. update consolidated spec/decision log;
3. update overview docs that repeat the contract;
4. update implementation plan/tests;
5. run stale-contract search;
6. do not leave contradictory active guidance.

Important stale-search terms after v3.2.1:

```text
hero.delete                 # acceptable only as CLI wording/history
hero.list_active_quests
initialHeroName in configure
resolve globally active Hero # inside Start mutation
sync-ready                  # acceptable only in supersession/history
finish without finishRequestId
flavor hash mod
ModelContextProtocol 2.0.0  # historical only
SQLite >=3.51.3             # historical only
```
