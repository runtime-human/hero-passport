# Hero Passport — Deployment Modes

**Status:** Accepted v3.1 boundary specification  
**Snapshot:** 2026-08-11

A local stdio process, loopback HTTP endpoint and public multi-tenant service have different trust/project/storage boundaries; never model them as one `transport` flag over identical assumptions.

---

## 1. Profile A — Local Project-Bound STDIO

**Release:** 0.1.0  
**Status:** primary/required

```text
MCP host
 -> hero-passport mcp
 -> stdin/stdout
 -> local Application
 -> same-host local SQLite
```

Trust:

```text
one local OS user
host allowed to execute local command
local filesystem permissions
```

No network listener/OAuth.

Project binding:

```text
host cwd or --project-root
-> project-identity/1
```

The process binds one project identity for its lifetime. Git identity uses `git-common-dir`/explicit scope rules from `PROJECT-IDENTITY.md`.

Hero binding: active/default or explicit local `--hero`.

Security:

```text
stdout protocol only
safe stderr/local logs
no forbidden source/path data
quest HeroId+ProjectId context check
```

---

## 2. Profile B — Private OpenAI Secure MCP Tunnel

**Release:** optional external integration  
**Status:** documented path

```text
OpenAI product
 -> OpenAI tunnel endpoint
 -> outbound tunnel-client
 -> local hero-passport stdio
 -> local SQLite
```

Hero Passport remains Profile A internally. Tunnel credentials/identity belong to OpenAI platform configuration, not game/project identity.

This is not the public plugin/server distribution model; public distribution needs a public HTTPS MCP endpoint under OpenAI requirements.

---

## 3. Profile C — Project-Scoped Streamable HTTP

**Release:** future, concrete trigger only  
**Status:** deferred

```text
MCP client
 -> http(s)://host/mcp
 -> HTTP adapter
 -> one configured project binding
 -> Application
 -> local/private storage
```

HTTP process cwd is not caller project identity. Endpoint binds one configured project explicitly.

Conceptual future launch only:

```text
hero-passport serve --project-root <path> --listen http://127.0.0.1:<port>
```

When implemented:

```text
ModelContextProtocol.AspNetCore
explicit stateless HTTP mode
Origin validation
loopback bind default
restricted expected Host names
no unauthenticated 0.0.0.0 default
```

Beyond-loopback exposure requires explicit authentication design.

---

## 4. Profile D — Public/Hosted Multi-Tenant Streamable HTTP

**Release:** separate future product phase  
**Status:** not MVP architecture

Not Profile C with public bind.

Requires:

```text
TLS/public HTTPS
MCP HTTP authorization
OAuth/resource audience validation
authenticated principal
principal -> hero/project authorization
tenant isolation
remote durable storage
backup/restore
rate/abuse controls
secret management
security/operational logging
retention/privacy policy
```

`HeroOperationContext` must be resolved from authenticated/authorized server state; `clientInfo`, questId and project fingerprint never become authentication shortcuts.

Storage is re-evaluated rather than assuming one local SQLite file is a hosted multi-tenant DB.

---

## 5. Legacy SSE

Do not implement a new legacy SSE server. Future URL deployments use Streamable HTTP.

---

## 6. Project binding matrix

| Profile | Project binding | Model sends local path? |
|---|---|---:|
| Local stdio | cwd / `--project-root` -> project-identity/1 | No |
| Secure tunnel to stdio | same local binding | No |
| Project HTTP | deployment-configured project | No |
| Hosted multi-tenant | authorized server-side resource | No by default |

A future remote multi-project selector, if needed, is an authorized opaque server identifier—not a local filesystem path.

---

## 7. Hero binding matrix

| Profile | Hero binding |
|---|---|
| Local stdio | active/default or `--hero` |
| Tunnel | same local binding |
| Project HTTP | configured local/principal policy |
| Multi-tenant | authenticated principal + authorization |

Never equate host/client product name with hero identity.

---

## 8. Application invariants across profiles

Every future adapter preserves:

```text
same StartQuest lifecycle semantics
same QuestDedupKeyV1 open-declaration retry behavior
same 16-active cap semantics
same quest-context validation
same FinishQuest idempotency
same deterministic RPG rules
same SafeText/wire meanings where MCP is used
same privacy deny-list
same stable HP error meanings
```

Transport/auth/binding may differ only through an explicitly designed adapter/context resolver.

---

## 9. Trigger criteria for own HTTP

Do not implement HTTP because the SDK supports it.

At least one concrete need must exist:

1. priority host cannot launch stdio but needs URL MCP;
2. private network deployment needs stable URL;
3. public/plugin distribution requires HTTPS endpoint;
4. measured operations show shared URL deployment materially improves product.

Before implementation update Architecture, Security, Dependencies, Testing, API contracts if auth/binding changes, Roadmap and Decision Log.
