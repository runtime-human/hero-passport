# Hero Passport — Deployment Modes

**Status:** Accepted v3.2.1 core + implemented 0.2-A/B/C/D/E Web boundary  
**Snapshot:** 2026-09-17

## 1. 0.1 primary profile — local project-bound stdio

```text
AI/MCP host
  -> launches hero-passport mcp
  -> stdin/stdout MCP
  -> local Application
  -> same-host SQLite
```

Trust boundary: one local OS user, host allowed to execute the command, local filesystem permissions.

No 0.1 Hero Passport network listener, cloud account or OAuth flow.

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

0.2-A introduced the first local browser slice:

```text
Browser
  -> HeroPassport.Web on code-defined loopback listener
  -> Blazor static SSR / bounded Web presentation model
  -> existing Application read semantics
  -> same local SQLite authority
```

The listener remains programmatic IPv4 loopback with an OS-assigned port. Generic URL configuration such as `ASPNETCORE_URLS=http://0.0.0.0:0` cannot widen the actual listener because the code-defined Kestrel endpoint is authoritative. LAN/public/wildcard binding is unsupported.

0.2-B adds a browser authorization envelope before any Web management/mutation surface:

```text
process start
  -> independent random bootstrap capability + session bearer
  -> Kestrel binds http://127.0.0.1:<dynamic-port>
  -> system browser opens /__hero/bootstrap#<capability>
  -> fragment removed from history and submitted in same-origin form POST
  -> antiforgery + origin + one-time capability validation
  -> HttpOnly SameSite=Strict process-session cookie
  -> authenticated static-SSR dashboard
```

The supported browser authority is exactly `127.0.0.1`. Host Filtering is code-owned with `AllowedHosts = ["127.0.0.1"]`; `localhost`, arbitrary DNS names, wildcard hosts and forwarded-host/reverse-proxy semantics are not part of 0.2-B/C/D/E.

The bootstrap capability and browser session bearer are independent 32-byte cryptographically random process-local values. They are not derived from Project identity, PID, port, machine/user identity or database state; they are not persisted. Bootstrap consumption is one-time and atomic. A wrong guess does not consume the legitimate capability; replay after success fails.

The launch URI places the bootstrap capability in the URL fragment, not query/path/request target. `GET /__hero/bootstrap` is state-free and public only to establish the local session. `POST /__hero/bootstrap/claim` is the single internal security endpoint and validates antiforgery plus local origin before issuing the cookie. All other product routes and product static assets require the current process session and return `401` before dashboard/Application reads when it is absent or stale.

Session cookie policy:

```text
Name=.HeroPassport.LocalSession
HttpOnly=true
SameSite=Strict
Path=/
Expires absent
Max-Age absent
Secure=false for current HTTP-loopback profile
```

`Secure=false` reflects the actual plain-HTTP local profile; 0.2-B/C/D/E makes no local-HTTPS claim. A future HTTPS slice would need its own certificate/lifecycle qualification.

Every Web process restart generates new bootstrap/session secrets, so a browser cookie from a prior process fails closed even if a port is reused. No Web session state is written to SQLite.

Project binding remains:

```text
explicit --project-root else process cwd
-> project-identity/1
```

0.2-A/B remain read-only. 0.2-C adds explicitly confirmed Start Quest. 0.2-D adds explicitly confirmed Finish Quest for one current-Project open Quest. 0.2-E adds authenticated read-only current-Project Quest history.

### 0.2-C Start Quest

```text
authenticated GET /quests/start
  -> dedicated static-SSR DTO: questType/title/goal
  -> Application PrepareStartQuest validation/normalization
  -> no Quest mutation
  -> process-local pending confirmation (max 8, 10-minute TTL)
  -> opaque 16-byte-random base64url handle in route
  -> authenticated same-origin antiforgery-protected confirm POST
  -> existing Application StartQuestAsync authority
  -> exactly the prepared HeroId + StartRequestId + normalized fields
```

### 0.2-D Finish Quest

```text
authenticated GET /quests/finish/{questId}
  -> questId parsed as canonical lowercase UUIDv7
  -> selector resolves only against current-Project open Quest context
  -> dedicated static-SSR DTO: result/summary/bounded attestations/1..3 Skills
  -> one FinishRequestId generated
  -> Application PrepareFinishQuest validation/normalization
  -> no report/XP/finalization mutation
  -> dedicated process-local pending confirmation (max 8, 10-minute TTL)
  -> opaque 16-byte-random base64url handle in route
  -> authenticated same-origin antiforgery-protected confirm POST
  -> existing Application FinishQuestAsync authority
  -> exact prepared QuestId + FinishRequestId + normalized payload
```

Both confirmation steps are human UX safety gates, not authentication. Active-Hero preference changes after preparation cannot retarget persisted Quest ownership. Pending confirmations are never persisted and disappear on process restart. Quest title/goal/summary and attestations are not placed in redirect/query URLs.

Start and Finish mutation POSTs are limited before form/Application processing and accept only `application/x-www-form-urlencoded`:

