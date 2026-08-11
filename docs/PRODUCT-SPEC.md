# Hero Passport — Product Specification

**Status:** Accepted v3 product contract  
**Snapshot:** 2026-08-11  
**Target:** 0.1.0 Portable Local MCP Core

## 1. Product definition

Hero Passport is a local-first RPG passport for AI agents. It turns meaningful agent work into persistent progression:

```text
start logical quest
-> agent works normally
-> finish explicit quest
-> deterministic XP/skills/traits/Trust/Risk
-> compact result
-> durable local history
```

The product is entertainment/companion-first, not employee monitoring, code surveillance or an LLM quality judge.

---

## 2. Positioning after architecture v3

Hero Passport is **not Codex-only**. Codex remains the first automated qualification host, but the product contract is portable MCP-first.

```text
reference qualification: Codex
portable integration standard: MCP
local transport for 0.1.0: stdio
```

A compatible host should not require Hero Passport business-code changes. Host differences belong to process/configuration adapters and documentation.

---

## 3. Primary user experience

A developer installs Hero Passport once and connects the local server to an MCP-capable coding host.

Example:

```text
User asks an agent to implement a feature.
Agent calls hero.start_quest.
Hero Passport returns questId.
Agent works normally.
Agent calls hero.finish_quest(questId,...).
Agent shows compact Hero Passport result.
```

If another agent works on a different task in the same repository, it may have a separate active quest. If an agent repeats the same logical task, Hero Passport returns the already-open quest rather than creating duplicate progression.

---

## 4. Product principles

### 4.1 Local-first

No account, cloud database or backend is required for 0.1.0.

### 4.2 Portable semantics

MCP contract is independent of Codex/VS Code/JetBrains/Zed/Cursor/Claude configuration syntax.

### 4.3 Status-first

The compact completion result is the primary UI for 0.1.0. Dashboard follows after the core loop.

### 4.4 Deterministic progression

The model reports bounded descriptive metrics; deterministic local rules calculate reward.

### 4.5 Agent-context efficiency

Normal work requires approximately:

```text
one start
one finish
```

List/card are recovery/inspection calls, not telemetry loops.

### 4.6 Privacy/data minimization

Code is unnecessary for gamification. Tool schemas do not provide a place for source/diff/raw logs.

### 4.7 Explicit state handles

Quest state crosses calls via `questId`, not hidden MCP sessions.

### 4.8 Multi-agent safe

Multiple distinct active quests are supported for one hero/project, bounded by product policy.

---

## 5. 0.1.0 MCP capability

Exactly:

```text
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

No additional administration MCP surface.

---

## 6. CLI capability

Minimum operator surface:

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

CLI administration may later include explicit hero/project reset/delete commands; such commands do not imply new MCP tools.

---

## 7. Quest model

Quest types:

```text
planning
research
coding
review
debugging
documentation
maintenance
```

Quest result:

```text
success
partial
failed
blocked
abandoned
```

A quest belongs to one `HeroId + ProjectId` context and carries one versioned logical key.

Application policy:

```text
max simultaneous open quests per hero/project = 16
```

Same logical task converges to the same open quest. Different logical tasks may coexist.

---

## 8. What an agent sends

At start:

```text
questType
goal <= 500 chars
```

At finish:

```text
questId
result
summary <= 2000 chars
bounded quality metrics
1..3 known/canonical skills
```

It does not send:

```text
heroId/projectId as routine model choices
workspace path
source code
file contents
diffs
raw logs
full chat/prompt
secrets/environment
arbitrary metadata
```

---

## 9. Local context resolution

Hero Passport resolves locally:

```text
hero binding
project binding
locale
presentation mode
data paths
rule versions
```

For stdio project binding:

```text
--project-root if provided
otherwise host cwd / Git-root resolution
```

The supported profile is project-bound launch. A single globally launched process with no reliable project binding is not promised to infer the caller workspace.

---

## 10. Hero

Fresh data initializes default hero:

```text
Nova
Level 1
Total XP 0
Trust 50
Risk 20
```

Hero is global across projects; project statistics are projections.

MCP does not make the model choose the hero every call. Optional host startup binding can select a hero locally.

---

## 11. Project identity

Project identity is local and privacy-preserving.

Persist:

```text
ProjectId
DisplayName
WorkspaceFingerprint
ProjectIdentityVersion
```

Do not persist full path by default.

The same physical project opened through different normalized paths should resolve consistently when the identity algorithm can detect a common Git root.

---

## 12. Core RPG acceptance

The clean successful coding golden remains:

```text
60 base
+10 tests
+10 clean scope
+10 clear summary
+5 no corrections
=95 XP
```

Full rule definitions live in `ENGINE-SPEC.md`.

---

## 13. Idempotency acceptance

### Start

Two concurrent/matching starts for the same hero/project/logical key result in one open quest ID.

### Finish

Any number of repeated/concurrent finish calls for one quest result in:

```text
one quest report
one XP ledger event
one set of aggregate mutations
same persisted original outcome returned on retries
```

### Context safety

A quest ID from another bound hero/project cannot be used to bypass local context; return `HP134`.

---

## 14. Presentation

0.1.0:

```text
RU + EN
compact default
normal optional local setting
```

Localized text is presentation, not persisted canonical rule state.

The list-active human text avoids echoing arbitrary goal text by default; structured output carries bounded stored goals for recovery.

---

## 15. Supported integration claim

Hero Passport separates:

```text
Qualified
Documented / protocol-compatible
Unsupported
```

Codex CLI is the first release-blocking Qualified host. Other hosts are not advertised as fully qualified until their smoke checklist is recorded for the release.

See `integrations/README.md`.

---

## 16. Deployment scope

0.1.0:

```text
local stdio process
local SQLite
single OS-user trust boundary
```

0.2.0:

```text
local Blazor dashboard over same application/storage core
```

Future Streamable HTTP is trigger-based. Public/multi-tenant hosting requires separate identity/authorization/storage architecture.

OpenAI Secure MCP Tunnel is an optional external deployment mechanism that can expose the private local stdio server to supported OpenAI surfaces without Hero Passport owning an HTTP listener.

---

## 17. Explicit exclusions for 0.1.0

```text
achievements module
items/artifacts
runtime plugins
source/diff ingestion
continuous telemetry
LLM judge
cloud sync
team/multi-tenant mode
our own HTTP/OAuth server
REST/GraphQL/gRPC public API
MCP Resources/Prompts as required behavior
MCP Apps
MCP Tasks
ACP agent implementation
legacy SSE server
```

---

## 18. 0.1.0 acceptance criteria

A release candidate is acceptable only when:

1. fresh local install initializes deterministically;
2. stdio MCP stdout is protocol-pure;
3. exact HP-MCP/2 manifest/schema snapshots match;
4. 2026 and 2025-11-25 compatibility paths pass;
5. Codex E2E passes start/list/finish/card;
6. same-task concurrent starts converge;
7. distinct parallel quests coexist;
8. finish race grants exactly one reward;
9. context mismatch is rejected;
10. SQLite migration/WAL/native version tests pass;
11. privacy/forbidden schema/log scans pass;
12. packaged dotnet tool runs on supported OS matrix;
13. AgentEvals do not regress the core lifecycle.

---

## 19. Success definition

Hero Passport 0.1.0 succeeds when a developer can install one local command, bind it to a project in a compatible MCP host, and reliably feel persistent RPG progression across agent sessions and even across different clients without exposing code or maintaining a cloud service.
