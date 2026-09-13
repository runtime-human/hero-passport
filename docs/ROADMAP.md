# Hero Passport — Roadmap

**Current design:** v3.2.1 core + 0.2-A/B/C local Web foundation/security/Start mutation  
**Snapshot:** 2026-09-13

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

Local visual read/management surface over the same Application/store.

0.2-A foundation provides:

```text
HeroPassport.Web as a sibling outer adapter
ASP.NET Core / Blazor static SSR
code-defined loopback-only dynamic listener
explicit --project-root with tested cwd fallback
fresh setup-required dashboard
configured Hero card/project/open-Quest/top-Skills dashboard
read-only Application-backed composition
privacy/no-write regression coverage
```

0.2-B adds the browser security boundary that later Web mutations reuse:

```text
canonical browser origin http://127.0.0.1:<dynamic-port>
code-owned Host Filtering for 127.0.0.1 only
one-time process-local 256-bit bootstrap capability
capability transport through URL fragment, not request target
antiforgery/origin-protected internal bootstrap claim
process-local 256-bit bearer session cookie
HttpOnly + SameSite=Strict + non-persistent cookie policy
fail-closed 401 before product-route/dashboard reads
restart invalidates prior browser sessions
Testing-only deterministic secrets / --no-open-browser seam
production browser launch is fail-closed
```

0.2-A/B remain read-only.

0.2-C implements the first bounded Web mutation pattern on top of that boundary:

```text
authenticated static-SSR Start Quest form
questType/title/goal dedicated form DTO only
Application-backed validation/normalization before mutation
bounded process-local confirmation state (8 entries / 10-minute TTL)
opaque >=128-bit confirmation handle
explicit human confirm POST before mutation
same prepared HeroId + StartRequestId retained through commit/retry
existing HeroPassportApplication.StartQuestAsync remains mutation authority
8 KiB urlencoded-only mutation request boundary before antiforgery/form parsing
real-process security/idempotency/privacy qualification
```

0.2-C adds Start Quest only. It does not add Finish Quest, result/summary/skills editing, Quest history, Hero/settings management, Identity/OAuth/accounts, public/LAN/local-HTTPS/reverse-proxy hosting, WebAssembly/Interactive Server, Streamable HTTP MCP or a general REST product surface.

Remaining 0.2 slices add, behind their own focused gates:

```text
Finish Quest Web flow with bounded result/attestation input
bounded project/Quest history
Skill progression
Rank/Traits/Titles detail
settings/Hero management
RU + EN Web presentation/accessibility polish
launch/package integration beyond the minimal browser launcher
published browser/concurrency/release qualification
```

Web never becomes a second game engine or direct DbContext UI. Future Web mutation slices must depend on the 0.2-B local browser authorization/CSRF boundary and the proven 0.2-C confirmation pattern instead of reimplementing or weakening either.

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
