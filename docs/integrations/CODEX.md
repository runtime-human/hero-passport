# Codex Integration

**Target status:** first release-blocking Qualified host  
**Architecture:** Hero Passport v3.2

Codex is the reference host for 0.1 qualification, but HP-MCP/2 remains host-neutral.

## Integration shape

```text
Codex
  -> Hero Passport Agent Skill
  -> local hero-passport mcp (stdio)
  -> same-host SQLite
```

Current OpenAI documentation supports Skills in Codex and describes OpenAI Skills as following the open Agent Skills standard. Hero Passport therefore ships its portable Skill in that format rather than duplicating the complete lifecycle in `AGENTS.md`.

## MCP setup rule

Use Codex’s **current official native MCP configuration mechanism** to launch:

```text
hero-passport mcp
```

from the project workspace, or pass:

```text
--project-root <project>
```

when the host’s launch cwd is not the intended project boundary.

Do not send the local path as an HP-MCP tool argument.

Exact Codex configuration syntax/paths are release-time compatibility data and must be copied from current official OpenAI docs/tested tooling, not frozen as Hero Passport architecture.

## Skill

Install/enable `skills/hero-passport/` using the current Codex Skill/plugin mechanism. The Skill is expected to:

- avoid Quests for short factual questions;
- auto-start meaningful project work;
- retain/recover `questId`;
- auto-finish only at genuine completion;
- report bounded provenance facts;
- render canonical result data without recalculation.

## Qualification gate

Before labeling a release Qualified on Codex, record Codex version/OS/date and prove:

```text
11 tools discovered in order
first-run conversational onboarding
clean Quest start/finish
95-XP golden through real MCP path
restart/recovery
Hero switch ownership
structuredContent compatibility output
Skill trigger/finish evals
no MCP stdout contamination
```

Codex may show its own tool confirmation UI; that host UX does not change Hero Passport invariants.
