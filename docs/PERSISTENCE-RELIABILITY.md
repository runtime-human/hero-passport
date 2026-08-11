# Hero Passport — SQLite Concurrency, Crash Recovery & Backup Deep Dive

**Status:** Accepted normative deep-dive  
**Snapshot:** 2026-08-11  
**Storage:** EF Core SQLite 10.0.10 + Microsoft.Data.Sqlite 10.0.10 behavior  
**Journal policy:** WAL + `synchronous=FULL`

This document is the detailed persistence-reliability source of truth. It resolves the remaining ambiguity in architecture v3 around writer serialization, active-quest caps, crash recovery, WAL handling and backup.

---

## 1. Reliability objective

Hero Passport has a tiny write workload, so persistence is optimized for **correctness and durability rather than maximum write throughput**.

Required guarantees:

1. one quest completion awards progression at most once;
2. one finish transaction commits all progression changes or none;
3. concurrent same-start requests converge;
4. the 16-open-quest cap cannot be exceeded by a race between local writers;
5. a process crash before commit leaves no partial progression;
6. a crash after commit but before response is safe to retry;
7. WAL/journal recovery is left to SQLite rather than reimplemented;
8. live backups are consistent;
9. writable network-filesystem databases are outside the supported profile;
10. corruption is detected and surfaced, never “repaired” automatically by guesswork.

---

## 2. Current provider fact that resolves the transaction ambiguity

Microsoft.Data.Sqlite documents serializable transactions as the default and warns that deferred transactions can fail while upgrading a read transaction to a write transaction, requiring the **entire transaction** to be retried.

More importantly, the source for the exact selected `Microsoft.Data.Sqlite 10.0.10` shows:

```text
SqliteConnection.BeginTransaction()
  -> BeginTransaction(IsolationLevel.Unspecified)
  -> provider promotes to Serializable
  -> non-deferred SqliteTransaction
  -> BEGIN IMMEDIATE;
```

Therefore Hero Passport does not need a speculative custom lock or raw-SQL transaction protocol.

Normative policy:

> Every Hero Passport read-modify-write operation begins a short non-deferred Serializable SQLite transaction **before reading the invariants it will modify**.

With the approved provider this means writer intent is acquired via `BEGIN IMMEDIATE`.

The implementation must have a provider integration test proving the selected EF/ADO path still produces the required immediate-writer behavior. If a future provider version changes it, preserve the behavior explicitly rather than silently accepting a weaker transaction.

---

## 3. Why immediate writer acquisition is correct here

SQLite WAL allows concurrent readers and one writer. An immediate write transaction obtains writer intent before the application performs the read/check/write sequence.

That is exactly what Hero Passport needs for tiny operations such as:

```text
read active count
check <= 15
insert quest
```

and:

```text
load quest state
check finished
calculate/write reward
mark finished
```

A deferred transaction would allow two writers to read the same precondition and then compete during upgrade. We have no throughput reason to accept that complexity.

The write transactions are deliberately very short; agent work itself never occurs inside a transaction.

---

## 4. Write transaction API policy

Preferred EF boundary:

```csharp
using var tx = context.Database.BeginTransaction(IsolationLevel.Serializable);
// short read/check/write sequence
context.SaveChanges();
tx.Commit();
```

Exact code may use a small Infrastructure-specific unit-of-work helper, but the observable behavior must remain:

```text
non-deferred
Serializable
writer intent acquired before invariant reads
one connection/context for the whole transaction
bounded execution
```

Do not issue an independent raw `BEGIN IMMEDIATE` while EF believes it owns transaction state.

Do not create a custom process-wide mutex, lock file, distributed lock or Polly retry layer around ordinary product writes.

---

## 5. `StartQuest` transaction — normative sequence

The previous v3 wording “lookup -> count -> insert, then determine whether immediate transaction is needed” is superseded.

Required sequence:

```text
resolve HeroOperationContext outside DB transaction
validate SafeText/QuestType outside transaction
compute QuestDedupKeyV1 outside transaction

BEGIN IMMEDIATE / provider non-deferred Serializable transaction

1. query open quest with same hero/project/dedup-key-version/dedup-key
   found -> commit/rollback read-only transaction state and return AlreadyOpen=true

2. count open quests for hero/project
   count >= 16 -> no write, end transaction, return HP133

3. insert quest
4. update any start projection counters that are part of the same invariant
5. SaveChanges
6. COMMIT
7. return AlreadyOpen=false
```

The unique open dedup-key index remains a database backstop against implementation bugs or unexpected paths, but normal serialization is provided by writer acquisition.

### Consequence for count-15 race

Initial state:

```text
15 open quests
writer A wants key A
writer B wants key B
```

Expected behavior:

```text
A obtains writer transaction
B waits/retries inside SQLite provider timeout policy
A sees 15, inserts #16, commits
B obtains writer transaction
B sees 16 -> HP133
```

Final count is exactly 16, never 17.

This is a release-blocking integration test.

---

## 6. `FinishQuest` transaction — normative sequence

Preparation outside transaction may include pure parsing/validation of request fields, but anything depending on durable quest/hero state belongs inside the writer transaction.

Required sequence:

```text
BEGIN IMMEDIATE / provider non-deferred Serializable transaction

1. load quest
   absent -> HP130
2. verify quest hero/project == HeroOperationContext
   mismatch -> HP134
3. if already finished:
      load persisted immutable outcome
      end transaction
      return AlreadyFinished=true
4. normalize/validate skills that do not require DB mutation
5. calculate deterministic reward from persisted/request values
6. insert quest report
7. insert quest_report_skills
8. insert UNIQUE xp_event for quest
9. update hero XP / Trust / Risk
10. update hero skills
11. unlock traits
12. update hero_project_stats
13. mark quest finished
14. SaveChanges
15. COMMIT
16. return persisted/constructed committed result
```

Do not mark the quest finished in a separate transaction from its reward.

Do not calculate a retry result using the latest rules after the quest has already been finished; return the original persisted reward/report projection.

---

## 7. Concurrent finish behavior

Two writers call `finish_quest` for the same `questId`.

With immediate writer serialization:

```text
writer A obtains writer lock
writer B waits
A completes + commits
B begins/continues after A
B loads finished quest
B returns original persisted result
```

Database defense remains:

```text
UNIQUE xp_events.quest_id
UNIQUE quest_reports.quest_id
```

Release invariant:

```text
quest_reports = 1
xp_events      = 1
progression mutation applied once
both callers observe the same persisted reward
```

No raw constraint or SQLITE_BUSY error should leak for the ordinary race path.

---

## 8. Read-only operations

`GetHeroCard` and `ListActiveQuests` do not acquire a writer transaction.

Prefer one bounded query/projection where practical.

If a read operation later requires several SQL statements that must observe one snapshot, use a short read transaction; do not hold it across formatting, model work, network calls or user interaction.

Long read transactions are harmful in WAL because they can prevent checkpoints from fully progressing.

---

## 9. Busy/locked policy

Current connection policy:

```text
Default Timeout = 5 seconds
```

Microsoft.Data.Sqlite retries `SQLITE_BUSY`/`SQLITE_LOCKED` until the command timeout; `DefaultTimeout` also applies to implicit commands such as `BeginTransaction`.

Hero Passport initially relies on that provider behavior only.

After timeout exhaustion:

```text
HP202 database_busy
category: storage
retryability: transient
```

Do not stack:

```text
SQLite retry
+ Polly retry
+ arbitrary loop
```

which could turn a five-second interactive bound into a much longer stall.

If measurement shows five seconds is wrong, change the product timeout explicitly and update tests/docs.

---

## 10. WAL policy

Required effective state:

```sql
PRAGMA journal_mode = WAL;
PRAGMA synchronous = FULL;
PRAGMA foreign_keys = ON;
```

SQLite documents that WAL permits readers and a writer concurrently but only one writer at a time. It also requires shared-memory coordination on the same host and therefore does not support the normal multi-process WAL model over a network filesystem.

