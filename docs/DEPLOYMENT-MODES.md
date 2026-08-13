# Hero Passport — Deployment Modes

**Status:** Accepted v3.2.1 boundary specification  
**Snapshot:** 2026-08-11

## 1. 0.1 primary profile — local project-bound stdio

```text
AI/MCP host
  -> launches hero-passport mcp
  -> stdin/stdout MCP
  -> local Application
  -> same-host SQLite
```

Trust boundary: one local OS user, host allowed to execute the command, local filesystem permissions.

No Hero Passport network listener, cloud account or OAuth flow.

Project binding:

```text
explicit --project-root else process cwd
-> project-identity/1
```

Hero binding: `activeHeroId` is only the default preference used by Skill/CLI when forming a new request. `hero.start_quest` carries explicit `heroId`; persisted Quest ownership is immutable.

## 2. Agent Skill deployment

Official Hero Passport Agent Skill may be installed separately from executable.

Portable Skill contains lifecycle/report/presentation policy and references, not secrets or mutable game state.

At activation/restart it uses `hero.get_context` for persisted settings, version compatibility and current-Project recovery.

Host-specific installation paths/config live under `docs/integrations/` and are release-smoke-tested; they never define product semantics.

## 3. stdio rules/onboarding

```text
stdout -> MCP protocol frames only
stderr -> safe diagnostics/logs
```

No terminal wizard is printed into MCP stdout.

Conversational first run:

```text
hero.get_context -> setupCompleted=false
Skill conducts short onboarding
hero.bootstrap with bootstrapRequestId
```

Post-setup preference changes use `hero.configure`.

## 4. SQLite deployment policy

Writable database is same-host local filesystem only.

Effective profile includes WAL, FULL synchronous, foreign keys ON, trusted_schema OFF and no shared cache.

Connection-scoped pragmas must be applied on every actual product connection; pooled/new-process behavior is qualified.

## 5. 0.2 local Web profile

Future local browser UI:

```text
Browser
  -> HeroPassport.Web on loopback/local process
  -> Application/read models
  -> same local SQLite
```

This is presentation/management; MCP Core/game semantics remain shared.

## 6. Future project-scoped Streamable HTTP

Deferred until a concrete consumer requires URL-based MCP.

Future HTTP must bind Project identity explicitly from server/auth configuration; cwd is not caller identity in a shared service.

Use current official MCP ASP.NET Core adapter/security requirements rather than custom framing.

## 7. Future public/multi-tenant service

A different architecture requiring HTTPS, current MCP authorization, authenticated principal, Hero/Project authorization, tenant isolation, remote durable store, abuse controls, secrets, backups and explicit retention/deletion/security logging.

Local fingerprints, questId and mutation request IDs are not authentication credentials.

## 8. Optional future sync

No sync requirement in 0.1/0.2. Current schema is sync-conscious, not sync-ready.

Future sync requires dedicated cross-device identity/conflict/delete/security design. Never point two machines at one shared writable SQLite WAL file.

## 9. Unsupported 0.1 profiles

```text
writable SQLite on network/NFS/cloud-shared filesystem
multiple hosts writing one DB file
public unauthenticated HTTP
legacy SSE server
team/shared local DB
```

## 10. Invariants across future adapters

Every future adapter preserves explicit mutation request identity, explicit Hero Start ownership, one open Quest per Hero+Project, immutable Quest owner, HP136 finalization-conflict detection, at-most-once committed progression, deterministic rule versions, bounded attestation semantics and privacy deny-list.

Transport differences never silently change game semantics.
