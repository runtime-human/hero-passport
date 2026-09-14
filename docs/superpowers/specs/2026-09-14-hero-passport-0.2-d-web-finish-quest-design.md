# Hero Passport 0.2-D — Web Finish Quest + bounded attestations + explicit confirmation

**Status:** design approved in chat; written spec pending review  
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
- a dedicated mutation middleware that requires same-origin browser provenance, accepts only `application/x-www-form-urlencoded`, sets an 8192-byte request-body limit before body reads, and installs bounded `FormOptions`;
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

0.2-D changes none of those product rules.

## 5. Application preparation seam

Today Finish validation, normalization and args-hash preparation occur inside `HeroPassportApplication.FinishQuestAsync(...)` immediately before store mutation. The confirmation page must display the exact normalized semantic payload before any mutation, so Web must not copy those rules.

Add a pure Application seam:

```text
HeroPassportApplication.PrepareFinishQuest(...)
```

It accepts the same logical `FinishQuestRequest` plus the current `ProjectBindingContext`, validates the Project and Finish payload, normalizes summary, validates metrics and Skills, and returns a prepared immutable value.

The prepared value contains at minimum:

```text
FinishRequestId
QuestId
Result
Summary
validated FinishQuestMetrics
validated SkillsUsed
```

`FinishQuestAsync(...)` is refactored to use the same private preparation core before hashing/calling the store. There is one validation/normalization implementation, not parallel Web/Application rule sets.

Preparation performs no store read/write, does not finalize a Quest, does not calculate rewards/progression and does not create a mutation receipt.

The args hash remains an internal Application/store concern and is not part of the Web prepared value or confirmation presentation.

## 6. Quest selection and ownership

Finish targets an explicit `questId`. The route identifier is a resource selector, not authorization.

The Web service loads `GetRuntimeContextAsync(currentProject)` and finds the matching open Quest in that current-Project context before offering preparation. This supplies safe presentation fields already available through Application:

- Quest title/type/goal;
- Hero display name;
- Project display name;
- start time/locale if needed for bounded context.

No new persistence read API is required for 0.2-D.

If the Quest isn't present in the current Project's open-Quest context, the Finish page fails safely as unavailable/stale and does not prepare a mutation.

The persisted Quest owner remains authoritative during commit. Active-Hero preference is never used to retarget Finish ownership. The existing Application/store behavior for Project mismatch and finalized/stale Quest state remains authoritative.

## 7. Web routes and form model

### 7.1 Finish page

Add static-SSR route:

```text
GET/POST /quests/finish/{questId}
```

The prepare form name is exactly:

```text
FinishQuestPrepare
```

Use one dedicated form DTO containing only user-editable Finish fields:

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

`QuestId`, Hero identity, Project identity, request identity, confirmation handle, reward/progression state and persistence identifiers are not form-bound editable fields.

The page may use HTML `maxlength`, numeric `min/max`, select options and checkbox controls for ergonomics. They are not authoritative validation.

### 7.2 Confirmation page

Add static-SSR route:

```text
GET/POST /quests/finish/confirm/{handle}
```

The confirmation form name is exactly:

```text
FinishQuestConfirm
```

The confirm form contains no hidden Finish product payload. The opaque route handle plus normal framework antiforgery/form metadata identifies server-side prepared state.

There must be exactly one `EditForm` for `FinishQuestConfirm` in the component. Conditional committed/pending presentation occurs inside that one form so the static-SSR form name remains unique.

## 8. Finish Web orchestration service

Add a focused Web-owned service:

```text
HeroPassportFinishQuestService
```

Responsibilities:

- load current runtime context;
- resolve the explicit open Quest under the current Project;
- generate `MutationRequestId.New()` exactly once per prepared Finish intent;
- construct a `FinishQuestRequest` from the dedicated form DTO and explicit QuestId;
- call `HeroPassportApplication.PrepareFinishQuest(...)`;
- store the prepared command plus safe presentation fields in the process-local pending store;
- load prepared confirmation presentation;
- commit only through `HeroPassportApplication.FinishQuestAsync(...)` using the exact stored `FinishRequestId`, `QuestId` and prepared payload;
- map known Application outcomes/errors to bounded Web statuses/messages.

It does not calculate reward, XP, levels, Skill progression, Rank, Trust/Strain, Streak, Traits/Titles or milestones.

## 9. Pending Finish confirmation store

Add a dedicated process-local singleton:

```text
PendingFinishQuestStore
```

Do not generalize or refactor `PendingStartQuestStore` in this slice.

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

The confirmation handle is generated from 16 cryptographically random bytes and encoded as unpadded base64url text (22 characters), matching the proven 0.2-C shape. It is an opaque process-local lookup handle, not authentication.

