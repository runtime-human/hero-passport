# Hero Passport — Architecture Decision Log

**Status:** canonical decision record  
**Baseline:** 2026-08-10

Changes to protocol, persistence, privacy, scoring, dependency baseline or module boundaries require a decision-log update.

## ADR-001 — C# 14 / .NET 10 LTS

**Status:** Accepted

Use C# 14 on `net10.0`, exact SDK baseline `10.0.302`. The current official MCP C# SDK, EF Core/SQLite, CLI stack and later Blazor dashboard can stay in one strongly typed ecosystem.

**Revisit:** only if a required platform cannot run the supported .NET 10 stack or measured deployment constraints make it unsuitable.

## ADR-002 — Modular monolith, no runtime plugins before post-MVP

**Status:** Accepted

One local product, one SQLite state store, explicit compile-time modules. No external DLL loading, plugin ABI, module registry, broker or distributed event architecture in MVP.

Cheap extensibility seams are rule versions, normalizers, ports, read models, migrations and golden fixtures.

## ADR-003 — Domain / Application / Infrastructure / App boundaries

**Status:** Accepted

```text
Domain <- Application <- Infrastructure <- App composition root
                         ^
                         + Web composition later
```

Domain contains rules only; Application owns use cases/contracts/ports; Infrastructure owns EF/SQLite/filesystem; App owns CLI/MCP. Web later consumes Application read models.

## ADR-004 — No standalone Contracts assembly initially

**Status:** Accepted

Keep transport-neutral records in `HeroPassport.Application.Contracts`. Extract a dedicated assembly only when an independently versioned second host/package creates a real contract boundary.

This revises the source report's early Contracts-project recommendation in favor of YAGNI without sacrificing future extraction.

## ADR-005 — Official MCP C# SDK 2.0.0; stdio-only MVP

**Status:** Accepted

Use stable `ModelContextProtocol 2.0.0`, current MCP revision `2026-07-28`, and local stdio only.

HTTP/Streamable HTTP requires a separate auth/network threat model. Apps and Tasks extensions are not MVP dependencies.

## ADR-006 — Exactly four deterministic tools

**Status:** Accepted

Fixed order:

```text
hero.start_quest
hero.finish_quest
hero.current_quest
hero.get_card
```

No step logging/telemetry/history-dump/judge tools before a concrete need. Stable deterministic discovery minimizes tool-context and prompt-cache churn.

## ADR-007 — No source artifacts or workspace path in MCP schema

**Status:** Accepted

Schema `1.0` has no source code, file contents, diffs, raw logs, prompts, environment bags, secrets or `workspacePath`.

Project auto-resolution is local. Persist display name + versioned SHA-256 identity fingerprint, not cleartext full path. An explicit Codex `cwd` may be configured locally where a host needs it.

## ADR-008 — Deterministic versioned RPG rules; no LLM judge

**Status:** Accepted

XP, levels, skills, trust/risk and traits use pure integer rules. Every completed quest stores relevant rule versions and immutable reward breakdown.

New semantics apply prospectively under a new rule version; historical rewards are not silently recalculated.

## ADR-009 — Integer permille scoring

**Status:** Accepted

Result multipliers are `1000/600/200/300/0`, eliminating floating-point/culture drift. Reward rule `1.0.0` locks the standard clean coding fixture to `95 XP`.

## ADR-010 — SQLite + EF Core migrations + WAL + current safe native bundle

**Status:** Accepted

Baseline:

```text
Microsoft.EntityFrameworkCore.Sqlite   10.0.10
SQLitePCLRaw.bundle_e_sqlite3           3.0.5
native SQLite                          >= 3.53.4
```

Use migrations, `foreign_keys=ON`, WAL, bounded busy timeout and no shared-cache optimization with WAL.

This supersedes the source report's interim `2.1.12` pin: by 2026-08-10 stable SQLitePCLRaw `3.0.5` is newer and carries a newer native SQLite floor. Runtime version is verified in tests/releases.

## ADR-011 — Immutable XP ledger plus current projections

**Status:** Accepted

One immutable `xp_events` quest-reward event per completed quest; hero/skill/trait/project projections update in the same atomic transaction.

`xp_events.quest_id` is unique. Future administrative corrections use compensating events rather than destructive history edits.

## ADR-012 — Finish retries return persisted original outcome

**Status:** Accepted

A repeated `finish_quest` on a completed quest performs no scoring/progression write. It returns the stored original outcome with `alreadyFinished=true`, even if current rules have since changed.

## ADR-013 — UUIDv7 + TimeProvider

**Status:** Accepted

Generate IDs with `Guid.CreateVersion7()` and use injected .NET `TimeProvider` for behavior-affecting time. No third-party ID/time abstraction is required.

## ADR-014 — Exact SDK + Central Package Management + lock files

**Status:** Accepted

Use exact `global.json` SDK pin, central package versions, committed `packages.lock.json`, and locked restore in CI/release.

Dependency updates are deliberate reviewable changes.

## ADR-015 — xUnit.net v3 + Microsoft Testing Platform

**Status:** Accepted

Stable `xunit.v3 3.2.2` with .NET 10 `dotnet test` / Microsoft Testing Platform repository configuration. Keep `xunit.runner.visualstudio 3.1.5` privately only where compatibility tooling still needs it.

Do not adopt prerelease xUnit v4 in MVP.

## ADR-016 — Use native Codex MCP management

**Status:** Accepted

Document/validate:

```text
codex mcp add hero-passport -- hero-passport mcp
codex mcp list
```

Hero Passport does not mutate `~/.codex/config.toml` in MVP. Codex owns its config schema and native management surface.

## ADR-017 — Codex CLI current-workspace is first acceptance path

**Status:** Accepted

The first E2E target is local Codex CLI launched in the intended repo/workspace. Current Codex `mcp add` creates stdio config with `cwd=None`, while config supports explicit `mcp_servers.<id>.cwd`.

If a specific client launches the server elsewhere, document a local explicit `cwd` for that setup rather than sending the path through model-visible MCP arguments.

**Revisit:** if Codex exposes a stable per-call/session workspace identity to local MCP servers.

## ADR-018 — .NET tool first; self-contained/single-file later

**Status:** Accepted

First packaging target is a .NET tool. Per-RID self-contained builds follow after core validation. Single-file is deferred until native SQLite bundling/extraction/update behavior has explicit tests.

## ADR-019 — Dashboard after 0.1.0, read-model driven

**Status:** Accepted

Ship the two-call status loop first. `HeroPassport.Web` later uses local Blazor/ASP.NET Core, loopback by default, Application read models, and never injects `HeroPassportDbContext` into Razor components.

## ADR-020 — Traits are not achievements

**Status:** Accepted

Traits are persistent behavioral progression. Achievements, artifacts/items, runtime plugins, MCP Apps/Tasks, cloud/team/auth and self-evolution are separately designed post-MVP modules, not hidden inside the trait system.

## ADR-021 — Logical JSON export before raw DB backup

**Status:** Accepted

MVP user portability uses versioned logical JSON export. Do not present live `.db` file copying as a safe generic backup while WAL may hold current state.

A future raw backup feature must use supported SQLite backup/checkpoint mechanisms and crash-consistency tests.
