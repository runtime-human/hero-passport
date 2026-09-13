# Hero Passport 0.2-B — Local Web Security Boundary Design

**Status:** Proposed for implementation after review  
**Issue:** #38  
**Baseline:** `main@f074ab7d0733f2fa590d95663415066939d4745c`  
**Branch:** `feat/0.2-b-web-security-boundary`  
**Date:** 2026-09-13

## 1. Goal

0.2-B establishes an explicit authorization and request-origin boundary around the local `HeroPassport.Web` process before any browser mutation surface exists.

The slice adds:

- a process-scoped one-time bootstrap capability;
- a process-scoped browser session bearer cookie;
- strict Host filtering;
- explicit antiforgery validation for the bootstrap security transition;
- fail-closed authorization for the existing read-only dashboard;
- a bounded browser-launch seam for production and headless tests.

It does **not** add Start/Finish Quest, Hero/settings mutations, public authentication, OAuth, accounts, local HTTPS, reverse-proxy support, a general REST API, Interactive Server, WebAssembly, cloud/sync, or new game rules.

## 2. Existing boundary

0.2-A established:

```text
Browser
  -> HeroPassport.Web
     - ASP.NET Core / Blazor static SSR
     - code-defined IPv4 loopback listener
     - read-only dashboard composition
  -> HeroPassport.Application read use cases
  -> HeroPassport.Infrastructure
  -> same-host SQLite
```

The current Kestrel endpoint is programmatically bound to `IPAddress.Loopback` with port `0`, so URL configuration cannot widen the listener. That remains unchanged.

0.2-B adds an authorization envelope around the Web adapter only:

```text
Browser
  -> canonical origin http://127.0.0.1:<dynamic-port>
  -> Host filter
  -> local-session authorization
       | bootstrap exception: /__hero/bootstrap + claim
       v
     existing static SSR dashboard
  -> Application read use cases
  -> same SQLite authority
```

Browser/session concerns stay out of Domain and Application.

## 3. Official platform semantics

The design is based on current official .NET 10 / ASP.NET Core 10 documentation.

### Kestrel and Host filtering

Kestrel binds endpoints but does not validate arbitrary `Host` headers. ASP.NET Core Host Filtering is the intended middleware boundary when the application receives `Host` directly.

0.2-B therefore does not treat loopback binding as sufficient DNS-rebinding protection.

Official references:

- https://learn.microsoft.com/aspnet/core/fundamentals/servers/kestrel?view=aspnetcore-10.0
- https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.hostfiltering.hostfilteringoptions?view=aspnetcore-10.0
- https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.hostfiltering.hostfilteringoptions.allowedhosts?view=aspnetcore-10.0

### Antiforgery / Blazor static SSR

`AddRazorComponents` provides antiforgery services and the application already calls `UseAntiforgery()`. ASP.NET Core 10 also has automatic Fetch Metadata / Origin based CSRF protection for unsafe requests. Token-based validation remains authoritative where explicitly used.

The bootstrap claim is a security transition, so it must explicitly validate an antiforgery token rather than disabling antiforgery for convenience.

Official references:

- https://learn.microsoft.com/aspnet/core/blazor/security/?view=aspnetcore-10.0
- https://learn.microsoft.com/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0
- https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.antiforgery.iantiforgery.validaterequestasync?view=aspnetcore-10.0

### Cryptographic primitives

Process capabilities use the platform cryptographic RNG. Secret equality uses fixed-time byte comparison.

Official references:

- https://learn.microsoft.com/dotnet/api/system.security.cryptography.randomnumbergenerator.getbytes?view=net-10.0
- https://learn.microsoft.com/dotnet/api/system.security.cryptography.cryptographicoperations.fixedtimeequals?view=net-10.0

## 4. Threat model

Treat browser/network input as untrusted even though the listener is loopback-only.

In scope:

- an arbitrary Internet page attempting requests to localhost;
- DNS rebinding or a forged `Host` header;
- another local process that discovers the listening port but does not possess the launch capability;
- guessed, malformed, replayed, or stale bootstrap material;
- stale/malformed browser session cookie;
- a browser session from a previous Hero Passport Web process;
- cross-origin unsafe requests;
- direct access to product routes before bootstrap;
- secret leakage through request URLs, redirects, HTML, normal logs, error bodies, SQLite, or repository state;
- environment/config attempts to widen the listener or Host allowlist.

Out of scope for this MVP boundary:

- an attacker with same-user debugger/process-memory access;
- malware that can inspect the browser profile/cookie database;
- an attacker that can modify the Hero Passport executable or its launch command;
- OS account compromise;
- HTTPS transport confidentiality on loopback.

