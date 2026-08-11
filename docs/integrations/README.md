# Hero Passport — MCP Host Integrations

**Documentation snapshot:** 2026-08-11

Host integrations are configuration/qualification layers over the same `hero-passport mcp` runtime and HP-MCP/2 contract. They do not define alternate product semantics.

## Support tiers

### Qualified

A specific Hero Passport release has passed the host smoke/E2E checklist with recorded version/environment evidence.

### Documented / protocol-compatible

Current official host documentation describes a compatible stdio/HTTP mechanism and Hero Passport provides a correct configuration pattern, but that host is not yet a release-blocking tested target.

### Unsupported/unknown

Known missing required behavior or no current evidence.

## Initial target matrix

| Host | Local stdio | URL MCP | Project binding option | Initial 0.1 status |
|---|---:|---:|---|---|
| Codex CLI / local Codex host | Yes | Streamable HTTP | `cwd`, project config, `--project-root` | **Qualified target** |
| VS Code | Yes | HTTP | workspace `cwd` / `${workspaceFolder}` | Documented; RC smoke |
| JetBrains AI Assistant | Yes | Streamable HTTP | Working directory + project level | Documented; RC smoke |
| Zed Agent | Yes | remote URL | args/env; use `--project-root` when needed | Documented; RC smoke |
| Cursor | Yes | Streamable HTTP | host config/project practice; `--project-root` fallback | Documented; RC smoke |
| Claude Code | Yes | HTTP | project/local config; `--project-root` fallback | Documented; RC smoke |
| ChatGPT web | not local-config stdio | hosted/plugin/tunnel paths | deployment-specific | Separate profile |

Do not turn this table into a claim that untested hosts are Qualified.

## Portable local command

```text
hero-passport mcp [--project-root <path>] [--hero <selector>]
```

If the host has a project-scoped working-directory field, prefer it. If it does not, pass `--project-root` in args.

Workspace paths remain host/local launch configuration and never enter Hero Passport MCP tool arguments.

## Required host behavior

For 0.1 core functionality a host needs only:

```text
MCP Tools
local stdio process launch
usable structured/text tool results
```

Server instructions improve behavior but are not a correctness/security boundary. Hero Passport does not require Resources, Prompts, Roots, Tasks or Apps.

## Qualification checklist

Record per host/release:

```text
host name/version
OS
Hero Passport version
transport
configuration scope
project binding method
tools/list exact 4 names
start quest
list active quests
finish quest
get card
server restart/recovery
parallel distinct tasks when practical
no stdout/config errors
known limitations
verified timestamp
```

## Pages

- [`CODEX.md`](CODEX.md)
- [`VSCODE.md`](VSCODE.md)
- [`JETBRAINS.md`](JETBRAINS.md)
- [`ZED.md`](ZED.md)
- [`CURSOR.md`](CURSOR.md)
- [`CLAUDE-CODE.md`](CLAUDE-CODE.md)
- [`CHATGPT.md`](CHATGPT.md)

## Configuration ownership

Hero Passport does not silently edit host configuration. Documentation/optional future `integration show` output is preferred to automatic mutation because host formats/scopes change independently.

When a host offers its own `mcp add` command or settings UI, use that native mechanism first.
