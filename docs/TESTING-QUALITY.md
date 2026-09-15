# Hero Passport — Testing and Quality Strategy

**Status:** Accepted v3.2.1 core + 0.2-A/B/C/D Web qualification  
**Snapshot:** 2026-09-15

## 1. Principle

Every architectural promise needs executable evidence at the layer where it can fail.

```text
pure rule -> Domain test
use-case semantics -> Application test
SQLite invariant -> real file-backed SQLite integration/concurrency/crash test
MCP contract -> schema/snapshot/vector test
Agent orchestration -> Agent Skill eval
Web boundary -> real Kestrel process/HTTP + architecture tests
installation -> packaged host E2E
```

Green unit tests alone are not release qualification.

## 2. Test projects

```text
tests/HeroPassport.Domain.Tests/
tests/HeroPassport.Application.Tests/
tests/HeroPassport.Infrastructure.Tests/
tests/HeroPassport.App.Tests/
tests/HeroPassport.Web.Tests/
tests/HeroPassport.Architecture.Tests/
tests/HeroPassport.Contract.Tests/
tests/HeroPassport.AgentEvals/
```

## 3. Domain goldens

Commit stable vectors for:

```text
reward/2.0.0
skill-allocation/1.0.0
hero-progression/2.0.0
skill-progression/2.0.0
rank/1.0.0
trust-strain/1.0.0
streak/1.0.0
unlock/2.0.0
SafeTextV1
```

Required properties:

```text
same canonical input/version -> same numeric/semantic output
XP never negative
integer-only multiplier arithmetic
Skill allocation conserves Quest XP
threshold/rank edges exact
Trust/Strain clamp
abandoned neutral
unlock monotonicity
active Title priority deterministic
checked JSON-safe ceilings
```

Flavor prose selection is not a Domain determinism test. Engine emits semantic milestone keys only.

## 4. Bootstrap tests

Application + real SQLite:

```text
fresh bootstrap -> one Hero + setup complete
same bootstrapRequestId + same args -> replay same Hero
same bootstrapRequestId + changed args -> HP135
fresh bootstrapRequestId after setup -> HP002
two concurrent bootstrap requests -> exactly one setup
crash before commit -> setup remains incomplete/no receipt
crash after commit before response -> same request replay recovers
```

## 5. Configuration/context tests

```text
configure before setup -> HP001
configure after setup changes only allowlisted preferences
identical configure -> no-op
get_context before setup returns versions/setup=false safely
get_context after setup returns persisted settings
restart with autoStart=false -> Skill sees false
get_context returns all open Quests for current Project across Heroes
get_context read-only -> no Project row creation / no WAL bookkeeping write
Skill/Core contract mismatch -> fail-safe guidance
```

## 6. Active-Hero/Start tests

```text
Start requires explicit heroId
Activate(B) concurrent with already-formed Start(heroId=A) -> Quest belongs A
Start never re-reads active Hero for ownership
same startRequestId + same Project/Hero/args -> same Quest
same startRequestId + changed Hero -> HP135
same startRequestId from different Project -> HP135
Start replay after active Hero changed -> original Quest/Hero
Start replay after locale changed -> original Quest locale
fresh request + same Hero+Project open -> HP133
PrepareStartQuest normalizes with the same validation authority and performs no store access
```

## 7. One-open/linked-worktree tests

Real Project identity + SQLite:

```text
same Hero + same Project -> one open Quest max
different Heroes + same Project -> independent open Quests allowed
same Hero + different Projects -> independent open Quests allowed
linked worktrees resolve same ProjectId
same Hero + two linked worktrees + independent starts -> second HP133
```

The final case is an explicit 0.1 support limitation, not a bug.

## 8. Finish identity/conflict tests

```text
same finishRequestId + same payload -> persisted replay
same finishRequestId + changed payload -> HP135
new finishRequestId + finalized Quest + equivalent payload -> original result / alreadyFinalized
new finishRequestId + finalized Quest + different payload -> HP136
concurrent partial vs success -> exactly one persists; loser observes HP136 after retry/re-evaluation
active Hero switch before Finish -> persisted Quest Hero receives progression
UNIQUE report/xp event remain intact
PrepareFinishQuest shares validation/normalization with FinishQuestAsync and performs no store access
prepared Skills are defensively copied
```

No test introduces lease/agent ownership.

## 9. Crash injection

Use child processes terminated at controlled persistence points.

Required:

