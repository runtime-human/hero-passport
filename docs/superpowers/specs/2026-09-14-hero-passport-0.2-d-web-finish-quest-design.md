# Hero Passport 0.2-D — Web Finish Quest + bounded attestations + explicit confirmation

**Status:** implemented; completion is governed by section 22  
**Issue:** #42  
**Baseline:** `main@9829facd653dc6fbcbd872a144eafdcb413c00b8`  
**Branch:** `feat/0.2-d-web-finish-quest`

## 1. Purpose

0.2-D adds the second product mutation through `HeroPassport.Web`: finishing one explicit open Quest in the Web process's current Project.

The slice exists to prove that the already-qualified 0.2-B browser security boundary and 0.2-C explicit-confirmation pattern also work for the richer existing Finish contract without turning Web into a second game engine.

The user-visible contract is:

1. open an authenticated Finish page for an explicit open Quest;
2. enter the existing Finish result, summary, bounded attestations and 1-3 Skills;
3. review the exact normalized Quest/Hero/Project/result/summary/attestation/Skill payload that will be committed;
4. explicitly confirm;
5. finalize exactly one Quest through the existing Application mutation authority;
6. converge safely on replay/equivalent already-finalized state;
7. return to the dashboard, which reflects that the Quest is no longer open and shows the resulting card/progression state already exposed there.

The confirmation step is a human UX safety gate. It is not authentication and it does not replace Application/store concurrency or idempotency semantics.

## 2. Repository truth and constraints

The starting repository already has:

- ASP.NET Core / Blazor static SSR in `HeroPassport.Web`;
- IPv4 loopback-only dynamic Kestrel binding;
- strict Host Filtering for `127.0.0.1`;
- one-time process-local browser bootstrap capability and process-local session cookie;
- fail-closed session middleware before product reads;
- `UseAntiforgery()`;
- `MutationRequestBoundaryMiddleware`, which requires same-origin browser provenance before form/antiforgery processing and currently protects Start mutation POSTs;
- a qualified Start Quest static-SSR mutation/confirmation flow;
- `HeroPassportApplication.FinishQuestAsync(...)` with durable receipt replay/mismatch detection, atomic finalization, progression calculation and Project binding;
- `GetRuntimeContextAsync(...)`, which returns current-Project open Quests across Heroes with safe Hero/Quest presentation fields;
- privacy rules that allow bounded local Quest metadata while forbidding source/diff/raw-log/prompt/secret/full-path/Git-remote ingestion and disclosure.

0.2-D must not weaken any of those boundaries. `HeroPassport.Web` remains an outer adapter and does not access EF Core/SQLite directly.

## 3. Alternatives considered

### A. Selected: dedicated Finish preparation + process-local confirmation store

Add a Finish-specific Application preparation seam and a Finish-specific Web service/store, mirroring the proven Start pattern while preserving the richer Finish contract.

Advantages:

- no refactor of the already-qualified Start flow;
- exact preview of the payload that will later be committed;
- no sensitive summary/attestation state in redirect URLs or hidden confirmation fields;
- stable `FinishRequestId` across confirmation and retry;
- existing Finish concurrency/replay semantics stay authoritative;
- no schema change, Data Protection payload format or generic mutation framework.

### B. Generic Start/Finish confirmation subsystem

A generic confirmation abstraction could reduce code duplication, but 0.2-D would then modify both the new Finish flow and the already-qualified Start flow. That increases regression surface before a third use case proves the abstraction worthwhile. Rejected for 0.2-D.

### C. Direct one-step Finish POST

This is mechanically simpler, but it drops the explicit human confirmation pattern established by 0.2-C and makes retries/unknown outcomes less legible at the Web boundary. Rejected.

## 4. Existing Finish contract remains authoritative

The existing Application request is logically:

```text
FinishRequestId
QuestId
Result
Summary
Metrics:
  TestsMentioned
  ScopeViolations
  UserCorrections
  BuildStatus
  BuildEvidence
  TestsStatus
  TestsEvidence
SkillsUsed (1..3)
```

Current closed values and bounds remain unchanged:

```text
Result:
  success | partial | blocked | failed | abandoned

Summary:
  SafeTextV1, 1..2000 Unicode scalar values

ScopeViolations:
  0..20

UserCorrections:
  0..20

BuildStatus / TestsStatus:
  not_run | passed | failed | unknown

BuildEvidence / TestsEvidence:
  observed | reported | none

Attestation consistency:
  not_run -> none
  passed/failed -> evidence != none
  unknown -> any allowed evidence
  TestsStatus != not_run -> TestsMentioned must be true

SkillsUsed:
  1..3 unique values from the existing Skill key set
```

0.2-D changes none of those product rules and must accept the full currently-valid semantic Finish payload, including a normalized 2000-scalar Unicode summary.

## 5. Application preparation seam

Finish validation, normalization and args-hash preparation formerly occurred only inside `HeroPassportApplication.FinishQuestAsync(...)` immediately before store mutation. The confirmation page must display the exact normalized semantic payload before any mutation, so Web must not copy those rules.

Add the pure Application seam:

```text
public static PreparedFinishQuest PrepareFinishQuest(
    FinishQuestRequest request,
    ProjectBindingContext project)
```

It validates the Project and Finish payload, normalizes summary, validates metrics and Skills, and returns an immutable prepared value containing at minimum:

```text
FinishRequestId
QuestId
Result
Summary
validated FinishQuestMetrics
validated SkillsUsed
```

`FinishQuestAsync(...)` calls the same private preparation core before args hashing and store mutation. There is one validation/normalization implementation.

Preparation performs no store reads/writes, no Quest finalization, no reward/progression calculation and no mutation-receipt creation.

Preparation defensively copies the Skill collection. Neither `PreparedFinishQuest` nor the pending store may retain a mutable list owned by the form DTO/request caller.

The args hash remains an internal Application/store concern and is never exposed in the Web prepared value.

## 6. Quest selection, route parsing and ownership

Finish targets an explicit route `questId`. The identifier is a resource selector, not authorization.

`QuestId.Parse` requires lowercase canonical UUIDv7. Web route behavior is exact:

- malformed/non-canonical/non-v7 `questId` -> `400 Bad Request`;
- canonical UUIDv7 not present among the current Project's open Quests -> `404 Not Found`;
- matching current-Project open Quest -> Finish form is available.

The same current-Project lookup rule applies on prepare POST before a pending confirmation is created. Web does not reveal whether a syntactically valid missing ID belongs to another Project or a finalized/deleted Quest.

The Web service uses `GetRuntimeContextAsync(currentProject)` to obtain safe Quest/Hero/Project presentation; no new persistence read API is required.

The persisted Quest owner remains authoritative at commit. Active-Hero preference is never used to retarget Finish ownership. Existing Application/store behavior remains authoritative for races after preparation.

## 7. Web routes and form model

### Finish page

```text
GET/POST /quests/finish/{questId}
FormName = FinishQuestPrepare
```

Use a dedicated form DTO containing only:

```text
Result
Summary
TestsMentioned
ScopeViolations
UserCorrections
BuildStatus
BuildEvidence
TestsStatus
TestsEvidence
SkillsUsed
```

`QuestId`, Hero/Project identity, `FinishRequestId`, confirmation handle, reward/progression state and persistence identifiers are not form-bound editable fields.

HTML `maxlength`, `min/max`, select options and checkbox controls are ergonomic only. Application validation remains authoritative.

### Confirmation page

```text
GET/POST /quests/finish/confirm/{handle}
FormName = FinishQuestConfirm
```

The confirm form contains no hidden Finish product payload. Only the opaque route handle plus framework form/antiforgery metadata identifies server-side prepared state.

There must be exactly one `EditForm` with `FinishQuestConfirm` in the component. The named form remains registered across ready/committed/busy/gone states so static-SSR POST routing can resolve stale or concurrent submissions; only actionable controls are conditional.

The prepare page follows the same static-SSR rule: `FinishQuestPrepare` remains registered while its loading/unavailable/ready contents vary, so a Quest becoming stale between GET and POST reaches the bounded Web handler instead of framework form-not-found handling.

## 8. Finish Web orchestration service

Add:

```text
HeroPassportFinishQuestService
```

Responsibilities:

- load current runtime context;
- parse/resolve the explicit open Quest under the current Project;
- generate `MutationRequestId.New()` exactly once per prepared Finish intent;
- map the dedicated form DTO into `FinishQuestRequest`;
- call `HeroPassportApplication.PrepareFinishQuest(...)`;
- store prepared state and safe presentation in process memory;
- load confirmation presentation;
- commit only through `HeroPassportApplication.FinishQuestAsync(...)` with the exact stored request identity and payload;
- map known Application outcomes/errors to bounded Web statuses/messages.

It never calculates reward, XP, levels, Skill progression, Rank, Trust/Strain, Streak, Traits/Titles or milestones.

## 9. Pending Finish confirmation store

Add the dedicated singleton:

```text
PendingFinishQuestStore
```

Do not generalize/refactor `PendingStartQuestStore` in 0.2-D.

Each entry contains:

```text
PreparedFinishQuest
safe Hero name
safe Project display name
safe Quest title/type/goal presentation
CreatedAtUtc
ExpiresAtUtc
state: pending | committing | committed
```

Handle shape matches 0.2-C: 16 cryptographically random bytes encoded as 22-character unpadded base64url. It is not authentication.

Match the current Start-store operational policy exactly:

- capacity = 8 live entries;
- lifetime = 10 minutes from preparation; commit doesn't extend it;
- remove expired entries before lookup/insert/transition;
- when full, evict only the oldest `pending` entry;
- never evict `committing` or `committed` entries;
- if still full because nothing is evictable, prepare fails safely with `429` and no game mutation;
- exactly one caller can transition `pending -> committing`;
- successful first commit, Application replay or equivalent `AlreadyFinalized` convergence -> `committed`;
- duplicate confirm of `committed` -> success without a fresh Application call;
- unexpected exception with uncertain durable outcome -> `committing -> pending` with the same `FinishRequestId`;
- terminal known conflicts (`HP135`, `HP136`, wrong/stale target) return the current bounded conflict response and remove the pending entry; a later use of that handle therefore yields `410`;
- no conflict path automatically creates a replacement request ID.

Pending Finish state is never persisted to SQLite.

## 10. Prepare flow

`POST /quests/finish/{questId}` performs:

1. current browser-session authorization;
2. current mutation request boundary before form/antiforgery parsing;
3. current same-origin provenance validation;
4. ASP.NET Core antiforgery validation;
5. static-SSR DTO binding;
6. strict UUIDv7 parse and current-Project open-Quest resolution;
7. generate one `FinishRequestId`;
8. call pure Application preparation;
9. store prepared state under an opaque handle;
10. redirect to `/quests/finish/confirm/{handle}`.

This phase performs no final report insert, XP event, projection write, receipt commit or Quest finalization.

## 11. Confirmation display

`GET /quests/finish/confirm/{handle}` requires the current browser session and renders exactly:

- Hero display name;
- Project display name;
- Quest type/title/goal as context;
- normalized result;
- normalized summary;
- `TestsMentioned`;
- scope-violation count;
- user-correction count;
- build status/evidence;
- tests status/evidence;
- selected Skills.

It does not render HeroId, Project fingerprint/internal ProjectId, `FinishRequestId`, mutation args hash, SQLite details or browser security secrets. `QuestId` isn't repeated as product text on the confirmation page.

Handle behavior:

- malformed handle -> `400`;
- valid but unknown/expired/evicted handle -> `410`;
- `committing` -> bounded busy state;
- `committed` -> bounded already-finished/continue state using the same single confirmation form.

Error bodies never reflect the handle or sensitive Finish content.

## 12. Commit flow

`POST /quests/finish/confirm/{handle}` performs:

1. session/origin/request-size/content-type/antiforgery gates;
2. handle syntax validation;
3. atomic `pending -> committing` claim;
4. reconstruct `FinishQuestRequest` solely from server-side prepared state;
5. call existing `HeroPassportApplication.FinishQuestAsync(...)` with the existing Web `ProjectBindingContext`;
6. classify the result/error;
7. update/remove/release pending state according to section 9;
8. on successful convergence redirect to `/`.

Successful convergence means first finalization, same-request replay, or semantically equivalent `AlreadyFinalized` result.

## 13. Existing Finish concurrency/idempotency mapping

