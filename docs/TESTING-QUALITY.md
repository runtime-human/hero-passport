# Hero Passport — Testing and Quality

**Status:** Accepted v3.1  
**Snapshot:** 2026-08-11

Deep-dive test vectors in `PROJECT-IDENTITY.md`, `PERSISTENCE-RELIABILITY.md` and `WIRE-CONTRACT.md` are release requirements, not optional examples.

---

## 1. Evidence model

Hero Passport requires evidence at eight levels:

```text
Domain rules
Application semantics
Project identity/binding
SQLite persistence/concurrency/crash/backup
HP-MCP wire contracts
MCP process/protocol compatibility
Host integration
Agent behavior evals
```

Passing unit tests alone does not prove persistence races, protocol compatibility or model tool-selection behavior.

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

Infrastructure tests use real temporary file-backed SQLite for DB guarantees.

AgentEvals may have manual/nightly/slower runners and remain separate from deterministic unit expectations.

---

## 3. Domain tests

Cover:

```text
reward bases/multipliers/bonuses/penalties
minimum zero
95-XP clean-coding golden
level thresholds/boundaries
skill XP exact conservation
SkillKeyNormalizer for non-MCP adapters
Trust/Risk clamp/transitions
trait unlocks
checked/max numeric behavior
```

RPG rule versions do not change merely because transport architecture changed.

---

## 4. SafeTextV1 tests

`WIRE-CONTRACT.md` vectors are required.

At minimum:

```text
ASCII/Russian normal text
NFC equivalence
emoji/supplementary scalar counts as one
unpaired surrogate rejection
NUL/DEL/C1 rejection
bidi formatting-control rejection
tab/newline/whitespace collapse
1/500/501 goal boundaries
1/2000/2001 summary boundaries
```

Do not use `.Length` assertions as the only Unicode length proof.

Reward clear-summary threshold is calculated from normalized SafeTextV1 scalar length.

---

## 5. QuestDedupKeyV1 tests

The retired `LogicalQuestKeyV1` case-fold behavior must not reappear.

Golden cases:

```text
same SafeText declaration -> same key
whitespace-equivalent -> same key
NFC-equivalent -> same key
case difference -> DIFFERENT key
quest type difference -> different key
punctuation difference -> different key
```

Changing these goldens requires a dedup-key version/contract review.

---

## 6. Application tests

Use small fakes/stubs for ports and deterministic `TimeProvider`; these do not prove SQLite behavior.

### StartQuest

```text
new declaration -> new quest
matching open declaration -> same quest + AlreadyOpen
same arguments after prior quest finished -> NEW quest
case-different goal -> distinct quest
distinct quests coexist
16 active -> HP133
InvocationOrigin does not alter behavior/reward
```

### FinishQuest

```text
all result categories
unknown UUID -> HP130
wrong hero -> HP134
wrong project -> HP134
already finished -> original persisted outcome
metrics cross-field validation
canonical skill input semantics
```

### ListActiveQuests

```text
empty -> [] success
only current hero/project
max 16
StartedAtUtc DESC, QuestId ASC
```

### Card

```text
global hero + bound project projection
no project ID/fingerprint/path
stable top-skill/trait ordering
```

---

## 7. Project identity test suite

Use real temporary Git repositories/worktrees/submodules where feasible; do not mock Git for the integration-level identity proof.

Required vectors from `PROJECT-IDENTITY.md`:

### Normal Git

```text
repo root cwd -> scope .
nested cwd -> same fingerprint
spaces/Unicode path
literal path beginning with '-'
```

### Linked worktrees

```text
main + linked worktree -> same fingerprint
same explicit repo-relative scope in both -> same fingerprint
private git-dir differences do not affect identity
```

### Monorepo

```text
nested cwd with no explicit root -> whole repo identity
explicit services/a -> separate scoped identity
explicit services/b -> different scoped identity
```

### Submodules/nested repos

```text
submodule -> distinct from superproject
nested independent repo -> distinct from parent
explicit parent binding overrides nested cwd when parent itself is selected
```

### Standalone/path

```text
ordinary non-Git directory stable
final symlink/junction resolves where supported
file/nonexistent -> HP310
```

### Git safety/errors

```text
bare repo -> HP313
Git missing + repo marker -> HP312
Git missing + no marker -> standalone allowed
unsafe/unreadable Git repo -> HP311
no test observes Hero Passport adding safe.directory
```

### Privacy

```text
DB has salted fingerprint, no absolute path
normal error/log does not expose absolute path
remote URL never queried/persisted
```