Match the current Start-store operational policy exactly unless implementation evidence proves a correctness problem:

- capacity: 8 live entries per Web process;
- lifetime: 10 minutes from preparation; commit does not extend lifetime;
- remove expired entries before lookup/insert/transition;
- if capacity is full, evict only the oldest `pending` entry;
- never evict `committing` or `committed` entries;
- if capacity remains full because no pending entry is evictable, new prepare returns safe capacity failure (`429`) without game mutation;
- exactly one caller can transition `pending -> committing`;
- successful commit, Application replay or equivalent already-finalized convergence -> `committed`;
- duplicate confirmation of a `committed` entry returns success without issuing a fresh Application mutation;
- unexpected failure where durable outcome is unknown -> `committing -> pending` with the same prepared `FinishRequestId`;
- known semantic conflict -> release to `pending` only when retrying the exact same prepared intent can still safely converge; otherwise return a bounded conflict and leave no automatic fresh request-ID path.

The store is process memory only and is never persisted to SQLite.

## 10. Prepare flow

`POST /quests/finish/{questId}` performs:

1. existing browser session authorization;
2. existing mutation request boundary before form/antiforgery parsing;
3. existing same-origin provenance check;
4. existing ASP.NET Core antiforgery validation;
5. static-SSR binding to the dedicated `FinishQuestPrepare` DTO;
6. load current runtime context and resolve the explicit open Quest under the current Project;
7. generate one `FinishRequestId`;
8. call pure Application preparation/normalization;
9. store the prepared Finish plus safe presentation under an opaque handle;
10. redirect to `/quests/finish/confirm/{handle}`.

This phase performs no Finish mutation, report insert, XP event, projection update or Quest finalization.

## 11. Confirmation display

`GET /quests/finish/confirm/{handle}` requires the normal browser session.

It renders the exact prepared state that will be committed:

- Hero display name;
- Project display name;
- Quest type/title/goal as safe context;
- normalized Finish result;
- normalized summary;
- `TestsMentioned`;
- scope-violation count;
- user-correction count;
- build status/evidence;
- tests status/evidence;
- selected Skill keys.

It does not render:

- `HeroId`;
- `QuestId` as raw internal text unless already required by the route itself;
- Project fingerprint/internal ProjectId;
- `FinishRequestId`;
- mutation args hash;
- SQLite details;
- bootstrap/session/antiforgery secrets.

Malformed handle syntax returns safe `400 Bad Request`. A syntactically valid but unknown, expired or evicted handle returns safe `410 Gone`. Neither response reflects the handle or sensitive Finish content.

A currently `committing` handle renders bounded busy state. A `committed` handle renders a safe already-finished/continue state with the same single confirmation form.

## 12. Commit flow

`POST /quests/finish/confirm/{handle}` performs:

1. existing session/origin/request-size/content-type/antiforgery gates;
2. validate bounded handle syntax;
3. atomically claim `pending -> committing`;
4. reconstruct `FinishQuestRequest` only from server-side prepared state;
5. call existing `HeroPassportApplication.FinishQuestAsync(...)` with the Web process's existing `ProjectBindingContext`;
6. classify the Application result/error;
7. update pending state;
8. on successful convergence redirect to `/`.

Successful convergence includes:

- first committed finalization;
- replay of the same durable request;
- existing `AlreadyFinalized` result for semantically equivalent finalization.

No new reward/progression calculation occurs in Web.

## 13. Existing Finish concurrency/idempotency mapping

The current core semantics are preserved:

```text
same FinishRequestId + same Project/Quest/payload
  -> replay original Finish

same FinishRequestId + changed semantic payload/context
  -> HP135

fresh request + already-finalized equivalent semantic payload
  -> AlreadyFinalized=true with original durable outcome

fresh request + already-finalized different semantic payload
  -> HP136

wrong Project binding
  -> HP134

concurrent different finalizations
  -> at most one durable finalization; loser conflicts
```

Web maps these without inventing new identity:

- first success / replay / equivalent `AlreadyFinalized` -> success redirect and mark confirmation committed;
- `HP135` -> safe `409 Conflict`;
- `HP136` -> safe `409 Conflict`;
- Project/Quest eligibility conflict -> safe bounded conflict/not-available response;
- unknown unexpected exception -> generic safe error and return the exact prepared entry to retryable `pending` state with the same request ID.

A conflict never generates a replacement `FinishRequestId` automatically.

## 14. Request bounds and same-origin boundary

Extend the existing `MutationRequestBoundaryMiddleware` path predicate to the new Finish POST routes. Do not create a second Finish-specific request-boundary middleware.

