# Hero Passport — Roadmap

**Current design:** v3.2  
**Snapshot:** 2026-08-11

Roadmap is scope guidance, not permission to pre-build future abstractions.

## 0.1.0 — Local Hero Passport MVP

Ship:

```text
C# 14 / .NET 10 LTS modular monolith
same-host SQLite
local stdio HP-MCP/2
portable Hero Passport Agent Skill
CLI onboarding/admin/doctor/export
RU + EN presentation
multiple local Heroes
one open Quest per Hero+Project
start request idempotency
atomic Finish progression
XP / Skills / Hero+Skill levels
Rank
Trust / Strain
Success Streak
Traits / Titles
compact history/project stats
```

Reference qualified host: Codex.

Release requires the test/eval/package evidence in `TESTING-QUALITY.md`.

## 0.1.x — Qualification and distribution polish

Only after 0.1 semantics are stable:

```text
broader host smoke/qualification
installer/package ergonomics
Skill installation improvements
error/presentation polish
performance/DB profiling if measured
additional curated flavor/localization quality
```

No rule-economy changes without a new rule version.

## 0.2.0 — Local Web UI

Add local visual read/management surface over the same Application/store:

```text
Hero card
project/Quest history
Skill progression
Rank/Traits/Titles
settings/Hero management
```

Web must not become a second game engine or access DbContext directly from Razor components.

## Future candidates — trigger-based only

### Optional sync

Trigger: real demand for one Hero across multiple devices.

Requires a dedicated design for:

```text
identity/account or user-controlled pairing
E2E/security expectations
conflicts
immutable event merge rules
delete/tombstone semantics
offline convergence
backup/recovery
```

UUIDv7/sync-ready local data is not itself a sync implementation.

### Streamable HTTP / hosted MCP

Trigger: priority host/deployment cannot use local stdio or public distribution demands a URL.

Requires separate auth/project/storage/threat-model design. Do not expose the local SQLite architecture on `0.0.0.0` and call it hosted mode.

### More languages

Trigger: translation demand. Add resource sets without changing domain keys/game rules.

### Manual Title equipment

Trigger: user demand for customization. Add explicit preference state without mechanical bonuses.

### Additional Traits/Titles/Ranks

Content expansion is allowed when unlock semantics are versioned/tested and old keys remain interpretable.

## Explicitly not planned through 0.2

```text
continuous editor telemetry
source/diff ingestion
raw log collection
LLM judge
employee/team monitoring
team/shared XP
random loot/items economy
HP/gold punishment loop
agent ownership/leases/heartbeats
MCP Tasks as Quest lifecycle
runtime plugin framework
REST/GraphQL/gRPC public API
```

## Architecture gate

Before promoting any future candidate into a release:

1. state the user problem;
2. compare prior art;
3. verify latest official stack docs;
4. write/update ADR/spec/threat model as applicable;
5. define executable acceptance evidence;
6. only then add dependencies/code.
