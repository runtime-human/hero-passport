# Hero Passport — architecture decision log

**Status:** Living normative ADR index  
**Snapshot:** 2026-08-10

This file records decisions that should not be rediscovered during implementation. If a decision changes, update the relevant detailed specification and add a superseding entry rather than silently editing history/meaning.

## ADR-001 — C# 14 / .NET 10 LTS

**Decision:** use C# 14 on pinned .NET 10 SDK/runtime.

**Why:** one mature cross-platform stack covers MCP host, CLI, persistence and later Blazor dashboard; official MCP C# SDK is current/stable.

**Rejected:** Go/Rust/TypeScript/Python core for MVP. Each can work technically but would fragment UI/tooling or add delivery cost without solving a stronger requirement.

---

## ADR-002 — local modular monolith

**Decision:** Domain -> Application -> Infrastructure -> App, Web later.

**Why:** one user, one local DB, no independent deployment/scaling ownership.

**Rejected:** microservices, message broker, service mesh.

---

## ADR-003 — no standalone Contracts assembly initially

**Decision:** transport-neutral contracts live in Application.

**Why:** there is no independently versioned second consumer yet. Extraction remains cheap later.

---

## ADR-004 — official MCP C# SDK 2.0.0

**Decision:** use `ModelContextProtocol 2.0.0` main package for stdio host.

**Why:** official protocol implementation/hosting/DI; MCP 2026-07-28 support.

**Rejected:** hand-written JSON-RPC/MCP, Core-only package, AspNetCore MCP package before HTTP is needed.

---

## ADR-005 — fixed four-tool MCP surface

**Decision:** exactly:

```text
hero.start_quest
hero.finish_quest
hero.current_quest
hero.get_card
```

**Why:** token/tool-selection simplicity; covers complete agent workflow.

**Rejected:** history/admin/log-step/file-tracking/evaluation tools in MVP.

---

## ADR-006 — explicit tool registration, no assembly scanning

**Decision:** register four dedicated MCP tool adapter types explicitly.

**Why:** fail-closed inventory, deterministic order, easier review/tests/trimming experiments.

**Influence:** GitHub MCP's strict inventory/config practices; large dynamic discovery is useful only at much larger surfaces.

---

## ADR-007 — MCP is not the full product API

**Decision:** admin/doctor/export/data paths/full history belong to CLI/dashboard by default.

**Why:** Context7/Playwright/DBHub patterns reinforce that always-advertised MCP surface has context cost. Shell/CLI is better for many operator operations.

---

## ADR-008 — explicit quest handle, no MCP-session correctness

**Decision:** `start_quest -> questId -> finish_quest` and durable SQLite state.

**Why:** MCP 2026-07-28 stateless direction; reconnect/restart resilience.

**Rejected:** hidden in-memory “current MCP session” state.

---

## ADR-009 — strict compact MCP contracts

**Decision:** remove per-call `schemaVersion`, `heroId`, `projectId`, `workspacePath`, `locale`, `outputMode`; remove `agentHint` and duplicated `statusText` outputs.

**Why:** these are protocol/local app/presentation concerns, not choices the model should repeatedly make.

**Result:** smaller schema, fewer conflicting inputs, lower privacy risk.

---

## ADR-010 — JSON Schema 2020-12 + output schemas

**Decision:** strict schemas, `additionalProperties:false`, bounded fields, `structuredContent`, `outputSchema`, accurate annotations.

**Why:** current MCP semantics; machine-checkable contract; prevents arbitrary metadata leakage.

---

## ADR-011 — no MCP Tasks/Apps/HTTP/OAuth in 0.1

**Decision:** all four operations are short local stdio calls; task support forbidden.

**Why:** no long-running/remote requirement.

**Review trigger:** genuine remote/team/UI-in-client requirement.

---

## ADR-012 — deterministic RPG engine, no LLM judge

**Decision:** integer versioned rules calculate XP/levels/skills/traits/Trust/Risk.

