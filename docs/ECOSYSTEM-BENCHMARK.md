# Hero Passport — Ecosystem / Prior-Art Benchmark

**Status:** v3.2 architecture evidence  
**Snapshot:** 2026-08-11

This is not a dependency shopping list. We compare mechanisms, take the smallest proven pattern that fits Hero Passport, and reject framework complexity or privacy models that do not fit the product.

## 1. Comparison method

For each source ask:

```text
What problem does it solve?
What identity/state boundary does it use?
What failure/retry model does it use?
What should Hero Passport borrow?
What should Hero Passport explicitly not copy?
```

Official specifications/docs remain authoritative for technologies Hero Passport actually uses. Repository prior art informs architecture, not package versions.

## 2. A2A — task/message/context identity

Source: `a2aproject/A2A` specification and proto.

Observed mechanism:

- a Message has creator-generated `messageId`;
- server-created Task has its own `taskId` and lifecycle;
- `contextId` can group multiple Tasks/messages;
- send operations may use message ID for duplicate detection;
- Task ID is a server-generated stateful unit of work.

### Take

Hero Passport separates:

```text
startRequestId = caller intent/retry identity
questId        = server work/game identity
```

This is much stronger than hashing `goal` text.

### Do not take yet

`contextId`/Quest hierarchy and A2A async task lifecycle are unnecessary for 0.1. Hero Passport’s tool call is short; the external AI agent performs the actual work.

## 3. AWS idempotent API design — caller expresses retry intent

Source: Amazon Builders’ Library “Making retries safe with idempotent APIs” and EC2 ClientToken documentation.

Observed mechanism:

- synthetic hashes of request parameters can confuse “retry” with “user wants another identical resource”;
- preferred API contract carries a caller-generated request identifier;
- same token + same parameters can safely retry;
- same token + changed parameters is a parameter-mismatch error;
- recording token and resource mutation must be ACID;
- late retries should converge to semantically equivalent outcomes when practical.

### Take

Required request IDs for Hero create/start/delete, atomic mutation receipts, canonical argument hashes and `HP135 idempotency_conflict`.

### Reject

Do not use content/goal hashing as the resource identity. Hero Passport can legitimately have two future Quests with identical title/goal.

## 4. Temporal .NET — running-work identity conflict

Source: `temporalio/sdk-dotnet` enum/API model.

Observed mechanism:

- workflow identity conflict is modeled explicitly;
- it is not valid to have two actively running executions for the same workflow identity;
- conflict policy can fail/use existing/terminate existing.

### Take

Make open-work conflict a first-class state/invariant rather than incidental application logic. Hero Passport chooses:

```text
one open Quest per Hero+Project
new different start -> HP133 active_quest_exists
recovery may explicitly resume the existing questId
```

### Reject

No Temporal runtime dependency, workflow history engine, activity workers, heartbeat or lease system. SQLite is sufficient for one local application state store.

## 5. MCP 2026-07-28 — stateless protocol, stateful application handles

Source: Model Context Protocol specification/blog and SEP-2567.

Observed mechanism:

- protocol core no longer relies on handshake/session state;
- stateful applications mint explicit handles and have the model pass them back;
- tool catalogs benefit from deterministic ordering;
- structured results have a compatibility TextContent pattern.

### Take

`questId` is explicit durable state handle. Application correctness is independent of connection/MCP session lifetime. Tools are explicitly ordered. Canonical structured results remain the authority.

### Reject/defer

MCP Tasks are not Quest lifecycle. Hero Passport should not make a 30–90 minute coding session one long MCP tool execution.

## 6. Agent Skills standard + Anthropic/OpenAI implementations

Sources: `agentskills/agentskills`, `anthropics/skills`, OpenAI Skills guidance.

Observed mechanism:

- Skill = portable directory rooted at `SKILL.md`;
- name/description are discovery metadata;
- full instructions load only when activated;
- detailed references/scripts load on demand;
- concise progressive disclosure reduces fixed context cost;
- both OpenAI and Anthropic ecosystems support/use the open format.

### Take

Ship the Hero Passport lifecycle as a portable official Agent Skill:

```text
SKILL.md = trigger + core workflow
references/ = recovery, finish facts, presentation detail
```

Treat trigger quality as an eval target: under-triggering misses quests, over-triggering creates noise.

### Reject

Do not place the entire architecture or game engine policy in Skill prose. Model instructions are not an invariant boundary.

## 7. OpenAI harness-engineering repo practice — docs as system of record

Source: OpenAI “Harness engineering”.

Observed mechanism:

