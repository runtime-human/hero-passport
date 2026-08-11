# Hero Passport — Interoperability

**Status:** Accepted v3.2 interoperability contract  
**Snapshot:** 2026-08-11

## 1. Product portability

Hero Passport semantics are host-neutral:

```text
portable local Core: HP-MCP/2
portable orchestration: Agent Skill format where supported
host-specific layer: installation/configuration only
```

Codex is the first release-blocking Qualified reference host, not the definition of Hero Passport behavior.

## 2. MCP protocol

Preferred semantics:

```text
2026-07-28
```

The application is stateless with respect to protocol sessions/connections. Stateful work uses explicit ordinary tool data (`questId`, mutation request IDs).

Compatibility qualification also exercises `2025-11-25` through the official C# SDK’s supported compatibility behavior.

Do not pin product meaning to a transport handshake or host-specific client metadata.

## 3. MCP tool contract

The same eleven HP-MCP/2 tools, schemas, errors and game meanings apply across hosts.

Hosts may differ in:

```text
how they launch stdio
whether they display tool-call confirmations
how they install/activate Skills
how project cwd is provided
how they render Markdown/text
```

Those differences do not change server invariants.

## 4. Agent Skill portability

Primary portable format follows the open Agent Skills specification:

```text
skills/hero-passport/SKILL.md
skills/hero-passport/references/*
```

Trigger description should be specific enough to activate for meaningful project work but not every programming conversation. Release AgentEvals measure both under-trigger and over-trigger behavior.

If a host does not implement the open Skill format, integration may map the same concise lifecycle guidance to the host’s supported instruction mechanism. Such mapping is documented and smoke-tested; it must not contain independent reward rules.

## 5. Result portability

Machine consumers use `structuredContent` fields. Human presentation is fallback/UX.

Compatibility TextContent contains JSON semantically equal to structured content, so a host that only sees text can still receive the machine result without a separate contradictory status language.

## 6. Locale portability

Canonical keys and numbers are host-neutral. `ru-RU` / `en-US` are presentation resources.

A host may converse in another language while Hero Passport still renders one supported locale; unsupported locale selection fails/falls back according to explicit configuration policy rather than inventing translated game keys.

## 7. Project interoperability

Project identity is local server state, not sent from the model as a path.

Integration should provide correct cwd or explicit `--project-root`. Linked worktrees/monorepos/submodules follow `PROJECT-IDENTITY.md` consistently regardless of host.

## 8. Qualification states

Use three support labels:

```text
Qualified                  release-blocking E2E evidence on current version
Documented compatible      setup documented; protocol expected; smoke evidence limited
Unsupported / unknown      no current support claim
```

A stale historical integration test does not justify a permanent Qualified label.

## 9. Cross-host smoke

For each claimed host version/integration path record:

```text
install/connect method
project binding behavior
11-tool discovery
first-run setup
Skill lifecycle start/finish
structured result rendering
restart/recovery
host confirmation behavior
destructive delete UX
known limitations
```

## 10. No host identity coupling

Never use host/client name/version as:

```text
Hero identity
Quest owner
authentication
reward input
project identity
idempotency key
```

It is safe diagnostic/qualification metadata only.
