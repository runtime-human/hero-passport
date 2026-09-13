# Hero Passport — Architecture

**Status:** Accepted v3.2.1 core + implemented 0.2-A/B Web adapter/security boundary  
**Snapshot:** 2026-09-13  
**Target:** 0.1 local stdio MCP + Agent Skill + CLI, plus secured local read-only 0.2 Web

Normative core design: `superpowers/specs/2026-08-11-hero-passport-v3.2.1-design.md`.
0.2-B security design: `superpowers/specs/2026-09-13-hero-passport-0.2-b-web-security-design.md`.

## 1. Runtime

```text
AI coding agent
  |
  | Hero Passport Agent Skill
  |  - lifecycle heuristics
  |  - get_context hydration/recovery
  |  - explicit Hero/Quest handles
  |  - bounded attestations
  v
HeroPassport.App
  - stdio MCP
  - CLI
  - localization/presentation
  |
  v
HeroPassport.Application
  |
  +--> HeroPassport.Domain
  |      deterministic rules only
  |
  v
HeroPassport.Infrastructure
  - EF Core / SQLite
  - Git project identity
  - filesystem/config/doctor
  |
  v
same-host SQLite
```

0.2-A/B adds a sibling secured read-only presentation adapter over the same Core/store:

```text
Browser
  -> http://127.0.0.1:<dynamic-port>
  -> code-owned Host filter
  -> one-time fragment bootstrap + antiforgery/origin validation
  -> process-local bearer session cookie
  -> HeroPassport.Web
     - ASP.NET Core / Blazor static SSR
     - code-defined IPv4 loopback listener
     - read-only dashboard composition
  -> HeroPassport.Application read use cases
  -> HeroPassport.Infrastructure
  -> same-host SQLite
```

`HeroPassport.Web` does not own game rules, mutate Quest/Hero state in 0.2-A/B, or query persistence directly from Razor/components. Browser authorization state is process memory owned by the Web adapter and is never persisted to SQLite. Later Web management slices must reuse both Application authority and the 0.2-B browser security boundary rather than creating a second game engine or parallel auth path.

## 2. Dependency direction

```text
Domain <- Application <- Infrastructure <- App
                                   ^
                                   |
                                  Web
```

Equivalently, both `HeroPassport.App` and `HeroPassport.Web` are outer adapters/composition roots. Domain has no EF/MCP/CLI/localization/Git/filesystem/network. Application has no MCP SDK/presentation. Infrastructure implements persistence/platform ports. App owns MCP/CLI/presentation composition; Web owns browser/static-SSR presentation and local browser security composition only.

No separate Contracts assembly in 0.1/0.2-A/B.

## 3. Domain authority

Domain owns versioned pure game semantics: reward, Skill allocation/progression, Hero progression, Rank, Trust/Strain, Streak, Trait/Title unlock semantics and semantic milestone events.

Flavor prose is not Domain truth.

## 4. Application use cases

```text
BootstrapApplication
ConfigureApplication
GetRuntimeContext
CreateHero
ListHeroes
ActivateHero
ArchiveHero
RestoreHero
StartQuest
FinishQuest
GetHeroCard
```

CLI-only administration includes permanent logical Hero deletion, diagnostics, export/backup and migration-lock recovery.

0.2-A/B Web consumes only existing read semantics (`GetRuntimeContext` / `GetHeroCard`) through a bounded Web presentation model. The 0.2-B bootstrap/session/Host/CSRF machinery authorizes browser access but adds no Application or game mutation use case.

## 5. Skill/Core boundary

Skill decides when work probably starts/ends and which open Quest appears to match current work. Core validates all durable invariants.

“one meaningful goal = one Quest” is Skill policy, not a server-verifiable fact.

Skill never sends XP/levels/rank/Trust/Strain/unlock decisions.

## 6. Explicit state handles

MCP application correctness is sessionless.

Explicit handles/identities include:

```text
heroId
questId
bootstrapRequestId
createRequestId
startRequestId
finishRequestId
```

MCP request ID is transport-only.

The Web bootstrap capability and browser session token are transport-security bearers, not Domain/Application identities and not persisted state handles.

## 7. Runtime context and multi-Hero recovery

`hero.get_context` hydrates separately installed/restarted Skills.

It returns settings/version state, default active Hero and current-Project open Quests across all Heroes.

The global active Hero pointer is preference/default only.

New Start requests include explicit `heroId`. Existing Quest ownership is immutable.

If recovery discovers multiple plausible open Quests, Skill must not guess. If exactly one clearly matches, it may resume that `questId` even when global active preference changed elsewhere.

## 8. One-open scope

DB invariant:

```text
one open Quest per HeroId + ProjectId
```

Linked worktrees intentionally share ProjectId. Therefore 0.1 does not support parallel independent same-Hero Quests across linked worktrees of one repository.

This is a deliberate MVP limitation rather than introducing WorkContext identity or a scheduler.

## 9. Start mutation

Project resolution may use Git/filesystem before SQLite.

After ProjectId is known:

```text
validate request
BEGIN non-deferred Serializable writer
receipt replay/mismatch using ProjectId + explicit HeroId + args
validate setup/Hero
snapshot locale
check one-open invariant
insert Quest + receipt + projections
COMMIT
```

No read of `activeHeroId` decides Start ownership.

## 10. Finish mutation

