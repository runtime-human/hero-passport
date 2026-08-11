# Hero Passport — Security and Privacy

**Status:** Accepted v3.2  
**Snapshot:** 2026-08-11

## 1. Security posture

Hero Passport is a local companion, not an agent permission gateway, anti-cheat system, source-code auditor or employee-monitoring product.

0.1 threat focus:

- accidental/destructive tool invocation;
- model-supplied malformed/untrusted input;
- sensitive data leakage through logs/errors/storage;
- unsafe retries/double progression;
- project identity/path disclosure;
- database corruption/unsupported storage;
- protocol stdout contamination.

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

Allowed Quest content is intentionally bounded:

```text
title
goal
summary
canonical skills
small result/quality counters
build/test semantic status + provenance
game progression
```

## 3. Provenance without surveillance

`observed | reported | none` records how a build/test fact was known. It does not store evidence artifacts.

Hero Passport trusts bounded agent self-report after validation. It does not independently read source/repo/logs to prove quality in 0.1.

## 4. Safe text

All persisted/model-returned user/model strings pass SafeTextV1 before use.

Reject dangerous controls/bidi formatting and malformed Unicode; normalize NFC/whitespace and bound by scalar count.

This is data-hygiene/input-hardening, not a claim that natural language is “safe to execute”. No input string becomes shell/SQL/code without a dedicated non-existent feature.

## 5. SQL

EF/parameterized commands only for data. Never interpolate model text into SQL identifiers or SQL strings.

Migration SQL is developer-authored static schema code, never model runtime input.

## 6. Destructive Hero delete

`hero.delete` is explicitly destructive and requires:

```text
deleteRequestId
heroId
confirmHeroName exactly matching current normalized name
```

Server rejects deleting:

- current active Hero;
- Hero with any open Quest;
- confirmation mismatch.

The request ID makes transport retry non-duplicating. Host UI confirmations are additional and not relied on as server enforcement.

## 7. Archive vs delete

Ordinary user removal should prefer reversible archive. Permanent delete is separate and irreversible.

Deleted Hero game/history data is removed locally. Only the minimal idempotency receipt required for safe retry remains, containing no Quest/history content.

## 8. Idempotency/security

Never infer a retry from natural-language equality. Caller request IDs plus canonical argument hash prevent a reused token with changed intent from silently mutating the wrong resource.

Request IDs are not authentication credentials and are safe to log only under normal safe diagnostic policy.

## 9. MCP annotations

Tool annotations are UX/model hints, not security controls.

Server-side validation/invariants enforce:

```text
setup gate
safe IDs/text/enums
active/open Quest guards
Hero lifecycle guards
project context
idempotency mismatch
permanent delete confirmation
```

## 10. MCP stdio

stdout is protocol-only. Diagnostics go to stderr and are privacy-scrubbed.

Never log full request bodies by default. In particular do not log `goal`, `summary`, environment variables or config secrets merely because Trace logging is enabled.

## 11. Project privacy

Project persistence stores salted `workspace_fingerprint` and display name, not full path/remote.

Routine MCP outputs omit ProjectId/fingerprint/path.

Git is invoked read-only for identity and with redirection environment variables scrubbed as specified in `PROJECT-IDENTITY.md`. Hero Passport does not weaken Git `safe.directory` protections.

## 12. SQLite storage

No built-in encryption-at-rest claim in 0.1. Hero Passport relies on OS/account/filesystem/device encryption. If application-level DB encryption becomes a product requirement, choose and threat-model it explicitly rather than implying SQLitePCLRaw encrypts data.

App-data file permissions should use normal per-user defaults and avoid deliberately broadening access.

## 13. Network boundary

0.1 stdio/local mode has no Hero Passport cloud endpoint, own OAuth flow or telemetry upload.

Future sync/HTTP requires a separate threat model covering authentication, authorization, encryption, deletion/tombstones, multi-device conflicts and privacy policy.

## 14. Logs/diagnostics

Safe allowlist fields:

```text
error code/category
operation name
tool name
rule/schema versions
SQLite version/pragmas
bounded timing/diagnostic values
UUIDs where useful
```

Default logs exclude:

```text
Quest goal/summary text
Hero confirmation strings
paths/remotes
SQL with bound values
raw exception data if it exposes paths/content
```

## 15. Export

Export is an explicit user action. Export schema must state exactly which Hero/Quest/game fields are included. Source/log/prompt data cannot appear because it is not collected.

## 16. Security tests

Release gates include:

```text
SafeText hostile vectors
closed MCP schemas / unknown fields rejected
UUIDv7 parsing
idempotency token reuse with changed args rejected
open-Quest invariant races
Finish replay cannot double reward
permanent delete guards + late retry
stdout contains only MCP frames
stderr/request logging privacy scans
no forbidden persistence columns/DTO fields
project path/remote absent from MCP/card/export unless explicitly designed
unsupported storage/old SQLite fail closed
```
