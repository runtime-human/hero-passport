# Hero Passport — Data Model and Persistence Specification

**Status:** Accepted for MVP  
**Store:** SQLite via EF Core 10  
**Baseline:** 2026-08-10

## 1. Persistence principles

1. One local SQLite database is authoritative.
2. Completing a quest and applying all progression is one short atomic transaction.
3. Reward history is immutable/append-only in MVP.
4. Read projections/counters are allowed but never replace immutable history.
5. Code, diffs, raw logs, prompts, secrets and full workspace paths have no persistence columns.
6. EF Core migrations start with the first persisted schema; `EnsureCreated` is not the product strategy.
7. Storage tests use real SQLite.

## 2. Application data directory

Resolve per-user platform paths through `IAppDataPaths`, never by scattering hard-coded paths through the code.

Logical files:

```text
hero-passport.db
config.json          # only when settings exist
logs/                # only when explicitly enabled
exports/             # optional convenience target
```

Tests redirect all paths to isolated temporary directories.

## 3. SQLite runtime baseline

Initialization must establish/verify:

```sql
PRAGMA foreign_keys = ON;
PRAGMA journal_mode = WAL;
```

Use a bounded default busy timeout. Do not combine `Cache=Shared` with WAL as an optimization strategy.

Keep SQLite's default automatic WAL checkpointing initially; custom checkpoint scheduling is justified only by measurements.

### Native dependency pin

Baseline:

```text
Microsoft.EntityFrameworkCore.Sqlite     10.0.10
SQLitePCLRaw.bundle_e_sqlite3             3.0.5
native SQLite                            >= 3.53.4
```

The direct SQLitePCLRaw pin is intentional. It prevents the application from silently resolving only to the much older transitive minimum accepted by EF/Microsoft.Data.Sqlite and places the shipped native SQLite above the WAL-reset bug fixed in SQLite 3.51.3.

Integration/release tests query `sqlite_version()` and fail below the approved floor.

## 4. ID/time/representation conventions

- IDs: UUIDv7 (`Guid.CreateVersion7()`).
- External JSON: lowercase canonical GUID string.
- DB GUID representation: choose one mapping in the first migration and never mix representations. Prefer 16-byte BLOB if EF/tooling/tests remain simple; otherwise canonical text is acceptable.
- Timestamps: UTC `DateTime`; persistence boundary enforces UTC.
- Total XP/counters: signed 64-bit integers.
- Per-event XP and bounded counters: 32-bit integers.
- Trust/risk: integer `0..100` with domain invariant and DB checks where practical.
- Domain enum/key persistence: canonical stable strings for inspectability/migration safety.

## 5. MVP tables

### `heroes`

```text
id                    PK
name                  required, <=80
name_normalized       required, unique
is_default            bool
created_at_utc        required
total_xp              long >=0
trust                 int 0..100
risk                  int 0..100
updated_at_utc        required
```

Initializer guarantees one default hero (`Nova`) idempotently.

### `projects`

```text
id                    PK
display_name          required, <=160
workspace_fingerprint required, <=128
identity_version      required, <=32
created_at_utc        required
last_seen_at_utc      required
```

Unique `(identity_version, workspace_fingerprint)`. No full-path column.

### `hero_project_stats`

Composite key `(hero_id, project_id)`.

```text
quests_started        long >=0
quests_finished       long >=0
quests_succeeded      long >=0
xp_earned             long >=0
last_quest_at_utc     nullable
```

Projection only; rebuildable from history.

### `quest_sessions`

```text
id                    PK
hero_id               FK heroes
project_id            FK projects
quest_type            required canonical key
goal                  required, <=500
status                open | completed
result                nullable until completed
idempotency_key       nullable, <=128
host_name             nullable, <=64
host_type             nullable, <=64
started_at_utc        required
finished_at_utc       nullable
created_at_utc        required
updated_at_utc        required
```

Constraints:

- open => `result`/`finished_at_utc` null;
- completed => both non-null;
- explicit idempotency key unique within hero/project when non-null;
- one automatic active quest per `(hero_id, project_id)` enforced at DB level where practical using a SQLite partial unique index.

If exact index semantics require migration SQL, keep that SQL fixed and covered by integration tests.

### `quest_reports`

Exactly one row per completed quest:

```text
quest_id                   PK/FK quest_sessions
summary                    required, <=2000
tests_mentioned            bool
scope_violations           int 0..100
user_corrections           int 0..100
build_status               canonical string
tests_status               canonical string
reward_rule_version        required
level_rule_version         required
skill_rule_version         required
trust_risk_rule_version    required
trait_rule_version         required
final_xp                   int >=0
reward_breakdown_json      required bounded JSON text
completed_at_utc           required
```

`reward_breakdown_json` is a stable audit snapshot; it does not turn the relational model into arbitrary schemaless storage.

### `quest_report_skills`

```text
quest_id               FK quest_reports
position               int 0..2
skill_key              canonical key
xp_gained              int >=0
```

PK `(quest_id, position)`, unique `(quest_id, skill_key)`. This preserves exact historical allocation and deterministic display order.

### `skills`

Canonical catalog:

```text
key                    PK
definition_version     required
sort_order             required
```

Seeded by initializer/migration. Localized labels live in application resources/maps, not as persisted identity.

### `hero_skills`

```text
hero_id                FK heroes
skill_key              FK skills
total_xp               long >=0
updated_at_utc         required
```

PK `(hero_id, skill_key)`.

