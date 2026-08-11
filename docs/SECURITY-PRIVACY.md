# Hero Passport — Security and Privacy

**Status:** Accepted v3.1  
**Snapshot:** 2026-08-11

Detailed local project/Git controls: `PROJECT-IDENTITY.md`.  
Detailed DB crash/backup controls: `PERSISTENCE-RELIABILITY.md`.  
Detailed MCP text/schema/result controls: `WIRE-CONTRACT.md`.

---

## 1. Security objective

Hero Passport remains local-first and low privilege. Primary strategy:

```text
data minimization
+ narrow closed contracts
+ explicit local binding
+ DB atomicity
+ safe output/logging
```

0.1 does not need a remote authorization framework.

---

## 2. Protected assets

```text
hero progression
quest/reward history
database integrity
project identity mapping
configuration
local path privacy
diagnostic logs
future remote credentials only if HTTP later exists
```

Source code is deliberately not ingested.

---

## 3. Trust boundaries

### Local stdio 0.1

Trust boundary:

```text
one OS user
+ MCP host allowed to execute Hero Passport
```

Threats still include:

```text
malicious/untrusted model arguments
wrong known questId
parallel local processes
stdout corruption
path leakage
prompt-like stored text
Git binding redirection/unsafe repo
local DB corruption/tampering
crash between commit and response
```

### Private tunnel

Adds OpenAI tunnel/workspace controls outside Hero Passport; local process still follows stdio model.

### Future HTTP

Adds network auth/origin/principal/tenant boundaries and requires a separate review before implementation.

---

## 4. Data deny-list

MCP schemas, DB and ordinary logs must not intentionally capture:

```text
source/file contents
diffs/patches
changed-file bodies
raw terminal/build/test logs
full prompts/chat history
API keys/tokens/secrets
environment dump
full workspace path
Git remote URL
generic metadata/context/payload bags
```

Architecture/contract/privacy tests enforce the deny-list.

---

## 5. SafeTextV1 for stored model text

The only routine model text persisted is bounded quest metadata such as goal/summary.

It passes explicit `SafeTextV1` before storage:

```text
valid Unicode scalars
reject prohibited controls/bidi formatting controls
NFC
trim
collapse whitespace to single-line ASCII spaces
scalar-aware length bounds
```

```text
goal <=500 scalars
summary <=2000 scalars
```

The normalized value is data, never instruction/config/path/SQL.

Web later renders it as encoded plain text unless a separate safe-markdown design exists.

MCP `displayText` does not echo arbitrary goal/summary by default.

---

## 6. Project binding security

Paths are local adapter input only.

Git probe:

```text
ProcessStartInfo.ArgumentList
no shell
read-only rev-parse queries
sanitized inherited repository-location Git env vars
no hooks/remotes/network
no Git config mutation
no safe.directory mutation
```

A repository marker plus Git trust/read failure returns a binding error rather than silently falling back to a standalone path identity.

Bare repositories are unsupported for normal coding-agent project binding.

---

## 7. Project fingerprint privacy

`project-identity/1` persists a salted SHA-256 fingerprint, display name and version, not a full path or remote URL.

The salt reduces casual/precomputed correlation but does not make paths cryptographically secret to an attacker who can already read all local product state.

Fingerprint is not authentication material.

Do not use project fingerprint to authorize future remote callers.

---

## 8. Quest ID/context safety

`questId` is an identifier, not a credential.

Every context-scoped finish verifies:

```text
quest.hero_id == context.HeroId
quest.project_id == context.ProjectId
```

Mismatch:

```text
HP134 quest_context_mismatch
```

Do not reveal the alternate owner/project.

---

## 9. Client metadata

Client name/version/capabilities are untrusted metadata.

Allowed:

```text
bounded local diagnostics
interop qualification
true capability fallback where required
```

Forbidden:

```text
authentication
authorization
hero/project selection
XP/Trust/Risk
feature entitlement
persistent identity by default
```

---

## 10. Quest dedup is not authorization or semantic truth

`QuestDedupKeyV1` is only a retry/declaration equality key for an open quest.

Case is preserved and no fuzzy semantic matching is performed.

It is not:

```text
a capability token
a model identity
a permanent idempotency key
a proof two natural-language tasks are semantically identical
```

Recovery/handoff uses explicit active listing and `questId`.

---

## 11. Multi-process correctness controls

Parallel clients are normal.

Controls:

```text
immediate writer transaction before mutation invariant reads
open dedup partial unique index
max 16 active policy under same writer transaction
UNIQUE quest_reports.quest_id
UNIQUE xp_events.quest_id
atomic FinishQuest
context match
```

No global process mutex for ordinary writers.

---

## 12. MCP schema/runtime validation

