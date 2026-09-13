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

### A. Selected: process-local prepared confirmation store

The first POST validates and normalizes input, captures the active Hero and current Project presentation, generates the durable Start request ID, and stores a short-lived prepared command in Web process memory under an opaque random handle. A confirmation GET renders that prepared state. The confirm POST carries only the opaque handle and antiforgery/form metadata; the actual Quest data is loaded from process memory and committed through Application.

Advantages:

- confirmation shows the exact state that will be committed;
- title/goal are not copied into redirect URLs or hidden confirm-form fields;
- Hero ownership cannot silently change between prepare and commit;
- no new persistence/schema is required;
- no new cryptographic payload format or Data Protection dependency is required;
- stale state naturally dies with the Web process.

### B. Re-post all fields on the confirmation POST

This would be simpler mechanically, but the second POST becomes a fresh untrusted copy of the user's input. The confirmation screen could show one value while a modified form submits another unless all values are compared/revalidated. It also repeats sensitive Quest metadata in hidden fields. Rejected.

### C. Signed/encrypted confirmation payload

A Data Protection/HMAC payload could make confirmation state stateless, but 0.2-C is a local single-process flow. Introducing a second cryptographic state protocol adds complexity without solving a demonstrated product need. Rejected for this slice.

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

The prepare form name is exactly `StartQuestPrepare`. It is bound with `[SupplyParameterFromForm]`. Domain/Application models are never directly form-bound.

The page loads runtime context through Application. Start is available only when setup is complete and an active Hero is present. If that active Hero already has an open Quest in the current Project, the page shows a bounded product conflict rather than offering a misleading successful path.

The UI may use HTML `maxlength`, select options and required markers for ergonomics, but these are not authoritative validation. Application validation remains authoritative.

### 5.2 Start Quest Web service

Add a Web-owned service responsible for orchestration only:

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
safe committed Quest presentation when committed
```

The handle is generated from 128 bits of cryptographic randomness and encoded as unpadded base64url text. It is an opaque lookup handle, not an authentication substitute.

The store is bounded:

- maximum 8 live entries total per Web process;
- 10-minute lifetime from preparation; commit does not extend that lifetime;
- expired entries are removed before lookup/insert;
- if capacity is full, evict the oldest `pending` or `committed` entry;
- never evict an entry currently in `committing` state;
- if all 8 live entries are `committing`, a new prepare attempt fails safely with `429 Too Many Requests` and no game mutation.

Eviction only invalidates an old confirmation page; it never mutates game state.

The store is concurrency-safe. Exactly one confirm operation can transition an entry from `pending` to `committing`.

State behavior is exact:

- `pending -> committing` atomically before calling Application;
- success/replay from Application -> `committed`, retaining the same safe result until the original 10-minute expiry;
- duplicate confirmation of a `committed` entry returns the same safe success redirect/result without calling Application again;
- unexpected failure where durable mutation outcome is unknown -> return the same entry to `pending` with the same `StartRequestId` so retry remains idempotent;
- terminal known product conflict such as `HP133`/`HP135` removes the pending entry and returns a safe conflict response; it never creates a replacement request ID automatically.

Because the existing Application Start contract is idempotent, even an unexpected failure after a durable commit is safe: retry uses the same `StartRequestId`, and Application/store replay semantics return the original Start instead of creating a second Quest.

## 6. Confirmation flow

### Prepare

`POST /quests/start` is the static-SSR `StartQuestPrepare` form submission.

Flow:

1. existing 0.2-B session middleware authenticates the browser session;
2. mutation request-bound middleware rejects unsupported/oversized form requests before expensive parsing;
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

The confirm form is named exactly `StartQuestConfirm` and contains only the opaque handle plus normal framework antiforgery/form metadata.

Malformed handle syntax returns a safe `400 Bad Request`. A syntactically valid but unknown, expired or evicted handle returns a safe `410 Gone`. Neither response reflects the handle or Quest metadata.

### Commit

`POST /quests/start/confirm/{handle}` is the static-SSR `StartQuestConfirm` submission and requires session + origin + antiforgery.

Flow:

1. validate bounded handle syntax;
2. atomically claim the prepared entry for commit;
3. use the stored `HeroId`, stored normalized Quest values and stored `StartRequestId`;
4. call `HeroPassportApplication.StartQuestAsync(...)` with the Web process's existing `ProjectBindingContext`;
5. on success or Application replay, mark the prepared entry `committed` and redirect to `/`;
6. dashboard reads current state normally and displays the newly opened Quest.

The active-Hero preference is not re-read to replace the prepared `HeroId`. A preference switch in another surface therefore cannot silently retarget the Quest.

If the prepared Hero was archived/deleted or a competing open Quest now exists, Application/store rules remain authoritative and Web presents a safe conflict without inventing fallback ownership.

## 7. Request bounds and form hardening

All Start/confirm mutation POSTs accept only `application/x-www-form-urlencoded`.

Before antiforgery/form parsing, a focused Web mutation request-bound middleware applies to the Start/confirm POST paths:

- maximum request body: 8192 bytes;
- unsupported content type -> `415 Unsupported Media Type`;
- known `Content-Length > 8192` -> `413 Payload Too Large`;
- set `IHttpMaxRequestBodySizeFeature.MaxRequestBodySize = 8192` before the body is read.

The existing bootstrap claim keeps its stricter 1024-byte boundary.

`AddRazorComponents` form mapping is tightened for the entire small static-SSR Web adapter:

```text
MaxFormMappingCollectionSize = 16
MaxFormMappingRecursionDepth = 4
MaxFormMappingErrorCount = 16
MaxFormMappingKeySize = 128
```

The mutation request boundary installs these exact form-reader limits before parsing:

```text
ValueCountLimit = 16
KeyLengthLimit = 128
ValueLengthLimit = 2048
BufferBody = false
```

Multipart is rejected before form parsing, so multipart limits are not part of the supported Start flow.

These limits intentionally remain above the product maximums (`title` <= 120 Unicode scalar values and `goal` <= 500 Unicode scalar values) plus form-name/antiforgery overhead while staying far below ASP.NET Core defaults. Real-process tests must prove that valid maximum-sized product input still succeeds and oversized/entry-flood/multipart requests fail before mutation.

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

The confirmation redirect contains only the opaque handle. The handle may appear in the local request path but is explicitly non-authenticating and contains no Quest/user data.

Safe error presentation uses bounded product messages keyed from known Application error codes. Raw exception text, SQL, full paths, Project fingerprint, internal IDs, request hashes and browser secrets are not rendered.

## 10. Error semantics

Expected cases:

- setup incomplete / no active Hero: Start unavailable, no mutation;
- invalid Quest type/title/goal: safe validation message, no prepared entry or mutation;
- malformed confirmation handle: `400`, no mutation;
- unknown/expired/evicted confirmation handle: `410`, no mutation;
- prepared-store saturation with all 8 entries actively committing: `429`, no mutation;
- missing/invalid session: existing `401` boundary;
- hostile Host: existing `400` boundary;
- cross-origin/missing antiforgery: framework/security boundary rejects before mutation;
- oversized POST: `413`;
- unsupported form content type: `415`;
- open Quest conflict (`HP133`): safe `409 Conflict` product response, no duplicate state;
- idempotency mismatch (`HP135`): safe `409 Conflict`, never silently regenerate/rebind the prepared request;
- referenced Hero no longer valid: safe conflict, no retargeting;
- unexpected server failure: generic safe error; prepared entry returns to `pending` with the same request ID when retry is possible.

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
tests/HeroPassport.Application.Tests/StartQuestPreparationTests.cs
tests/HeroPassport.Web.Tests/StartQuestWebAcceptanceTests.cs
existing LocalWebSecurity/WebProcess regression suites
architecture tests for Web persistence/API/interactivity boundaries
```

