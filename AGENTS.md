# AGENTS.md — Hero Passport

## Mission

Build Hero Passport as a portable local-first RPG state layer for AI coding agents. The reference qualification client is Codex, but product semantics and MCP contracts are host-neutral. Follow canonical documents under `docs/`; do not invent an alternative architecture inside implementation PRs.

## Read before coding

Read the smallest relevant set:

```text
docs/PRODUCT-SPEC.md
docs/ARCHITECTURE.md
+ feature-specific contract/spec
docs/DECISION-LOG.md relevant decisions
```

MCP/API work:

```text
docs/API-CONTRACTS.md
docs/MCP-CONTRACT.md
docs/INTEROPERABILITY.md
docs/integrations/README.md
```

Storage/config work:

```text
docs/DATA-MODEL.md
docs/CONFIGURATION.md
docs/SECURITY-PRIVACY.md
```

Rules: `docs/ENGINE-SPEC.md`. Dependencies: `docs/DEPENDENCIES.md`. Tests/release: `docs/TESTING-QUALITY.md`.

## Hard project boundaries

```text
Domain
  - deterministic game policy only
  - no EF/MCP/CLI/HTTP/localization/filesystem/config

Application
  - typed semantic use cases + ports
  - Domain dependency only
  - no MCP SDK, host config or localized output

Infrastructure
  - EF/SQLite/filesystem/config/project-binding adapters

App
  - composition root
  - MCP stdio adapter
  - CLI adapter
  - presentation/localization

Web (0.2+)
  - Application/read models
  - no DbContext in Razor components
```

Do not add generic repositories, mediator/event-bus frameworks, runtime plugins, REST/GraphQL/gRPC APIs or a generic HTTP abstraction without a superseding ADR and an actual consumer requirement.

## HP-MCP/2 invariants

Exactly four 0.1.0 tools, explicitly registered in this stable order:

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

Never use assembly-wide tool scanning in MVP.

Tool inventory is static. If it changes, review prompt/tool-selection impact, contract snapshots and agent evals.

Tool inputs must never add fields for:

```text
source code
file contents
diffs/patches
changed-file bodies
raw logs
full prompts/chat history
secrets/API keys
environment bags
workspace paths
arbitrary metadata/context/payload bags
clientName/clientVersion solely for identification
```

All input objects are shallow, bounded and reject unknown properties.

### Protocol compatibility

- Design semantics for MCP `2026-07-28`.
- Leave `McpServerOptions.ProtocolVersion` unset/null in the ordinary server so the official SDK can negotiate supported older revisions.
- Do not use MCP session/connection identity as application state.
- `questId` is the explicit state handle across calls.
- For future Streamable HTTP, set the C# SDK transport stateless mode explicitly; do not pretend that an HTTP session is hero/project identity.
- Do not depend on deprecated Roots, Sampling or MCP Logging for core behavior.
- Tasks, Apps and MRTR are outside 0.1.0.

### Tool list caching

`tools/list` is deterministic and identical for every local user. Advertise explicit public cache scope. The exact TTL is implementation policy, verified in interop tests, not a public HP-MCP semantic guarantee. Do not advertise `listChanged`/dynamic inventory while the tool list is static.

## Multi-agent quest semantics

Multiple distinct quests may be open for the same hero/project.

A same logical work item intentionally converges across clients:

```text
logical key v1 = SHA-256(questType + canonicalized goal)
```

Canonicalization is versioned and deterministic. It performs Unicode NFC, trim, whitespace collapse and invariant case normalization for key calculation only; original bounded goal text remains stored for display/history.

Repeated matching `start_quest` returns the existing open quest. A different logical work item creates another quest.

Safety policy:

```text
max open quests per hero/project = 16
```

The cap is application policy, not a SQLite architectural limitation.

A finished quest rewards once. Repeated finish returns the original persisted outcome.

`questId` is an identifier, not a credential. `finish_quest` must verify that the quest belongs to the locally resolved hero/project operation context.

