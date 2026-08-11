# Hero Passport — Security and Privacy

**Status:** Accepted v3.2.1  
**Snapshot:** 2026-08-11

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

## 14. Project privacy

Persist salted workspace fingerprint/display name, not full path/remote.

Routine MCP outputs omit internal ProjectId/fingerprint/path.

Git identity resolver is read-only, scrubs redirection env vars, does not weaken `safe.directory`.

## 15. SQLite storage

No encryption-at-rest claim in 0.1. Hero Passport relies on user/OS/device/filesystem protection.

If application-level DB encryption becomes required, select/threat-model it explicitly rather than implying SQLitePCLRaw provides encryption.

## 16. Network boundary

0.1 local stdio mode has no Hero Passport cloud endpoint, own OAuth or telemetry upload.

Future HTTP/sync requires separate auth/authz/encryption/deletion/conflict/privacy threat model.

## 17. Logs/diagnostics allowlist

Safe diagnostic fields may include:

```text
error code/category
operation/tool
rule/schema/contract versions
SQLite version/pragmas
bounded timing values
UUIDs where useful
```

Default diagnostics exclude Quest text, paths/remotes, bound SQL values and raw exception material that exposes user content.

## 18. Security tests

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
