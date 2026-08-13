# Cursor Integration

**Status:** documented compatibility candidate; release smoke required  
**Architecture:** Hero Passport v3.2.1

Use Cursor’s current official MCP configuration mechanism to launch local stdio Core:

```text
hero-passport mcp
```

Prefer correct project cwd; use `--project-root <path>` only as local launch configuration when necessary.

For ambient UX, map/install official Agent Skill only through a currently supported Skill/rules/instruction mechanism. Do not copy game rules into host rules.

Before claiming Qualified support on the exact Cursor version, verify:

```text
exact current HP-MCP/2 v3.2.1 inventory/schema
get_context + bootstrap
explicit Hero Start
project binding
Skill trigger behavior
Finish/restart/all-Hero recovery
structured result rendering
HP135/HP136 behavior where practical
host tool-confirmation UX
MCP permanent delete absent
```
