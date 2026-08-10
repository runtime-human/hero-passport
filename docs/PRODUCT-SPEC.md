# Hero Passport — product specification

**Status:** Accepted MVP product contract  
**Snapshot:** 2026-08-10  
**Target release:** 0.1.0

## 1. Product definition

Hero Passport is a local-first RPG passport for AI coding agents.

It turns meaningful agent work into a lightweight quest loop:

```text
start meaningful quest
-> agent performs normal work
-> finish explicit quest
-> deterministic RPG progression
-> compact result in the agent response
-> durable local history
```

The product is entertainment-first and companion-like. Its value is the feeling of persistent agent progression, not employee surveillance, code-quality scoring or enterprise productivity analytics.

---

## 2. Primary user story

A developer installs Hero Passport once, connects it to Codex as a local stdio MCP server, and then ordinary meaningful coding-agent sessions produce persistent progression without requiring a dashboard or cloud account.

Example:

```text
User asks Codex to implement a feature.
Codex calls hero.start_quest.
Codex works normally.
Codex calls hero.finish_quest once.
Codex includes:

Hero Passport: ✨ +95 XP · Nova ур.1 · XP 95/100 · Доверие 51 · Риск 19
```

The next Codex process/server instance sees the same local hero state.

---

## 3. Product principles

### 3.1 Status-first

The most important UI in 0.1.0 is the compact end-of-session status.

Dashboard is not required to make the product useful.

### 3.2 Local-first

No account, cloud database, analytics backend or remote API is required.

### 3.3 Deterministic game rules

An LLM may report compact metrics, but XP/skills/traits/Trust/Risk are calculated locally by deterministic versioned rules.

### 3.4 Agent-context efficiency

Hero Passport should not consume context continuously. Normal meaningful workflow is approximately two tool calls:

```text
start once
finish once
```

### 3.5 Data minimization

Hero Passport does not need the code to gamify the agent.

### 3.6 Narrow interfaces

MCP exposes only the operations useful inside agent reasoning. Administration/diagnostics belong to CLI; visual exploration belongs to future dashboard.

---

## 4. Target users

Primary:

- developers using Codex or compatible coding agents;
- local-first/privacy-conscious users;
- users who enjoy RPG progression around AI-agent work;
- hackathon/demo users who need immediate visible payoff.

Secondary after MVP:

- users wanting a visual hero/dashboard;
- users operating more than one AI agent/client locally;
- users comparing project-specific progression.

Not a target in MVP:

- enterprise employee monitoring;
- multi-tenant teams;
- hosted SaaS analytics;
- code-compliance enforcement;
- security scanning.

---

## 5. MVP capability set

### MCP

Exactly:

```text
hero.start_quest
hero.finish_quest
hero.current_quest
hero.get_card
```

### CLI

Minimum useful operator surface:

```text
hero-passport init
hero-passport mcp
hero-passport doctor
hero-passport card
hero-passport quest current
hero-passport export
hero-passport data path
```

Additional explicit hero/project data-management commands may enter 0.1.0 only if needed for a usable fresh-install/reset workflow and must not expand MCP.

### Local state

```text
heroes
projects
hero/project stats
quest sessions/reports
XP ledger
skills
traits
Trust/Risk
app state/settings
```

### Presentation

```text
Russian + English compact text
compact default
normal optional local presentation mode
```

---

## 6. Meaningful quest types

```text
planning
research
coding
review
debugging
documentation
maintenance
```

These are game categories, not a claim that Hero Passport independently verified the quality of the work.

The agent chooses a suitable type from the closed enum.

---

## 7. What the model sends

At start:

```text
questType
goal (short bounded text)
```

At finish:

```text
questId
result
short summary
small quality metrics
up to 3 canonical/recognized skills
```

The model does **not** send:

```text
source code
diff
changed-file list
raw build/test output
prompt/chat transcript
workspace path
secrets
environment
arbitrary metadata
```

---

## 8. Product state resolution

The model should not repeatedly select stable local state.

Hero Passport resolves locally:

```text
active/default hero
current project identity
locale
presentation mode
data path
rule versions
```

This shrinks schemas and prevents model mistakes such as choosing a different hero ID on each call.

---

## 9. Hero

Initial experience creates one default hero when needed:

```text
Nova
```

Initial stats:

```text
Level 1
Total XP 0
Trust 50
Risk 20
```

Hero is global across projects; project-specific stats are separate projections.

Multi-hero management can exist in CLI/product state, but the MCP core does not make the model choose `heroId` on every call.

---

## 10. Project identity

Project is resolved locally from Git root/current working directory.

Stored identity:

```text
display name
opaque project ID
versioned workspace fingerprint
```

Absolute workspace path is not stored by default.

This supports project stats without turning Hero Passport into repository telemetry.

---

## 11. Quest lifecycle

Canonical state machine:

```text
             start
   none  ------------> open
                         |
                         | finish
                         v
                      finished
```

