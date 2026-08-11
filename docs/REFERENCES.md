# Hero Passport — References

**Verification snapshot:** 2026-08-11

Use primary/official sources for implementation claims. Repository prior art informs design but does not override official documentation for the actual Hero Passport stack.

## .NET / Microsoft

- .NET 10 downloads / SDK: https://dotnet.microsoft.com/en-us/download/dotnet/10.0
- .NET 10 support policy: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- `Guid.CreateVersion7`: https://learn.microsoft.com/en-us/dotnet/api/system.guid.createversion7
- Microsoft.Data.Sqlite transactions: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions
- `SqliteConnection.BeginTransaction`: https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlite.sqliteconnection.begintransaction
- SQLite EF Core provider: https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite
- Microsoft.Data.Sqlite package: https://www.nuget.org/packages/Microsoft.Data.Sqlite
- System.CommandLine package: https://www.nuget.org/packages/System.CommandLine

Verified stable baseline at snapshot:

```text
.NET SDK 10.0.302 / .NET 10 LTS
Microsoft.EntityFrameworkCore.Sqlite 10.0.10
Microsoft.Data.Sqlite 10.0.10
System.CommandLine 2.0.10
```

## MCP

- MCP final 2026-07-28 announcement: https://blog.modelcontextprotocol.io/posts/2026-07-28/
- MCP Tools specification: https://modelcontextprotocol.io/specification/2026-07-28/server/tools
- SEP-2567 explicit state handles: https://modelcontextprotocol.io/seps/2567-sessionless-mcp
- C# SDK repository: https://github.com/modelcontextprotocol/csharp-sdk
- C# SDK releases: https://github.com/modelcontextprotocol/csharp-sdk/releases
- Previous 2025-11-25 Tools contract: https://modelcontextprotocol.io/specification/2025-11-25/server/tools

Verified latest stable C# SDK at snapshot:

```text
ModelContextProtocol 2.1.0
released 2026-08-05
```

## SQLite

- SQLite home/latest release: https://sqlite.org/
- SQLite 3.53.4 release history: https://sqlite.org/changes.html
- SQLite WAL documentation: https://sqlite.org/wal.html
- SQLite 3.51.3 WAL-reset fix release: https://sqlite.org/releaselog/3_51_3.html
- SQLitePCLRaw repository: https://github.com/ericsink/SQLitePCL.raw
- SQLitePCLRaw bundle 3.0.5: https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3/3.0.5

Verified at snapshot:

```text
SQLite latest stable: 3.53.4 (2026-07-24)
SQLitePCLRaw.bundle_e_sqlite3 3.0.5 requires native SQLite >=3.53.4
```

## Agent Skills

- Agent Skills specification: https://agentskills.io/specification
- Agent Skills best practices: https://agentskills.io/skill-creation/best-practices
- Trigger-description guidance: https://agentskills.io/skill-creation/optimizing-descriptions
- Agent Skills repository: https://github.com/agentskills/agentskills
- Anthropic Skills examples: https://github.com/anthropics/skills
- OpenAI Skills documentation: https://help.openai.com/en/articles/20001066
- OpenAI Harness Engineering: https://openai.com/index/harness-engineering/

## Idempotency / work identity prior art

### AWS

- Making retries safe with idempotent APIs: https://aws.amazon.com/builders-library/making-retries-safe-with-idempotent-APIs/
- EC2 ClientToken idempotency: https://docs.aws.amazon.com/ec2/latest/devguide/ec2-api-idempotency.html

### A2A

- A2A repository: https://github.com/a2aproject/A2A
- A2A specification: https://github.com/a2aproject/A2A/blob/main/docs/specification.md
- A2A proto: https://github.com/a2aproject/A2A/blob/main/specification/a2a.proto

### Temporal

- Temporal .NET SDK: https://github.com/temporalio/sdk-dotnet

## Local-first / telemetry / gamification comparisons

### Atuin

- Repository: https://github.com/atuinsh/atuin
- README: https://github.com/atuinsh/atuin/blob/main/README.md

### WakaTime

- CLI repository: https://github.com/wakatime/wakatime-cli
- Plugin architecture / heartbeat documentation: https://wakatime.com/help/creating-plugin

### Habitica

- Repository: https://github.com/HabitRPG/habitica

### NeuroArxiv

- Repository: https://github.com/UditAkhourii/neuroarxiv

Hero Passport uses NeuroArxiv as inspiration for the **research workflow** (prior art -> isolated mechanism extraction -> comparison -> official-doc verification -> adaptation), not as a runtime dependency/code source.

## Git project identity

- `git rev-parse`: https://git-scm.com/docs/git-rev-parse
- `git worktree`: https://git-scm.com/docs/git-worktree
- `safe.directory`: https://git-scm.com/docs/git-config#Documentation/git-config.txt-safedirectory
- .NET `Directory.ResolveLinkTarget`: https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.resolvelinktarget

## Test framework

- xUnit v3 package: https://www.nuget.org/packages/xunit.v3
- xUnit docs: https://xunit.net/

Verified stable baseline at snapshot:

```text
xunit.v3 3.2.2
```

## Review rule

Before changing any pinned version or adopting an externally inspired mechanism:

1. re-open the official/current source;
2. verify release date/stability/support status;
3. read breaking/security/reliability notes;
4. compare against our actual requirements;
5. update the relevant contract/evidence tests;
6. record the decision in `DECISION-LOG.md` when architectural.
