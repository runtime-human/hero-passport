# Hero Passport — Testing and Quality Strategy

**Status:** Accepted v3.2.1  
**Snapshot:** 2026-08-11

## 1. Principle

Every architectural promise needs executable evidence at the layer where it can fail.

```text
pure rule -> Domain test
use-case semantics -> Application test
SQLite invariant -> real file-backed SQLite integration/concurrency/crash test
MCP contract -> schema/snapshot/vector test
Agent orchestration -> Agent Skill eval
installation -> packaged host E2E
```

Green unit tests alone are not release qualification.

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
same canonical input/version -> same numeric/semantic output
XP never negative
integer-only multiplier arithmetic
Skill allocation conserves Quest XP
threshold/rank edges exact
Trust/Strain clamp
abandoned neutral
unlock monotonicity
active Title priority deterministic
checked JSON-safe ceilings
```

Flavor prose selection is not a Domain determinism test. Engine emits semantic milestone keys only.

## 4. Bootstrap tests

Application + real SQLite:

```text
fresh bootstrap -> one Hero + setup complete
same bootstrapRequestId + same args -> replay same Hero
same bootstrapRequestId + changed args -> HP135
fresh bootstrapRequestId after setup -> HP002
two concurrent bootstrap requests -> exactly one setup
crash before commit -> setup remains incomplete/no receipt
crash after commit before response -> same request replay recovers
```

## 5. Configuration/context tests

```text
configure before setup -> HP001
configure after setup changes only allowlisted preferences
identical configure -> no-op
get_context before setup returns versions/setup=false safely
get_context after setup returns persisted settings
restart with autoStart=false -> Skill sees false
get_context returns all open Quests for current Project across Heroes
get_context read-only -> no Project row creation / no WAL bookkeeping write
Skill/Core contract mismatch -> fail-safe guidance
```

## 6. Active-Hero/Start tests

```text
Start requires explicit heroId
Activate(B) concurrent with already-formed Start(heroId=A) -> Quest belongs A
Start never re-reads active Hero for ownership
same startRequestId + same Project/Hero/args -> same Quest
same startRequestId + changed Hero -> HP135
same startRequestId from different Project -> HP135
Start replay after active Hero changed -> original Quest/Hero
Start replay after locale changed -> original Quest locale
fresh request + same Hero+Project open -> HP133
```

## 7. One-open/linked-worktree tests

Real Project identity + SQLite:

```text
same Hero + same Project -> one open Quest max
different Heroes + same Project -> independent open Quests allowed
same Hero + different Projects -> independent open Quests allowed
linked worktrees resolve same ProjectId
same Hero + two linked worktrees + independent starts -> second HP133
```

The final case is an explicit 0.1 support limitation, not a bug.

## 8. Finish identity/conflict tests

```text
same finishRequestId + same payload -> persisted replay
same finishRequestId + changed payload -> HP135
new finishRequestId + finalized Quest + equivalent payload -> original result / alreadyFinalized
new finishRequestId + finalized Quest + different payload -> HP136
concurrent partial vs success -> exactly one persists; loser observes HP136 after retry/re-evaluation
active Hero switch before Finish -> persisted Quest Hero receives progression
UNIQUE report/xp event remain intact
```

No test introduces lease/agent ownership.

## 9. Crash injection

Use child processes terminated at controlled persistence points.

Required:

```text
bootstrap before/after commit
Start before/after commit
Finish before/after commit
CLI logical delete before/after commit
migration lock acquired then process killed
```

Never “recover” by deleting WAL/SHM.

## 10. SQLite connection-policy tests

Temporary file-backed DB only.

Prove effective:

```text
sqlite_version >=3.53.4
journal_mode=wal
foreign_keys=ON
synchronous=FULL
trusted_schema=OFF
Cache != Shared
Default Timeout=5
```

Repeat through:

```text
fresh connection
pooled reopen
clear pool + reopen
new child process
```

## 11. Direct schema-invariant tests

Bypass Application and attempt invalid SQL/EF inserts. Database must reject:

```text
invalid Quest status/result/status/evidence
Trust=-1 / 101
Strain=-1 / 101
negative XP/counters
scope/user-correction out of range
open Quest with finished_at
finished Quest without finished_at
second singleton app_settings row
setup=true with null active Hero
second open Quest same Hero+Project
invalid FK references
```

## 12. Mutation receipt tests

```text
args_encoding_version persisted
mutation-args/1 golden byte/hash vectors
serializer/whitespace changes do not change canonical hash
Start receipt binds ProjectId + HeroId
Finish receipt binds canonical finalization payload
Hero permanent delete marks related surviving receipts target_deleted
late create/start retry after target deletion never resurrects data
historical receipt remains interpretable after a new encoding version is introduced in fixture code
```

## 13. Migration tests

Every schema/release:

```text
empty -> latest
previous release fixture -> latest
model snapshot diff
CHECK/FK/partial-index review
quick_check + foreign_key_check
representative populated rebuild migration if required
```

Abandoned migration lock scenario:

```text
child acquires EF migration lock
kill child
next doctor reports suspicious __EFMigrationsLock
normal startup does not silently clear
explicit repair after safety preconditions
migration completes
integrity checks pass
```

## 14. Projection rebuild test

Canonical surviving history -> rebuild mutable projections -> public read models identical.

At minimum rebuild/compare:

```text
Hero total XP
Trust/Strain
success streak
hero_skills
hero_project_stats
Hero card/project stats
```

This validates repair/migration seams without introducing event sourcing.

## 15. Level-cap wire tests

```text
Hero L49 -> L50
Hero already L50 receives more XP
Skill L9 -> L10
Skill L10 receives more XP
isLevelCapped=true at cap
nextLevelXpRequired absent at cap
nextLevelXpRequired present below cap
XP continues accumulating
```

## 16. MCP snapshots

Current HP-MCP/2 v3.2.1 snapshot asserts:

```text
hero.bootstrap
hero.configure
hero.get_context
hero.create
hero.list
hero.activate
hero.archive
hero.restore
hero.start_quest
hero.finish_quest
hero.get_card
```

Also assert:

```text
annotations
closed schemas
request-ID fields
explicit heroId Start
finishRequestId
HP136 error
pre-setup allowed tools
get_context result/version fields
level-cap optional fields
forbidden hero.delete/list_active_quests absence
structuredContent + one compatibility TextContent semantic equality
```

Exact JSON minification is not public business semantics.

## 17. MCP protocol qualification

Exercise preferred `2026-07-28` and `2025-11-25` compatibility path through official C# SDK 2.1.0.

Task 1 first proves actual package restore/build availability; do not rely on search indexes alone.

stdio framing must keep stdout protocol-only.

## 18. Agent Skill evals

Minimum scenarios:

```text
short factual question -> no start
meaningful work -> start
meaningful-goal boundary treated as heuristic
persisted autoStart=false after restart -> no auto-start
same-goal followups -> no fragmentation
await input -> no finish
complete -> finish
explicit switch -> partial/abandoned then new start
ambiguous switch -> no silent close
inactive-Hero Quest discoverable via get_context
several plausible open Quests -> no guess
active Hero changed elsewhere after context -> explicit Start heroId stable
Start transport retry -> same startRequestId
Finish transport retry -> same finishRequestId
HP136 -> no overwrite attempt
observed/reported terminology accurate
Hero Passport calls -> no self-awarded tool_use
milestone flavor does not change semantic facts
Skill/Core version mismatch -> fail safe
```

Measure false-positive starts and premature finishes; conservative behavior is preferred.

## 19. Privacy/security tests

Static/runtime scans:

```text
no source/diff/raw-log/prompt/path/remote DTO/entity fields
Quest metadata sensitivity documented
MCP permanent delete absent
CLI delete wording does not claim forensic erasure
read-only MCP tools cause no durable writes
stdout protocol-only
stderr/request logging scrubbed
trusted_schema OFF
Git safe.directory not weakened
```

## 20. Packaging/E2E risk-first checkpoint

Reference host: Codex.

Before implementing full RPG layers, prove a packaged vertical slice:

```text
fresh HERO_PASSPORT_HOME
bootstrap
get_context
real temporary Git repo
Skill Start explicit Hero
minimal Finish + base XP
server restart
context/history recovery
retry/crash/race vectors
```

Only after this checkpoint expand full reward/Skills/levels/Trust-Strain/Streak/Traits/Titles/localization/admin features.

## 21. Release checklist

No 0.1 release unless:

```text
all focused/unit suites green
real SQLite concurrency/crash/connection/schema tests green
migration-lock recovery green
projection rebuild green
MCP contract/protocol qualification green
Agent Skill eval thresholds accepted
RU/EN complete
privacy scans green
packaged Codex E2E green
cross-host compatibility recorded
```
