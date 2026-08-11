# Hero Passport — Deployment Modes

**Status:** Accepted v3 boundary specification  
**Snapshot:** 2026-08-11

## 1. Why deployment modes are separate architecture

A local stdio process, a loopback HTTP endpoint and a public multi-tenant MCP service have different trust, project-binding and storage requirements. They must not be represented as one `transport` switch over otherwise identical security assumptions.

Hero Passport defines explicit deployment profiles.

---

## 2. Profile A — Local Project-Bound STDIO

**Release:** 0.1.0  
**Status:** primary/required

```text
MCP host
  -> launches hero-passport mcp
  -> stdin/stdout JSON-RPC
  -> local Application
  -> local SQLite
```

### Trust boundary

```text
one local OS user
host application allowed to execute the local command
local filesystem permissions
```

No network listener and no MCP OAuth flow.

### Project binding

```text
host cwd or --project-root
```

One server process is bound to one project identity for its lifetime.

### Hero binding

Default/active local hero or optional `--hero` startup selector.

### Storage

Local SQLite.

### Security

- stdout protocol only;
- stderr/local logs only;
- no source/diff/raw-log fields;
- quest context checked against bound hero/project;
- local process path/config controls are outside model input.

This is the common compatibility profile for Codex, VS Code, JetBrains, Zed, Cursor and Claude Code where their local MCP paths support stdio.

---

## 3. Profile B — Private OpenAI Secure MCP Tunnel

**Release:** optional external integration; does not require Hero Passport HTTP  
**Status:** documented integration path

```text
OpenAI product
   -> OpenAI-hosted tunnel endpoint
   -> outbound tunnel-client
   -> local hero-passport mcp (stdio)
   -> local SQLite
```

Hero Passport remains profile A internally. OpenAI `tunnel-client` is external infrastructure.

### Why this matters

It can make a private developer-machine/on-prem MCP server reachable by supported ChatGPT/Codex/Responses surfaces without opening inbound firewall ports and can forward to local stdio.

### Boundary

Tunnel identity/permissions are OpenAI platform configuration. They are not stored in Hero Passport game config and do not become hero/project identity.

### Public distribution

Secure MCP Tunnel is not the deployment model for a public plugin/server submission. Public distribution requires a stable public HTTPS MCP endpoint according to OpenAI's requirements.

---

## 4. Profile C — Project-Scoped Streamable HTTP

**Release:** future, only on concrete consumer trigger  
**Status:** deferred

```text
MCP client
  -> http(s)://host/mcp
  -> Hero Passport HTTP adapter
  -> one configured project binding
  -> Application
  -> local/private storage
```

This profile is useful only when a target host/environment requires a URL rather than a subprocess or when a private network deployment is operationally preferable.

### Project binding

A HTTP server process `cwd` is **not** caller project identity. Profile C binds the endpoint to one configured project at startup/deployment.

Example conceptual launch:

```text
hero-passport serve --project-root <path> --listen http://127.0.0.1:<port>
```

Exact CLI is future design, not 0.1 commitment.

### C# SDK mode

Use `ModelContextProtocol.AspNetCore` only when implementing this profile. Configure the HTTP MCP transport explicitly as stateless so application correctness remains independent of MCP sessions.

### Local network security

For loopback/local deployment:

```text
bind loopback by default
validate Origin according to MCP transport requirements
restrict accepted Host names to loopback/known hosts
no 0.0.0.0 unauthenticated default
```

### Authentication

A purely same-user loopback endpoint may use local deployment controls, but exposure beyond loopback requires an explicit authentication design. Do not infer safety from “it's only MCP”.

---

## 5. Profile D — Public/Hosted Multi-Tenant Streamable HTTP

**Release:** separate future product phase  
**Status:** not designed for MVP

This is **not** profile C with `--listen 0.0.0.0`.

Required architecture includes:

```text
TLS/public HTTPS
MCP HTTP authorization compliance
OAuth/resource audience validation
authenticated principal
principal -> hero authorization
principal -> project authorization
tenant/workspace isolation
remote durable storage strategy
backup/restore
rate limiting/abuse controls
secret management
audit/security logging
privacy/retention policy
operational monitoring
```

Local `HeroOperationContext` becomes an authenticated/authorized context resolver; clientInfo still is not identity.

Likely storage design must be re-evaluated rather than assuming one SQLite file is a hosted multi-tenant database.

Requires a dedicated architecture review and threat model before implementation.

---

## 6. Legacy SSE

Do not implement a new legacy SSE transport.

Some hosts retain SSE for compatibility with older servers, but MCP `2026-07-28` formally deprecates the legacy HTTP+SSE direction. New Hero Passport URL deployments use Streamable HTTP.

---

## 7. Project binding matrix

| Profile | Project binding source | Model sends path? |
|---|---|---:|
| Local stdio | cwd / `--project-root` | No |
| Secure tunnel to stdio | same local stdio binding | No |
| Project HTTP | server deployment config | No |
| Hosted multi-tenant | authenticated server-side resource binding | No by default |

Remote multi-project APIs may eventually need a project selector, but it must be a server-controlled opaque project identifier authorized against the principal—not a local filesystem path supplied by the model.

---

## 8. Hero binding matrix

| Profile | Hero binding |
|---|---|
| Local stdio | local active/default or `--hero` |
| Tunnel | same local binding |
| Project HTTP | configured local/principal-specific policy |
| Multi-tenant | authenticated principal + explicit authorization |

Never equate MCP client product name with hero identity.

---

## 9. Application invariants across profiles

All deployment profiles must preserve:

```text
same StartQuest semantics
same logical-key convergence
same quest-context validation
same FinishQuest idempotency
same deterministic RPG rules
same privacy deny-list
same stable HP error meanings
```

Transport adapters may differ in auth/binding/protocol framing only.

---

## 10. Trigger criteria for adding HTTP

Do not implement own Streamable HTTP merely because the SDK supports it.

A new HTTP adapter is justified when at least one is true:

1. a high-priority host cannot launch local stdio but supports Streamable HTTP;
2. a required private network deployment needs a stable URL;
3. a public/plugin distribution requires an HTTPS endpoint;
4. operational evidence shows a shared URL deployment materially improves the product.

Before implementation update:

```text
ARCHITECTURE.md
SECURITY-PRIVACY.md
DEPENDENCIES.md
TESTING-QUALITY.md
API-CONTRACTS.md if binding/auth changes
ROADMAP.md
DECISION-LOG.md
```
