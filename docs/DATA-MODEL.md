# Hero Passport — data model and persistence specification

**Status:** Accepted baseline  
**Snapshot:** 2026-08-10  
**Database:** SQLite via EF Core 10.0.10  
**Native SQLite:** pinned through `SQLitePCLRaw.bundle_e_sqlite3 3.0.5`; actual runtime version verified by tests/doctor

## 1. Persistence goals

The database is the authoritative durable source for game state. Persistence must guarantee:

1. a quest can be rewarded at most once;
2. all mutations from one quest completion commit atomically;
3. historical reward interpretation survives rule upgrades;
4. source code, diffs, raw logs, prompts, secrets and full workspace paths are not stored;
5. migrations are reproducible and upgrade-tested;
6. local concurrent reads remain responsive while writes are short;
7. the schema remains understandable without an ORM-generated abstraction maze.

SQLite is selected because Hero Passport is a single-user local application with a very small write rate. A server database would add deployment/auth/backup complexity without solving an existing requirement.

---

## 2. EF Core boundaries

Infrastructure owns:

```text
HeroPassportDbContext
EF persistence entities
IEntityTypeConfiguration<T>
migrations
SQL-specific migration fragments
store/query implementations
```

Domain/Application do not expose EF entities.

Use:

```text
IDbContextFactory<HeroPassportDbContext>
```

Each Application operation gets one short-lived context through an Infrastructure store/coordinator and disposes it after the unit of work.

Do not:

```text
use a singleton DbContext
share one DbContext concurrently
inject DbContext into MCP tool classes
inject DbContext into Razor components
use lazy-loading proxies
use EF InMemory to simulate SQLite semantics
```

---

## 3. Synchronous database execution

Microsoft.Data.Sqlite documents that SQLite has no asynchronous I/O; async ADO.NET calls execute synchronously. The persistence implementation therefore uses synchronous database operations intentionally.

Rules:

- short bounded SQL only;
- no `Task.Run` wrapper around EF/SQLite calls;
- no “async all the way” ceremony around synchronous SQLite work;
- cancellation is checked before expensive/use-case stages and transaction commit boundaries where meaningful;
- long-running analytics/history queries are designed as bounded/paged reads rather than hidden blocking calls.

MCP adapter methods can be asynchronous because the SDK contract is asynchronous, but database work remains an explicit synchronous segment.

---

## 4. Database location

Never hardcode roaming `%APPDATA%` for the DB.

Canonical locations are defined in `CONFIGURATION.md`:

```text
Windows: %LOCALAPPDATA%\HeroPassport\data\hero-passport.db
macOS:   ~/Library/Application Support/HeroPassport/data/hero-passport.db
Linux:   $XDG_DATA_HOME/hero-passport/hero-passport.db
         fallback ~/.local/share/hero-passport/hero-passport.db
```

Tests/dev may isolate everything using `HERO_PASSPORT_HOME`.

Absolute paths are local process concerns and do not enter MCP DTOs or persisted project identity.

---

## 5. Connection policy

Build with `SqliteConnectionStringBuilder`.

Application baseline:

```text
Mode=ReadWriteCreate
Cache=Default
Foreign Keys=True
Pooling=True
Default Timeout=5
```

The 5-second timeout is Hero Passport policy, not a SQLite universal recommendation. It must survive concurrency tests before release. Writes should normally complete in milliseconds; a much longer wait is more harmful to an interactive agent than returning actionable `HP202 database_busy`.

Do not use `Cache=Shared` with WAL.

---

## 6. Required PRAGMA state

At initialization/verification:

```sql
PRAGMA journal_mode = WAL;
PRAGMA synchronous = FULL;
PRAGMA foreign_keys = ON;
```

Rationale:

- WAL permits readers while a writer is active;
- `FULL` prioritizes earned-progression durability over maximum write throughput;
- foreign keys are explicitly enabled rather than relying on an implicit native default.

Tests and `doctor` verify effective values.

Do not run `VACUUM`, aggressive checkpointing or maintenance on every quest finish.

Use SQLite's normal WAL auto-checkpoint initially. Add explicit maintenance only from measured evidence.

---

## 7. IDs and timestamps

Identifiers:

```text
UUIDv7 generated with Guid.CreateVersion7()
```

Persist as 16-byte/GUID-compatible EF representation or canonical text based on the final EF mapping benchmark; the choice must be consistent across all tables and tested for ordering/round-trip. Do not invent prefixed string IDs (`qst_...`) unless a product UX requirement justifies the additional converter contract.

External JSON uses canonical lowercase UUID text.

Timestamps:

```text
UTC only
```

Application uses `TimeProvider`. Persistence invariant rejects/normalizes accidental local/unspecified time according to one tested mapping policy. Do not model SQLite as if it has native `DateTimeOffset` arithmetic semantics.

