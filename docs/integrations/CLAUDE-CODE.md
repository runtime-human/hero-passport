# Claude Code Integration

**Status:** Documented compatible; current native Skill/MCP mechanisms verified, release host smoke still required  
**Architecture:** Hero Passport v3.2.1  
**Verified: 2026-09-09** against current official Claude Code Skills and MCP documentation.

## Integration shape

```text
Claude Code
  -> repo-local Hero Passport Agent Skill
  -> local hero-passport mcp (stdio)
  -> same-host SQLite
```

Hero Passport ships one canonical Skill source in the release bundle:

```text
skills/hero-passport/
```

Do not fork reward rules or lifecycle semantics per host.

## Skill installation

Current Claude Code project Skills live under `.claude/skills/<skill-name>/SKILL.md`. Claude Code follows a project Skill directory symlink.

Map the canonical release Skill into the intended project, for example:

```text
<repo>/.claude/skills/hero-passport -> <hero-passport-release>/skills/hero-passport
```

A byte-for-byte copy is acceptable where symlinks are unsuitable, but the canonical release package remains the policy authority.

## MCP setup

Current Claude Code supports local STDIO MCP servers through the CLI. For a project-scoped Hero Passport server:

```text
claude mcp add --transport stdio --scope project hero-passport -- hero-passport mcp
claude mcp list
```

Project-scoped servers are represented in the repository `.mcp.json`. If the host launch directory cannot reliably represent the intended Hero Passport project boundary, pass `--project-root <path>` as a local process argument rather than an HP-MCP tool argument.

Official release-time references:

- https://code.claude.com/docs/en/skills
- https://code.claude.com/docs/en/mcp

## Qualification boundary

Before changing this page to `Qualified`, record an actual current Claude Code host smoke covering Skill discovery, MCP connection/tool discovery, onboarding, explicit-Hero Start, Finish, restart/recovery, structured results and relevant confirmation/trust behavior.

The packaged Core E2E does not by itself prove Claude Code host behavior.
