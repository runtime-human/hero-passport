# Hero Passport — Data Model and Persistence

**Status:** Accepted v3.1  
**Snapshot:** 2026-08-11  
**Database:** SQLite via EF Core 10.0.10  
**Native bundle baseline:** `SQLitePCLRaw.bundle_e_sqlite3 3.0.5`

Exact transaction/crash/backup policy is normative in [`PERSISTENCE-RELIABILITY.md`](PERSISTENCE-RELIABILITY.md). Exact project fingerprint algorithm is normative in [`PROJECT-IDENTITY.md`](PROJECT-IDENTITY.md).

---

## 1. Persistence goals

SQLite is authoritative durable local game state.

Guarantees:

1. a quest rewards at most once;
2. one finish commits all progression or none;
3. distinct open quests may coexist for one hero/project;
4. concurrent matching starts converge through a DB-backed dedup invariant;
5. the active cap remains exactly enforced under races;
6. historical reward interpretation survives rule upgrades;
7. source/diffs/raw logs/prompts/secrets/full workspace paths are not stored;
8. migrations are reproducible and upgrade-tested;
9. crash recovery/backup are performed through SQLite semantics rather than file hacks.

---

## 2. EF boundaries

Infrastructure owns:

```text
HeroPassportDbContext
EF entities/configurations
migrations/model snapshot
SQLite transaction coordinator/stores/queries
database diagnostics/backup adapter
```

Use:

```text
IDbContextFactory<HeroPassportDbContext>
one short-lived context per operation/unit of work
```

Never:

```text
singleton/long-lived DbContext
share DbContext concurrently
inject DbContext into MCP tools/Razor components
lazy-loading proxies
EF InMemory as a SQLite substitute
```

---

## 3. Database execution model

Actual SQLite I/O is short and synchronous; do not wrap it in `Task.Run`.

Read-modify-write operations (`StartQuest`, `FinishQuest`, future mutation use cases) start a non-deferred Serializable transaction before reading the invariants they mutate. With selected Microsoft.Data.Sqlite 10.0.10 this is qualified as `BEGIN IMMEDIATE` behavior; implementation tests must keep proving it.

Read-only card/list operations do not take writer transactions.

---

## 4. Database location

See `CONFIGURATION.md`.

```text
Windows: %LOCALAPPDATA%\HeroPassport\data\hero-passport.db
macOS:   ~/Library/Application Support/HeroPassport/data/hero-passport.db
Linux:   $XDG_DATA_HOME/hero-passport/hero-passport.db
         fallback ~/.local/share/hero-passport/hero-passport.db
```

`HERO_PASSPORT_HOME` isolates dev/tests.

Supported writable DB profile is local filesystem on the same host. Known network filesystems are outside 0.1 supported WAL deployment; see `PERSISTENCE-RELIABILITY.md`.

---

## 5. Connection/PRAGMA policy

Build connection strings with `SqliteConnectionStringBuilder`.

```text
Mode=ReadWriteCreate
Cache=Default
Foreign Keys=True
Pooling=True
Default Timeout=5
```

Do not use `Cache=Shared` with WAL.

Required effective state:

```sql
PRAGMA journal_mode = WAL;
PRAGMA synchronous = FULL;
PRAGMA foreign_keys = ON;
```

Doctor/tests verify these plus the actual loaded:

```sql
SELECT sqlite_version();
```

Normal supported WAL runtime requires SQLite `>=3.51.3` under the v3.1 qualification policy.

---

## 6. IDs, time and JSON-safe numeric ceiling

IDs:

```text
Guid.CreateVersion7()
```

Persistence representation must round-trip exactly and be consistent across tables.

External HP-MCP UUID form is defined by `WIRE-CONTRACT.md`.

Time:

```text
UTC only
Application TimeProvider
HP-MCP output millisecond UTC canonicalization
```

Long-lived nonnegative counters/XP that can be exposed through JSON are bounded by:

```text
9_007_199_254_740_991
```

Use checked arithmetic and persistence validation; never wrap.

---

## 7. Core tables

```text
heroes
projects
hero_project_stats
quest_sessions
quest_reports
quest_report_skills
skills
hero_skills
traits
hero_traits
xp_events
app_settings
```