Preserve current core semantics:

```text
same FinishRequestId + same Project/Quest/payload
  -> replay original Finish

same FinishRequestId + changed semantic payload/context
  -> HP135

fresh request + already-finalized equivalent payload
  -> AlreadyFinalized=true with original outcome

fresh request + already-finalized different payload
  -> HP136

wrong Project binding
  -> HP134

concurrent different finalizations
  -> at most one durable finalization
```

Web mapping:

- first success / replay / equivalent `AlreadyFinalized` -> mark committed and redirect success;
- `HP135` -> safe `409 Conflict`, remove pending entry;
- `HP136` -> safe `409 Conflict`, remove pending entry;
- stale/wrong target conflict -> bounded conflict/not-found semantics and no new request ID;
- unexpected exception -> generic safe error and release the exact entry back to `pending` for same-ID retry.

## 14. Request bounds and same-origin boundary

0.2-D extends the existing `MutationRequestBoundaryMiddleware`; it does not create a parallel Finish middleware.

Security behavior stays common across Start and Finish mutation POSTs:

```text
Content-Type:
  application/x-www-form-urlencoded only

unsupported content type:
  415

missing/invalid same-origin provenance:
  400

body ceiling configured through IHttpMaxRequestBodySizeFeature
before request body read
```

Mutation-path matching follows ASP.NET Core endpoint equivalence for the supported paths: case-insensitive comparison and one optional trailing slash, while parameterized routes still require exactly one route-value segment. This prevents route spelling variants from bypassing the request boundary.

The body/value ceilings are path-specific because Finish prepare carries the existing 2000-Unicode-scalar summary while confirmation carries no product payload.

### Existing Start prepare + confirm — unchanged

```text
MaxRequestBodySize = 8192
BufferBodyLengthLimit = 8192
KeyLengthLimit = 128
ValueCountLimit = 16
ValueLengthLimit = 2048
```

### Finish prepare

```text
MaxRequestBodySize = 131072        # 128 KiB
BufferBodyLengthLimit = 131072
KeyLengthLimit = 128
ValueCountLimit = 16
ValueLengthLimit = 114688           # 112 KiB encoded individual value
HTML textarea maxlength = 12000    # raw UTF-16 code units, ergonomic only
```

### Finish confirm

```text
MaxRequestBodySize = 8192
BufferBodyLengthLimit = 8192
KeyLengthLimit = 128
ValueCountLimit = 16
ValueLengthLimit = 2048
```

These are raw transport ceilings, not semantic text limits. `SafeTextV1.Normalize(...)` validates UTF-16, normalizes to NFC, collapses whitespace and only then enforces the existing `1..2000` Unicode-scalar summary contract. Therefore a semantically valid 2000-scalar normalized value can be materially larger on the wire before normalization.

The supplementary-code-point case alone can approach 24 KiB after form percent-encoding, but it is not the worst canonical-equivalence case. For example, 2000 Hangul syllables can be submitted in NFD as roughly 6000 Jamo; the URL-encoded payload is above the former 32 KiB boundary and then composes to the same valid 2000-scalar NFC value. The qualified 12000-code-unit / 112 KiB-value / 128 KiB-request envelope admits the bounded canonical-decomposition case with headroom for antiforgery and the remaining bounded fields without widening Application semantics.

The larger envelope applies only to exact Finish prepare POST paths. Finish confirm deliberately returns to the compact 8 KiB / 2 KiB profile because it posts only framework metadata and the opaque route handle. Start remains unchanged at 8 KiB / 2 KiB. Bootstrap remains its separate 1024-byte boundary.

Known `Content-Length` above the route ceiling returns `413`. The Kestrel feature is set before body reads.

The already-qualified global Razor form-mapping limits remain:

```text
MaxFormMappingCollectionSize = 16
MaxFormMappingRecursionDepth = 4
MaxFormMappingErrorCount = 16
MaxFormMappingKeySize = 128
```

The raw form parser also retains `ValueCountLimit = 16`. Qualification includes actual `ReadFormAsync` coverage for the decomposed maximum-valid value, an outer-limit rejection, a compact-confirm regression and a 17-entry form failure before Application work.

## 15. ASP.NET Core 10 requirements

