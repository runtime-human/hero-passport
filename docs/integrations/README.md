# Hero Passport — Host Integrations

**Architecture:** v3.2  
**Snapshot:** 2026-08-11

Host integrations configure two portable pieces:

```text
Hero Passport local stdio MCP Core
Hero Passport Agent Skill / equivalent host instructions
```

They never define alternate game semantics.

## Support labels

```text
Qualified              current release passed recorded E2E/smoke on that host/version
Documented compatible  official host capabilities appear compatible; limited current smoke evidence
Unknown/unsupported    no current evidence or required behavior unavailable
```

Codex is the first release-blocking Qualified target. Other labels are earned by `TESTING-QUALITY.md`, not by this documentation table.

## Required 0.1 host behavior

Minimum Core integration:

```text
launch/connect to local MCP stdio process
Tools support
usable structured/text tool results
correct project cwd or ability to pass --project-root
```

Desired ambient UX additionally needs native Agent Skills support or an equivalent persistent instruction mechanism capable of applying `docs/AGENT-SKILL.md` semantics.

## Portable command

```text
hero-passport mcp [--project-root <path>]
```

There is no normal `--hero` binding in v3.2: the globally active Hero is persisted application state, and an existing Quest keeps its original Hero owner.

## What qualification must prove

Record host + version + OS + date, then test:

```text
11 exact HP-MCP/2 tools discovered
fresh first-run setup
Skill/equivalent starts meaningful work without user ritual
short factual question does not start
Quest result finishes automatically when truly complete
restart/recovery uses same questId
active Hero behavior correct
structured result rendered usefully
host confirmation behavior documented
permanent delete remains explicit/destructive
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

Hero Passport does not silently edit third-party host configuration. Prefer the host’s current native MCP/Skill installation mechanism and verify it against that host’s latest official documentation at release time.

Host configuration syntax/paths are compatibility data and may change independently of HP-MCP/2. Product invariants stay in Hero Passport docs/tests.
