# Hero Passport

> Portable local-first RPG passport for AI coding agents.

Hero Passport turns meaningful AI-agent work into persistent RPG progression without collecting source code or requiring a cloud account. Codex is the first Qualified reference host; HP-MCP/2 is host-neutral.

```text
AI/MCP host
  -> hero.start_quest
  -> normal agent work
  -> hero.finish_quest
  -> deterministic local RPG progression
  -> SQLite history
```

Example presentation:

```text
✨ +95 XP · Nova ур.1 · XP 95/100 · Доверие 51 · Риск 19
```

## Architecture snapshot — 11 August 2026

```text
C# 14 / .NET 10 LTS
.NET SDK 10.0.302
official ModelContextProtocol C# SDK 2.0.0
preferred MCP semantics 2026-07-28; protocol unpinned for SDK compatibility
EF Core SQLite 10.0.10
SQLitePCLRaw.bundle_e_sqlite3 3.0.5
System.CommandLine 2.0.10
xUnit.net v3 3.2.2
```

```text
Domain
  ^
Application
  ^
Infrastructure
  ^
App (CLI + MCP stdio + presentation)

Web -> Application   # 0.2+
```

MCP is an adapter over transport-neutral Application semantics, not the architecture of the whole product.

## HP-MCP/2

Exactly four explicitly registered tools:

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

Current exact wire behavior is specified in [`docs/WIRE-CONTRACT.md`](docs/WIRE-CONTRACT.md).

Important properties after the v3.1 deep dive:

- `start_quest` is **not** advertised MCP-idempotent; it is only retry/dedup-safe while the same normalized declaration remains open;
- multiple distinct quests may be open for one hero/project, capped at 16;
- `QuestDedupKeyV1` hashes `questType + SafeTextV1(goal)` with **case preserved**;
- successful MCP calls return typed `structuredContent` plus one minified JSON TextContent representing the same object for backward compatibility;
- tool/business errors return `isError=true` + safe TextContent and no structuredContent;
- runtime validators explicitly enforce bounds/UUID/text rules because C# SDK schema annotations do not validate arguments at runtime;
- no source/diff/log/path/secret fields exist in tool contracts.

## Project identity

Project binding is local launch state:

```text
hero-passport mcp [--project-root <path>] [--hero <selector>]
```

Git-aware identity is specified in [`docs/PROJECT-IDENTITY.md`](docs/PROJECT-IDENTITY.md):

- linked Git worktrees share one project through canonical `git-common-dir`;
- ordinary nested cwd maps to the whole Git repository;
- explicit `--project-root` inside a monorepo creates a deliberate repo-relative scope;
- submodules/nested repositories are separate by default;
- Git safety failures never silently become standalone identities;
- full paths and remote URLs are not persisted.

## Persistence reliability

SQLite is authoritative local state. [`docs/PERSISTENCE-RELIABILITY.md`](docs/PERSISTENCE-RELIABILITY.md) fixes the write protocol:

```text
read-modify-write operation
  -> short non-deferred Serializable transaction
  -> selected Microsoft.Data.Sqlite 10.0.10 behavior: BEGIN IMMEDIATE
  -> read/check/write
  -> COMMIT
```

This makes the 16-active-quest cap and finish idempotency race-safe without a custom mutex.

Operational baseline:

```text
WAL
synchronous=FULL
foreign_keys=ON
Default Timeout=5
local filesystem only for writable supported DB
actual sqlite_version() qualification >= 3.51.3
```

Live database backup never uses raw `File.Copy`; use SQLite's online backup API and verify the result. WAL/SHM recovery files are never manually deleted.

## Privacy

Hero Passport does not intentionally request/persist:

```text
source/file contents
diffs/patches
raw build/test/terminal logs
full prompts/chat history
API keys/secrets/tokens
environment dumps
full workspace paths
Git remote URLs
arbitrary metadata/context bags
```

## Deployment boundary

```text
0.1.0  local stdio MCP + CLI
0.1.1  broader host qualification/distribution polish
0.2.0  local Blazor dashboard
later   own Streamable HTTP only after a concrete requirement
```

Private OpenAI surfaces can use OpenAI Secure MCP Tunnel to the local server; public/multi-tenant HTTP remains a separate authorization/storage architecture.

## Documentation

Start with [`docs/README.md`](docs/README.md).

The three high-risk deep dives are:

- [`docs/PROJECT-IDENTITY.md`](docs/PROJECT-IDENTITY.md)
- [`docs/PERSISTENCE-RELIABILITY.md`](docs/PERSISTENCE-RELIABILITY.md)
- [`docs/WIRE-CONTRACT.md`](docs/WIRE-CONTRACT.md)

Other normative files:

- [`docs/PRODUCT-SPEC.md`](docs/PRODUCT-SPEC.md)
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)
- [`docs/API-CONTRACTS.md`](docs/API-CONTRACTS.md)
- [`docs/MCP-CONTRACT.md`](docs/MCP-CONTRACT.md)
- [`docs/ENGINE-SPEC.md`](docs/ENGINE-SPEC.md)
- [`docs/DATA-MODEL.md`](docs/DATA-MODEL.md)
- [`docs/CONFIGURATION.md`](docs/CONFIGURATION.md)
- [`docs/SECURITY-PRIVACY.md`](docs/SECURITY-PRIVACY.md)
- [`docs/TESTING-QUALITY.md`](docs/TESTING-QUALITY.md)
- [`docs/DEPENDENCIES.md`](docs/DEPENDENCIES.md)
- [`docs/DEPLOYMENT-MODES.md`](docs/DEPLOYMENT-MODES.md)
- [`docs/DISTRIBUTION.md`](docs/DISTRIBUTION.md)
- [`docs/DECISION-LOG.md`](docs/DECISION-LOG.md)
- [`docs/REFERENCES.md`](docs/REFERENCES.md)
- [`docs/integrations/README.md`](docs/integrations/README.md)
- [`docs/superpowers/plans/2026-08-10-hero-passport-implementation.md`](docs/superpowers/plans/2026-08-10-hero-passport-implementation.md)

## Current status

Architecture/specification phase. No product implementation is intentionally mixed into this documentation PR, so no product build/test claim is made yet.

## License

Apache License 2.0. See `LICENSE`.
