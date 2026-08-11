# Hero Passport — Data Model and Persistence

**Status:** Accepted v3.2  
**Snapshot:** 2026-08-11  
**Database:** SQLite via EF Core 10.0.10 / Microsoft.Data.Sqlite 10.0.10

`PERSISTENCE-RELIABILITY.md` is normative for transactions/crash/backup. `PROJECT-IDENTITY.md` is normative for project identity.

## 1. Persistence goals

SQLite is authoritative durable local game state.

Guarantees:

1. at most one open Quest per Hero+Project;
2. safe retry identities are persisted atomically with their mutation;
3. a Quest commits progression at most once;
4. Finish commits all progression or none;
5. historical outcomes retain exact rule versions and deltas;
6. multiple Heroes and archive/delete semantics are explicit;
7. no source/diff/raw-log/prompt/secret/full-path storage;
8. migration/crash/backup behavior is executable-testable;
9. public identities are sync-ready UUIDv7, never rowids.

## 2. EF boundary

Infrastructure owns DbContext/entities/configurations/migrations/SQLite write coordination/read queries/backup/diagnostics.

Use `IDbContextFactory<HeroPassportDbContext>` and one short-lived context per operation/unit of work.

Do not use singleton DbContext, concurrent DbContext access, lazy-loading proxies or EF InMemory as a SQLite correctness substitute.

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

## 4. `app_settings`

Typed settings, not an arbitrary key/value dumping ground.

Required logical values:

```text
setup_completed
active_hero_id
locale
presentation_style
auto_start_quest
auto_finish_quest
project_identity_salt_v1
config_version
```

Implementation may use typed columns in a single installation row or a strictly allowlisted key/value table. Unknown model-provided keys are never persisted.

## 5. `heroes`

```text
id                    UUIDv7 PK
name                  SafeTextV1 1..64
total_xp              0..JSON-safe-max
trust                 0..100
strain                0..100
success_streak        >=0
archived_at_utc       nullable
created_at_utc
updated_at_utc
```

Hero Level, Rank and active Title are derived from rule tables/unlocks rather than authoritative duplicated values.

Archived Heroes retain all game history. Permanent delete explicitly removes it.

## 6. `projects`

```text
id                    UUIDv7 PK
display_name          bounded local presentation text
workspace_fingerprint char(64) UNIQUE
identity_version      `project-identity/1`
created_at_utc
last_seen_at_utc
```

No full workspace path or Git remote URL.

## 7. `hero_project_stats`

Unique `(hero_id, project_id)`.

```text
quests_started
quests_finished
quests_succeeded
total_xp_earned
last_quest_at_utc nullable
```

Success rate and top Skill contributions are derived/read projections.

## 8. `mutation_receipts`

Supports caller-generated request idempotency for resource/destructive mutations.

```text
operation_key       create_hero | start_quest | delete_hero
request_id          UUIDv7
args_hash           32-byte SHA-256 of canonical semantic arguments
entity_id           UUIDv7 text/binary value; no FK requirement
effective_at_utc
```

Unique:

```text
(operation_key, request_id)
```

The receipt contains no prompt/source/history payload. Its purpose is only safe retry and argument-mismatch detection.

For `start_quest`, `entity_id` is QuestId. For create it is HeroId. For delete it is the deleted HeroId and the timestamp is the deletion receipt needed for a late retry.

Mutation receipt insertion and the corresponding mutation are in one DB transaction.

## 9. Canonical argument hashing

Hash only already-validated canonical fields in a versioned, length-delimited binary encoding; do not concatenate arbitrary strings with ambiguous separators.

Initial `mutation-args/1` encoding:

```text
UTF-8 operation key length + bytes
for each field in fixed schema order:
  field tag byte
  byte length as fixed unsigned integer
  canonical UTF-8/value bytes
```

Then SHA-256 the complete buffer.

This hash proves retry argument equivalence; it is not task identity, authentication or semantic natural-language matching.

## 10. `quest_sessions`

```text
id                    UUIDv7 PK
hero_id               FK heroes
project_id            FK projects
quest_type            canonical key
title                 SafeTextV1 1..120
goal                  SafeTextV1 1..500
locale                 ru-RU | en-US
status                 open | finished
started_at_utc
finished_at_utc        nullable
created_at_utc
```

Start request identity lives in `mutation_receipts`; natural-language content is never a dedup key.

### One-open invariant

Database backstop:

```sql
CREATE UNIQUE INDEX ux_quest_sessions_one_open_per_hero_project
ON quest_sessions(hero_id, project_id)
WHERE status = 'open';
```

Normal races serialize through the writer transaction; this index is the final invariant barrier.

Active query index may additionally cover `(hero_id, project_id, status, started_at_utc, id)`.

## 11. `quest_reports`

Exactly one per finished Quest:

```text
id
quest_id                  FK + UNIQUE
result
summary

tests_mentioned
scope_violations
user_corrections
build_status
build_evidence
tests_status
tests_evidence

reward_rule_version
hero_progression_version
skill_progression_version
skill_allocation_version
trust_strain_rule_version
streak_rule_version
unlock_rule_version
rank_rule_version

base_xp
bonus_xp
penalty_xp
raw_xp
outcome_permille
xp_gained

hero_total_xp_before/after
hero_level_before/after
rank_before/after
trust_before/after
strain_before/after
streak_before/after
active_title_before/after

created_at_utc
```

