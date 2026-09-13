# Hero Passport — Security and Privacy

**Status:** Accepted v3.2.1 core + implemented 0.2-B/C local Web boundary  
**Snapshot:** 2026-09-13

## 1. Security posture

Hero Passport is a local companion, not an agent permission gateway, anti-cheat system, source-code auditor or employee-monitoring product.

0.1 threat focus:

- accidental/destructive invocation;
- malformed/untrusted model input;
- sensitive leakage through logs/errors/storage;
- unsafe retries/double progression;
- project identity/path disclosure;
- database corruption/unsupported storage;
- migration crash recovery;
- protocol stdout contamination;
- Skill/Core version skew.

0.2 local Web additionally treats the browser request boundary as untrusted despite loopback transport: hostile Host values, cross-site localhost requests, DNS-rebinding-style Host abuse, stale/replayed browser capabilities, oversized/form-abuse requests and tampered confirmation handles must fail closed.

## 2. Privacy minimum

Routine Hero Passport inputs/storage must not include:

```text
source/file contents
diffs/patches
raw terminal/build/test logs
full prompts/chat transcripts
secrets/tokens/API keys
environment dumps
full workspace paths
Git remote URLs
arbitrary free-form metadata/context payloads
```

Allowed bounded Quest content:

```text
title
goal
summary
canonical skills
small result/attestation counters
build/test semantic status + provenance
game progression
```

## 3. Quest metadata may still be sensitive

No-source telemetry does not mean zero sensitive metadata.

Quest `title`, `goal` and `summary` may contain confidential project/client/release information supplied by user/agent.

SafeText protects Unicode/control hygiene, not semantic secrets/redaction.

Users should assume bounded Quest metadata is stored locally in Hero Passport history.

## 4. Attestations without surveillance

Use the terms **bounded agent attestations** / **reported signals**.

`observed` means the agent asserts it directly ran/saw the referenced result. It is not independently verified by Hero Passport.

Hero Passport does not read source/diffs/raw logs to prove these claims.

Trust/Strain are RPG stats derived from these bounded signals, not objective productivity/reliability telemetry.

## 5. Safe text

Persisted/model-returned user/model strings pass SafeTextV1.

Reject dangerous controls/bidi formatting/malformed Unicode; normalize NFC/whitespace and bound scalar count.

No natural-language input becomes executable shell/SQL/code.

## 6. SQL/schema

EF/parameterized commands only for data. Never interpolate model text into SQL identifiers/strings.

Migration SQL is developer-authored static schema code.

Initial schema uses CHECK/FK/index constraints so malformed state is rejected even if Application validation regresses.

`trusted_schema=OFF` is applied on product connections after compatibility qualification.

## 7. Permanent Hero deletion is CLI-only

MCP exposes reversible:

```text
hero.archive
hero.restore
```

0.1 does **not** expose permanent delete as a model-controlled tool.

Reason: a model can read a Hero name, so “confirmHeroName” is not proof of human destructive intent. Requiring MRTR just for rare administration would also expand host-qualification scope.

Future model-controlled permanent delete requires a separately reviewed human-confirmation design and contract revision.

## 8. Logical delete vs forensic erasure

Normative claim:

> Permanent Hero delete irreversibly removes the Hero from the active Hero Passport logical database state. Hero Passport does not claim forensic secure erasure from storage media, filesystem snapshots, backups or previously exported copies.

No 0.1 guarantee that deleted bytes are unrecoverable from SQLite free pages/media forensics.

Do not imply `secure_delete`, VACUUM, device storage or backup deletion semantics that the product does not enforce.

## 9. Retry identity/security

Never infer retry from natural-language equality.

Request IDs + versioned canonical hash prevent one request token with changed context/intent from silently applying a different mutation.

Receipts persist only minimal IDs/hash/version/context/status and may outlive a deleted target as `target_deleted` to prevent accidental resurrection.

Request IDs are not auth secrets.

## 10. Active Hero and ownership safety

Global active Hero is a preference/default only.

Start mutation takes explicit `heroId`; another local host changing active Hero cannot silently retarget an already-formed Start request.

0.2-C preserves this rule across its human confirmation step: prepared state captures the intended Hero and one `StartRequestId`; commit uses those prepared identities instead of re-reading the current active Hero for ownership.

Existing Quest ownership is immutable.

## 11. MCP annotations

Annotations are UX/model hints, not security controls.

Server-side validation/invariants enforce setup/bootstrap state, safe IDs/text/enums, Hero/open-Quest guards, Project context, idempotency mismatch and finalized-Quest conflict.

## 12. MCP stdio

stdout is protocol only. Diagnostics use privacy-scrubbed stderr.

Never log full request bodies by default, especially goal/summary, environment variables or secrets.

## 13. Read-only means no hidden writes

`hero.get_context`, `hero.list`, `hero.get_card` must not create Project rows, update last-seen analytics or write preferences merely because they were called.

This keeps `readOnlyHint` truthful and reduces unnecessary WAL/lock churn.