The product must not claim stronger isolation than this design provides.

## 5. Canonical local origin

0.2-B uses one canonical browser origin:

```text
http://127.0.0.1:<dynamic-port>
```

The current listener is IPv4 loopback, so 0.2-B does not add IPv6 or hostname ambiguity merely for convenience.

`localhost` is not a required supported authority in this slice. `[::1]` is not allowed until the application actually binds IPv6 loopback and qualifies it.

Benefits:

- Host allowlist has one exact value: `127.0.0.1`;
- browser launch is deterministic;
- no dependency on hostname resolution order;
- no accidental `localhost -> ::1` mismatch with an IPv4-only listener;
- tests exercise the same authority as production.

The port remains dynamic and is not part of the Host-filter allowlist value.

## 6. Process secret authority

`HeroPassport.Web` owns one process-local security authority for the lifetime of the Web process.

Conceptually:

```text
LocalWebSessionAuthority
  bootstrapCapability: 32 random bytes, one-time
  sessionToken:         32 random bytes, process-lifetime
  bootstrapState:       available | consumed
```

Production generation uses `RandomNumberGenerator.GetBytes(32)` independently for each value.

The values are not derived from:

- project identity/fingerprint;
- database salt;
- PID;
- port;
- time;
- machine/user name;
- Hero/Project IDs;
- one another.

They are never persisted to SQLite, configuration files, repository files, or normal application logs.

### Comparison

Incoming capability/session values are decoded to fixed-size bytes and compared with `CryptographicOperations.FixedTimeEquals`.

Malformed encoding or wrong length fails before content comparison. Different-length rejection is acceptable because token length is not secret; token contents remain protected from value-dependent timing comparison.

### One-time bootstrap consumption

Bootstrap claim is atomic:

```text
available + correct capability -> consume -> issue session
consumed + any capability       -> fail
available + wrong capability    -> remain available -> fail
```

A wrong guess must not consume the legitimate bootstrap capability and lock out the user.

Concurrent valid claims must produce exactly one winner.

## 7. Bootstrap transport

The bootstrap capability must not appear in an HTTP request target.

Launch URI:

```text
http://127.0.0.1:<port>/__hero/bootstrap#<base64url-capability>
```

The URI fragment is processed by the browser and is not sent as part of the initial HTTP request.

### Bootstrap page

`GET /__hero/bootstrap` is the only unauthenticated rendered page. It contains:

- no Hero/Project/Quest data;
- no filesystem/database metadata;
- a server-generated antiforgery token;
- a hidden form posting to `/__hero/bootstrap/claim`;
- minimal inline JavaScript only.

The script:

1. reads the fragment;
2. immediately calls `history.replaceState` to remove the fragment from the current history entry;
3. places the capability into the hidden form field in memory;
4. submits the same-origin POST;
5. never writes the capability into DOM text, storage, console, query string, path, analytics, or logs.

No external script/CDN is introduced.

Response headers for the bootstrap page should include at least:

```text
Cache-Control: no-store
Referrer-Policy: no-referrer
X-Content-Type-Options: nosniff
```

The existing/default frame protection supplied by ASP.NET Core antiforgery behavior is retained; the implementation may make equivalent explicit headers if tests show a gap.

## 8. Bootstrap claim endpoint

Internal endpoint:

```text
POST /__hero/bootstrap/claim
Content-Type: application/x-www-form-urlencoded
```

This is an internal security bootstrap endpoint, not a general product API.

The endpoint must:

1. validate the request antiforgery token through `IAntiforgery.ValidateRequestAsync`;
2. reject clearly cross-site browser requests under the framework CSRF policy;
3. read exactly one bounded capability field;
4. decode and fixed-time compare the capability;
5. atomically consume it on success;
6. issue the process session cookie;
7. return `303 See Other` to `/`.

Failure behavior:

```text
invalid/missing antiforgery -> 400
wrong/malformed/replayed capability -> 403
```

Failure bodies are empty or bounded generic text and never reflect supplied material.

The endpoint does not accept JSON, arbitrary fields, redirects, return URLs, paths, commands, project roots, or any other privileged input.

### Architecture guard exception

0.2-A currently guards against accidental Minimal API product surface. 0.2-B may relax that guard only for this exact internal security endpoint.

The architecture test should continue to reject arbitrary `MapGet/MapPost/MapGroup/...` additions outside the explicitly recognized bootstrap claim composition.

The endpoint must not become precedent for a REST façade.

## 9. Session cookie

Successful bootstrap establishes one process-local bearer session.

Cookie policy:

