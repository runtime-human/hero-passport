# Hero Passport — Decision Log

**Current baseline:** v3.2  
**Snapshot:** 2026-08-11

This log records architectural decisions. Detailed contracts live in focused specs; this file captures intent and supersession.

## Earlier retained decisions

v3/v3.1 decisions retained unless explicitly superseded below:

- C# 14 / .NET 10 LTS modular monolith;
- Domain -> Application -> Infrastructure -> App boundaries;
- same-host SQLite/WAL persistence;
- EF migrations from day one;
- Git-aware `project-identity/1` using canonical Git common-dir;
- local privacy deny-list;
- deterministic game engine, no LLM judge;
- stdio as 0.1 MCP transport;
- explicit MCP tool registration;
- immutable completed Quest reward history;
- UUIDv7 public IDs and JSON-safe integer ceiling;
- no source/diff/raw-log ingestion;
- Web UI deferred.

## ADR-046 — One meaningful goal is one Quest

**Status:** Accepted v3.2.

A Quest represents one meaningful user goal, not a chat session, MCP connection or agent process lifetime. One conversation may contain zero or multiple Quests.

The official Agent Skill performs conservative automatic start/finish; manual lifecycle controls are overrides/recovery.

## ADR-047 — One open Quest per Hero + Project

**Status:** Accepted v3.2; supersedes the v3.1 “up to 16 active Quests” policy.

Exactly one open Quest is permitted for a `(HeroId, ProjectId)` pair. A partial unique SQLite index is the durable backstop.

Reason: ambient recovery and multi-agent handoff are clearer when a project has one current meaningful goal for a Hero.

## ADR-048 — Caller request identity replaces goal-derived dedup

**Status:** Accepted v3.2; supersedes `QuestDedupKeyV1`.

`startRequestId` identifies one caller start intent/retry sequence; server `questId` identifies the durable Quest.

Same request ID + changed canonical arguments fails `HP135 idempotency_conflict`.

Reason: identical natural-language arguments can represent a legitimate new future Quest; retry identity and work identity are different concepts.

## ADR-049 — Mutation receipts

**Status:** Accepted v3.2.

Create Hero, Start Quest and permanent Delete Hero use caller-generated UUIDv7 request IDs. The mutation receipt stores operation, request ID, canonical argument hash, resulting/target entity ID and timestamp atomically with the mutation.

No prompt/source/history payload is stored in a receipt.

## ADR-050 — Agent Skill is a first-class MVP component

**Status:** Accepted v3.2.

MCP exposes safe semantic operations; the official portable Agent Skill owns lifecycle recognition, recovery guidance, bounded fact reporting and presentation.

Core invariants/game calculations never rely on Skill correctness.

The Skill follows the open Agent Skills `SKILL.md` format and progressive disclosure.

## ADR-051 — Multiple Heroes with one global active default

**Status:** Accepted v3.2.

Multiple local Heroes can be created, activated, archived/restored and permanently deleted. One globally active Hero owns new Quests by default.

A Quest’s HeroId is immutable after start. Switching active Hero never transfers existing Quest XP/history.

## ADR-052 — Archive is normal removal; permanent delete is explicit

**Status:** Accepted v3.2.

Archive is reversible. Permanent delete is destructive, requires exact name confirmation + request identity, and rejects globally active/open-Quest Heroes.

Normal rule-version upgrades never delete historical events; explicit user deletion is the privacy/lifecycle exception.

## ADR-053 — Trust + Strain supersede Trust + Risk

**Status:** Accepted v3.2.

`Risk` is retired. `Trust` models demonstrated reliability; `Strain` models accumulated technical friction/turbulence.

Both are deterministic `0..100`, Quest-driven only, do not regenerate by time, do not directly alter XP and do not gate product functionality in 0.1.

## ADR-054 — Bounded fact provenance, no surveillance verifier

**Status:** Accepted v3.2.

Build/test facts carry `observed | reported | none`. Only direct observed passed tests get the testing XP bonus.

Hero Passport does not ingest source/diffs/raw logs to independently audit the agent.

## ADR-055 — Reward engine v2

**Status:** Accepted v3.2.

XP is transparent: quest-type base + small fixed bonuses/penalties, then fixed outcome multiplier.

```text
success 1.00
partial 0.60
blocked 0.30
failed 0.10
abandoned 0.00
```

Clean successful coding remains 95 XP. Time/tokens/lines/diff size/agent complexity are excluded.

## ADR-056 — Soft versioned progression tables

**Status:** Accepted v3.2.

Hero Level and Skill Level use static versioned threshold tables. Rank is a cosmetic milestone derived from Hero Level.

Traits/Titles/Streak are cosmetic and never create XP multipliers or feature locks in 0.1.

## ADR-057 — RU/EN localization and first-run onboarding in MVP

**Status:** Accepted v3.2.

MVP supports `ru-RU` and `en-US`. Domain/Application use semantic keys; App/Skill render localized text.

First-run setup is a short five-step wizard. stdio MCP never prints interactive prompts into protocol stdout; the Skill can onboard conversationally via `hero.configure`.

## ADR-058 — HP-MCP/2 expands to explicit Hero/config operations

**Status:** Accepted v3.2; supersedes the v3.1 four-tool-only decision.

v3.2 statically registers eleven narrow tools: configure, six Hero-management tools, start/finish/list-active/card.

Reason: multiple Heroes and conversational onboarding are real product requirements; narrow explicit operations are safer than a generic `hero.manage` bag.

## ADR-059 — MCP C# SDK 2.1.0 and sessionless design

**Status:** Accepted v3.2.

Baseline updates to official `ModelContextProtocol 2.1.0`. Preferred protocol remains MCP `2026-07-28`, with release qualification against `2025-11-25` compatibility behavior.

Application state uses ordinary explicit IDs/handles and never depends on protocol connection/session lifetime.

## ADR-060 — SQLite runtime floor 3.53.4

**Status:** Accepted v3.2; supersedes the v3.1 `>=3.51.3` minimum.

Selected `SQLitePCLRaw.bundle_e_sqlite3 3.0.5` currently resolves native SQLite >=3.53.4. Doctor/release checks the actual loaded runtime and requires >=3.53.4.

## ADR-061 — Local-first now, sync-ready only

**Status:** Accepted v3.2.

No account/cloud/sync dependency in 0.1. UUIDv7 identities, immutable completion facts, explicit timestamps/versions and archive/delete semantics preserve a future sync design seam.

No CRDT/event-sourcing framework is adopted preemptively.

## ADR-062 — Quest belongs to work, not an agent

**Status:** Accepted v3.2.

No agent ownership, lease, heartbeat, leader election or dispatcher. Multiple agents may continue the same durable Quest handle.

SQLite serializes only Hero Passport’s own state changes; it does not coordinate code editing among agents.

## ADR-063 — Prior-art gate for nontrivial mechanisms

**Status:** Accepted process rule.

For a nontrivial mechanism:

```text
identify prior art
isolate mechanism + limitation
compare several strong sources
verify actual stack behavior against latest official docs/source
adapt minimally
encode the claim in tests
```

NeuroArxiv inspired this research process; it is not a runtime dependency.

## Superseded active v3.1 concepts

The following are historical only and must not appear as active requirements:

```text
QuestDedupKeyV1 / goal-derived start dedup
up to 16 open Quests per Hero+Project
Risk as a Hero stat
hero.start_quest idempotent=false
exactly four HP-MCP tools
ModelContextProtocol 2.0.0 baseline
SQLite runtime floor 3.51.3
failed outcome multiplier 0.20
old large XP penalties
```