The 0.2-A/B dashboard keeps the same no-hidden-write property. 0.2-C preparation is also non-mutating; only the explicit confirmation step invokes the existing Start mutation authority.

## 14. Project privacy

Persist salted workspace fingerprint/display name, not full path/remote.

Routine MCP outputs omit internal ProjectId/fingerprint/path.

Web confirmation renders only safe Hero/Project display values plus normalized Quest type/title/goal. It does not render full workspace paths, Project fingerprints, internal HeroId, mutation request identity or persistence internals.

Git identity resolver is read-only, scrubs redirection env vars, does not weaken `safe.directory`.

## 15. SQLite storage

No encryption-at-rest claim in 0.1/0.2. Hero Passport relies on user/OS/device/filesystem protection.

If application-level DB encryption becomes required, select/threat-model it explicitly rather than implying SQLitePCLRaw provides encryption.

## 16. Network boundary

0.1 local stdio mode has no Hero Passport cloud endpoint, own OAuth or telemetry upload.

0.2 local Web binds only to code-defined IPv4 loopback with canonical browser authority `http://127.0.0.1:<dynamic-port>`. LAN/public/wildcard binding, `localhost`/IPv6 authority, reverse proxy/forwarded-host deployment and local HTTPS are unsupported in the current profile.

Future public HTTP/sync requires separate auth/authz/encryption/deletion/conflict/privacy threat model. The local process browser capability/session is not public authentication.

## 17. 0.2 local Web browser and mutation boundary

0.2-B establishes the browser authorization envelope:

```text
Kestrel = IPv4 loopback only
Host allowlist = 127.0.0.1 only
bootstrap capability = independent random 32 bytes, one-time
session bearer = independent random 32 bytes, process-lifetime
bootstrap transport = URL fragment, removed before same-origin claim POST
bootstrap claim = antiforgery + local origin + exact capability
session cookie = HttpOnly + SameSite=Strict + Path=/ + non-persistent
all product routes without current session = 401 before product reads
unsupported Host = 400
process restart = prior browser session invalid
```

Bootstrap/session secrets are never persisted and must not appear in ordinary process logs, product HTML, redirect targets or SQLite. The Testing-only deterministic secret and `--no-open-browser` seams are rejected outside the exact Testing environment.

0.2-C reuses that boundary without creating parallel authentication. Its confirmation handle is random process-local lookup state, not an auth credential. Pending confirmation state is capped at 8 entries, expires after 10 minutes and is never persisted. Malformed handles fail with 400; unknown/expired handles fail with 410; neither can mutate.

Mutation request hardening:

```text
routes = POST /quests/start and POST /quests/start/confirm/*
content type = application/x-www-form-urlencoded only
body ceiling = 8192 bytes before antiforgery/form parsing
form models = dedicated Web DTOs only
static SSR form names = unique
session + same-origin + antiforgery = still mandatory
bootstrap claim keeps its separate 1024-byte boundary
```

Quest title/goal may be rendered in the authenticated prepare/confirmation UX, but are excluded from redirect/query URLs and ordinary diagnostics. Web does not bind Domain/Application records directly from form input and does not expose a general REST/minimal-API product surface. `POST /__hero/bootstrap/claim` remains the only Minimal API-style endpoint.

This boundary does not claim isolation from a malicious same-user process able to inspect process memory or browser storage.

## 18. Logs/diagnostics allowlist

Safe diagnostic fields may include:

```text
error code/category
operation/tool
rule/schema/contract versions
SQLite version/pragmas
bounded timing values
UUIDs where useful
```

Default diagnostics exclude Quest text, paths/remotes, bound SQL values, browser capability/session/antiforgery values and raw exception material that exposes user content.

## 19. Security tests

Release gates include:

```text
SafeText hostile vectors
closed MCP schemas
UUID parsing
bootstrap/Start/Finish request reuse with changed args -> HP135
Finish semantic disagreement -> HP136
one-open race
read-only no-write assertions
SQLite CHECK/FK direct-invalid-write rejection
trusted_schema OFF / foreign_keys ON
logical CLI delete guards + target_deleted receipts
privacy wording does not claim forensic erasure
stdout protocol-only
forbidden DTO/entity/log fields absent
Skill/Core incompatibility fails safe
```

0.2 Web security qualification additionally proves:

```text
loopback-only listener cannot be widened by ASPNETCORE_URLS
hostile Host fails before product processing
wrong bootstrap does not consume the valid one; successful replay fails
prior-process browser session fails after restart
missing/cross-site antiforgery claims fail closed
security secrets are absent from normal output/SQLite/product HTML
Production rejects Testing-only secret/no-browser bypasses
Start prepare creates no Quest
multipart mutation form -> 415
oversized mutation form -> 413
missing-antiforgery Start POST -> 400/no mutation
explicit confirm creates one Quest through Application
repeat confirm is idempotent
prepared Hero/request identity is stable across active-Hero preference changes
malformed/unknown confirmation handles fail 400/410 without mutation
confirmation HTML omits internal IDs/fingerprint/request identity/full path
```