The existing exact policy remains:

```text
Content-Type:
  application/x-www-form-urlencoded only

Max request body:
  8192 bytes

Known Content-Length > 8192:
  413 Payload Too Large

Unsupported content type:
  415 Unsupported Media Type

Missing/invalid same-origin browser provenance:
  400 Bad Request

IHttpMaxRequestBodySizeFeature.MaxRequestBodySize:
  8192 before body read

FormOptions:
  BufferBodyLengthLimit = 8192
  KeyLengthLimit = 128
  ValueCountLimit = 16
  ValueLengthLimit = 2048
```

The existing bootstrap claim remains separate with its stricter 1024-byte boundary.

The global `AddRazorComponents` form-mapping limits already qualified in 0.2-C remain unchanged unless real maximum-valid Finish input proves them insufficient. Any limit adjustment must be evidence-driven and must not broaden bootstrap or unrelated request surfaces.

Because Finish has more fields than Start, acceptance must prove that the maximum supported semantic payload can still be encoded under the existing 8192-byte request limit and 16-value form limit. If the legitimate field count exceeds the current form-value limit once actual Blazor field names/antiforgery metadata are counted, the implementation must first document the exact required count and raise only the narrowest relevant limit. It must not silently relax the boundary.

## 15. ASP.NET Core 10 static-SSR requirements

Follow the current official ASP.NET Core 10 guidance:

- `FormName` is required for statically rendered server-side POST forms and must be unique;
- `[SupplyParameterFromForm]` does not use MVC model binding; dedicated form DTOs are required to prevent overposting;
- `AddRazorComponents(...)` is the supported place for form-mapping bounds;
- request-body size overrides must be set before the request body has started being read;
- `UseAntiforgery()` remains enabled and token-based validation remains part of the POST boundary;
- all client-supplied values remain untrusted until server-side Application validation.

Official references:

- https://learn.microsoft.com/aspnet/core/blazor/forms/binding?view=aspnetcore-10.0
- https://learn.microsoft.com/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0
- https://learn.microsoft.com/aspnet/core/blazor/security/static-server-side-rendering?view=aspnetcore-10.0
- https://learn.microsoft.com/aspnet/core/fundamentals/servers/kestrel?view=aspnetcore-10.0

0.2-C demonstrated that a valid antiforgery token must not be treated as a substitute for the Web adapter's explicit same-origin provenance requirement. 0.2-D keeps both layers.

## 16. Privacy and diagnostics

Potentially sensitive local metadata includes:

- Quest title/goal;
- Finish summary;
- selected Skills;
- build/test status/evidence attestations;
- user-correction/scope-violation counts.

These values may appear on the intended authenticated Finish/confirmation pages because the user must review them. Outside that intended presentation they must not appear in ordinary process stdout/stderr, redirect/query strings, application-controlled request-target logs, generic error bodies or unrelated telemetry.

Redirects contain only the opaque random confirmation handle.

Safe errors are bounded product messages. Raw exception text, SQL, workspace fingerprint, internal ProjectId, request hashes, mutation receipt data, browser bootstrap/session material, full paths, source/diffs/raw logs/prompts and Git remotes are not rendered.

The pending Finish store is memory-only, so there is no pre-commit SQLite copy of summary/attestation data.

## 17. Error semantics

Expected bounded cases:

- setup incomplete / no matching current-Project open Quest: Finish unavailable, no mutation;
- malformed route QuestId: safe not-found/bad-request behavior, no mutation;
- invalid result/summary/metrics/Skills: safe validation error, no pending game mutation;
- malformed confirmation handle: `400`, no mutation;
- unknown/expired/evicted confirmation handle: `410`, no mutation;
- confirmation currently committing: bounded busy state, no second commit;
- pending-store saturation with no evictable pending entry: `429`, no mutation;
- missing/invalid session: existing `401` boundary;
- hostile Host: existing `400` boundary;
- cross-site/missing provenance: existing mutation boundary `400` before mutation;
- missing/invalid antiforgery: framework/security rejection before mutation;
- oversized POST: `413`;
- unsupported form content type: `415`;
- `HP135`: safe `409 Conflict`;
- `HP136`: safe `409 Conflict`;
- wrong Project/stale Quest/eligibility conflict: bounded conflict/unavailable response with no retargeting;
- unexpected server failure: generic safe error; retry reuses the same prepared `FinishRequestId`.

No fallback path bypasses explicit confirmation.

## 18. UI scope

The Finish page is intentionally functional, not a broad visual redesign.

Required UI additions:

- a Finish action from the existing dashboard for open Quest rows/cards;
- one static-SSR Finish form;
- one confirmation page;
- bounded validation/conflict/busy/gone messages;
- redirect to the existing dashboard after successful convergence.

