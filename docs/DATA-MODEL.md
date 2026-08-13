# Hero Passport — Data Model and Persistence

**Status:** Accepted v3.2.1  
**Snapshot:** 2026-08-11  
**Database:** SQLite via EF Core 10.0.10 / Microsoft.Data.Sqlite 10.0.10

`PERSISTENCE-RELIABILITY.md` is normative for connection/transaction/crash/migration/backup behavior. `PROJECT-IDENTITY.md` is normative for Project identity.

## 1. Persistence goals

SQLite is authoritative durable local game state.

Required guarantees:

1. at most one open Quest per Hero+Project;
2. mutation retry identities are stored atomically with accepted mutations;
3. request hashing remains interpretable through `args_encoding_version`;
4. Start retry scope binds ProjectId + explicit HeroId + explicit request args;
5. Finish commits progression at most once and detects conflicting later finalization;
6. completed historical game facts retain rule versions/deltas;
7. mutable totals/stats are rebuildable projections;
8. multiple Heroes/archive/CLI logical delete are explicit;
9. no source/diff/raw-log/prompt/secret/full-path storage;
10. migration/crash/backup behavior is executable-testable.

Current UUIDv7 identities are **sync-conscious**, not a claim of implemented/solved sync.

## 2. EF boundary

Infrastructure owns DbContext/entities/configurations/migrations/SQLite write coordination/read models/backup/diagnostics.

Use `IDbContextFactory<HeroPassportDbContext>` and one short-lived context per operation/unit of work.

Never use singleton/concurrently accessed DbContext, lazy-loading proxies or EF InMemory as SQLite correctness evidence.

## 3. Core tables

```text
app_settings
heroes
projects
hero_project_stats
mutation_receipts
quest_sessions
quest_reports
quest_reward_components
quest_trust_strain_components
quest_report_skills
skills
hero_skills
traits
hero_traits
titles
hero_titles
quest_milestones
xp_events
```

## 4. `app_settings` — typed singleton

Exactly one row:

```text
id                       INTEGER PRIMARY KEY CHECK(id = 1)
setup_completed          INTEGER NOT NULL CHECK(setup_completed IN (0,1))
active_hero_id           nullable FK heroes(id) ON DELETE RESTRICT
locale                   TEXT NOT NULL CHECK(locale IN ('ru-RU','en-US'))
presentation_style       TEXT NOT NULL CHECK(presentation_style IN ('rpg_engineering','classic_rpg','minimal'))
auto_start_quest         INTEGER NOT NULL CHECK(auto_start_quest IN (0,1))
auto_finish_quest        INTEGER NOT NULL CHECK(auto_finish_quest IN (0,1))
project_identity_salt_v1 BLOB NOT NULL CHECK(length(project_identity_salt_v1) = 32)
config_version           INTEGER NOT NULL CHECK(config_version >= 1)
created_at_utc           TEXT NOT NULL
updated_at_utc           TEXT NOT NULL
```

Cross-field CHECK:

```text
(setup_completed=0 AND active_hero_id IS NULL)
OR
(setup_completed=1 AND active_hero_id IS NOT NULL)
```

Migration `0001` inserts row `id=1`, setup incomplete, active Hero null, safe default preferences and a generated local salt.

No generic configuration KV store is allowed for these settings.

## 5. `heroes`

```text
id                    UUIDv7 TEXT PRIMARY KEY
name                  SafeTextV1 1..64
total_xp              INTEGER NOT NULL CHECK(total_xp BETWEEN 0 AND JSON_SAFE_MAX)
trust                 INTEGER NOT NULL CHECK(trust BETWEEN 0 AND 100)
strain                INTEGER NOT NULL CHECK(strain BETWEEN 0 AND 100)
success_streak        INTEGER NOT NULL CHECK(success_streak >= 0)
archived_at_utc       nullable TEXT
created_at_utc        TEXT NOT NULL
updated_at_utc        TEXT NOT NULL
```

Initial values:

```text
total_xp = 0
trust = 50
strain = 20
success_streak = 0
```

Hero Level/Rank/active Title are derived from current rules/unlocks. `total_xp`, Trust/Strain and streak are mutable projections that must be rebuildable for a surviving Hero.

## 6. `projects`

```text
id                    UUIDv7 TEXT PRIMARY KEY
display_name          bounded presentation text
workspace_fingerprint TEXT NOT NULL UNIQUE CHECK(length(workspace_fingerprint)=64)
identity_version      TEXT NOT NULL CHECK(identity_version='project-identity/1')
created_at_utc        TEXT NOT NULL
```