### `traits`

```text
key                    PK
definition_version     required
activation_threshold   int >0
sort_order             required
```

### `hero_traits`

```text
hero_id                FK heroes
trait_key              FK traits
progress               long >=0
state                  locked | active
activated_at_utc       nullable
updated_at_utc         required
```

PK `(hero_id, trait_key)`. Rule `1.0.0` never deactivates an active trait.

### `xp_events`

Immutable XP ledger:

```text
id                    PK
hero_id               FK heroes
project_id            FK projects
quest_id              FK quest_sessions, UNIQUE
event_type            quest_reward
xp_delta              int >=0
reward_rule_version   required
breakdown_json        required
event_at_utc          required
```

The unique `quest_id` is a final DB-level defense against duplicate quest reward.

Future corrections, if ever required, are compensating events rather than history mutation.

### `app_settings`

Small bounded non-secret key/value settings only:

```text
key                   PK
value                 bounded string
updated_at_utc        required
```

Do not store API keys or credentials. Future secret needs require an OS credential-store design.

## 6. Delete behavior

History-preserving defaults:

```text
hero -> history       Restrict
project -> history    Restrict
quest -> report/xp    Restrict/NoAction in product paths
skill/trait catalog   Restrict when referenced
```

A destructive reset/delete feature requires its own data-management spec. Do not add broad cascade delete as a convenience.

## 7. Start transaction

A new start transaction:

```text
resolve/create project if needed
resolve hero
ensure hero_project_stats row
check idempotency/open quest
insert only if new
increment quests_started only if new
commit
```

Retry returning an existing quest performs no stat increment.

## 8. Atomic finish transaction

A first successful `FinishQuest` commits atomically:

```text
quest completion state
quest_reports
quest_report_skills
xp_events
heroes total_xp/trust/risk
hero_skills
hero_traits
hero_project_stats
```

On any failure, none of the progression changes commit.

## 9. Finish idempotency/concurrency

Inside the write transaction:

1. Load quest.
2. Missing -> `HP130`.
3. Completed -> load persisted outcome and return `alreadyFinished=true`; no writes.
4. Open -> calculate reward from immutable quest type + accepted rule version.
5. Insert unique quest reward ledger event.
6. Apply all projections and report.
7. Mark quest completed.
8. Commit.

If two processes race, only one may commit the unique reward/state transition. The losing caller reloads the completed outcome and converges to idempotent success when possible.

Do not rely on database-generated `rowversion`; SQLite does not provide that SQL Server concurrency primitive.

## 10. EF Core usage

- Short-lived `DbContext` per query/unit of work.
- Never singleton `DbContext`.
- Read projections use `AsNoTracking` where tracking is unnecessary.
- Use focused `IEntityTypeConfiguration<T>` mappings or equivalent.
- Persistence entities stay Infrastructure-internal where practical.
- Application/MCP/Web never return EF entities.
- Explicitly align EF Core package versions.
- Use `Microsoft.EntityFrameworkCore.Design` with a matched 10.0.10 version for migrations/tooling.

## 11. Migration policy

Every migration has:

- descriptive name;
- reviewed generated SQL/operations;
- fresh-database migration test;
- upgrade test from the previous released fixture once releases exist;
- data-preservation assertions for nontrivial rebuilds.

SQLite can rebuild tables for unsupported ALTER operations; destructive/rebuild-heavy migrations require explicit review and backup guidance.

Startup/init surfaces migration errors cleanly. `doctor` reports database/migration/native version state.

## 12. Backup/export

MVP portability is a **logical versioned JSON export**, not blindly copying a live `.db` file.

WAL mode can keep relevant state in WAL/SHM files, so copying only the main DB during use is not a general safe backup mechanism.

Export manifest:

```text
exportFormatVersion
createdAtUtc
appVersion
schemaVersion
ruleVersions
```

Write export atomically (temp + replace/rename where supported) and include only allowed product data.

Raw SQLite backup may be added later using SQLite-supported backup/checkpoint mechanisms and dedicated crash-consistency tests.

## 13. File permissions

- use per-user application data;
- on Unix-like systems apply/check user-only permissions when supported;
- on Windows inherit the user's profile ACL; never weaken it;
- `doctor` warns about obviously unsafe directory permissions where detection is reliable.

No at-rest encryption claim in MVP. OS account/disk security remains part of the local trust boundary.

## 14. Common-path indexes

Required/likely:

```text
projects(identity_version, workspace_fingerprint) UNIQUE
quest_sessions(hero_id, project_id, status)
quest_sessions(hero_id, project_id, idempotency_key) conditional UNIQUE
quest_sessions(project_id, started_at_utc)
quest_sessions(hero_id, started_at_utc)
xp_events(quest_id) UNIQUE
xp_events(hero_id, event_at_utc)
xp_events(project_id, event_at_utc)
```

Do not index every property speculatively; inspect real query plans later.

## 15. Required storage integration tests

- migrate empty DB to latest;
- initializer/seed idempotency;
- exactly one default hero;
- foreign keys enabled;
- WAL enabled;
- bounded busy timeout;
- no shared-cache+WAL configuration;
- runtime SQLite version >= approved floor;
- unique project fingerprint;
- active/start idempotency sequential + race;
- one XP event per quest;
- concurrent finish race;
- full rollback on injected failure;
- reopen temp-file DB and preserve state;
- no persisted full workspace path;
- export excludes prohibited fields/path/environment sentinels.
