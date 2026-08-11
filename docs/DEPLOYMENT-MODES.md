# Hero Passport — Deployment Modes

**Status:** Accepted v3.2 boundary specification  
**Snapshot:** 2026-08-11

## 1. 0.1 primary profile — local project-bound stdio

```text
AI/MCP host
  -> launches hero-passport mcp
  -> stdin/stdout MCP
  -> local Application
  -> same-host SQLite
```

Trust boundary:

```text
one local OS user
host allowed to execute the command
local filesystem permissions
```

No Hero Passport network listener, cloud account or OAuth flow.

Project binding:

```text
explicit --project-root else process cwd
-> project-identity/1
```

Hero binding: globally active Hero for new Quests; existing Quest owner is persisted and immutable.

## 2. Agent Skill deployment

The official Hero Passport Agent Skill is installed into a host-supported Skill/instruction location separately from the executable when necessary.

The portable Skill does not contain secrets or mutable game state. It only contains lifecycle/report/presentation instructions and references.

Host-specific installation paths/config are documented under `docs/integrations/` and are release-smoke-tested; they do not define product semantics.

## 3. stdio rules

```text
stdout -> MCP protocol frames only
stderr -> safe diagnostics/logs
```

No first-run terminal wizard is written into MCP stdout. Conversational onboarding occurs through the host/Skill and `hero.configure`.

## 4. 0.2 local Web profile

Future local browser UI:

```text
Browser
  -> HeroPassport.Web on loopback/local process
  -> Application/read models
  -> same local SQLite
```

This is primarily presentation/management; MCP Core semantics do not change.

Exact hosting/origin/security design is finalized before 0.2 implementation.

## 5. Future project-scoped Streamable HTTP

Deferred until a concrete consumer requires URL-based MCP.

A future HTTP deployment must bind Project identity explicitly from server configuration/auth context; process cwd is not a caller identity mechanism for a shared HTTP service.

If implemented, use the official MCP ASP.NET Core adapter and current transport/security requirements. Do not build custom HTTP framing when the official SDK already supplies it.

## 6. Future public/multi-tenant service

This is a **different security/storage architecture**, not local mode with a public bind.

It requires at minimum:

```text
public HTTPS
current MCP authorization compliance
authenticated principal
Hero/Project authorization
tenant isolation
remote durable store choice
rate/abuse controls
secret management
backup/restore
privacy/retention/delete policy
security/operational logging
```

Local project fingerprints, client name, `questId` and request IDs are never authentication credentials.

## 7. Optional future sync

Sync is not a deployment requirement in 0.1/0.2. The local database remains useful offline with no server.

A sync service requires its own conflict/delete/security design. Do not point two machines at one shared SQLite WAL file.

## 8. Unsupported 0.1 profiles

```text
writable SQLite on network/NFS/cloud-shared filesystem
multiple hosts writing one DB file
public unauthenticated HTTP
legacy SSE server
team/shared local DB
```

## 9. Invariants across future adapters

Every future adapter preserves:

```text
explicit mutation request identity
one open Quest per Hero+Project
immutable Quest owner
at-most-once committed progression
same deterministic rule versions
same bounded fact/provenance semantics
same privacy deny-list
same stable HP error meanings
```

Transport differences never silently change game semantics.
