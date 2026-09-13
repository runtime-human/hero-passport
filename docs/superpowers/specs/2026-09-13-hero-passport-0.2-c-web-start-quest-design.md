# Hero Passport 0.2-C — Web Start Quest mutation + explicit confirmation

**Status:** design approved in chat; written spec pending review  
**Issue:** #40  
**Baseline:** `main@ededcb5a44e822533dc3b00f28d322b5eca67d33`  
**Branch:** `feat/0.2-c-web-start-quest`

## 1. Purpose

0.2-C introduces the first product mutation through `HeroPassport.Web`: starting a Quest for the current active Hero and the Web process's current Project.

The slice exists to prove one safe, reusable Web mutation pattern on top of the already-qualified 0.2-B browser boundary. It intentionally does not implement Finish Quest, history, Hero administration or settings.

The user-visible contract is:

1. open an authenticated Start Quest form;
2. enter Quest type, title and goal;
3. review the exact normalized Hero/Project/type/title/goal that will be committed;
4. explicitly confirm;
5. create exactly one Quest through the existing Application mutation authority;
6. return to the dashboard, which now shows the open Quest.

The confirmation step is a human UX safety gate. It is not a second authentication scheme.

## 2. Repository truth and constraints

The starting repository already has:

- Blazor static SSR in `HeroPassport.Web`;
- loopback-only Kestrel binding;
- strict Host Filtering for `127.0.0.1`;
- a one-time browser bootstrap capability and process-local session cookie;
- `UseAntiforgery()` plus same-origin checks;
- a read-only dashboard backed by `HeroPassportApplication`;
- `HeroPassportApplication.StartQuestAsync(...)` and durable Start idempotency;
- one-open-Quest-per-Hero+Project storage enforcement;
- privacy rules that allow bounded Quest `title`/`goal` but forbid source, diffs, raw logs, prompts, secrets, full paths and Git remotes.

0.2-C must not weaken any of those boundaries. Web remains an outer adapter and does not access `DbContext`/SQLite directly.

## 3. Alternatives considered

### A. Recommended: process-local prepared confirmation store

The first POST validates and normalizes input, captures the active Hero and current Project presentation, generates the durable Start request ID, and stores a short-lived prepared command in Web process memory under an opaque random handle. A confirmation GET renders that prepared state. The confirm POST carries only the opaque handle and antiforgery fields; the actual Quest data is loaded from process memory and committed through Application.

Advantages:

- confirmation shows the exact state that will be committed;
- title/goal are not copied into redirect URLs or hidden confirm-form fields;
- Hero ownership cannot silently change between prepare and commit;
- no new persistence/schema is required;
- no new cryptographic payload format or Data Protection dependency is required;
- stale state naturally dies with the Web process.

This is the selected design.

### B. Re-post all fields on the confirmation POST

This would be simpler mechanically, but the second POST becomes a fresh untrusted copy of the user's input. The confirmation screen could show one value while a modified form submits another unless all values are compared/revalidated. It also repeats sensitive Quest metadata in hidden fields. Rejected.

### C. Signed/encrypted confirmation payload

A Data Protection/HMAC payload could make the confirmation state stateless, but 0.2-C is a local single-process flow. Introducing a second cryptographic state protocol adds complexity without solving a demonstrated product need. Rejected for this slice.

## 4. Application validation seam

Today Start validation and normalization occur inside `HeroPassportApplication.StartQuestAsync(...)`, immediately before the store mutation. The Web confirmation page must display the exact normalized values before any mutation, so Web must not duplicate those rules.

0.2-C therefore adds a small pure Application preparation seam:

```text
HeroPassportApplication.PrepareStartQuest(...)
```

It accepts the same logical Start input and current `ProjectBindingContext`, performs the same existing Project/Quest-type/text validation and normalization, and returns a prepared value object containing only the safe normalized Start request fields needed for later commit.

`StartQuestAsync(...)` is refactored to use the same preparation logic before calling the store. There is one validation implementation, not parallel Web/Application rule sets.

The preparation seam performs no store reads or writes and does not create Project/Quest rows.