Current official ASP.NET Core 10 guidance is normative for framework behavior:

- static-SSR POST forms require unique `FormName` values;
- `[SupplyParameterFromForm]` doesn't use MVC model binding, so dedicated DTOs are the overposting boundary;
- `AddRazorComponents(...)` is the supported form-mapping configuration point;
- per-request body limits must be configured before request-body reading begins;
- `UseAntiforgery()` remains enabled and token validation remains part of the POST boundary;
- missing or invalid antiforgery tokens are both an invalid antiforgery verdict and fail before mutation;
- all client input remains untrusted until server-side Application validation.

Official references:

- https://learn.microsoft.com/aspnet/core/blazor/forms/binding?view=aspnetcore-10.0
- https://learn.microsoft.com/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0
- https://learn.microsoft.com/aspnet/core/blazor/security/static-server-side-rendering?view=aspnetcore-10.0
- https://learn.microsoft.com/aspnet/core/fundamentals/servers/kestrel?view=aspnetcore-10.0

0.2-C demonstrated that a valid antiforgery token isn't a substitute for this adapter's explicit same-origin provenance requirement. 0.2-D keeps both layers.

## 16. Privacy and diagnostics

Potentially sensitive local metadata includes Quest title/goal, Finish summary, selected Skills, build/test status/evidence and correction/violation counts.

These values may appear only in intended authenticated Finish/confirmation presentation. Outside that presentation they must not appear in ordinary process stdout/stderr, redirect/query strings, application-controlled request-target logs, generic error bodies or unrelated telemetry.

Redirects contain only the opaque random confirmation handle.

Safe errors never render raw exception text, SQL, workspace fingerprint, internal ProjectId, request hashes, mutation-receipt data, browser bootstrap/session material, full paths, source/diffs/raw logs/prompts or Git remotes.

The pending Finish store is memory-only, so there is no pre-commit SQLite copy of summary/attestation data.

## 17. Error semantics

Exact bounded behavior:

- malformed route `questId` -> `400`, no mutation;
- canonical UUIDv7 not available as an open Quest in the current Project -> `404`, no mutation;
- invalid result/summary/metrics/Skills -> safe validation error, no pending durable mutation;
- malformed confirmation handle -> `400`;
- unknown/expired/evicted/terminal-conflict-removed handle -> `410`;
- confirmation currently committing -> bounded busy state, no second commit;
- pending-store saturation with no evictable pending entry -> `429`;
- missing/invalid session -> existing `401`;
- hostile Host -> existing `400`;
- cross-site/missing provenance -> `400` before form processing;
- missing/invalid antiforgery -> framework `400` before mutation;
- oversized POST -> `413` using the route-specific ceiling;
- unsupported content type -> `415`;
- `HP135` -> `409`, remove pending entry;
- `HP136` -> `409`, remove pending entry;
- unexpected server failure -> generic safe error; retry uses the same prepared `FinishRequestId`.

No fallback path bypasses confirmation.

## 18. UI scope

Required UI work is functional only:

- Finish action from existing dashboard open-Quest presentation;
- one static-SSR Finish form;
- one confirmation page;
- bounded validation/conflict/busy/gone messages;
- redirect to existing dashboard after successful convergence.

No separate reward/final-report/progression result page is added. Detailed history/reward/Skill/Rank/Trait/Title presentation remains later roadmap work.

## 19. Expected files

Application files:

```text
src/HeroPassport.Application/Runtime/HeroPassportApplication.cs
src/HeroPassport.Application/Runtime/FinishQuestModels.cs
```

Web files:

```text
src/HeroPassport.Web/Program.cs
src/HeroPassport.Web/Components/Pages/Home.razor
src/HeroPassport.Web/Components/Pages/FinishQuest.razor
src/HeroPassport.Web/Components/Pages/ConfirmFinishQuest.razor
src/HeroPassport.Web/Services/HeroPassportFinishQuestService.cs
src/HeroPassport.Web/Services/PendingFinishQuestStore.cs
src/HeroPassport.Web/Security/MutationRequestBoundaryMiddleware.cs
```

Tests:

```text
tests/HeroPassport.Application.Tests/FinishQuestPreparationTests.cs
tests/HeroPassport.Web.Tests/PendingFinishQuestStoreTests.cs
tests/HeroPassport.Web.Tests/FinishQuestServiceTests.cs
tests/HeroPassport.Web.Tests/FinishQuestProcessTests.cs
tests/HeroPassport.Web.Tests/FinishQuestStaticSsrTests.cs
tests/HeroPassport.Web.Tests/MutationRequestBoundaryTests.cs
existing WebProcess/LocalWebSecurity/Start regression suites
tests/HeroPassport.Architecture.Tests/ProjectDependencyTests.cs
```

Canonical docs reflect the established behavior and transport contracts; final completion still requires the exact-head gate below.

## 20. TDD and acceptance evidence

Implementation uses RED tests before production behavior and exact failure evidence before boundary corrections.

Required exact-head evidence:

1. unauthenticated Finish/confirm routes fail through the 0.2-B session boundary;
2. Finish GET performs no report/XP/projection/finalization write;
3. prepare POST performs no durable Finish mutation;
4. Application prepare/commit share one validation/normalization core and prepared Skills are defensively copied;
5. confirmation shows the exact normalized safe payload that will commit;
6. invalid result/summary/metrics/Skills don't create a durable Finish mutation;
7. maximum-valid 2000-scalar summary submitted in canonically decomposed form successfully passes the bounded Finish-prepare transport and confirmation renders the NFC-normalized value;
8. a Finish-prepare request above 128 KiB, multipart input and excess form entries fail boundedly before mutation/Application work, while Finish confirm remains at its compact 8 KiB / 2 KiB-value limits;
9. missing/invalid antiforgery and cross-site POST fail before mutation;
10. malformed QuestId -> 400 and valid unavailable QuestId -> 404 without mutation;
11. explicit confirm finalizes exactly one Quest and commits report/XP/projections once;
12. active-Hero preference changes after prepare don't redirect progression;
13. duplicate confirmation converges to success without a new Application mutation;
14. unknown-outcome retry uses the same `FinishRequestId` and converges through receipt semantics;
15. equivalent already-finalized Finish converges through `AlreadyFinalized`;
16. different already-finalized payload preserves `HP136` with no durable change;
17. wrong/stale target and malformed/unknown/expired/evicted confirmation state fail closed, with static-SSR named forms still registered so stale/concurrent POSTs reach bounded handlers;
18. summary/attestation/Quest metadata is absent from ordinary diagnostics, generic errors and redirect targets outside intended authenticated content;
19. dashboard no longer presents the finalized Quest as open and reflects existing card/progression reads;
20. Start Web flow stays GREEN with its unchanged 8 KiB / 2 KiB-value boundary, and case/trailing-slash endpoint variants cannot bypass mutation hardening;
21. CLI/MCP/Application contracts and packaged behavior remain unchanged;
22. full CI, real-process Web acceptance and cross-platform packaged qualification are GREEN on exact PR head.

Review-driven TDD evidence additionally covers route-equivalent case/trailing-slash matching, supplementary Unicode browser length, canonical-decomposition transport size, stale prepare form dispatch and Gone/Busy confirmation form dispatch. These regressions are retained as executable tests rather than review-only assumptions.

## 21. Explicit non-goals

0.2-D does not add or refactor:

- generic mutation/confirmation framework;
- Quest history browser;
- dedicated reward/final-report detail page;
- Skill progression/detail pages;
- Rank/Traits/Titles detail pages;
- Hero management or settings UI;
- new RPG rules/reward formulas/Skill keys;
- direct Web persistence access;
- general REST/Minimal API/GraphQL/gRPC product surface;
- Interactive Server/WebAssembly;
- Identity/OAuth/accounts;
- public/LAN/local-HTTPS/reverse-proxy hosting;
- Streamable HTTP MCP;
- broad visual redesign.

## 22. Completion gate

0.2-D is complete only when:

- exact PR head passes full CI and cross-platform/package qualification;
- architecture guards prove Web still owns no persistence and no general product API surface;
- security acceptance proves 0.2-B/0.2-C boundaries remain effective for Finish;
- the PR is independently reviewed for privacy/idempotency/concurrency regressions;
- post-merge `main` passes push CI;
- #42 closes through merge.