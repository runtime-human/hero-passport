# VS Code Integration

**Status:** Documented compatible; current Agent Skills/MCP mechanisms verified, release host smoke still required  
**Architecture:** Hero Passport v3.2.1  
**Verified: 2026-09-09** against current official Visual Studio Code Agent Skills and MCP documentation.

## Integration shape

```text
VS Code / GitHub Copilot agent
  -> project Hero Passport Agent Skill
  -> local hero-passport mcp (stdio)
  -> same-host SQLite
```

## Skill installation

Current VS Code project Agent Skills can live under `.github/skills/`, `.claude/skills/`, or `.agents/skills/`. For a VS Code-specific project mapping, place the canonical release Skill at:

```text
<repo>/.github/skills/hero-passport
```

The release source remains `skills/hero-passport/`; copying or mapping it into the host discovery location must not create a separately maintained policy fork.

## MCP setup

Current workspace MCP configuration lives in `.vscode/mcp.json`. Configure Hero Passport as a local STDIO server and bind cwd to the workspace:

```json
{
  "servers": {
    "hero-passport": {
      "type": "stdio",
      "command": "hero-passport",
      "args": ["mcp"],
      "cwd": "${workspaceFolder}"
    }
  }
}
```

If workspace cwd is deliberately not the Hero Passport project boundary, pass `--project-root <path>` in the local process `args`. Never send the path as an HP-MCP tool argument.

VS Code requires trust/approval for changed local MCP configurations; that host UX is part of release smoke evidence, not Core semantics.

Official release-time references:

- https://code.visualstudio.com/docs/agent-customization/agent-skills
- https://code.visualstudio.com/docs/agent-customization/mcp-servers
- https://code.visualstudio.com/docs/agents/reference/mcp-configuration

## Qualification boundary

Before changing this page to `Qualified`, record an actual current VS Code/Copilot host smoke covering Skill discovery, MCP server start/tool discovery, onboarding, explicit-Hero Start, Finish, restart/recovery, structured results and trust/tool-confirmation behavior.