`app_settings` includes installation-local identity material such as the random `project_identity_salt_v1` used by `project-identity/1`. It is product state, not model input.

---

## 8. `heroes`

```text
id                    PK
name                  required bounded SafeText-compatible text
total_xp              integer 0..JSON-safe-max
trust                 integer 0..100
risk                  integer 0..100
created_at_utc
updated_at_utc
```

Level is derived from total XP/rules, not authoritative duplicated storage.

---

## 9. `projects`

```text
id                    PK
display_name          bounded text
workspace_fingerprint char(64), unique
identity_version      required, `project-identity/1`
created_at_utc
last_seen_at_utc
```

No full workspace path or Git remote URL.

Fingerprint is local identity aid, not authentication material.

`PROJECT-IDENTITY.md` defines Git common-dir/scoped/standalone behavior and the salted hash material.

---

## 10. `hero_project_stats`

Unique:

```text
(hero_id, project_id)
```

Fields:

```text
quests_started       >=0
quests_finished      >=0
quests_succeeded     >=0
total_xp_earned      0..JSON-safe-max
last_quest_at_utc    nullable
```

This is a projection for reads. Quest reports/events remain historical record.

---

## 11. `quest_sessions` — v3.1

```text
id                    PK
hero_id               FK
project_id            FK
quest_type            canonical key
goal                  SafeTextV1 bounded normalized text
dedup_key             32-byte SHA-256 / fixed 64-hex representation
dedup_key_version     integer, initially 1
status                open | finished
started_at_utc
finished_at_utc       nullable
created_at_utc
```

### 11.1 Retired naming

Before public 0.1 release:

```text
LogicalQuestKeyV1 -> QuestDedupKeyV1
logical_key       -> dedup_key
logical_key_version -> dedup_key_version
```

Reason: this key represents exact normalized retry declaration identity, not semantic natural-language task equivalence.

### 11.2 Dedup key

Normative algorithm is in `WIRE-CONTRACT.md`:

```text
SHA-256(UTF8(canonicalQuestType + "\n" + SafeTextV1(goal)))
```

Case is preserved.

### 11.3 Open dedup uniqueness

```sql
CREATE UNIQUE INDEX ux_quest_sessions_open_dedup
ON quest_sessions(hero_id, project_id, dedup_key_version, dedup_key)
WHERE status = 'open';
```

Observable uniqueness is normative even if exact EF migration SQL differs.

This is a final database backstop. Normal races serialize through the immediate writer transaction defined in `PERSISTENCE-RELIABILITY.md`.

### 11.4 Active query index

Conceptually:

```sql
CREATE INDEX ix_quest_sessions_active
ON quest_sessions(hero_id, project_id, status, started_at_utc DESC, id);
```

Exact layout may be tuned from query plans without changing API ordering semantics.

### 11.5 Active cap

Application policy:

```text
<=16 open quests per hero/project
```

This is enforced through a writer transaction that acquires writer intent **before** same-key lookup/count/insert. It is not a global unique constraint and not a custom mutex.

Count=15 + two concurrent distinct starts must finish exactly at count 16; one call returns HP133.

---

## 12. `quest_reports`

Exactly one per finished quest:

```text
id
quest_id              FK + UNIQUE
result                canonical key
summary               SafeTextV1 bounded normalized text
tests_mentioned       bool
scope_violations      0..20
user_corrections      0..20
build_status          canonical key
tests_status          canonical key
reward_rule_version
trust_risk_rule_version
trait_rule_version
base_xp
result_xp
bonus_xp
penalty_xp
xp_gained
trust_before
trust_after
risk_before
risk_after
level_before
level_after
total_xp_before
total_xp_after
created_at_utc
```

Persist enough immutable outcome data to return a finished retry without recalculating under newer rules.

`testsStatus != not_run` is valid only when `tests_mentioned=true`, according to `WIRE-CONTRACT.md`/Application validation.

---

## 13. `quest_report_skills`

```text
quest_report_id FK
ordinal         0..2
skill_key       canonical FK/key
xp_gained       >=0
```

Unique:

```text
(quest_report_id, ordinal)
(quest_report_id, skill_key)
```

