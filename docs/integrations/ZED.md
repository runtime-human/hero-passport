# Zed Integration

**Status:** Documented compatible candidate; current native Skill/MCP mechanisms verified, release host smoke still required  
**Architecture:** Hero Passport v3.2.1  
**Verified: 2026-09-09** against current official Zed Skills and MCP documentation.

## Integration shape

```text
Zed Agent
  -> project-local Hero Passport Skill
  -> Zed local MCP server
  -> same-host SQLite
```

## Skill installation

Current Zed project-local Skills live under `<worktree>/.agents/skills/`. Map the canonical Hero Passport release Skill as:

```text
<worktree>/.agents/skills/hero-passport -> <hero-passport-release>/skills/hero-passport
```

Zed documents `.agents/skills` for project-local Skills and permits a symlink when the Skill lives elsewhere. The canonical release package remains the only lifecycle/game-policy authority.

## MCP setup

Current Zed local custom MCP servers are configured under `context_servers`. A minimal local configuration is:

```json
{
  "context_servers": {
    "hero-passport": {
      "command": "hero-passport",
      "args": ["mcp"],
      "env": {}
    }
  }
}
```

Launch Zed for the intended worktree/project boundary, or pass `--project-root <path>` as a local process argument when needed. Local paths are never HP-MCP tool arguments.

Zed tool permissions can require confirmation for MCP calls and Skills. Record the actual permission behavior in host smoke evidence rather than altering Core semantics.

Official release-time references:

- https://zed.dev/docs/ai/skills
- https://zed.dev/docs/ai/mcp
- https://zed.dev/docs/ai/tool-permissions

## Qualification boundary

Before changing this page to `Qualified`, record a current Zed host smoke covering Skill discovery, MCP active status/tool discovery, onboarding, explicit-Hero Start, Finish, restart/recovery, structured results and confirmation behavior.