No reopen in rule/contract v1.

### Start idempotency

If the same normalized quest type + normalized goal is already open for the same hero/project, return it.

If a conflicting open quest exists, return a clear conflict rather than silently creating multiple active quests.

### Finish idempotency

A finished quest always returns its persisted original result on retry.

No second reward.

---

## 12. Result values

```text
success
partial
failed
blocked
abandoned
```

These feed deterministic game rules documented in `ENGINE-SPEC.md`.

---

## 13. RPG progression

MVP includes:

```text
XP
levels
skill XP
3 behavioral traits
Trust
Risk
project stats
quest history
```

MVP excludes:

```text
achievements
items/artifacts
random loot
season pass
currency/shop
streak mechanics without full semantics
LLM judge
self-evolution
```

---

## 14. Compact status UX

Start target:

```text
🧭 Квест начат · Nova ур.1 · XP 0/100
```

Finish target:

```text
✨ +95 XP · Nova ур.1 · XP 95/100 · Доверие 51 · Риск 19
```

Card target:

```text
Nova · ур.1 · XP 95/100 · Доверие 51 · Риск 19
```

The exact punctuation can evolve as presentation without changing reward rules. Contract-level size budgets remain enforced.

Russian terminology:

```text
scope_control -> Контроль
clean scope bonus -> Бонус за контроль
scope violation -> Выход за задачу
```

---

## 15. Privacy promise

Plain-language promise:

> Hero Passport stores compact quest/game state locally. It does not need or intentionally collect source code, diffs, raw terminal logs, full prompts, secrets, environment variables or full workspace paths.

The promise is enforced structurally through schemas/storage, not merely marketing text.

---

## 16. First-run experience

Desired flow:

```text
install hero-passport
hero-passport init
codex mcp add hero-passport -- hero-passport mcp
codex mcp list
start meaningful Codex task
see quest/progression status
```

`init` should be idempotent. If normal MCP startup can safely bootstrap an empty DB, first-run may become even simpler, but explicit `init` remains useful for diagnosis and predictable setup.

No browser/dashboard required for first success.

---

## 17. Diagnostic UX

`hero-passport doctor` is the canonical support command.

It checks:

```text
version/runtime/platform
app-data/config availability
config validity
database/migrations
native SQLite version
WAL/durability/FK state
seed/default hero state
MCP manifest
```

It does not dump secrets/environment/request contents.

The user should receive actionable remediation rather than raw exception text.

---

## 18. Error UX

Stable codes enable troubleshooting without exposing internals.

Examples:

```text
HP132 quest_conflict
HP202 database_busy
HP301 invalid_config
HP900 internal_error
```

Normal user message explains what happened and what action to take.

Raw stack/SQL/local path is not an MCP response.

---

## 19. Dashboard 0.2.0

Dashboard is a local Blazor read-focused experience over existing Application/read models.

First dashboard:

```text
hero card
level/XP progress
Trust/Risk
skills
traits
last reward
recent quests
project stats
```

It must not become a reason to move business rules into Razor/JavaScript or introduce a second backend.

---

## 20. Explicit non-goals through 0.1.0

```text
remote HTTP MCP
OAuth/auth
cloud sync
team/multi-user
OpenAI Apps SDK/MCP Apps
MCP Tasks
runtime plugins
achievement system
artifact inventory
continuous activity monitoring
per-keystroke/per-line XP
WakaTime compatibility
source/diff ingestion
LLM judge
agent self-modification
full REST API
remote telemetry
```

---

## 21. Success criteria for 0.1.0

A release is product-successful when all are true:

1. User can install/initialize locally on claimed platforms.
2. Codex sees exactly four tools.
3. A meaningful task completes start -> work -> finish.
4. Clean coding golden produces 95 XP.
5. Restart preserves state.
6. Retry cannot duplicate XP.
7. Final agent answer shows compact status without raw JSON.
8. No source/diff/raw-log path exists in normal contract/storage.
9. CLI `doctor` can diagnose common setup/database problems.
10. Real Codex agent eval shows lifecycle is useful and not called on trivial interactions excessively.
11. No dashboard is required to achieve the above.

---

## 22. Product quality guardrail

A proposed feature is not automatically appropriate for MCP.

Use this decision test:

```text
Does the model need this capability during normal reasoning/workflow?
Does a typed MCP call reduce ambiguity compared with shell/CLI?
Does advertising it on every session justify its context/schema cost?
Does it preserve the privacy contract?
```

If not, put it in CLI/dashboard or defer it.

---

## 23. Future product direction

Post-MVP may explore:

```text
richer hero visualization
dashboard widgets
additional well-specified traits
history filters/comparisons
portable export/import
agent identity/profile UX
selective MCP resources only if a client use case emerges
self-evolution/advanced mechanics only behind a separate design
```

These are not architecture commitments until designed and accepted.
