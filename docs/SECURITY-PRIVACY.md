# Hero Passport — Security and Privacy

**Status:** Accepted v3  
**Snapshot:** 2026-08-11

## 1. Security objective

Hero Passport is local-first and intentionally low-privilege. Its primary security strategy is **data minimization + narrow contracts**, not a complex authorization framework in the MVP.

The local product should remain useful without reading source code, diffs, raw logs, prompts or secrets.

---

## 2. Protected assets

```text
hero progression
database integrity
quest/reward history
local project identity mapping
configuration
diagnostic logs
local filesystem path privacy
future authentication material (only if HTTP exists later)
```

Source code is not a Hero Passport asset because it is deliberately not ingested.

---

## 3. Trust boundaries

### Local stdio 0.1

Trust boundary is one OS user plus the MCP host that can execute Hero Passport.

Risks:

```text
malicious/untrusted model arguments
another local client using wrong questId
concurrent processes/races
stdout corruption
path leakage
untrusted goal/summary rendered as instructions
local DB tampering/corruption
```

### Private tunnel

Adds OpenAI tunnel identity/organization/workspace controls outside Hero Passport. Local Hero Passport still runs under the local stdio trust model.

### Streamable HTTP future

Adds network-origin/authentication/authorization risks and requires the separate deployment threat model in `DEPLOYMENT-MODES.md`.

---

## 4. Privacy deny-list

MCP schemas, storage models and ordinary logs must not contain fields for:

```text
source code
file contents
diffs/patches
changed-file contents
raw terminal logs
raw build/test logs
full prompt/chat transcript
API keys/tokens/secrets
environment dump
full workspace path
generic metadata/context/payload bag
```

This is enforced through schema/architecture tests, not only documentation.

---

## 5. Bounded untrusted text

Hero Passport does store two model-provided text values:

```text
goal <= 500 chars
summary <= 2000 chars
```

Treat them as untrusted data:

- never execute them;
- never interpret as config/path/SQL;
- parameterize DB writes;
- do not render raw goal into list-active human text by default;
- when Web later renders them, encode as plain text unless an explicitly safe markdown policy exists;
- never concatenate them into agent/system instructions.

---

## 6. Project-path privacy

Project path is transient launch context.

Allowed local uses:

```text
resolve Git root
choose display name
calculate versioned workspace fingerprint
```

Not allowed by default:

```text
MCP input/output
SQLite project identity path column
export
ordinary diagnostic log
```

Verbose terminal doctor may display a local path when the user explicitly requests local diagnostics; this does not authorize MCP exposure.

---

## 7. Project fingerprint

Fingerprint prevents casual path disclosure and establishes stable local identity. It is not a cryptographic authorization token.

Do not use fingerprint as proof that a remote caller is allowed to access a project.

Project identity algorithm is versioned so normalization changes can be migrated explicitly.

---

## 8. Quest ID and context safety

`questId` is a UUID identifier, not a secret/capability token.

Every context-scoped quest operation verifies:

```text
quest.hero_id == HeroOperationContext.HeroId
quest.project_id == HeroOperationContext.ProjectId
```

Mismatch returns:

```text
HP134 quest_context_mismatch
```

Do not reveal which alternate hero/project owns the quest.

This prevents accidental cross-project/client operations locally and provides the semantic prerequisite for future remote authorization.

---

## 9. Client metadata

MCP client name/version/capabilities are untrusted protocol metadata.

Allowed:

```text
bounded local diagnostic entry
interop statistics during explicit test/eval
compatibility fallback if protocol capability genuinely requires it
```

Forbidden:

```text
authentication
authorization
hero selection
project selection
XP/Trust/Risk changes
feature entitlement
persistent identity by default
```

A caller can spoof a client name; architecture must behave safely anyway.

---

## 10. Multi-agent race/security model

Parallel clients are normal, not suspicious.

Correctness barriers:

```text
logical-key partial unique index
max-active application policy + writer-serialization tests
UNIQUE xp_events.quest_id
atomic FinishQuest transaction
context match
```

Do not solve local races with a global process mutex that fails when several processes are legitimately used.

---

## 11. MCP schema security

All inputs:

```text
closed object
bounded text
bounded arrays/counters
closed enums where applicable
no arbitrary dictionary
```

