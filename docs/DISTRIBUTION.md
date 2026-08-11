# Hero Passport — Distribution

**Status:** Accepted v3.2 distribution contract  
**Snapshot:** 2026-08-11

## 1. 0.1 deliverables

Release bundle conceptually contains:

```text
hero-passport executable
Hero Passport Agent Skill directory
documentation / license / notices
```

The executable provides CLI + local stdio MCP. The Skill provides portable lifecycle orchestration.

## 2. Supported platform target

Target .NET 10 supported desktop/server OSes selected in CI/release matrix. Exact OS/architecture artifacts are release-tested before claiming support.

Do not claim a platform merely because .NET can theoretically run there.

## 3. Data locations

```text
Windows: %LOCALAPPDATA%\HeroPassport
macOS:   ~/Library/Application Support/HeroPassport
Linux:   XDG data/config conventions
```

Database is under the application data directory. `HERO_PASSPORT_HOME` overrides the root for development/tests and deliberate portable isolation.

The product never stores normal game state inside a repository’s `.git` directory.

## 4. First run

Human CLI path:

```text
hero-passport init
```

Agent path:

```text
host launches hero-passport mcp
-> gameplay call reports HP001 setup_required
-> Hero Passport Skill conducts short onboarding
-> hero.configure persists setup
```

The MCP stdio server never prints an interactive wizard into stdout.

## 5. Host installation

Installation has two independent concerns:

1. configure the host to launch/connect to `hero-passport mcp` for the project;
2. install/enable the official Hero Passport Agent Skill where the host supports Agent Skills or equivalent instruction packaging.

Host-specific commands/paths live in `docs/integrations/` and must be verified against the current official host documentation during release qualification.

A host without native Skill support may use an equivalent project/global instruction mechanism; this is compatibility glue, not a fork of game semantics.

## 6. Project binding

Preferred host setup launches MCP from the intended project cwd. Explicit `--project-root` is available when host cwd is unreliable or when a deliberate monorepo scope is required.

`project-identity/1` decides identity; installers/integration docs must not create a second project-ID scheme.

## 7. Updates

Package/application updates must preserve the local database through EF migrations.

Before an update that performs a material DB migration, follow the backup/migration policy in `PERSISTENCE-RELIABILITY.md`.

Game rule updates are versioned. Upgrading the executable never silently recalculates completed Quest rewards.

## 8. Uninstall

Executable/Skill removal and user-data removal are separate actions.

Normal uninstall should not silently delete the user’s Hero Passport database. A deliberate purge operation may remove app data with clear irreversible intent.

## 9. Export/backup

`hero-passport export` is a logical user-readable/machine-readable data export, not a physical live DB copy.

Physical backup uses the SQLite backup API and independent integrity verification.

## 10. Supply-chain/release checks

Before publishing an artifact:

```text
restore from locked/pinned stable dependencies
build Release
run full test/eval matrix
publish platform artifact
run fresh-artifact smoke
verify version output
verify MCP stdio stdout purity
verify Skill format
verify packaged files/license/notices
record checksums/signing if release infrastructure supports them
```

No release claim is made from source tests alone; test the packaged artifact.
