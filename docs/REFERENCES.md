# Hero Passport — references and research baseline

**Snapshot:** 2026-08-10  
**Policy:** normative technical claims are checked against current primary/official sources. Open-source repositories are used to extract implementation patterns. License is intentionally excluded from architectural ranking for this research task.

## 1. Source priority

When documents disagree:

```text
1. current official protocol/specification/documentation
2. current official SDK/package documentation/source
3. current production open-source repository/source
4. official reference/example repositories
5. older Hero Passport report/docs
6. secondary articles/blogs
```

Secondary sources may point to a topic but do not override current primary documentation.

Every architecture snapshot should store the date because MCP/Codex/.NET package behavior can change quickly.

---

## 2. Model Context Protocol

### MCP 2026-07-28 release

https://blog.modelcontextprotocol.io/posts/2026-07-28/

Used for:

- current protocol revision direction;
- stateless core;
- explicit application handles rather than protocol-session state;
- deterministic/cacheable list behavior;
- extensions/auth/deprecation context.

### MCP Tools specification

https://modelcontextprotocol.io/specification/draft/server/tools

Used for:

- tool naming/schema semantics;
- `inputSchema`/`outputSchema`;
- structured content;
- deterministic list requirements;
- current tool result/error behavior.

### MCP tool annotations guidance

https://blog.modelcontextprotocol.io/posts/2026-03-16-tool-annotations/

Used for current annotation intent/defaults and the rule that annotations are hints, not authorization/security enforcement.

### Official MCP C# SDK

https://github.com/modelcontextprotocol/csharp-sdk

https://csharp.sdk.modelcontextprotocol.io/

Used for:

- package choice;
- stdio host/API model;
- explicit tool registration APIs;
- attributes/annotations;
- SDK-aligned implementation patterns.

### ModelContextProtocol 2.0.0 package

https://www.nuget.org/packages/ModelContextProtocol/2.0.0

Used to pin the stable SDK release baseline on the snapshot date.

### MCP Inspector

https://github.com/modelcontextprotocol/inspector

Used/planned for protocol smoke testing. Automated use must pin a version rather than rely on `latest`.

### Official MCP reference servers

https://github.com/modelcontextprotocol/servers

Used for protocol examples, not as a production architecture template. The repository itself positions these as reference/educational implementations.

---

## 3. OpenAI Codex — official docs only for normative integration

### Codex MCP

https://developers.openai.com/codex/mcp/

Used for:

- current Codex MCP surfaces;
- stdio/HTTP support;
- `codex mcp add/list`;
- server instructions;
- recommendation that first 512 instruction characters be self-contained;
- shared configuration model.

### Codex configuration reference

https://developers.openai.com/codex/config-reference/

Used for:

- `mcp_servers.<id>.command`;
- `args`;
- `cwd`;
- `enabled_tools`/`disabled_tools`;
- startup/tool timeout fields;
- approval-related MCP configuration;
- project/global configuration behavior.

Release docs must recheck these pages before shipping examples.

---

## 4. .NET 10 / C# / hosting

### .NET 10 overview

https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview

Used for .NET 10 baseline/context.

### Generic Host

https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host

Used for process composition/DI/lifetime conventions.

### TimeProvider

https://learn.microsoft.com/en-us/dotnet/standard/datetime/timeprovider-overview

Used instead of inventing an application-wide custom clock abstraction.

### Environment.SpecialFolder

https://learn.microsoft.com/en-us/dotnet/api/system.environment.specialfolder?view=net-10.0

Important distinction:

```text
ApplicationData = roaming user application data
LocalApplicationData = non-roaming local user application data
```

Hero Passport SQLite storage therefore uses LocalApplicationData on Windows.

---

## 5. EF Core / SQLite — official Microsoft docs

### EF Core SQLite limitations

https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations

Used for:

- provider type/schema limitations;
- lack of database-generated concurrency token like SQL Server rowversion;
- migration/rebuild limitations;
- EF migration-lock behavior for SQLite (`__EFMigrationsLock`).

### Applying EF Core migrations

https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying

Used for:

- migration application guidance;
- EF9+ database-wide migration lock;
- deployment caution;
- `dotnet ef migrations has-pending-model-changes` CI strategy where applicable.

### Microsoft.Data.Sqlite async limitations

https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async

Critical implementation fact:

SQLite does not support asynchronous I/O; Microsoft.Data.Sqlite async ADO.NET methods execute synchronously. Hero Passport therefore uses short synchronous DB segments instead of fake async wrappers.

### Microsoft.Data.Sqlite connection strings

https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings

Used for:

- connection builder/options;
- default timeout;
- pooling;
- foreign-key option;
- cache mode;
- warning against shared cache with WAL.

### Microsoft.Data.Sqlite database errors

https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/database-errors

Used for busy/locked retry behavior and error translation design.

### Blazor + EF Core

https://learn.microsoft.com/en-us/aspnet/core/blazor/blazor-ef-core?view=aspnetcore-10.0

Used for future dashboard architecture: short-lived contexts/DbContextFactory rather than sharing scoped DbContext across a long-lived circuit.

### EF Core SQLite package

https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite/10.0.10

Stable package baseline.

---

## 6. SQLite — official upstream

### WAL

https://www.sqlite.org/wal.html

Used for reader/writer behavior, checkpoint semantics and WAL operational considerations.

### PRAGMA synchronous