Unknown fields are rejected rather than silently ignored so a model cannot assume unsupported/private data has been processed.

Advanced JSON Schema features are avoided to keep validation behavior consistent across hosts.

---

## 12. MCP stdout

In stdio mode stdout is protocol-only.

Never write:

```text
banner
progress spinner
console table
debug trace
EF logging
migration output
exception stack
```

there.

Use stderr/local logging, configured so no request bodies are captured by default.

Process-level stdout tests are mandatory.

---

## 13. Logging policy

Default:

```text
Microsoft.Extensions.Logging
stderr where appropriate
file logging off
no request/response bodies
no goal/summary content by default
no paths/secrets
```

Useful safe fields:

```text
operation name
quest ID
HP error code
duration bucket
DB migration version
normalized invocation surface
```

A verbose local troubleshooting mode can increase local detail, but privacy-sensitive values remain explicit and redacted where possible.

---

## 14. SQLite integrity

Security/reliability settings:

```text
foreign_keys=ON
WAL
synchronous=FULL
bounded busy timeout
parameterized queries/EF
migrations only; no EnsureCreated product schema
verified native SQLite version
```

Do not allow arbitrary SQL through any Hero Passport interface.

Database file follows OS-local app-data permissions. Hero Passport is not intended to protect game data from the same OS account with direct filesystem access.

---

## 15. Export policy

Default export is a safe logical projection, not an indiscriminate DB/config directory zip.

Exclude:

```text
absolute paths
logs
host configs
credentials
environment
SQLite temp/WAL internals unless explicit backup operation
```

If user requests a full diagnostic bundle in the future, its contents/redaction must have a separate documented contract and preview before sharing.

---

## 16. Local HTTP future requirements

If Streamable HTTP is added:

```text
use official Streamable HTTP, not legacy SSE
explicit stateless transport mode
validate Origin
bind loopback by default for local profile
restrict Host names to expected loopback/known names
no unauthenticated 0.0.0.0 default
```

Any non-loopback exposure must explicitly define authentication.

---

## 17. Remote HTTP future requirements

Public/hosted deployment requires:

```text
TLS
MCP authorization compliance
issuer/resource validation
authenticated principal
hero/project authorization
tenant isolation
rate/abuse controls
secure secret storage
remote persistence/backup design
privacy retention policy
```

Do not use `clientInfo`, `questId`, project fingerprint or a custom header as a shortcut for authentication.

---

## 18. Secure MCP Tunnel boundary

OpenAI Secure MCP Tunnel is an external private-connectivity mechanism. Hero Passport does not receive/control the OpenAI platform tunnel credential as game state.

If documented for users:

- keep tunnel runtime API keys out of repo/config examples;
- use environment/official tunnel configuration;
- clarify that private tunnel access and public plugin distribution are different models.

---

## 19. Supply-chain policy

- stable package versions only unless ADR;
- Central Package Management;
- lock files;
- NuGet vulnerability audit gate;
- direct SQLite native bundle pin;
- verify actual loaded SQLite version;
- dependencies reviewed in `DEPENDENCIES.md`.

Do not add host integration SDKs when standard MCP configuration is sufficient.

---

## 20. Threat cases and expected controls

| Threat | Control |
|---|---|
| model tries to send code | schema has no field; deny-list tests |
| prompt injection in goal | goal treated as plain untrusted data |
| wrong quest ID | hero/project context check |
| duplicate finish | unique XP event + transaction |
| duplicate same-task start | logical-key unique constraint |
| parallel distinct agents | multiple active quests supported |
| MCP host lies about name | clientInfo never trusted |
| cwd points elsewhere | explicit project binding/doctor/context |
| logs leak payload | body logging disabled + tests |
| stdout corruption | child-process protocol purity test |
| old protocol client | SDK version negotiation + compatibility tests |
| public HTTP accidentally exposed | not implemented in 0.1; future loopback/Origin/auth rules |

---

## 21. Security review triggers

Mandatory focused review before:

```text
adding any code/diff/file field
adding generic metadata bags
adding HTTP listener
adding OAuth/remote users
adding MCP Apps/resources containing user data
adding cloud/team mode
adding diagnostic bundle upload
adding source-code telemetry
persisting client identity
```