The prepared Application value contains:

- `StartRequestId`;
- `HeroId`;
- normalized `QuestType`;
- normalized `Title`;
- normalized `Goal`.

It does not expose Project fingerprint/path or persistence IDs for rendering.

## 5. Web components and services

### 5.1 Start page

Add a static-SSR page:

```text
/quests/start
```

The page uses a dedicated form DTO with only:

```text
questType
title
goal
```

The form has a unique `FormName` and is bound with `[SupplyParameterFromForm]`. Domain/Application models are never directly form-bound.

The page loads runtime context through Application. Start is available only when setup is complete and an active non-archived Hero is present. If the active Hero already has an open Quest in the current Project, the page shows a bounded product conflict rather than offering a misleading successful path.

The UI may use HTML `maxlength`, select options and required markers for ergonomics, but these are not authoritative validation. Application validation remains authoritative.

### 5.2 Start Quest Web service

Add a Web-owned service responsible for orchestration only, for example:

```text
HeroPassportStartQuestService
```

Responsibilities:

- load current runtime context;
- capture active `HeroId` and safe Hero/Project display names;
- generate `MutationRequestId.New()` once per prepared intent;
- call the pure Application preparation seam;
- insert the prepared command into the process-local confirmation store;
- commit a prepared command through existing `StartQuestAsync(...)`;
- map known Application error codes to bounded Web presentation results.

It does not own game rules, SQL or persistence.

### 5.3 Prepared confirmation store

Add a Web process-local singleton dedicated only to Start Quest confirmations.

Each entry contains:

```text
opaque confirmation handle
prepared Start Application value
safe Hero display name
safe Project display name
created/expires timestamps
state: pending | committing | committed
```

The handle is generated from at least 128 bits of cryptographic randomness and encoded as bounded base64url text. It is an opaque lookup handle, not an authentication substitute.

The store is bounded:

- maximum 8 live pending entries per Web process;
- 10-minute lifetime per entry;
- expired entries are removed before lookup/insert;
- when capacity remains full after expiry cleanup, the oldest pending entry is evicted;
- eviction only invalidates the old confirmation page; it never mutates game state.

The store is concurrency-safe. Exactly one confirm operation can transition an entry from `pending` to `committing`. Concurrent confirm submissions for the same handle cannot both invoke a fresh Start mutation. After a successful Application result, the entry becomes `committed` long enough to return/replay the same safe success result during the current request/retry window, then can be removed. If Application returns a retryable transport/process failure before a durable result is known, the service must not manufacture a second request ID; it retains the same prepared Start request ID for retry.

Because the existing Application Start contract is already idempotent, even a duplicated commit attempt using the same prepared request ID cannot award/create a second Quest.

## 6. Confirmation flow

### Prepare

`POST /quests/start` is the static-SSR form submission.

Flow:

1. existing 0.2-B session middleware authenticates the browser session;
2. request-bound middleware rejects unsupported/oversized form requests before expensive parsing;
3. ASP.NET Core antiforgery/origin checks run;
4. Razor static-SSR form mapping binds the dedicated DTO;
5. Web service loads current runtime context;
6. it captures current active Hero identity and current Project display name;
7. it generates one `StartRequestId`;
8. Application preparation validates/normalizes Quest input without mutation;
9. Web stores the prepared command under an opaque handle;
10. response redirects to `/quests/start/confirm/{handle}`.

No Quest mutation occurs during this phase.

### Confirm display

`GET /quests/start/confirm/{handle}` requires the normal 0.2-B session.

It loads the process-local prepared entry and renders exactly:

- Hero display name;
- Project display name;
- normalized Quest type;
- normalized title;
- normalized goal.

It does not render `HeroId`, Project fingerprint, internal ProjectId, request hash, bootstrap/session secrets or storage details.

The confirm form contains only the opaque handle plus normal framework antiforgery/form metadata.

### Commit

`POST /quests/start/confirm/{handle}` (or same confirmation route with a unique confirm `FormName`) requires session + origin + antiforgery.

Flow:

1. validate bounded handle syntax;
2. atomically claim the prepared entry for commit;
3. use the stored `HeroId`, stored normalized Quest values and stored `StartRequestId`;
4. call `HeroPassportApplication.StartQuestAsync(...)` with the Web process's existing `ProjectBindingContext`;
5. on success, mark the prepared entry committed and redirect to `/`;
6. dashboard reads current state normally and displays the newly opened Quest.

The active-Hero preference is not re-read to replace the prepared `HeroId`. A preference switch in another surface therefore cannot silently retarget the Quest.

If the prepared Hero was archived/deleted or a competing open Quest now exists, Application/store rules remain authoritative and the Web presents a safe conflict without inventing fallback ownership.

## 7. Request bounds and form hardening

All Start/confirm mutation POSTs accept only `application/x-www-form-urlencoded`.

Before antiforgery/form parsing, a focused Web request-bound middleware applies to the Start/confirm POST paths:

- maximum request body: 8 KiB;
- reject unsupported content type with `415`;
- reject known oversized content length with `413`;
- set `IHttpMaxRequestBodySizeFeature.MaxRequestBodySize = 8192` before the body is read.

The existing bootstrap claim keeps its stricter 1 KiB boundary.

`AddRazorComponents` form mapping is tightened for the entire small static-SSR Web adapter:

```text
MaxFormMappingCollectionSize = 16
MaxFormMappingRecursionDepth = 4
MaxFormMappingErrorCount = 16
MaxFormMappingKeySize = 128
```

The dedicated mutation request middleware additionally installs bounded form parsing compatible with these small forms, including a small value-count/key/value limit. Exact runtime constants must fit the antiforgery/FormName overhead plus the product maximums (`title` <= 120 scalar chars, `goal` <= 500 scalar chars) while remaining comfortably below framework defaults. Multipart parsing is not enabled for this flow.

The implementation must verify these limits with real-process tests instead of assuming middleware ordering.

## 8. Security model

0.2-C reuses 0.2-B unchanged as the authentication/request-origin boundary:

- loopback listener;
- Host Filtering;
- process-local session cookie;
- same-origin checks;
- `UseAntiforgery()`;
- fail-closed unauthenticated product routes.

The confirmation handle is not an authentication credential. Possessing a handle without the current browser session and valid same-origin antiforgery context must not authorize commit.

No permissive CORS, forwarded-header trust, public/LAN binding, Identity/OAuth or reverse proxy support is added.

All form-bound data is treated as untrusted. The server-side prepared entry, not hidden client data, is the source of truth for commit.

## 9. Privacy and diagnostics

Quest title/goal are intentionally rendered on authenticated Start/confirmation pages because the user must review them. Outside that intended content, they must not appear in normal process logs, exception responses, redirects, query strings, request-target diagnostics under application control or telemetry.

The confirmation redirect contains only the opaque handle.

Safe error presentation uses bounded product messages keyed from known Application error codes. Raw exception text, SQL, full paths, Project fingerprint, internal IDs, request hashes and browser secrets are not rendered.

## 10. Error semantics

Expected cases:

- setup incomplete / no active Hero: Start unavailable, no mutation;
- invalid Quest type/title/goal: safe validation message, no prepared entry or mutation;
- stale/evicted/malformed confirmation handle: fail closed with safe stale-confirmation page;
- missing/invalid session: existing `401` boundary;
- hostile Host: existing `400` boundary;
- cross-origin/missing antiforgery: framework/security boundary rejects before mutation;
- oversized POST: `413`;
- unsupported form content type: `415`;
- open Quest conflict (`HP133`): safe conflict UI, no duplicate state;
- idempotency mismatch (`HP135`): fail closed and surface a generic retry/conflict message, never silently regenerate/rebind the prepared request;
- referenced Hero no longer valid: safe conflict, no retargeting.

No fallback mutation path bypasses confirmation.

## 11. Files expected to change

Likely production areas:

