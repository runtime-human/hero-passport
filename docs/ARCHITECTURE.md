# Hero Passport — Architecture

**Status:** Accepted architecture v3.2  
**Snapshot:** 2026-08-11  
**Target:** 0.1.0 local stdio MCP + Agent Skill + CLI  
**Style:** modular monolith with explicit domain/application/adapter/persistence boundaries

Normative sources, in order for their own topics:

1. `superpowers/specs/2026-08-11-hero-passport-v3.2-design.md` — consolidated product semantics;
2. `WIRE-CONTRACT.md` — exact MCP wire contract;
3. `PERSISTENCE-RELIABILITY.md` — SQLite/write/crash/backup;
4. `PROJECT-IDENTITY.md` — project binding;
5. `ENGINE-SPEC.md` — deterministic game rules;
6. `AGENT-SKILL.md` — agent orchestration behavior.

## 1. Executive architecture

Hero Passport is a **local deterministic RPG application with an MCP adapter and a portable orchestration Skill**.

```text
AI coding agent
   |
   | loads Hero Passport Agent Skill
   |  - recognizes meaningful goal boundaries
   |  - calls explicit MCP tools
   |  - carries questId
   |  - renders canonical results
   v
HeroPassport.App (stdio MCP + CLI + presentation)
   |
   v
HeroPassport.Application (semantic use cases)
   |
   +--> HeroPassport.Domain (pure deterministic rules)
   |
   v
HeroPassport.Infrastructure (EF/SQLite/Git/FS/config)
   |
   v
same-host SQLite
```

Web is a 0.2 read/presentation adapter over the same Application/store.

## 2. Architectural priorities

1. deterministic game semantics;
2. local privacy/data ownership;
3. atomic persistence and crash safety;
4. protocol correctness and safe retries;
5. ambient low-friction agent UX;
6. cross-agent/host interoperability;
7. explicit project identity;
8. testability and release evidence;
9. migration/upgrade safety;
10. optional future sync compatibility;
11. performance;
12. extensibility only after a demonstrated requirement.

## 3. Project structure

0.1:

```text
src/
  HeroPassport.Domain/
  HeroPassport.Application/
  HeroPassport.Infrastructure/
  HeroPassport.App/

tests/
  HeroPassport.Domain.Tests/
  HeroPassport.Application.Tests/
  HeroPassport.Infrastructure.Tests/
  HeroPassport.App.Tests/
  HeroPassport.Architecture.Tests/
  HeroPassport.Contract.Tests/
  HeroPassport.AgentEvals/

skills/
  hero-passport/
    SKILL.md
    references/
```

0.2:

```text
src/HeroPassport.Web/
```

No separate Contracts assembly until there is a real independently versioned .NET consumer.

## 4. Dependency direction

```text
Domain
  ^
Application
  ^
Infrastructure
  ^
App

Web -> Application
Web composition -> Infrastructure
```

Domain has no EF/MCP/CLI/localization/filesystem/Git/config/network concerns. Application has no MCP SDK or localized strings. Infrastructure implements ports. App composes adapters and presentation.

## 5. Domain boundary

Domain owns:

```text
typed IDs/enums
reward rules
Hero/Skill progression thresholds
rank milestones
Trust/Strain rules
streak
Traits/Titles unlock policy
pure invariants
rule versions
```

Domain does not read time directly. Application supplies timestamps from injected `TimeProvider`.

## 6. Application use cases

Game/runtime operations:

```text
ConfigureApplication
CreateHero
ListHeroes
ActivateHero
ArchiveHero
RestoreHero
DeleteHero
StartQuest
FinishQuest
ListActiveQuests
GetHeroCard
```

CLI/admin:

```text
InitializeApplication
GetDiagnostics
ExportData
GetDataPath
```

The Application layer returns typed semantic results/errors. It never returns MCP SDK types or presentation strings as authority.

## 7. Operation contexts

Project-bound game operations resolve local ProjectId through `project-identity/1`.

New Quest creation resolves the globally active Hero. Once created, a Quest permanently carries its HeroId and ProjectId.

`InvocationOrigin` may record safe diagnostics such as surface/client name/version in memory, but host identity never affects ownership, reward, authorization or game rules.

## 8. Agent Skill boundary

The Skill is orchestration policy, not game logic.

It recognizes:

```text
idle/discussion
meaningful work begins
active Quest continues
current goal is complete
goal switched explicitly
recovery after restart
manual override
```

It calls MCP but never calculates XP or game state. Core correctness cannot depend on the Skill being perfect: all invariants and validation live server-side.

The Skill follows the open Agent Skills format and uses progressive disclosure: concise `SKILL.md`, focused reference files, and host-specific installation notes outside core workflow text.

## 9. Quest identity architecture

There are three distinct notions:

```text
startRequestId  caller-generated identity of one start intent/retry sequence
questId         server-generated durable work/game identity
MCP request id  transport-level JSON-RPC identity only
```

Never derive idempotency from goal text.

Start request records are durable enough for late retries. Same request identity with changed canonical arguments produces `HP135 idempotency_conflict`.

Exactly one open Quest is allowed per `(HeroId, ProjectId)`.

This is enforced by both transaction logic and a partial unique database backstop on open Quest ownership.

## 10. Multi-agent architecture

A Quest has no `agentOwnerId`.

```text
Agent A -> starts quest Q
Agent B -> discovers/resumes Q
Agent B -> finishes Q
```

No leases, heartbeats, leader election or agent locks. If two callers finish Q concurrently, one transaction commits the immutable result; the other returns the persisted result.

Correct claim: **at-most-once committed progression per Quest**.

## 11. HP-MCP/2 v3.2 adapter

Official C# SDK baseline:

