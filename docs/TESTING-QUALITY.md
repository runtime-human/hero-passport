# Hero Passport — Testing and Quality Strategy

**Status:** Accepted v3.2  
**Snapshot:** 2026-08-11

## 1. Principle

Every architectural promise must have executable evidence at the layer where it can actually fail.

```text
pure game rule -> Domain test
use-case semantics -> Application test
SQLite invariant -> real SQLite integration/concurrency/crash test
MCP field/schema -> contract snapshot/vector test
Agent lifecycle -> Agent Skill eval
installation -> packaged smoke/E2E
```

A green unit-test suite alone is not release qualification.

## 2. Test projects

```text
tests/HeroPassport.Domain.Tests/
tests/HeroPassport.Application.Tests/
tests/HeroPassport.Infrastructure.Tests/
tests/HeroPassport.App.Tests/
tests/HeroPassport.Architecture.Tests/
tests/HeroPassport.Contract.Tests/
tests/HeroPassport.AgentEvals/
```

## 3. Domain goldens

Commit stable vectors for:

```text
reward/2.0.0
skill-allocation/1.0.0
hero-progression/2.0.0
skill-progression/2.0.0
rank/1.0.0
trust-strain/1.0.0
streak/1.0.0
unlock/2.0.0
SafeTextV1
```

Required properties:

```text
same input/version -> same output
XP never negative
integer-only outcome arithmetic
Skill XP allocation sums exactly to Quest XP
threshold edges exact
Trust/Strain clamp
abandoned neutral
unlock monotonicity
active Title priority deterministic
checked numeric ceilings
```

## 4. Application tests

Cover typed semantic operations independent of MCP:

```text
first-run configure + initial Hero
post-setup config allowlist
create/activate/archive/restore/delete guards
Quest binds active Hero at start
active Hero switch does not move Quest
Finish uses persisted Quest Hero
one-open conflict semantics
retry receipt same/different args
already-finished retry returns persisted outcome
project context mismatch
```

## 5. SQLite integration

Use temporary **file-backed** SQLite, not EF InMemory, for:

```text
partial unique indexes
FK/cascade behavior
WAL/pragmas
actual loaded sqlite_version()
non-deferred writer semantics
busy timeout
concurrent Start race
concurrent Finish race
mutation receipt atomicity
permanent delete transaction
migrations
backup
```

## 6. Concurrency vectors

At minimum:

### Start

- same `startRequestId` + same args concurrently -> one Quest, replay semantics;
- same ID + changed args -> one success + HP135 path, no second Quest;
- two different start IDs same Hero+Project -> exactly one open Quest, other HP133;
- different Heroes same Project -> independent open Quests allowed;
- same Hero different Projects -> independent open Quests allowed.

### Finish

- two identical Finish calls -> one progression event;
- two conflicting Finish payloads -> first committed persisted report wins; later retry cannot rewrite;
- Finish after active Hero switch -> original Quest Hero receives XP;
- unique report/xp ledger constraints stay intact.

## 7. Crash injection

Use child-process fixtures that terminate at controlled persistence points.

Required:

```text
Start crash before commit -> no Quest/receipt partial
Start crash after commit before response -> same request replay recovers
Finish crash before commit -> Quest remains open/no partial progression
Finish crash after commit before response -> retry returns persisted finish
Delete crash before commit -> Hero/history remain
Delete crash after commit before response -> receipt provides safe retry
```

Never “recover” by deleting WAL/SHM.

## 8. Migration tests

Every released schema:

```text
empty -> latest
previous release fixture -> latest
schema snapshot/model diff
partial index preserved
FK/cascade review
quick_check + foreign_key_check
runtime data sanity
```

If a migration can destroy/rebuild data, test a representative populated fixture and document backup/recovery.

## 9. MCP contract snapshots

Implementation generates authoritative snapshots under:

```text
contracts/mcp/hp-mcp-2/
```

Assert:

```text
11 exact tools in exact order
annotations exact
input/output schemas exact
additionalProperties:false
bounds/enums exact
request-ID fields exact
forbidden-field absence
success structuredContent shape
exactly one compatibility TextContent
semantic JSON equality
error isError/no-structuredContent shape
```

Do not maintain a separate hand-written runtime schema implementation merely to satisfy snapshots.

## 10. MCP protocol qualification

Exercise:

```text
MCP 2026-07-28 preferred path
2025-11-25 compatibility/fallback path
stdio framing with stdout purity
MCP Inspector smoke
```

No Quest correctness test may assume a protocol session.

## 11. Agent Skill evals

Build a deterministic prompt/scenario fixture set around orchestration decisions, not subjective prose quality.

Minimum scenarios:

```text
short factual question -> no start
meaningful implementation -> start
same-goal followups -> no fragmentation
await user input -> no finish
complete result -> finish
completed goal then new goal -> finish + start
explicit mid-work switch with useful result -> partial + start
explicit mid-work switch no useful result -> abandoned + start
ambiguous switch -> no silent close
restart same goal -> recover same questId
restart different goal -> recovery choice
transport retry -> reuse request ID
observed vs reported evidence
Hero Passport calls do not self-award tool_use
milestone flavor never changes numbers
```

Measure false-positive starts and premature finishes explicitly; conservative behavior is desired.

## 12. Localization tests

```text
ru-RU complete
en-US complete
same semantic keys in both
format placeholders match
no missing/extra placeholders
SafeText output remains valid
Quest locale snapshot respected
locale change does not mutate history
```

## 13. Privacy/architecture tests

Static/contract scans:

```text
Domain references no EF/MCP/CLI/localization packages
Application references no MCP SDK
MCP DTOs contain no path/remote/source/diff/log/prompt fields
entities contain no forbidden telemetry columns
stdout protocol-only under stdio
AGENTS/docs links resolve
```

## 14. Packaging/E2E

Reference E2E host: Codex.

Packaged binary flow:

```text
fresh HERO_PASSPORT_HOME
first-run setup
create/activate Hero
open real temporary Git repo
Skill/agent start
finish clean coding golden
restart server
recover history/card
archive/restore secondary Hero
backup/doctor
```

Cross-host smoke validates current integration instructions/Skill packaging for supported hosts; failures become documented compatibility status, not hidden assumptions.

## 15. Dependency qualification

When upgrading MCP SDK, EF/Microsoft.Data.Sqlite, SQLite native bundle or .NET SDK, rerun the affected contract/concurrency/migration suite before accepting the bump.

Runtime SQLite check must confirm >=3.53.4 regardless of NuGet metadata.

## 16. Release checklist

No 0.1 release unless:

```text
unit/integration/contract tests green
concurrency and crash suites green
migration fixtures green
MCP 2026+legacy qualification green
Agent Skill eval thresholds accepted
RU/EN complete
privacy scans green
packaged Codex E2E green
cross-host matrix recorded
doctor validates runtime SQLite/pragmas
```
