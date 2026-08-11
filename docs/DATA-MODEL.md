# Hero Passport — Data Model and Persistence

**Status:** Accepted v3  
**Snapshot:** 2026-08-11  
**Database:** SQLite via EF Core 10.0.10  
**Native bundle:** SQLitePCLRaw.bundle_e_sqlite3 3.0.5; actual runtime SQLite verified by tests/doctor

## 1. Persistence goals

SQLite is authoritative durable game state. Persistence guarantees:

1. a quest rewards at most once;
2. one finish either commits all progression or none;
3. multiple distinct open quests may coexist per hero/project;
4. repeated/concurrent starts of the same logical work item converge;
5. historical reward interpretation survives rule upgrades;
6. local code/diffs/raw logs/prompts/secrets/full workspace paths are not stored;
7. schema/migrations are reproducible and upgrade-tested;
8. concurrent local readers remain practical while writes are short.

---

## 2. EF boundaries

Infrastructure owns `HeroPassportDbContext`, EF entities/configurations, migrations and storage/query adapters.

Use:

```text
IDbContextFactory<HeroPassportDbContext>
one short-lived context per operation/unit of work
```

Do not:

```text
singleton/long-lived DbContext
share DbContext concurrently
inject DbContext into MCP tool classes
inject DbContext into Razor components
lazy-loading proxies
EF InMemory as SQLite substitute
```

---

## 3. Database execution model

Microsoft.Data.Sqlite has no true async SQLite I/O. Persistence therefore uses short synchronous database calls deliberately.

Rules:

```text
no Task.Run around DB work
short transactions
bounded/paged reads
check cancellation before entering expensive/commit stages where meaningful
no hidden long-running analytics on MCP path
```

---

## 4. Database path

Canonical locations are defined in `CONFIGURATION.md`.

```text
Windows: %LOCALAPPDATA%\HeroPassport\data\hero-passport.db
macOS:   ~/Library/Application Support/HeroPassport/data/hero-passport.db
Linux:   $XDG_DATA_HOME/hero-passport/hero-passport.db
         fallback ~/.local/share/hero-passport/hero-passport.db
```

`HERO_PASSPORT_HOME` isolates tests/dev.

---

## 5. Connection and PRAGMA policy

Build with `SqliteConnectionStringBuilder`.

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
PRAGMA journal_mode=WAL;
PRAGMA synchronous=FULL;
PRAGMA foreign_keys=ON;
```

Doctor/tests verify effective values and `SELECT sqlite_version()`.

---

## 6. IDs and time

Generate internal IDs with `.NET Guid.CreateVersion7()`.

External JSON uses lowercase canonical UUID text.

Persist timestamps as UTC; Application obtains current time from `TimeProvider`.

Do not pretend SQLite provides rich native `DateTimeOffset` semantics.

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

---

## 8. `heroes`

```text
id                    PK
name                  required bounded text
total_xp              integer >=0
trust                 integer 0..100
risk                  integer 0..100
created_at_utc
updated_at_utc
```

Level is derived from XP/rule curve, not the authoritative stored source.

---

## 9. `projects`

```text
id                    PK
display_name          bounded text
workspace_fingerprint unique text
identity_version      required version
created_at_utc
last_seen_at_utc
```

No absolute path by default.

Fingerprint is identity aid, not auth credential.

---

## 10. `hero_project_stats`

Unique:

```text
(hero_id, project_id)
```

Projection fields:

```text
quests_started
quests_finished
quests_succeeded
total_xp_earned
last_quest_at_utc
```

This is derived/summary state; immutable quest reports/events remain the historical record.

---

## 11. `quest_sessions` — architecture v3

```text
id                    PK
hero_id               FK
project_id            FK
quest_type            canonical key
goal                  bounded original text
logical_key           SHA-256 canonical representation
logical_key_version   integer/string version, initially 1
status                open | finished
started_at_utc
finished_at_utc       nullable
created_at_utc
```

### 11.1 Removed v2 invariant

Do **not** enforce one open quest for `(hero_id, project_id)`.

That would make parallel agents/workstreams conflict for no domain reason.

### 11.2 Logical open-quest uniqueness

Create a partial unique index equivalent to:

```sql
CREATE UNIQUE INDEX ux_quest_sessions_open_logical
ON quest_sessions(hero_id, project_id, logical_key_version, logical_key)
WHERE status = 'open';
```

Exact EF migration SQL may differ but the observable constraint is normative.

This gives DB-backed convergence for concurrent matching starts.

### 11.3 Active query index

Create an index supporting bounded active listing, conceptually:

```sql
CREATE INDEX ix_quest_sessions_active
ON quest_sessions(hero_id, project_id, status, started_at_utc DESC, id);
```

SQLite/index syntax and query plan are verified in tests; exact descending/index-layout may be adjusted from measured plans without changing API semantics.

### 11.4 Logical key v1

Input:

```text
canonical quest type
original validated goal
```

Canonical key text:

```text
Unicode NFC
trim
collapse whitespace runs to one ASCII space
invariant case normalization
```

Hash:

```text
SHA-256 UTF-8(questType + "\n" + canonicalGoal)
```

Persist both hash and version.

The hash is not secret/security material.

---

## 12. Active quest cap

Application policy v1 allows at most 16 open quests per hero/project.

The logical-key unique index alone does not enforce this count. The write transaction must enforce the cap consistently.

Initial implementation strategy:

```text
SQLite write transaction
-> query active count
-> insert if below cap
```

Because SQLite serializes writers, tests must verify two distinct concurrent starts at count 15 cannot both commit and produce 17 open rows. If the selected EF transaction mode does not provide that guarantee, use a SQLite-appropriate immediate write transaction/SQL path localized in Infrastructure rather than weakening the product cap.

Do not add a distributed lock.

---

## 13. `quest_reports`

One-to-one completed quest:

```text
id
quest_id              FK + UNIQUE
result
summary               bounded text
tests_mentioned
scope_violations
user_corrections
build_status
tests_status
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
finished_at_utc
```

Store sufficient reward projection to return an already-finished retry without recalculating under later rules.

No raw test/build logs.

---

## 14. `quest_report_skills`

Immutable snapshot of skill allocation for one report:

```text
quest_report_id FK
skill_key
position
xp_gained
```

Unique per report/key. Position preserves canonical reward distribution ordering.

---

## 15. Skills

`skills`:

```text
key PK
```

`hero_skills`:

```text
hero_id FK
skill_key FK
total_xp
updated_at_utc
UNIQUE(hero_id, skill_key)
```

Only canonical keys are persisted.

---

## 16. Traits

`traits` stores canonical trait definitions/keys where persisted seeding is useful.

`hero_traits` stores progress/unlock state:

```text
hero_id
trait_key
progress
unlocked_at_utc nullable
updated_at_utc
UNIQUE(hero_id, trait_key)
```

Trait rules are deterministic/versioned in reports, not dynamically interpreted from DB strings.

---

## 17. `xp_events`

Append-only reward ledger:

```text
id                    PK
hero_id               FK
project_id            FK
quest_id              FK
amount                integer >=0
reward_rule_version
created_at_utc
```

Critical invariant:

```text
UNIQUE(quest_id)
```

This is the final DB-level double-award barrier.

---

## 18. `app_settings`

Persist product state/settings that are genuinely database state, for example active hero selector if modeled there.

Do not use as arbitrary JSON bag or duplicate file configuration.

---

## 19. StartQuest transaction

Conceptual algorithm:

```text
begin write transaction
resolve/load hero/project
compute logical key
query matching open logical key
  if found -> return existing, rollback/read-only exit as appropriate
