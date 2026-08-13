# Hero Passport — MCP Contract Overview

**Status:** Accepted HP-MCP/2 v3.2.1 overview  
**Snapshot:** 2026-08-11  
**SDK:** official C# `ModelContextProtocol 2.1.0`  
**Preferred protocol:** MCP `2026-07-28`  
**0.1 transport:** stdio

Exact field/schema/result semantics are normative in `WIRE-CONTRACT.md`.

## 1. Current static tool surface

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

Explicit deterministic registration; no aliases, broad assembly scanning or dynamic tool sets.

The current count is a contract snapshot, not a permanent architecture invariant.

`hero.delete` is CLI-only in 0.1. `hero.list_active_quests` is replaced by project-wide `hero.get_context` recovery.

## 2. Stateless protocol posture

Application correctness never depends on protocol session/connection/server lifetime.

MCP 2026-07-28 is stateless at protocol layer. Hero Passport threads explicit ordinary handles/arguments such as `heroId` and `questId` across calls.

Legacy `2025-11-25` compatibility is qualified through the official SDK path.

## 3. Features used/not used

Required: MCP Tools.

Not required for 0.1 correctness:

```text
Resources
Prompts
MCP Tasks
MCP Apps
own Streamable HTTP/OAuth
implicit server-side session state
```

A Hero Quest is not an MCP Task: actual coding/research occurs outside the short local Hero Passport mutation.

## 4. Model-control boundary

Normal gameplay tools rely on server validation/invariants, not host confirmation as correctness.

Permanent destructive Hero deletion is intentionally not a model-facing 0.1 tool. CLI provides that rare administration path. A future MCP delete would require a separately qualified human-confirmation design such as MRTR and a new contract revision.

## 5. Agent Skill

Ambient lifecycle policy lives in `AGENT-SKILL.md`, not MCP connection state.

Skill:

```text
calls get_context to hydrate settings/recovery/version data
uses one meaningful goal per Quest as a heuristic
passes explicit selected heroId on Start
carries questId
collects bounded attestations
uses finishRequestId for finalization
renders canonical progression
```

Core validates invariants/calculates all game state.

## 6. Idempotency

Retry identities:

```text
bootstrapRequestId
createRequestId
startRequestId
finishRequestId
```

Receipts persist versioned canonical argument fingerprints/context.

Start hash scope includes process-bound ProjectId + explicit HeroId.

Finish semantic disagreement after another finalization returns HP136; it is not silently labeled an ordinary retry.

Natural-language goal is never an idempotency key.

## 7. Result compatibility

Success:

```text
structuredContent = canonical typed result
content            = one deterministic serialized JSON TextContent
                     semantically equal to structuredContent
```

JSON minification/whitespace is implementation formatting, not business semantics.

Expected business/tool error:

```text
isError=true
one safe TextContent
no structuredContent
```

## 8. Server instructions

Canonical meaning:

```text
Use the installed Hero Passport Agent Skill for ambient lifecycle policy.
Call hero.get_context to hydrate/recover uncertain state.
Pass explicit heroId when starting a Quest and carry returned questId.
Reuse mutation request IDs only for retries of the same canonical intent.
Never send source, diffs, raw logs, prompts, secrets, environment dumps or workspace paths.
```

Instructions are guidance, never invariant enforcement.

## 9. Setup

Before setup:

```text
hero.get_context -> allowed
hero.bootstrap   -> allowed
all other tools -> HP001
```

Bootstrap is caller-request-idempotent. Configure is post-setup preference-only.

stdio wizard text is never written directly to stdout; conversational setup belongs to host/Skill and interactive terminal setup to CLI.

## 10. Read-only policy

MCP reads (`get_context`, `list`, `get_card`) must be physically read-only: no read-driven Project creation, last-seen updates or analytics WAL writes.

## 11. Release evidence

```text
current tool inventory/order/annotations
closed schemas
structured/text semantic equality
bootstrap replay/concurrency/crash
get_context pre/post setup + all-Hero recovery
Start explicit Hero/Project retry scope
Finish finishRequestId + HP136
one-open Quest invariant
read-only no-write
level-cap shapes
SafeText/UUID/time/integer vectors
MCP Inspector
2026-07-28 + 2025-11-25 qualification
Codex vertical E2E
cross-host Skill smoke after reference path
```
