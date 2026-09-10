# Cursor Integration

**Status:** documented compatibility candidate; release smoke required  
**Architecture:** Hero Passport v3.2.1  
Verified: 2026-09-09 against current official Cursor Agent Skills and MCP documentation

Cursor currently has native Agent Skills support and local STDIO MCP support. This page records those verified host mechanisms; it does **not** promote Cursor to Qualified until the exact supported Cursor build passes Hero Passport host smoke.

## Agent Skill

Cursor automatically discovers project skills from `.agents/skills/` and `.cursor/skills/` (and compatibility locations for other agents). For Hero Passport, use the portable project-level mapping:

```text
.agents/skills/hero-passport
```

Map that directory to the release bundle's canonical `skills/hero-passport/` package. Prefer a symlink where the local platform/workflow supports it so the game/lifecycle policy remains single-source. Do not fork or rewrite Hero Passport rules into Cursor rules.

## MCP

Project-specific Cursor MCP configuration lives at:

```text
.cursor/mcp.json
```

A local release installation can launch Hero Passport as STDIO:

```json
{
  "mcpServers": {
    "hero-passport": {
      "type": "stdio",
      "command": "hero-passport",
      "args": ["mcp", "--project-root", "${workspaceFolder}"]
    }
  }
}
```

`--project-root` is local host launch configuration, not an HP-MCP tool argument. If Cursor already launches the server with the intended project as process cwd, the simpler `hero-passport mcp` form remains valid.

Current official Cursor MCP configuration supports project `.cursor/mcp.json`, local `stdio`, `command`, `args`, and `${workspaceFolder}` interpolation.

## Release smoke before Qualified

Verify on the exact supported Cursor version/OS/date:

```text
Agent discovers hero-passport Skill from project mapping
current HP-MCP/2 v3.2.1 inventory/schema
get_context pre/post setup
bootstrap
explicit-Hero Start
project identity
meaningful-work trigger and factual-question non-trigger
Finish/restart/all-Hero recovery
structured result rendering
HP135/HP136 behavior where practical
host tool-confirmation UX
MCP permanent delete absent
```

Official capability references checked on 2026-09-09:

- Cursor Docs — Agent Skills: https://cursor.com/docs/skills
- Cursor Docs — Model Context Protocol: https://cursor.com/docs/mcp
