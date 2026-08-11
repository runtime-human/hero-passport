# Hero Passport — Dependency Decisions

**Status:** Accepted v3.1 baseline  
**Snapshot:** 2026-08-11

## 1. Policy

Add a dependency only for a concrete capability materially better than BCL/framework/direct code.

```text
stable by default
Central Package Management
committed lock files
vulnerability audit
one explicit owning layer/project
critical native behavior verified, not assumed transitively
```

Preview dependencies require an ADR/removal plan.

---

## 2. Accepted 0.1 baseline

```text
.NET SDK                                    10.0.302
Target Framework                            net10.0
C#                                          14
ModelContextProtocol                        2.0.0
Microsoft.EntityFrameworkCore               10.0.10
Microsoft.EntityFrameworkCore.Sqlite        10.0.10
Microsoft.EntityFrameworkCore.Design        10.0.10 private/dev
SQLitePCLRaw.bundle_e_sqlite3               3.0.5
System.CommandLine                           2.0.10
xunit.v3                                     3.2.2
xunit.runner.visualstudio                    3.1.5 private compatibility if required
```

BCL/framework first:

```text
Generic Host / DI / Logging / Options
TimeProvider
System.Text.Json
Guid.CreateVersion7
System.Text.Rune
SHA256
RandomNumberGenerator
ProcessStartInfo.ArgumentList
Directory.ResolveLinkTarget
```

---

## 3. `ModelContextProtocol`

**Decision:** ACCEPT official stable 2.0.0.

Rules:

```text
ProtocolVersion unset/null
explicit four-tool registration
no session state as Application state
exact CallToolResult success/error representation from WIRE-CONTRACT.md
explicit runtime argument validation
```

Important: official C# SDK schema/DataAnnotations can shape advertised schema but **do not enforce runtime argument validation**. Do not add a validation library merely to compensate; current validation surface is small and explicit.

No hand-rolled MCP/JSON-RPC.

---

## 4. `ModelContextProtocol.AspNetCore`

**Decision:** DEFER.

Not a 0.1 package. Add only for an approved Streamable HTTP deployment profile.

HTTP brings auth/origin/project-binding/security work not solved by package installation.

---

## 5. EF Core SQLite

**Decision:** ACCEPT.

Use:

```text
IDbContextFactory
short-lived contexts
EF migrations
real file-backed integration tests
```

Do not use EF InMemory as SQLite correctness evidence.

Selected Microsoft.Data.Sqlite 10.0.10 transaction behavior is explicitly qualified: non-deferred Serializable transaction uses immediate writer intent for our mutation use cases. Re-test on provider upgrade.

---

## 6. SQLitePCLRaw bundle

**Decision:** ACCEPT direct pin 3.0.5.

Native SQLite correctness/security version is an explicit release concern.

Every normal published artifact queries actual:

```sql
SELECT sqlite_version();
```

v3.1 supported WAL floor:

```text
>=3.51.3
```

because upstream SQLite documents the WAL-reset corruption fix beginning there. NuGet package version is never accepted as proof of which native library actually loaded.

---

## 7. System.CommandLine

**Decision:** ACCEPT 2.0.10.

Own parsing/help/exit codes only; no CLI types in Application.

---

## 8. xUnit.net v3

**Decision:** ACCEPT 3.2.2.

Use for unit/integration/process/crash/architecture/contract suites. Long-running AgentEvals stay separately categorized.

---

## 9. MediatR

**Decision:** REJECT baseline.  
Direct typed handlers are clearer for a four-operation core.

## 10. FluentValidation

**Decision:** REJECT/DEFER baseline.  
SafeText/UUID/enums/metrics/config have small explicit validators. Revisit only if validation surface grows materially.

## 11. AutoMapper

**Decision:** REJECT baseline.  
Explicit mappings are a privacy/public-contract safeguard.

## 12. Dapper

**Decision:** REJECT baseline.  
Avoid dual persistence stacks; localized EF/raw SQL is sufficient.

## 13. Polly

**Decision:** REJECT baseline.  
Microsoft.Data.Sqlite already applies busy/locked timeout retry behavior. Generic retries around writer transactions can extend stalls or duplicate effects.

## 14. Serilog/NLog

**Decision:** REJECT baseline.  
Built-in logging is enough for path/payload-safe local diagnostics initially.

## 15. Spectre.Console

**Decision:** DEFER.  
Pretty CLI is secondary to stdout/stderr correctness.

## 16. OpenTelemetry exporters

**Decision:** DEFER.  
Keep `System.Diagnostics` seams; no remote telemetry by default.

## 17. Testcontainers

**Decision:** REJECT for SQLite.  
Real local file-backed SQLite has higher fidelity for the embedded DB.

## 18. REST/OpenAPI libraries

**Decision:** REJECT baseline.  
MCP is language-neutral agent integration; separate public REST needs a real consumer/design.

## 19. OAuth/server auth libraries

**Decision:** NOT 0.1.  
Future HTTP uses standard ASP.NET/MCP authorization design, not a custom token layer.

## 20. Host-specific SDKs

**Decision:** REJECT by default.  
Standard MCP + host config is the integration mechanism. Add a host SDK only for a concrete non-MCP product capability.

---

## 21. Dependency upgrade procedure

Every upgrade:

1. read official release/security notes;
2. confirm stable status;
3. update central version + locks;
4. locked restore/build/impacted tests;
5. MCP SDK upgrade: schemas/results/2026+2025/Inspector/Codex E2E;
6. EF/Microsoft.Data.Sqlite upgrade: re-prove immediate-writer behavior, migrations, races, crash/backup;
7. SQLite/native upgrade: verify actual loaded version and full WAL/concurrency suite;
8. update ADR/spec when behavior changes.

Do not combine blind mass dependency upgrades with public contract/rule changes.
