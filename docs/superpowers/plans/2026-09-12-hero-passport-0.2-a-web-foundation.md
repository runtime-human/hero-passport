# Hero Passport 0.2-A Web Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `HeroPassport.Web` local Blazor Web App adapter that renders one read-only dashboard vertical for an explicitly resolved local project without creating a second game engine, persistence authority, REST API, or browser-side state store.

**Architecture:** `HeroPassport.Web` is a new presentation/composition adapter over the existing Application/Infrastructure stack. The first slice uses static SSR, explicit loopback binding, existing project identity and existing read models; it performs no Web mutations and no direct EF/SQLite queries from components.

**Tech Stack:** C# 14, .NET 10 LTS, ASP.NET Core 10 Blazor Web App, existing EF Core/SQLite persistence, xUnit v3, ASP.NET Core process/HTTP smoke qualification.

**Spec:** GitHub issue #36 (`0.2-A: Local Web foundation and read-only dashboard vertical`) and the accepted 0.2 umbrella design from the repository planning discussion.

## Global Constraints

- Work from exact baseline `6b51c9d171063662779b2718b2fb429a72cda0a0`.
- Keep `net10.0` / C# 14; do not move to .NET 11.
- Use static SSR unless an acceptance behavior proves Interactive Server necessary; none is expected in 0.2-A.
- Default listener must be loopback-only.
- No WebAssembly client project.
- No REST/minimal API surface for browser state.
- No direct `DbContext`, `DbSet`, raw SQL, filesystem, or Git access from Razor components.
- Domain/Application remain Web-agnostic.
- Reuse `project-identity/1` and existing Application read models.
- Preserve privacy deny-list and existing CLI/MCP behavior.
- TDD: observe RED before production implementation for each behavior.

---

### Task 1: Add executable architecture guard for the Web adapter

**Files:**
- Modify: `tests/HeroPassport.Architecture.Tests/ArchitectureTests.cs`
- Modify: `HeroPassport.slnx`
- Create: `src/HeroPassport.Web/HeroPassport.Web.csproj`

**Interfaces:**
- Consumes: existing `HeroPassport.Application` and `HeroPassport.Infrastructure` project references.
- Produces: a `HeroPassport.Web` project that Core projects do not reference and which does not depend on `HeroPassport.App`.

- [ ] **Step 1: Write a failing architecture test** that loads the Web assembly/project reference graph and requires: Domain/Application/Infrastructure do not reference `HeroPassport.Web`, and `HeroPassport.Web` does not reference `HeroPassport.App`.
- [ ] **Step 2: Run the architecture test** and confirm RED because `HeroPassport.Web` does not exist yet.
- [ ] **Step 3: Create the minimal Web project** with `Microsoft.NET.Sdk.Web`, `TargetFramework=net10.0`, and only the existing lower-layer project references required for composition.
- [ ] **Step 4: Add the project to `HeroPassport.slnx`** and run architecture tests again.
- [ ] **Step 5: Confirm GREEN** and commit the architecture boundary separately.

### Task 2: Prove real loopback-only Web startup before rendering product data

**Files:**
- Create: `tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj`
- Create: `tests/HeroPassport.Web.Tests/WebProcessTests.cs`
- Create: `src/HeroPassport.Web/Program.cs`
- Modify: `HeroPassport.slnx`
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: `HERO_PASSPORT_HOME`, optional explicit `--project-root`, existing application composition seam.
- Produces: a Web process that starts on loopback and responds over HTTP without exposing a public listener.

- [ ] **Step 1: Add a process-level failing test** that launches the Web assembly with an isolated `HERO_PASSPORT_HOME`, captures the announced endpoint, resolves it, and asserts the actual listener address is loopback.
- [ ] **Step 2: Run only the new Web tests** and confirm RED because no Web executable exists.
- [ ] **Step 3: Implement minimal `Program.cs`** with explicit loopback endpoint binding and graceful cancellation. Avoid `UseUrls` wildcard behavior.
- [ ] **Step 4: Add the test project to solution/CI** and run the Web process test.
- [ ] **Step 5: Confirm GREEN**, then run the full build/architecture suite and commit.

### Task 3: Add the first read-only dashboard vertical through Application semantics

