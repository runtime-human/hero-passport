# Hero Passport — Security and Privacy Specification

**Status:** Accepted for local MVP  
**Baseline:** 2026-08-10  
**Security model:** single-user local application, untrusted agent-supplied metadata, no network listener

## 1. Security objective

Hero Passport should be safe to leave enabled for routine agent work without turning an MCP integration into a hidden source-code or secret collection path.

The strongest MVP control is **data minimization by contract**: fields for raw code, diffs, logs, prompts and secrets do not exist.

## 2. Trust boundaries

### Trusted enough to execute locally

- the installed Hero Passport binary/source selected by the user;
- the user's OS account and local filesystem permissions;
- the local SQLite library/package versions pinned by the repository.

### Untrusted input

- all MCP arguments supplied by an AI agent/client;
- quest goal and summary strings;
- host metadata;
- skill aliases;
- imported JSON (when import is later designed);
- environment/current working directory data until normalized by the local resolver.

### Out of MVP trust boundary

There is no remote HTTP MCP server, cloud backend, team account, OAuth flow, external plugin loader or multi-user authorization model in MVP.

## 3. Data classification

### Allowed persisted data

```text
hero identity/game state
project display name + privacy-preserving fingerprint
quest goal
short quest summary
quest/result/quality counters
canonical skill keys and XP
trait progress
trust/risk
reward breakdown
rule versions
timestamps
bounded host name/type
```

### Prohibited by default

```text
source code
file contents
diffs/patches
raw terminal logs
raw build/test logs
full prompts
full chat history
API keys/tokens/passwords
complete environment variables
credentials
browser/session cookies
full workspace path
arbitrary attachments/binary blobs
```

A feature that needs one of the prohibited categories requires a new threat model and explicit product approval; it cannot be slipped into an existing metadata field.

## 4. Request minimization

MCP schemas use allowlisted properties, bounded strings/arrays/counters and `additionalProperties: false` where practical.

The server must never accept a generic property like:

```text
context
metadata: object
payload
rawData
extra: dictionary<string, object>
```

in the MVP tool contracts. Generic bags become accidental exfiltration channels and destroy schema/token discipline.

## 5. Workspace identity privacy

The server resolves the current project locally. The MCP request has no `workspacePath` field in schema `1.0`.

Persist only:

- display name suitable for UI;
- versioned SHA-256 fingerprint derived locally from normalized root identity;
- no cleartext root path by default.

The fingerprint reduces accidental disclosure but is not encryption and must not be marketed as anonymous against a local attacker.

## 6. Summary/goal handling

Quest goal/summary are still user/agent-controlled text and can accidentally contain sensitive material.

Controls:

- hard length bounds;
- documentation tells the agent to provide semantic summaries, not copied logs/code;
- normal logs never echo request bodies;
- exports include goal/summary because they are product data, so users should treat exports accordingly;
- future secret-pattern scanning may be added as defense-in-depth, but regex redaction is not considered a complete security boundary.

Do not attempt to parse/execute Markdown, shell commands or code contained in these strings. They are inert text.

## 7. MCP transport safety

MVP uses stdio only. This removes network exposure but creates a strict framing requirement:

- stdout is protocol only;
- diagnostics use stderr/local log;
- no command shell interpolation of MCP arguments;
- process exits on unrecoverable protocol-host startup failure;
- the server should inherit only the environment needed to run; it must not serialize the environment into state or results.

Do not add Streamable HTTP by merely exposing an ASP.NET endpoint. Remote MCP requires authentication/authorization, origin/security review, deployment configuration, rate limits and MCP-specific security best-practice review.

## 8. MCP tool-risk semantics

`start_quest` and `finish_quest` mutate only local Hero Passport state. They do not change source files, execute shell commands, call networks or control external services.

Read tools are `current_quest` and `get_card`.

Tool annotations are UX hints only. Security must not rely on a client honoring annotations.

## 9. Database security

