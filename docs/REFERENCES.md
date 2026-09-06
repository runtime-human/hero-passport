# Hero Passport — References

**Verification snapshot:** 2026-09-06

Use current primary/official sources for implementation claims. Repository prior art informs design but never overrides official documentation for the actual stack.

## .NET / Microsoft

- .NET 10 downloads / SDK: https://dotnet.microsoft.com/en-us/download/dotnet/10.0
- .NET support policy: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- `Guid.CreateVersion7`: https://learn.microsoft.com/en-us/dotnet/api/system.guid.createversion7
- Microsoft.Data.Sqlite connection strings: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings
- Microsoft.Data.Sqlite transactions: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions
- `SqliteConnection.BeginTransaction`: https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlite.sqliteconnection.begintransaction
- EF Core SQLite limitations / migrations / `__EFMigrationsLock`: https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations
- SQLite EF Core provider: https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite
- Microsoft.Data.Sqlite package: https://www.nuget.org/packages/Microsoft.Data.Sqlite
- System.CommandLine package: https://www.nuget.org/packages/System.CommandLine

Verified implementation baseline:

```text
.NET SDK 10.0.302 / .NET 10 LTS
Microsoft.EntityFrameworkCore.Sqlite 10.0.10
Microsoft.Data.Sqlite 10.0.10
System.CommandLine 2.0.10
```

Important verified implications:

- `Foreign Keys=True` sends `PRAGMA foreign_keys=1` after open;
- connection string exposes Cache/Foreign Keys/Default Timeout/Pooling but no `Synchronous=Full` keyword;
- EF SQLite migration protection uses `__EFMigrationsLock`, and official docs describe abandoned-lock recovery after unexpected process termination.

## MCP

- MCP final 2026-07-28 announcement: https://blog.modelcontextprotocol.io/posts/2026-07-28/
- MCP Tools specification: https://modelcontextprotocol.io/specification/2026-07-28/server/tools
- SEP-2567 / sessionless explicit state handles: https://modelcontextprotocol.io/seps/2567-sessionless-mcp
- C# SDK repository: https://github.com/modelcontextprotocol/csharp-sdk
- C# SDK releases: https://github.com/modelcontextprotocol/csharp-sdk/releases
- C# SDK package: https://www.nuget.org/packages/ModelContextProtocol
- Previous 2025-11-25 Tools contract: https://modelcontextprotocol.io/specification/2025-11-25/server/tools

Verified current C# SDK baseline:

```text
ModelContextProtocol 2.2.0
released 2026-08-13
```

Important verified implications:

- MCP 2026-07-28 removes protocol session/handshake dependence and recommends explicit ordinary application handles for state across calls;
- tools returning `structuredContent` SHOULD also return serialized JSON in TextContent for backwards compatibility;
- Hero Passport qualifies both `2026-07-28` and `2025-11-25` against the real stdio subprocess using the official C# SDK path;
- the SDK's server primitive collection is dictionary-backed, so HP-MCP/2 applies a narrow official `ListTools` request filter to restore the contract's deterministic tool order without replacing SDK dispatch;
- MRTR/input-required supports server requests for missing input/user confirmation, but Hero Passport deliberately avoids requiring that cross-host capability for permanent delete in 0.1 by keeping delete CLI-only.

## SQLite

- SQLite current/release history: https://sqlite.org/changes.html
- SQLite PRAGMA reference (`synchronous`, `trusted_schema`, `secure_delete`): https://sqlite.org/pragma.html
- SQLite WAL: https://sqlite.org/wal.html
- SQLite 3.51.3 WAL-reset fix: https://sqlite.org/releaselog/3_51_3.html
- SQLitePCLRaw repository: https://github.com/ericsink/SQLitePCL.raw
- SQLitePCLRaw bundle 3.0.5: https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3/3.0.5

Verified baseline:

```text
SQLite current supported floor for Hero Passport: >=3.53.4
SQLite 3.53.4 release: 2026-07-24
SQLitePCLRaw.bundle_e_sqlite3: 3.0.5
```

Important verified implications:

- `trusted_schema` is per-connection;
- WAL + `synchronous=FULL` provides the chosen stronger commit-durability behavior versus NORMAL;
- ordinary delete is not forensic secure erasure; `secure_delete`/VACUUM have separate semantics Hero Passport does not promise in 0.1.

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

- Repository: https://github.com/a2aproject/A2A
- Specification: https://github.com/a2aproject/A2A/blob/main/docs/specification.md
- Proto: https://github.com/a2aproject/A2A/blob/main/specification/a2a.proto

### Temporal

- .NET SDK: https://github.com/temporalio/sdk-dotnet

These sources support separating caller retry identity from server work identity and modeling active-work/finalization conflicts explicitly. Hero Passport does not import their runtimes.

## Local-first / telemetry / gamification comparisons

### Atuin
- https://github.com/atuinsh/atuin

### WakaTime
- https://github.com/wakatime/wakatime-cli
- https://wakatime.com/help/creating-plugin

### Habitica
- https://github.com/HabitRPG/habitica

### NeuroArxiv
- https://github.com/UditAkhourii/neuroarxiv

NeuroArxiv is research-workflow inspiration only: prior art -> isolated mechanism -> comparison -> official-doc verification -> adaptation -> tests.

## Git project identity

- `git rev-parse`: https://git-scm.com/docs/git-rev-parse
- `git worktree`: https://git-scm.com/docs/git-worktree
- `safe.directory`: https://git-scm.com/docs/git-config#Documentation/git-config.txt-safedirectory
- .NET `Directory.ResolveLinkTarget`: https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.resolvelinktarget

## Test framework

- xUnit v3 package: https://www.nuget.org/packages/xunit.v3
- xUnit docs: https://xunit.net/

Verified baseline: `xunit.v3 3.2.2`.

## Review rule

Before changing any pinned version or adopting an external mechanism:

1. re-open current official source;
2. verify release/stability/support status;
3. read breaking/security/reliability notes;
4. compare against actual Hero Passport requirements;
5. update contract/evidence tests;
6. record architecture changes in `DECISION-LOG.md`.
