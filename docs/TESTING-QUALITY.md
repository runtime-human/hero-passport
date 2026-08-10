# Hero Passport — Testing and Quality Strategy

**Status:** Accepted  
**Baseline:** .NET 10 + xUnit.net v3 + Microsoft Testing Platform  
**Primary command:** `dotnet test`

## 1. Quality model

Hero Passport is small enough that correctness should be enforced primarily through fast deterministic tests rather than manual end-to-end checking.

Risk priority:

```text
P0  reward correctness / double-award / protocol stdout / persistence integrity
P1  schema compatibility / migrations / privacy / cross-platform paths
P2  presentation formatting / dashboard read models
P3  performance tuning / optional packaging variants
```

## 2. Test stack

Approved baseline:

```text
xunit.v3                         3.2.2
xunit.runner.visualstudio        3.1.5 (compatibility during transition)
.NET 10 dotnet test
Microsoft Testing Platform       selected in global.json
```

xUnit v4 remains prerelease as of the baseline date and is not used.

While all supported local/CI environments are standardized on Microsoft Testing Platform, the VS/VSTest adapter may be removed later. Until then keep compatibility dependencies private where required by tooling.

## 3. Test project responsibilities

### `HeroPassport.Domain.Tests`

Pure, very fast tests:

- reward formulas;
- level curve;
- skill normalization/allocation;
- trust/risk;
- trait progress;
- state-transition invariants;
- value objects/IDs;
- golden fixtures.

No database/filesystem/process.

### `HeroPassport.Application.Tests`

Use-case behavior with fakes/in-memory test ports:

- start/finish branching;
- validation/error mapping;
- idempotency orchestration logic;
- project/hero resolution selection;
- deterministic output projection;
- export model creation;
- time behavior with injected `TimeProvider`.

Do not duplicate Domain formula tests here.

### `HeroPassport.Infrastructure.Tests`

Use **real SQLite** in isolated temp files:

- migrations;
- constraints/indexes;
- transaction rollback;
- race/idempotency defenses;
- WAL/foreign-key configuration;
- project fingerprint persistence;
- JSON export filesystem behavior;
- package/native SQLite runtime floor.

Do not use EF Core InMemory as a substitute for SQLite semantics.

### `HeroPassport.App.Tests`

Executable/protocol surface:

- CLI parsing/help/exit codes;
- `init` idempotency;
- `doctor` behavior;
- MCP tool registry/order/descriptions;
- MCP request/response mapping;
- child-process stdout purity;
- stderr diagnostics;
- no Spectre/normal CLI output in MCP mode.

### `HeroPassport.Architecture.Tests`

Fitness functions:

- project dependency direction;
- Domain cannot reference EF/MCP/CLI/ASP.NET packages/namespaces;
- Application contracts cannot expose Infrastructure/EF types;
- future Web components cannot depend on `HeroPassportDbContext`;
- no prohibited raw-data contract properties;
- central package versioning policy checks when feasible.

Use simple reflection/MSBuild/file checks before adding a heavy architecture-test library.

## 4. Golden tests

Golden fixtures are first-class compatibility assets.

Required initial fixtures:

```text
reward/coding-success-clean-95.json
reward/failed-with-violations.json
reward/zero-clamped.json
skills/three-skills-95.json
levels/boundaries.json
trust-risk/default-clean-success.json
mcp/start-quest-compact.json
mcp/finish-quest-compact.json
mcp/card-compact.txt
```

Rules:

- canonical JSON serialization/order where the product owns output;
- no timestamps/UUID randomness in golden values unless fixed test providers are injected;
- changing a golden requires explaining whether it is a product-contract change or a test correction;
- reward golden change must update `RuleVersions` when semantics change.

## 5. Determinism tests

For every rule engine:

- run the same input many times;
- run with culture `ru-RU`, `en-US` and invariant where practical;
- verify no culture-dependent decimal/string behavior changes scoring;
- verify no wall-clock/random/environment dependency;
- verify integer overflow is checked/handled at boundaries;
- verify output ordering of adjustments/skills/tools.

## 6. Idempotency tests

### Start

1. First request creates a quest and increments `quests_started` once.
2. Same idempotency key returns same quest.
3. Fallback automatic retry returns existing open quest.
4. Conflicting explicit new key while active quest exists -> conflict.
5. Concurrent equivalent starts -> one persisted active quest.

### Finish

1. First finish creates exactly one report and XP event.
2. Same request retry returns original persisted outcome.
3. Different retry payload after completion still returns original outcome, not a second/recomputed reward.
4. Two concurrent finishes -> one reward event; both callers can converge on same completed outcome.
5. Simulated failure before commit -> no partial reward/projection mutation.

## 7. Persistence tests

Minimum matrix:

```text
fresh DB migration
migration rerun/startup idempotency
seed idempotency
FK violations rejected
check constraints/bounds
unique project identity
unique quest reward event
filtered active-quest uniqueness
WAL active
foreign_keys ON
bounded busy timeout
no Cache=Shared when WAL is selected
reopen persisted DB
read model no-tracking query correctness
```