```text
Start prepare + confirm:
  body ceiling = 8192 bytes
  encoded individual value ceiling = 2048 bytes

Finish prepare:
  body ceiling = 131072 bytes (128 KiB)
  encoded individual value ceiling = 112 KiB
  textarea raw UTF-16 ceiling = 12000 code units

Finish confirm:
  body ceiling = 8192 bytes
  encoded individual value ceiling = 2048 bytes

All mutation forms:
  form entry count = 16
  form key ceiling = 128 bytes
  dedicated static-SSR form models and unique form names
```

The larger Finish-prepare limits are transport-only and exist so Application-valid text is not rejected before SafeText normalization. The semantic summary contract remains SafeTextV1 `1..2000` Unicode scalars after NFC/whitespace normalization. Canonically decomposed input can contain up to three raw code points for one composed Hangul syllable; URL percent-encoding expands UTF-8 further. The qualified 128 KiB / 112 KiB prepare envelope admits that bounded canonical-decomposition case while the payload-free Finish confirmation route remains at the stricter 8 KiB / 2 KiB boundary. Bootstrap keeps its separate 1024-byte boundary.

Finish idempotency/conflict behavior is inherited rather than reimplemented: success, same-request replay and equivalent `AlreadyFinalized` converge; `HP135`/`HP136` remain bounded terminal conflicts; an unknown response outcome releases the same pending entry so retry preserves its `FinishRequestId`.

### 0.2-E bounded Quest history

```text
authenticated GET /history
  -> existing session boundary
  -> setup gate through GetRuntimeContextAsync
  -> Application GetProjectQuestHistoryAsync
  -> current Project only
  -> started_at_utc DESC, QuestId DESC
  -> maximum 25 rows

authenticated GET /history/{questId}
  -> canonical lowercase UUIDv7 parse before history lookup
  -> Application GetQuestHistoryDetailAsync
  -> QuestId + current Project predicate
  -> missing and foreign-Project Quest share one 404 path
  -> .NET 10 NavigationManager.NotFound / Router.NotFoundPage
```

History is static SSR and GET-only. It has no form, query/filter protocol, client-side interactivity or product REST endpoint. Web consumes dedicated Application presentation contracts and owns no SQL/EF. Infrastructure performs parameterized reads through the existing `SqliteHeroPassportStateStore`; multi-query reads use a short deferred read snapshot. Visiting history for an unseen Project does not create that Project row, and qualified history GETs commit no durable product-state change. No schema, migration or index was added for 0.2-E.

0.2-C/D/E do not add Hero/settings management, Identity/OAuth/accounts, public/LAN hosting, local HTTPS, reverse-proxy support, Streamable HTTP MCP or a general REST/minimal-API product surface. `Security/BootstrapEndpoint.cs` remains the only Minimal API-style POST endpoint.

The production process opens the system browser only after the actual loopback endpoint is known. If browser launch fails synchronously, startup fails closed instead of exposing or printing the capability. `--no-open-browser` plus deterministic Web secrets are Testing-only seams and are rejected outside `ASPNETCORE_ENVIRONMENT=Testing`.

Static assets use the ASP.NET Core static-web-assets manifest. Source-backed assets are explicitly enabled only for the `Testing` process qualification profile; Production does not opt into source-backed static Web assets. Final published Web artifact/static-asset and broader launch/package qualification remains part of the later 0.2 release work.

## 6. Future project-scoped Streamable HTTP

Deferred until a concrete consumer requires URL-based MCP.

Future HTTP must bind Project identity explicitly from server/auth configuration; cwd is not caller identity in a shared service.

Use current official MCP ASP.NET Core adapter/security requirements rather than custom framing.

## 7. Future public/multi-tenant service

A different architecture requiring HTTPS, current MCP authorization, authenticated principal, Hero/Project authorization, tenant isolation, remote durable store, abuse controls, secrets, backups and explicit retention/deletion/security logging.

The 0.2-B/C/D/E local process capability/session is not a public authentication system and must not be reused as one.

Local fingerprints, questId, confirmation handles and mutation request IDs are not authentication credentials.

## 8. Optional future sync

No sync requirement in 0.1/0.2. Current schema is sync-conscious, not sync-ready.

Future sync requires dedicated cross-device identity/conflict/delete/security design. Never point two machines at one shared writable SQLite WAL file.

## 9. Unsupported 0.1/0.2-A/B/C/D/E profiles

```text
writable SQLite on network/NFS/cloud-shared filesystem
multiple hosts writing one DB file
public unauthenticated HTTP
LAN/wildcard Web binding
localhost/IPv6 authority in the current Web profile
reverse proxy / forwarded-host deployment
persistent Web browser sessions across process restarts
legacy SSE server
team/shared local DB
```

## 10. Invariants across future adapters

Every future adapter preserves explicit mutation request identity, explicit Hero Start ownership, one open Quest per Hero+Project, immutable Quest owner, HP136 finalization-conflict detection, at-most-once committed progression, deterministic rule versions, bounded attestation semantics and privacy deny-list.

Transport differences never silently change game semantics.
