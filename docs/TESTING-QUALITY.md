# Hero Passport — testing, evaluation and release quality

**Status:** Accepted quality baseline  
**Snapshot:** 2026-08-10  
**Principle:** deterministic tests prove correctness; protocol tests prove MCP compatibility; agent evals prove workflow usability.

## 1. Quality model

Hero Passport has three distinct failure classes:

```text
A. game/storage bug
   deterministic tests catch it

B. MCP/CLI contract bug
   protocol/process tests catch it

C. model chooses tools badly
   agent evaluations catch it
```

Do not collapse these into one test suite. Sentry MCP is a useful precedent: unit tests and agent evaluations answer different questions.

Release-blocking suites are deterministic. Agent evaluations begin as manual/nightly evidence and may become blocking only after the harness/model/config is sufficiently reproducible.

---

## 2. Test projects

```text
tests/
  HeroPassport.Domain.Tests/
  HeroPassport.Application.Tests/
  HeroPassport.Infrastructure.Tests/
  HeroPassport.App.Tests/
  HeroPassport.Architecture.Tests/
  HeroPassport.AgentEvals/
```

`AgentEvals` may initially be a console/test harness rather than an ordinary xUnit project if that gives clearer model-run artifacts. It must still be versioned in the repository.

---

## 3. Domain tests

Pure, fast, exhaustive around boundaries.

### XP/reward

Cover:

```text
every quest type
every result multiplier
each bonus individually
each penalty individually
bonus+penalty combinations
floor/minimum-zero behavior
large bounded counters
standard 95 XP golden
rule-version fixture
```

### Levels

Cover level thresholds exactly before/at/after:

```text
0, 99, 100, 249, 250, ...
```

Verify cumulative/next-level formulas agree.

### Skill normalization/distribution

Cover:

```text
canonical keys
aliases
case/whitespace
unknown key rejection
duplicate normalization
1/2/3 skill distributions
rounding remainder
sum(skillXp) == rewardXp
```

### Trust/Risk

Boundary/clamp tests at 0 and 100 plus all result/correction/scope combinations.

### Traits

Progress/unlock threshold tests and permanent-unlock invariant.

Domain goldens contain machine values, not localized MCP punctuation.

---

## 4. Application tests

Use in-memory fakes/stubs of application ports, not EF InMemory.

### StartQuest

```text
first start creates open quest
matching retry returns same ID
conflicting open quest -> HP132
project/hero resolution failure maps to typed error
TimeProvider timestamp used
```

### FinishQuest

```text
open success -> reward mutation intent
already finished -> persisted original outcome
not found -> HP130
skills normalized before engine
typed reward/report contains rule versions
no renderer/MCP dependency
```

### Reads

Card/current quest projection behavior, empty/current states and active-hero resolution.

Application tests assert typed results only.

---

## 5. Infrastructure tests — real SQLite only

Every persistence test uses an isolated temporary **file-backed SQLite** database with the same provider/native bundle and relevant PRAGMAs as production.

Why file-backed rather than `:memory:` for core integration coverage:

- WAL behavior matters;
- file locking/concurrency matters;
- migration files matter;
- app-data lifecycle matters.

In-memory SQLite may be used for a narrowly isolated mapping/query experiment, but never as the only persistence proof.

### Required coverage

```text
migration 0001 -> working DB
all FKs and unique constraints
partial/open-quest uniqueness
UNIQUE xp_events.quest_id
WAL journal mode
FULL synchronous mode
foreign_keys ON
native sqlite_version accepted
read/write/open behavior
busy timeout behavior
pooling does not corrupt test isolation
```

### Finish atomicity

Inject controlled failure at each mutation stage and prove no partial progression survives.

### Concurrency

Two independent contexts/process-like operations attempt to finish the same quest:

```text
one commits canonical reward
other returns/reloads canonical already-finished state
exactly one xp_event exists
hero XP changed once
```

Also test simultaneous read during short writer under WAL.

### Migration upgrades

Keep representative database fixtures for the immediately previous released version once releases exist.

Test:

```text
previous DB -> Migrate -> current model/data preserved
fresh DB -> Migrate -> current schema
```

CI runs:

```text
dotnet ef migrations has-pending-model-changes
```

with the pinned SDK/toolchain.

---

## 6. MCP contract tests

Do not test only C# attributes/DTOs. Launch/build the actual configured MCP server and inspect what it advertises.

Canonical assertions:

