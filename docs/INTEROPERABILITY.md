# Hero Passport — Interoperability

**Status:** Accepted v3.2.1 interoperability contract  
**Snapshot:** 2026-08-11

## 1. Product portability

Hero Passport semantics are host-neutral:

```text
portable local Core: HP-MCP/2
portable orchestration: Agent Skill format where supported
host-specific layer: installation/configuration only
```

Codex is the first release-blocking qualified reference host, not the definition of product behavior.

## 2. MCP protocol

Preferred semantics: `2026-07-28`.

Application correctness is independent of protocol sessions/connections. Stateful work uses explicit ordinary data (`heroId`, `questId`, mutation request IDs).

Qualification also exercises `2025-11-25` compatibility through the official C# SDK path.

## 3. MCP tool contract

The current HP-MCP/2 v3.2.1 tool set/schemas/errors have identical meaning across hosts. The exact current inventory is normative in `WIRE-CONTRACT.md`; do not turn its present count into a permanent architecture rule.

Hosts may differ in stdio launch, tool-call UX, Skill install/activation, cwd/project binding and Markdown rendering. Those differences never alter Core invariants.

## 4. Agent Skill portability

Portable shape:

```text
skills/hero-passport/SKILL.md
skills/hero-passport/references/*
```

Skill uses `hero.get_context` to hydrate persisted settings/recovery/version compatibility rather than relying on host-local remembered defaults.

Trigger description/evals should activate for meaningful project work without turning every programming conversation into a Quest.

If a host lacks open Skill-format support, integration may map the same concise lifecycle guidance to that host’s current supported instruction mechanism. Mapping is release-time documented/smoke-tested and never contains independent reward rules.

## 5. Result portability

Machine consumers use `structuredContent` as canonical result.

One deterministic serialized JSON TextContent remains semantically equal for backwards compatibility. Whitespace/minification is not business semantics.

## 6. Locale portability

Canonical keys/numbers are host-neutral. `ru-RU` / `en-US` are presentation resources.

Persisted locale/presentation preferences are returned by get_context after restart.

## 7. Project interoperability

Project identity is local server state, not a model-supplied filesystem path.

Integration supplies correct cwd or explicit `--project-root`. Linked worktrees/monorepos/submodules follow `PROJECT-IDENTITY.md` consistently.

Linked worktrees share ProjectId, so same-Hero parallel independent open Quests in linked worktrees are explicitly unsupported in 0.1 across every host.

## 8. Multi-host active Hero semantics

Global active Hero is a default preference only.

Skill selects a Hero from context and sends explicit `heroId` on Start. If another host activates a different Hero concurrently, it cannot silently retarget that already formed request.

Recovery context lists open Quests for the current Project across all Heroes.

## 9. Qualification states

```text
Qualified             release-blocking E2E evidence on current version
Documented compatible setup documented; protocol expected; limited smoke evidence
Unsupported / unknown no current support claim
```

A stale historical test never implies permanent qualification.

## 10. Cross-host smoke

For each claimed host/version record:

```text
install/connect method
project binding behavior
current tool discovery
get_context pre/post setup
bootstrap
Skill Start/Finish lifecycle
explicit Hero ownership
structured result rendering
restart/all-Hero recovery
HP135/HP136 handling where practical
host tool-confirmation behavior
known limitations
```

Permanent Hero delete is CLI-only and is not a host MCP UX qualification item in 0.1.

## 11. No host identity coupling

Never use host/client name/version as Hero identity, Quest owner, auth, reward input, Project identity or idempotency key.

Host metadata is safe diagnostic/qualification context only.
