# Hero Passport — dependency and library decisions

**Status:** Accepted baseline  
**Snapshot date:** 2026-08-10  
**Rule:** stable dependencies only unless an ADR explicitly approves a preview dependency.

## 1. Goal

This document prevents implementation-time library drift. Hero Passport deliberately uses a small dependency set. A package is accepted only when it materially reduces implementation risk or removes infrastructure we would otherwise have to own.

Evaluation criteria:

```text
1. official/current support
2. architectural fit
3. deterministic behavior
4. security/update quality
5. cross-platform behavior
6. trimming/packaging impact
7. testability
8. token/context impact on coding agents
9. maintenance surface
10. replacement cost
```

Popularity is secondary. “Modern” does not mean “add a framework for every pattern”.

---

## 2. Accepted production dependencies

### 2.1 ModelContextProtocol 2.0.0 — ACCEPT

Package:

```xml
<PackageVersion Include="ModelContextProtocol" Version="2.0.0" />
```

Why:

- official C# SDK;
- stable 2.0.0 released with MCP 2026-07-28;
- provides hosting/DI and stdio transport integration needed by `HeroPassport.App`;
- avoids reimplementing JSON-RPC/framing/schema/protocol compatibility.

Why not `ModelContextProtocol.Core`:

Hero Passport is not building a custom low-level transport/client. The main package is explicitly the official fit for most non-HTTP server projects.

Why not `ModelContextProtocol.AspNetCore`:

MVP is stdio only. Pulling ASP.NET-specific MCP transport code into the executable before a remote requirement exists increases dependencies and attack surface with no product benefit.

Upgrade rule:

- patch/minor upgrades require protocol-schema regression tests;
- major/protocol-revision upgrades require an ADR and Codex E2E validation;
- never use `*`, floating versions or `--prerelease` in product setup docs.

### 2.2 Microsoft.EntityFrameworkCore.Sqlite 10.0.10 — ACCEPT

Package:

```xml
<PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.10" />
```

Why EF Core rather than raw SQL/Dapper:

- first-class SQLite provider;
- migrations from day one;
- explicit entity configuration and indexes/constraints;
- simple projections for future dashboard/read models;
- one data-access paradigm across writes and reads;
- mature tooling and .NET 10 alignment.

Important constraint:

EF Core is persistence infrastructure, **not the domain model**. EF entities/configuration stay in Infrastructure. Domain policy never depends on EF.

Operational constraint:

Microsoft.Data.Sqlite has no real async I/O. SQLite calls are deliberately synchronous and short; using EF async methods merely to look “async all the way” is rejected.

### 2.3 Microsoft.EntityFrameworkCore.Design 10.0.10 — ACCEPT, dev/private asset

Used for migration tooling only.