```text
tools/list contains exactly 4 tools
exact canonical order
names stable
descriptions within budgets
input schemas strict
output schemas present
annotations exact
taskSupport forbidden
no dynamic/unexpected tool
```

Input negative tests:

```text
unknown property
oversized goal
oversized summary
malformed UUID
unknown enum
negative/excessive counter
4th skill
unknown skill
```

Output tests:

```text
structured result conforms to output schema
compact displayText bounded
no duplicate statusText/agentHint/schemaVersion baggage
no path/env/secret fields
finish retry returns original persisted data
```

### MCP Inspector

Use official MCP Inspector as a development/manual protocol smoke tool. Once automated, pin its version/invocation instead of using an unbounded `latest` dependency in CI.

### Stdout guard

Process-level test starts:

```text
hero-passport mcp
```

and proves stdout contains only valid protocol traffic. Capture stderr separately.

Test startup/migration failure path too; no human banner may leak to stdout.

---

## 7. MCP catalog budget test

Serialize/obtain the actual advertised tool catalog and measure UTF-8 bytes/characters.

Initial target:

```text
total 4-tool catalog <= 10 KiB
individual description <= 300 chars
```

This is a proxy for context/token cost, not a claim of exact model tokens.

If the catalog grows:

1. simplify descriptions/schema;
2. remove redundant fields;
3. challenge whether the new tool belongs in MCP;
4. only then consider discovery/toolset mechanisms.

Do not introduce dynamic discovery for a four-tool server.

---

## 8. Presentation golden tests

`HeroTextRenderer` is tested separately from Domain.

Goldens by locale/presentation mode cover:

```text
start
finish 95 XP
level-up
no active quest
hero card
error summaries
```

RU terminology fixture must include:

```text
scope_control -> Контроль
clean scope -> Бонус за контроль
scope violation -> Выход за задачу
```

Presentation changes do not alter reward-rule goldens.

---

## 9. CLI tests

Test parsing and process behavior.

Required commands/gates:

```text
--help
--version
init
doctor
card
quest current
export
data path
mcp dispatch path
```

Test:

- expected exit codes;
- stdout/stderr separation;
- `--json` where supported;
- malformed option behavior;
- no MCP protocol bytes in ordinary CLI;
- no ordinary CLI text in MCP mode.

---

## 10. Configuration/path tests

Inject platform/environment/path inputs rather than relying on CI host only.

Cover:

```text
Windows LocalApplicationData
macOS Application Support
Linux XDG set/unset/empty
HERO_PASSPORT_HOME
unknown config field rejection
configVersion rejection
malformed JSON
precedence CLI > env > config > default
unwritable data dir
```

Separate real OS smoke tests on Windows/Linux/macOS validate actual filesystem semantics.

---

## 11. Security/privacy tests

Automated sentinel strategy:

Use obvious sensitive strings such as:

```text
SUPER_SECRET_SENTINEL_...
C:\private\repo\...
/home/user/private/...
```

feed them only into places under test and assert they do not appear in:

```text
MCP response where forbidden
logs
export fields where forbidden
error messages
```

Also test injection-like goal/summary strings remain inert data.

Architecture reflection/source tests reject broad fields named like:

```text
metadata
context
payload
sourceCode
diff
rawLog
environment
workspacePath
```

in MCP input contracts unless an explicit reviewed exception exists.

---

## 12. Architecture tests

Minimum rules:

```text
Domain references no EF/MCP/CLI/ASP.NET
Application references no MCP/EF implementation
MCP inventory == canonical 4
no assembly-wide MCP tool scan call
all MCP input schemas strict
no DbContext in Razor components later
CPM owns package versions
no runtime project references test-only packages
```

Use direct reflection/MSBuild/project-file checks first. Add ArchUnitNET/NetArchTest only if the hand-written checks become materially harder to maintain.

---

## 13. Agent evaluations

This is the principal new quality layer learned from mature MCP products.

### Why

A schema can be perfectly valid while Codex:

- starts too many quests;
- never finishes;
- calls `get_card` unnecessarily;
- sends oversized/forbidden content;
- misreads “meaningful task” guidance;
- uses the wrong tool after a description change.

Unit tests cannot detect those behaviors.

### Initial eval corpus

At least:

1. meaningful coding task -> start once, finish once;
2. debugging task -> correct type/lifecycle;
3. planning task -> lifecycle works without code changes;
4. tiny factual question -> no quest expected;
5. existing matching open quest -> no duplicate;
6. conflicting open quest -> model recovers deliberately;
7. model reconnect/context recovery -> current quest if needed;
8. finish retry -> no duplicate reward;
9. privacy adversarial task -> no code/diff/raw log passed;
10. user explicitly asks to show card -> get_card only as needed.