## Operation-context invariants

Application handlers receive a transport-neutral operation context resolved by adapters.

Conceptually:

```text
HeroOperationContext
  HeroId
  ProjectId
  InvocationOrigin
```

`InvocationOrigin` can identify surface (`mcp_stdio`, future `mcp_http`, `cli`, `web`) and optional normalized diagnostic client metadata, but:

```text
client identity != hero identity
client identity != authentication identity
client metadata != reward input
client metadata != authorization
```

Do not persist raw MCP client metadata by default.

## Project binding

For local stdio:

```text
explicit --project-root
> process working directory / Git root resolution
> working-directory fallback
```

Hosts may provide `cwd`; `--project-root` is the portable fallback.

Never put workspace path in MCP DTOs. Do not rely on MCP Roots for project identity; Roots are deprecated in the 2026 protocol line.

A global long-lived MCP process cannot magically infer a different project per stateless request. If a host cannot provide project-scoped process launch/binding, document that limitation instead of guessing.

## Persistence invariants

```text
EF Core migrations from day one
IDbContextFactory + short-lived contexts
short synchronous SQLite operations
no Task.Run around database work
WAL + synchronous=FULL + foreign_keys=ON
no Cache=Shared with WAL
UNIQUE xp_events.quest_id
FinishQuest = one atomic transaction
logical open quest partial uniqueness
no custom migration mutex; EF owns migration lock
```

Persistence tests use real temporary file-backed SQLite.

## RPG invariants

Canonical clean-coding golden:

```text
60 base
+10 tests
+10 clean scope
+10 clear summary
+5 no corrections
=95 XP
```

Use deterministic integer rules and persist rule versions. Skill XP allocation must conserve reward XP exactly.

Russian terminology:

```text
scope_control -> Контроль
clean scope bonus -> Бонус за контроль
scope violation -> Выход за задачу
```

Localized text belongs to App presentation, not Domain/Application state.

## Error/API invariants

Application errors have stable `HPxxx` codes. Adapters translate one semantic error into their own surface representation; they do not invent different business meanings.

New v3 codes include:

```text
HP133 active_quest_limit
HP134 quest_context_mismatch
```

`hero.list_active_quests` returns an empty list when there are none; absence is not an exceptional state.

## Dependency policy

Use Central Package Management and pinned stable versions from `docs/DEPENDENCIES.md`. Prefer BCL/framework/direct code over new abstractions until a measured requirement exists. No preview dependency without ADR.

`ModelContextProtocol.AspNetCore` is not a 0.1.0 dependency; add it only when Streamable HTTP is actually implemented.

## Testing workflow

For behavior/contract changes:

```text
write focused failing test
confirm failure
implement minimum coherent change
run focused test
run impacted suite
run architecture/privacy/contract snapshot tests
run affected agent-eval scenarios for MCP metadata/schema changes
update docs and goldens in the same PR
```

Contract tests must cover at least:

```text
2026-07-28 path
initialize-era compatibility path (2025-11-25)
exact tool inventory/order
input/output schemas
annotations/cache hints
structured + text representation
multi-quest/idempotency races
project binding/context mismatch
forbidden-field scan
```

## Support claims

Use support tiers from `docs/integrations/README.md`.

Do not write “supported everywhere” because a config example exists. A host is Qualified only after its release smoke/e2e gate passes. Otherwise call it Documented/Protocol-compatible.

## Scope guard

0.1.0 excludes:

```text
dashboard
achievements/items
runtime plugins
our own Streamable HTTP listener
remote OAuth/tenancy
dedicated REST/GraphQL/gRPC API
MCP Apps/Tasks
cloud/team mode
continuous telemetry
LLM judge
source/diff ingestion
```

0.2.0 adds the local Blazor dashboard. HTTP/remote work is trigger-based and must follow `DEPLOYMENT-MODES.md`, not be opportunistically prebuilt.