No full workspace path, remote URL or read-driven last-seen timestamp.

Project rows are created by meaningful mutations when needed, not by read-only MCP tools.

Projects are not user-deletable in 0.1.

## 7. `hero_project_stats` — rebuildable projection

Unique `(hero_id, project_id)`.

```text
hero_id              FK heroes ON DELETE CASCADE
project_id           FK projects ON DELETE RESTRICT
quests_started       INTEGER NOT NULL CHECK(quests_started >= 0)
quests_finished      INTEGER NOT NULL CHECK(quests_finished >= 0)
quests_succeeded     INTEGER NOT NULL CHECK(quests_succeeded >= 0)
total_xp_earned      INTEGER NOT NULL CHECK(total_xp_earned >= 0)
last_quest_at_utc    nullable TEXT
```

Success rate/top Skills are derived read values.

## 8. `mutation_receipts`

Supports crash-safe caller request identity for:

```text
bootstrap
create_hero
start_quest
finish_quest
```

Logical fields:

```text
operation_key         TEXT NOT NULL CHECK(operation_key IN ('bootstrap','create_hero','start_quest','finish_quest'))
request_id            UUIDv7 TEXT NOT NULL
args_encoding_version TEXT NOT NULL
args_hash             BLOB NOT NULL CHECK(length(args_hash)=32)
result_kind           TEXT NOT NULL CHECK(result_kind IN ('bootstrap','hero','quest_start','quest_finish'))
result_entity_id      nullable UUIDv7 TEXT
project_id            nullable UUIDv7 TEXT
hero_id               nullable UUIDv7 TEXT
result_status         TEXT NOT NULL CHECK(result_status IN ('active','target_deleted'))
effective_at_utc      TEXT NOT NULL
```

Primary/unique key:

```text
PRIMARY KEY(operation_key, request_id)
```

`project_id`, `hero_id`, `result_entity_id` intentionally have **no FK**: minimal receipts must survive permanent Hero/history deletion.

Receipt contains no title/goal/summary/source/log/prompt/history payload.

## 9. Canonical mutation encoding

Initial version:

```text
mutation-args/1
```

Hash only already-validated canonical semantic fields in a fixed, length-delimited binary encoding. Do not hash ad-hoc JSON serializer bytes.

Encoding shape:

```text
encoding version tag
operation key length + UTF-8 bytes
for every operation field in fixed schema order:
  field tag byte
  fixed-width byte length
  canonical value bytes
```

Then SHA-256 the complete buffer.

Start canonical scope includes:

```text
ProjectId
HeroId
questType
title
goal
```

Finish canonical scope includes:

```text
questId
result
summary
all validated metrics in fixed field order
skillsUsed in canonical order
```

Bootstrap/create include all mutation inputs except request ID itself.

The stored `args_encoding_version` tells later releases how an old hash was produced.

## 10. Receipt lifecycle after permanent deletion

Permanent CLI Hero deletion does not erase request identity needed to prevent resurrection/replay confusion.

For receipts bound to the deleted Hero/its removed Quests:

```text
result_status = target_deleted
```

Only minimal IDs/hash/version/timestamp remain.

Late retry semantics may say “previously committed; target subsequently deleted” but never recreate the Hero/Quest or expose deleted history.

## 11. `quest_sessions`

```text
id              UUIDv7 TEXT PRIMARY KEY
hero_id         FK heroes ON DELETE CASCADE
project_id      FK projects ON DELETE RESTRICT
quest_type      TEXT NOT NULL CHECK(quest_type IN (...canonical seven...))
title           SafeTextV1 1..120
goal            SafeTextV1 1..500
locale          TEXT NOT NULL CHECK(locale IN ('ru-RU','en-US'))
status          TEXT NOT NULL CHECK(status IN ('open','finished'))
started_at_utc  TEXT NOT NULL
finished_at_utc nullable TEXT
created_at_utc  TEXT NOT NULL
```

Status/time CHECK:

```text
(status='open' AND finished_at_utc IS NULL)
OR
(status='finished' AND finished_at_utc IS NOT NULL)
```

Start request identity is in receipts; language text is never a dedup key.

One-open backstop:

```sql
CREATE UNIQUE INDEX ux_quest_sessions_one_open_per_hero_project
ON quest_sessions(hero_id, project_id)
WHERE status='open';
```

Linked worktrees share ProjectId, so this index deliberately prevents parallel independent same-Hero open Quests across linked worktrees in 0.1.

## 12. `quest_reports`