- data directory is per-user;
- never place the database in a repository by default;
- add DB/config/log/export locations to product-generated `.gitignore` snippets only when the path can occur inside a workspace;
- no secrets in `app_settings`;
- no SQL built from raw model input outside parameterized EF/SQLite APIs;
- foreign keys enabled;
- WAL/native SQLite safe baseline pinned;
- local file permissions checked best-effort.

At-rest DB encryption is **not** an MVP claim. If required later, choose an explicitly supported encryption approach and document licensing/platform/native packaging implications.

## 10. Logging policy

Default production behavior:

```text
minimum useful lifecycle/diagnostic events
no request bodies
no response bodies
no goals/summaries at Information level
no local path values at Information level
quest/request IDs allowed
exception type + safe message allowed
stack traces only diagnostic/debug sink
```

MCP logging facilities deprecated by the 2026-07-28 protocol are not a product dependency. Use normal .NET logging to stderr or opt-in local sink.

## 11. Export security

JSON export is user-invoked and local.

Export includes only allowed persisted product data and a manifest:

```text
exportFormatVersion
createdAtUtc
appVersion
schemaVersion
ruleVersions
```

It does not include:

- database connection strings;
- filesystem paths;
- log files;
- environment variables;
- secret config;
- temporary/cache files.

Write exports atomically via temp file + rename where supported to avoid a half-written portable backup.

## 12. Dependency/supply-chain controls

Repository controls:

- exact .NET SDK pin;
- Central Package Management;
- committed `packages.lock.json`;
- CI `dotnet restore --locked-mode`;
- stable package versions by default;
- dependency vulnerability audit gate at an agreed severity level;
- explicit SQLite native package pin because transitive minimum `2.1.11` is not an acceptable August 2026 baseline;
- GitHub Actions pinned to trusted major/commit policy when workflows are added;
- Dependabot/Renovate-style updates may propose upgrades but never auto-change scoring/protocol rules.

## 13. Threat scenarios

### T1 — agent sends source code in summary

Mitigation: contract/docs say semantic summary only; length bound limits blast radius; no generic raw fields; no body logging. Optional future classifier/redactor is defense-in-depth.

### T2 — malicious text attempts SQL injection

Mitigation: parameterized EF/SQLite usage; no dynamic SQL from model text except carefully fixed migration SQL unrelated to requests.

### T3 — duplicate/replayed finish grants XP repeatedly

Mitigation: immutable quest state + unique XP event by quest + atomic transaction + stored-outcome idempotent return.

### T4 — two local processes finish simultaneously

Mitigation: SQLite transaction/unique constraints; loser reloads committed outcome; no second ledger event.

### T5 — MCP stdout corrupted by a banner/log

Mitigation: dedicated MCP output path; process integration test; logs on stderr.

### T6 — local project path leaks to model/export

Mitigation: resolve locally; no path in schema; persist fingerprint/display name only.

### T7 — vulnerable native SQLite under WAL concurrency

Mitigation: explicit safe native bundle pin and runtime version test; dependency audit.

### T8 — malicious local user reads DB

Out of scope for application-level isolation under the same OS user. Rely on account/filesystem/disk security; do not claim encryption.

### T9 — future dashboard exposes network endpoint

Dashboard defaults to loopback only. Binding beyond loopback is not allowed without a separate remote-access/auth design.

## 14. Security acceptance tests

- MCP DTO schema has no prohibited raw-data fields.
- Oversized goal/summary rejected.
- Negative/counter overflow inputs rejected.
- Unknown object fields rejected where schema behavior supports it.
- Logs do not contain a sentinel secret placed in a test request.
- Export does not contain local path/environment sentinel values.
- project DB row contains no full path.
- duplicate finish creates one XP event.
- MCP stdout test detects injected banner/log regression.
- database foreign keys are on.
- runtime native SQLite version is at/above approved safe floor.

## 15. Security review trigger list

A new security review is mandatory before any of:

```text
HTTP/network MCP
remote dashboard access
cloud sync
accounts/auth/team mode
external plugins/DLL loading
arbitrary file reading
source/diff ingestion
shell/process execution
LLM judging with external API
secret storage
third-party telemetry/export upload
```

Until then the security architecture should stay intentionally boring and local.
