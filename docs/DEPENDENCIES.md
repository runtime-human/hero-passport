# Hero Passport — Dependency Baseline

**Status:** Accepted v3.2.1 dependency policy  
**Snapshot:** 2026-08-11

Versions are pinned to stable releases verified during the architecture pass. Task 0 of the implementation plan performs a real package restore gate before product work proceeds.

## 1. Runtime/language

```text
.NET SDK             10.0.302
Target framework     net10.0
Language             C# 14
Support channel      .NET 10 LTS
```

Do not move 0.1 to .NET 11 preview.

## 2. MCP

```text
ModelContextProtocol  2.1.0
```

Official C# SDK v2.1.0 was released 2026-08-05. The architecture does not regress to 2.0.0 based on stale search/package indexes; implementation still proves actual restore/build from configured feeds.

`ModelContextProtocol.AspNetCore` is not required because 0.1 supported transport is stdio.

Do not add the Tasks extension: Hero Passport calls are short local reads/mutations; actual agent work occurs outside them.

## 3. Persistence

```text
Microsoft.EntityFrameworkCore.Sqlite   10.0.10
Microsoft.Data.Sqlite                  10.0.10
SQLitePCLRaw.bundle_e_sqlite3           3.0.5
```

Official NuGet metadata for bundle 3.0.5 declares native SQLite dependency `>=3.53.4`. Hero Passport nevertheless checks the **actual loaded runtime** with `sqlite_version()` and requires >=3.53.4 in doctor/release qualification.

Use central package management once product projects exist.

## 4. SQLite effective policy is not a package

Required runtime state:

```text
WAL
synchronous=FULL
foreign_keys=ON
trusted_schema=OFF
Cache=Default
Pooling=True
Default Timeout=5
```

Microsoft.Data.Sqlite connection strings expose Foreign Keys/Pooling/Default Timeout/Cache but no Synchronous keyword, so FULL/trusted-schema are explicit connection-open policy, not dependency configuration.

## 5. CLI

```text
System.CommandLine 2.0.10
```

Do not adopt a preview major for 0.1 without a separate requirement/qualification.

## 6. Testing

```text
xunit.v3 3.2.2
Microsoft.NET.Test.Sdk stable .NET10/xUnit-compatible version resolved/pinned at implementation start
```

Do not choose preview solely because it is newer.

## 7. Localization/logging

Prefer built-in .NET resources / Microsoft.Extensions localization integration. Domain/Application depend only on semantic keys/locale values.

Use Microsoft.Extensions.Logging already supplied by host stack. Do not add Serilog/NLog without an operational requirement.

stdio:

```text
stdout -> MCP protocol only
stderr -> safe diagnostics
```

## 8. Deliberately excluded dependencies

```text
MediatR
AutoMapper
Dapper
Polly
FluentValidation
Serilog
NLog
MassTransit/event bus
Temporal runtime
CRDT libraries
cloud sync SDKs
LLM judge SDK
```

Reason: small modular-monolith use cases, explicit trust-boundary mapping/validation, SQLite/provider coordination, no distributed workflow/cloud architecture and deterministic non-LLM game calculation.

## 9. External development/release tools

May include:

```text
MCP Inspector
Agent Skills reference validator (`skills-ref validate`)
Git
SQLite CLI for diagnostics where available
```

These are not runtime correctness dependencies merely because CI uses them.

## 10. Upgrade policy

Before implementation and every release:

1. check current official vendor/package sources;
2. prefer stable patch upgrades within selected major when qualified;
3. read release/security/reliability notes;
4. re-run affected persistence/MCP qualification;
5. update `REFERENCES.md`;
6. never rewrite historical game rule versions as a package-upgrade side effect.
