# AGENTS.md

## Repository mission

Build Hero Passport as a local-first RPG state layer for AI coding agents. The MVP is Codex-first, MCP stdio-first, deterministic, privacy-preserving, and intentionally small.

## Required baseline

- Target `net10.0`, C# 14.
- Pin the repository SDK through `global.json` to the approved .NET 10 SDK.
- Use Central Package Management and committed package lock files.
- Use stable dependencies only unless an ADR explicitly approves a preview.
- Keep the official MCP C# SDK on the current approved stable major/revision.
- Use real SQLite for storage/integration tests; do not substitute EF InMemory for SQLite semantics.

## Architecture rules

Dependency direction is strict:

```text
HeroPassport.Domain            -> no project dependencies
HeroPassport.Application       -> Domain
HeroPassport.Infrastructure    -> Application + Domain
HeroPassport.App               -> Application + Infrastructure
HeroPassport.Web (later)       -> Application; Infrastructure only in composition root
```

Organize code by feature inside projects (`Quests`, `Heroes`, `Rewards`, `Skills`, `Traits`, `Projects`) rather than large technical catch-all folders.

Business rules belong in Domain/Application. EF Core, SQLite, filesystem, console, MCP SDK, ASP.NET Core and CLI types must not leak into Domain.

Do not add a runtime plugin system, external DLL loading, event bus, distributed cache, message broker, cloud backend, CQRS framework, MediatR, AutoMapper or repository abstraction per entity unless a concrete requirement and ADR justify it.

## MCP invariants

MVP exposes only:

```text
hero.start_quest
hero.finish_quest
hero.current_quest
hero.get_card
```

Tool registration order must be deterministic. Tool schemas must be bounded and reject unknown properties where practical.

`hero-passport mcp` stdout is protocol-only. Never write banners, logs, Spectre output or diagnostics to stdout in MCP mode. Use stderr or an explicitly enabled local log sink.

For meaningful coding/review/debugging/documentation/planning sessions, consumer instructions should call `hero.start_quest` once at the beginning and `hero.finish_quest` once at the end. Do not introduce step-by-step telemetry calls.

## Privacy invariants

Never add request fields or persistence for raw source code, file contents, diffs, patches, raw terminal/build/test logs, full prompts/chat history, secrets, API keys or environment variables in MVP.

Do not persist the full workspace path by default. Resolve a project locally to a display name plus privacy-preserving fingerprint.

Do not log tool request/response bodies by default.

## Determinism invariants

- XP and score rules use integer arithmetic.
- Persist rule version with reward events/reports.
- Repeating `finish_quest` for an already-finished quest must not grant XP twice.
- Canonical skill keys are persisted; localized labels are presentation-only.
- Time comes from injected `TimeProvider`, not direct `DateTime.UtcNow` calls in domain/application code.

Russian UI terminology:

```text
scope_control        -> Контроль
Clean scope bonus    -> Бонус за контроль
Scope violation      -> Выход за задачу
```

## Testing rules

Development is test-first for domain behavior and regression fixes.

Required gates before merging implementation changes:

```text
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

Add focused tests for every domain rule, idempotency path, migration/constraint, MCP stdout behavior and privacy invariant touched by a change.

## Documentation discipline

Read the smallest relevant canonical document from `docs/` before changing behavior. Update the corresponding specification/decision when behavior changes.

Do not silently change formulas, persistence invariants, MCP schemas or privacy boundaries. Such changes require an entry in `docs/DECISION-LOG.md` and versioning consideration.

## Scope control

The minimal MVP excludes achievements, artifacts/items, runtime plugins, HTTP MCP, MCP Apps/Tasks, cloud sync, team/auth, LLM judging, self-evolution, continuous telemetry and per-keystroke/per-diff XP. Do not implement excluded scope as opportunistic cleanup.
