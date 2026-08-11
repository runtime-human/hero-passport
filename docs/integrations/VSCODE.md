# Hero Passport — VS Code Integration

**Status:** Documented / protocol-compatible; RC smoke required before Qualified  
**Documentation verified:** 2026-08-11  
**Transport:** local stdio

## 1. Recommended configuration

VS Code supports MCP configuration in workspace/user `mcp.json` and provides a stdio `cwd` setting. For a project-scoped Hero Passport server, prefer a workspace config such as:

```json
{
  "servers": {
    "hero-passport": {
      "type": "stdio",
      "command": "hero-passport",
      "args": ["mcp"],
      "cwd": "${workspaceFolder}"
    }
  }
}
```

This keeps project path in host launch configuration rather than MCP tool arguments.

## 2. Required tools

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

Hero Passport requires only MCP Tools for the core lifecycle. VS Code support for additional MCP features does not change this baseline.

## 3. Scope

Workspace-level configuration is preferred for project identity. User-global configuration without a stable project cwd must use an explicit Hero Passport `--project-root` binding or is not considered project-aware.

## 4. RC smoke

Before marking a Hero Passport release Qualified on VS Code, record:

```text
VS Code version
OS
configuration scope
${workspaceFolder}/cwd result
exact four tools
start/list/finish/card
server restart/recovery
parallel distinct quest behavior
```

## 5. Security

Do not add workspace path to MCP tool inputs. VS Code sandbox/trust features are host controls and do not replace Hero Passport's own narrow schema/privacy rules.

## 6. Ownership

VS Code owns `mcp.json` and host trust prompts. Hero Passport does not mutate this file automatically in 0.1.