Hero Passport writes are small, so `FULL` is chosen for durability. With WAL, `FULL` syncs the WAL on each commit.

---

## 11. Writable storage location policy

Supported 0.1 profile:

```text
local filesystem on the same host
```

Not release-supported for the live writable DB:

```text
NFS
SMB/network share
UNC/network drive when detectable
cross-machine shared volume relying on network locking
active cloud-sync location used as a database-sharing mechanism
```

Reason: WAL relies on same-host shared-memory/locking behavior and SQLite explicitly states normal WAL does not work across a network filesystem.

`doctor` and startup should reject a storage location when the platform can confidently identify it as a network filesystem. Where reliable classification is unavailable, `doctor` reports the limitation and records that the filesystem could not be proven local.

Do not claim cross-machine shared-SQLite support.

Suggested error when a known unsupported writable filesystem is detected:

```text
HP211 unsupported_storage_location
```

---

## 12. WAL checkpoint policy

Keep SQLite's default automatic checkpoint strategy initially.

SQLite defaults to automatic checkpointing around 1000 WAL pages and documents this as a strategy that works well for ordinary workstation applications.

0.1 policy:

- do not run a manual checkpoint after every quest;
- do not run `TRUNCATE` or `RESTART` checkpoints in the hot path;
- keep read transactions short;
- observe WAL size/checkpoint health in `doctor`/tests;
- add explicit checkpoint scheduling only if measured evidence shows checkpoint starvation or latency problems.

A long-running reader can prevent a checkpoint from completing and let the WAL grow; Hero Passport avoids this primarily by not holding long readers.

---

## 13. Native SQLite safety floor

This requires an explicit 2026 correction.

SQLite documents a rare **WAL-reset database-corruption bug** in versions through 3.51.2, fixed in 3.51.3 and later (with selected older backports).

Hero Passport deliberately uses a current SQLitePCLRaw/native SQLite baseline. Runtime qualification must therefore verify the actual loaded native version.

Normative release policy:

```text
SELECT sqlite_version()
```

must report:

```text
>= 3.51.3
```

for the normal supported bundle path.

Older patched backports are not part of the 0.1 tested matrix even if upstream provides them.

The package baseline remains `SQLitePCLRaw.bundle_e_sqlite3 3.0.5`; restore/publish qualification must record the exact native SQLite actually loaded by each artifact. Do not assume package metadata proves which native library the operating system loaded.

Suggested error:

```text
HP208 unsupported_sqlite_version
```

This should fail release qualification and normal write startup rather than silently run an unqualified WAL build.

---

## 14. Crash semantics

### Crash before commit

If the process dies before successful `COMMIT`, the transaction is not reported as earned progression. SQLite recovery/rollback semantics restore a consistent pre-transaction state when the DB is next opened.

Expected:

```text
no partial quest report
no partial XP event
no half-updated hero aggregate
```

### Crash during commit

The application treats `Commit()` as the durability boundary. A return from `SaveChanges()` is not enough.

The Microsoft.Data.Sqlite provider source explicitly accounts for COMMIT itself failing (for example `SQLITE_FULL`). Do not report success until commit completes successfully.

### Crash after commit but before response

This is an expected distributed-process ambiguity:

```text
DB committed
client never received response
```

The client retries `finish_quest(questId, ...)`.

Hero Passport loads the already-finished persisted outcome and returns it without awarding again.

This is why finish idempotency is a correctness requirement, not only a convenience.

---

## 15. WAL/journal recovery rules

Never manually delete, rename or detach:

```text
hero-passport.db-wal
hero-passport.db-shm
rollback journal files
```

as a “recovery” action.

SQLite documents the WAL as part of persistent database state; separating a DB from its live/hot WAL can lose committed transactions or corrupt the DB.

After an unclean shutdown:

1. open the database normally with SQLite;
2. allow SQLite to perform its recovery;
3. only then classify resulting errors/integrity state;
4. never delete recovery files merely because they remained after a crash.

Normal `doctor` must not manipulate WAL/SHM files directly.

