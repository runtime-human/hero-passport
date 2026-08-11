# Hero Passport — Zed Integration

**Status:** Documented / protocol-compatible; RC smoke required before Qualified  
**Documentation verified:** 2026-08-11  
**Transport:** local command/stdin-stdout

## 1. Current host model

Zed supports MCP context servers through `context_servers`, including local command/args/env configuration and remote URL-based servers. Zed also forwards configured MCP capabilities to supported external agents through its agent architecture.

Hero Passport remains a standard MCP server; it does not implement a Zed extension runtime or ACP agent.

## 2. Project binding

Unlike hosts with a first-class MCP `cwd` field in the documented config shape, portable Hero Passport configuration should use the explicit startup binding when needed:

```json
{
  "context_servers": {
    "hero-passport": {
      "command": {
        "path": "hero-passport",
        "args": ["mcp", "--project-root", "/absolute/path/to/project"],
        "env": {}
      }
    }
  }
}
```

Use the exact current Zed schema/UI syntax when configuring the released product; the architectural requirement is the same command and project-bound launch.

If future Zed configuration exposes a stable project-root variable/working-directory primitive, prefer that over an absolute hardcoded argument.

## 3. Required MCP capability

Hero Passport requires Tools only:

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

Zed's support for Prompts or external-agent ACP integration is optional and does not alter the HP-MCP contract.

## 4. RC smoke

Record:

```text
Zed version
OS
context_servers configuration
project-root binding
exact four tools
start/list/finish/card
restart/recovery
parallel distinct quests
external-agent forwarding path if claimed
```

## 5. Distribution direction

Do not build a custom Zed-specific Hero Passport server extension merely for installation. Zed's ecosystem is moving toward standard MCP distribution/Registry mechanisms; Hero Passport should keep one standard package/runtime.

## 6. Security

Absolute project paths stay in local Zed settings/startup args. They are never sent through Hero Passport tools or stored as the canonical project identity.
