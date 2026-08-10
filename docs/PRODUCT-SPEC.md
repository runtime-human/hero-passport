# Hero Passport — Product Specification

**Status:** Accepted  
**Baseline:** 2026-08-10  
**Audience:** product, architecture, implementation, QA, AI coding agents

## 1. Product thesis

Hero Passport is a **local-first RPG passport for AI agents**. It adds a short game loop around meaningful agent work without turning the product into surveillance, agent orchestration or a second chat system.

The product promise is deliberately narrow:

> Start a quest, let the agent work normally, finish the quest, receive a compact and explainable RPG result, and keep the hero's progression locally.

Hero Passport should feel like an entertaining companion/passport first. The engineering architecture may support deeper agent evolution later, but the MVP must not sell complexity as value.

## 2. Primary user and environment

Primary MVP user:

- individual developer using Codex or another MCP-capable coding agent;
- local machine on Windows, Linux or macOS;
- wants persistent RPG progression with negligible interruption and token overhead;
- expects local data ownership and no source-code upload.

Primary client: Codex local clients through MCP stdio. Other MCP clients are compatibility targets after the Codex path works end-to-end.

## 3. Core user journey

```text
1. User installs/configures Hero Passport locally.
2. Codex launches `hero-passport mcp` as a stdio MCP server.
3. For a meaningful task, the agent calls `hero.start_quest` once.
4. Hero Passport resolves the hero/project and returns compact context.
5. The agent performs the task with no Hero Passport step logging.
6. The agent calls `hero.finish_quest` once with a concise self-report.
7. Hero Passport validates the report and deterministically calculates rewards.
8. One transaction stores the completed quest, report, reward ledger and projections.
9. The tool returns structured data plus a ready-to-display `displayText`.
10. The agent shows only the compact Hero Passport block to the user.
11. Later, a local dashboard reads the same persisted state through application queries.
```

## 4. Product principles

### 4.1 Local-first by default

The authoritative game state is local SQLite. No account, backend, cloud sync or telemetry service is required for the MVP.

### 4.2 Status-first, not dashboard-first

The end-of-session result is the first complete experience. Dashboard work begins only after MCP lifecycle, scoring, persistence and output formatting are stable.

### 4.3 Deterministic before intelligent

Reward computation must be replayable and explainable from stored inputs + `ruleVersion`. No LLM judge controls XP, trust, risk or traits in MVP.

### 4.4 Minimal agent interruption

The normal lifecycle is two write calls: start once, finish once. `current_quest` and `get_card` are recovery/query tools, not mandatory polling.

### 4.5 Data minimization

Hero Passport consumes semantic quest metadata, not work artifacts. Source code, diffs, logs, prompts and secrets are out of contract.

### 4.6 Canonical state, localized presentation

Persist stable keys/enums/numbers. Render Russian/English labels at the edge. Historical data must not depend on the language used when it was created.

## 5. MVP capabilities

### 5.1 Hero

MVP has a default hero (`Nova`) and supports the underlying concept of multiple heroes without making hero administration central to the first-use flow.

Hero state includes:

- ID and display name;
- total XP and level;
- trust and risk;
- skill progression;
- trait progression/unlocks;
- created/updated timestamps.

### 5.2 Project

A project represents a local workspace/repository identity. `projectId = auto` resolves locally from the active workspace. The persisted project record uses a display name plus a privacy-preserving workspace fingerprint; the full local path is not stored by default.

### 5.3 Quest

A quest is one meaningful unit of agent work. MVP quest types:

```text
planning
research
coding
review
debugging
documentation
maintenance
```

Lifecycle:

```text
open -> completed
open -> abandoned
```

`partial`, `failed`, and `blocked` are completion results, not additional lifecycle states.

### 5.4 Reward/result

On finish, the product returns:

- gained XP;
- total XP / level progress;
- trust/risk after the quest;
- up to three skill changes;
- trait changes when applicable;
- deterministic reward breakdown;
- one compact `displayText` ready for the agent to show.

### 5.5 History

Persistence retains enough structured state for:

- current card;
- recent quest history;
- reward audit/replay;
- project-level statistics;
- future dashboard projections.

It does not retain raw work artifacts.

## 6. MCP surface

MVP exposes exactly four tools:

```text
hero.start_quest       mutation, idempotent
hero.finish_quest      mutation, idempotent
hero.current_quest     read-only
hero.get_card          read-only
```

