# Hero Passport — Codex Integration

**Status:** Reference qualification target for 0.1.0  
**Documentation verified:** 2026-08-11  
**Transport:** local stdio

## 1. Role

Codex is Hero Passport's first automated qualification host, not a special product mode. It consumes the same HP-MCP/2 contract as every other compatible MCP client.

Required tool inventory:

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

---

## 2. Official Codex MCP model

Current Codex documentation supports local stdio and Streamable HTTP MCP servers. Local Codex surfaces share MCP configuration via `config.toml`; project-level `.codex/config.toml` can scope configuration to a project, and `mcp_servers.<id>.cwd` controls the stdio server working directory.

Codex also exposes `enabled_tools`/`disabled_tools`, startup/tool timeout configuration and server instructions behavior.

Hero Passport uses these native mechanisms instead of modifying Codex internals.

---

## 3. Preferred local configuration

For deterministic project binding, explicitly set the MCP process `cwd` to the project root in machine-local/project configuration:

```toml
[mcp_servers.hero-passport]
command = "hero-passport"
args = ["mcp"]
cwd = "/absolute/path/to/project"
enabled_tools = [
  "hero.start_quest",
  "hero.finish_quest",
  "hero.list_active_quests",
  "hero.get_card",
]
```

On Windows use a valid TOML string/path representation appropriate to the local machine.

Why explicit `cwd`:

- project identity is resolved locally;
- no workspace path enters MCP tool input;
- one MCP server process is bound to one project;
- behavior is independent of whatever working directory launched the outer Codex process.

If managing the server through `codex mcp add`, use Codex's native command for registration and then verify/adjust project binding through supported config. Do not assume a registration command inferred the desired project cwd unless current Codex behavior/documentation proves it.

---

## 4. Portable binding fallback

If a Codex environment cannot conveniently express `cwd`, use Hero Passport startup binding:

```text
command = "hero-passport"
args = ["mcp", "--project-root", "/absolute/path/to/project"]
```

Do not put project path in `hero.start_quest`.

---

## 5. Server instructions

Hero Passport registers concise cross-tool instructions. For Codex, keep the first 512 characters self-contained because official Codex guidance uses that initial region as the essential instruction budget.

Semantics:

```text
start one quest per logical meaningful work item
keep questId
finish the same quest once
several distinct quests may coexist
list active quests when context was lost
never send code/diffs/raw logs/prompts/secrets/env/workspace paths
show compact returned status
```

Project-specific `AGENTS.md` may reinforce usage, but correctness/security cannot depend on a repository instruction file being present.

---

## 6. Tool allow-list

Use `enabled_tools` as defense in depth:

```text
start
finish
list_active
card
```

Hero Passport itself still advertises only these four. A Codex allow-list is not allowed to mask an accidental fifth server tool in tests.

---

## 7. Lifecycle examples

### Meaningful coding task

```text
Codex -> hero.start_quest(coding, goal)
Hero Passport -> questId
Codex performs work
Codex -> hero.finish_quest(questId,...)
Hero Passport -> typed reward + compact text
```

### Parallel/recovered work

If Codex loses a prior `questId` or several agents share the project:

```text
hero.list_active_quests()
```

Then select the quest whose bounded stored goal/type matches the work being continued.

Do not call `start_quest` blindly merely because process context was restarted.

### Same work duplicate start

A duplicate logical start returns the existing open `questId`; this is not an error.

---

## 8. Qualification E2E

0.1 release requires current Codex CLI to pass:

```text
install/register local server
exact four tools visible
project-bound launch
new start
same-task start reuse
second distinct active quest
list active quests
finish one
finish retry
get card
restart Hero Passport process
recover remaining quest
privacy sentinel scenario
```

Record Codex version/OS/date in release evidence.

---

## 9. ChatGPT desktop vs ChatGPT web

Current OpenAI Codex documentation distinguishes local Codex host surfaces (desktop app/CLI/IDE extension sharing configuration) from ChatGPT web's hosted/plugin MCP path.

Do not assume a local `~/.codex/config.toml` entry automatically creates a ChatGPT web integration.

Private web/Responses access can use Secure MCP Tunnel as documented separately in `CHATGPT.md`/`DEPLOYMENT-MODES.md`.

---

## 10. Troubleshooting

Use:

```text
codex mcp list
hero-passport doctor
hero-passport --version
```

Check:

```text
command resolution
project cwd / --project-root
HERO_PASSPORT_HOME if intentionally overridden
DB migrations/SQLite state
exact four tools
stderr diagnostics
```

Never redirect Hero Passport diagnostics to stdio stdout.

---

## 11. Ownership boundary

Codex owns:

```text
config.toml
MCP server enable/disable
allowed tools
Codex auth/model settings
```

Hero Passport owns:

```text
its process arguments
its local config/data
HP-MCP semantics
hero/project game state
```

No `hero-passport codex install-config` mutator is required in 0.1. A later `integration show codex` may print validated guidance without writing Codex files.