### Eval scoring

Machine-observable dimensions:

```text
tool call sequence
tool count
argument schema conformance
forbidden-field/content sentinel absence
quest state result
XP-event count
final displayText presence/duplication
```

Human review dimension:

```text
Was use of Hero Passport helpful rather than intrusive?
```

### Release policy

- initially non-blocking/nightly/manual on supported Codex configuration;
- record model/client/version/config with result;
- changes to tool names/descriptions/server instructions require rerun;
- a consistent regression blocks the MCP UX change even if unit tests pass.

Do not tune core reward rules to make an eval model behave differently; fix tool/instruction ergonomics separately.

---

## 14. Real Codex E2E

0.1.0 requires installed-tool end-to-end validation with the current supported Codex path.

Test flow:

```text
fresh isolated HERO_PASSPORT_HOME
install/build hero-passport command
register via native Codex MCP configuration/CLI
verify codex mcp list
run representative meaningful task
observe start
complete work
observe finish
query card/current state
restart Codex/server
verify durable state
```

Record exact Codex build/version used in release evidence.

Test both default process cwd workflow and explicit Codex `mcp_servers.hero-passport.cwd` configuration where supported.

---

## 15. Cross-platform matrix

Release qualification:

```text
Windows x64  P0 (primary development environment)
Linux x64    P0
macOS arm64  P0 before claiming macOS support
macOS x64    best-effort/CI depending available runner
```

At minimum test:

```text
build/test
init/paths
SQLite native load/version
WAL
CLI
MCP stdio process
self-contained/tool packaging form being shipped
```

Do not claim a RID/platform supported solely because `dotnet publish` succeeds.

---

## 16. Dependency/reproducibility gates

Repository:

```text
global.json exact SDK
Directory.Packages.props
packages.lock.json
locked restore in CI/release
NuGet audit including transitive packages
```

Release fails on known high/critical vulnerability findings. Moderate findings require explicit review rather than silent ignore.

Verify runtime native SQLite with `SELECT sqlite_version()`.

Preview packages are rejected unless ADR-approved.

---

## 17. Static/build quality

Baseline project properties:

```text
Nullable=enable
ImplicitUsings=enable
AnalysisLevel=latest compatible with pinned SDK
EnforceCodeStyleInBuild=true
Deterministic=true
ContinuousIntegrationBuild=true in CI
```

`TreatWarningsAsErrors` policy:

Prefer enabling it early for product projects once SDK/bootstrap warnings are clean. If a warning must be suppressed, use the narrowest scope and document why; do not disable analyzer classes globally to make CI green.

Format check:

```text
dotnet format --verify-no-changes
```

or the current pinned .NET equivalent selected in implementation.

---

## 18. Coverage philosophy

Do not chase a single repository-wide percentage.

Require high branch/boundary coverage for:

```text
reward engine
level curve
trust/risk
traits
state transitions
idempotency
MCP validation
migration/data integrity
```

Presentation/bootstrap glue can be lower if process tests cover it.

A line-coverage number never substitutes for golden/boundary tests.

---

## 19. Release gates for 0.1.0

All must pass:

```text
restore locked
build Release
format/static analysis
deterministic unit tests
real SQLite integration tests
migration tests + pending model check
architecture/privacy tests
CLI process tests
MCP manifest/schema/stdout tests
MCP Inspector smoke
NuGet audit policy
Windows/Linux/macOS qualified matrix as claimed
real Codex E2E
agent-eval review with no unresolved lifecycle/privacy regression
packaging/install/uninstall smoke
docs consistency review
```

No dashboard requirement for 0.1.0.

---

## 20. Documentation consistency test/review

Before release, search for stale contract terms:

```text
workspacePath in MCP input
per-call schemaVersion/outputMode/locale
agentHint/statusText
SQLite under %APPDATA%
custom migration mutex
async SQLite requirement
achievements in MVP
HTTP MCP in MVP
```

Any stale normative statement must be removed or explicitly marked historical.

## 21. Primary sources

See `REFERENCES.md`, particularly:

- official MCP specification/C# SDK/Inspector;
- Sentry MCP evaluation practices;
- EF Core migration/SQLite docs;
- Microsoft.Data.Sqlite docs;
- official Codex MCP/config documentation.
