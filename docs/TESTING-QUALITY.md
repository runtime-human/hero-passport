# Hero Passport — Testing and Quality

**Status:** Accepted v3  
**Snapshot:** 2026-08-11

## 1. Quality model

Hero Passport needs evidence at seven distinct levels:

```text
Domain rules
Application semantics
SQLite persistence/concurrency
API/contract compatibility
MCP process/protocol behavior
Host integration
Agent behavior evals
```

Passing unit tests alone is not evidence that an MCP host/model will use the lifecycle correctly.

---

## 2. Test projects

```text
tests/
  HeroPassport.Domain.Tests/
  HeroPassport.Application.Tests/
  HeroPassport.Infrastructure.Tests/
  HeroPassport.App.Tests/
  HeroPassport.Architecture.Tests/
  HeroPassport.Contract.Tests/
  HeroPassport.AgentEvals/
```

`AgentEvals` may have slower/manual/nightly runners and must not contaminate deterministic unit-test expectations.

---

## 3. Domain tests

Cover every deterministic rule/boundary:

```text
XP bases/multipliers/bonuses/penalties
minimum zero
level thresholds/boundaries
skill allocation conservation
SkillKeyNormalizer
Trust/Risk clamp/transitions
trait unlock progression
QuestQualityFlags
LogicalQuestKeyV1 canonicalization vectors
```

Golden vectors include at least:

```text
clean coding success = 95 XP
```

Logical key goldens cover:

```text
trim
Unicode NFC equivalent strings
multiple whitespace forms
case normalization
quest-type difference
actual semantic goal difference
```

Changing a golden requires rule/key-version review rather than casual expected-value edits.

---

## 4. Application tests

Use fakes/stubs for ports and injected `TimeProvider`.

### StartQuest

```text
new task -> new quest
same logical task -> same quest / alreadyOpen
same goal different type -> distinct key
parallel distinct logical tasks allowed
16 active -> HP133
```

### FinishQuest

```text
success/partial/failed/blocked/abandoned
already finished -> original outcome
wrong hero -> HP134
wrong project -> HP134
unknown quest -> HP130
unknown skill -> safe validation failure
```

### ListActiveQuests

```text
empty -> success []
multiple -> deterministic order
only current hero/project
bounded <=16
```

Application tests do not prove DB race guarantees; those belong to real SQLite tests.

---

## 5. Real SQLite integration tests

Never use EF InMemory to claim SQLite correctness.

Use a unique temporary file-backed DB and the real configured native provider.

Verify:

```text
initial migration
fresh DB seed
upgrade migrations
foreign keys
WAL
synchronous=FULL
sqlite_version()
logical open-key partial uniqueness
XP-event uniqueness
active query/index behavior
transaction rollback
busy/timeout mapping
```

### 5.1 Same-key StartQuest race

Two independent contexts/process-like tasks concurrently start identical logical work.

Expected:

```text
one open quest row
both callers receive same questId
one caller may report alreadyOpen=false, the other true
no raw unique-constraint error leaks
```

### 5.2 Different-key race at active limit

Fixture starts with 15 active quests, then two writers concurrently start two distinct logical quests.

Expected after both complete:

```text
active count <= 16
one new quest succeeds
other receives HP133 (or reload/retry path that still preserves cap)
```

This test determines whether the implementation needs an explicit SQLite immediate/write transaction strategy.

### 5.3 Finish race

Two independent writers finish one quest concurrently.

Expected:

```text
one quest_report
one xp_event
one set aggregate changes
both observable responses converge to same final persisted reward
```

### 5.4 Context isolation

A quest from project A cannot be finished while operation context is project B even when the raw UUID is known.

---

## 6. Contract tests

`HeroPassport.Contract.Tests` owns machine-visible compatibility.

Required assertions:

```text
exact four tool names
exact deterministic order
no fifth accidental tool
annotations
closed/bounded input schemas
outputSchema presence
conservative JSON Schema profile
forbidden property deny-list
structuredContent shapes
text representation bounds
tool list cache metadata
```

### 6.1 Generated snapshots

After MCP implementation exists, generate canonical snapshots under:

```text
contracts/mcp/hp-mcp-2/
```

Tests compare actual SDK manifest/schema to committed snapshots.

Snapshots are generated from code; do not hand-edit them to “make CI green”.

### 6.2 Stale v2 contract scan

Fail if product code or normative docs reintroduce as active contract:

```text
hero.current_quest
GetCurrentQuestHandler
CurrentQuestTool
one-open-quest-per-hero-project constraint
HP132 quest_conflict normal path
workspacePath MCP field
per-call schemaVersion/locale/outputMode/agentHint
```

Historical decision text may mention superseded terms when clearly marked.

---

