# Hero Passport v3.1 — Consolidated Design Specification

**Status:** Accepted implementation design snapshot  
**Date:** 2026-08-11  
**Target:** 0.1.0

This file is the consolidated design used by implementation agents. It deliberately points to detailed normative contracts instead of duplicating their algorithms.

Normative precedence is `docs/README.md`.

Required deep dives:

```text
PROJECT-IDENTITY.md
PERSISTENCE-RELIABILITY.md
WIRE-CONTRACT.md
```

---

## 1. Product

Hero Passport is a local-first RPG passport/state layer for AI coding agents.

Core loop:

```text
meaningful work
 -> hero.start_quest
 -> agent works normally
 -> hero.finish_quest
 -> deterministic XP/skills/traits/Trust/Risk
 -> local SQLite history
```

Entertainment/companion value is primary. It is not employee monitoring, a code scanner, agent orchestrator or enterprise telemetry product.

---

## 2. Architecture

```text
Domain <- Application <- Infrastructure <- App
Web 0.2+ -> Application
```

MCP/CLI are adapters. Application semantics are transport-neutral. SQLite is local authoritative state.

No runtime plugin framework, message bus, REST/GraphQL/gRPC or HTTP MCP in 0.1.

---

## 3. HP-MCP/2

Exact tool inventory:

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

Protocol:

```text
preferred 2026-07-28 semantics
ProtocolVersion null/unset
2025-11-25 compatibility qualification
stdio only in 0.1
```

Exact wire contract: `WIRE-CONTRACT.md`.

Key v3.1 corrections:

```text
start idempotent hint = false
finish/list/card idempotent = true

success:
  structuredContent typed object
  one minified JSON TextContent semantically equal to structuredContent

business/validation error:
  isError=true
  one safe TextContent
  no structuredContent
```

Generated schemas never substitute for runtime validation.

---

## 4. Untrusted text

`goal` and `summary` pass through `SafeTextV1` before persistence/use.

Properties:

```text
valid Unicode scalars
NFC
safe single-line whitespace normalization
control/bidi formatting deny-list
scalar-aware bounds
```

```text
goal 1..500
summary 1..2000
```

---

## 5. Start retry/dedup semantics

Retired before public release:

```text
LogicalQuestKeyV1
case-folded natural-language semantic key
```

Use:

```text
QuestDedupKeyV1 = SHA256(questType + newline + SafeTextV1(goal))
```

Case preserved.

This means identical normalized open declaration, not semantic task identity.

Multiple distinct active quests per hero/project are allowed, hard cap 16.

After a quest finishes, the same declaration may start a new quest. Recovery/handoff uses `list_active_quests` and explicit `questId`.

---

## 6. Operation context

Application receives:

```text
HeroOperationContext
  HeroId
  ProjectId
  InvocationOrigin
```

Client metadata is diagnostics only.

Model does not routinely supply:

```text
heroId
projectId
workspacePath
locale
presentation mode
client identity
```

---

## 7. Project identity

Exact design: `PROJECT-IDENTITY.md`.

Summary:

```text
binding start = --project-root else cwd
Git anchor = canonical git-common-dir
scope . by default
explicit in-repo --project-root -> explicit repo-relative scope
linked worktrees -> same project
submodule/nested repo -> separate
non-Git -> standalone local path identity
```

Fingerprint is salted SHA-256 under `project-identity/1`; full path/remote URL not persisted.

Git safety failures never silently become standalone projects and Hero Passport never writes `safe.directory`.

---

## 8. Persistence

Exact design: `PERSISTENCE-RELIABILITY.md`.

```text
SQLite + EF Core
IDbContextFactory
short synchronous DB calls
WAL
synchronous=FULL
foreign_keys=ON
Default Timeout=5
same-host local writable filesystem
```

Read-modify-write operations begin non-deferred Serializable transaction before invariant reads. Selected Microsoft.Data.Sqlite 10.0.10 is qualified to use `BEGIN IMMEDIATE` for this path.

This serializes local writers and makes the 16-active cap and finish idempotency race-safe without a custom mutex.

---

## 9. Schema

Core tables:

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

`quest_sessions` includes:

```text
dedup_key
dedup_key_version
```

Partial unique open dedup key:

