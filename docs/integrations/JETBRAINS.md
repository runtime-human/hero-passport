# JetBrains Integration

**Status:** Documented Core-compatible candidate; native Hero Passport Skill orchestration is not yet qualified  
**Architecture:** Hero Passport v3.2.1  
**Verified: 2026-09-09** against current JetBrains AI Assistant 2026.2 MCP documentation.

Current JetBrains AI Assistant supports local MCP servers over **STDIO** and can scope a server to the current project.

## MCP setup

Open:

```text
Settings -> Tools -> AI Assistant -> Model Context Protocol (MCP)
```

Add a local server using the current JSON shape:

```json
{
  "mcpServers": {
    "hero-passport": {
      "command": "hero-passport",
      "args": ["mcp"]
    }
  }
}
```

Set the server working directory to the intended project. If that cannot represent the intended Hero Passport project boundary, pass `--project-root <path>` as a local launch argument. Never send a filesystem path as an HP-MCP tool argument.

Official release-time reference:

- https://www.jetbrains.com/help/ai-assistant/mcp.html

## Skill/orchestration boundary

This verification proves current JetBrains local MCP capability only. It does not establish a current portable Agent Skills installation surface equivalent to the canonical `skills/hero-passport/` package.

Therefore JetBrains must not be labeled fully `Qualified` until a current host workflow is demonstrated that preserves the Hero Passport Skill lifecycle semantics (or an explicitly supported equivalent) without duplicating game/reward rules.

A future host smoke must cover tool discovery, get_context/bootstrap, explicit-Hero Start, Finish, restart/recovery, structured results, HP135/HP136 where practical, host confirmation behavior and the ambient orchestration mechanism actually used.