Persist enough immutable result data to answer retries without recalculating under later rules.

## 12. `quest_reward_components`

One row per applied XP component:

```text
quest_report_id FK
ordinal
component_key
xp_delta signed integer
```

Examples: `base`, `observed_tests`, `clean_scope`, `clear_summary`, `no_corrections`, `scope_violation`, `user_correction`.

## 13. `quest_trust_strain_components`

```text
quest_report_id FK
ordinal
component_key
trust_delta signed
strain_delta signed
```

This preserves explainability of Trust/Strain changes.

## 14. `skills` and `hero_skills`

`skills` is a seeded canonical catalog.

`hero_skills` unique `(hero_id, skill_key)`:

```text
xp
updated_at_utc
```

Skill Level is derived from `skill-progression/*` threshold content.

## 15. `quest_report_skills`

```text
quest_report_id FK
ordinal 0..2
skill_key
xp_gained
xp_before
xp_after
level_before
level_after
```

Unique `(quest_report_id, ordinal)` and `(quest_report_id, skill_key)`.

## 16. Traits and Titles

Seeded catalogs:

```text
traits { trait_key, catalog_version }
titles { title_key, priority, catalog_version }
```

Hero unlock rows:

```text
hero_traits { hero_id, trait_key, unlocked_at_utc, source_quest_id }
hero_titles { hero_id, title_key, unlocked_at_utc, source_quest_id }
```

Unique by Hero + key. Unlock is monotonic for a living Hero under v3.2.

## 17. `quest_milestones`

Immutable bounded semantic milestone events generated by a completed Quest:

```text
quest_report_id
ordinal
event_key
semantic_key
flavor_key nullable
```

No rendered localized text is stored as game truth.

## 18. `xp_events`

Immutable ledger while the Hero exists:

```text
id
quest_id UNIQUE
hero_id
project_id
xp_delta >=0
reward_rule_version
created_at_utc
```

`UNIQUE quest_id` is the final double-reward barrier.

Permanent Hero deletion is an explicit privacy/lifecycle exception that removes this Hero’s ledger/history rows; normal balance upgrades never mutate them.

## 19. Start transaction

```text
resolve active Hero + Project
canonicalize request + args hash outside transaction
BEGIN non-deferred Serializable writer transaction
lookup mutation receipt(start_quest, startRequestId)
  found + hash equal -> load persisted Quest and return replay
  found + hash differs -> HP135
query open Quest for Hero+Project
  found -> HP133
insert Quest
insert mutation receipt pointing to Quest
update hero_project_stats quests_started
SaveChanges
COMMIT
```

The partial unique open index protects the invariant even if application logic regresses.

## 20. Finish transaction

```text
BEGIN writer
load Quest by questId
check process-bound ProjectId
already finished -> load immutable stored result
calculate current deterministic rules once
insert report/component/skill/milestone rows
insert UNIQUE xp_event
update Hero/Skills/Streak/unlocks/project stats
mark Quest finished
SaveChanges
COMMIT
```

Current active Hero is irrelevant to an existing Quest’s owner.

## 21. Hero management transactions

Create Hero:

```text
request receipt check
insert Hero
insert create receipt
commit
```

Activate Hero:

```text
validate non-archived target
update one installation active_hero_id
commit
```

Archive:

```text
reject active Hero
reject any open Quest owned by Hero
set archived_at_utc
commit
```

Restore clears archive state; it does not activate automatically.

Permanent delete:

```text
request receipt check
validate exact confirmation name
reject active Hero
reject any open Quest owned by Hero
insert minimal delete mutation receipt
delete Hero-owned history/progression rows explicitly/cascade by reviewed FK policy
commit
```

Never silently abandon Quests as part of Hero management.

## 22. Migrations

Use EF migrations from schema `0001`; never `EnsureCreated` for product schema management.

Every release migration gate:

```text
empty -> latest
previous release fixture -> latest
model snapshot diff review
SQLite rebuild/destructive operation review
foreign-key/index/partial-index review
pending-model-change gate
backup/recovery consideration
```

## 23. File location

```text
Windows: %LOCALAPPDATA%\HeroPassport\data\hero-passport.db
macOS:   ~/Library/Application Support/HeroPassport/data/hero-passport.db
Linux:   $XDG_DATA_HOME/hero-passport/hero-passport.db
         fallback ~/.local/share/hero-passport/hero-passport.db
```

`HERO_PASSPORT_HOME` is the dev/test isolation override.

## 24. Numeric/time rules

UUIDs: `Guid.CreateVersion7()`.

Time: injected `TimeProvider`, UTC persistence.

JSON-exposed long-lived integers must remain <= `9_007_199_254_740_991` with checked arithmetic.

## 25. Test rule

Only real temporary file-backed SQLite proves partial indexes, writer behavior, race invariants, busy behavior, WAL recovery, crash atomicity, backups and migrations. EF InMemory is not accepted evidence for those properties.
