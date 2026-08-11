# Hero Passport — References

**Verified snapshot:** 2026-08-11

Use primary/official sources for normative architecture. Repository comparisons remain secondary evidence and never override protocol/framework/provider documentation.

---

## 1. MCP protocol

### MCP `2026-07-28` Tools

- https://modelcontextprotocol.io/specification/2026-07-28/server/tools

Used for:

```text
four-tool wire behavior
input/output schemas
structuredContent
serialized JSON TextContent backward-compatibility SHOULD
tool execution errors via isError=true
explicit state-handle guidance
deterministic tools/list ordering
cache fields
```

### MCP architecture / versioning / transports

- https://modelcontextprotocol.io/specification/2026-07-28/architecture
- https://modelcontextprotocol.io/specification/2026-07-28/basic/versioning
- https://modelcontextprotocol.io/specification/2026-07-28/basic/transports

### MCP 2026 release notes

- https://blog.modelcontextprotocol.io/posts/2026-07-28/
- https://blog.modelcontextprotocol.io/posts/2026-07-28-release-candidate/

Used for stateless-core/deprecation/cache/schema context.

### MCP `2025-11-25` Tools compatibility reference

- https://modelcontextprotocol.io/specification/2025-11-25/server/tools

Used as required initialize-era compatibility qualification path.

---

## 2. Official MCP C# SDK

- https://csharp.sdk.modelcontextprotocol.io/v2/
- https://csharp.sdk.modelcontextprotocol.io/api/ModelContextProtocol.Server.McpServerOptions.html
- https://csharp.sdk.modelcontextprotocol.io/api/ModelContextProtocol.Server.McpServerToolAttribute.html
- https://csharp.sdk.modelcontextprotocol.io/api/ModelContextProtocol.Protocol.ToolAnnotations.html
- https://csharp.sdk.modelcontextprotocol.io/api/ModelContextProtocol.Protocol.CallToolResult.html

Key verified facts:

```text
SDK baseline 2.0.0
ProtocolVersion can remain unset for supported revision negotiation
tool arguments are untrusted
DataAnnotations can influence generated schema but do not enforce runtime validation
CallToolResult supports explicit content/isError/structuredContent
annotation semantics include exact idempotent/readOnly/destructive/openWorld meanings
```

NuGet:

- https://www.nuget.org/packages/ModelContextProtocol/2.0.0

---

## 3. OpenAI / Codex

Official OpenAI sources only:

- https://developers.openai.com/codex/mcp
- https://developers.openai.com/codex/config-reference
- https://learn.chatgpt.com/api/docs/guides/secure-mcp-tunnels

Used for Codex stdio/HTTP integration, `mcp_servers.<id>.cwd`, native `codex mcp` configuration and private Secure MCP Tunnel deployment.

---

## 4. .NET / Microsoft.Data.Sqlite / EF Core

### Microsoft.Data.Sqlite transactions

- https://learn.microsoft.com/dotnet/standard/data/sqlite/transactions

Key behavior:

```text
Serializable is default
only one transaction can have pending changes
deferred read->write upgrade can fail under locking and requires retrying the entire transaction
```

### Exact provider source selected for 0.1

- https://github.com/dotnet/efcore/blob/v10.0.10/src/Microsoft.Data.Sqlite.Core/SqliteConnection.cs
- https://github.com/dotnet/efcore/blob/v10.0.10/src/Microsoft.Data.Sqlite.Core/SqliteTransaction.cs

Key qualification fact:

```text
non-deferred Serializable transaction executes BEGIN IMMEDIATE
```

This behavior is covered by integration tests and re-qualified on provider upgrade.

### SQLite backup API

- https://learn.microsoft.com/dotnet/standard/data/sqlite/backup

Used for consistent live backup; current implementation blocks other writers during backup.

### EF SQLite limitations/migrations

- https://learn.microsoft.com/ef/core/providers/sqlite/limitations

Used for migrations, rebuild limitations and EF migration lock behavior.

### Microsoft.Data.Sqlite async limitations

- https://learn.microsoft.com/dotnet/standard/data/sqlite/async

Used for intentional short synchronous SQLite I/O policy.

### EF package baseline

- https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite/10.0.10
- https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Design/10.0.10

---

## 5. SQLite upstream

### WAL

- https://sqlite.org/wal.html

Used for:

