# Hero Passport — SQLite Persistence and Reliability

**Status:** Accepted v3.2.1 reliability contract  
**Snapshot:** 2026-08-11

## 1. Supported storage profile

0.1 supports one writable database on a **same-host local filesystem**.

Qualified effective state:

```text
SQLite runtime >= 3.53.4
journal_mode=WAL
synchronous=FULL
foreign_keys=ON
trusted_schema=OFF
Cache=Default
Pooling=True
Default Timeout=5 seconds
```

Do not use `Cache=Shared` with WAL.

Runtime floor is checked with `SELECT sqlite_version()`; package metadata alone is not release evidence.

Network/NFS/cloud-shared SQLite files and multi-host writers are unsupported.

## 2. Runtime floor

`SQLitePCLRaw.bundle_e_sqlite3 3.0.5` is the selected native bundle wrapper. Release/doctor verifies the actually loaded SQLite library and requires >=3.53.4.

The required floor includes the WAL-reset corruption fix present in current supported SQLite and subsequent maintenance.

## 3. Connection string policy

Construct with `SqliteConnectionStringBuilder`:

```text
Mode=ReadWriteCreate
Cache=Default
Foreign Keys=True
Pooling=True
Default Timeout=5
```

Microsoft.Data.Sqlite has no connection-string `Synchronous=Full` keyword; Hero Passport therefore treats synchronous/trusted-schema as explicit connection-open policy.

## 4. Database-persistent vs connection-scoped policy

### Initialization/database qualification

On database initialization/upgrade qualification:

```text
PRAGMA journal_mode=WAL;
verify journal_mode == wal
SELECT sqlite_version();
verify >=3.53.4
```

WAL is a database-level persistent property and need not be reset for every command.

### Every opened product connection

Immediately after open and before ordinary application work:

```text
PRAGMA synchronous=FULL;
PRAGMA trusted_schema=OFF;
```

Foreign keys are enabled through `Foreign Keys=True` and verified in qualification/doctor paths.

Do not assume one initialization connection configured pooled/future connections.

Required connection-policy tests:

```text
fresh connection -> FULL / foreign_keys ON / trusted_schema OFF
pooled reopen -> same
clear pool + reopen -> same
new process -> same
```

## 5. DbContext lifetime

Use `IDbContextFactory<HeroPassportDbContext>`.

One operation/unit of work gets one short-lived context. Never share DbContext across concurrent MCP calls and never hold it for an entire agent Quest.

SQLite I/O is short local I/O; do not use `Task.Run` as fake async database support.

## 6. Writer transaction rule

All read-modify-write operations begin a **non-deferred Serializable transaction before invariant reads**.

Qualified provider behavior:

```text
connection.BeginTransaction(IsolationLevel.Serializable, deferred:false)
```

Release tests prove this obtains immediate writer intent with the selected Microsoft.Data.Sqlite version. Do not rely only on an assumed `BEGIN IMMEDIATE` mapping.

Only one SQLite writer may have pending changes; transactions must remain short and never span agent work/user interaction.

## 7. Busy handling

SQLite is the writer coordinator. No custom process-global writer mutex.

Default/provider busy timeout is 5 seconds. No Polly transaction retry layer is added.

Exhausted contention maps to:

```text
HP202 database_busy
```

Caller retries a retry-safe mutation with the same request ID and same canonical arguments.

## 8. Bootstrap transaction

Canonicalize bootstrap payload and `mutation-args/1` hash before writer acquisition.

Inside writer:

```text
1 lookup receipt(bootstrap, bootstrapRequestId)
2 found + same hash/version -> replay persisted bootstrap target
3 found + changed -> HP135
4 no receipt + setup already complete -> HP002
5 create initial Hero
6 update typed singleton settings + active Hero
7 insert bootstrap receipt
8 SaveChanges
9 COMMIT
```

Two concurrent fresh bootstrap requests serialize. Exactly one initializes; the other observes completed setup and fails HP002 unless it is replaying the winning request ID.

## 9. StartQuest transaction

ProjectId is resolved before DB work. Canonical Start scope includes ProjectId + explicit HeroId + quest type/title/goal.

Inside writer:

```text
1 lookup receipt(start_quest, startRequestId)
2 found:
     same encoding/hash/context -> load ORIGINAL persisted Quest or safe target-deleted state
     changed -> HP135
3 validate setup
4 validate explicit Hero exists and not archived
5 snapshot settings.locale
6 query open Quest for HeroId+ProjectId
7 found -> HP133 active_quest_exists
8 create Project row if this first meaningful mutation needs it
9 insert Quest
10 insert receipt with ProjectId/HeroId/QuestId
11 update project projection
12 SaveChanges
13 COMMIT
```

`active_hero_id` is never read to decide Start ownership.

DB backstops:

```text
PRIMARY/UNIQUE mutation_receipts(operation_key, request_id)
UNIQUE quest_sessions(hero_id, project_id) WHERE status='open'
```

## 10. FinishQuest transaction

Canonicalize finish payload and hash before writer acquisition.

Inside writer:

```text
1 lookup receipt(finish_quest, finishRequestId)
2 found:
     same hash/version -> persisted replay
     changed -> HP135
3 load Quest by questId and verify invocation ProjectId
4 if already finished:
     compare payload hash to persisted finalization hash
     equal -> persist/accept this finish request receipt; return original result, alreadyFinalized=true
     different -> HP136 quest_already_finalized_conflict
5 calculate current deterministic rule versions once
6 insert Quest report + reward/Trust-Strain/Skill/milestone rows
7 insert UNIQUE xp_event
8 insert finish receipt
9 update Hero/Skill/Streak/project projections + unlocks
10 mark Quest finished + timestamp
11 SaveChanges
12 COMMIT
```