Expected package metadata:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" PrivateAssets="all" />
```

It must not become a runtime dependency of the shipped application.

### 2.4 SQLitePCLRaw.bundle_e_sqlite3 3.0.5 — ACCEPT and pin directly

Package:

```xml
<PackageVersion Include="SQLitePCLRaw.bundle_e_sqlite3" Version="3.0.5" />
```

Why direct pin:

- makes the native SQLite baseline intentional instead of an incidental transitive choice;
- 3.0.5 is the current stable major-line package at the snapshot date;
- it depends on a native SQLite baseline >= 3.53.4;
- avoids the old vulnerable/obsolete native version drift that can occur when only relying on broad transitive lower bounds.

Implementation acceptance test must execute:

```sql
SELECT sqlite_version();
```

and record/assert an accepted minimum. NuGet metadata alone is not sufficient evidence for the actually loaded native library.

### 2.5 System.CommandLine 2.0.10 — ACCEPT

Package:

```xml
<PackageVersion Include="System.CommandLine" Version="2.0.10" />
```

Why:

- stable 2.x line;
- Microsoft-maintained command parsing;
- good cross-platform conventions;
- enough for subcommands, validation, help and machine-readable modes.

Why not 3.x:

3.x is still preview on the snapshot date. Hero Passport has no requirement that justifies preview API churn.

CLI rendering policy:

Use plain `Console`/`TextWriter` first. Rich terminal formatting is not a core architecture dependency.

### 2.6 Microsoft.Extensions.Hosting / logging/options — ACCEPT as framework stack

Use the .NET Generic Host and built-in dependency injection/options/logging abstractions where they are already part of the platform/application host.

Rules:

- do not wrap `ILogger<T>` in a custom logging framework;
- do not introduce a generic service locator;
- configuration binding is restricted to a known Hero Passport options object;
- MCP stdout never receives normal logging output.

---

## 3. Accepted test dependencies

### 3.1 xunit.v3 3.2.2 — ACCEPT

```xml
<PackageVersion Include="xunit.v3" Version="3.2.2" />
```

Stable v3 is the baseline. v4 pre-release packages are not used for MVP.

### 3.2 xunit.runner.visualstudio — ACCEPT only if required by chosen IDE/test integration

Keep as a private test asset. If `dotnet test` with Microsoft Testing Platform no longer requires it for the repository workflow, remove it rather than retaining it by habit.

### 3.3 Microsoft.NET.Test.Sdk — CONDITIONAL

Only include if required by the final xUnit/MTP project shape. Do not blindly carry legacy VSTest packages into every test project.

### 3.4 MCP Inspector — DEV/E2E TOOL, not runtime dependency

Use the official MCP Inspector for manual/protocol smoke testing. Pin its dev invocation/version in repository tooling once implementation starts; do not document `@latest` as the reproducible CI path.

---

## 4. Explicitly rejected libraries

These rejections are intentional architecture decisions, not omissions.

### 4.1 MediatR — REJECT

Why it looks attractive:

- command/query dispatch;
- pipeline behaviors;
- popular clean-architecture examples.

Why it is wrong here:

Hero Passport starts with four MCP use cases plus a small CLI. Direct handler/service interfaces are clearer than introducing an in-process message bus. MediatR would add indirection exactly where Codex and maintainers benefit from direct call graphs.

Replace with:

```text
StartQuestHandler
FinishQuestHandler
GetCurrentQuestHandler
GetHeroCardHandler
```

called directly through DI.

Revisit only if cross-cutting pipelines become genuinely numerous and direct composition becomes repetitive.

### 4.2 FluentValidation — REJECT for MVP

Tool/CLI contracts are small. Use:

- JSON Schema constraints at MCP boundary;
- small explicit validators for semantic checks;
- options validation for configuration.

Adding a validation DSL and another set of conventions would not remove meaningful complexity.

### 4.3 AutoMapper — REJECT

Mappings are small and security-sensitive. Explicit mapping makes it obvious which fields cross boundaries and helps prevent accidental source/path/log leakage.

### 4.4 Dapper — REJECT for baseline

There is no demonstrated query where EF projection is insufficient. Two ORM/data-access paradigms would increase cognitive load and transaction coordination risk.

If a future measured dashboard query is problematic, use localized parameterized SQL through EF/ADO.NET before adding another general data layer.

### 4.5 Generic repository framework — REJECT

A generic `IRepository<T>` hides the real application storage needs and often leaks persistence semantics back upward.

Use capability-specific ports such as:

```text
IQuestStore
IHeroStore
IProjectStore
IHeroReadStore
IUnitOfWork / explicit transaction coordinator if needed
```

The interfaces are driven by use cases, not CRUD symmetry.

### 4.6 Polly — REJECT

MVP has no outbound network dependency. Microsoft.Data.Sqlite already handles busy/locked retries up to its command timeout. Adding a general retry framework could accidentally duplicate non-idempotent application operations.

Retry policy belongs at known transient boundaries only.

### 4.7 Serilog / NLog — REJECT for baseline

Built-in `Microsoft.Extensions.Logging` is enough for stderr and optional local file diagnostics. No structured external sink is required.

Revisit only if actual observability requirements exceed the built-in stack.

### 4.8 Spectre.Console — DEFER

It is a good library, but not required for the architecture. Rich CLI output creates another output abstraction and raises the chance of accidental reuse in MCP stdout paths.

MVP CLI should first be correct and scriptable:

```text
human text by default
--json for machine use where meaningful
stderr for diagnostics
```

Spectre can be evaluated later as a presentation-only dependency isolated to non-MCP CLI code.

### 4.9 OpenTelemetry SDK/exporters — DEFER

The application can use normal .NET logging and `System.Diagnostics` seams without shipping an exporter. A local single-user stdio process does not need distributed tracing infrastructure.

MCP 2026-07-28 makes trace propagation relevant for remote deployments, but that is not a reason to ship exporters in local MVP.

### 4.10 Testcontainers — REJECT for SQLite tests

SQLite is embedded. Tests must use a real temporary file database with the same native provider, WAL and PRAGMAs. A container would add startup cost without increasing SQLite fidelity.

### 4.11 Respawn/database reset frameworks — REJECT

Create a fresh per-test/per-fixture temp SQLite database instead. Isolation is cheaper and clearer.

### 4.12 Verify/Snapshooter snapshot frameworks — DEFER

Protocol/tool schema and RPG results need reviewable goldens, but plain checked-in JSON/text fixtures plus explicit assertions are sufficient initially. Avoid introducing snapshot magic until fixture maintenance becomes painful.

### 4.13 FluentAssertions/Shouldly — OPTIONAL/DEFER

xUnit assertions are sufficient. Assertion libraries are developer ergonomics, not architecture requirements.

### 4.14 ArchUnitNET / NetArchTest — DEFER

Architecture invariants can initially be tested with straightforward reflection/project-reference tests. Add a library only when rules become numerous enough that a DSL materially improves maintenance.

### 4.15 BenchmarkDotNet — DEFER

No performance hot path has been demonstrated. For initial performance smoke checks, measure startup/tool latency with simple integration fixtures. Add BenchmarkDotNet only when optimizing a measured algorithm.

### 4.16 Refit/RestSharp/HttpClientFactory extras — REJECT

MVP does not call remote APIs.

### 4.17 SQLite encryption wrappers — REJECT for MVP

Hero Passport intentionally avoids storing code/secrets. OS account/file permissions are the baseline security boundary. Encryption-at-rest introduces key management, native build complexity and portability issues. Revisit only when the data sensitivity model changes.

---

## 5. Framework choices without new packages

### 5.1 TimeProvider

Use .NET `TimeProvider`; do not create `IClock` unless a specific abstraction gap appears.

Tests use a controllable/fake provider.

### 5.2 Guid.CreateVersion7

Use built-in UUIDv7 for generated IDs. No third-party ULID/UUID package is required.

### 5.3 System.Text.Json

Use built-in JSON serialization. No Newtonsoft.Json baseline dependency.

Reasons:

- current .NET integration;
- source-generation option if later needed;
- enough strictness/options for our contracts/config/export.

### 5.4 Options pattern

Use typed options with startup validation. Reject arbitrary dictionary-based configuration for core settings.

### 5.5 CancellationToken

Carry cancellation from MCP/CLI host boundaries into Application operations. Do not manufacture cancellation tokens in Domain.

SQLite execution remains synchronous; cancellation can be checked between bounded operations and before transaction commit. Do not pretend synchronous SQLite calls can be interrupted as true async I/O.

---

## 6. Persistence technology decision: EF Core vs alternatives

| Criterion | EF Core SQLite | Dapper | raw Microsoft.Data.Sqlite |
|---|---:|---:|---:|
| migrations | **excellent** | manual | manual |
| aggregate writes | **good** | good | verbose |
| query projections | **good** | excellent | verbose |
| future Blazor reuse | **excellent** | good | good |
| change tracking complexity | moderate | none | none |
| compile-time mapping visibility | good | manual | manual |
| implementation code volume | **low** | medium | high |
| one-stack simplicity | **best fit** | requires migration story | low-level ownership |

Decision: **EF Core SQLite**.

Guardrails remove its common failure modes:

- no lazy loading;
- no proxies;
- no generic repository;
- no long-lived DbContext;
- no EF entities outside Infrastructure;
- no `EnsureCreated` product path;
- no fake async dogma;
- no client-evaluated queries hidden from review;
- purpose-built projections for reads.

---

## 7. DbContext lifetime decision

Standardize on:

```text
IDbContextFactory<HeroPassportDbContext>
```

inside Infrastructure.

Every command/query creates one context for one unit of work and disposes it immediately.

Why this is stronger than relying on ambient scoped DbContext:

- App stdio/CLI requests are not HTTP request scopes;
- future Blazor server components should not share a context across a circuit;
- explicit lifetime maps directly to SQLite's connection/thread-safety model;
- write transaction boundaries are obvious.

Do not share a DbContext between concurrent operations.

---

## 8. SQLite operational baseline

Connection creation uses `SqliteConnectionStringBuilder`, not string concatenation.

Baseline intent:

```text
Mode = ReadWriteCreate
Cache = Default (not Shared)
Foreign Keys = true explicitly
Pooling = true
Default Timeout = 5 seconds (Hero Passport policy; measured/reviewed before release)
```

Why 5 seconds rather than provider default 30:

A local MCP call hanging for 30 seconds because another process has a stale/long writer is poor agent UX. Hero Passport writes are expected to be milliseconds-long. Five seconds is long enough for normal contention and short enough to return actionable `HP202 database_busy` diagnostics. This number is an application policy and must be validated under concurrency tests before 0.1.0.

Journal/durability:

```text
journal_mode = WAL
synchronous = FULL
```

`FULL` is chosen because progression writes are low frequency and the additional durability across OS crash/power loss is worth more than micro-optimizing commit latency. If benchmarks later show a material problem, `NORMAL` can be considered through an ADR with explicit durability trade-off.

Do not set `Cache=Shared` with WAL; Microsoft documentation discourages that combination.

Do not add aggressive manual checkpointing to every write. SQLite's WAL auto-checkpoint is sufficient initially. Maintenance/checkpoint commands are introduced only when measured database behavior requires them.

---

## 9. Migration strategy

Use EF Core migrations from the first schema.

Important correction:

**Do not implement a custom migration mutex/file lock.** EF Core 9+ already acquires a database-wide migration lock. For SQLite it uses `__EFMigrationsLock`.

Hero Passport responsibilities:

- run/verify migrations through EF-supported paths;
- CI: `dotnet ef migrations has-pending-model-changes`;
- `doctor`: detect migration mismatch and a suspicious abandoned `__EFMigrationsLock` condition;
- document recovery rather than silently deleting migration locks during normal startup.

A stale migration lock recovery action must be explicit because deleting the lock table/row while a real migration is active is unsafe.

---

## 10. NuGet reproducibility and audit policy

Repository baseline:

```text
Directory.Packages.props
packages.lock.json committed
RestoreLockedMode=true in CI/release
NuGet audit enabled
transitive audit enabled
```

.NET 10/NuGet audits transitive packages by default. We still configure audit policy explicitly where practical so future default changes do not silently weaken the pipeline.

Release workflow must fail on known high/critical vulnerabilities and should treat moderate findings as a review gate. Exact warning policy is implemented only after validating NuGet warning IDs/properties against the pinned SDK.

Dependency updates are reviewed as code changes because they can change:

- MCP wire behavior;
- generated JSON Schema;
- native SQLite version;
- migration behavior;
- CLI parsing/output;
- test runner behavior.

---

## 11. Dependency addition checklist

A PR adding any production package must answer:

```text
What concrete code/complexity does it remove?
Why can the BCL/framework not do this adequately?
What is its stable version and maintenance status?
Does it add native assets?
Does it affect trimming/single-file/self-contained publish?
Does it add reflection/dynamic loading?
Does it add configuration/secrets/network access?
How is it tested?
How is it removed/replaced if abandoned?
Does docs/DEPENDENCIES.md need an ADR update?
```

If these answers are weak, do not add the package.

## 12. Current version matrix

| Package/runtime | Baseline | Status on 2026-08-10 |
|---|---:|---|
| .NET SDK | 10.0.302 | stable pinned SDK |
| .NET runtime | 10.0.10 | stable |
| ModelContextProtocol | 2.0.0 | stable, MCP 2026-07-28 |
| EF Core SQLite | 10.0.10 | stable |
| EF Core Design | 10.0.10 | stable dev asset |
| SQLitePCLRaw.bundle_e_sqlite3 | 3.0.5 | stable current major |
| native SQLite | >= 3.53.4 via bundle | verify at runtime/test |
| System.CommandLine | 2.0.10 | stable 2.x; 3.x preview rejected |
| xunit.v3 | 3.2.2 | stable v3; v4 prerelease rejected |

## 13. Primary sources

- MCP C# SDK repository/package guidance: https://github.com/modelcontextprotocol/csharp-sdk
- ModelContextProtocol 2.0.0: https://www.nuget.org/packages/ModelContextProtocol/2.0.0
- EF Core SQLite 10.0.10: https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite/10.0.10
- EF Core SQLite limitations: https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations
- EF Core migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying
- Microsoft.Data.Sqlite async: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async
- Microsoft.Data.Sqlite connection strings: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings
- Microsoft.Data.Sqlite database errors: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/database-errors
- SQLite PRAGMA synchronous: https://www.sqlite.org/pragma.html#pragma_synchronous
- System.CommandLine 2.0.10: https://www.nuget.org/packages/System.CommandLine/2.0.10
- xunit.v3 3.2.2: https://www.nuget.org/packages/xunit.v3/3.2.2