Exactly one per finished Quest:

```text
id                              UUIDv7 TEXT PRIMARY KEY
quest_id                        FK quest_sessions ON DELETE CASCADE UNIQUE
result                          CHECK IN success/partial/blocked/failed/abandoned
summary                         SafeTextV1 1..2000

tests_mentioned                 bool CHECK
scope_violations                INTEGER CHECK 0..20
user_corrections                INTEGER CHECK 0..20
build_status                     closed status CHECK
build_evidence                   closed evidence CHECK
tests_status                     closed status CHECK
tests_evidence                   closed evidence CHECK

finalization_args_encoding_version TEXT NOT NULL
finalization_args_hash             BLOB NOT NULL CHECK(length(...)=32)

reward_rule_version
hero_progression_version
skill_progression_version
skill_allocation_version
trust_strain_rule_version
streak_rule_version
unlock_rule_version
rank_rule_version

base_xp / bonus_xp / penalty_xp / raw_xp / outcome_permille / xp_gained
hero_total_xp_before / hero_total_xp_after
hero_level_before / hero_level_after
rank_before / rank_after
trust_before / trust_after
strain_before / strain_after
streak_before / streak_after
active_title_before / active_title_after
created_at_utc
```

Numeric XP/count fields are nonnegative or signed only where explicitly component deltas.

Cross-field DB CHECKs should encode feasible status/evidence combinations where practical; Application validation remains more descriptive.

Persist enough immutable result data to answer Finish retries without re-running later game rules.

## 13. Report component tables

`quest_reward_components`:

```text
quest_report_id FK report ON DELETE CASCADE
ordinal >=0
component_key
xp_delta signed integer
PRIMARY KEY(report, ordinal)
```

`quest_trust_strain_components`:

```text
quest_report_id FK report ON DELETE CASCADE
ordinal >=0
component_key
trust_delta signed
strain_delta signed
PRIMARY KEY(report, ordinal)
```

## 14. Skills

`skills` is seeded canonical catalog.

`hero_skills` rebuildable projection:

```text
hero_id      FK heroes ON DELETE CASCADE
skill_key    FK skills ON DELETE RESTRICT
xp           INTEGER NOT NULL CHECK(xp BETWEEN 0 AND JSON_SAFE_MAX)
updated_at_utc
UNIQUE(hero_id, skill_key)
```

`quest_report_skills` canonical delta rows:

```text
quest_report_id FK report ON DELETE CASCADE
ordinal CHECK 0..2
skill_key FK skills ON DELETE RESTRICT
xp_gained >=0
xp_before >=0
xp_after >=0
level_before
level_after
UNIQUE(report, ordinal)
UNIQUE(report, skill_key)
```

Skill Level is derived from the versioned threshold table recorded on the report.

## 15. Traits and Titles

Seeded catalogs:

```text
traits(trait_key PK, catalog_version)
titles(title_key PK, priority, catalog_version)
```

Canonical unlock rows:

```text
hero_traits(hero_id FK CASCADE, trait_key FK RESTRICT, unlocked_at_utc, source_quest_id nullable)
hero_titles(hero_id FK CASCADE, title_key FK RESTRICT, unlocked_at_utc, source_quest_id nullable)
```

Unique Hero+key. `source_quest_id` may be `ON DELETE SET NULL`; Hero delete still cascades the unlock row itself.

Active Title is derived projection/read logic, not duplicated authoritative state.

## 16. `quest_milestones`

Canonical semantic events only:

```text
quest_report_id FK report ON DELETE CASCADE
ordinal >=0
event_key
semantic_key
PRIMARY KEY(report, ordinal)
```

No authoritative `flavor_key`/rendered localized text. Presentation may evolve independently.

## 17. `xp_events`

Immutable while Hero exists:

```text
id             UUIDv7 TEXT PRIMARY KEY
quest_id       FK quest_sessions ON DELETE CASCADE UNIQUE
hero_id        FK heroes ON DELETE CASCADE
project_id     FK projects ON DELETE RESTRICT
xp_delta       INTEGER NOT NULL CHECK(xp_delta >= 0)
reward_rule_version TEXT NOT NULL
created_at_utc TEXT NOT NULL
```

`UNIQUE quest_id` is the final double-reward barrier.

## 18. Canonical history vs rebuildable projections

Canonical surviving history:

```text
quest_sessions + quest_reports
xp_events
reward/trust-strain/report-skill component rows
hero_traits / hero_titles unlock rows
quest_milestones
rule versions
```

Rebuildable projections:

```text
heroes.total_xp
heroes.trust
heroes.strain
heroes.success_streak
hero_skills
hero_project_stats
```

Projection rebuild starts from Hero initial defaults and ordered canonical completed Quest history. Release tests require rebuilt public card/project stats to match pre-rebuild values exactly.

This is not a generic event-sourcing architecture.

## 19. Bootstrap transaction

```text
canonicalize bootstrap args/hash
BEGIN writer
receipt(bootstrap, bootstrapRequestId)?
  same hash -> replay stored bootstrap target
  changed -> HP135
else if setup_completed=1 -> HP002
else:
  insert initial Hero
  update singleton settings + active Hero
  insert bootstrap receipt
COMMIT
```

Concurrent different bootstrap requests serialize: one initializes, the other gets HP002 after waiting/semantic re-evaluation.

## 20. Start transaction

```text
resolve ProjectId
canonicalize request including ProjectId + explicit HeroId
BEGIN writer
receipt(start_quest, startRequestId)?
  same encoding/hash -> load original Quest or target-deleted safe state
  changed -> HP135
validate setup + explicit Hero exists/not archived
snapshot settings.locale
query open Hero+Project -> HP133 if found
create Project row if this meaningful mutation needs it
insert Quest
insert receipt with ProjectId/HeroId/QuestId
update hero_project_stats
COMMIT
```

No `active_hero_id` read determines ownership.

## 21. Finish transaction

```text
resolve ProjectId
canonicalize finish payload
BEGIN writer
receipt(finish_quest, finishRequestId)?
  same -> replay
  changed -> HP135
load Quest by questId + verify ProjectId
if finished:
  canonical payload hash equals report.finalization hash
    -> insert/accept this request receipt; return original, alreadyFinalized=true
  differs
    -> HP136
else:
  calculate current versioned rules once
  insert report + component/Skill/milestone rows
  insert UNIQUE xp_event
  insert finish receipt
  update rebuildable projections/unlocks
  mark Quest finished
COMMIT
```

Current active Hero is irrelevant to existing Quest ownership.

## 22. Hero management

Create Hero uses `createRequestId` receipt.

Activate changes singleton default only.

Archive rejects active default Hero and any Hero with an open Quest in any Project.

Restore clears archive state and does not activate.

Permanent logical delete is CLI-only:

```text
reject active Hero
reject any open Quest owned by Hero
mark related surviving mutation receipts target_deleted
remove Hero-owned history/projections through reviewed cascade/explicit deletes
commit
```

Never silently abandon Quest as a side effect.

## 23. FK policy summary

```text
app_settings.active_hero_id -> heroes        RESTRICT
hero_project_stats.hero_id -> heroes         CASCADE
hero_project_stats.project_id -> projects    RESTRICT
quest_sessions.hero_id -> heroes             CASCADE
quest_sessions.project_id -> projects        RESTRICT
quest_reports.quest_id -> quest_sessions     CASCADE
report children -> quest_reports             CASCADE
hero_skills.hero_id -> heroes                CASCADE
hero_skills.skill_key -> skills              RESTRICT
hero_traits/hero_titles.hero_id -> heroes     CASCADE
catalog keys -> catalog tables               RESTRICT
xp_events.quest_id -> quest_sessions          CASCADE
xp_events.hero_id -> heroes                   CASCADE
xp_events.project_id -> projects              RESTRICT
mutation receipt context/target IDs           NO FK
```

## 24. Migrations

Use EF migrations from schema `0001`; never product `EnsureCreated`.

Initial migration must include all important CHECK/FK/partial-index/singleton invariants because later SQLite changes can require table rebuilds.

Release matrix:

```text
empty -> latest
previous release fixture -> latest
model snapshot diff
CHECK/FK/index/partial-index review
migration lock/crash recovery
backup/recovery consideration
projection rebuild check
quick_check + foreign_key_check
```

## 25. File location

```text
Windows: %LOCALAPPDATA%\HeroPassport\data\hero-passport.db
macOS:   ~/Library/Application Support/HeroPassport/data/hero-passport.db
Linux:   $XDG_DATA_HOME/hero-passport/hero-passport.db
         fallback ~/.local/share/hero-passport/hero-passport.db
```

`HERO_PASSPORT_HOME` is dev/test isolation override.

## 26. Test rule

Only real temporary file-backed SQLite proves partial indexes, writer behavior, races, busy behavior, connection pragmas, WAL recovery, crash atomicity, migration locking, backups and projection rebuild.

EF InMemory is not accepted evidence for those properties.