https://www.sqlite.org/pragma.html#pragma_synchronous

Used for durability choice. Hero Passport chooses WAL + `synchronous=FULL` because progression writes are low frequency and power-loss durability is preferred over peak commit throughput.

### SQLite changes/security baseline

https://www.sqlite.org/changes.html

Used when reviewing native SQLite upgrades/issues. Runtime acceptance still executes `SELECT sqlite_version()`.

---

## 7. Native SQLite packaging

### SQLitePCLRaw.bundle_e_sqlite3 3.0.5

https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3/3.0.5

Directly pinned so the native SQLite baseline is intentional and visible rather than only a broad transitive dependency.

Implementation/release tests verify the actual native version loaded.

---

## 8. CLI/testing packages

### System.CommandLine 2.0.10

https://www.nuget.org/packages/System.CommandLine/2.0.10

Stable 2.x baseline. 3.x preview is not used for MVP.

### xunit.v3 3.2.2

https://www.nuget.org/packages/xunit.v3/3.2.2

Stable v3 baseline. Pre-release major versions are not pulled into MVP without need.

### xUnit v3/Microsoft Testing Platform docs

https://xunit.net/docs/getting-started/v3/microsoft-testing-platform

Used to define the exact test-runner/project setup during foundation implementation.

---

## 9. Package management/security

### NuGet Central Package Management

https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management

Used for `Directory.Packages.props`.

### NuGet package lock files / locked restore

https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies

Used for reproducibility.

### NuGet audit

https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages

Used for direct/transitive vulnerability policy. Exact SDK warning/property behavior must be verified with pinned SDK during Task 1.

---

## 10. Cross-platform filesystem conventions

### XDG Base Directory Specification

https://specifications.freedesktop.org/basedir/latest/

Used for Linux data/config/state locations and directory-permission guidance.

### Apple Application Support

https://developer.apple.com/documentation/foundation/url/applicationsupportdirectory

Used for macOS persistent app support data.

### .NET SpecialFolder

See section 4 for Windows LocalApplicationData.

---

# Production open-source MCP/app architecture benchmark

These repositories are analyzed for patterns, not treated as normative protocol documentation.

## 11. GitHub MCP Server

https://github.com/github/github-mcp-server

Patterns adopted:

- minimize enabled tool inventory;
- strict/fail-closed tool configuration;
- compatibility aliases when tools are renamed;
- security modes override convenience selection;
- dynamic discovery is a scale solution, not a default.

Pattern rejected for Hero Passport now:

- toolsets/dynamic discovery; unnecessary for four tools.

## 12. Sentry MCP

https://github.com/getsentry/sentry-mcp

Patterns adopted:

- optimize for human-in-loop coding agents rather than API completeness;
- separate unit tests from agent evaluations/manual testing;
- tool/workflow UX requires behavioral evals.

Rejected:

- embedded/meta LLM agent for our four deterministic operations.

## 13. DBHub

https://github.com/bytebase/dbhub

Patterns adopted:

- very small token-efficient tool surface;
- progressive disclosure instead of dumping large context;
- local-first development orientation;
- guardrails close to capability boundary.

Applied to Hero Passport:

- no full history MCP tool in MVP;
- future history starts paged/summary-first if exposed to agents.

## 14. Context7

https://github.com/upstash/context7

Pattern adopted:

- MCP and CLI/Skills can coexist; not every capability deserves an always-advertised tool.

Applied:

- MCP owns quest lifecycle;
- CLI owns doctor/export/data maintenance.

## 15. Playwright MCP / Playwright CLI

https://github.com/microsoft/playwright-mcp

Pattern adopted:

- large MCP schema/results consume coding-agent context; CLI+Skills may be superior for operator-like actions.

This is used as a guardrail against MCP API sprawl, not as a reason to remove Hero Passport's small stateful MCP workflow.

## 16. ToolHive

https://github.com/stacklok/toolhive

Patterns adopted:

- versioned configuration;
- explicit security/runtime boundaries;
- architecture docs as maintained product artifacts;
- validation over silent best-effort behavior.

Rejected for current scale:

- gateway/proxy runtime;
- registry/operator/container orchestration;
- OAuth/platform topology;
- semantic tool discovery;
- default OTel/exporter architecture.

---

## 17. Historical Hero Passport input report

The project began from the technical report supplied on 2026-08-10. It remains useful for initial product positioning, rules and stack hypotheses, but this documentation set supersedes it wherever current official docs/repository analysis produced a stronger decision.

Key supersessions include:

```text
interim SQLitePCLRaw 2.1.12 -> stable 3.0.5 baseline
roaming Windows APPDATA path -> LocalApplicationData
per-call MCP locale/outputMode/schemaVersion -> local config/contract
Domain/Application display text -> App presentation boundary
async-looking SQLite calls -> explicit synchronous DB segments
custom migration locking idea -> EF built-in migration lock
assembly scanning convenience -> explicit four-tool registration
unit/integration only -> add agent evaluations
```

---

## 18. Review cadence

Before each RC and any major architecture change, revalidate:

```text
MCP current revision/SDK
Codex MCP/config docs
.NET servicing SDK/runtime
EF Core servicing release
SQLitePCLRaw/native SQLite
System.CommandLine stable line
xUnit stable line
NuGet vulnerabilities
production MCP repo patterns if tool surface/remote needs changed
```

A dated architecture without a revalidation trigger becomes stale by design; this document makes revalidation explicit.
