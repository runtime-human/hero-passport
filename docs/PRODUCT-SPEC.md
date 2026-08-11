# Hero Passport — Product Specification

**Status:** Accepted v3.1 product contract  
**Snapshot:** 2026-08-11  
**Target:** 0.1.0 Portable Local MCP Core

---

## 1. Product definition

Hero Passport is a local-first RPG passport for AI coding agents.

```text
start quest
-> agent works normally
-> finish explicit quest
-> deterministic XP/skills/traits/Trust/Risk
-> compact result
-> durable local history
```

It is entertainment/companion-first, not employee monitoring, code surveillance, LLM quality judging or an agent orchestration platform.

---

## 2. Positioning

Hero Passport is MCP-portable, not Codex-only.

```text
reference Qualified host: Codex
portable model integration: MCP
0.1 transport: local stdio
```

Host config syntax never defines product semantics.

---

## 3. Primary experience

```text
User asks an agent to implement/review/debug/research/document meaningful work.
Agent calls hero.start_quest.
Hero Passport returns questId.
Agent works normally.
Agent calls hero.finish_quest(questId,...).
Agent surfaces Hero Passport displayText/result.
```

Another agent may concurrently work on a different quest in the same project.

A repeated **same normalized start declaration while its quest remains open** reuses that open quest. This is conservative retry deduplication, not fuzzy semantic matching.

After the previous quest has finished, the same start arguments may correctly create a new quest for a new work cycle.

---

## 4. Product principles

### Local-first

No account/cloud backend required for 0.1.

### Portable semantics

The same HP-MCP/2 semantics apply across compatible hosts.

### Status-first

Compact completion result is the primary 0.1 UI; dashboard follows in 0.2.

### Deterministic progression

The agent reports bounded metrics; local versioned rules calculate progression.

### Agent-context efficiency

Normal lifecycle is approximately:

```text
one start
one finish
```

List/card are recovery/inspection, not telemetry loops.

### Data minimization

No need to ingest source, diffs or raw logs to provide RPG progression.

### Explicit state handle

`questId` carries workflow state across calls; no hidden MCP session state.

### Multi-agent safe

Distinct active quests coexist up to a bounded local policy.

---

## 5. MCP surface

Exactly:

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

No MCP administration mirror.

Exact field/result semantics: `WIRE-CONTRACT.md`.

---

## 6. CLI surface

```text
hero-passport init
hero-passport mcp [--project-root <path>] [--hero <selector>]
hero-passport doctor
hero-passport card
hero-passport quest list --active
hero-passport export
hero-passport data path
hero-passport --version
```

CLI data-management commands may expand without expanding MCP.

---

## 7. Quest model

Quest type:

```text
planning
research
coding
review
debugging
documentation
maintenance
```

Result:

```text
success
partial
failed
blocked
abandoned
```

Each quest belongs to one resolved HeroId + ProjectId context.

Application cap:

```text
max 16 open quests per hero/project
```

Open retry dedup uses `QuestDedupKeyV1` from the normalized `questType + SafeTextV1(goal)` with case preserved.

This key does not claim semantic natural-language equivalence.

---

## 8. What the agent sends

Start:

```text
questType
goal: SafeTextV1, 1..500 Unicode scalar values
```

Finish:

```text
questId: canonical UUIDv7
result
summary: SafeTextV1, 1..2000 scalars
bounded metrics
1..3 canonical ordered skills
```

The model does not routinely send:

```text
heroId/projectId
workspace path
source/file content
diffs
raw logs
full prompt/chat
secrets/environment
arbitrary metadata bags
```

---

## 9. Result behavior

For MCP success:

```text
canonical structuredContent object
+ equivalent minified JSON TextContent for compatibility
+ displayText inside result object
```

For validation/business error:

```text
isError=true
safe TextContent
no structuredContent
```

Machine consumers use typed fields, not parse `displayText`.

---

## 10. Local context resolution

Hero Passport resolves locally:

```text
hero
project
locale
presentation
data paths
rule versions
```

Project launch starts from:

```text
--project-root if explicit
else host/process cwd
```

