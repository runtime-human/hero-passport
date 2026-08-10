# Hero Passport — Codex integration

**Status:** Accepted primary-agent integration  
**Snapshot:** 2026-08-10  
**Primary target:** current OpenAI Codex CLI + shared MCP configuration model

## 1. Principle

Hero Passport is Codex-first, but must not hard-code Codex internals into Domain/Application.

Codex integration is one host adapter/configuration contract:

```text
Codex stdio MCP
  -> HeroPassport.App/Mcp
  -> Application
  -> Domain + Infrastructure
```

The product remains usable by another conforming MCP client if that client supports the required tool semantics.

---

## 2. Current official Codex MCP model

Current Codex documentation supports MCP in:

- Codex CLI;
- Codex IDE extension;
- ChatGPT desktop/Codex integrations that share the configuration model where documented.

Codex supports local stdio and Streamable HTTP servers. Hero Passport chooses **stdio only** for MVP.

Codex configuration lives in:

```text
~/.codex/config.toml
```

and trusted project-local:

```text
.codex/config.toml
```

where supported by current Codex configuration rules.

Hero Passport does **not** own or rewrite these files.

---

## 3. Installation/registration

Preferred user path:

```bash
codex mcp add hero-passport -- hero-passport mcp
codex mcp list
```

This keeps server registration under Codex's own supported CLI.

Hero Passport may provide documentation/diagnostics such as:

```text
hero-passport doctor
hero-passport data path
```

but not an MVP command that edits Codex TOML.

Why:

- OpenAI owns config schema/evolution;
- avoids partial/TOML mutation bugs;
- avoids overwriting unrelated user settings;
- removes duplicate compatibility code;
- `codex mcp` already solves registration.

---

## 4. Recommended explicit project configuration

When a workspace-specific server cwd is required, Codex exposes `mcp_servers.<id>.cwd`.

Example:

```toml
[mcp_servers.hero-passport]
command = "hero-passport"
args = ["mcp"]
cwd = "/absolute/local/project/path"
enabled = true
enabled_tools = [
  "hero.start_quest",
  "hero.finish_quest",
  "hero.current_quest",
  "hero.get_card"
]
```

The path lives in **Codex local configuration/process launch**, not in Hero Passport MCP request payload or SQLite.

The exact config snippet in user-facing docs must be revalidated against the current official Codex config reference before each release because OpenAI can evolve keys/defaults.

---

## 5. `enabled_tools` as defense-in-depth

Hero Passport already advertises only four tools. Codex's `enabled_tools` can still be documented as an explicit host-side allow-list.

Benefits:

- config clearly communicates expected inventory;
- accidental future tool exposure is less likely to reach Codex before review;
- easier diagnostic comparison between expected and host-enabled tools.

This is defense-in-depth only. Hero Passport's own explicit registration remains authoritative.

Do not depend on `disabled_tools`/host filtering to hide accidental tools from other MCP clients.

---

## 6. Server instructions

Codex supports MCP server instructions and recommends concise cross-tool workflow constraints there. Current docs note that the first 512 characters should be self-contained because client handling can truncate/limit instructions.

Hero Passport instructions must fit essential behavior immediately:

```text
Use Hero Passport for meaningful coding, debugging, review, planning, research, or documentation work. Start one quest before work, keep questId, finish it once when done. Never send code, diffs, raw logs, prompts, secrets, environment values, or workspace paths. Show returned displayText briefly in the final answer.
```

Then optional remainder may clarify:

- tiny factual questions do not require quests;
- use `current_quest` to recover after context/restart;
- use `get_card` only when useful/requested;
- do not print raw structured result unless user asks.

Server instructions guide behavior; they are not access control.

---

## 7. AGENTS.md relationship

Repository/project AGENTS guidance can strengthen intent for agents operating in that project, but it should remain short.

Recommended project snippet:

```md
## Hero Passport

For meaningful coding, debugging, review, planning, research, or documentation work:
- start one Hero Passport quest before the work;
- keep the returned `questId`;
- finish that quest once when done;
- show the returned compact `displayText` briefly.

Never send source code, diffs, raw logs, prompts, secrets, environment variables, or workspace paths to Hero Passport.
```

Do not paste the full architecture/roadmap into AGENTS.md. Tool selection becomes worse when every turn carries implementation history that is irrelevant to the current task.

---

## 8. Meaningful-task policy

The desired behavior is not “call Hero Passport on every prompt”.

Expected quest examples:

```text
implement feature
fix/debug bug
perform code review
write/refactor meaningful documentation
research architecture/technology
produce implementation plan
perform substantial maintenance
```

Usually no quest:

```text
what does this word mean?
show current time
small factual lookup
single-line clarification
casual conversation
```