```text
(hero_id, project_id, dedup_key_version, dedup_key)
WHERE status='open'
```

`quest_reports.quest_id` and `xp_events.quest_id` are unique.

No absolute workspace path column.

---

## 10. Start transaction

Preparation outside transaction:

```text
context
SafeText validation
QuestDedupKey
```

Writer transaction:

```text
BEGIN IMMEDIATE semantics
matching open dedup lookup
active count
insert if count <16
commit
```

Release race golden:

```text
initial count 15
2 distinct concurrent starts
=> final count exactly 16
=> one HP133
```

---

## 11. Finish transaction

```text
BEGIN writer
load quest
hero/project context check
already finished -> stored outcome
else deterministic reward
report + skill rows + unique xp event
hero + skills + traits + project projection
mark finished
COMMIT
```

Crash after commit but before response is recovered by retrying explicit questId and returning stored outcome.

---

## 12. SQLite qualification/recovery

Runtime qualification queries actual:

```text
sqlite_version()
```

Supported normal WAL floor:

```text
>=3.51.3
```

No manual deletion/renaming of WAL/SHM/journal files.

No writable-network-filesystem support in 0.1.

No raw live-DB `File.Copy`. Physical backup uses SQLite/Microsoft.Data.Sqlite online backup API and verifies the destination.

---

## 13. RPG engine

Rules remain v1:

```text
reward/1.0.0
trust-risk/1.0.0
traits/1.0.0
```

Clean coding golden:

```text
60 +10 tests +10 clean scope +10 clear summary +5 no corrections = 95 XP
```

Skill XP:

```text
1 skill 100
2 skills 60/40
3 skills 50/30/20
```

Cumulative-floor allocation conserves XP exactly.

MCP skill array is ordered primary->secondary->tertiary and canonical-only.

---

## 14. Canonical wire profile

From `WIRE-CONTRACT.md`:

```text
UUID       lowercase canonical UUIDv7
Timestamp  YYYY-MM-DDTHH:mm:ss.fffZ
Long int   <= 9_007_199_254_740_991
Enums      lower_snake_case
Current optional/null fields: none
Closed nested objects
```

`testsStatus != not_run` requires `testsMentioned=true`.

---

## 15. Privacy

Do not request/persist:

```text
source/file contents
diffs/patches
raw logs
full prompts/chat history
secrets/API keys/tokens
environment dumps
workspace paths
Git remote URLs
generic metadata/context bags
```

`questId` is not a credential; finish verifies bound HeroId+ProjectId.

---

## 16. Errors

Stable typed Application errors, including:

```text
HP100 invalid_request
HP130 quest_not_found
HP133 active_quest_limit
HP134 quest_context_mismatch
HP202 database_busy
HP203 storage_full
HP204 storage_read_only
HP205 storage_io_error
HP206 database_corrupt
HP208 unsupported_sqlite_version
HP211 unsupported_storage_location
HP310 invalid_project_binding
HP311 git_repository_unavailable
HP312 git_required_for_repository_binding
HP313 bare_repository_unsupported
HP900 internal_error
```

Adapters preserve semantic meaning and redact internals.

---

## 17. Testing

Release-blocking categories:

```text
Domain rules
SafeText/Dedup semantics
ProjectIdentity real Git/worktree/submodule vectors
SQLite provider immediate-writer behavior
same-key start race
15->16 active-cap race
finish race
child-process crash before/after commit
WAL recovery
backup consistency
SQLite runtime floor
HP-MCP schema/result goldens
MCP 2026 + 2025 compatibility
Inspector
Codex E2E
cross-host RC smoke
AgentEvals
```

No product test pass is claimed during the documentation-only architecture phase.

---

## 18. Deferred

Through 0.1:

```text
Blazor dashboard (0.2)
achievements/items
runtime plugins
own Streamable HTTP/OAuth
public multi-tenant service
MCP Apps/Tasks
cloud/team mode
continuous telemetry
LLM judge
source/diff ingestion
```

---

## 19. Implementation handoff

Execute:

```text
docs/superpowers/plans/2026-08-10-hero-passport-implementation.md
```

The plan is subordinate to this design and the normative deep dives. If implementation evidence conflicts with an assumed provider/SDK behavior, reproduce it with a focused test, update the relevant spec/ADR, and only then continue.