```text
readers/writer concurrency
single-writer model
same-host requirement / network filesystem limitation
autocheckpoint behavior
WAL/SHM persistence/recovery
2026 WAL-reset bug and fixed-version guidance
```

### SQLite 3.51.3 release

- https://sqlite.org/releaselog/3_51_3.html

Used for the normal supported WAL floor because it fixes the WAL-reset corruption race documented upstream.

### Result codes

- https://sqlite.org/rescode.html

Used for HP202..HP208 translation policy.

### Corruption/recovery guidance

- https://sqlite.org/howtocorrupt.html

Used for no-live-File.Copy/no-manual-journal-deletion recovery rules.

### SQLite backup API

- https://sqlite.org/backup.html

Upstream model behind Microsoft.Data.Sqlite backup behavior.

### Native bundle baseline

- https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3/3.0.5

Actual loaded `sqlite_version()` remains the qualification authority.

---

## 6. Git project identity

### `git rev-parse`

- https://git-scm.com/docs/git-rev-parse

Used for:

```text
--path-format=absolute
--git-common-dir
--show-toplevel
--show-prefix
--show-superproject-working-tree
--is-inside-work-tree
--is-bare-repository
```

### Git worktree

- https://git-scm.com/docs/git-worktree

Used for linked worktree private `$GIT_DIR` vs shared `$GIT_COMMON_DIR` semantics.

### Repository layout

- https://git-scm.com/docs/gitrepository-layout

### Git safe-directory security

- https://git-scm.com/docs/git-config#Documentation/git-config.txt-safedirectory

Hero Passport never weakens/auto-writes Git safe-directory configuration.

---

## 7. .NET filesystem / crypto / text

- https://learn.microsoft.com/dotnet/api/system.io.directory.resolvelinktarget?view=net-10.0
- https://learn.microsoft.com/dotnet/api/system.security.cryptography.randomnumbergenerator.getbytes?view=net-10.0
- https://learn.microsoft.com/dotnet/api/system.guid.createversion7?view=net-10.0
- https://learn.microsoft.com/dotnet/api/system.text.rune?view=net-10.0
- https://learn.microsoft.com/dotnet/api/system.timeprovider?view=net-10.0

Used for local link/junction resolution, installation salt, UUIDv7, Unicode scalar-aware validation and deterministic time injection.

---

## 8. JSON / schemas / timestamps

### JSON Schema 2020-12

- https://json-schema.org/draft/2020-12/json-schema-validation

Used for string length/schema/profile semantics.

### RFC 8259 JSON

- https://www.rfc-editor.org/rfc/rfc8259

Used for JSON string/interoperable integer considerations. HP-MCP adopts the safe exact-integer ceiling `2^53-1` for long-lived exposed integers.

### RFC 3339 timestamps

- https://www.rfc-editor.org/rfc/rfc3339

HP-MCP narrows producer output further to `yyyy-MM-ddTHH:mm:ss.fffZ` for deterministic compact snapshots.

---

## 9. CLI/testing dependencies

- https://www.nuget.org/packages/System.CommandLine/2.0.10
- https://www.nuget.org/packages/xunit.v3/3.2.2
- https://xunit.net/docs/getting-started/v3/getting-started

---

## 10. Host integration sources

Use each host's official documentation and re-check during RC because configuration surfaces change independently of HP-MCP.

- VS Code: https://code.visualstudio.com/docs/copilot/chat/mcp-servers
- JetBrains AI Assistant: https://www.jetbrains.com/help/ai-assistant/mcp.html
- Zed: https://zed.dev/docs/ai/mcp
- Cursor: https://docs.cursor.com/context/model-context-protocol
- Claude Code: https://docs.anthropic.com/en/docs/claude-code/mcp

Host pages in `docs/integrations/` record verification status; protocol compatibility is not inferred from a copied config example.

---

## 11. Open repository comparison sources

These are design-pattern evidence, not normative protocol/framework sources:

- GitHub MCP Server — https://github.com/github/github-mcp-server
- Sentry MCP — https://github.com/getsentry/sentry-mcp
- DBHub — https://github.com/bytebase/dbhub
- Context7 — https://github.com/upstash/context7
- Playwright MCP — https://github.com/microsoft/playwright-mcp
- ToolHive — https://github.com/stacklok/toolhive

See `ECOSYSTEM-BENCHMARK.md` for adopted/rejected patterns.
