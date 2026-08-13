# Claude Code Integration

**Status:** documented compatibility candidate; qualification evidence required per release

## Integration shape

```text
Claude Code
  -> Hero Passport Agent Skill or equivalent supported Skill/instruction package
  -> local hero-passport mcp (stdio)
  -> same-host SQLite
```

Use Claude Code’s current official MCP configuration mechanism to launch `hero-passport mcp` in the project workspace. If reliable project cwd cannot be supplied, pass `--project-root <path>` as local launch configuration.

Use the portable `skills/hero-passport/` package when the current host version supports the open Agent Skills-compatible workflow; otherwise map only the lifecycle guidance to the host’s supported persistent instruction surface.

Do not fork reward rules or tool semantics per host.

## Qualification checklist

Before upgrading this page to Qualified, verify against current official Claude documentation and an actual current host build:

```text
11 HP-MCP tools discovered
stdio project binding
first-run onboarding
Skill trigger behavior
auto-finish behavior
restart/recovery
structured result rendering
host mutation-confirmation UX
known limitations
```

Record exact host version/OS/date. Configuration syntax is intentionally not frozen here because it is third-party compatibility data, not Hero Passport architecture.