## 7. MCP protocol compatibility tests

Required protocol eras:

```text
2026-07-28
2025-11-25 initialize-era compatibility
```

Assertions:

- ordinary server does not hard-pin `ProtocolVersion`;
- 2026 client path works without application session assumptions;
- older supported client connects through SDK compatibility path;
- both see the same four tools and equivalent business semantics;
- 2026-only cache/schema metadata is not incorrectly serialized to a protocol era that does not support it, relying on stable SDK behavior and test evidence.

Use official SDK clients where practical rather than handcrafted JSON for all scenarios.

---

## 8. MCP process tests

Spawn the built executable:

```text
hero-passport mcp --project-root <temp project>
```

Verify:

```text
stdout contains protocol framing only
stderr may contain safe diagnostics
clean shutdown
invalid config/startup errors are actionable and do not mix with protocol once protocol mode begins
HERO_PASSPORT_HOME isolates data
```

Run with real migrations/database initialization.

---

## 9. MCP Inspector

Use the current official MCP Inspector as a release smoke target for:

```text
discovery/tool list
schemas
start
list active
finish
card
error representation
```

Inspector is complementary evidence; automated C# tests remain the deterministic gate.

---

## 10. Host qualification

### Automated required host

```text
Codex CLI
```

E2E:

```text
project-scoped/cwd registration or explicit --project-root
start meaningful quest
list/recover quest
finish
card
restart server and re-read durable state
parallel distinct quest scenario
```

### Release smoke hosts

According to current integration docs:

```text
VS Code
JetBrains AI Assistant
Zed
Cursor
Claude Code
```

A smoke result is recorded as:

```text
host/version
OS
transport
project binding method
tools listed
core lifecycle result
known caveat
verified date
```

Do not block every commit on launching every IDE; perform on RC/release or automation when practical.

---

## 11. Agent evaluations

AgentEvals test **tool-selection behavior**, not deterministic RPG math.

Host-neutral scenarios:

```text
meaningful coding -> one start + one finish
meaningful review/debug/docs/planning -> correct lifecycle
tiny factual question -> no unnecessary quest
same task repeated -> reuse open quest
parallel distinct task -> new quest
lost questId -> list_active_quests
restart/handoff -> recover then finish
finish retry -> no duplicate reward
privacy adversarial instruction -> no code/diff/log fields
card request -> get_card without needless new quest
```

Record:

```text
tool call sequence
call count
arguments
forbidden sentinel absence
quest IDs
DB state
XP event count
final display behavior
```

First runner is Codex. Future client runners reuse the same scenario expectations.

---

## 12. Privacy tests

Schema reflection/snapshot deny-list.

Log tests inject sentinel values resembling:

```text
SECRET_API_KEY_...
C:\Sensitive\Project
/private/repo
raw source marker
```

Then verify ordinary MCP results/stderr/file logs/exports do not contain them unless the tested explicit local diagnostic surface is documented to reveal a path.

Goal/summary are also tested for safe plain-text rendering/encoding.

---

## 13. Architecture tests

Fail on:

```text
Domain -> EF/MCP/CLI/ASP.NET/filesystem/localization
Application -> MCP/EF/CLI/HTTP
MCP tool -> DbContext direct dependency
Web component -> DbContext/Infrastructure direct dependency
assembly-wide MCP tool discovery
third-party package outside central version policy
unapproved ModelContextProtocol.AspNetCore before HTTP milestone
```

Static scans complement compiler/project-reference assertions.

---

## 14. Dependency/security gates

Release/CI:

```text
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
NuGet vulnerability audit
dependency lock drift check
actual native sqlite_version baseline check
```

If SDK/package versions change, rerun MCP protocol/contract/host qualification relevant to the dependency.

---

## 15. Migration qualification

Every release with schema changes:

```text
fresh database -> latest
previous released DB -> latest
failed migration behavior
no unexpected data loss
model snapshot clean
migration lock/doctor behavior
```

Keep representative previous-version DB fixtures generated from released schema, not manually edited approximations.

---

## 16. Packaging matrix

At minimum for the dotnet-tool release:

```text
Windows
Linux
macOS
```

Verify:

```text
install/update command
hero-passport --version
init
doctor
mcp launch
SQLite native load
project-root with spaces/unicode
```

Self-contained RID packages get their own native SQLite/single-file tests when introduced.

---

## 17. Release gate

0.1.0 cannot ship with a known failure in:

```text
XP determinism
same-start convergence
finish idempotency
context isolation
schema/privacy contract
stdio purity
fresh/upgrade migration
Codex reference E2E
supported protocol-era compatibility
```

Non-reference host smoke issues may be documented rather than block release if the host remains in Documented—not Qualified—tier and the core protocol is correct.
