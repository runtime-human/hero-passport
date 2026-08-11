# Hero Passport — Claude Code Integration

**Status:** Documented / protocol-compatible; RC smoke required before Qualified  
**Official documentation recheck required at RC:** yes  
**Transport:** local stdio

## 1. Current host model

Claude Code officially supports local stdio MCP servers and remote HTTP MCP servers, with configuration scopes and OAuth for remote use. Hero Passport 0.1 uses the local stdio path.

## 2. Preferred local registration

Use Claude Code's native MCP management/configuration according to the current official CLI. Conceptually the launched process must be:

```text
hero-passport mcp --project-root <absolute project path>
```

A current CLI form is typically built around `claude mcp add` plus command/args. Because Claude Code's CLI/config syntax can evolve independently, release documentation must verify the exact current invocation rather than hard-code an untested shell command as a permanent API guarantee.

If the current Claude Code project/local scope guarantees a suitable process working directory, that host-native project binding may replace explicit `--project-root` after smoke verification.

## 3. Required tools

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

The same HP-MCP/2 contract applies; no Claude-specific tool names or request fields.

## 4. Scopes

When choosing between user/project/local MCP configuration, prefer a scope that preserves the intended project binding and does not accidentally reuse one server entry for unrelated repositories.

A globally shared entry without a reliable per-project binding is not considered project-aware Hero Passport operation.

## 5. RC qualification

Record:

```text
Claude Code version
OS
actual MCP add/config syntax
scope used
project binding behavior
exact four tools
start/list/finish/card
restart/recovery
parallel distinct quests
```

Only then promote the tested release to Qualified for Claude Code.

## 6. Remote HTTP/OAuth

Claude Code can consume remote MCP, but Hero Passport 0.1 does not expose its own HTTP endpoint. Future remote deployment uses standard Streamable HTTP/security architecture in `DEPLOYMENT-MODES.md`.

## 7. Ownership

Claude Code owns its MCP config/auth. Hero Passport owns only its local process, local binding arguments and game data. No automatic third-party config mutation in 0.1.