Once version `0.0.x` databases exist, keep upgrade fixtures under `tests/fixtures/db/<version>/` and test migration to current.

## 8. MCP protocol/process tests

### 8.1 In-process SDK tests

Use the official MCP C# client/server APIs where possible to:

- discover tools;
- assert exact names/order;
- call each tool;
- validate structured outputs;
- validate tool error mapping;
- exercise backward-compatible client/server path supported by the SDK where practical.

### 8.2 Stdout guard test

Launch the built `hero-passport mcp` child process with isolated temp data.

Assertions:

- no UTF-8 BOM/banner/whitespace/log line before first valid protocol frame;
- normal logger output appears only on stderr when enabled;
- a request/response exchange succeeds;
- graceful termination does not append decorative stdout.

This is a P0 regression test.

## 9. Privacy tests

Use sentinel strings:

```text
HP_TEST_SECRET_...
C:\Users\Sensitive\Repo
/home/private/repo
-----BEGIN PRIVATE KEY-----
```

Inject only into test request/environment/path contexts and assert they do not appear in:

- logs;
- project DB path fields (none should exist);
- MCP output except where a goal/summary echo is explicitly intended and safe;
- exports where prohibited;
- exception messages.

Use reflection/schema tests to fail if DTOs acquire forbidden properties such as `code`, `diff`, `rawLog`, `environment`, `fullPrompt`, `fileContents`.

## 10. Localization tests

- canonical keys remain language-neutral;
- `scope_control` renders Russian `Контроль`;
- reward key `clean_scope_bonus` renders `Бонус за контроль`;
- `scope_violation` renders `Выход за задачу`;
- unknown locale follows defined fallback;
- changing locale never changes numeric rewards or persisted canonical keys.

## 11. CLI tests

Required behaviors:

```text
--help exits 0
--version exits 0
invalid command exits usage error
init twice succeeds
status before init gives actionable result
mcp rejects/ignores pretty-output mode
export creates valid bounded JSON
 doctor reports DB/migration/native SQLite state
```

Avoid brittle full-console snapshots for dynamic terminal formatting. Snapshot/golden only stable semantic text blocks.

## 12. Cross-platform CI

On pull requests to `main`:

```text
ubuntu-latest
windows-latest
macos-latest
```

Each runs with pinned .NET SDK and locked restore.

Core gate:

```bash
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

Add format/analyzer gate once the repository scaffold exists:

```bash
dotnet format --verify-no-changes --no-restore
```

Do not make GitHub-hosted multi-platform jobs unnecessarily expensive for every docs-only change if path filters/CI policy can safely avoid it. Correctness code changes always run the full supported OS matrix before merge/release.

## 13. Static analysis/build settings

Initial repository policy:

```text
Nullable                  enable
ImplicitUsings            enable
AnalysisLevel             latest
EnforceCodeStyleInBuild   true
Deterministic             true
ContinuousIntegrationBuild true in CI
```

Introduce `TreatWarningsAsErrors=true` from the scaffold if the chosen package/toolchain is clean; if a third-party analyzer blocks bootstrap, suppress narrowly with justification rather than disabling analysis globally.

## 14. Dependency quality gates

- Central Package Management only.
- Commit lock files.
- Locked restore in CI.
- NuGet audit/vulnerability check at build/release.
- No accidental prerelease dependencies.
- Verify native SQLite version at runtime/integration test.
- Keep all Microsoft EF Core package versions aligned (`10.0.10` baseline).

## 15. Release qualification

Before tagging a release:

1. clean checkout;
2. locked restore;
3. Release build;
4. all tests on Windows/Linux/macOS;
5. pack .NET tool;
6. install tool into isolated temp tool path;
7. run `hero-passport init`;
8. launch MCP smoke client and complete start/finish/card flow;
9. verify standard 95 XP fixture;
10. verify second finish adds zero extra XP events;
11. export JSON and validate schema/privacy;
12. dependency audit;
13. migration smoke on previous release fixture (once applicable);
14. changelog/docs/version alignment.

## 16. Performance budgets

Do not optimize prematurely, but keep regression budgets for the local experience:

- domain reward calculation: effectively sub-millisecond; benchmark only if a regression appears;
- cold CLI `--help`: no DB migration work;
- MCP start: target human-imperceptible local startup; measure after implementation;
- start/finish DB operations: short transactions, no long-lived read locks;
- compact `displayText`: <= ~900 visible chars target;
- tool list: exactly four stable tools.

Benchmarks are not a P0 project until real measurements show a concern.

## 17. Definition of done for any implementation task

A task is done when:

- behavior has focused tests;
- relevant canonical docs are updated if contract/rules changed;
- no new prohibited dependency direction appears;
- no new privacy field/logging exposure appears;
- build/test gates pass locally for the relevant scope;
- implementation contains no placeholder/TODO for required behavior;
- migration/schema changes have real SQLite tests;
- reward/protocol changes have compatibility/version consideration.
