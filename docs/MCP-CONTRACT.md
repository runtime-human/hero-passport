# Hero Passport — MCP Contract Overview

**Status:** Accepted HP-MCP/2 v3.2 overview  
**Snapshot:** 2026-08-11  
**SDK:** official C# `ModelContextProtocol 2.1.0`  
**Preferred protocol:** MCP `2026-07-28`  
**0.1 transport:** stdio

Exact field/schema/result semantics are normative in `WIRE-CONTRACT.md`.

## 1. Static tool surface

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

Explicit deterministic registration only. No aliases, assembly scanning or dynamic tool sets in 0.1.

## 2. Stateless protocol posture

Application correctness never depends on:

```text
Mcp-Session-Id
connection identity
server instance lifetime
initialize state
in-memory per-client workflow state
```

MCP `2026-07-28` uses a stateless core. Hero Passport carries ordinary explicit state handles/IDs such as `questId` between calls.

The official SDK handles the compatibility path for legacy `2025-11-25`; qualification tests exercise both paths.

## 3. Features used/not used

Required:

```text
Tools
```

Not required by 0.1 correctness:

```text
Resources
Prompts
MCP Tasks
MCP Apps
own Streamable HTTP/OAuth
server-side session state
```

Hero Quest is not an MCP Task: coding/research work occurs in the external AI agent, while Hero Passport calls are short local state mutations/reads.

## 4. Model-control boundary

MCP tools are model-invocable, but host applications control their own human-confirmation UX. Hero Passport does not require an additional in-product confirmation for normal start/finish/read operations.

Permanent Hero delete remains explicitly destructive and requires target confirmation at the server contract even if a host also prompts.

## 5. Agent Skill

The complete ambient Quest lifecycle policy lives in `AGENT-SKILL.md`, not in MCP connection state.

The Skill:

```text
recognizes meaningful work
starts/resumes a Quest
carries questId
collects bounded finish facts
finishes when the goal is done
renders canonical progression
```

The MCP Core validates all invariants and calculates all game state, so correctness does not depend on perfect model behavior.

## 6. Idempotency

Caller-generated mutation request IDs make retry intent explicit for create/start/delete. Finish is idempotent by `questId` and immutable persisted result.

Natural-language `goal` is never used as an idempotency key.

## 7. Result compatibility

Success:

```text
structuredContent = canonical typed result
content            = exactly one minified JSON TextContent
                     semantically equal to structuredContent
```

Expected tool/business error:

```text
isError=true
one safe TextContent
no structuredContent
```

## 8. Server instructions

Server instructions stay short. Canonical meaning:

```text
Use Hero Passport for meaningful project work. Prefer the installed Hero Passport Agent Skill for automatic lifecycle behavior. Carry explicit questId between calls. Never send source code, diffs, raw logs, prompts, secrets, environment dumps or workspace paths. If an open Quest is lost, use hero.list_active_quests; never infer identity from similar goal text.
```

Instructions are guidance, never security/invariant enforcement.

## 9. stdio safety

```text
stdin/stdout -> MCP protocol only
stderr       -> safe diagnostics
```

First-run conversational questions are conducted by the host/Skill. The MCP process never corrupts stdout with a terminal wizard.

## 10. Release evidence

```text
exact tool inventory/order
exact annotations/schemas
structured/text equivalence
SafeText/UUID/time/integer vectors
mutation request replay/mismatch
one-open-Quest behavior
Finish replay/concurrency
MCP Inspector
2026-07-28 path
2025-11-25 compatibility path
Codex E2E
cross-host Skill smoke matrix
```
