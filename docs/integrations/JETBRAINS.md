# JetBrains Integration

**Status:** documented compatibility candidate; release smoke required  
**Architecture:** Hero Passport v3.2.1

Use the current official JetBrains AI/MCP mechanism to launch:

```text
hero-passport mcp
```

Bind intended project through host working directory or local `--project-root <path>` launch config.

Ambient Hero Passport UX additionally requires a reusable Skill/instruction mechanism equivalent to `docs/AGENT-SKILL.md`. If current JetBrains surface cannot support that reliably, Core MCP may be compatible but integration is not fully Qualified.

Release qualification verifies current product/version, not generic MCP compatibility:

```text
exact current HP-MCP/2 v3.2.1 inventory/schema
get_context + bootstrap
explicit Hero Start
project binding
Quest Finish/restart/all-Hero recovery
structured result rendering
HP135/HP136 behavior where practical
host tool-confirmation behavior
Skill/equivalent orchestration status
MCP permanent delete absent
```
