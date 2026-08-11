# Hero Passport — Host Integrations

**Architecture:** v3.2.1  
**Snapshot:** 2026-08-11

Host integrations configure two portable pieces:

```text
Hero Passport local stdio MCP Core
Hero Passport Agent Skill / equivalent host instructions
```

They never define alternate game semantics.

## Support labels

```text
Qualified             current release passed recorded E2E/smoke on host/version
Documented compatible official host capabilities appear compatible; limited current smoke evidence
Unknown/unsupported   no current evidence or required behavior unavailable
```

Codex is the first release-blocking Qualified target.

## Required 0.1 host behavior

Minimum Core integration:

```text
launch/connect local MCP stdio
Tools support
usable structured/text results
correct project cwd or --project-root
```

Ambient UX additionally needs Agent Skills or equivalent persistent instructions capable of applying `docs/AGENT-SKILL.md`.

## Portable command

```text
hero-passport mcp [--project-root <path>]
```

There is no normal `--hero` process binding. `activeHeroId` is persisted default preference, while the Skill reads it through `hero.get_context` and sends explicit `heroId` on Start. Existing Quest owner remains immutable.

## Qualification must prove

Record host/version/OS/date, then test:

```text
current HP-MCP/2 v3.2.1 tool inventory/order discovered
get_context works before/after setup
fresh hero.bootstrap onboarding
persisted auto-start/auto-finish settings hydrate after restart
Skill starts meaningful work without user ritual
short factual question does not start
Start carries explicit HeroId
Finish uses finishRequestId
restart/recovery sees current-Project open Quests across Heroes
HP136 conflict is not overwritten
structured result rendered usefully
host tool-confirmation behavior documented
MCP permanent delete absent / CLI delete documented separately
stdout/config errors absent
known limitations recorded
```

## Current pages

- [`CODEX.md`](CODEX.md) — first Qualified target
- [`CLAUDE-CODE.md`](CLAUDE-CODE.md)
- [`VSCODE.md`](VSCODE.md)
- [`JETBRAINS.md`](JETBRAINS.md)
- [`ZED.md`](ZED.md)
- [`CURSOR.md`](CURSOR.md)
- [`CHATGPT.md`](CHATGPT.md)

## Configuration ownership

Hero Passport does not silently edit third-party host config.

Use the host’s current native MCP/Skill installation mechanism and verify it against latest official host docs at release time.

Host config syntax/paths are compatibility data and may change independently of HP-MCP/2. Product invariants remain in Hero Passport docs/tests.
