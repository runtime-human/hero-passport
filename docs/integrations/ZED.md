# Zed Integration

**Status:** documented compatibility candidate; release smoke required

Configure the current Zed agent/MCP surface, using its official documentation, to launch:

```text
hero-passport mcp
```

Provide correct workspace cwd or `--project-root <path>` in local launch configuration. Local paths never become HP-MCP arguments.

Full ambient UX requires support for the Hero Passport Agent Skill or a faithful equivalent persistent instruction mechanism. Core MCP compatibility alone does not prove Skill-trigger behavior.

Release smoke records Zed version/OS/date and verifies:

```text
11-tool discovery
project identity
onboarding
start/finish/recovery
structured results
confirmation UX
Skill/equivalent behavior
```
