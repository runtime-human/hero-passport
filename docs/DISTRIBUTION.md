# Hero Passport — Distribution

**Status:** Accepted v3.2.1 distribution contract  
**Snapshot:** 2026-09-10

## 1. 0.1 release subject

Hero Passport 0.1 publishes one **framework-dependent portable ZIP** as the canonical binary release subject:

```text
hero-passport-0.1.0.zip
  HeroPassport.App.dll + framework-dependent runtime files
  Hero Passport Agent Skill directory
  user-facing documentation / license / notices
  release-manifest.json
```

This is a **single cross-platform release archive**, not three independently built OS archives. It is deliberately **not a native apphost** and does not bundle the .NET runtime. A compatible .NET 10 runtime must already be installed on the target machine.

The canonical raw-archive entry point is:

```text
dotnet HeroPassport.App.dll <command>
```

Documentation may use `hero-passport` as the logical CLI product name. The raw 0.1 ZIP itself does not claim to install a PATH-level native `hero-passport` launcher. A future installer, wrapper, native apphost or self-contained package is a separate distribution decision and must receive its own qualification evidence.

Executable payload provides CLI + local stdio MCP. Skill provides portable lifecycle orchestration.

## 2. Supported platforms and exact-byte qualification

The canonical 0.1 archive is framework-dependent and is intended for supported .NET 10 desktop/server environments selected by release qualification.

A platform claim requires the **same exact archive** bytes to be downloaded from the release candidate handoff, SHA-256/manifest verified, safely extracted and exercised on that platform. The release-time matrix is:

```text
Ubuntu 24.04
Windows Server 2025
macOS 15
```

Each platform runs the packaged vertical E2E against the extracted archive. Codex remains the reference host and its full host lifecycle qualification runs only on Linux so host work is not duplicated across the OS matrix.

The ordinary pull-request `release-platform` workflow remains a fast portability regression gate. It is useful evidence, but it does not replace final same-exact-archive qualification.

## 3. Data locations

```text
Windows: %LOCALAPPDATA%\HeroPassport
macOS:   ~/Library/Application Support/HeroPassport
Linux:   XDG data/config conventions
```

`HERO_PASSPORT_HOME` overrides root for development/tests/deliberate portable isolation.

Normal game state is never stored in repository `.git`.

## 4. First run

Human CLI, using an installed/wrapped logical command name where one exists:

```text
hero-passport init
```

Raw portable archive equivalent:

```text
dotnet HeroPassport.App.dll init
```

Agent path:

```text
host launches Hero Passport MCP
-> Skill calls hero.get_context
-> setupCompleted=false
-> Skill conducts short onboarding
-> hero.bootstrap(bootstrapRequestId, ...)
```

Ambiguous bootstrap response is retried with the same request ID/arguments. Post-setup preferences use `hero.configure`.

MCP stdio never prints terminal wizard text to stdout.

## 5. Host installation

Two independent concerns:

1. configure host to launch/connect the exact Hero Passport release payload with `mcp` for the intended project;
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

## 10. Supply-chain and release gate

The final release workflow is manual-only. It must be dispatched from the frozen release commit with the exact 40-character `expected_sha`; the workflow fails closed if `GITHUB_SHA` differs.

The release pipeline must perform, in order:

```text
restore pinned stable dependencies, including ModelContextProtocol 2.2.0
build/test exact product version 0.1.0
publish framework-dependent payload with UseAppHost=false
verify CLI/runtime version authority
build deterministic ZIP with fixed entry metadata/order
record SHA-256 + release manifests
upload one immutable candidate between jobs
verify the same exact archive on Ubuntu/Windows/macOS
run PackagedE2E on each extracted archive
run Codex reference-host lifecycle on Linux
only after platform qualification, generate GitHub Artifact Attestations/Sigstore provenance
verify the attestation for the exact ZIP
retain the qualified candidate for release publication
```

The archive verifier rejects mismatched checksums/manifests, unsafe ZIP paths, duplicate entries and symlink entries before executing the payload.

No release claim is made from source tests, a different publish directory, or a separately rebuilt OS payload. The checksum, platform tests and attestation all refer to the same exact archive release subject.

Current implementation follows the official .NET framework-dependent deployment contract and GitHub Artifact Attestations guidance. Release-time qualification must recheck current official documentation before publication:

- .NET `dotnet publish`: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish
- .NET SDK `UseAppHost`: https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props
- GitHub Artifact Attestations: https://docs.github.com/en/actions/how-tos/secure-your-work/use-artifact-attestations/use-artifact-attestations