Known limitations are tested/documented:

```text
repository metadata move -> new v1 fingerprint
fresh clone -> distinct local project
```

---

## 8. Real SQLite qualification

Never use EF InMemory to prove:

```text
transaction locking
partial unique indexes
WAL
busy/timeout
crash recovery
backup
migration locking
```

Every test gets isolated real file-backed state and approved native provider.

Verify:

```text
fresh migration
upgrade fixture
foreign_keys=ON
journal_mode=WAL
synchronous=FULL
actual sqlite_version()
qualified SQLite floor >=3.51.3
open dedup partial index
UNIQUE quest_reports.quest_id
UNIQUE xp_events.quest_id
active query plan/index
```

---

## 9. Provider transaction behavior test

Release-blocking proof for selected Microsoft.Data.Sqlite version:

```text
BeginTransaction(IsolationLevel.Serializable)
=> non-deferred writer semantics
=> second writer cannot pass invariant reads concurrently
```

Where practical, use SQLite tracing/locking behavior/second-connection observation rather than an assertion coupled only to implementation source text.

If an upgraded provider changes the behavior, either preserve immediate writer acquisition explicitly or re-open the ADR.

---

## 10. Start race tests

### Same dedup declaration

Two independent contexts concurrently start the same hero/project/type/SafeText goal.

Expected:

```text
one open row
same questId returned
one AlreadyOpen=false, later caller AlreadyOpen=true
no raw constraint leak
```

### Active-cap race

Fixture:

```text
15 open quests
writer A -> new dedup A
writer B -> new dedup B
```

Expected:

```text
exactly one new row
final open count ==16
other caller -> HP133
no transient committed count 17
```

This must not be relaxed to `<=17` or “usually 16”.

### Busy timeout

Hold writer beyond configured bound with a test connection.

Expected after provider timeout:

```text
HP202 database_busy
no raw path/SQL
no additional Polly retry delay
```

---

## 11. Finish race tests

Two independent writers finish one quest.

After both:

```text
one quest_report
one xp_event
one aggregate mutation
finished quest exactly once
both observable outcomes same persisted reward
```

Unique constraints remain backstops but ordinary race does not leak SQLite constraint errors.

---

## 12. Child-process crash tests

In-process exception injection is insufficient for journaling claims.

Use child processes + same file DB + test-only fault points.

Minimum:

```text
Start after insert before commit -> kill -> no open row after recovery
Finish after report/xp writes before commit -> kill -> no partial progression
Finish after COMMIT before response -> kill -> retry returns original committed outcome
killed process leaving WAL -> fresh SQLite open recovers without manual WAL deletion
```

Never make a crash test pass by deleting `-wal`/`-shm`.

---

## 13. Storage error mapping tests

Exercise safely through fixtures/test seams:

```text
BUSY/LOCKED timeout -> HP202
FULL -> HP203
READONLY -> HP204
IOERR/CANTOPEN -> HP205
CORRUPT/NOTADB -> HP206
unexpected constraint -> HP207
unqualified SQLite version fixture -> HP208
known unsupported network location -> HP211 where deterministic
```

Error text must not leak SQL/paths/connection strings.

---

## 14. Backup tests

Approved live backup path uses SQLite BackupDatabase.

Tests:

```text
source active while backup runs
backup independently opens
PRAGMA quick_check passes
PRAGMA foreign_key_check empty
migration/schema readable
live source remains usable
writer blocking is bounded/observed
implementation contains no raw live-DB File.Copy path
```

Logical export tests are separate; export is not a physical backup claim.

---

## 15. WAL/checkpoint tests

Verify policy rather than overmanage WAL:

```text
default autocheckpoint remains active
no per-finish TRUNCATE/RESTART checkpoint
short readers do not stay open after result materialization
long-reader fixture can demonstrate WAL growth/checkpoint limitation
```

Doctor may observe checkpoint/WAL health but must not disruptively checkpoint by default.

---

## 16. HP-MCP contract tests

`HeroPassport.Contract.Tests` generates/compares actual SDK registration.

Required:

```text
exact 4 names/order
no accidental fifth tool
start annotation idempotent=false
finish/list/card idempotent=true
other annotations exact
closed input/output schemas
all required fields
canonical enum lists
UUID schema/runtime contract
SafeText bounds
skill enum/order constraints
metrics cross-field validation
forbidden field deny-list
```

Generated snapshots:

```text
contracts/mcp/hp-mcp-2/
```

