# Hero Passport — Official Reference Baseline

**Snapshot date:** 2026-08-10  
**Policy:** architecture/tooling claims should be checked against first-party/official documentation before implementation or dependency upgrades.

This is a dated research snapshot, not a substitute for re-checking docs when implementing later.

## 1. .NET / C# / ASP.NET Core

### .NET 10

- .NET 10 downloads: https://dotnet.microsoft.com/en-us/download/dotnet/10.0
- What's new in .NET 10: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview
- .NET and .NET Core support policy: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core

Verified baseline on 2026-08-10:

```text
.NET runtime / ASP.NET Core  10.0.10
.NET SDK                     10.0.302
.NET 10                      LTS
```

### C# 14

- C# 14 overview: https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14

### ASP.NET Core / Blazor

- Blazor docs for ASP.NET Core 10: https://learn.microsoft.com/en-us/aspnet/core/blazor/?view=aspnetcore-10.0

Blazor is a post-MVP dashboard dependency, not part of the initial MCP critical path.

## 2. MCP

### Official C# SDK

- Microsoft .NET Blog — MCP C# SDK v2.0 (2026-07-28): https://devblogs.microsoft.com/dotnet/announcing-v20-of-the-official-mcp-csharp-sdk/
- MCP C# SDK repository: https://github.com/modelcontextprotocol/csharp-sdk
- C# SDK documentation: https://modelcontextprotocol.github.io/csharp-sdk/

Verified baseline:

```text
ModelContextProtocol  2.0.0
MCP revision          2026-07-28
```

Relevant v2 facts:

- v2 implements MCP `2026-07-28` and remains backward compatible with older stable clients/servers except the redesigned Tasks extension;
- `ModelContextProtocol` is the package intended for most stdio/hosted servers;
- `ModelContextProtocol.AspNetCore` is for Streamable HTTP;
- Apps and Tasks are separate opt-in extension packages;
- sampling/roots/legacy MCP logging patterns are deprecated in the new stateless direction.

### Protocol revision

- MCP maintainers — 2026-07-28 specification announcement: https://blog.modelcontextprotocol.io/posts/2026-07-28/
- Specification repository: https://github.com/modelcontextprotocol/modelcontextprotocol
- Tools specification: https://modelcontextprotocol.io/specification/2026-07-28/server/tools

Architecture implications used by Hero Passport:

- stateless application handles are preferable to hidden transport session state;
- list responses/tool catalogs should be deterministic for caching/prompt-cache stability;
- Hero Passport does not need MRTR, Apps or Tasks for the MVP;
- stdio remains the minimal local transport.

## 3. OpenAI Codex

Use only official OpenAI documentation/source for Codex integration decisions.

- Codex MCP: https://learn.chatgpt.com/docs/extend/mcp
- Codex config reference: https://learn.chatgpt.com/docs/config-file/config-reference
- Codex AGENTS.md: https://learn.chatgpt.com/docs/agent-configuration/agents-md
- OpenAI Codex repository: https://github.com/openai/codex
- Current `codex mcp` command implementation: https://github.com/openai/codex/blob/main/codex-rs/cli/src/mcp_cmd.rs

Verified integration facts on 2026-08-10:

```text
codex mcp add <server-name> -- <stdio command>
codex mcp list
```

For stdio servers Codex supports config fields including:

```text
command
args
env
env_vars
cwd
startup_timeout_sec
tool_timeout_sec
enabled_tools
disabled_tools
approval modes
```

Official guidance also says MCP server `instructions` should keep the first 512 characters self-contained.

`codex mcp add` currently creates the stdio server entry with `cwd = None`; explicit `mcp_servers.<id>.cwd` is available in config. Hero Passport therefore treats current-workspace Codex CLI as its first acceptance path and documents explicit local `cwd` configuration for clients/setups that launch the server elsewhere.

Codex project documentation via `AGENTS.md` has a default combined project-doc size limit of 32 KiB, reinforcing Hero Passport's policy to keep root agent instructions short and move detailed architecture into `docs/`.

## 4. CLI

- System.CommandLine overview: https://learn.microsoft.com/en-us/dotnet/standard/commandline/
- NuGet package: https://www.nuget.org/packages/System.CommandLine

Verified stable baseline:

```text
System.CommandLine  2.0.10
```

Do not use 3.x prerelease APIs in the MVP baseline.