count active quests
  if >=16 -> HP133
insert quest_session
update project/hero_project_stats start counters
commit
```

### 19.1 Same-key race

If two writers race the same key:

- one wins insertion;
- the loser encounters unique constraint after writer serialization/race path;
- loser reloads the now-open matching quest and returns `alreadyOpen=true`;
- do not surface an internal constraint error to the model.

### 19.2 Different-key race at cap

Must preserve max-16 policy. Integration tests run real file-backed SQLite with two contexts/process-like tasks.

---

## 20. FinishQuest transaction

```text
begin write transaction
load quest + hero/project state
verify quest context equals bound HeroOperationContext
  else HP134
if finished:
  load original report/reward projection
  return without mutation
normalize skills
calculate reward/trust/risk/traits in memory
insert quest_report
insert quest_report_skills
insert xp_event (UNIQUE quest_id)
update hero totals
update skills/traits
update hero_project_stats
mark quest finished
commit
```

If unique XP insertion loses a finish race, rollback and reload the completed persisted outcome.

Never partially commit aggregate changes before retry resolution.

---

## 21. Context mismatch

A valid UUID from another hero/project is not enough to operate on that quest.

Application/Infrastructure load must prove:

```text
quest.hero_id == context.hero_id
quest.project_id == context.project_id
```

Mismatch -> `HP134 quest_context_mismatch`.

This is a correctness/privacy boundary in local MVP and becomes an authorization prerequisite for any future remote deployment.

---

## 22. Read models

Purpose-built projections:

```text
HeroCardReadModel
ActiveQuestReadModel
ProjectStatsReadModel
RecentQuestReadModel
DashboardSnapshotReadModel   # 0.2
```

`ListActiveQuests` projects directly and uses no tracking.

Order:

```text
started_at_utc DESC
id ASC
```

Limit <=16.

EF entities never escape Infrastructure.

---

## 23. Migrations

Use EF migrations from the initial schema; never product `EnsureCreated`.

EF SQLite migration locking uses `__EFMigrationsLock`; do not add a custom mutex/lock table.

Every release migration gate includes:

```text
fresh DB migration
upgrade from previous released DB
model snapshot consistency
SQLite rebuild-operation review
data preservation check
backup/restore consideration
```

Destructive migration requires explicit ADR/data migration plan.

---

## 24. Backup/export boundary

Export is not a raw database dump by default. It projects documented safe product state and excludes local filesystem paths/config secrets.

A future backup command that copies the DB must perform a correct SQLite/WAL-aware checkpoint/backup procedure; do not copy a live main DB file naïvely while WAL contains uncheckpointed writes.

---

## 25. Future HTTP/multi-tenant note

The local schema can remain useful for project-scoped HTTP, but it is **not automatically a multi-tenant schema**. Public hosting requires explicit principal/tenant ownership columns/authorization design and likely a different operational database strategy.

Do not add tenant columns to the local MVP “just in case”.
