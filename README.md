# Hero Passport

> Local-first RPG passport for AI coding agents.

Hero Passport is a small deterministic state layer that turns meaningful AI-agent work into persistent RPG progression without collecting source code or requiring a cloud account.

```text
Codex / MCP client
   -> hero.start_quest
   -> normal work
   -> hero.finish_quest
   -> compact RPG status
   -> local SQLite history
```

Example:

```text
✨ +95 XP · Nova ур.1 · XP 95/100 · Доверие 51 · Риск 19
```

## Architecture snapshot — 10 August 2026

```text
C# 14 / .NET 10 LTS
.NET SDK 10.0.302
official ModelContextProtocol C# SDK 2.0.0
MCP revision 2026-07-28
EF Core SQLite 10.0.10
SQLitePCLRaw.bundle_e_sqlite3 3.0.5
System.CommandLine 2.0.10
xUnit.net v3 3.2.2
```

Architecture:

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

The product is a **modular monolith**, not an MCP gateway/platform.

## MCP MVP

Exactly four explicitly registered stdio tools:

```text
hero.start_quest
hero.finish_quest
hero.current_quest
hero.get_card
```

No dynamic tool discovery/toolsets, HTTP/OAuth, MCP Apps, Tasks, runtime plugins or hidden protocol-session state in 0.1.0.

MCP contracts are intentionally small:

- local application state resolves hero/project/locale/presentation;
- strict JSON Schema with `additionalProperties: false`;
- structured results + output schemas;
- accurate read-only/idempotent/open-world annotations;
- no source/diff/log/path/secret fields;
- server instructions describe the cross-tool lifecycle.

## Privacy

Hero Passport does **not** intentionally collect or persist:

```text
source code
file contents
diffs/patches
raw terminal/build/test logs
full prompts/chat history
API keys/secrets
environment dumps
full workspace paths
```

It stores compact quest/game state locally.

## Persistence

SQLite is authoritative local state.

Key guarantees:

```text
one short DbContext/unit of work
WAL + synchronous=FULL + foreign keys
one atomic finish transaction
UNIQUE xp_events.quest_id
finish retry returns original persisted outcome
EF migrations from migration 0001
EF built-in migration locking; no custom migration mutex
```

Microsoft.Data.Sqlite does not provide real async SQLite I/O, so database segments are intentionally short and synchronous rather than wrapped in fake async/`Task.Run`.

## Platform data locations

```text
Windows  %LOCALAPPDATA%\HeroPassport
macOS    ~/Library/Application Support/HeroPassport
Linux    XDG data/config/state roots
```

`HERO_PASSPORT_HOME` provides isolated development/test storage.

## Codex-first integration

Preferred registration:

```bash
codex mcp add hero-passport -- hero-passport mcp
codex mcp list
```

Codex owns its own MCP configuration. Hero Passport does not mutate `~/.codex/config.toml` in MVP.

`mcp_servers.<id>.cwd` can pin a local project working directory when needed; workspace path remains local host configuration, not MCP payload/database state.

## Development philosophy

Modernity here means current protocol semantics + precise boundaries + small mechanisms, not maximum framework count.

Deliberately not baseline dependencies:

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

See [`docs/DEPENDENCIES.md`](docs/DEPENDENCIES.md) for the full accept/reject analysis.

## Documentation

Start with [`docs/README.md`](docs/README.md).

Most important:

- [`docs/PRODUCT-SPEC.md`](docs/PRODUCT-SPEC.md)
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)
- [`docs/ECOSYSTEM-BENCHMARK.md`](docs/ECOSYSTEM-BENCHMARK.md)
- [`docs/DEPENDENCIES.md`](docs/DEPENDENCIES.md)
- [`docs/MCP-CONTRACT.md`](docs/MCP-CONTRACT.md)
- [`docs/ENGINE-SPEC.md`](docs/ENGINE-SPEC.md)
- [`docs/DATA-MODEL.md`](docs/DATA-MODEL.md)
- [`docs/CONFIGURATION.md`](docs/CONFIGURATION.md)
- [`docs/SECURITY-PRIVACY.md`](docs/SECURITY-PRIVACY.md)
- [`docs/TESTING-QUALITY.md`](docs/TESTING-QUALITY.md)
- [`docs/integrations/CODEX.md`](docs/integrations/CODEX.md)
- [`docs/ROADMAP.md`](docs/ROADMAP.md)
- [`docs/DECISION-LOG.md`](docs/DECISION-LOG.md)
- [`docs/REFERENCES.md`](docs/REFERENCES.md)
- [`docs/superpowers/specs/2026-08-10-hero-passport-design.md`](docs/superpowers/specs/2026-08-10-hero-passport-design.md)
- [`docs/superpowers/plans/2026-08-10-hero-passport-implementation.md`](docs/superpowers/plans/2026-08-10-hero-passport-implementation.md)

## Current status

Architecture/specification phase. Implementation begins from the reviewed roadmap/plan; no product code is intentionally mixed into the architecture PR.

Target:

```text
0.1.0 = local Codex-first MCP + CLI MVP
0.2.0 = local Blazor dashboard
```

## License

Apache License 2.0. See `LICENSE`.