## 5. EF Core / Microsoft.Data.Sqlite

- EF Core SQLite provider limitations: https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations
- Microsoft.Data.Sqlite connection strings: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings
- EF Core repository / Microsoft.Data.Sqlite: https://github.com/dotnet/efcore
- EF Core SQLite NuGet: https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite

Verified baseline:

```text
Microsoft.EntityFrameworkCore          10.0.10
Microsoft.EntityFrameworkCore.Sqlite   10.0.10
Microsoft.EntityFrameworkCore.Design   10.0.10
```

Important implementation constraints:

- SQLite does not provide SQL Server-style database-generated `rowversion`; design concurrency around explicit state/constraints/transactions;
- some migrations require table rebuilds;
- `Microsoft.Data.Sqlite` documents that shared cache and WAL should not be mixed as an optimization strategy.

## 6. SQLite / SQLitePCLRaw

### SQLite WAL

- Official SQLite WAL documentation: https://sqlite.org/wal.html
- PRAGMA reference: https://sqlite.org/pragma.html

Relevant facts:

- WAL allows readers and a writer to overlap, but still only one writer at a time;
- WAL is local-host storage, not a network-filesystem design;
- default automatic checkpointing is normally adequate for this workload;
- SQLite documented a rare WAL reset corruption bug affecting versions through 3.51.2; it is fixed in 3.51.3 and later.

### SQLitePCLRaw

- Official repository: https://github.com/ericsink/SQLitePCL.raw
- Current bundle package: https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3
- Current `3.0.5` package: https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3/3.0.5

Verified baseline on 2026-08-10:

```text
SQLitePCLRaw.bundle_e_sqlite3  3.0.5
native SQLite                  >= 3.53.4 (package dependency)
```

SQLitePCLRaw 3.x is the current major line. The maintainer documents that `bundle_e_sqlite3` remains a backward-compatible convenience bundle and pulls the current provider/config/native build packages.

This supersedes the source report's interim `2.1.12` recommendation: `2.1.12` was an important July security/native update, but `3.0.5` is the newer stable package by the architecture snapshot date.

## 7. NuGet reproducibility

- Central Package Management: https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management
- Package lock files / repeatable restore: https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies
- `global.json`: https://learn.microsoft.com/en-us/dotnet/core/tools/global-json

Repository policy derived from these sources:

```text
exact SDK in global.json
Central Package Management
packages.lock.json committed
locked restore in CI/release
```

## 8. Testing

- xUnit.net: https://xunit.net/
- xUnit v3 getting started: https://xunit.net/docs/getting-started/v3/getting-started
- xUnit v3 + Microsoft Testing Platform: https://xunit.net/docs/getting-started/v3/microsoft-testing-platform
- .NET `dotnet test`: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test

Verified stable baseline:

```text
xunit.v3                    3.2.2
xunit.runner.visualstudio   3.1.5 (private compatibility dependency when needed)
```

xUnit v4 is prerelease and is intentionally not an MVP dependency.

## 9. Secondary product analogs

These are **product/UX analogs only**, not normative technical sources:

- WakaTime: https://wakatime.com/features
- Wakapi: https://wakapi.dev/
- ActivityWatch privacy: https://docs.activitywatch.net/en/latest/privacy.html
- Code::Stats API: https://codestats.net/api-docs

Architectural ideas retained:

```text
local/privacy-first state
lightweight metadata instead of source content
simple progression/card visualization
```

Not retained:

```text
cloud-first analytics
continuous activity monitoring
per-keystroke/per-line XP
WakaTime API compatibility
```

GitHub Achievements are explicitly excluded as a product analogue by project requirement.

## 10. Re-verification checklist

Before implementing a milestone that depends on external tooling, re-check at least:

```text
M0: .NET SDK/runtime + System.CommandLine + xUnit stable versions
M5: EF Core patch + SQLitePCLRaw/native SQLite security baseline
M8: MCP C# SDK stable release/spec revision
M9: official Codex MCP/config/AGENTS docs and CLI syntax
M10/release: all package advisories and supported runtime patches
M12/dashboard: current ASP.NET Core/Blazor security/default binding docs
```

When a newer stable dependency exists, do not upgrade blindly: verify compatibility, migration notes and whether the repository's pinned baseline should change through an ADR/document update.