**Why:** explainability, reproducibility, zero remote cost/privacy expansion.

---

## ADR-013 — Presentation leaves Domain/Application

**Decision:** `HeroPassport.App/Presentation` renders localized `displayText`; Domain/Application return typed values.

**Why:** localization/punctuation is not game policy; later Web consumes typed data directly.

**Supersedes:** architecture-v1 assumption that Core/Application could produce card/status text.

---

## ADR-014 — local state resolved outside MCP calls

**Decision:** active hero, project identity, locale and presentation are local application/config state.

**Why:** reduces agent choices and token overhead; avoids path/user-state leakage.

---

## ADR-015 — SQLite + EF Core

**Decision:** EF Core SQLite 10.0.10, migrations from day one.

**Why:** embedded local fit, migration tooling, future dashboard projections, one persistence stack.

**Rejected:** server DB, Dapper/raw baseline, EF InMemory product tests.

---

## ADR-016 — short-lived DbContextFactory pattern

**Decision:** `IDbContextFactory<HeroPassportDbContext>`, one context/unit of work.

**Why:** stdio/CLI have no HTTP request scope; SQLite/DbContext thread safety; aligns with later Blazor guidance.

---

## ADR-017 — synchronous SQLite execution

**Decision:** actual SQLite/EF DB segments are synchronous and short.

**Why:** Microsoft.Data.Sqlite async methods execute synchronously because SQLite has no async I/O.

**Rejected:** `Task.Run` wrappers and fake async ceremony.

---

## ADR-018 — WAL + FULL durability

**Decision:** WAL, foreign keys ON, `synchronous=FULL`.

**Why:** reader concurrency plus stronger power-loss durability; write rate is tiny.

**Rejected:** optimizing to NORMAL without measurement/durability decision.

---

## ADR-019 — 5-second SQLite busy policy

**Decision:** initial `Default Timeout=5` seconds, validated by concurrency tests.

**Why:** local write transactions should be milliseconds; 30-second provider default would create poor interactive-agent behavior when something is wrong.

**Status:** application policy subject to measurement before 0.1.0, not protocol invariant.

---

## ADR-020 — use EF migration lock, no custom mutex

**Decision:** rely on EF Core migration locking (`__EFMigrationsLock` on SQLite); `doctor` diagnoses abandoned lock.

**Why:** EF Core 9+ already implements database-wide migration lock. A second lock creates inconsistent recovery paths.

---

## ADR-021 — atomic finish + unique XP ledger

**Decision:** quest report/reward/hero/skill/trait/project updates commit in one transaction; `UNIQUE xp_events.quest_id`.

**Why:** retries/races cannot farm XP; database is final integrity boundary.

---

## ADR-022 — historical reward immutability

**Decision:** completed quest stores original reward breakdown/rule versions; retries return it, never recalculate under current rules.

**Why:** upgrades must not change earned history.

---

## ADR-023 — platform-correct application data paths

**Decision:** Windows LocalApplicationData, macOS Application Support, Linux XDG data/config/state; `HERO_PASSPORT_HOME` for isolated dev/tests.

**Why:** SQLite DB is machine-local and should not use Windows roaming ApplicationData; native platform conventions reduce surprises.

---

## ADR-024 — strict config v1

**Decision:** tiny versioned JSON config, unknown properties rejected; application state remains SQLite.

**Why:** avoids arbitrary dynamic configuration and model-facing config choices.

---

## ADR-025 — Codex owns Codex configuration

**Decision:** document/use `codex mcp add/list` and native `mcp_servers.*`; no Hero Passport TOML mutator in MVP.

**Why:** OpenAI owns schema/evolution; avoids corrupting unrelated user config.

---

## ADR-026 — Codex server instructions + short AGENTS guidance

**Decision:** essential workflow/privacy guidance in server instructions (first 512 chars self-contained for Codex), short project AGENTS snippet.

**Why:** workflow is cross-tool context; do not duplicate `agentHint` in every response.