Inputs are closed/bounded.

The official C# SDK schema/DataAnnotations do not enforce runtime validation, so tool adapters explicitly validate:

```text
SafeText
UUIDv7 canonical form
enums
integer bounds
metrics consistency
canonical-only ordered skills
```

Unknown fields are rejected by contract/schema and runtime mapping path; no arbitrary dictionaries.

---

## 13. MCP success/error sanitation

Success:

```text
structuredContent typed object
one equivalent minified JSON TextContent
bounded displayText inside object
```

Business/validation error:

```text
isError=true
one concise safe TextContent
no structuredContent
```

No result/error contains stack trace, SQL, connection string, local path, request dump, secrets or raw env.

---

## 14. stdio isolation

In MCP mode stdout is protocol-only.

Never write banner, spinner, EF logs, migration messages, stack traces or normal CLI output to stdout.

Diagnostics use stderr/local logs with payload/path redaction defaults.

Child-process stdout tests are mandatory.

---

## 15. Logging

Default:

```text
Microsoft.Extensions.Logging
no request/response bodies
no goal/summary text by default
no paths/remotes/secrets
file logging off
```

Safe diagnostic fields may include:

```text
operation name
questId
HP code
duration bucket
migration version
invocation surface
qualified SQLite version
```

---

## 16. SQLite integrity/security

```text
WAL
synchronous=FULL
foreign_keys=ON
bounded provider busy timeout
parameterized EF/SQLite
migrations, no product EnsureCreated
actual sqlite_version qualification >=3.51.3
```

Writable network filesystem DB is outside supported 0.1 profile.

No arbitrary SQL Hero Passport interface.

Same OS account with direct filesystem access is outside the product's confidentiality boundary; Hero Passport does not claim to encrypt/protect game state from that account.

---

## 17. Crash/WAL recovery safety

Never automatically delete/rename:

```text
.db-wal
.db-shm
rollback journal
```

SQLite performs journal recovery on normal reopen.

Crash-before-commit must yield no partial progression. Crash-after-commit-before-response is recovered by retrying explicit questId and returning the committed outcome.

---

## 18. Backup safety

Logical export is not a physical backup.

Never raw-`File.Copy` a live SQLite DB. A live physical backup uses SQLite/Microsoft.Data.Sqlite BackupDatabase and verifies destination quick/FK/schema state before publication.

Restore/replacement is a separate future workflow; do not overwrite an open DB.

---

## 19. Export policy

Default export is a safe logical projection.

Exclude:

```text
absolute paths
Git remote URLs
logs
host configs
credentials
environment
SQLite WAL/SHM internals
project identity salt unless an explicit full-state backup workflow requires it
```

A future diagnostic bundle/upload has a separate contract and preview/redaction policy.

---

## 20. Supply chain

```text
stable exact package versions
Central Package Management
package locks
NuGet vulnerability audit
direct SQLite native bundle baseline
actual loaded sqlite_version proof per published artifact
```

Do not add host SDKs when standard MCP suffices.

---

## 21. Future local HTTP

Before implementation:

```text
Streamable HTTP only
explicit stateless HTTP mode
Origin validation
loopback bind default
restricted Host names
no unauthenticated 0.0.0.0 default
```

Non-loopback exposure requires explicit authentication design.

---

## 22. Future public/remote HTTP

Requires:

```text
TLS
MCP authorization compliance
issuer/resource validation
authenticated principal
hero/project authorization
tenant isolation
rate/abuse controls
secure secrets
remote persistence/backup
retention policy
```

Never use clientInfo, questId or fingerprint as shortcut authentication.

---

## 23. Threat/control matrix

| Threat | Control |
|---|---|
| model sends code | no schema field + deny-list tests |
| control/bidi text spoofing | SafeTextV1 |
| wrong questId | HeroId+ProjectId context check |
| duplicate finish | immediate writer + unique report/XP + persisted retry |
| duplicate open start | case-preserved QuestDedupKey + unique open index |
| cap race | immediate writer before count |
| malicious Git env/path | sanitized env + ArgumentList + no shell |
| Git unsafe repo | fail, never auto safe.directory |
| live DB copied incorrectly | BackupDatabase only |
| manual WAL loss | recovery policy forbids deleting WAL/SHM |
| old affected SQLite WAL build | runtime >=3.51.3 qualification |
| stdout corruption | process purity test |
| spoofed MCP clientInfo | never trusted |

---

## 24. Mandatory review triggers

Focused security review before:

```text
any source/diff/file-content field
new generic metadata bag
HTTP listener/OAuth/remote users
cloud/team mode
MCP App/resource exposing user data
diagnostic bundle upload
cross-machine/shared DB
persistent client identity
project relink/import that consumes external identity data
```
