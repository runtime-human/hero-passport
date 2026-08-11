# Hero Passport — Roadmap

**Current design:** v3.2.1  
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
explicit Start Hero ownership
crash-safe mutation request identities
project-wide get_context recovery
atomic/conflict-detecting Finish
XP / Skills / Hero+Skill levels
Rank
Trust / Strain
Success Streak
Traits / Titles
compact history/project stats
```

Reference qualified host: Codex.

Permanent logical Hero deletion is CLI-only. Model-facing removal uses archive/restore.

Release requires evidence in `TESTING-QUALITY.md`.

## 0.1 implementation checkpoint

Before full RPG expansion, prove a minimal real vertical slice:

```text
SQLite/migrations/per-connection policy
project-identity/1
bootstrap + typed settings + get_context
minimal Start with explicit heroId
minimal Finish with finishRequestId/base XP
actual MCP C# SDK adapter
minimal packaged Agent Skill
Codex E2E
restart/retry/concurrency/crash vectors
```

Only after this checkpoint implement complete reward/Skills/levels/Rank/Trust-Strain/Streak/Traits/Titles/localization/admin polish.

## 0.1.x — Qualification/distribution polish

After 0.1 semantics are stable:

```text
broader host qualification
installer/package ergonomics
Skill install improvements
error/presentation polish
performance/DB profiling if measured
additional curated flavor/localization quality
```

No rule-economy changes without a new rule version.

## 0.2.0 — Local Web UI

Local visual read/management surface over the same Application/store:

```text
Hero card
project/Quest history
Skill progression
Rank/Traits/Titles
settings/Hero management
```

Web never becomes a second game engine or direct DbContext UI.

## Future candidates — trigger-based only

### Optional sync

Trigger: real demand for one Hero across multiple devices.

Current architecture is **sync-conscious, not sync-ready**.

A future sync design must explicitly solve:

```text
stable cross-device Project identity
device/origin identity
Hero/account namespace
auth/privacy
open-Quest conflicts
delete tombstones
projection rebuild/merge
clock/causality semantics
offline convergence
backup/recovery
```

UUIDv7 and immutable completed outcomes are useful seams but do not solve these problems.

### Streamable HTTP / hosted MCP

Trigger: priority host cannot use local stdio or distribution needs a URL.

Requires separate auth/project/storage/threat-model design. Do not expose local SQLite on `0.0.0.0` and call it hosted mode.

### Model-controlled permanent delete

Trigger: proven user value beyond CLI administration.

Requires separately qualified human-confirmation semantics (for example current MCP MRTR capabilities), host support matrix and updated threat model/contract tests.

### More languages

Trigger: translation demand. Add resources without changing game keys/rules.

### Manual Title equipment

Trigger: customization demand. Add explicit preference state without mechanical bonuses.

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
CRDT/event-sourcing framework
```

## Architecture gate

Before promoting a future candidate:

1. state user problem;
2. compare strong prior art;
3. verify latest official stack docs;
4. write/update ADR/spec/threat model;
5. define executable acceptance evidence;
6. then add dependencies/code.