---

## 16. File-copy backup is forbidden for a live database

Do not implement backup as:

```text
File.Copy("hero-passport.db", "backup.db")
```

while the application/database may be active.

SQLite documents that copying a DB file during a transaction can produce an inconsistent/corrupt backup and that a WAL/hot journal is part of database state.

Approved live-backup mechanism for a future DB-backup command/pre-migration safety copy:

```text
Microsoft.Data.Sqlite SqliteConnection.BackupDatabase
```

Microsoft documents that the current `BackupDatabase` implementation makes a consistent online backup but blocks other writers while it runs. Hero Passport accepts this tradeoff because backups are explicit/rare.

`export` is a logical data export and is **not** advertised as a byte-for-byte SQLite backup.

---

## 17. Backup verification

A backup is not considered successful merely because the API call returned.

Recommended sequence:

```text
1. create backup to a temporary destination
2. close/flush backup connection
3. open the backup independently
4. PRAGMA quick_check
5. PRAGMA foreign_key_check
6. verify schema/migration metadata can be read
7. atomically publish/rename the completed backup when the filesystem supports it safely
```

Do not overwrite the only previous good backup until the new backup has been verified.

A future restore command requires a separate restore/replace design; do not replace an open database file.

---

## 18. Integrity checks and `doctor`

### Normal doctor

Fast checks:

```text
open DB through SQLite
sqlite_version()
journal_mode
synchronous
foreign_keys
migration state
read core schema
WAL/checkpoint status where available without disruptive checkpointing
PRAGMA quick_check
PRAGMA foreign_key_check
```

`quick_check` is bounded enough for the expected small local database; if measurements show otherwise, split it into a fuller mode.

### Full/repair diagnostics

A future explicit `doctor --full` may run `PRAGMA integrity_check` and more expensive analysis.

No doctor mode automatically:

- deletes WAL/SHM;
- drops `__EFMigrationsLock` blindly;
- reindexes corruption without evidence;
- rewrites the database;
- copies files behind SQLite's back.

Diagnosis and destructive repair are separate operations.

---

## 19. EF migration lock handling

EF Core owns migration locking. SQLite's EF provider uses `__EFMigrationsLock`.

Policy:

- no custom migration mutex/table around EF migrations;
- startup does not delete the lock table blindly;
- suspicious abandoned-lock state is surfaced by `doctor`;
- manual recovery requires confirming no competing migration process and creating a verified backup first where state permits;
- every released migration is tested from a previous release DB and from an empty DB.

---

## 20. Error translation contract

Use `SqliteException.SqliteErrorCode` / extended error information internally. Do not expose raw SQLite messages, SQL, paths or connection strings to MCP.

Recommended mapping:

```text
SQLITE_BUSY / SQLITE_LOCKED / lock-contention equivalents after timeout
  -> HP202 database_busy

SQLITE_FULL
  -> HP203 storage_full

SQLITE_READONLY
  -> HP204 storage_read_only

SQLITE_IOERR / SQLITE_CANTOPEN
  -> HP205 storage_io_error

SQLITE_CORRUPT / SQLITE_NOTADB
  -> HP206 database_corrupt

unexpected SQLITE_CONSTRAINT
  -> HP207 storage_constraint

qualified-version failure
  -> HP208 unsupported_sqlite_version

known unsupported network storage
  -> HP211 unsupported_storage_location
```

Expected uniqueness constraints are translated semantically before the generic constraint mapping:

```text
open dedup-key uniqueness -> retry/reload matching quest
xp_events.quest_id uniqueness -> already-finished/reload path or defect detection
quest_reports.quest_id uniqueness -> already-finished/reload path or defect detection
```

`SQLITE_FULL`, `READONLY`, corruption and I/O failures are not reported as retryable `database_busy`.

---

## 21. Failure injection points

Infrastructure tests should support test-only deterministic fault points; they do not ship as user-accessible runtime switches.

Useful points:

