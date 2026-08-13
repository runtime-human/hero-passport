# Hero Passport — Ecosystem / Prior-Art Benchmark

**Status:** v3.2.1 architecture evidence  
**Snapshot:** 2026-08-11

This is not a dependency shopping list. Compare mechanisms, take the smallest proven pattern that fits Hero Passport, reject framework complexity/privacy models that do not fit.

Official specifications/docs remain authoritative for technologies Hero Passport actually uses. Repository prior art informs architecture, not package versions.

## 1. A2A — request/message vs durable work identity

Strong pattern: creator/request identity and server-created task/work identity are separate.

**Take:** caller request IDs (`startRequestId`, etc.) are different from durable server `questId`.

**Reject:** A2A runtime/context hierarchy/async task machinery for 0.1.

## 2. AWS idempotent APIs — caller expresses retry intent

Strong pattern: caller token + same semantic parameters replays; token + changed semantic parameters conflicts; recording request identity and mutation must be atomic.

**Take:** crash-safe mutation receipts, `HP135`, versioned canonical argument encoding.

v3.2.1 strengthens this: Start scope includes bound ProjectId + explicit HeroId; receipts persist `args_encoding_version` and survive deleted targets minimally.

**Reject:** natural-language/content hashing as resource/work identity.

## 3. Temporal — explicit active-work conflict

Strong pattern: active-work identity conflict is explicit rather than accidental.

**Take:** one open Quest per Hero+Project + HP133.

**Important limit:** Temporal’s Workflow ID analogy does not prove Hero+whole-repository is universally optimal scope. Hero Passport explicitly documents linked-worktree same-Hero parallel independent work as unsupported in 0.1 instead of overclaiming the analogy.

**Reject:** Temporal runtime, workers, workflow history, leases/heartbeats.

## 4. MCP 2026-07-28 — stateless protocol, explicit application handles

Strong current MCP guidance: protocol sessions are gone; stateful applications return explicit handles and receive them as ordinary later arguments. Structured tool results retain JSON TextContent compatibility guidance.

**Take:** explicit `heroId`/`questId`, no connection/session ownership, deterministic explicit tool registration, canonical structured result + semantic JSON compatibility block.

v3.2.1 further removes mutable global active-Hero state from Start ownership: the selected Hero is an explicit argument.

**Reject/defer:** MCP Tasks for Quest lifecycle; the agent does the long-running coding/research, Hero Passport calls are short local operations.

## 5. MCP MRTR — confirmation capability vs MVP destructive surface

Current MCP supports multi-round-trip input/confirmation patterns.

**Lesson:** model-readable `confirmHeroName` is not proof of human intent.

**Decision:** do not make permanent Hero delete a model-facing 0.1 tool. CLI-only logical permanent delete is safer/smaller. If MCP delete returns later, qualify a real human-confirmation design such as MRTR across supported hosts.

## 6. Agent Skills — portable orchestration policy

Strong pattern: concise `SKILL.md` + progressive disclosure/reference files; model-driven activation needs trigger/no-trigger evals.

**Take:** official Hero Passport Agent Skill owns lifecycle heuristics, context hydration, bounded attestations and presentation.

v3.2.1 adds `hero.get_context` for persisted settings/recovery/version skew and treats “one meaningful goal” explicitly as Skill heuristic rather than Core-verifiable truth.

**Reject:** putting game engine/invariants into prose instructions.

## 7. OpenAI harness/repository practice — docs as system of record

Strong pattern: concise agent map/guardrails plus focused authoritative docs, rather than one giant instruction file.

**Take:** short `AGENTS.md`, focused normative specs, executable tests, stale-contract scans.

## 8. Atuin — local-first usefulness, optional future sync

Strong pattern: useful local state exists without mandatory cloud account.

**Take:** Hero Passport 0.1 is fully local/offline-useful.