This distinction is verified with agent evals rather than encoded as a brittle keyword classifier in Hero Passport.

Hero Passport server never decides whether Codex *should* have started; it only validates calls it receives.

---

## 9. CWD/project identity behavior

Normal project resolution:

```text
server process cwd
 -> find Git root upward
 -> use Git root identity if found
 -> else normalized cwd fallback
```

Acceptance tests cover:

1. Codex CLI invoked from project and server inheriting usable cwd;
2. explicit `mcp_servers.hero-passport.cwd`;
3. no Git repo fallback;
4. restart preserves same fingerprint/project.

Do not assume every Codex surface launches stdio with exactly the same cwd until tested. Support claims are tied to an actual acceptance matrix.

---

## 10. Timeout behavior

Codex exposes MCP startup/tool timeout configuration. Hero Passport's normal calls should be far below those limits.

Product target:

```text
start/get/current: local milliseconds-scale warm path
finish: local milliseconds/low tens of ms under normal DB state
```

These are expectations, not fabricated release SLAs; release smoke measurements will establish actual numbers.

Hero Passport DB busy policy is 5 seconds. A prolonged SQLite writer should produce actionable `HP202 database_busy` before an extremely long host timeout makes Codex appear hung.

Do not increase Codex tool timeout to hide slow/migration-corrupt application behavior.

---

## 11. Approval behavior

Codex configuration can have approval modes/policies for MCP tools. Hero Passport annotations accurately mark read-only/idempotent/open-world semantics so host UX can make informed choices.

But:

- annotations do not grant permission;
- Hero Passport does not assume writes are auto-approved;
- start/finish must remain retry-safe even if the client repeats after approval/network/process uncertainty.

No destructive MCP tools in MVP.

---

## 12. Final response UX

Desired agent output after finish:

```text
[normal task answer]

Hero Passport: ✨ +95 XP · Nova ур.1 · XP 95/100 · Доверие 51 · Риск 19
```

Do not dump raw tool JSON by default.

Do not repeat:

```text
statusText + displayText + field-by-field reward object
```

The structured object exists for machine/tool continuation; `displayText` is the compact human representation.

---

## 13. Recovery behavior

If Codex loses conversational context or MCP server restarts:

```text
hero.current_quest
```

returns the locally resolved active quest.

If the agent still has `questId`, it can finish explicitly.

Because state is SQLite-backed and not MCP-session-backed, server restart does not invalidate an open quest.

Do not introduce an in-memory “current session” cache as correctness state.

---

## 14. Codex-specific data minimization

Codex knows the repository/source; Hero Passport does not need copies.

Never ask Codex to provide:

```text
git diff
changed files
source excerpts
test output
terminal transcript
full prompt
conversation history
cwd path
```

Quality signals in v1 are compact declarations/counters such as tests mentioned/status, scope violations and user corrections.

If future verification wants trustworthy build/test signals, design a local evidence adapter that reads narrowly scoped status itself rather than asking the LLM to dump logs into MCP.

---

## 15. E2E acceptance

Before 0.1.0:

```text
fresh isolated HERO_PASSPORT_HOME
restore/build/install tool
hero-passport init
codex mcp add hero-passport -- hero-passport mcp
codex mcp list
run coding eval task
observe exactly one start
complete task
observe exactly one finish
verify DB/card
restart server/client
verify persisted card/current state
```

Repeat with explicit project `cwd` config.

Capture:

```text
Codex version/build
Hero Passport version
MCP SDK version
OS
.NET version
native SQLite version
config mode
```

---

## 16. Agent eval regressions

Any change to:

```text
tool name
tool description
server instructions
input schema
output schema
annotations
AGENTS snippet
```

requires rerunning representative Codex evals.

Questions answered:

- Does Codex still start once?
- Does it finish once?
- Does it keep/use questId?
- Does it avoid forbidden content?
- Does it avoid overusing card/current?
- Does it render only compact display text?

This is a product-quality gate learned from mature agent-oriented MCP servers.

---

## 17. Unsupported/deferred Codex features

Not in 0.1:

```text
Hero Passport auto-editing ~/.codex/config.toml
remote HTTP MCP
OAuth
Codex-specific proprietary tool APIs
Codex cloud synchronization
MCP Apps UI
MCP Tasks
automatic source/diff capture
background continuous telemetry
```

Hero Passport stays portable at the MCP/Application boundary.

## 18. Primary sources

Current official OpenAI documentation only:

- Codex MCP: https://developers.openai.com/codex/mcp/
- Codex configuration reference: https://developers.openai.com/codex/config-reference/

Release documentation must be rechecked against those pages before publishing install/config examples.
