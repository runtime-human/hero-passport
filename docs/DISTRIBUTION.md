# Hero Passport — Distribution

**Status:** Accepted v3.2.1 distribution contract  
**Snapshot:** 2026-08-11

## 1. 0.1 deliverables

Release bundle conceptually contains:

```text
hero-passport executable
Hero Passport Agent Skill directory
documentation / license / notices
```

Executable provides CLI + local stdio MCP. Skill provides portable lifecycle orchestration.

## 2. Supported platforms

Target .NET 10 supported desktop/server OSes selected in CI/release matrix. Exact OS/architecture artifacts are release-tested before support is claimed.

## 3. Data locations

```text
Windows: %LOCALAPPDATA%\HeroPassport
macOS:   ~/Library/Application Support/HeroPassport
Linux:   XDG data/config conventions
```

`HERO_PASSPORT_HOME` overrides root for development/tests/deliberate portable isolation.

Normal game state is never stored in repository `.git`.

## 4. First run

Human CLI:

```text
hero-passport init
```

Agent path:

```text
host launches hero-passport mcp
-> Skill calls hero.get_context
-> setupCompleted=false
-> Skill conducts short onboarding
-> hero.bootstrap(bootstrapRequestId, ...)
```

Ambiguous bootstrap response is retried with the same request ID/arguments. Post-setup preferences use `hero.configure`.

MCP stdio never prints terminal wizard text to stdout.

## 5. Host installation

Two independent concerns:

1. configure host to launch/connect `hero-passport mcp` for intended project;
2. install/enable official Agent Skill where host supports Agent Skills or equivalent instruction packaging.

Host-specific commands/paths live under `docs/integrations/` and must be verified against current official host docs at release time.

Host without native Skill support may use equivalent instructions; compatibility glue never forks game semantics.

## 6. Project/Hero binding

Preferred host setup launches MCP from intended project cwd. Explicit `--project-root` exists when cwd is unreliable or deliberate monorepo scope is required.

`project-identity/1` is the only Project identity scheme.

Skill hydrates default active Hero via `hero.get_context`, then passes explicit `heroId` to Start. A concurrent host changing active Hero cannot retarget an already formed request.

## 7. Updates

Updates preserve local DB through EF migrations.

Material DB migration follows backup/migration policy in `PERSISTENCE-RELIABILITY.md`, including abandoned migration-lock diagnostics/recovery semantics.

Game rule updates are versioned; executable upgrade never recalculates completed Quest rewards.

## 8. Uninstall/delete

Executable/Skill removal and user-data removal are separate.

Normal uninstall does not silently delete Hero Passport DB.

Permanent individual Hero deletion is explicit CLI logical deletion. It does not claim forensic erasure from backups/snapshots/storage media.

A deliberate full purge may remove application data only with clear irreversible user intent and separately documented behavior.

## 9. Export/backup

`hero-passport export` is logical bounded export, not raw live DB copy.

Physical backup uses SQLite backup API and independent integrity/schema validation before publishing the candidate.

## 10. Supply-chain/release checks

Before publishing:

```text
restore pinned stable dependencies (including actual ModelContextProtocol 2.1.0 restore)
build Release
run full test/eval matrix
publish platform artifact
fresh-artifact smoke
verify version output
verify actual SQLite runtime/pragmas
verify MCP stdout purity
verify Skill format/compat metadata
verify packaged files/licenses/notices
record checksums/signing if infrastructure supports them
```

No release claim from source tests alone; packaged artifact is tested.