---

## ADR-027 — agent evaluations are a first-class quality layer

**Decision:** maintain behavioral eval corpus in addition to unit/integration/protocol tests.

**Why:** Sentry MCP demonstrates and the problem demands a separate test for whether an agent chooses tools correctly. A valid schema cannot prove model workflow behavior.

---

## ADR-028 — no runtime tool discovery/toolsets before scale threshold

**Decision:** challenge tool growth; dedicated review if inventory exceeds 6.

**Why:** GitHub MCP's dynamic discovery/toolsets solve dozens/hundreds of operations. Four tools do not justify that complexity.

---

## ADR-029 — no MediatR

**Decision:** direct typed handlers through DI.

**Why:** four core use cases do not need an in-process message bus/pipeline framework; direct graph is clearer for humans/Codex.

---

## ADR-030 — no AutoMapper

**Decision:** explicit mappings.

**Why:** boundaries are small and privacy-sensitive; hidden mapping can leak fields accidentally.

---

## ADR-031 — no FluentValidation for MVP

**Decision:** JSON Schema + small explicit semantic validators + options validation.

**Why:** contract set is tiny; a DSL adds conventions without reducing enough code.

---

## ADR-032 — no generic repository

**Decision:** capability-specific stores/queries.

**Why:** application invariants are not generic CRUD and transaction needs must remain visible.

---

## ADR-033 — no Polly baseline

**Decision:** no general retry library.

**Why:** no remote dependencies; SQLite provider already retries busy/locked until command timeout; broad retries risk duplicate side effects.

---

## ADR-034 — no third-party logging baseline

**Decision:** Microsoft.Extensions.Logging, stderr/optional local file.

**Why:** sufficient local diagnostics; no remote sinks.

---

## ADR-035 — defer Spectre.Console

**Decision:** plain/scriptable CLI first.

**Why:** presentation-only dependency; avoiding it initially simplifies stdout separation. May be reconsidered after correctness.

---

## ADR-036 — no OpenTelemetry exporter baseline

**Decision:** use normal .NET diagnostics seams only; exporter post-MVP/opt-in.

**Why:** single local stdio process has no distributed tracing need.

---

## ADR-037 — explicit native SQLite pin

**Decision:** direct `SQLitePCLRaw.bundle_e_sqlite3 3.0.5` and runtime `sqlite_version()` test.

**Why:** native security/durability baseline must not be an accidental transitive choice.

---

## ADR-038 — Central Package Management + lock/audit

**Decision:** CPM, committed lock files, locked CI/release restore, NuGet audit including transitives.

**Why:** reproducible agent/CI builds and visible supply-chain changes.

---

## ADR-039 — default hero/global state with project projections

**Decision:** hero exists globally; project identity/stats are separate. MCP doesn't require heroId/projectId per call.

**Why:** passport identity should persist across repositories while project stats remain meaningful.

---

## ADR-040 — achievements/artifacts/plugins remain post-MVP

**Decision:** retain only cheap seams (rule versions, normalizers, read models, contracts/goldens); no subsystem skeleton that has no current use.

**Why:** YAGNI and prior project scope decision.

---

## ADR-041 — documentation/research hierarchy

When sources conflict or age:

```text
current official specification/documentation
> current official SDK/package docs/source
> current production open-source repository behavior
> reference/example repositories
> historical report/old project docs
```

Open repositories are mined for patterns regardless of license in this architecture analysis, per project instruction; copied implementation still requires a separate practical/legal decision if distribution policy ever matters.

---

## ADR-042 — benchmark rerun triggers

Re-run ecosystem/library analysis before:

```text
tool count > 6
remote/HTTP MCP
multi-user/team
second storage backend
runtime plugins
network/API dependencies
MCP Apps/Tasks
major MCP SDK/protocol revision
major EF/SQLite architecture change
```

Modernity is maintained by explicit review triggers, not by speculative abstractions.
