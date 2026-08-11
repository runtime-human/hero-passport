# AGENTS.md — Hero Passport

## Mission

Build Hero Passport as a portable local-first RPG state layer for AI coding agents. Codex is the first Qualified reference host; product semantics and HP-MCP/2 remain host-neutral.

Do not invent alternate architecture inside implementation PRs.

## Read before coding

Always read:

```text
docs/PRODUCT-SPEC.md
docs/ARCHITECTURE.md
```

Then read the relevant normative deep dive:

```text
project/Git binding      -> docs/PROJECT-IDENTITY.md
SQLite/write/recovery    -> docs/PERSISTENCE-RELIABILITY.md
MCP schemas/results      -> docs/WIRE-CONTRACT.md
RPG calculation          -> docs/ENGINE-SPEC.md
```

Also use the corresponding compact specs (`API-CONTRACTS`, `MCP-CONTRACT`, `DATA-MODEL`, `CONFIGURATION`, `SECURITY-PRIVACY`, `TESTING-QUALITY`). Normative precedence is in `docs/README.md`.

---

## Layer boundaries

```text
Domain
  deterministic game policy only
  no EF/MCP/CLI/HTTP/localization/filesystem/Git/config

Application
  typed semantic use cases + ports
  Domain dependency only
  no MCP SDK/host config/localized output

Infrastructure
  EF/SQLite/filesystem/config/project+hero binding adapters

App
  composition root
  CLI
  MCP stdio adapter
  presentation/localization

Web 0.2+
  Application/read models
  no DbContext in Razor components
```

Do not add generic repositories, MediatR/event-bus frameworks, runtime plugin frameworks, REST/GraphQL/gRPC, or HTTP MCP merely for hypothetical extensibility.

---

## HP-MCP/2

Exactly four tools in stable order:

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

Explicit registration only. No assembly-wide scanning.

Protocol:

```text
preferred semantics 2026-07-28
McpServerOptions.ProtocolVersion unset/null
qualification includes 2026-07-28 and 2025-11-25 paths
application state never depends on MCP sessions/connections
```

### Tool annotations

```text
start_quest          readOnly=false destructive=false idempotent=false openWorld=false
finish_quest         readOnly=false destructive=false idempotent=true  openWorld=false
list_active_quests   readOnly=true  destructive=false idempotent=true  openWorld=false
get_card             readOnly=true  destructive=false idempotent=true  openWorld=false
```

`start_quest` is retry-safe only while a matching normalized declaration remains open. The same arguments after finishing are allowed to create a new quest, therefore the MCP idempotent hint is false.

### Success representation

Every successful tool call:

```text
structuredContent = typed result object
content = exactly one TextContent containing minified JSON semantically equal to structuredContent
displayText = required human field inside the object
```

Do not substitute an unrelated compact status as the only TextContent fallback.

### Error representation

Business/validation errors:

```text
isError=true
exactly one safe TextContent
no structuredContent
```

Protocol framing/unknown-tool errors remain protocol errors.

### Runtime validation

Official C# SDK schema/data annotations do not enforce runtime argument validation. Every MCP boundary explicitly validates SafeText, enum, UUID, ranges, metrics consistency and skills before Application.

---

## SafeTextV1

Model-supplied `goal` and `summary`:

```text
valid Unicode scalar values only
reject unpaired surrogates
reject non-whitespace C0/C1 controls
reject bidi formatting controls listed in WIRE-CONTRACT.md
NFC
trim Unicode whitespace
collapse whitespace runs to ASCII space
Rune/scalar-aware length bounds
```

```text
goal     1..500 scalars
summary  1..2000 scalars
```

Never use `.Length` alone as the wire-length authority.

---

## Quest dedup semantics

`LogicalQuestKeyV1` is retired before public release.

Use:

```text
QuestDedupKeyV1 = SHA-256(
  UTF8(canonicalQuestType + "\n" + SafeTextV1(goal))
)
```

**Case is preserved.** Do not lowercase/case-fold goal text: coding identifiers may be case-sensitive.

Meaning is conservative retry deduplication of one normalized start declaration, not semantic equivalence of natural-language tasks.

Multiple distinct open quests may coexist for one hero/project.

```text
max open quests per hero/project = 16
```

Recovery/handoff uses explicit `questId` from `list_active_quests` rather than fuzzy semantic matching.

---

## Project identity

Follow `PROJECT-IDENTITY.md` exactly.

Core rules:

```text
explicit --project-root else process cwd
Git repository -> anchor on canonical git-common-dir
linked worktrees -> same project
normal nested cwd -> whole repo scope '.'
monorepo explicit subproject -> repo-relative explicit scope
submodule/nested repo -> separate project by default
bare repo -> rejected
non-Git -> standalone canonical directory
```

Do not:

```text
persist full workspace path
use remote URL as identity
write .git identity files
modify safe.directory
shell-interpolate Git commands
fall back to standalone when a repository exists but Git cannot safely resolve it
```

Project fingerprint is salted local SHA-256; it is not a credential or encryption.

---

## SQLite reliability

Follow `PERSISTENCE-RELIABILITY.md` exactly.

All read-modify-write use cases start a short non-deferred Serializable transaction **before invariant reads**. With selected Microsoft.Data.Sqlite 10.0.10 this is `BEGIN IMMEDIATE` behavior.

Start transaction:

```text
BEGIN writer
same dedup key lookup
active count
insert if <16
COMMIT
```

Finish transaction atomically writes report, XP event, hero/skills/traits/project stats and finished state.

```text
WAL
synchronous=FULL
foreign_keys=ON
Default Timeout=5
no Cache=Shared
no Task.Run DB wrappers
no Polly retry stack
no custom writer mutex
```

Release/runtime qualification checks `sqlite_version()`; normal supported WAL path requires `>=3.51.3` because SQLite fixed the 2026 WAL-reset corruption bug there.

Never `File.Copy` a live SQLite DB. Live backup uses SQLite/Microsoft.Data.Sqlite backup API and verifies the backup.

Never manually delete/rename `-wal` or `-shm` during recovery.

Writable DB on network filesystems is not a supported 0.1 profile.

---

## Persistence uniqueness

```text
quest_sessions open dedup uniqueness:
(hero_id, project_id, dedup_key_version, dedup_key) WHERE status='open'

UNIQUE quest_reports.quest_id
UNIQUE xp_events.quest_id
```

Concurrent count=15 + two distinct starts must finish with exactly 16 open quests, not 17.

Concurrent finish must produce exactly one report/event/reward mutation.

---

## Wire IDs/timestamps/numbers

```text
questId      canonical lowercase UUIDv7
Timestamp    YYYY-MM-DDTHH:mm:ss.fffZ
JSON long-lived integers <= 9_007_199_254_740_991
no current HP-MCP null fields
all nested schema objects additionalProperties:false
```

MCP `skillsUsed` accepts canonical skills only, 1..3, ordered primary->secondary->tertiary. CLI/import alias normalization is separate.

`testsStatus != not_run` requires `testsMentioned=true`.

---

## Privacy

Never add MCP fields/storage/logging for:

```text
source code
file contents
diffs/patches
raw logs
full prompts/chat history
secrets/API keys/tokens
environment bags
workspace paths
remote Git URLs
arbitrary metadata/context/payload bags
```

`questId` is an identifier, not a credential. Finish validates the quest against bound HeroId + ProjectId.

---

## RPG rules

Do not change rule set as a side effect of architecture work.

Canonical clean coding golden:

```text
60 base
+10 tests mentioned
+10 clean scope
+10 clear summary
+5 no corrections
=95 XP
```

Persist rule versions. Skill allocation conserves exact XP.

RU terminology:

```text
scope_control -> Контроль
clean scope bonus -> Бонус за контроль
scope violation -> Выход за задачу
```

---

## Testing gates

Changes to these areas require their deep-dive vectors, not only unit tests.

Minimum release evidence:

```text
ProjectIdentity worktree/monorepo/submodule/privacy vectors
SafeText/UUID/timestamp/wire goldens
same-dedup Start race
distinct Start race from count=15 -> exactly 16
concurrent Finish -> one XP event
child-process crash before commit -> no partial state
crash after commit before response -> retry-safe
live backup consistency
actual sqlite_version qualification
MCP 2026-07-28 + 2025-11-25 paths
structuredContent == parsed JSON TextContent
MCP Inspector
Codex E2E
cross-host RC smoke according to integrations/README.md
```

Do not claim implementation tests pass until product code exists and commands were actually run.

---

## Scope through 0.1

Still excluded:

```text
dashboard
achievements/items
runtime plugins
our own Streamable HTTP listener
remote OAuth/tenancy
generic REST/GraphQL/gRPC
MCP Apps/Tasks
cloud/team mode
continuous telemetry
LLM judge
source/diff ingestion
```