```text
Name:     .HeroPassport.LocalSession
HttpOnly: true
SameSite: Strict
Path:     /
Expires:  absent
Max-Age:  absent
Secure:   false for the current HTTP-loopback profile
```

No persistent-cookie lifetime is introduced. Browser session persistence does not extend authorization across a Hero Passport Web process restart because the server token changes every process.

`Secure=false` is deliberate for the current plain-HTTP loopback profile. 0.2-B must not set `Secure=true` and then rely on browser-specific behavior to make HTTP work. A future local-HTTPS slice can strengthen this independently.

The cookie value is the encoded random session bearer. It is not an ASP.NET Core Identity ticket and carries no user/project claims.

## 10. Product-route authorization

A Web middleware/component validates the session cookie before product content is served.

Public exceptions are exactly:

```text
GET  /__hero/bootstrap
POST /__hero/bootstrap/claim
```

Everything else, including `/`, dashboard routes, and product static assets, requires the current session token.

Invalid or absent session:

```text
401 Unauthorized
Cache-Control: no-store
no product body
```

The middleware does not redirect unauthenticated requests to bootstrap because it does not possess or expose the launch capability. Only the process/browser launcher can construct the capability-bearing fragment URI.

Authorization occurs before the dashboard service executes so an unauthorized GET cannot trigger project/application reads merely to render an error page.

## 11. Host filtering

Host filtering is explicit and code-owned, not weakened by environment configuration.

Target policy:

```text
AllowedHosts = ["127.0.0.1"]
AllowEmptyHosts = false
IncludeFailureMessage = false
```

A wildcard is forbidden.

The final implementation should prefer explicit `HostFilteringOptions`/middleware configuration over a mutable configuration-only `AllowedHosts` value so environment/appsettings cannot silently replace the security invariant.

Host filtering runs before bootstrap/session authorization.

Expected behavior:

```text
Host: 127.0.0.1:<actual-port> -> eligible for further processing
Host: localhost:<port>        -> 400
Host: attacker.example        -> 400
Host: 0.0.0.0:<port>          -> 400
empty Host where protocol permits -> 400
```

Loopback Kestrel binding remains independently enforced.

## 12. CSRF/origin policy

0.2-B preserves both layers available in ASP.NET Core 10:

- automatic Fetch Metadata / Origin CSRF protection;
- token-based antiforgery through existing `UseAntiforgery()`.

The bootstrap claim additionally calls `IAntiforgery.ValidateRequestAsync` explicitly.

No permissive CORS policy is added. No endpoint uses `DisableAntiforgery` or `[IgnoreAntiforgeryToken]` for the bootstrap transition.

Future mutation endpoints must separately require the established local session **and** appropriate antiforgery validation; this spec does not implement those mutations.

## 13. Browser launch and headless mode

The Web application must know the actual dynamic port before constructing the launch URI.

The production startup flow may therefore move from a single `RunAsync()` call to the equivalent lifecycle:

```text
Build
StartAsync
resolve actual bound 127.0.0.1 address
launch browser with fragment capability
WaitForShutdownAsync
```

This is a hosting/composition change only; Application/Infrastructure lifecycle semantics stay unchanged.

### Production default

Default behavior opens the system browser once after successful server start.

Failure to launch the browser is non-fatal to the Web server and produces a bounded secret-free diagnostic. It must not print the raw capability as a fallback.

### Headless/tests

`--no-open-browser` suppresses browser launch.

For child-process tests, deterministic bootstrap/session secret injection is permitted only under an explicit test environment gate. Proposed rule:

```text
ASPNETCORE_ENVIRONMENT=Testing
+ HERO_PASSPORT_WEB_TEST_BOOTSTRAP=<test value>
+ HERO_PASSPORT_WEB_TEST_SESSION=<test value>
```

If either test-secret variable is present outside `Testing`, startup fails closed rather than accepting predictable production secrets.

Tests must not scrape ordinary stdout/stderr for secrets.

No production command-line option prints or exports the generated capability.

## 14. Logging and privacy

Security material is deny-listed from logging and rendering.

Never log:

- bootstrap fragment/capability;
- submitted capability form value;
- session-cookie value;
- full `Cookie` header;
- antiforgery token;
- a hash/fingerprint of those secrets intended to correlate them across requests.

Normal diagnostics may log bounded facts such as:

```text
bootstrap claim accepted/rejected
session authorized/unauthorized
host rejected
browser launch success/failure
```

without secret values.

The bootstrap POST uses form data only; HTTP request-body logging must not be enabled for it.

Existing Hero Passport privacy rules remain in force: no raw source/diff/log/prompt/full-path/remote-url exposure.

## 15. Failure semantics

The boundary fails closed.

