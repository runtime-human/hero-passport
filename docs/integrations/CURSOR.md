# Cursor Integration

**Status:** documented compatibility candidate; release smoke required

Use Cursor’s current official MCP configuration mechanism to launch the local stdio Hero Passport Core:

```text
hero-passport mcp
```

Prefer correct project cwd; use `--project-root <path>` only as local launch configuration when necessary.

For ambient auto-start/auto-finish, map/install the official Hero Passport Agent Skill only through a currently supported Skill/rules/instruction mechanism. Do not copy the game engine into a host rules file.

Before claiming Qualified support, test the exact current Cursor version for:

```text
11 tools
project binding
first-run setup
Skill trigger behavior
Quest completion/recovery
structured result rendering
host confirmation UX
```
