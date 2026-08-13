# Hero Passport 0.1 — Pull Request Execution Plan

**Status:** execution policy for the 0.1 implementation  
**Baseline:** architecture v3.2.1  
**Updated:** 2026-08-13

## 1. Purpose

Hero Passport 0.1 is implemented as a sequence of independently reviewable pull requests. The objective is not to minimize PR count or file count. The objective is to keep each change narrow enough that its invariants can be reviewed in isolation while still leaving the repository in a buildable, testable and meaningful state.

The documentation/architecture baseline is merged first. Product implementation never shares that PR.

## 2. Review rules

Every product PR must satisfy all of the following before merge:

```text
one coherent behavior/risk boundary
no unrelated cleanup
TDD for behavior changes: observed red -> minimal green -> refactor
focused tests for the changed invariant
full impacted test suite green
warnings treated as errors
no unresolved review findings
normative docs updated only when the implemented contract changes
comments only when they explain a non-obvious why
```

A PR should not be split merely because it contains many files if those files implement one inseparable invariant. Conversely, two small changes belong in separate PRs when they can be independently accepted or rejected.

## 3. Comment policy

Production comments are English-only. Prefer expressive names, types, small methods and tests over comments.

A comment is justified when it preserves a reason that the code alone cannot safely communicate, such as:

```text
provider/protocol behavior forcing an unusual implementation
security/privacy constraint that must not be weakened
ordering required for crash or concurrency correctness
intentional compatibility workaround and its removal condition
non-obvious algorithmic choice where the simpler alternative is wrong
```

Do not add comments that restate the next line, narrate control flow, contain change history, or preserve commented-out code.

## 4. PR sequence

### PR 1 — Architecture and product contracts

**Scope:** documentation and repository instructions only.

Includes product spec, architecture, wire contract, data model, persistence/reliability contract, project identity, deterministic engine specification, Agent Skill specification, testing strategy, integration guidance, decision log, implementation plan and repository agent instructions.

**Must not include:** `.cs`, `.csproj`, solution/build configuration, CI, runtime Skill package or product implementation.

### PR 2 — Build baseline and architecture guards

**Purpose:** prove the chosen .NET/package baseline restores and establish the project dependency graph.

Includes:

```text
global.json
Directory.Build.props
Directory.Packages.props
HeroPassport.slnx
four product project files
architecture test project and dependency-graph tests
minimal CI for restore/build/architecture tests
```

No product behavior.

### PR 3 — SQLite foundation and schema invariants

**Purpose:** establish the durable local storage substrate before use cases depend on it.

Includes:

```text
DbContext/factory
migration 0001 + EF designer metadata + model snapshot
connection-string policy
per-connection pragmas
SQLite runtime qualification
WAL initialization
physical CHECK/FK/partial-index tests
migration/model-drift checks
```

Does not include Hero/Quest business workflows.

### PR 4 — Safe primitives and project identity

**Purpose:** establish identity/text primitives and privacy-preserving project binding.

Includes UUIDv7 typed IDs, JSON-safe integer guard, SafeTextV1 and `project-identity/1` with its Git/filesystem qualification matrix.

Does not include onboarding or Quest mutations.

### PR 5 — Bootstrap, settings and runtime context

**Purpose:** make first run and recovery state durable and retry-safe.

Includes bootstrap request identity, typed singleton settings, minimal Hero creation/activation required by onboarding, configuration and read-only `hero.get_context` application semantics.

MCP transport is not introduced here.

### PR 6 — Durable Quest Start

**Purpose:** add one open Quest per Hero+Project with explicit Hero ownership and replay-safe Start identity.

Includes canonical Start mutation encoding, Start receipt handling, locale snapshot, project/stat creation and concurrency tests.

Does not include Finish or RPG progression.

### PR 7 — Durable Quest Finish and at-most-once progression seam

**Purpose:** finalize a Quest once with conflict-safe retry semantics before the full RPG engine is layered on top.

Includes finish request identity, finalization hash, HP135/HP136 semantics, report/XP event persistence and conflicting/concurrent Finish tests using the minimal reward seam required for this PR.

### PR 8 — Deterministic RPG progression

**Purpose:** add authoritative versioned game calculations without changing transport or persistence identity semantics.

Includes reward, Hero/Skill progression, Rank, Trust/Strain, Streak, Traits/Titles, milestones, allocation rules and golden vectors. Persistence stores rule versions and resulting deltas atomically.

### PR 9 — HP-MCP/2 adapter and contract qualification

**Purpose:** expose the already-tested Application behavior through the official C# MCP SDK.

Includes stdio host, explicit tool catalog, closed schemas, structured content, path-safe errors and per-tool annotations.

Annotations are reviewed semantically per tool; mutating tools must not be globally marked additive/non-destructive when they replace or finalize state.

`HeroPassport.Contract.Tests` must contain executable wire-contract tests and CI must reject accidental zero-test contract assemblies.

### PR 10 — Agent Skill package and behavioral evals

**Purpose:** add portable orchestration only after Core/MCP semantics are stable.

Includes the Skill package, recovery/presentation references, static contract checks and behavioral eval scenarios for false-positive starts, premature finishes, retry identity reuse, ambiguous recovery and provenance handling.

### PR 11 — CLI and administrative safety boundary

**Purpose:** implement human-owned operations that should not be model-facing.

Includes the minimal 0.1 CLI surface such as `init`, `doctor`, explicit logical permanent Hero deletion, export/backup and narrowly scoped repair commands required by accepted contracts.

Administrative operations reuse Application/Infrastructure semantics rather than creating a second game engine.

### PR 12 — Reliability and release qualification

**Purpose:** prove the guarantees that unit/in-process tests cannot establish.

Includes:

```text
child-process crash injection before/after commits
WAL recovery qualification
abandoned EF migration-lock diagnosis and explicit repair
quick_check / foreign_key_check
backup validation
projection rebuild verification
packaged artifact smoke tests
Windows/Linux/macOS release matrix
Codex packaged E2E: bootstrap -> start -> finish -> restart -> context -> replay
```

0.1.0 is not released before this PR's required gates are green.

## 5. Re-review rule

Before merging each PR, review the complete diff as if written by another engineer. Re-check the relevant official documentation when behavior depends on MCP, .NET/EF Core, SQLite, Git or host integration semantics.

A green CI result is necessary but not sufficient. The reviewer must also verify that tests prove the intended invariant rather than merely preserve the implementation.

## 6. Scope changes

If implementation exposes a genuine architecture defect, update the smallest authoritative contract first and record the decision. Do not silently widen the PR with unrelated redesign.

If a new feature is desirable but not required for the 0.1 acceptance criteria, keep it out of the active PR and out of the MVP unless it removes a demonstrated implementation blocker.