```text
StartQuest.after_begin
StartQuest.after_insert_before_commit
StartQuest.after_commit_before_return
FinishQuest.after_begin
FinishQuest.after_report_write
FinishQuest.after_xp_event
FinishQuest.before_commit
FinishQuest.after_commit_before_return
```

A child-process test can terminate the process at selected points and reopen the same file from a fresh process.

Do not use in-memory mocks to claim crash safety.

---

## 22. Required concurrency/crash test matrix

### Writer serialization

1. prove selected provider path begins a non-deferred/IMMEDIATE Serializable transaction;
2. second writer waits/retries rather than reading through a stale cap check;
3. reads remain possible while one WAL writer is active where SQLite semantics permit.

### Start

4. two same-key starts -> one row, same questId;
5. count=15 + two distinct starts -> exactly one succeeds, final count exactly 16, other HP133;
6. count=16 -> no insert, HP133;
7. writer timeout -> HP202 without raw SQLite leakage.

### Finish

8. two concurrent finishes -> one report, one XP event, one progression mutation;
9. second caller returns persisted outcome;
10. forced unique-race path does not grant twice.

### Crash

11. terminate after writes but before commit -> no partial state after reopen;
12. terminate after commit but before response -> retry returns committed outcome exactly once;
13. leave WAL after killed process -> next SQLite open recovers normally;
14. no test manually deletes WAL/SHM to make recovery pass.

### Storage failures

15. simulate/fixture FULL mapping -> HP203;
16. READONLY -> HP204;
17. IOERR/CANTOPEN translation -> HP205 where safely testable;
18. corrupt/not-a-db fixture -> HP206;
19. unsupported/old native SQLite qualification fixture -> HP208;
20. known network path fixture -> HP211 where platform detection is deterministic.

### Backup

21. online backup while source is active -> backup opens and passes quick/FK checks;
22. backup does not corrupt/replace the live DB;
23. backup writer blocking is bounded/observed;
24. raw `File.Copy` is absent from live DB backup implementation.

### Checkpoint

25. default autocheckpoint remains enabled;
26. no per-finish TRUNCATE/RESTART checkpoint;
27. long-reader test/fixture documents WAL growth behavior without introducing a long-lived read in product code.

---

## 23. Performance bounds relevant to reliability

Because all write invariants serialize on SQLite's single writer, performance policy is simple:

- transaction preparation outside writer lock whenever safe;
- no network/filesystem scans during writer transaction;
- no text localization during writer transaction;
- no model work during writer transaction;
- bounded result sets;
- indexes support same-key lookup, active count/list and quest finish lookup;
- writer transaction duration is measured in integration tests.

A future performance optimization must not replace immediate writer serialization with a weaker race-prone pattern without a new proof/ADR.

---

## 24. Revisit triggers

Reconsider this persistence model only when evidence shows one of:

- write contention is material for ordinary local users;
- DB grows enough that current indexes/checkpoint behavior is problematic;
- a second host/machine must write the same state store;
- cloud/team mode requires a server database;
- backup/restore becomes a first-class product workflow;
- the selected Microsoft.Data.Sqlite transaction behavior changes.

Multi-machine writes are a database-architecture change, not a reason to place the existing WAL file on a shared network drive.

---

## 25. Official references verified 2026-08-11

- Microsoft.Data.Sqlite transactions: https://learn.microsoft.com/dotnet/standard/data/sqlite/transactions
- Microsoft.Data.Sqlite online backup: https://learn.microsoft.com/dotnet/standard/data/sqlite/backup
- Microsoft.Data.Sqlite 10.0.10 source (`SqliteConnection` / `SqliteTransaction`): https://github.com/dotnet/efcore/tree/v10.0.10/src/Microsoft.Data.Sqlite.Core
- SQLite WAL: https://sqlite.org/wal.html
- SQLite result/error codes: https://sqlite.org/rescode.html
- SQLite corruption/recovery guidance: https://sqlite.org/howtocorrupt.html
- SQLite 3.51.3 WAL-reset fix: https://sqlite.org/releaselog/3_51_3.html
- SQLitePCLRaw bundle 3.0.5: https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3/3.0.5