0.2-D does not add a separate reward/progression result page. The existing dashboard/card read model remains the post-commit destination. Detailed reward/history/progression presentation belongs to later roadmap slices.

## 19. Files expected to change

Likely Application areas:

```text
src/HeroPassport.Application/Runtime/HeroPassportApplication.cs
src/HeroPassport.Application/Runtime/FinishQuestModels.cs
```

Likely Web areas:

```text
src/HeroPassport.Web/Program.cs
src/HeroPassport.Web/Components/Pages/Home.razor
src/HeroPassport.Web/Components/Pages/FinishQuest.razor
src/HeroPassport.Web/Components/Pages/ConfirmFinishQuest.razor
src/HeroPassport.Web/Services/HeroPassportFinishQuestService.cs
src/HeroPassport.Web/Services/PendingFinishQuestStore.cs
src/HeroPassport.Web/Security/MutationRequestBoundaryMiddleware.cs
src/HeroPassport.Web/wwwroot/app.css
```

Expected tests:

```text
tests/HeroPassport.Application.Tests/FinishQuestPreparationTests.cs
tests/HeroPassport.Web.Tests/PendingFinishQuestStoreTests.cs
tests/HeroPassport.Web.Tests/FinishQuestServiceTests.cs
tests/HeroPassport.Web.Tests/FinishQuestWebAcceptanceTests.cs
existing WebProcess/LocalWebSecurity/Start Quest regression suites
tests/HeroPassport.Architecture.Tests/ProjectDependencyTests.cs
```

Documentation changes occur only after behavior is green and remain narrow/evidence-backed.

## 20. TDD and acceptance evidence

Implementation starts with RED tests before production behavior.

Required exact-head evidence:

1. unauthenticated Finish and confirm routes fail through the existing 0.2-B session boundary;
2. Finish page GET performs no report/XP/projection/Quest-finalization write;
3. prepare POST performs no durable Finish mutation;
4. Application preparation and `FinishQuestAsync(...)` use the same validation/normalization core;
5. confirmation renders the exact safe normalized Hero/Project/Quest/result/summary/metrics/Skills intended for commit;
6. invalid result/summary/metrics/Skills produce no pending game mutation;
7. maximum valid Finish payload fits the chosen request/form limits;
8. multipart/oversized/excess-form-entry/malformed form abuse fails boundedly before mutation;
9. missing/invalid antiforgery and clearly cross-site POST fail before mutation;
10. explicit confirm finalizes exactly one Quest and commits report/XP/projections once;
11. active-Hero preference changes after preparation do not redirect progression;
12. duplicate confirmation converges to success and does not issue a new durable Finish;
13. retry after an unknown outcome uses the same `FinishRequestId` and converges through existing receipt semantics;
14. an equivalent already-finalized Finish converges safely through `AlreadyFinalized` semantics;
15. a different already-finalized payload preserves `HP136` and does not alter the durable outcome;
16. wrong Project / missing Quest / stale Quest / malformed, unknown, expired or evicted confirmation state fails closed;
17. summary/attestation/Quest metadata is absent from ordinary diagnostics, generic errors and redirect targets outside intended authenticated content;
18. dashboard no longer shows the finalized Quest as open and reflects the resulting card/progression data already in its read model;
19. existing Start Web flow remains GREEN, including same-origin and request-boundary regression tests;
20. CLI/MCP/Application contracts and packaged behavior remain unchanged;
21. full CI, Web process acceptance and cross-platform packaged qualification are GREEN on exact PR head.

## 21. Explicit non-goals

0.2-D does not add or refactor:

- a generic mutation/confirmation framework;
- Quest history browser;
- dedicated reward/final-report detail page;
- Skill progression/detail pages;
- Rank/Traits/Titles detail pages;
- Hero create/activate/archive/restore/delete Web management;
- settings UI;
- new RPG/game rules, reward formulas or Skill keys;
- direct Web persistence access;
- general REST/Minimal API/GraphQL/gRPC product surface;
- Interactive Server/WebAssembly;
- Identity/OAuth/accounts;
- public/LAN/local-HTTPS/reverse-proxy hosting;
- Streamable HTTP MCP;
- broad visual redesign.

## 22. Completion gate

0.2-D is complete only when:

- the exact PR head passes full CI and required cross-platform/package qualification;
- architecture guards prove Web still owns no persistence and no general product API surface;
- security acceptance proves the 0.2-B/0.2-C boundary remains effective for Finish;
- the PR is independently reviewed for privacy/idempotency/concurrency regressions;
- post-merge `main` passes its push CI;
- #42 closes through the merge.