```text
src/HeroPassport.Application/Runtime/HeroPassportApplication.cs
src/HeroPassport.Application/Runtime/StartQuestModels.cs
src/HeroPassport.Web/Program.cs
src/HeroPassport.Web/Components/Pages/Home.razor
src/HeroPassport.Web/Components/Pages/StartQuest.razor
src/HeroPassport.Web/Components/Pages/ConfirmStartQuest.razor
src/HeroPassport.Web/Services/HeroPassportStartQuestService.cs
src/HeroPassport.Web/Services/PendingStartQuestStore.cs
src/HeroPassport.Web/Security/MutationRequestBoundaryMiddleware.cs
src/HeroPassport.Web/wwwroot/app.css
```

Expected tests:

```text
tests/HeroPassport.Application.Tests/StartQuestBehaviorTests.cs (or focused preparation tests)
tests/HeroPassport.Web.Tests/*StartQuest*Tests.cs
existing LocalWebSecurity/WebProcess regression suites
architecture tests if the new route/form pattern needs a guard
```

Documentation changes should be narrow and evidence-backed after behavior is green.

## 12. TDD and acceptance

Implementation starts with RED tests before production behavior.

Required evidence:

1. unauthenticated Start and confirm routes fail through 0.2-B;
2. Start page GET causes no game-state write;
3. prepare POST causes no Quest mutation;
4. prepare uses Application normalization, not duplicated Web rules;
5. confirmation displays the exact stored normalized Hero/Project/type/title/goal;
6. invalid input produces no prepared mutation/game write;
7. multipart/oversized/malformed form abuse fails boundedly before mutation;
8. missing antiforgery/cross-site POST fails before mutation;
9. explicit confirm creates exactly one Quest;
10. active-Hero preference changes after prepare do not retarget the stored HeroId;
11. same prepared request ID retains existing Start replay/idempotency semantics;
12. competing open Quest preserves `HP133` semantics;
13. stale/evicted/tampered confirmation handles fail closed;
14. title/goal are absent from ordinary diagnostics/error bodies/redirect targets outside intended authenticated page content;
15. dashboard reflects the new open Quest after success;
16. CLI/MCP/Application behavior remains unchanged;
17. full CI and packaged qualification remain green on exact PR head.

## 13. Non-goals

0.2-C does not add:

- Finish Quest Web mutation;
- result/summary/skills/attestations UI;
- Quest history;
- Skill detail/progression pages;
- Rank/Traits/Titles detail pages;
- Hero create/activate/archive/restore/delete management;
- settings UI;
- general REST/minimal API surface;
- Blazor Interactive Server/WebAssembly;
- Identity/OAuth/accounts;
- public/LAN/reverse-proxy/local-HTTPS hosting;
- Streamable HTTP MCP;
- new game rules;
- broad visual redesign.

## 14. Official platform baseline

Implementation must be checked against current official ASP.NET Core 10/.NET 10 documentation, especially:

- Blazor forms binding and static SSR form names / `[SupplyParameterFromForm]`: https://learn.microsoft.com/aspnet/core/blazor/forms/binding?view=aspnetcore-10.0
- Blazor authentication/security and SSR CSRF behavior: https://learn.microsoft.com/aspnet/core/blazor/security/?view=aspnetcore-10.0
- antiforgery: https://learn.microsoft.com/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0
- static SSR threat mitigation: https://learn.microsoft.com/aspnet/core/blazor/security/static-server-side-rendering?view=aspnetcore-10.0
- `IHttpMaxRequestBodySizeFeature`: https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.features.ihttpmaxrequestbodysizefeature.maxrequestbodysize?view=aspnetcore-10.0
- `FormOptions`: https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.features.formoptions?view=aspnetcore-10.0

Current official guidance confirms that static SSR forms require form names, dedicated form DTOs are the correct overposting defense for `[SupplyParameterFromForm]`, cross-origin SSR form posts are rejected by the ASP.NET Core CSRF pipeline, form-mapping defaults are much broader than this product needs, and request body limits must be set before body reading.

## 15. Exit condition

0.2-C is complete only when the Start Quest confirmation pattern is proven on the exact PR head with RED→GREEN evidence, repository review feedback is resolved, required CI is green, and the focused PR is merged without expanding scope into the later 0.2 slices.