Do not return success until COMMIT succeeds.

Durable backstops:

```text
UNIQUE quest_reports.quest_id
UNIQUE xp_events.quest_id
```

Correct guarantee: **at-most-once committed progression per Quest**.

## 11. Concurrent Finish

Two agents may attempt different finalizations.

Expected behavior:

- one transaction commits the immutable final report/progression;
- equivalent later payloads return that result;
- different later payloads return HP136;
- no overwrite/recalculation occurs;
- no lease/heartbeat/agent owner is introduced.

Required race tests cover identical and conflicting payloads.

## 12. Mutation receipts and deleted targets

Receipts persist:

```text
operation/request ID
args_encoding_version
args_hash
result kind/entity ID
bound ProjectId/HeroId as applicable
result_status active|target_deleted
effective timestamp
```

Receipt target/context IDs intentionally have no FK.

Permanent CLI Hero deletion marks related surviving receipts `target_deleted` before/while deleting private Hero history in the same transaction.

Late retry never resurrects a deleted Hero/Quest and never requires deleted title/goal/report data.

## 13. Crash semantics

Never manually delete/rename SQLite sidecars:

```text
*.db-wal
*.db-shm
rollback journals
```

SQLite owns crash recovery.

Required outcomes:

```text
crash before COMMIT -> no partial mutation/receipt/progression
crash after COMMIT before response -> same request converges to persisted result
```

Use child-process termination, not only in-process exceptions.

## 14. WAL/checkpoint policy

No custom checkpoint loop in 0.1 without measured need. Use SQLite WAL behavior and test representative growth/recovery.

Doctor may inspect WAL state but never “repair” by deleting sidecars.

## 15. Storage/error mapping

```text
HP200 storage_unavailable
HP202 database_busy
HP203 storage_full
HP204 storage_read_only
HP205 storage_io_error
HP206 database_corrupt
HP207 storage_constraint
HP208 unsupported_sqlite_version
HP210 app_data_unavailable
HP211 unsupported_storage_location
```

Expected unique-constraint races are translated into semantic replay/conflict outcomes rather than leaked as generic HP207.

## 16. Doctor/integrity

Doctor inspects:

```text
actual sqlite_version()
journal_mode
synchronous
foreign_keys
trusted_schema
current/pending EF migration state
__EFMigrationsLock presence/state
PRAGMA quick_check
PRAGMA foreign_key_check
supported storage location
```

Read-only doctor inspection must not mutate state.

## 17. EF migration abandoned locks

EF Core SQLite uses `__EFMigrationsLock` to serialize migrations. Unexpected process termination can leave an abandoned lock that prevents future migration completion.

Hero Passport must **not** silently clear it at ordinary startup.

Doctor reports a suspicious lock with safe recovery guidance.

An explicit CLI repair path may clear/drop the abandoned migration lock only when:

1. the user has stopped competing Hero Passport processes;
2. repair opens the DB and performs a fresh safety/integrity check;
3. the operation is explicitly requested;
4. post-repair migration state is revalidated.

Required child-process scenario:

```text
migration lock acquired
process killed
next startup/doctor detects lock
explicit repair
migration completes
quick_check/foreign_key_check pass
```

## 18. Backup

Live backup uses `SqliteConnection.BackupDatabase`/SQLite backup API, never raw `File.Copy` of an active WAL DB.

Verified flow:

```text
backup temporary destination
open independently
quick_check
foreign_key_check
validate migration/schema metadata
publish completed backup atomically
```

Do not replace the only known-good backup before candidate validation.

## 19. Permanent Hero delete semantics

Permanent Hero deletion is CLI-only in 0.1.

It is a **logical irreversible deletion from the active Hero Passport database state**, not forensic secure erasure.

Hero Passport does not claim removed bytes are unrecoverable from:

```text
SQLite free pages
filesystem snapshots
OS/cloud backups of app-data
previous Hero Passport backups
previous exports
storage media forensics
```

Do not add `secure_delete`/VACUUM erasure guarantees unless a future threat model explicitly requires them.

## 20. Rebuildable projections

Canonical surviving Quest/report/event/delta/unlock history is sufficient to rebuild:

```text
Hero total XP
Trust/Strain
success streak
hero_skills
hero_project_stats
```

Release test:

```text
capture public card/project read models
destroy/recompute projection rows/values from canonical history
capture again
assert semantic equality
```

This is repair/migration insurance, not event sourcing.

## 21. Migrations

Use EF migrations; no `EnsureCreated` product DB.

Migration 0001 must include critical CHECK/FK/partial-index/singleton invariants to avoid preventable later SQLite table rebuild debt.

Release matrix:

```text
empty -> latest
previous fixture -> latest
model snapshot review
CHECK/FK/index review
abandoned-lock recovery
projection rebuild
backup consideration
quick_check + foreign_key_check
```

## 22. Local filesystem detection

Do not claim reliable WAL semantics on arbitrary remote/network storage. Known unsupported locations return HP211. Unknown characteristics are documented outside the support guarantee rather than advertised as multi-host safe.

## 23. Required persistence qualification

Real file-backed SQLite only:

```text
runtime >=3.53.4
WAL
per-connection FULL/foreign_keys/trusted_schema across pooling/processes
non-deferred writer intent
bootstrap same/different request replay + crash
Start context-aware replay/mismatch/race + crash
Finish identical/conflicting request/race + crash
receipt target_deleted lifecycle
DB CHECK/FK violations rejected physically
one-open partial index
busy timeout mapping
WAL crash recovery
migration abandoned-lock recovery
backup validation
projection rebuild
```

EF InMemory cannot substitute for these guarantees.
