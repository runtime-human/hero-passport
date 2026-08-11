# Hero Passport — SQLite Persistence and Reliability

**Status:** Accepted v3.2 reliability contract  
**Snapshot:** 2026-08-11

## 1. Supported storage profile

0.1 supports one writable database on a **same-host local filesystem**.

Required effective state:

```text
SQLite runtime >= 3.53.4
journal_mode=WAL
synchronous=FULL
foreign_keys=ON
Cache=Default
Pooling=True
Default Timeout=5 seconds
```

Do not use `Cache=Shared` with WAL.

The runtime floor is checked through `SELECT sqlite_version()`; package metadata alone is not sufficient release evidence.

Network/shared filesystems, NFS-like mounts, cloud-shared SQLite files and multi-host writers are unsupported for 0.1.

## 2. Why the runtime floor is 3.53.4

The selected `SQLitePCLRaw.bundle_e_sqlite3 3.0.5` resolves native SQLite >=3.53.4. SQLite 3.51.3 was the first patch containing the WAL-reset corruption fix; 3.53.4 is the current bundled/runtime baseline and includes that fix plus subsequent maintenance.

Release/doctor must reject an unexpectedly loaded older native library rather than assuming NuGet restore proves runtime identity.

## 3. Connection policy

Construct with `SqliteConnectionStringBuilder`:

```text
Mode=ReadWriteCreate
Cache=Default
Foreign Keys=True
Pooling=True
Default Timeout=5
```

On initialization/doctor verify effective pragmas after opening a connection.

Do not set WAL/synchronous repeatedly on every routine command when a dedicated initialization/qualification path can establish and verify them safely.

## 4. DbContext lifetime

Use `IDbContextFactory<HeroPassportDbContext>`.

One operation/unit of work gets one short-lived context. Never share a DbContext across concurrent tool calls and never hold one for an entire agent Quest.

SQLite I/O is intentionally short synchronous local I/O; do not wrap it in `Task.Run` to simulate asynchronous database support.

## 5. Writer transaction rule

All read-modify-write operations begin a **non-deferred Serializable transaction before invariant reads**.

Conceptual provider call:

```text
connection.BeginTransaction(IsolationLevel.Serializable, deferred: false)
```

Microsoft.Data.Sqlite documents Serializable as the normal isolation and warns that deferred read transactions can fail during read-to-write upgrade if the database becomes locked. Hero Passport avoids that upgrade pattern for mutation invariants.

Release tests must prove the selected provider version obtains immediate writer intent (qualified as `BEGIN IMMEDIATE` behavior) rather than relying on documentation wording alone.

## 6. Busy handling

SQLite is the writer coordinator. Hero Passport adds no process-global writer mutex.

Provider/SQLite busy timeout is 5 seconds. Do not stack an independent Polly retry policy around transactions; nested retry layers make latency/error behavior less predictable.

Map exhausted contention to:

```text
HP202 database_busy
```

The caller may retry an idempotent mutation using the **same** request identity/arguments. A Finish retry uses the same `questId`.

## 7. StartQuest transaction

Canonical request validation/argument hashing occurs before opening the transaction.

Inside the writer transaction:

```text
1. lookup mutation receipt for (start_quest, startRequestId)
2. if found:
     args hash equal -> load persisted Quest and return replay
     args hash differs -> HP135
3. query open Quest for resolved Hero+Project
4. if present -> HP133 active_quest_exists
5. insert Quest
6. insert matching mutation receipt
7. update start projection
8. SaveChanges
9. COMMIT
```

DB backstops:

```text
UNIQUE mutation_receipts(operation_key, request_id)
UNIQUE quest_sessions(hero_id, project_id) WHERE status='open'
```

Two concurrent new starts for the same Hero+Project must produce one open Quest and one `HP133` result, never two rows.

## 8. FinishQuest transaction

```text
1. BEGIN writer
2. load Quest by questId
3. verify process-bound ProjectId
4. if already finished -> load/return persisted result
5. calculate deterministic current rule versions
6. insert Quest report + reward/Trust-Strain/Skill/milestone children
7. insert UNIQUE xp_event for questId
8. update Hero + HeroSkills + Streak + unlocks + project stats
9. mark Quest finished + timestamp
10. SaveChanges
11. COMMIT
```

