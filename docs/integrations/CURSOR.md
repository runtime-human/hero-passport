# Hero Passport — Cursor Integration

**Status:** Documented / protocol-compatible; RC smoke required before Qualified  
**Official documentation recheck required at RC:** yes  
**Transport:** local stdio

## 1. Current host model

Cursor officially documents MCP integration including local stdio and Streamable HTTP, with OAuth for remote server scenarios. Host product/config details evolve quickly, so this page deliberately avoids making unverified assumptions about undocumented working-directory behavior.

## 2. Recommended local shape

Use Cursor's current project/user MCP configuration with the standard Hero Passport command. When no verified first-class MCP cwd primitive is available in the current documented configuration, bind the project explicitly:

```json
{
  "mcpServers": {
    "hero-passport": {
      "command": "hero-passport",
      "args": ["mcp", "--project-root", "/absolute/path/to/project"]
    }
  }
}
```

The exact file location/schema must be verified against the current Cursor documentation/product at release time.

## 3. Required tools

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

No Cursor-specific tool variants.

## 4. RC qualification requirement

Before calling a Hero Passport release Qualified for Cursor, verify with the then-current official docs/product:

```text
Cursor version
OS
actual MCP config location/schema
project binding behavior
exact four tools
start/list/finish/card
restart/recovery
parallel distinct quests
```

If current Cursor provides a reliable project-scoped cwd/variable mechanism, update this page to prefer it and keep `--project-root` as fallback.

## 5. Remote MCP

Cursor can consume remote MCP, but Hero Passport does not ship its own Streamable HTTP listener in 0.1. A future URL deployment follows `DEPLOYMENT-MODES.md`; do not tunnel the local SQLite product onto a public network without the corresponding security architecture.

## 6. Ownership

Cursor owns its MCP configuration and OAuth for remote servers. Hero Passport does not mutate Cursor config automatically.
