# Hero Passport — Codex Integration

**Status:** Accepted MVP integration design  
**Verified:** 2026-08-10 against current official OpenAI Codex documentation/source

## 1. Supported first path

The first release-quality integration target is **local Codex CLI + local stdio MCP server**.

Hero Passport does not require a network server, OAuth or an OpenAI API key. Codex launches the local `hero-passport mcp` process and calls its tools over stdio.

## 2. Prerequisites

After Hero Passport is packaged/installed:

```bash
hero-passport init
hero-passport doctor
```

The command `hero-passport` must be resolvable from the environment in which Codex starts MCP processes.

## 3. Register with Codex

Current official Codex CLI syntax:

```bash
codex mcp add hero-passport -- hero-passport mcp
codex mcp list
```

`codex mcp add` is the preferred installation path. Hero Passport must **not** rewrite `~/.codex/config.toml` automatically.

The resulting logical config is equivalent to:

```toml
[mcp_servers.hero-passport]
command = "hero-passport"
args = ["mcp"]
```

Exact config shape remains owned by Codex; use `codex mcp` commands when possible.

## 4. Working directory and project identity

Hero Passport `projectId = "auto"` resolves a project from the MCP server process's local working context. The MCP payload intentionally does not send a full workspace path.

Current Codex implementation creates an `mcp add` stdio entry with `cwd = None`. Codex also supports:

```toml
[mcp_servers.hero-passport]
command = "hero-passport"
args = ["mcp"]
cwd = "/local/path/to/project"
```

Use explicit `cwd` **only** when the particular Codex client/setup launches Hero Passport outside the intended workspace. This path remains local Codex configuration; Hero Passport neither returns it to the model nor persists it in cleartext.

### MVP acceptance assumption

For the main acceptance test:

```text
1. open terminal in the repository/workspace;
2. launch Codex CLI there;
3. Codex launches Hero Passport locally;
4. Hero Passport detects the Git root/current directory;
5. project fingerprint/display name resolve correctly.
```

Codex desktop/IDE client behavior must be tested separately before claiming equal support because process working-directory behavior can differ by host/version.

## 5. Recommended repository `AGENTS.md` snippet for consumers

A consuming project that wants automatic Hero Passport usage can add:

```md
## Hero Passport

For meaningful coding, review, debugging, documentation, research, or planning tasks:
1. Call `hero.start_quest` once near the beginning with a concise goal and appropriate quest type.
2. Work normally; do not call Hero Passport for every step/file/command.
3. When the task is actually complete or blocked, call `hero.finish_quest` once with a concise semantic summary, result, quality counters, and up to three canonical/recognizable skills.
4. Show only the returned `displayText` in a final `Hero Passport` section. Do not dump the structured JSON.

Never send Hero Passport source code, diffs, file contents, raw terminal/build/test logs, secrets, environment variables, or full prompts/chat history.
```

Keep this compact. Current Codex project instructions have a finite combined size budget; architecture details belong in Hero Passport's own `docs/`, not every consuming repository.

## 6. Hero Passport server instructions

The MCP server should publish concise cross-tool instructions whose first 512 characters are self-contained, matching current Codex guidance:

```text
Hero Passport tracks local RPG progress for meaningful agent work. Call hero.start_quest once at the start and hero.finish_quest once after the work. Do not send source code, diffs, raw logs, secrets, environment variables, file contents, or full prompts/chat history. Show only displayText from Hero Passport results; never dump raw structured JSON. Use current_quest only for recovery and get_card only when status is requested.
```

Do not duplicate the whole product manual in MCP instructions.

## 7. Recommended tool approvals

Hero Passport writes only its own local RPG database. It does not edit the workspace or run shell commands through MCP tools.

Current Codex supports server-level and per-tool approval configuration. The integration docs should initially rely on Codex defaults; advanced users may configure approvals according to their policy.

Do not instruct users to disable the Codex sandbox or bypass approvals globally just to use Hero Passport.

