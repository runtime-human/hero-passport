# Hero Passport

> Portable local-first RPG passport for AI coding agents.

Hero Passport turns meaningful agent work into persistent RPG progression without collecting source code or requiring a cloud account. The product is **MCP-portable, Codex-qualified first**: Codex is the reference client used for automated acceptance, while the MCP contract is intentionally host-neutral and designed for compatible local clients such as VS Code, JetBrains AI Assistant, Zed, Cursor and Claude Code.

```text
AI/MCP host
   -> hero.start_quest
   -> normal agent work
   -> hero.finish_quest
   -> compact RPG status
   -> local SQLite history
```

Example:

```text
✨ +95 XP · Nova ур.1 · XP 95/100 · Доверие 51 · Риск 19
```

## Architecture snapshot — 11 August 2026

```text
C# 14 / .NET 10 LTS
.NET SDK 10.0.302
official ModelContextProtocol C# SDK 2.0.0
preferred MCP semantics: 2026-07-28
protocol negotiation: unpinned / SDK compatibility negotiation
EF Core SQLite 10.0.10
SQLitePCLRaw.bundle_e_sqlite3 3.0.5
System.CommandLine 2.0.10
xUnit.net v3 3.2.2
```

Hero Passport is a modular monolith:

```text
HeroPassport.Domain
        ^
        |
HeroPassport.Application
        ^
        |
HeroPassport.Infrastructure
        ^
        |
HeroPassport.App

HeroPassport.Web -> Application   # 0.2.0
```

MCP is an **adapter over the application**, not the architecture of the whole product.

## HP-MCP/2

The 0.1.0 MCP surface is exactly four explicitly registered tools, in stable order:

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

Key contract properties:

- explicit `questId` carries workflow state across calls;
- multiple distinct quests may be open for the same hero/project;
- repeated starts for the same logical work item converge to the same open quest;
- completed quests reward exactly once;
- protocol/session state is never required for application correctness;
- strict, shallow, bounded JSON Schema inputs;
- `structuredContent` is canonical machine output;
- concise text content is the human/compatibility representation;
- tool inventory is static, deterministic and explicitly registered;
- source code, diffs, raw logs, prompts, secrets, environment bags and workspace paths are absent from tool schemas.

See [`docs/MCP-CONTRACT.md`](docs/MCP-CONTRACT.md) and [`docs/API-CONTRACTS.md`](docs/API-CONTRACTS.md).

## Portable local integration

The 0.1.0 runtime transport is stdio. A local host starts:

```text
hero-passport mcp [--project-root <path>] [--hero <name-or-id>]
```

Project binding is a **launch concern**, not model input:

```text
explicit --project-root
    > host-provided process working directory
    > Git root discovery from that directory
    > working-directory fallback
```

`--project-root` exists because MCP host configuration formats differ and not every host provides the same working-directory primitive. Absolute workspace paths remain local process configuration and are not returned through MCP or persisted as project identity.

Host configuration is intentionally thin. Hero Passport does not ship Codex-, Cursor-, JetBrains- or Claude-specific runtime adapters. See [`docs/integrations/README.md`](docs/integrations/README.md).

## Protocol compatibility

Hero Passport targets the semantics of MCP `2026-07-28`, including explicit application state handles, deterministic/cacheable lists and stateless protocol design, but **does not pin the C# SDK server `ProtocolVersion` to `2026-07-28`**. Leaving protocol version negotiation unpinned lets the official SDK interoperate with supported initialize-era clients as well as 2026 clients.

The application itself is transport/session independent. A future Streamable HTTP host will explicitly use the SDK's stateless transport mode.

## Privacy

Hero Passport does not intentionally request or persist:

```text
source code
file contents
diffs/patches
changed-file bodies
raw terminal/build/test logs
full prompts/chat history
API keys/secrets
environment dumps
full workspace paths
arbitrary metadata/context bags
```

It stores bounded quest metadata and game state locally.

## Persistence

SQLite is authoritative local state.

Core guarantees:

```text
short-lived IDbContextFactory contexts
short synchronous SQLite segments
WAL + synchronous=FULL + foreign_keys=ON
one atomic FinishQuest transaction
UNIQUE xp_events.quest_id
finished retry returns original stored result
logical open-quest uniqueness per hero/project/work item
EF migrations from migration 0001
EF migration locking; no custom migration mutex
```

## Deployment boundaries

```text
0.1.0  local stdio MCP + CLI
0.1.1  integration/distribution polish and broader host qualification
0.2.0  local Blazor dashboard
later   Streamable HTTP only after a concrete deployment requirement
```

Private OpenAI surfaces can already reach a private stdio server through OpenAI Secure MCP Tunnel, so Hero Passport does not need to rush an HTTP listener merely for remote ChatGPT/Codex access.

Public/multi-tenant HTTP is a separate security/product architecture: principal identity, hero/project authorization, tenant isolation and remote persistence are not smuggled into the local MVP.

## Development philosophy

Modernity means current protocol semantics, strong contracts, portability and explicit boundaries—not maximum framework count.

Not baseline dependencies:

```text
MediatR
FluentValidation
AutoMapper
Dapper
Polly
Serilog/NLog
OpenTelemetry exporters
runtime plugin frameworks
CQRS/event-bus frameworks
```

See [`docs/DEPENDENCIES.md`](docs/DEPENDENCIES.md).

## Documentation

Start with [`docs/README.md`](docs/README.md).

Primary normative documents:

- [`docs/PRODUCT-SPEC.md`](docs/PRODUCT-SPEC.md)
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)
- [`docs/API-CONTRACTS.md`](docs/API-CONTRACTS.md)
- [`docs/MCP-CONTRACT.md`](docs/MCP-CONTRACT.md)
- [`docs/INTEROPERABILITY.md`](docs/INTEROPERABILITY.md)
- [`docs/DATA-MODEL.md`](docs/DATA-MODEL.md)
- [`docs/CONFIGURATION.md`](docs/CONFIGURATION.md)
- [`docs/DEPLOYMENT-MODES.md`](docs/DEPLOYMENT-MODES.md)
- [`docs/DISTRIBUTION.md`](docs/DISTRIBUTION.md)
- [`docs/SECURITY-PRIVACY.md`](docs/SECURITY-PRIVACY.md)
- [`docs/TESTING-QUALITY.md`](docs/TESTING-QUALITY.md)
- [`docs/ROADMAP.md`](docs/ROADMAP.md)
- [`docs/DECISION-LOG.md`](docs/DECISION-LOG.md)

## Status

Architecture/specification phase. Production implementation intentionally begins only from the reviewed contract and implementation plan.

## License

Apache License 2.0. See `LICENSE`.
