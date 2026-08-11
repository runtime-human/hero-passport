# VS Code Integration

**Status:** documented compatibility candidate; release smoke required

Hero Passport expects a current VS Code agent/MCP environment capable of launching a local stdio MCP server and providing the intended workspace/project root.

## Core

Configure the host through its current official MCP settings to launch:

```text
hero-passport mcp
```

Prefer workspace cwd. Use local launch argument `--project-root <path>` only when necessary.

## Skill/orchestration

If the active VS Code agent surface supports Agent Skills or an equivalent reusable instruction package, install/map the official `skills/hero-passport/` lifecycle. Otherwise MCP remains manually usable, but ambient auto-start/auto-finish is not considered Qualified until equivalent orchestration is proven.

## Release smoke

Verify on the exact supported VS Code/agent extension version:

```text
11 tools
project binding
first-run setup
meaningful-work auto-start
completion auto-finish
restart recovery
structured result rendering
host confirmation behavior
```

Do not copy stale third-party configuration syntax into Hero Passport architecture; update tested setup instructions only from current official host documentation.