`project-identity/1` then performs Git-aware identity according to `PROJECT-IDENTITY.md`.

---

## 11. Hero

Fresh state creates default hero:

```text
Nova
Level 1
Total XP 0
Trust 50
Risk 20
```

Hero is global across projects; project stats are separate projections.

MCP does not choose a hero each call. Optional process startup binding can select one locally.

---

## 12. Project identity

Persist:

```text
ProjectId
DisplayName
WorkspaceFingerprint
IdentityVersion=project-identity/1
```

No full workspace path/remote URL.

Key behavior:

```text
linked Git worktrees -> same project
normal nested cwd -> whole repo
explicit monorepo --project-root scope -> separate scoped project
submodule/nested repo -> separate project
non-Git -> standalone local path identity
```

Repository move/fresh clone may produce a new v1 identity; this is documented rather than hidden behind unreliable remote heuristics.

---

## 13. RPG acceptance

Clean successful coding golden:

```text
60 base
+10 tests
+10 clean scope
+10 clear summary
+5 no corrections
=95 XP
```

Full rules: `ENGINE-SPEC.md`.

---

## 14. Retry/concurrency acceptance

### Start

While a matching normalized declaration is open:

```text
concurrent/repeated starts -> one questId
```

Case-different/code-sensitive declaration is distinct.

After that quest finishes, the same arguments may start a new quest.

### Active cap

```text
15 existing + two concurrent distinct starts -> final exactly 16; one HP133
```

### Finish

Repeated/concurrent finish for one quest:

```text
one quest report
one XP event
one aggregate mutation
same original persisted outcome on retries
```

### Context

Wrong locally bound hero/project for a valid questId -> HP134 without revealing the alternate owner.

---

## 15. Persistence reliability acceptance

```text
same-host local writable SQLite/WAL
non-deferred Serializable writer transaction before mutation invariant reads
WAL + synchronous=FULL + foreign_keys=ON
actual sqlite_version >=3.51.3 qualified
crash before commit -> no partial progression
crash after commit-before-response -> safe retry
no manual WAL/SHM deletion
live physical backup uses SQLite backup API, not File.Copy
```

---

## 16. Presentation

```text
RU + EN
compact default
normal optional
```

`displayText` stays bounded and does not echo arbitrary goal/summary by default.

Localized labels are not persisted domain keys.

---

## 17. Support claims

```text
Qualified
Documented/protocol-compatible
Unsupported/unknown
```

Codex is first release-blocking Qualified host. Other hosts require recorded release smoke evidence.

---

## 18. Deployment scope

0.1:

```text
local stdio
local same-host SQLite
single OS-user trust boundary
```

0.2:

```text
local Blazor dashboard over same Application/store
```

Future own Streamable HTTP is trigger-based. Public/multi-tenant HTTP requires separate authentication/authorization/storage design.

---

## 19. Explicit exclusions through 0.1

```text
achievements/items
runtime plugins
source/diff ingestion
continuous telemetry
LLM judge
cloud/team mode
own HTTP/OAuth
REST/GraphQL/gRPC public API
required MCP Resources/Prompts
MCP Apps/Tasks
ACP agent
legacy SSE
```

---

## 20. Release acceptance

0.1 requires:

1. deterministic fresh initialization;
2. protocol-pure stdio;
3. exact HP-MCP/2 generated contract snapshots;
4. 2026-07-28 + 2025-11-25 compatibility paths;
5. success structured/TextContent semantic equality;
6. explicit runtime input validation;
7. ProjectIdentity linked-worktree/monorepo/submodule/privacy vectors;
8. same-dedup start race convergence;
9. count-15 distinct race ends exactly 16;
10. finish race awards once;
11. child-process crash-before/after-commit evidence;
12. backup verification;
13. actual SQLite version/PRAGMA/migration evidence;
14. Codex E2E + host-neutral AgentEvals;
15. privacy/forbidden schema/log scans;
16. packaged artifact smoke on supported OS matrix.

---

## 21. Success definition

A developer can install one local command, bind it predictably to a project in a compatible MCP host, and receive persistent deterministic RPG progression across sessions/clients without exposing code or maintaining cloud infrastructure.