Do not hand-edit to hide drift.

---

## 17. Success result compatibility tests

For every successful tool call:

```text
structuredContent exists
exactly one TextContent exists
TextContent is valid minified JSON
parse(TextContent.text) deep-equals structuredContent
structuredContent validates against outputSchema
displayText exists inside result and stays within tool bound
```

The old human-only TextContent fallback is a stale-contract failure.

---

## 18. Error result tests

For validation/business failures:

```text
isError=true
exactly one safe TextContent
structuredContent absent
no output-schema violation
no raw exception/SQL/path/request dump
```

Unknown tool/malformed protocol remains protocol error, not fabricated HP business result.

---

## 19. Wire canonicalization tests

Required from `WIRE-CONTRACT.md`:

```text
canonical lowercase UUIDv7 accepted
uppercase UUID input rejected
UUIDv4 rejected for questId
malformed UUID -> HP100
valid unknown v7 -> HP130

Timestamp exactly YYYY-MM-DDTHH:mm:ss.fffZ
higher precision producer time truncates to milliseconds

long-lived JSON integers never exceed 9_007_199_254_740_991

canonical MCP skills only
alias like `code` rejected by MCP boundary
1..3 unique skills
input semantic order preserved in skill allocation output

testsStatus passed/failed/unknown + testsMentioned=false rejected
not_run with true/false accepted
```

---

## 20. Stale-contract architecture scan

Fail active product code/normative docs that reintroduce:

```text
hero.current_quest
CurrentQuestTool/GetCurrentQuestHandler
one-open-quest-per-project
HP132 normal quest conflict
LogicalQuestKeyV1 as active type
logical_key persistence as active schema
case-folded quest goal dedup
start idempotentHint=true
human-only unrelated TextContent for structured success
workspacePath MCP field
per-call schemaVersion/locale/outputMode/agentHint
raw live SQLite File.Copy backup
manual WAL/SHM deletion recovery
```

Historical/superseded text is allowed only when clearly marked.

---

## 21. Protocol compatibility

Required official SDK paths:

```text
MCP 2026-07-28
MCP 2025-11-25
```

Both must see equivalent four-tool business semantics.

2025 compatibility particularly verifies that JSON TextContent remains sufficient when structured rendering is not available/used by a host.

Ordinary server must not hard-pin `ProtocolVersion`.

---

## 22. MCP process tests

Spawn built executable:

```text
hero-passport mcp --project-root <temp project>
```

Verify:

```text
stdout protocol framing only
safe stderr diagnostics only
clean shutdown
HERO_PASSPORT_HOME isolation
real migrations/project identity
no startup banner on stdout
```

---

## 23. MCP Inspector and Codex

Official MCP Inspector release smoke:

```text
tools/list
schemas
start
list active
finish
card
error representation
```

Codex is first automated Qualified host:

```text
project binding
one normal lifecycle
restart/recovery
parallel distinct quests
same-declaration retry
finished retry
```

---

## 24. Cross-host release smoke

According to `integrations/README.md`:

```text
VS Code
JetBrains AI Assistant
Zed
Cursor
Claude Code
```

Record:

```text
host/version
OS
transport
project binding method
tools listed
lifecycle result
known caveat
verified date
```

A configuration example alone never becomes a Qualified support claim.

---

## 25. Agent evaluations

AgentEvals measure tool selection/orchestration, not deterministic math.

Scenarios:

```text
meaningful task -> start + finish
trivial factual question -> no quest
same open declaration -> retry/reuse
same declaration after finish -> new quest when a new work cycle begins
parallel distinct work -> distinct quest
lost questId -> list_active_quests
restart/handoff -> recover explicit quest
finish retry -> no duplicate reward
privacy adversarial prompt -> no forbidden fields
```

Scenario definition stays host-neutral; Codex runner first.

---

## 26. Architecture/dependency/privacy gates

Static/fitness checks:

```text
layer references
no assembly-wide tool scanning
no unapproved dependency versions
no ModelContextProtocol.AspNetCore in 0.1
no MCP schema forbidden fields
no hard ProtocolVersion pin
no session-dependent app state
no path persistence
no stale v3 contract terms listed above
```

NuGet vulnerability audit is part of dependency qualification.

---

## 27. Release evidence rule

Before claiming 0.1.0 is complete, record exact executed commands/artifacts and inspect results.

Do not infer a pass from documentation completeness.

Current architecture PR contains no product implementation, so none of these product tests are claimed as already passing.
