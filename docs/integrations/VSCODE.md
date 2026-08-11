# VS Code Integration

**Status:** documented compatibility candidate; release smoke required  
**Architecture:** Hero Passport v3.2.1

Hero Passport expects a current VS Code agent/MCP environment capable of launching local stdio Core and providing intended workspace/project root.

## Core

Configure the host through current official MCP settings to launch:

```text
hero-passport mcp
```

Prefer workspace cwd. Use local `--project-root <path>` only when necessary.

## Skill/orchestration

If the active agent surface supports Agent Skills or equivalent reusable instructions, install/map official `skills/hero-passport/`. Otherwise MCP remains manually usable, but ambient lifecycle is not Qualified until equivalent orchestration is proven.

## Release smoke

Verify on exact supported VS Code/agent extension version:

```text
exact current HP-MCP/2 v3.2.1 inventory/schema
get_context + bootstrap
explicit Hero Start
project binding
meaningful-work auto-start
completion Finish with finishRequestId
restart/all-Hero recovery
structured result rendering
HP135/HP136 behavior where practical
host tool-confirmation behavior
MCP permanent delete absent
```

Do not freeze stale third-party config syntax in architecture; update tested instructions only from current official host documentation.