- keep `AGENTS.md` short as a map/table of contents;
- keep structured docs as system of record;
- avoid context-heavy giant instruction files that rot.

### Take

Hero Passport `AGENTS.md` becomes a concise navigation/guardrail file. Normative details live in focused docs and executable tests.

## 8. Atuin — local SQLite first, sync optional

Source: `atuinsh/atuin`.

Observed mechanism:

- useful local product state lives in SQLite;
- sync is optional rather than required to use the product;
- cross-device architecture can be added without making local use depend on a cloud account.

### Take

Hero Passport 0.1 is fully local and offline-useful. UUIDv7 identities, immutable completion facts and explicit lifecycle timestamps/versions keep a later optional sync design possible.

### Reject

Do not copy Atuin’s telemetry domain. Hero Passport intentionally does not capture command/file/cwd-style continuous activity.

## 9. WakaTime CLI/plugins — useful contrast: heartbeat telemetry

Source: WakaTime plugin/CLI documentation.

Observed mechanism:

- editor hooks send heartbeats on file focus/type/save events;
- CLI receives absolute current file and detects project/language/metadata;
- activity can be queued/synced to an API.

### What this teaches us

This is a coherent architecture for time/activity analytics, **but it is the wrong product boundary for Hero Passport**.

Hero Passport explicitly rejects:

```text
continuous editor heartbeat collection
absolute file-path telemetry
time-based XP
background activity monitoring
cloud activity upload as a prerequisite
```

Quest boundaries come from agent intent/work lifecycle, not keystroke/file events.

## 10. Habitica — RPG motivation without copying punitive economy

Source: `HabitRPG/habitica`.

Observed mechanism:

- familiar RPG metaphors make non-game progress emotionally legible;
- levels/rewards/gear/HP create strong reinforcement;
- failures can have punitive consequences.

### Take

Use readable RPG progression: XP, Skills, Levels, Ranks, Traits, Titles, Streak and milestone flavor.

### Reject for MVP

No HP loss, Gold/gear economy, random loot, strong failure punishment or reward multipliers that encourage farming. Hero Passport is a work companion; failed/blocked sessions should be informative, not anxiety-producing.

## 11. NeuroArxiv — research-process inspiration, not runtime dependency

Source: `UditAkhourii/neuroarxiv` (previous architecture research discussion).

Useful process pattern:

```text
find prior art
isolate sources
extract mechanism + limitation
compare/converge
verify chosen mechanism against official docs for actual stack
adapt + test
```

### Take

Use this workflow as an architectural research gate for nontrivial mechanisms.

### Reject

Do not copy NeuroArxiv code/runtime or treat arXiv papers as higher authority than current .NET/MCP/SQLite official documentation for implementation details.

## 12. Consolidated decision matrix

| Concern | Strong precedent | Hero Passport decision |
|---|---|---|
| retry identity | AWS, A2A | caller request ID + atomic receipt |
| work identity | A2A, MCP handles | server QuestId |
| active-work conflict | Temporal | one open Hero+Project Quest |
| state across MCP calls | MCP 2026 | explicit questId, no session dependency |
| agent orchestration | Agent Skills | official portable Skill |
| instruction repository | OpenAI harness | short AGENTS + structured docs |
| offline data | Atuin | local SQLite first |
| future sync | Atuin | data model ready, sync not built |
| activity detection | WakaTime contrast | reject continuous heartbeats |
| gamification | Habitica | cosmetic/soft RPG progression, no harsh economy |
| architecture research | NeuroArxiv process | prior-art gate + official-doc verification |

## 13. What we deliberately do not import

```text
Temporal workflow runtime
A2A protocol runtime
WakaTime telemetry model
Habitica economy/HP/gear
Atuin sync protocol
CRDT/event-sourcing framework
agent leases/heartbeats/leadership
LLM judge
```

The benchmark is successful only if it reduces uncertainty while keeping Hero Passport smaller than the systems it studies.

## 14. Critical open risks after comparison

1. **Agent Skill trigger reliability** — no server protocol can guarantee every host/model will invoke start/finish correctly. Mitigation: conservative policy + evals + manual override + recovery.
2. **Self-reported finish facts** — provenance improves honesty but is not independent verification. Accepted privacy tradeoff for 0.1.
3. **Future sync deletion/conflicts** — sync-ready IDs are not a sync design. A future cloud feature needs new ADRs and tests.
4. **Multiple concurrent agents** — one Quest can be shared, but Hero Passport is not coordinating their code work. It only serializes its own game state.
5. **Game balance** — transparent versioned tables allow rebalance, but 0.1 thresholds still require dogfooding before claiming optimal motivation.