**Correction:** do **not** call the current schema sync-ready. UUIDv7/immutable completions/rebuildable projections are only **sync-conscious** seams; cross-device Project identity, deletion, causality/conflict semantics remain future design work.

**Reject:** copying a sync protocol or activity telemetry domain now.

## 9. WakaTime — telemetry counterexample

Strong architecture for activity analytics uses editor/file heartbeats, paths and continuous events.

**Decision:** explicitly reject that boundary for Hero Passport:

```text
no continuous editor heartbeat
no absolute file-path telemetry
no time-based XP
no background work monitoring
no cloud activity upload requirement
```

Quest lifecycle comes from agent/user work intent, not keystrokes/files.

## 10. Habitica — readable RPG motivation

**Take:** XP, Skills, Levels, Ranks, Trust/Strain as RPG stats, Streak, Traits/Titles, milestone flavor.

**Reject:** HP loss, currency/gear economy, random loot, harsh punishment, farmable multipliers.

## 11. NeuroArxiv — research-process inspiration

Use as process:

```text
find prior art
isolate mechanism/limitations
compare several sources
verify chosen stack behavior against current official docs/source
adapt minimally
encode claims in tests
```

Not a runtime dependency and not higher authority than current official MCP/.NET/SQLite/Git docs.

## 12. SQLite / EF current official guidance

v3.2.1 adopts several corrections directly supported by current official behavior:

- Microsoft.Data.Sqlite connection string has Foreign Keys/Pooling/Default Timeout/Cache, but not a `Synchronous=Full` keyword -> explicit connection-open PRAGMA policy;
- SQLite `trusted_schema` is per-connection and suitable to disable for this static application schema after qualification;
- FULL synchronous in WAL is the chosen durability policy and must be true on actual writer connections;
- EF SQLite `__EFMigrationsLock` can remain after process termination -> doctor diagnosis + explicit repair, never silent startup deletion;
- initial CHECK/FK/index choices matter because later SQLite schema changes may require table rebuilds.

## 13. Consolidated decision matrix

| Concern | Strong precedent | v3.2.1 decision |
|---|---|---|
| retry identity | AWS, A2A | caller request IDs + atomic versioned receipts |
| work identity | A2A, MCP handles | server questId |
| Start owner | MCP explicit handles | explicit heroId, not ambient active Hero |
| active-work conflict | Temporal pattern | one open Hero+Project Quest + documented scope limit |
| state across MCP calls | MCP 2026 | get_context + explicit IDs, no session dependency |
| finalization race | idempotent mutation pattern | finishRequestId + HP136 conflict; no overwrite |
| agent orchestration | Agent Skills | official portable Skill |
| offline data | Atuin | local SQLite first |
| future sync | local-first precedent | sync-conscious only, no sync claim |
| destructive model UX | MCP MRTR lesson | permanent delete CLI-only in 0.1 |
| activity detection | WakaTime contrast | reject continuous telemetry |
| gamification | Habitica | soft/cosmetic RPG, no punitive economy |
| architecture research | NeuroArxiv process | prior-art + official-doc + tests |

## 14. Deliberately not imported

```text
Temporal workflow runtime
A2A protocol runtime
WakaTime telemetry model
Habitica economy/HP/gear
Atuin sync protocol
CRDT/event-sourcing framework
agent ownership/leases/leadership
LLM judge
```

## 15. Remaining risks after v3.2.1

1. **Skill trigger reliability** — mitigated by conservative policy, persisted settings, evals, manual override and get_context recovery.
2. **Agent attestations are not independent verification** — accepted privacy tradeoff; terminology no longer overclaims “facts”.
3. **Game balance** — versioned transparent rules require dogfooding; current numbers are testable, not claimed psychologically optimal.
4. **Parallel same-Hero linked-worktree goals** — explicitly unsupported in 0.1 rather than hidden.
5. **Future sync** — intentionally unsolved beyond sync-conscious seams.
6. **Cross-host compatibility** — reference Codex vertical slice is proven before broad host qualification.
