# AGENTS.md — Hero Passport

## Purpose

Build Hero Passport as a small local-first RPG state layer for AI coding agents. Follow the canonical docs in `docs/`; do not invent alternative architecture in implementation PRs.

## Read before coding

For any task, read the smallest relevant set:

```text
docs/PRODUCT-SPEC.md
docs/ARCHITECTURE.md
+ the feature-specific spec
docs/DECISION-LOG.md relevant ADRs
```

MCP work: also `MCP-CONTRACT.md` + `integrations/CODEX.md`.
Storage work: also `DATA-MODEL.md` + `CONFIGURATION.md` + `SECURITY-PRIVACY.md`.
Rules: `ENGINE-SPEC.md`.
Dependencies: `DEPENDENCIES.md`.
Tests/release: `TESTING-QUALITY.md`.

## Hard architecture rules

```text
Domain -> no EF/MCP/CLI/ASP.NET/localization/filesystem
Application -> Domain only; typed use cases/ports; no MCP SDK or localized text
Infrastructure -> EF/SQLite/filesystem/config adapters
App -> composition + CLI + MCP + presentation
Web later -> Application/read models; no DbContext in Razor components
```

Do not add a generic repository, mediator/event bus, runtime plugin framework or HTTP MCP abstraction without a superseding ADR.

## MCP invariants

Exactly four MVP tools, explicit registration and stable order:

```text
hero.start_quest
hero.finish_quest
hero.current_quest
hero.get_card
```

Never use assembly-wide tool scanning in MVP.

Do not add MCP fields/tools for:

```text
source code
file contents
diffs
raw logs
prompts/chat history
secrets
environment
workspace paths
arbitrary metadata/context payloads
```

All tool input objects reject additional properties and stay bounded.

Stable local state (active hero/project/locale/presentation) is resolved locally; do not put it back into every MCP call.

MCP stdout is protocol only. Diagnostics go to stderr/local logging.

## Persistence invariants

```text
EF Core SQLite migrations from day one
IDbContextFactory, short-lived contexts
short synchronous SQLite operations
no Task.Run around DB I/O
WAL + synchronous=FULL + foreign_keys ON
no Cache=Shared with WAL
UNIQUE xp_events.quest_id
FinishQuest is one atomic transaction
finished retry returns original stored outcome
no custom migration mutex; EF owns migration lock
```

Persistence tests use real temporary file-backed SQLite.

## RPG invariants

Canonical clean coding golden:

```text
60 base
+10 tests
+10 clean scope
+10 clear summary
+5 no corrections
= 95 XP
```

Use deterministic integer rules and persist rule versions.

Skill XP distribution conserves total XP exactly.

Russian terminology:

```text
scope_control -> Контроль
clean scope bonus -> Бонус за контроль
scope violation -> Выход за задачу
```

Localized text is App presentation, not Domain state.

## Dependency policy

Use Central Package Management and pinned stable packages in `DEPENDENCIES.md`.

Do not introduce a package until its benefit beats the BCL/framework/direct code and the dependency checklist is answered.

No preview dependency without ADR.

## Testing workflow

For behavior changes:

```text
write failing focused test
run and confirm failure
implement minimum coherent change
run focused test
run impacted suite
run architecture/privacy tests when boundary changes
update docs/goldens in same PR
```

MCP name/description/schema/instructions changes also require the agent-eval scenarios relevant to tool selection.

Before claiming completion, run the exact verification commands defined by the implementation milestone and inspect output.

## Scope guard

0.1.0 excludes:

```text
dashboard
achievements/items
runtime plugins
HTTP/OAuth
MCP Apps/Tasks
cloud/team/auth
continuous telemetry
LLM judge
source/diff ingestion
```

0.2.0 introduces the local Blazor dashboard only after 0.1.0 gates pass.

If a task seems to require a deferred subsystem, stop architecture expansion and document the actual requirement/ADR rather than prebuilding a framework.