```text
bootstrap before/after commit
Start before/after commit
Finish before/after commit
CLI logical delete before/after commit
migration lock acquired then process killed
```

Never “recover” by deleting WAL/SHM.

## 10. SQLite connection-policy tests

Temporary file-backed DB only.

Prove effective:

```text
sqlite_version >=3.53.4
journal_mode=wal
foreign_keys=ON
synchronous=FULL
trusted_schema=OFF
Cache != Shared
Default Timeout=5
```

Repeat through:

```text
fresh connection
pooled reopen
clear pool + reopen
new child process
```

## 11. Direct schema-invariant tests

Bypass Application and attempt invalid SQL/EF inserts. Database must reject:

```text
invalid Quest status/result/status/evidence
Trust=-1 / 101
Strain=-1 / 101
negative XP/counters
scope/user-correction out of range
open Quest with finished_at
finished Quest without finished_at
second singleton app_settings row
setup=true with null active Hero
second open Quest same Hero+Project
invalid FK references
```

## 12. Mutation receipt tests

```text
args_encoding_version persisted
mutation-args/1 golden byte/hash vectors
serializer/whitespace changes do not change canonical hash
Start receipt binds ProjectId + HeroId
Finish receipt binds canonical finalization payload
Hero permanent delete marks related surviving receipts target_deleted
late create/start retry after target deletion never resurrects data
historical receipt remains interpretable after a new encoding version is introduced in fixture code
```

## 13. Migration tests

Every schema/release:

```text
empty -> latest
previous release fixture -> latest
model snapshot diff
CHECK/FK/partial-index review
quick_check + foreign_key_check
representative populated rebuild migration if required
```

Abandoned migration lock scenario:

```text
child acquires EF migration lock
kill child
next doctor reports suspicious __EFMigrationsLock
normal startup does not silently clear
explicit repair after safety preconditions
migration completes
integrity checks pass
```

## 14. Projection rebuild test

Canonical surviving history -> rebuild mutable projections -> public read models identical.

At minimum rebuild/compare:

```text
Hero total XP
Trust/Strain
success streak
hero_skills
hero_project_stats
Hero card/project stats
```

This validates repair/migration seams without introducing event sourcing.

## 15. Level-cap wire tests

```text
Hero L49 -> L50
Hero already L50 receives more XP
Skill L9 -> L10
Skill L10 receives more XP
isLevelCapped=true at cap
nextLevelXpRequired absent at cap
nextLevelXpRequired present below cap
XP continues accumulating
```

## 16. MCP snapshots

Current HP-MCP/2 v3.2.1 snapshot asserts:

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

Also assert:

```text
annotations
closed schemas
request-ID fields
explicit heroId Start
finishRequestId
HP136 error
pre-setup allowed tools
get_context result/version fields
level-cap optional fields
forbidden hero.delete/list_active_quests absence
structuredContent + one compatibility TextContent semantic equality
```

Exact JSON minification is not public business semantics.

## 17. MCP protocol qualification

Exercise preferred `2026-07-28` and `2025-11-25` compatibility path through official ModelContextProtocol C# SDK 2.2.0.

Task 1 first proves actual package restore/build availability; do not rely on search indexes alone.

stdio framing must keep stdout protocol-only.

## 18. Agent Skill evals

Minimum scenarios:

```text
short factual question -> no start
meaningful work -> start
meaningful-goal boundary treated as heuristic
persisted autoStart=false after restart -> no auto-start
same-goal followups -> no fragmentation
await input -> no finish
complete -> finish
explicit switch -> partial/abandoned then new start
ambiguous switch -> no silent close
inactive-Hero Quest discoverable via get_context
several plausible open Quests -> no guess
active Hero changed elsewhere after context -> explicit Start heroId stable
Start transport retry -> same startRequestId
Finish transport retry -> same finishRequestId
HP136 -> no overwrite attempt
observed/reported terminology accurate
Hero Passport calls -> no self-awarded tool_use
milestone flavor does not change semantic facts
Skill/Core version mismatch -> fail safe
```

Measure false-positive starts and premature finishes; conservative behavior is preferred.

## 19. Privacy/security tests

Static/runtime scans:

```text
no source/diff/raw-log/prompt/path/remote DTO/entity fields
Quest metadata sensitivity documented
MCP permanent delete absent
CLI delete wording does not claim forensic erasure
read-only MCP tools cause no durable writes
stdout protocol-only
stderr/request logging scrubbed
trusted_schema OFF
Git safe.directory not weakened
```