---

## 8. Core schema

### 8.1 `heroes`

Purpose: durable global hero identity/progression.

Fields:

```text
id                    PK
name                  text, required, bounded
total_xp              integer >= 0
trust                 integer 0..100
risk                  integer 0..100
created_at_utc
updated_at_utc
```

Do not persist current level as the authoritative source; derive it from `total_xp` and the applicable current display curve where appropriate. Historical quest reports store before/after level projections needed for immutable reward replay.

### 8.2 `projects`

```text
id                    PK
display_name          text, bounded
workspace_fingerprint text, unique
identity_version      text/int, required
created_at_utc
last_seen_at_utc
```

No absolute workspace path by default.

`workspace_fingerprint` is a local identity aid, not authentication material.

### 8.3 `hero_project_stats`

Composite unique key:

```text
(hero_id, project_id)
```

Fields:

```text
hero_id FK
project_id FK
quests_started
quests_finished
quests_succeeded
total_xp_earned
last_quest_at_utc
```

This is a projection/summary for fast reads. Canonical event/report history remains the source for audit/reconstruction where relevant.

### 8.4 `quest_sessions`

```text
id                    PK
hero_id               FK
project_id            FK
quest_type            canonical key
goal                  bounded text
status                open | finished
started_at_utc
finished_at_utc       nullable
created_at_utc
```

Initial invariant: at most one open quest for a hero+project active slot.

SQLite implementation may use a filtered/partial unique index over `(hero_id, project_id)` where `status = 'open'`, implemented/tested in the migration if EF metadata cannot express the exact desired form portably.

Goal is untrusted compact text. It is data, not an instruction to the server.

### 8.5 `quest_reports`

One-to-one with completed quest.

```text
id
quest_id              FK + UNIQUE
result                canonical result
summary               bounded text
tests_mentioned       boolean
scope_violations      integer >= 0
user_corrections      integer >= 0
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

Persist the calculated breakdown required to explain/replay the original outcome. Do not rerun historical reward rules on retry.

### 8.6 `quest_report_skills`

Normalizes the 1..3 skills attributed to the completion.

```text
quest_report_id FK
skill_key
ordinal               0..2
xp_gained
```

Unique:

```text
(quest_report_id, skill_key)
(quest_report_id, ordinal)
```

### 8.7 `skills`

Canonical dictionary/seeding table:

```text
key                   PK canonical stable key
sort_order
is_active
introduced_rule_version
```

Localized label does **not** belong here as the canonical key. Localization lives in presentation resources/code.

Initial keys include at least:

```text
coding
testing_awareness
scope_control
documentation
tool_use
planning
research
debugging
review
maintenance
```

The exact initial dictionary is versioned in the engine spec/seed tests.

### 8.8 `hero_skills`

```text
hero_id FK
skill_key FK
total_xp >= 0
updated_at_utc
```

Composite PK/unique `(hero_id, skill_key)`.

### 8.9 `traits`

Canonical trait dictionary:

```text
key PK
introduced_rule_version
sort_order
is_active
```

### 8.10 `hero_traits`

```text
hero_id FK
trait_key FK
progress >= 0
is_unlocked
unlocked_at_utc nullable
updated_at_utc
```

Composite key `(hero_id, trait_key)`.

Unlocked traits never silently relock under v1 rules.

### 8.11 `xp_events`

Immutable XP ledger.

```text
id PK
hero_id FK
project_id FK
quest_id FK
amount >= 0
reward_rule_version
created_at_utc
```

Critical unique constraint:

```text
UNIQUE(quest_id)
```

This is the final DB-level barrier against double reward.

Do not use this table as a generic event bus.

### 8.12 `app_settings`

Typed product-state settings that belong to user data, not operator config.

Prefer rows with known keys and validated serializers/value shapes. Initial example:

```text
active_hero_id
```

Do not turn it into an arbitrary plugin metadata store.

---

## 9. FinishQuest transaction

Canonical write unit:

```text
BEGIN
  load quest
  load hero/project state

  if quest.status == finished:
      read persisted report/xp projection
      ROLLBACK/complete read-only path
      return alreadyFinished=true

  validate open quest
  compute reward in memory

  INSERT quest_report
  INSERT quest_report_skills
  INSERT xp_events        -- UNIQUE quest_id
  UPDATE heroes
  UPSERT hero_skills
  UPSERT hero_traits
  UPSERT hero_project_stats
  UPDATE quest_sessions -> finished
