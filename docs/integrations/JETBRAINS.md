# JetBrains Integration

**Status:** documented compatibility candidate; release smoke required

Use the current official JetBrains AI/MCP configuration mechanism to launch the local stdio command:

```text
hero-passport mcp
```

Bind the intended project through host working-directory support or local `--project-root <path>` launch configuration.

Ambient Hero Passport UX additionally requires a reusable Skill/instruction mechanism equivalent to `docs/AGENT-SKILL.md`. If the current JetBrains surface does not support that reliably, Core MCP may still be compatible but the integration is not fully Qualified.

Release qualification must verify the current product/version rather than infer support from generic MCP compatibility:

```text
11 tools
project binding
first-run setup
Quest start/finish/recovery
structured result rendering
host confirmation behavior
Skill/equivalent orchestration status
```