For the local Web boundary also prove:

```text
bootstrap/session secrets absent from normal stdout/stderr
bootstrap/session secrets absent from rendered product HTML and redirect targets
bootstrap/session secrets absent from SQLite bytes
hostile Host fails before product processing
cross-site/missing-antiforgery bootstrap claims fail closed
wrong bootstrap guess does not consume legitimate capability
successful capability replay fails
prior-process session cookie fails after restart
Testing-only deterministic secret/no-browser seams fail outside Testing
Start prepare/confirm POSTs reject multipart and >8 KiB bodies before product processing
Finish prepare POST rejects multipart and >128 KiB bodies before mutation
Finish confirm remains capped at 8 KiB body / 2 KiB encoded values
Start individual encoded form values remain capped at 2 KiB
Finish prepare individual encoded form values remain capped at 112 KiB
Finish prepare textarea raw UTF-16 input remains capped at 12000 code units
form-entry count remains capped at 16 and excess entries fail form parsing before Application work
missing-antiforgery Start/Finish POSTs fail without durable mutation
cross-site Start/Finish POSTs fail before mutation
case/trailing-slash route variants keep the same mutation boundary as endpoint routing
static-SSR prepare/confirm FormName handlers remain registered across stale/Gone/Busy POST states
confirmation pages omit internal HeroId/fingerprint/requestId/session/bootstrap material
Quest title/goal/summary and Finish attestations do not enter redirect URLs or ordinary process diagnostics
```

The wider Finish-prepare envelope is explicitly a raw `application/x-www-form-urlencoded` transport allowance, not a wider semantic text contract. `SafeTextV1` normalizes to NFC/whitespace before enforcing the existing `1..2000` Unicode-scalar summary limit. A maximum-valid normalized summary can arrive in canonically decomposed form substantially larger than its normalized representation; the qualified 112 KiB per-value / 128 KiB whole-request envelope admits that bounded case. Start and payload-free Finish confirmation remain at the stricter 8 KiB / 2 KiB limits.

## 20. 0.2-A/B/C/D Web qualification

`HeroPassport.Web.Tests` launches real Kestrel child processes against isolated `HERO_PASSPORT_HOME` and temporary Project roots.

Inherited 0.2-A read-path evidence remains required after 0.2-B authorization:

```text
actual listener is exactly IPv4 loopback / 127.0.0.1
ASPNETCORE_URLS=http://0.0.0.0:0 cannot widen the listener
explicit --project-root resolves through project-identity/1
omitted --project-root uses process cwd fallback
fresh storage renders bounded setup-required state after bootstrap
configured storage renders existing Application Hero/card truth after bootstrap
GET / creates no Project/Quest/history/receipt bookkeeping rows
HTML omits full local paths, workspace fingerprints and receipt/raw-evidence internals
Testing source-backed static asset manifest serves product CSS after bootstrap
```

0.2-B additionally qualifies the browser security transition at pure and real-process layers:

```text
process authority uses exact 32-byte bootstrap/session values
correct bootstrap consumes exactly once
wrong bootstrap does not consume legitimate capability
concurrent correct bootstrap has exactly one winner
malformed/wrong session fails closed
direct GET / without current session -> 401 and no product state
GET /__hero/bootstrap is state-free and contains no capability/session secret
unsupported Host -> 400
valid antiforgery + same-origin + capability claim -> 303 / + session cookie
cookie is HttpOnly + SameSite=Strict + Path=/ and has no Expires/Max-Age
missing antiforgery -> 400 without consuming capability
cross-site Origin/Fetch Metadata -> 400 without consuming capability
successful capability replay -> 403
session cookie from process A -> 401 against process B
security secrets are absent from SQLite and normal process output
Production rejects --no-open-browser
Production rejects deterministic test-secret environment variables
```

0.2-C adds unit, architecture and real-process evidence for the first Web mutation vertical:

```text
PrepareStartQuest shares Application validation/normalization and performs no store access
pending Start confirmations are bounded to 8 entries with a 10-minute TTL
claim/commit is concurrency-safe and duplicate successful confirm does not call Application twice
unauthenticated GET /quests/start -> 401
valid prepare redirects only to an opaque confirmation handle and DB still has zero Quest rows
confirmation displays normalized safe Hero/Project/type/title/goal without internal IDs/fingerprint/requestId
explicit confirm creates exactly one open Quest through StartQuestAsync
re-post of committed confirmation remains idempotent
prepared ownership/request identity survives active-Hero preference changes
multipart -> 415 and >8 KiB -> 413 before antiforgery/form parsing
malformed confirmation handle -> 400; unknown/expired handle -> 410; neither mutates
GET / after commit reflects the opened Quest
```

