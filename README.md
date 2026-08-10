# Hero Passport

> Local-first RPG passport for AI coding agents.

Hero Passport is a small, deterministic state layer for AI agents. An agent starts a quest, works normally, finishes the quest, and receives a compact RPG status. Progress is stored locally in SQLite and can later be visualized by a local dashboard.

```text
Hero Passport = local-first MCP server
              + deterministic RPG engine
              + SQLite persistence
              + CLI
              + compact end-of-session status
              + local dashboard later
```

## Product direction

The MVP is **Codex-first, MCP-first, status-first, dashboard-second**.

Primary loop:

```text
Codex / MCP client
  -> hero.start_quest
  -> normal agent work
  -> hero.finish_quest
  -> compact displayText
  -> local SQLite history
```

Example final status:

```text
✨ +95 XP · Nova ур.1 · XP 95/100 · Доверие 51 · Риск 19
```

Hero Passport is **not** an agent orchestrator, telemetry collector, LLM judge, code scanner, cloud analytics service, or achievement marketplace.

## Technology baseline — 10 August 2026

- C# 14 / .NET 10 LTS
- .NET SDK `10.0.302`, runtime/ASP.NET Core `10.0.10`
- official `ModelContextProtocol` C# SDK `2.0.0`, MCP revision `2026-07-28`
- SQLite + EF Core SQLite `10.0.10`
- direct pin `SQLitePCLRaw.bundle_e_sqlite3` `3.0.5` (native SQLite `>= 3.53.4`)
- System.CommandLine `2.0.10`
- xUnit.net v3 `3.2.2`
- Microsoft Testing Platform via .NET 10 `dotnet test`
- Blazor Web App only after the MCP/status loop is stable

Preview dependencies are not part of the MVP baseline.

## Planned solution shape

```text
src/
  HeroPassport.Domain/          # pure deterministic domain model and rules
  HeroPassport.Application/     # use cases, ports, request/response contracts
  HeroPassport.Infrastructure/  # SQLite/EF, filesystem, migrations, adapters
  HeroPassport.App/             # executable: CLI + MCP stdio composition root
  HeroPassport.Web/             # post-MVP local Blazor dashboard

tests/
  HeroPassport.Domain.Tests/
  HeroPassport.Application.Tests/
  HeroPassport.Infrastructure.Tests/
  HeroPassport.App.Tests/
  HeroPassport.Architecture.Tests/
```

Compile-time dependency rule:

```text
Domain <- Application <- Infrastructure
                    \<- App
Application <------- Web (later; Infrastructure only at composition root)
```

The system is a modular monolith: one local product, explicit module boundaries, no runtime plugin loading before post-MVP.

## MVP MCP surface

Exactly four tools:

```text
hero.start_quest
hero.finish_quest
hero.current_quest
hero.get_card
```

No `hero.log_step`, per-file telemetry, diff ingestion, code upload, continuous activity stream, HTTP MCP, MCP Apps, MCP Tasks, achievements, artifacts, cloud sync, auth, team mode, or LLM judge in the minimal MVP.

## Privacy contract

By default Hero Passport must not persist or request:

- source code or file contents;
- diffs/patches;
- raw terminal/build/test logs;
- prompts or full chat history;
- secrets, API keys, environment variables;
- full workspace paths.

It stores compact quest metadata and game state only. See [`docs/SECURITY-PRIVACY.md`](docs/SECURITY-PRIVACY.md).

## Documentation

Start with [`docs/README.md`](docs/README.md).

Canonical documents:

- [`docs/PRODUCT-SPEC.md`](docs/PRODUCT-SPEC.md) — product scope, UX and acceptance criteria
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — system/module architecture and data flows
- [`docs/MCP-CONTRACT.md`](docs/MCP-CONTRACT.md) — MCP tools, schemas and output policy
- [`docs/ENGINE-SPEC.md`](docs/ENGINE-SPEC.md) — XP, levels, skills, traits, trust/risk rules
- [`docs/DATA-MODEL.md`](docs/DATA-MODEL.md) — SQLite model, migrations and transactions
- [`docs/SECURITY-PRIVACY.md`](docs/SECURITY-PRIVACY.md) — threat model and local-first privacy rules
- [`docs/TESTING-QUALITY.md`](docs/TESTING-QUALITY.md) — test pyramid, CI gates and release quality
- [`docs/integrations/CODEX.md`](docs/integrations/CODEX.md) — current Codex MCP integration
- [`docs/ROADMAP.md`](docs/ROADMAP.md) — implementation sequence and release gates
- [`docs/DECISION-LOG.md`](docs/DECISION-LOG.md) — architecture decision record
- [`docs/REFERENCES.md`](docs/REFERENCES.md) — official documentation baseline
- [`docs/superpowers/specs/2026-08-10-hero-passport-design.md`](docs/superpowers/specs/2026-08-10-hero-passport-design.md) — consolidated design specification
- [`docs/superpowers/plans/2026-08-10-hero-passport-implementation.md`](docs/superpowers/plans/2026-08-10-hero-passport-implementation.md) — task-by-task implementation plan

## Status

Architecture/specification phase. The repository intentionally contains no product implementation yet; implementation begins from the reviewed design and roadmap.

## License

Apache License 2.0. See [`LICENSE`](LICENSE).