| Condition | Result |
| --- | --- |
| unsupported Host | `400` before product processing |
| direct `/` without session | `401`, no dashboard data |
| malformed session cookie | `401` |
| stale previous-process cookie | `401` |
| bootstrap GET without fragment | bootstrap shell renders but cannot authorize |
| missing/invalid antiforgery on claim | `400` |
| wrong bootstrap capability | `403`, capability remains usable by legitimate browser |
| replay after successful claim | `403` |
| browser launch failure | server continues; secret is not logged |
| test secret override outside `Testing` | startup failure |

No failure path silently disables security to improve UX.

## 16. Concurrency and restart semantics

The bootstrap capability has an atomic one-time transition. Parallel valid claims produce one session issuance; all later claims fail.

The process session bearer remains valid for concurrent browser requests in that process.

Every Web process restart generates a new bootstrap capability and a new session bearer. A cookie from process A is invalid against process B even if the same dynamic port happens to be reused.

No session state is stored in SQLite, so database backup/restore cannot resurrect browser authorization.

## 17. Test design

TDD remains mandatory: capture RED against the merged 0.2-A baseline before production security behavior.

### Pure/unit tests

Cover the process secret authority independently:

- generation shape/length;
- correct capability accepted once;
- wrong capability does not consume;
- replay rejected;
- concurrent valid claim produces one winner;
- session validation accepts only exact current token;
- stale/wrong/malformed tokens rejected.

Do not assert statistical randomness. Production code review/API choice establishes cryptographic RNG; tests assert boundaries and injection behavior.

### Real process/HTTP tests

Launch `HeroPassport.Web` with isolated `HERO_PASSPORT_HOME`, temporary project, `ASPNETCORE_ENVIRONMENT=Testing`, deterministic test secrets, and `--no-open-browser`.

Prove:

1. actual listener remains IPv4 loopback;
2. hostile `ASPNETCORE_URLS` still cannot widen it;
3. direct GET `/` returns `401` and no local Hero/project data;
4. `GET /__hero/bootstrap` contains no project data and sets no authorization session;
5. valid antiforgery + capability POST yields `303` and the required cookie attributes;
6. replay yields `403`;
7. wrong capability yields `403` without consuming the real capability;
8. authenticated GET `/` renders the same Application-backed dashboard as 0.2-A;
9. invalid/stale cookie yields `401`;
10. a session cookie from process A fails against process B;
11. forged Host values yield `400`;
12. cross-site/missing antiforgery claim is rejected;
13. secrets are absent from captured stdout/stderr, response bodies, redirects, rendered HTML, and SQLite;
14. bootstrap/static/product route authorization order prevents dashboard service reads before authorization where observable without invasive instrumentation.

### Architecture tests

Guard:

- Domain/Application/Infrastructure do not depend on Web;
- no Identity/OAuth/authentication provider packages are introduced;
- no Interactive Server/WebAssembly render mode is introduced;
- no general product Minimal API surface appears;
- the only explicitly accepted security POST is the bootstrap claim;
- browser/session types remain in the Web adapter.

### Repository qualification

Before merge:

- Release build;
- Architecture, Domain, Application, Infrastructure, Contract, App, Web, Agent Skill suites;
- packaged App/Codex E2E;
- release-platform where triggered;
- exact-head required GitHub `build-test` GREEN;
- final diff/privacy review.

## 18. Scope boundaries

0.2-B is complete when the existing dashboard is accessible only through the process capability/session boundary and all acceptance evidence is GREEN.

Explicitly deferred:

- Start/Finish Quest Web mutation/confirmation semantics;
- Hero/settings management;
- richer history/project views;
- local HTTPS;
- IPv6/`localhost` authority support;
- remote/LAN/public hosting;
- user accounts/OAuth/Identity;
- Streamable HTTP MCP;
- Interactive Server/WebAssembly;
- packaging/desktop tray integration beyond the minimal browser launcher;
- persistent Web sessions.

The next mutation issue must depend on #38 rather than weakening or reimplementing this boundary.

## 19. Acceptance summary

The design is accepted only if implementation proves all of the following without broadening scope:

```text
loopback-only listener remains enforced
canonical Host = 127.0.0.1 only
one-time 256-bit bootstrap capability
capability never enters HTTP request URL
atomic bootstrap consumption
process-only 256-bit session bearer
HttpOnly + SameSite=Strict session cookie
no persistent session expiry/Max-Age
unauthorized product routes fail closed
hostile Host rejected
bootstrap POST antiforgery validated
no permissive CORS
no secret logging/persistence/reflection
restart invalidates old session
no Web product mutations
no Identity/OAuth/general REST surface
full repository CI remains green
```