```text
validate/fingerprint finish payload
BEGIN writer
receipt replay/mismatch
load Quest by questId + verify Project context
if already finalized:
  equivalent -> original result
  different -> HP136
else:
  calculate current versioned rules once
  persist report/components/XP event/receipt
  update projections/unlocks
  mark finished
COMMIT
```

First committed finalization wins; later semantic disagreement is detected, never overwritten.

No agent leases/heartbeats/owners.

## 11. Current HP-MCP/2 adapter

Official C# SDK baseline:

```text
ModelContextProtocol 2.2.0
preferred MCP 2026-07-28
qualification path 2025-11-25
```

Current tool order:

```text
hero.bootstrap
hero.configure
hero.get_context
hero.create
hero.list
hero.activate
hero.archive
hero.restore
hero.start_quest
hero.finish_quest
hero.get_card
```

Explicit registration; no assembly-wide scanning/aliases.

Successful structured results use canonical `structuredContent` and one deterministic serialized JSON TextContent compatibility block with semantic equality. JSON whitespace/minification is implementation formatting, not business semantics.

## 12. Setup architecture

Migration seeds typed singleton `app_settings` with setup incomplete.

Before setup:

```text
hero.get_context -> allowed
hero.bootstrap   -> allowed
all other HP-MCP tools -> HP001 setup_required
```

After setup, bootstrap with a fresh request returns HP002; configure becomes preference-only.

stdio stdout remains MCP protocol only.

## 13. Persistence

Use EF Core / Microsoft.Data.Sqlite with `IDbContextFactory` and short-lived contexts.

Supported profile:

```text
same-host local filesystem
SQLite >=3.53.4
WAL
synchronous=FULL
foreign_keys=ON
trusted_schema=OFF
Cache=Default
Pooling=True
Default Timeout=5
```

WAL/runtime version are database initialization/qualification concerns. `synchronous=FULL` and `trusted_schema=OFF` are enforced for every opened product connection; foreign keys are enabled through connection policy and verified.

All invariant read-modify-write operations acquire writer intent before invariant reads.

Web browser-session state is not part of persistence. Restart creates new random bootstrap/session secrets and invalidates any prior browser cookie independently of SQLite lifecycle.

## 14. Data authority

Canonical history survives ordinary upgrades: Quest/final report, XP event, reward/Trust-Strain/Skill deltas, unlock rows, semantic milestones and rule versions.

Mutable totals/stats/Skill totals/streak are rebuildable projections. A rebuild test must reproduce public read models.

This is not event sourcing.

## 15. Schema invariants

Migration 0001 contains physical CHECK/FK/index protections for closed enums, numeric ranges, Quest status/time consistency, singleton settings and one-open Quest.

Mutation receipts persist `args_encoding_version` and bound Project/Hero context and can survive target deletion as minimal `target_deleted` tombstones without private history.

## 16. Permanent delete

MCP does not expose permanent Hero delete in 0.1.

CLI delete is explicit and irreversible at the logical active-database level. It rejects active/open-Quest Heroes.

No forensic secure-erasure claim is made for deleted SQLite pages, backups, snapshots or exports.

## 17. Migration recovery

Doctor detects suspicious abandoned EF `__EFMigrationsLock`; normal startup never silently clears it.

Explicit repair requires stopped competing Hero Passport processes and a fresh safety check.

## 18. Privacy and local Web security

No routine source/diff/raw-log/prompt/secret/environment/full-path/Git-remote ingestion.

Build/test fields are bounded attestations. `observed` is an agent assertion of direct observation, not independent verification.

Quest title/goal/summary remain potentially sensitive local metadata.

The Web presentation renders bounded fields only. Full local paths, workspace fingerprints, mutation receipt internals and raw evidence remain outside the browser surface.

0.2-B additionally treats the browser request boundary as untrusted even on loopback:

```text
Kestrel bind = IPv4 loopback only
canonical Host = 127.0.0.1 only
bootstrap capability = independent random 32 bytes, one-time
session bearer = independent random 32 bytes, process-lifetime
bootstrap transport = URL fragment -> same-origin antiforgery POST
product route without session = 401 before dashboard/Application read
unsupported Host = 400
restart = prior cookie invalid
```

The bootstrap capability/session/antiforgery material is deny-listed from normal logs, product HTML, error bodies, redirects and SQLite. The internal bootstrap POST is the only HTTP security endpoint added by 0.2-B and is not a product REST façade.

This local process capability does not claim protection against a malicious same-user process that can inspect process memory/browser storage, nor does it constitute public authentication. Public/LAN/reverse-proxy/HTTPS deployments require a different security design.

## 19. Level and presentation semantics

At Hero Level 50 / Skill Level 10 XP continues accumulating.

Wire progress uses `isLevelCapped` and omits `nextLevelXpRequired` when capped.

Game engine emits semantic milestone events. Curated flavor is presentation and may evolve without changing historical game facts.

## 20. Local-first, sync-conscious

No sync implementation exists. Current identity/history choices are sync-conscious only; cross-device identity/deletion/conflict/causality need future ADRs.

No CRDT/event sourcing is introduced preemptively.

## 21. Implementation risk order

Before full RPG expansion, prove:

```text
restore/build baseline
SQLite/migrations/connections
project identity
bootstrap/get_context
minimal Start
minimal Finish/base XP
real MCP adapter
minimal Agent Skill
Codex E2E + restart/retry/race/crash
```

Only after that checkpoint implement complete reward/Skills/levels/Trust-Strain/Traits/Titles/localization/admin/release matrix.
