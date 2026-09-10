# Codex Integration

**Target status:** first release-blocking reference host; live host smoke still required before `Qualified`  
**Architecture:** Hero Passport v3.2.1  
**Verified: 2026-09-09** against current official OpenAI Codex Skills and MCP documentation.

Codex is the reference host for 0.1 qualification; HP-MCP/2 remains host-neutral.

## Integration shape

```text
Codex
  -> repo-local Hero Passport Agent Skill
  -> local hero-passport mcp (stdio)
  -> same-host SQLite
```

Hero Passport ships one canonical Skill source in the release bundle:

```text
skills/hero-passport/
```

Do not fork lifecycle or game policy into host-specific instructions.

## Skill installation

Current Codex repository Skill discovery scans `.agents/skills` from the working directory up through the repository root. Codex follows symlinked Skill directories.

Map the canonical release Skill into the intended repository, for example:

```text
<repo>/.agents/skills/hero-passport -> <hero-passport-release>/skills/hero-passport
```

A byte-for-byte copy is acceptable where symlinks are unsuitable, but it is deployment material, not a second policy authority.

## MCP setup

Current Codex CLI supports local STDIO MCP servers directly. With `hero-passport` available on PATH:

```text
codex mcp add hero-passport -- hero-passport mcp
codex mcp list
```

For project-scoped configuration, current Codex also supports trusted-project `.codex/config.toml`:

```toml
[mcp_servers.hero-passport]
command = "hero-passport"
args = ["mcp"]
```

If launch cwd is not the intended project boundary, either configure the MCP server `cwd` or pass local launch arguments:

```toml
[mcp_servers.hero-passport]
command = "hero-passport"
args = ["mcp", "--project-root", "/intended/project"]
```

The filesystem path is launch configuration only. Never send it as an HP-MCP tool argument.

Official release-time references:

- https://developers.openai.com/codex/skills
- https://developers.openai.com/codex/mcp

## Skill behavior

Expected behavior remains host-neutral:

- call `hero.get_context` for persisted settings/recovery/version compatibility;
- bootstrap first run with one `bootstrapRequestId`;
- avoid Quests for short factual questions;
- auto-start meaningful project work conservatively;
- pass explicit selected `heroId` to Start;
- retain/recover `questId` across restart/handoff;
- auto-finish only at genuine completion;
- use `finishRequestId` and respect HP136 finalization conflict;
- report bounded attestations, not independently verified facts;
- render canonical result without recalculation.

## Qualification boundary

The repository packaged vertical E2E already proves the published executable/Skill package, real stdio HP-MCP lifecycle, restart/recovery, retries and HP135/HP136 behavior independently of a model host.

`Qualified` for Codex additionally requires release-time evidence from a current Codex build that the installed Skill is discovered and the configured MCP server is usable through the actual host path. Host tool-confirmation UX may differ; it never changes Core invariants.
