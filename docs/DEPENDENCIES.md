# Hero Passport — Dependency Baseline

**Status:** Accepted v3.2 dependency policy  
**Snapshot:** 2026-08-11

Versions are intentionally pinned to stable releases verified during the v3.2 architecture pass. Preview packages are not the MVP baseline when a suitable stable version exists.

## 1. Runtime and language

```text
.NET SDK             10.0.302
Target framework     net10.0
Language             C# 14
Support channel      .NET 10 LTS
```

Do not move 0.1 to .NET 11 preview.

## 2. MCP

```text
ModelContextProtocol                    2.1.0
```

`ModelContextProtocol.AspNetCore` is **not** required in 0.1 because the supported MCP transport is stdio. Add it only with an accepted Streamable HTTP product requirement.

Do not add the Tasks extension: Hero Passport tool calls are short local mutations/reads and the actual coding-agent work occurs outside the MCP call.

## 3. Persistence

```text
Microsoft.EntityFrameworkCore.Sqlite   10.0.10
Microsoft.Data.Sqlite                  10.0.10
SQLitePCLRaw.bundle_e_sqlite3           3.0.5
```

The bundle currently resolves native SQLite >=3.53.4. Hero Passport doctor/release qualification requires actual loaded SQLite >=3.53.4.

Use central package management (`Directory.Packages.props`) once product projects are created.

## 4. CLI

```text
System.CommandLine                      2.0.10
```

The 3.x line is preview as of the snapshot; do not adopt it for 0.1 without a separate compatibility reason.

## 5. Testing

```text
xunit.v3                                3.2.2
Microsoft.NET.Test.Sdk                  stable version compatible with the chosen .NET 10/xUnit runner at implementation start
```

xUnit 4.x is preview as of the snapshot; keep stable v3 for MVP.

The exact test SDK/transitive runner versions must be re-resolved from official package metadata at implementation start and pinned centrally; do not guess a preview merely because it is newer.

## 6. Localization

Prefer built-in .NET resource infrastructure / `Microsoft.Extensions.Localization` integration already compatible with the Generic Host rather than a third-party localization framework.

Localization resources live in App/presentation; Domain/Application depend only on semantic keys and locale value objects.

## 7. Logging

Use `Microsoft.Extensions.Logging` abstractions/providers already supplied by the host stack. Do not add Serilog/NLog in 0.1 without an operational requirement.

For MCP stdio:

```text
stdout -> protocol only
stderr -> safe diagnostics/logging
```

## 8. Deliberately excluded dependencies

Do not baseline:

```text
MediatR
AutoMapper
Dapper
Polly
FluentValidation
Serilog
NLog
MassTransit/event bus
Temporal SDK runtime dependency
CRDT libraries
cloud sync SDKs
LLM SDK for judging
```

Reasons:

- the modular monolith has direct, small use cases;
- mapping/validation can remain explicit at trust boundaries;
- SQLite/provider already owns the primary local retry/locking model;
- no distributed workflow/cloud event architecture exists in 0.1;
- deterministic RPG calculation must not depend on an LLM.

## 9. External tools, not runtime packages

Development/release tooling may use:

```text
MCP Inspector
Agent Skills reference validator (`skills-ref validate`)
Git
SQLite CLI for diagnostics where available (not correctness dependency)
```

The product must not require these tools at runtime merely because CI uses them.

## 10. Upgrade policy

Before implementation and each release:

1. check official vendor/package sources for latest stable patch versions;
2. prefer patch upgrades within the selected stable major unless compatibility evidence says otherwise;
3. read release notes/security advisories;
4. re-run persistence/MCP contract qualification after relevant upgrades;
5. update `REFERENCES.md` snapshot metadata;
6. never silently rewrite historical game rule versions as part of a package update.