## 8. Expected agent lifecycle

### Start

Agent call conceptually:

```json
{
  "schemaVersion": "1.0",
  "heroId": "auto",
  "projectId": "auto",
  "questType": "coding",
  "goal": "Implement reward calculation",
  "host": {
    "name": "codex",
    "type": "coding-agent"
  },
  "outputMode": "compact",
  "locale": "ru"
}
```

Hero Passport returns an explicit `questId`. The model threads that application handle to `finish_quest`; no hidden MCP session state is required.

### Work

No Hero Passport calls are needed during ordinary edits/tests/review.

### Finish

```json
{
  "schemaVersion": "1.0",
  "questId": "<returned questId>",
  "result": "success",
  "summary": "Implemented deterministic reward calculation and verified it with focused xUnit tests.",
  "metrics": {
    "testsMentioned": true,
    "scopeViolations": 0,
    "userCorrections": 0,
    "buildStatus": "passed",
    "testsStatus": "passed"
  },
  "skillsUsed": ["coding", "scope_control", "testing_awareness"],
  "outputMode": "compact",
  "locale": "ru"
}
```

Then render only `displayText`.

## 9. Recovery behavior

If the agent loses `questId` after compaction/restart:

```text
hero.current_quest
```

is the recovery path. It should not poll this tool routinely.

If no open quest exists, the agent may start one if the task is still meaningful and active; do not synthesize a completed historical quest after the work merely to farm XP.

## 10. Tool selection / prompt-cache discipline

Hero Passport keeps:

- exactly four tools;
- fixed registration order;
- compact descriptions;
- stable names;
- bounded schemas;
- no dynamic tool creation;
- no per-step telemetry tool.

When adding a future tool, test whether Codex can already satisfy the experience with the four existing tools/read output before expanding the tool catalog.

## 11. Manual Codex acceptance test

From a clean isolated user data directory/tool installation:

1. `hero-passport init`.
2. `hero-passport doctor` is green.
3. `codex mcp add hero-passport -- hero-passport mcp`.
4. `codex mcp list` shows the server enabled.
5. Start Codex in a Git repository.
6. Ask for a small meaningful coding change and require Hero Passport lifecycle through consumer instructions.
7. Confirm `hero.start_quest` is called once.
8. Confirm no Hero Passport calls occur for each file/test command.
9. Confirm `hero.finish_quest` is called after verification.
10. Confirm final reply shows only the human `displayText`, not raw JSON.
11. Retry `finish_quest` with the same quest ID; verify no extra XP event.
12. Call `hero.get_card`; verify totals match.
13. Restart Codex/Hero Passport and verify state persists.
14. Run from a second repository; verify project stats separate.
15. Inspect export/DB/logs for the privacy invariants.

## 12. Troubleshooting design

### Server not visible

Check:

```bash
codex mcp list
hero-passport --version
hero-passport doctor
```

Then inspect Codex's current official MCP config/docs, not copied stale configuration examples.

### Wrong project is resolved

Check the MCP server process working directory. If that Codex host does not launch it from the desired workspace, use an explicit local `mcp_servers.hero-passport.cwd` for that project/setup until a better stable host workspace signal exists.

Do not solve this by putting the full path into model-visible tool arguments.

### MCP startup failure

Hero Passport diagnostics belong on stderr. Verify there is no startup banner/log on stdout and increase Codex MCP startup timeout only if measurements demonstrate the need; the product should normally start quickly.

### Agent does not call tools

Keep server instructions and consuming `AGENTS.md` imperative and concise. Do not add more tools as a workaround for weak instructions.

## 13. Claims we do not make for 0.1.0

Before explicit qualification, do not claim:

- every Codex desktop/IDE build resolves working directories identically to CLI;
- Codex cloud can run the local stdio server;
- non-interactive Codex execution needs no approval-policy consideration;
- Hero Passport modifies Codex config safely on the user's behalf.

The integration is deliberately based on the official local stdio/config surfaces that can be tested and controlled.
