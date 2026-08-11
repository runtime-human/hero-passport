# Hero Passport — JetBrains AI Assistant Integration

**Status:** Documented / protocol-compatible; RC smoke required before Qualified  
**Documentation verified:** 2026-08-11  
**Transport:** STDIO

## 1. Current official host model

JetBrains AI Assistant 2026.2 documentation supports STDIO and Streamable HTTP MCP servers, retains SSE for legacy compatibility, and exposes explicit **Working directory** plus project/global server level.

Hero Passport uses STDIO for 0.1.0 and does not implement new legacy SSE.

## 2. Configuration

Use the JetBrains MCP settings UI or its JSON import form to configure the command:

```json
{
  "mcpServers": {
    "hero-passport": {
      "command": "hero-passport",
      "args": ["mcp"]
    }
  }
}
```

Then set:

```text
Working directory = project root
Server level = Project
```

Use the current JetBrains UI/schema as the source of truth if the product changes how Working directory is represented.

If Working directory cannot be set reliably, pass:

```text
args = ["mcp", "--project-root", "<absolute project path>"]
```

## 3. Junie/external agents

JetBrains can expose configured MCP servers to agent experiences such as Junie. Hero Passport remains the same MCP server; do not add a `JunieAdapter` or ACP-specific Hero Passport runtime.

## 4. Required tools

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

## 5. RC smoke

Record:

```text
IDE/product version
AI Assistant version where separately relevant
OS
Project-level server configuration
Working directory behavior
exact four tools
start/list/finish/card
parallel distinct quests
restart/recovery
```

Only then promote the release from Documented to Qualified for the tested JetBrains path.

## 6. Security/config ownership

Project path stays in JetBrains launch configuration or `--project-root`; never MCP arguments.

JetBrains owns its MCP settings. Hero Passport does not edit IDE settings automatically.