Ordinal preserves semantic primary/secondary/tertiary order used by deterministic skill XP weighting.

---

## 14. `skills` / `hero_skills`

`skills` seeds the canonical set. MCP accepts canonical keys only; CLI/import may normalize documented aliases before Application.

`hero_skills`:

```text
(hero_id, skill_key) unique
xp 0..JSON-safe-max
updated_at_utc
```

No model-invented persistent skill keys.

---

## 15. `traits` / `hero_traits`

Traits are a small seeded rule catalog, not achievements.

`hero_traits` unique:

```text
(hero_id, trait_key)
```

Unlock is monotonic in rule v1.

---

## 16. `xp_events`

Immutable XP ledger.

```text
id
quest_id         FK + UNIQUE
hero_id          FK
project_id       FK
xp_delta         >=0
reward_rule_version
created_at_utc
```

`UNIQUE quest_id` is the last-resort double-reward barrier.

Never mutate/delete a prior XP event merely because a new rule version exists.

---

## 17. Start transaction

Normative sequence from `PERSISTENCE-RELIABILITY.md`:

```text
resolve/validate/context + SafeText + dedup key outside transaction
BEGIN non-deferred Serializable writer transaction
query matching open dedup key
  found -> return existing
count open hero/project
  >=16 -> HP133
insert quest + start projections
SaveChanges
COMMIT
```

Do not use a deferred read-then-upgrade pattern for this invariant.

---

## 18. Finish transaction

```text
BEGIN non-deferred Serializable writer transaction
load quest
context check
already finished -> persisted original outcome
calculate deterministic reward
insert quest report
insert report skills
insert UNIQUE xp event
update hero/skills/traits/project stats
mark quest finished
SaveChanges
COMMIT
```

No transaction spans actual agent work.

Success is not returned until COMMIT succeeds.

---

## 19. Busy/storage/corruption mapping

Detailed mapping lives in `PERSISTENCE-RELIABILITY.md`.

Core codes:

```text
HP202 database_busy
HP203 storage_full
HP204 storage_read_only
HP205 storage_io_error
HP206 database_corrupt
HP207 storage_constraint
HP208 unsupported_sqlite_version
HP211 unsupported_storage_location
```

Expected dedup/finish uniqueness is translated to domain retry/reload semantics before generic HP207.

---

## 20. Migrations

EF migrations from schema `0001`; never use `EnsureCreated` as product schema management.

Use EF provider migration locking; no second custom migration mutex/table.

Every release migration gate:

```text
empty -> latest
previous release fixture -> latest
model snapshot diff review
SQLite rebuild/destructive operation review
pending model-change gate
backup/recovery consideration
```

Do not blindly delete `__EFMigrationsLock`; diagnose explicit abandoned-lock state.

---

## 21. Crash/WAL recovery

Never manually delete/rename:

```text
hero-passport.db-wal
hero-passport.db-shm
rollback journals
```

SQLite owns journal recovery after unclean shutdown.

Crash-before-commit leaves no partial progression. Crash-after-commit-before-response is recovered by retrying explicit `questId` and returning the persisted result.

Child-process crash injection tests prove these states.

---

## 22. Backup

A logical `export` is not a physical DB backup.

A future/pre-migration live DB backup uses `SqliteConnection.BackupDatabase`, not raw `File.Copy` while active.

Verified backup flow:

```text
backup to temporary destination
open destination independently
PRAGMA quick_check
PRAGMA foreign_key_check
read schema/migration metadata
publish completed backup safely
```

Never replace the only good backup before the new one passes validation.

---

## 23. Doctor storage checks

Normal doctor checks:

```text
open database through SQLite
actual sqlite_version() >= qualified floor
journal_mode=WAL
synchronous=FULL
foreign_keys=ON
migration state/core schema
PRAGMA quick_check
PRAGMA foreign_key_check
known storage-location support
```

No automatic destructive repair.

---

## 24. Testing rule

Only real temporary file-backed SQLite proves:

```text
partial indexes
BEGIN IMMEDIATE writer behavior
active-cap race
finish race
busy timeout
WAL recovery
crash atomicity
backup consistency
migration behavior
```

EF InMemory cannot substitute for these tests.