0.2-D adds the richer Finish mutation qualification without changing Core semantics:

```text
PrepareFinishQuest shares the same Application validation/normalization core as FinishQuestAsync and performs no store access
prepared Skills are defensively copied
pending Finish confirmations are independently bounded to 8 entries with a 10-minute TTL
malformed/non-canonical/non-v7 route QuestId -> 400 before runtime lookup
canonical current-Project-unavailable QuestId -> 404
unauthenticated Finish/confirm routes fail through the existing session boundary
Finish GET and prepare POST create no report/XP/finalization mutation
confirmation displays exact normalized safe Quest/Hero/Project/result/summary/attestation/Skill content only
explicit confirm commits one report/progression path through FinishQuestAsync
re-post of committed confirmation remains idempotent without a second Application call
unknown post-commit response failure releases the same prepared FinishRequestId for receipt-safe replay
equivalent AlreadyFinalized converges to success
HP135 and HP136 are terminal conflicts and remove pending state
active-Hero changes cannot redirect persisted Quest progression
maximum-valid 2000-scalar summary submitted as canonically decomposed NFD Hangul (~6000 raw Jamo and above the former 32 KiB request envelope) passes the real URL-encoded Kestrel/form/Application path and is rendered NFC-normalized in confirmation
17th form entry fails the configured form parser budget
Finish prepare multipart -> 415 and >128 KiB -> 413 before mutation
Finish confirm remains independently bounded at 8 KiB / 2 KiB-value limits
missing antiforgery/cross-site POST -> 400 without mutation
route-equivalent case/trailing-slash variants do not bypass mutation request hardening
stale prepare and Gone/Busy confirmation POSTs still resolve their named static-SSR forms instead of falling into framework form-not-found handling
redirect targets contain only the opaque handle; confirmation omits internal IDs/fingerprint/requestId/full path
GET / after Finish no longer presents the Quest as open and uses existing card/progression reads
Start remains GREEN with its unchanged 8 KiB / 2 KiB-value boundary
```

Architecture tests prove Web Components/Services do not own EF/SQLite access. They permit exactly one Minimal API-style POST location, `Security/BootstrapEndpoint.cs`, with exact route `/__hero/bootstrap/claim`, while rejecting general `MapGet/MapPut/MapDelete/MapPatch/MapGroup`, Identity/OAuth/authentication provider wiring, permissive CORS, forwarded-header deployment and interactive Blazor modes. Start/Finish prepare and confirmation pages are guarded to retain dedicated unique static-SSR form names and avoid hidden product payloads.

The security process tests intentionally use exact `ASPNETCORE_ENVIRONMENT=Testing` with deterministic secrets and `--no-open-browser`; both seams are rejected outside Testing. `UseStaticWebAssets()` is enabled only for that exact Testing profile so local build-output CSS can be qualified without enabling source-backed static Web assets in Production. Published Production Web artifact/static-asset and broader launch/package qualification remain separate later 0.2 release work.

## 21. Packaging/E2E risk-first checkpoint

Reference host: Codex.

Before implementing full RPG layers, prove a packaged vertical slice:

```text
fresh HERO_PASSPORT_HOME
bootstrap
get_context
real temporary Git repo
Skill Start explicit Hero
minimal Finish + base XP
server restart
context/history recovery
retry/crash/race vectors
```

Only after this checkpoint expand full reward/Skills/levels/Trust-Strain/Streak/Traits/Titles/localization/admin features.

## 22. Release checklist

No 0.1 release unless:

```text
all focused/unit suites green
real SQLite concurrency/crash/connection/schema tests green
migration-lock recovery green
projection rebuild green
MCP contract/protocol qualification green
Agent Skill eval thresholds accepted
RU/EN complete
privacy scans green
packaged Codex E2E green
cross-host compatibility recorded
```

0.2 release adds Web-specific browser security, bounded mutation confirmation, management, published-artifact and cross-platform Web qualification on top of these inherited Core gates. 0.2-A/B/C/D are incremental slices, not by themselves a full 0.2 release claim.