COMMIT
```

All reward computation happens before mutating durable state where practical.

If `xp_events.quest_id` uniqueness loses a race, reload the completed outcome and return retry semantics if the final state is valid; do not issue a second reward.

Implementation must prove this race path with a real SQLite concurrency test.

---

## 10. StartQuest transaction

```text
BEGIN
  resolve existing open quest for hero/project
  if exact logical match -> return existing
  if conflicting open quest -> HP132
  insert quest
  update project last_seen/stats where applicable
COMMIT
```

Logical match v1:

```text
canonical questType + normalized trimmed goal exact match
```

Do not use fuzzy/LLM matching for idempotency.

---

## 11. Read patterns

Purpose-built projections only.

Examples:

```text
HeroCardReadModel
CurrentQuestReadModel
RecentQuestReadModel
ProjectStatsReadModel
DiagnosticsReadModel
DashboardSnapshotReadModel
```

Use no-tracking reads where mutation is not required.

History is paged:

```text
ORDER BY finished_at_utc DESC, id DESC
LIMIT page_size
```

Prefer keyset/cursor pagination when history becomes a UI/API feature; avoid unbounded `ToList()`.

MCP does not expose full history in 0.1.0.

---

## 12. Migrations

Use EF migrations from migration 0001.

Never use `EnsureCreated` in product startup.

### 12.1 EF migration lock

EF Core 9+ acquires a database-wide migration lock; SQLite uses `__EFMigrationsLock`.

Do **not** add a custom mutex/file lock around migrations.

A killed migration can leave the lock abandoned. `doctor` detects and explains this condition; normal startup does not blindly delete it.

### 12.2 Release migration gates

For every schema change:

```text
fresh DB migration test
previous released DB -> new migration test
migration rollback/recovery analysis where supported
model snapshot review
foreign key/index review
destructive/rebuild-operation review
`dotnet ef migrations has-pending-model-changes`
```

SQLite can rebuild tables for some schema operations; migrations must be reviewed as generated SQL behavior, not blindly accepted because scaffolding succeeded.

---

## 13. Initialization and seeding

Initialization is idempotent.

Sequence:

```text
resolve/create app dirs
validate config
open DB
SELECT sqlite_version()
apply migrations
verify PRAGMAs
seed canonical skills
seed canonical traits
create default hero Nova only if no hero exists
ensure active-hero state is valid
close
```

Seeds are stable canonical keys. Updating localized names must not produce data migrations.

---

## 14. Backup/export

MVP export is logical JSON through Application read models, not raw copying while the DB is open.

A later raw backup command should use SQLite-supported backup semantics or a correctly coordinated checkpoint/copy process; never promise safe raw `*.db` file copying in WAL mode without accounting for `-wal`/`-shm` state.

Export contains no absolute workspace paths because they were never stored.

Export schema gets its own version (`export/1`).

---

## 15. Data deletion/reset

Destructive data operations are CLI/Web concerns, not MCP tools.

When introduced they must be explicit and scoped:

```text
reset hero progression
delete one project history
delete all local data
```

No “cleanup” command may silently remove an unrecognized database or migration lock.

---

## 16. Integrity checks / doctor

`doctor` verifies at minimum:

```text
file readable/writable
SQLite native version
PRAGMA journal_mode
PRAGMA synchronous
PRAGMA foreign_keys
PRAGMA integrity_check or quick_check policy
EF latest migration
pending model changes (developer/CI path)
possible abandoned __EFMigrationsLock
one active hero points to an existing hero
canonical seed dictionaries present
no duplicate xp event per quest by constraint
```

Use `quick_check` for routine lightweight diagnostic if measured acceptable; reserve full `integrity_check` for explicit deeper diagnostic if runtime cost becomes meaningful.

---

## 17. Sensitive/untrusted data policy

Allowed user/agent text:

```text
hero name
goal
summary
project display name
```

All are bounded and untrusted.

Forbidden persistence fields:

```text
source_code
file_content
diff
patch
raw_log
terminal_output
prompt
chat_history
environment
api_key
secret
workspace_path
arbitrary_metadata_json
```

Architecture tests inspect MCP contracts and persistence entities for prohibited broad field patterns.

---

## 18. Performance expectations

MVP does not need a benchmark lab, but implementation should record smoke baselines on release hardware/CI:

```text
cold init/startup
warm get_card
start_quest
finish_quest
current_quest
1000-quest card/history projection
```

Targets are set after first implementation measurement, not invented now. Correctness gates precede performance targets.

SQLite index selection is driven by actual query shapes:

```text
open quest lookup
recent quests per hero/project
hero skills
hero traits
xp ledger by hero/project/time
```

Avoid speculative indexes on every column.

---

## 19. Primary sources

See `REFERENCES.md`, especially:

- EF Core SQLite limitations;
- EF Core migrations/application guidance;
- Microsoft.Data.Sqlite async, connection-string and database-error docs;
- SQLite WAL and `PRAGMA synchronous` documentation.
