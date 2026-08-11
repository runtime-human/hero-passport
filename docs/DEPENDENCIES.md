# Hero Passport — Dependency Decisions

**Status:** Accepted v3 baseline  
**Snapshot:** 2026-08-11

## 1. Policy

A dependency is added only when it provides a concrete capability that is materially better than .NET/BCL/direct code and its operational/security cost is understood.

Requirements:

```text
stable release by default
Central Package Management
committed lock files
vulnerability audit
explicit package ownership in one project/layer
no transitive reliance for critical native SQLite version
```

Preview dependencies require an ADR with removal/upgrade plan.

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
xunit.runner.visualstudio                    3.1.5 private compatibility
```

Use current framework-provided/BCL capabilities where possible:

```text
Generic Host
Microsoft.Extensions.DependencyInjection
Microsoft.Extensions.Logging
Options
TimeProvider
System.Text.Json
Guid.CreateVersion7()
SHA256
```

Actual native SQLite version must be verified at runtime/tests using `sqlite_version()`.

---

## 3. `ModelContextProtocol`

**Decision:** ACCEPT.

Reasons:

- official C# SDK;
- current stable 2.0 release implements MCP 2026-07-28 and compatibility paths;
- typed server/tool abstractions;
- stdio support;
- future ASP.NET integration exists without requiring us to use it now.

Implementation rules:

```text
ordinary server ProtocolVersion unset/null
explicit four-tool registration
no assembly-wide scanning
no SDK session state used as application state
```

Do not fork/hand-roll JSON-RPC/MCP framing.

---

## 4. `ModelContextProtocol.AspNetCore`

**Decision:** DEFER.

Not a 0.1 dependency.

Add only when `DEPLOYMENT-MODES.md` Profile C or D is actually implemented.

Why defer:

- stdio covers local coding hosts;
- OpenAI Secure MCP Tunnel can reach private stdio for that integration path;
- HTTP introduces network/auth/project-binding/security concerns that are not solved by adding the package.

When added, keep it in the HTTP host/composition boundary, not Domain/Application.

---

## 5. EF Core SQLite

**Decision:** ACCEPT.

Why:

- mapping + migrations + projections in one supported stack;
- local single-user data model is relational;
- future Blazor reads can reuse Application/Infrastructure;
- mature migration tooling.

Use `IDbContextFactory` and short-lived contexts.

Do not use EF InMemory for SQLite semantics.

---

## 6. SQLitePCLRaw bundle

**Decision:** ACCEPT direct pin.

Reason: native SQLite security/correctness version matters and should not be an opaque transitive detail.

Current baseline is 3.0.5 with a modern SQLite dependency. Build/release tests still query actual loaded `sqlite_version()`.

---

## 7. System.CommandLine

**Decision:** ACCEPT.

CLI responsibilities are parsing/help/exit codes and clean adapters. This is preferable to maintaining a custom parser.

Do not let CLI attributes/types leak into Application.

---

## 8. xUnit.net v3

**Decision:** ACCEPT.

Use for deterministic unit/integration/process/architecture tests. Keep `dotnet test` release flow and current Microsoft Testing Platform compatibility.

---

## 9. MediatR

**Decision:** REJECT baseline.

Four core commands/queries do not justify an indirection bus. Direct typed handlers make call graphs clearer to humans and coding agents.

Revisit only if there are many independent cross-cutting pipelines with demonstrated duplication that cannot be solved cleanly through ordinary composition.

---

## 10. FluentValidation

**Decision:** DEFER/REJECT baseline.

We already have:

```text
JSON Schema boundary validation
small semantic validators
Options/config validation
Domain invariants
```

Revisit if validation rules become numerous/reused enough to justify a dedicated DSL.

---

## 11. AutoMapper

**Decision:** REJECT baseline.

Explicit mapping is valuable at privacy/public-contract boundaries. We want code review to see exactly what crosses EF -> Application -> MCP.

---

## 12. Dapper

**Decision:** REJECT baseline.

Do not create dual persistence stacks without a measured query problem. EF projections/raw SQL in localized Infrastructure code are sufficient initially.

Revisit only after profiling a real hot query or unsupported mapping shape.

---

## 13. Polly

**Decision:** REJECT baseline.

No remote API dependency in MVP. SQLite already has busy/timeout behavior. Generic retries around write use cases can duplicate effects if applied incorrectly.

Use explicit idempotency and narrowly scoped transient handling instead.

---

## 14. Serilog/NLog

**Decision:** REJECT baseline.

`Microsoft.Extensions.Logging` covers stderr/optional local diagnostics. Add a richer sink stack only if structured rotation/sinks become a real requirement.

---

## 15. Spectre.Console

**Decision:** DEFER.

Pretty CLI is secondary to predictable stdout/stderr/`--json` and strict MCP stdout isolation. Add only after baseline CLI is stable.

---

## 16. OpenTelemetry exporters

**Decision:** DEFER.

Keep `System.Diagnostics` seams. A local single-user product does not need a remote telemetry exporter by default. Future hosted HTTP may revisit observability separately.

---

## 17. Testcontainers

**Decision:** REJECT for SQLite integration tests.

Use real file-backed SQLite on the host. Containers add indirection without improving fidelity for an embedded DB.

---

## 18. REST/OpenAPI libraries

**Decision:** REJECT baseline.

Hero Passport does not add a REST API for hypothetical portability. MCP is the language-neutral agent integration; CLI/Web use their own adapter semantics.

If a real non-MCP remote consumer appears, design that API explicitly rather than leaking internal DTOs.

---

## 19. OAuth libraries/server frameworks

**Decision:** NOT 0.1.

stdio does not use MCP HTTP authorization. Future remote HTTP should integrate standard ASP.NET/auth middleware and MCP authorization requirements rather than inventing a custom token protocol.

Package selection belongs to the HTTP architecture review.

---

## 20. Host-specific SDKs

**Decision:** REJECT by default.

Do not add Codex/VS Code/Cursor/Claude/JetBrains/Zed SDK dependencies just to register the MCP server. Standard MCP + configuration is the portability mechanism.

A host-specific SDK is justified only for a genuinely non-MCP capability that has explicit product value.

---

## 21. Dependency upgrade procedure

For each upgrade:

1. inspect release notes/security/advisories;
2. confirm stable status;
3. update central version + lock files;
4. run build/full impacted tests;
5. for MCP SDK: run both protocol-era compatibility, manifest snapshots, Inspector and Codex E2E;
6. for EF/SQLite: run fresh/upgrade DB, concurrency and native-version checks;
7. update this document/reference snapshot when architecture-relevant behavior changes.

Do not perform blind “latest all packages” updates in the same change as domain/API behavior.