Documentation changes after behavior is green remain narrow and evidence-backed.

## 12. TDD and acceptance

Implementation starts with RED tests before production behavior.

Required evidence:

1. unauthenticated Start and confirm routes fail through 0.2-B;
2. Start page GET causes no game-state write;
3. prepare POST causes no Quest mutation;
4. prepare uses Application normalization, not duplicated Web rules;
5. confirmation displays the exact stored normalized Hero/Project/type/title/goal;
6. invalid input produces no prepared mutation/game write;
7. maximum valid product input fits within the chosen request/form limits;
8. multipart/oversized/excess-entry/malformed form abuse fails boundedly before mutation;
9. missing antiforgery/cross-site POST fails before mutation;
10. explicit confirm creates exactly one Quest;
11. active-Hero preference changes after prepare do not retarget the stored HeroId;
12. duplicate confirmation returns the same successful outcome and does not create/call a second fresh mutation;
13. same prepared request ID retains existing Start replay/idempotency semantics after an unknown-outcome retry;
14. competing open Quest preserves `HP133` semantics;
15. malformed/stale/evicted/tampered confirmation handles fail closed with the specified status behavior;
16. title/goal are absent from ordinary diagnostics/error bodies/redirect targets outside intended authenticated page content;
17. dashboard reflects the new open Quest after success;
18. CLI/MCP/Application behavior remains unchanged;
19. full CI and packaged qualification remain green on exact PR head.

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

Current official guidance confirms that static SSR forms require unique form names, dedicated form DTOs are the correct overposting defense for `[SupplyParameterFromForm]`, cross-origin SSR form posts are rejected by the ASP.NET Core CSRF pipeline, form-mapping defaults are much broader than this product needs, and request body limits must be set before body reading.

## 15. Exit condition

0.2-C is complete only when the Start Quest confirmation pattern is proven on the exact PR head with RED→GREEN evidence, repository review feedback is resolved, required CI is green, and the focused PR is merged without expanding scope into later 0.2 slices.