**Files:**
- Create: `src/HeroPassport.Web/Components/App.razor`
- Create: `src/HeroPassport.Web/Components/Routes.razor`
- Create: `src/HeroPassport.Web/Components/Layout/MainLayout.razor`
- Create: `src/HeroPassport.Web/Components/Pages/Home.razor`
- Create: `src/HeroPassport.Web/wwwroot/app.css`
- Create or modify only if required by existing composition: a minimal shared composition/runtime-path seam in the lowest appropriate existing project.
- Modify: `tests/HeroPassport.Web.Tests/*`

**Interfaces:**
- Consumes: existing setup/runtime context and hero-card/read-model Application use cases; existing project identity resolver.
- Produces: static SSR HTML showing setup state, presentation-safe project state, active/default Hero, Hero progression/card summary, and current-project open Quest state where already available.

- [ ] **Step 1: Add failing HTTP/render tests** for fresh/unconfigured storage and configured Hero/project data. The tests must assert useful product text/data and absence of template demo content.
- [ ] **Step 2: Run tests and confirm RED** because dashboard routes/components do not exist.
- [ ] **Step 3: Implement the minimal static SSR component tree** and wire it to Application read models only.
- [ ] **Step 4: If composition cannot be reused without `Web -> App`, extract only the smallest reusable service-registration/runtime-path seam, with a focused architecture test.
- [ ] **Step 5: Re-run focused Web tests** until GREEN.
- [ ] **Step 6: Run existing CLI/MCP/App/Contract tests** to prove no behavior regression and commit.

### Task 4: Add privacy/read-only regression guards for the Web slice

**Files:**
- Modify: `tests/HeroPassport.Web.Tests/*`
- Modify: `tests/HeroPassport.Architecture.Tests/ArchitectureTests.cs` if static dependency assertions are needed.

**Interfaces:**
- Consumes: rendered dashboard and existing persistence semantics.
- Produces: executable evidence that GET rendering is bounded and does not bypass privacy/architecture boundaries.

- [ ] **Step 1: Add failing tests** asserting rendered output does not expose full workspace paths, Git remote URLs, request IDs, mutation hashes, raw source/diff/log/prompt fields, or internal project fingerprints.
- [ ] **Step 2: Add a failing read-only behavior test** proving a GET against fresh project context does not create project/history bookkeeping solely because the dashboard was viewed.
- [ ] **Step 3: Run and observe RED** for any leaking or write-on-read behavior.
- [ ] **Step 4: Implement the minimal fix at the Application/Web projection boundary**, never by string-scrubbing HTML after rendering.
- [ ] **Step 5: Re-run focused tests and full impacted suite** and commit.

### Task 5: Documentation and final qualification for #36

**Files:**
- Modify only normative docs whose implemented 0.2-A state changed, likely `docs/ARCHITECTURE.md`, `docs/DEPLOYMENT-MODES.md`, `docs/TESTING-QUALITY.md`, and `docs/ROADMAP.md`.
- Do not broaden product scope beyond #36.

**Interfaces:**
- Produces: repository truth matching the implemented read-only Web foundation.

- [ ] **Step 1: Update documentation only after executable behavior is GREEN.** Record `HeroPassport.Web` as an implemented local loopback adapter and keep mutations/security hardening beyond #36 explicitly deferred.
- [ ] **Step 2: Run `dotnet restore`, Release build, all existing test suites, new Web tests, publish/package regression tests that are part of ordinary CI.**
- [ ] **Step 3: Verify the GitHub required `build-test` workflow is GREEN on the exact PR head.**
- [ ] **Step 4: Review the full diff for accidental direct EF access, public bind, REST surface, interactive-server drift, template demo content, or privacy leaks.**
- [ ] **Step 5: Open the focused PR referencing `#36` with RED/GREEN evidence and exact head SHA.**

## Self-review

- Coverage: architecture, loopback process behavior, read-only vertical, privacy/read-only guards, documentation and final CI evidence are all assigned to explicit tasks.
- Scope: no Web mutations, browser-token security slice, full history UI, settings management, packaging redesign or release publication are included.
- Type/API names intentionally follow repository truth discovered during implementation; no new domain/game types are introduced by this plan.