Do not return success until COMMIT succeeds.

Current globally active Hero is not substituted for the Quest’s persisted Hero owner.

Durable backstops:

```text
UNIQUE quest_reports.quest_id
UNIQUE xp_events.quest_id
```

Correct guarantee: **at-most-once committed progression per Quest**.

## 9. Concurrent Finish

Two callers may concurrently finish the same Quest.

Expected result:

- one transaction commits one immutable report/event/progression update;
- the other waits/fails busy/reloads according to SQLite timing;
- a retry returns the persisted original result;
- current game rules are never applied twice.

Tests must cover both same and conflicting finish payloads. Once a Quest is finished, persisted first committed outcome is authoritative; later different finish arguments do not rewrite history.

## 10. Create/Delete mutation receipts

Create Hero and permanent Delete Hero use caller request IDs and the same receipt pattern as Start.

Receipt insertion and the mutation are atomic.

For permanent delete, the receipt stores only:

```text
operation key
request ID
canonical args hash
deleted HeroId
deleted timestamp
```

It does not retain the deleted Hero’s game history.

## 11. Crash semantics

Never manually delete/rename SQLite sidecar files:

```text
*.db-wal
*.db-shm
rollback journals
```

SQLite owns crash recovery.

Required outcomes:

```text
crash before COMMIT -> no partial progression/mutation receipt
crash after COMMIT before response -> retry converges to persisted result
```

Child-process crash injection, not only exceptions in one process, proves this.

## 12. WAL checkpoint policy

Do not invent custom checkpoint loops in 0.1 without measured need. Use SQLite’s WAL behavior and test database growth/recovery under representative workloads.

Doctor may inspect WAL-related state but must not “repair” by deleting sidecar files.

## 13. Storage/error mapping

Stable storage meanings:

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

Expected unique-constraint races for request/open-Quest/Finish invariants are first translated into their semantic replay/conflict outcome rather than leaked as generic HP207.

## 14. Corruption/integrity

Doctor:

```text
open DB through SQLite
SELECT sqlite_version()
PRAGMA journal_mode
PRAGMA synchronous
PRAGMA foreign_keys
migration/schema state
PRAGMA quick_check
PRAGMA foreign_key_check
supported local storage location
```

Do not perform destructive automatic repair. A corruption signal becomes safe diagnostics/recovery guidance.

## 15. Backup

Logical export is not physical database backup.

A future/pre-migration live backup uses `SqliteConnection.BackupDatabase` or equivalent SQLite backup API, never raw `File.Copy` of an active WAL database.

Verified flow:

```text
backup into temporary destination
open destination independently
PRAGMA quick_check
PRAGMA foreign_key_check
validate schema/migration metadata
publish completed backup atomically
```

Never replace the only known-good backup before the new candidate passes validation.

## 16. Migrations

Use EF migrations; no `EnsureCreated` product database.

Use provider migration locking rather than adding a second custom migration lock mechanism.

Release migration matrix:

```text
empty -> latest
previous-release fixture -> latest
crash/interruption around migration as applicable
model snapshot review
partial/unique index preservation
FK/cascade review
backup consideration
```

Permanent-delete cascade paths require explicit review because they are privacy-sensitive destructive behavior.

## 17. Local filesystem support detection

Do not pretend reliable SQLite WAL semantics on arbitrary remote/network storage. Known unsupported location detection returns `HP211` with a safe message. Unknown location characteristics are documented as outside the support guarantee rather than silently advertising multi-host safety.

## 18. Release qualification tests

File-backed SQLite only:

```text
non-deferred writer intent
same request ID same args replay
same request ID changed args conflict
two-new-start race -> one open Quest
two-Finish race -> one progression event
busy timeout mapping
crash before/after commit
partial unique indexes
foreign key behavior
permanent Hero delete transaction
WAL recovery
runtime version/pragmas
backup consistency
migration upgrade fixtures
```

EF InMemory cannot substitute for any of these guarantees.