Detailed schemas and error semantics are owned by `MCP-CONTRACT.md`.

## 7. First-use experience

Target path after installation:

```text
hero-passport init
codex mcp add hero-passport -- hero-passport mcp
codex mcp list
```

The product should not write Codex configuration automatically in the MVP. Codex already provides a native MCP registration command; Hero Passport documentation should use it rather than maintaining a second config mutator.

`hero-passport init` is idempotent and:

- creates the application data directory;
- creates/migrates the database;
- seeds the default hero and canonical skills/traits;
- prints the resolved local data location to normal CLI output;
- never modifies another application's config.

## 8. CLI MVP

Required commands:

```text
hero-passport init
hero-passport mcp
hero-passport status
hero-passport doctor
hero-passport export --format json
hero-passport data path
```

Recommended shortly after the core loop:

```text
hero-passport hero list
hero-passport hero create <name>
hero-passport project list
hero-passport quest recent
```

`mcp` is a special protocol mode: stdout is reserved exclusively for MCP protocol messages.

## 9. Output UX

### 9.1 Output modes

```text
compact  default; target <= 900 visible characters
normal   target <= 1600 visible characters
verbose  explicit user request/debug UX only
```

These are product budgets, not wire-size guarantees. Structured fields should also remain bounded.

### 9.2 End-of-quest example

```text
## Hero Passport

✨ Квест завершён: +95 XP
Nova · ур.1 · XP 95/100
Доверие 51 · Риск 19
Навыки: Кодинг +47, Контроль +29, Тесты +19
Следующее: ур.2 через 5 XP
```

### 9.3 Russian terminology

Canonical key -> Russian presentation:

```text
scope_control        -> Контроль
clean_scope_bonus    -> Бонус за контроль
scope_violation      -> Выход за задачу
```

Trait names are separately localized. Internal keys are never translated.

## 10. MVP non-goals

Explicitly excluded before the minimal MVP:

- achievements module or GitHub Achievements analogy;
- artifacts/items/inventory;
- external/runtime plugins and DLL loading;
- HTTP/Streamable HTTP MCP;
- MCP Apps UI extension;
- MCP Tasks extension;
- cloud synchronization;
- multi-user/team mode;
- authentication/authorization server;
- continuous editor/activity telemetry;
- per-keystroke, per-line or per-diff XP;
- source/diff/log ingestion;
- LLM judge;
- self-evolution / automatic rule rewriting;
- permission gateway for other tools;
- full trace capture;
- public REST API;
- OpenAI Apps UI integration.

The architecture may leave low-cost seams for future work, but no post-MVP module is implemented speculatively.

## 11. Success criteria for minimal MVP

The MVP is complete only when all conditions are true:

1. Fresh install on Windows, Linux and macOS can initialize the local store.
2. Codex can launch Hero Passport through stdio using documented native MCP configuration.
3. The four MCP tools are discoverable in deterministic order.
4. A coding quest can start and finish end-to-end.
5. The standard successful coding fixture produces exactly `95 XP` under reward rule `1.0.0`.
6. Retrying `start_quest` does not create a duplicate active quest for the same idempotency identity.
7. Retrying `finish_quest` does not grant duplicate XP or mutate projections again.
8. The same stored report + rule version always yields the same reward breakdown.
9. MCP mode emits no non-protocol stdout bytes.
10. The database contains no code/diff/raw-log/full-prompt fields.
11. Fresh migrations and upgrade migration fixtures pass against real SQLite.
12. Package restore is reproducible from lock files.
13. Cross-platform CI build/test gates are green.
14. `displayText` is readable without exposing raw structured JSON.
15. A user can export their local Hero Passport state to JSON.

## 12. Post-MVP product sequence

After minimal MVP stabilization, preferred order is:

1. local Blazor dashboard using read models;
2. richer history/project statistics;
3. additional hero/card presentation and safe export/import;
4. carefully versioned new traits/rules;
5. optional MCP resources/prompts only when a concrete client UX benefits;
6. achievements/artifacts only as separately designed game modules;
7. remote/cloud/team features only with a new threat/auth architecture.

## 13. Product anti-metrics

Do not optimize for:

- number of MCP tools;
- number of persisted events;
- number of dashboard widgets;
- token usage caused by Hero Passport;
- lines/files changed by the coding agent;
- time spent typing.

Optimize for reliable completion of the two-call loop, understandable progression and low friction.