```text
ModelContextProtocol 2.1.0
preferred MCP semantics 2026-07-28
legacy qualification path 2025-11-25
```

Tool order is static and explicit:

```text
hero.configure
hero.create
hero.list
hero.activate
hero.archive
hero.restore
hero.delete
hero.start_quest
hero.finish_quest
hero.list_active_quests
hero.get_card
```

No assembly-wide scanning or host aliases.

The `2026-07-28` protocol is stateless. Hero Passport therefore uses explicit ordinary IDs/handles and never relies on connection/session lifetime for application state.

Success returns canonical `structuredContent` plus exactly one JSON TextContent semantically equal to it for compatibility. Human fallback text is a field in the typed result.

## 12. MCP vs Skill

Tool descriptions and server instructions should make each operation understandable, but the complete lifecycle policy belongs in the Agent Skill. This keeps MCP contracts stable and transport-neutral while allowing host UX/orchestration to evolve.

A host may still prompt users before MCP mutations; Hero Passport itself does not add redundant confirmation for normal safe gameplay tools. Permanent delete has its own explicit target confirmation field because it is destructive regardless of host UX.

## 13. Onboarding architecture

First-run state is persisted in local configuration.

CLI can interactively run `init`.

For MCP stdio:

```text
stdout = protocol only
stderr = safe diagnostics only
```

No interactive prompts may be written to stdout. Until configured, gameplay mutations return `HP001 setup_required`; the Skill can gather answers conversationally and call `hero.configure`.

## 14. Localization architecture

Domain/Application expose canonical semantic keys and typed values. App owns resource-based localization and renderers. Skill may render from canonical result fields but never reinterpret game numbers.

0.1 locales:

```text
ru-RU
en-US
```

A Quest snapshots its effective locale at start for stable presentation. Global locale changes affect new Quests and general UI, not historical semantics.

## 15. Persistence architecture

SQLite + EF Core + `IDbContextFactory`.

Operational profile:

```text
same-host local filesystem
WAL
synchronous=FULL
foreign_keys=ON
Cache=Default
Pooling=True
Default Timeout=5
```

Every read-modify-write use case starts a short non-deferred Serializable transaction before invariant reads. The selected Microsoft.Data.Sqlite path is release-tested for immediate writer intent.

No custom process-wide writer mutex and no separate Polly retry stack.

## 16. Start write transaction

```text
canonicalize/validate request outside transaction
BEGIN writer
lookup (HeroId, ProjectId, startRequestId)
  found + same args -> persisted start result
  found + different args -> HP135
query open Quest for HeroId+ProjectId
  exists -> HP133
insert start request + Quest + project projection changes
COMMIT
```

A unique index on request identity and a partial unique index on open Hero+Project are final DB backstops.

## 17. Finish write transaction

```text
BEGIN writer
load Quest
validate Hero/Project context
if finished -> persisted immutable result
calculate deterministic versioned rules
insert Quest report + report skills + UNIQUE XP event
update Hero, Skills, Trust/Strain, Streak, Traits/Titles, project stats
mark Quest finished
COMMIT
```

No transaction spans actual agent work.

## 18. Hero lifecycle persistence

The installation has one globally active Hero pointer.

Hero archive is reversible state. Permanent delete requires explicit intent, rejects open-Quest Heroes, and removes the Hero’s game/history rows through an explicit Application workflow. Any future sync/tombstone requirement is designed separately; local MVP does not retain private deleted history merely to simulate cloud semantics.

## 19. Read architecture

`hero.list`, `hero.list_active_quests` and `hero.get_card` use bounded reads and no writer transaction.

Card includes:

```text
Hero name
Level/Rank/active Title
XP progress
Trust/Strain
Success Streak
top Skills
active Quest if present
bound project compact stats
```

Detailed history is CLI/Web scope.

## 20. Project identity

`PROJECT-IDENTITY.md` remains normative.

Key invariant:

```text
explicit --project-root else cwd
Git repository -> canonical git-common-dir anchor
linked worktrees -> same project
explicit monorepo scope -> deliberate scoped identity
submodule/nested repo -> separate
non-Git -> standalone local directory identity
```

Persist no full workspace path or Git remote URL.

## 21. Privacy/security architecture

Do not add routine fields/storage/logging for:

```text
source/file content
diffs/patches
raw build/test/terminal logs
full prompts/chat
secrets/tokens/environment dumps
full workspace paths
Git remotes
arbitrary metadata bags
```

Build/test provenance is bounded semantic evidence, not a raw evidence store.

## 22. Local-first, sync-ready boundary

0.1 is fully local and requires no account. IDs, immutable reports/events, timestamps, versions and explicit lifecycle operations are chosen so optional future sync is possible.

No event-sourcing framework, CRDT, cloud backend or cross-machine conflict engine is introduced until sync becomes a concrete product requirement.

## 23. Deferred architecture

Through 0.1 defer:

```text
Web dashboard
own Streamable HTTP/OAuth
MCP Tasks for Quest lifecycle
MCP Apps
runtime plugin framework
cloud/team mode
source/diff ingestion
continuous telemetry
LLM judge
random loot/economy
mechanical title/rank bonuses
```

## 24. Release qualification

Release evidence must include:

```text
startRequestId retry/mismatch/late retry
one-open Quest concurrent start race
concurrent Finish one committed progression
crash before/after commit
WAL/runtime SQLite qualification
backup/migration tests
Hero switch/archive/delete invariants
all RPG goldens and threshold tables
RU/EN resource completeness
MCP exact tool order/schema/annotation/result snapshots
MCP 2026 + 2025 paths
Agent Skill lifecycle trigger/recovery evals
Codex reference E2E + cross-host smoke
privacy forbidden-field scans
```

Unit tests alone are not release qualification.
