# Codex Integration

**Target status:** first release-blocking Qualified host  
**Architecture:** Hero Passport v3.2.1

Codex is the reference host for 0.1 qualification; HP-MCP/2 remains host-neutral.

## Integration shape

```text
Codex
  -> Hero Passport Agent Skill
  -> local hero-passport mcp (stdio)
  -> same-host SQLite
```

Use current official OpenAI/Codex Skills and MCP mechanisms at release time. Hero Passport ships the lifecycle as a portable Agent Skill rather than duplicating full policy in `AGENTS.md`.

## MCP setup rule

Configure Codex using its **current official native MCP configuration mechanism** to launch:

```text
hero-passport mcp
```

from project workspace, or pass `--project-root <project>` when launch cwd is not the intended boundary.

Do not send local filesystem path as an HP-MCP tool argument.

Exact Codex config syntax/paths are release-time compatibility data, not frozen architecture.

## Skill behavior

Install/enable `skills/hero-passport/` with the current supported Skill mechanism.

Expected behavior:

- call `hero.get_context` for persisted settings/recovery/version compatibility;
- bootstrap first run with one `bootstrapRequestId`;
- avoid Quests for short factual questions;
- auto-start meaningful project work conservatively;
- pass explicit selected `heroId` to Start;
- retain/recover `questId` across restart/handoff;
- auto-finish only at genuine completion;
- use `finishRequestId` and respect HP136 finalization conflict;
- report bounded attestations, not “verified facts”;
- render canonical result without recalculation.

## Risk-first qualification checkpoint

Before implementing/claiming all RPG polish, prove packaged Codex vertical E2E:

```text
current HP-MCP/2 v3.2.1 tools discovered
get_context pre-setup
first-run conversational bootstrap
minimal Quest Start explicit Hero
minimal Finish/base XP
server restart/recovery
Start/Finish retry behavior
conflicting Finish HP136
SQLite pooled/new-process effective pragmas
stdio purity
```

After Phase-B RPG implementation, full 0.1 qualification additionally proves 95-XP golden, Skill/Level/Rank/Trust-Strain/cosmetic progression, RU/EN presentation and full Agent Skill evals.

Codex may show its own tool confirmation UI; host UX never changes Core invariants.
