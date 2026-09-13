# Hero Passport 0.2-B Web Security Boundary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Put the existing local static-SSR dashboard behind a one-time process bootstrap capability, process-local browser session, exact Host allowlist, and antiforgery/origin boundary before any Web mutation is introduced.

**Architecture:** Keep all browser-security state inside `HeroPassport.Web`. A 256-bit process authority owns independent bootstrap/session secrets; an unauthenticated static-SSR bootstrap shell transfers the fragment capability through one antiforgery-protected internal POST, which sets a process-only bearer cookie. Host filtering and a narrow session middleware fail closed before any dashboard/Application reads, while the existing IPv4 loopback Kestrel endpoint remains authoritative.

**Tech Stack:** C# 14 / .NET 10, ASP.NET Core 10 Kestrel + HostFiltering + Antiforgery, Blazor Web App static SSR, xUnit v3, existing SQLite/Application stack.

**Spec:** `docs/superpowers/specs/2026-09-13-hero-passport-0.2-b-web-security-design.md`

## Global Constraints

- Baseline is `main@f074ab7d0733f2fa590d95663415066939d4745c`; implementation branch is `feat/0.2-b-web-security-boundary`.
- Keep the code-defined listener `Listen(IPAddress.Loopback, 0)`; no wildcard/LAN/public/IPv6 listener in this slice.
- Canonical browser authority is exactly `http://127.0.0.1:<dynamic-port>`; Host allowlist contains only `127.0.0.1` and excludes ports.
- Bootstrap and session secrets are independent 32-byte values from `RandomNumberGenerator.GetBytes(32)` in production.
- The bootstrap capability is one-time; a wrong guess does not consume it; concurrent correct claims have exactly one winner.
- Secret comparison uses decoded fixed-size bytes plus `CryptographicOperations.FixedTimeEquals`.
- Bootstrap capability travels in a browser URL fragment, never query/path/request target; the bootstrap shell removes the fragment from history before POST.
- Session cookie name is `.HeroPassport.LocalSession`; `HttpOnly=true`, `SameSite=Strict`, `Path=/`, no `Expires`, no `Max-Age`, and `Secure=false` for the current plain-HTTP loopback profile.
- `GET /__hero/bootstrap` and `POST /__hero/bootstrap/claim` are the only unauthenticated exceptions; all product routes/assets fail `401` before dashboard reads without a valid session.
- Host filtering fails invalid hosts with `400` and is code-owned; environment/appsettings cannot widen the allowlist.
- Keep `UseAntiforgery()`; the bootstrap claim also explicitly validates the antiforgery token and rejects cross-site `Origin`/Fetch Metadata signals.
- No permissive CORS, Identity/OAuth/accounts, reverse-proxy support, local HTTPS, Interactive Server, WebAssembly, Streamable HTTP MCP, REST product façade, or Web product mutations.
- Test secret injection is accepted only under `ASPNETCORE_ENVIRONMENT=Testing`; production fails closed if test-secret variables or `--no-open-browser` are supplied.
- Production browser launch failure is fatal/fail-closed and must never print the bootstrap capability.
- No bootstrap/session/antiforgery material may appear in normal logs, rendered product HTML, redirect targets, SQLite, source state, or error bodies.
- Preserve the existing Web project dependency boundary: Application + Infrastructure only; no new package references unless official shared-framework APIs prove insufficient.
- Use current official ASP.NET Core 10/.NET 10 semantics rechecked during design: HostFiltering, Blazor/ASP.NET antiforgery, `RandomNumberGenerator`, `CryptographicOperations.FixedTimeEquals`.

---

## File Structure

Create focused Web-only units rather than growing `Program.cs` into the security implementation:

- `src/HeroPassport.Web/Security/LocalWebSessionAuthority.cs` — process secret generation, test-only secret gate, bootstrap atomic consumption, session validation.
- `src/HeroPassport.Web/Security/LocalWebOriginPolicy.cs` — exact same-origin check for unsafe bootstrap claim using `Sec-Fetch-Site`/`Origin` when present.
- `src/HeroPassport.Web/Security/LocalWebSessionMiddleware.cs` — exact public-path exceptions plus fail-closed cookie validation before product endpoints.
- `src/HeroPassport.Web/Security/BootstrapEndpoint.cs` — the single internal `POST /__hero/bootstrap/claim` composition and response/cookie semantics.
- `src/HeroPassport.Web/Services/SystemBrowserLauncher.cs` — one responsibility: open the capability-bearing local URI; no security-state ownership.
- `src/HeroPassport.Web/Components/Pages/Bootstrap.razor` — state-free bootstrap shell with `<AntiforgeryToken />` and minimal inline fragment-to-form script.
- `src/HeroPassport.Web/Properties/AssemblyInfo.cs` — test visibility for Web-internal security primitives only.
- `tests/HeroPassport.Web.Tests/LocalWebSessionAuthorityTests.cs` — pure authority/concurrency/restart/test-gate tests.
- `tests/HeroPassport.Web.Tests/WebProcessTests.cs` — real-process Host/session/bootstrap/CSRF/privacy qualification and adaptation of 0.2-A dashboard tests.
- `tests/HeroPassport.Architecture.Tests/ProjectDependencyTests.cs` — keep the no-general-HTTP-surface rule while admitting exactly the internal bootstrap POST.
- `src/HeroPassport.Web/Program.cs` — composition/lifecycle only: options, middleware ordering, endpoint mapping, start/launch/wait.
- `docs/ARCHITECTURE.md`, `docs/DEPLOYMENT-MODES.md`, `docs/TESTING-QUALITY.md`, `docs/ROADMAP.md` — update repository truth only after exact behavior is GREEN.

---

### Task 1: Capture the 0.2-B boundary as RED tests

**Files:**
- Create: `tests/HeroPassport.Web.Tests/LocalWebSessionAuthorityTests.cs`
- Modify: `tests/HeroPassport.Web.Tests/WebProcessTests.cs`

**Interfaces:**
- Consumes: current 0.2-A `HeroPassport.Web` process and existing `StartWebAsync`/sandbox helpers.
- Produces: executable expectations for `LocalWebSessionAuthority`, deterministic `Testing` secrets, bootstrap flow, Host rejection, unauthorized `401`, replay/stale-session rejection, and existing-dashboard compatibility.

- [ ] **Step 1: Add pure authority RED tests against the intended API**

Create `LocalWebSessionAuthorityTests.cs` with tests expressed against this exact internal contract:

```csharp
using HeroPassport.Web.Security;
using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class LocalWebSessionAuthorityTests
{
    private const string Bootstrap = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8";
    private const string Session = "ICEiIyQlJicoKSorLC0uLzAxMjM0NTY3ODk6Ozw9Pj8";

    [Fact]
    public void CorrectBootstrapIsConsumedExactlyOnce()
    {
        var authority = LocalWebSessionAuthority.CreateForTesting(Bootstrap, Session);
        Assert.True(authority.TryConsumeBootstrap(Bootstrap));
        Assert.False(authority.TryConsumeBootstrap(Bootstrap));
    }

    [Fact]
    public void WrongBootstrapDoesNotConsumeLegitimateCapability()
    {
        var authority = LocalWebSessionAuthority.CreateForTesting(Bootstrap, Session);
        Assert.False(authority.TryConsumeBootstrap(Session));
        Assert.True(authority.TryConsumeBootstrap(Bootstrap));
    }

    [Fact]
    public async Task ConcurrentCorrectBootstrapHasOneWinner()
    {
        var authority = LocalWebSessionAuthority.CreateForTesting(Bootstrap, Session);
        var results = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => authority.TryConsumeBootstrap(Bootstrap))));
        Assert.Single(results, static result => result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64url!")]
    [InlineData("AA")]
    public void MalformedSessionFailsClosed(string? candidate)
    {
        var authority = LocalWebSessionAuthority.CreateForTesting(Bootstrap, Session);
        Assert.False(authority.IsSessionValid(candidate));
    }

    [Fact]
    public void SessionValidationIsProcessAuthoritySpecific()
    {
        var first = LocalWebSessionAuthority.CreateForTesting(Bootstrap, Session);
        var second = LocalWebSessionAuthority.CreateForTesting(
            "QEFCQ0RFRkdISUpLTE1OT1BRUlNUVVZXWFlaW1xdXl8",
            "YGFiY2RlZmdoaWprbG1ub3BxcnN0dXZ3eHl6e3x9fn8");

        Assert.True(first.IsSessionValid(Session));
        Assert.False(second.IsSessionValid(Session));
    }
}
```

- [ ] **Step 2: Adapt the process harness to declare deterministic Testing secrets and no-browser mode**

Extend `StartWebAsync` so the new security process can be launched deterministically:

```csharp
private const string TestBootstrap = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8";
private const string TestSession = "ICEiIyQlJicoKSorLC0uLzAxMjM0NTY3ODk6Ozw9Pj8";

startInfo.ArgumentList.Add("--no-open-browser");
startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
startInfo.Environment["HERO_PASSPORT_WEB_TEST_BOOTSTRAP"] = TestBootstrap;
startInfo.Environment["HERO_PASSPORT_WEB_TEST_SESSION"] = TestSession;
```

For the one existing static-assets qualification that needs Development behavior, add a narrowly separate `serveStaticAssetsFromSource` test switch rather than weakening the security gate: the process still uses the `Testing` security gate while the test sets the static-web-assets behavior explicitly only if the current build manifest requires it. Do not accept test secrets under `Development`/`Production`.

- [ ] **Step 3: Add process RED tests for authorization, Host, bootstrap, replay, and stale sessions**

Add tests with these exact assertions:

```csharp
[Fact]
public async Task DirectDashboardGetWithoutSessionFailsClosed()
{
    await using var web = await StartWebAsync(...);
    using var response = await web.Client.GetAsync("/", token);
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.DoesNotContain(Path.GetFileName(sandbox.ProjectRoot), await response.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
}

[Fact]
public async Task HostHeaderOutsideCanonicalLoopbackIsRejected()
{
    await using var web = await StartWebAsync(...);
    using var request = new HttpRequestMessage(HttpMethod.Get, "/__hero/bootstrap");
    request.Headers.Host = "attacker.example";
    using var response = await web.Client.SendAsync(request, token);
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
}
```

Add `BootstrapAsync` to:

1. GET `/__hero/bootstrap`;
2. parse the hidden antiforgery `name/value` from HTML;
3. POST form fields `__RequestVerificationToken` + `capability` with redirects disabled;
4. assert `303` + `Location: /`;
5. retain `.HeroPassport.LocalSession` in a `CookieContainer`.

Then add assertions for:

- valid bootstrap then authenticated `/` returns the same 0.2-A dashboard truth;
- wrong capability returns `403` and a later correct claim succeeds;
- successful capability replay returns `403`;
- missing token/cross-site bootstrap POST returns `400`;
- cookie from process A returns `401` against process B;
- session cookie attributes include `HttpOnly`, `SameSite=Strict`, `Path=/`, and exclude `Expires`/`Max-Age`;
- bootstrap/session deterministic values never occur in bootstrap HTML, dashboard HTML, redirect Location, or captured process output;
- `ASPNETCORE_URLS=http://0.0.0.0:0` still produces an IPv4 loopback listener.

Update all existing 0.2-A successful-dashboard/static-asset tests to call `BootstrapAsync` before accessing protected content.

- [ ] **Step 4: Run focused tests and capture expected RED**

Run:

```bash
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj -c Release --no-restore
```

Expected RED before production code:

- compile failure because `HeroPassport.Web.Security.LocalWebSessionAuthority` does not exist, or after a temporary compile shim, behavioral failures showing `/` still returns `200`, hostile Host is not strictly rejected by code-owned policy, and bootstrap routes do not exist.

- [ ] **Step 5: Commit RED evidence**

```bash
git add tests/HeroPassport.Web.Tests
git commit -m "test(web): define 0.2-B local security boundary"
```

Record the RED commit SHA in issue #38/PR evidence later.

---

### Task 2: Implement the process-local secret authority

**Files:**
- Create: `src/HeroPassport.Web/Security/LocalWebSessionAuthority.cs`
- Create: `src/HeroPassport.Web/Properties/AssemblyInfo.cs`
- Test: `tests/HeroPassport.Web.Tests/LocalWebSessionAuthorityTests.cs`

**Interfaces:**
- Consumes: `IHostEnvironment`, process environment variables, `RandomNumberGenerator`, `WebEncoders`, `CryptographicOperations`.
- Produces: `LocalWebSessionAuthority.Create(IHostEnvironment)`, `CreateForTesting(string,string)`, `BootstrapCapability`, `SessionToken`, `TryConsumeBootstrap(string?)`, and `IsSessionValid(string?)`.

- [ ] **Step 1: Add internal test visibility**

Create:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("HeroPassport.Web.Tests")]
```

- [ ] **Step 2: Implement exact-size secret decoding and fixed-time comparison**

`LocalWebSessionAuthority` must store raw 32-byte arrays and expose encoded values only where launch/cookie issuance needs them:

```csharp
internal sealed class LocalWebSessionAuthority
{
    internal const int SecretSize = 32;
    internal const string CookieName = ".HeroPassport.LocalSession";

    private readonly byte[] _bootstrap;
    private readonly byte[] _session;
    private int _bootstrapConsumed;

    private LocalWebSessionAuthority(byte[] bootstrap, byte[] session)
    {
        _bootstrap = bootstrap;
        _session = session;
    }

    internal string BootstrapCapability => WebEncoders.Base64UrlEncode(_bootstrap);
    internal string SessionToken => WebEncoders.Base64UrlEncode(_session);

    internal bool TryConsumeBootstrap(string? candidate)
    {
        if (Volatile.Read(ref _bootstrapConsumed) != 0 || !Matches(candidate, _bootstrap))
        {
            return false;
        }

        return Interlocked.CompareExchange(ref _bootstrapConsumed, 1, 0) == 0;
    }

    internal bool IsSessionValid(string? candidate) => Matches(candidate, _session);

    private static bool Matches(string? encoded, byte[] expected)
    {
        if (string.IsNullOrWhiteSpace(encoded)) return false;
        byte[] decoded;
        try { decoded = WebEncoders.Base64UrlDecode(encoded); }
        catch (FormatException) { return false; }
        return decoded.Length == SecretSize && CryptographicOperations.FixedTimeEquals(decoded, expected);
    }
}
```

- [ ] **Step 3: Add production creation and fail-closed Testing override gate**

Implement:

```csharp
internal static LocalWebSessionAuthority Create(IHostEnvironment environment)
{
    var testBootstrap = Environment.GetEnvironmentVariable("HERO_PASSPORT_WEB_TEST_BOOTSTRAP");
    var testSession = Environment.GetEnvironmentVariable("HERO_PASSPORT_WEB_TEST_SESSION");
    var hasTestOverride = testBootstrap is not null || testSession is not null;

    if (hasTestOverride && !environment.IsEnvironment("Testing"))
        throw new InvalidOperationException("Web test secrets are only accepted in the Testing environment.");

    if (environment.IsEnvironment("Testing"))
    {
        if (testBootstrap is null || testSession is null)
            throw new InvalidOperationException("Testing requires both deterministic Web test secrets.");
        return CreateForTesting(testBootstrap, testSession);
    }

    return new LocalWebSessionAuthority(
        RandomNumberGenerator.GetBytes(SecretSize),
        RandomNumberGenerator.GetBytes(SecretSize));
}
```

`CreateForTesting` must decode and require exactly 32 bytes for both inputs; malformed values throw only during test/process startup, never request handling.

- [ ] **Step 4: Run pure tests to GREEN**

```bash
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj -c Release --no-restore --filter LocalWebSessionAuthorityTests
```

Expected: authority tests GREEN; process boundary tests remain RED.

- [ ] **Step 5: Commit the authority**

```bash
git add src/HeroPassport.Web/Security/LocalWebSessionAuthority.cs src/HeroPassport.Web/Properties/AssemblyInfo.cs tests/HeroPassport.Web.Tests/LocalWebSessionAuthorityTests.cs
git commit -m "feat(web): add process-local session authority"
```

---

### Task 3: Enforce code-owned Host filtering and product-route session authorization

**Files:**
- Create: `src/HeroPassport.Web/Security/LocalWebSessionMiddleware.cs`
- Modify: `src/HeroPassport.Web/Program.cs`
- Modify: `tests/HeroPassport.Web.Tests/WebProcessTests.cs`

**Interfaces:**
- Consumes: `LocalWebSessionAuthority`, `HostFilteringOptions`, request path/method/cookie.
- Produces: exact `127.0.0.1` Host policy and pre-endpoint `401` authorization boundary.

- [ ] **Step 1: Implement the exact public exception/middleware rule**

Create middleware with no redirect behavior:

```csharp
internal sealed class LocalWebSessionMiddleware(
    RequestDelegate next,
    LocalWebSessionAuthority authority)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (IsBootstrapRequest(context.Request))
        {
            await next(context);
            return;
        }

        if (!context.Request.Cookies.TryGetValue(LocalWebSessionAuthority.CookieName, out var token)
            || !authority.IsSessionValid(token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.CacheControl = "no-store";
            return;
        }

        await next(context);
    }

    private static bool IsBootstrapRequest(HttpRequest request) =>
        (HttpMethods.IsGet(request.Method) && request.Path == "/__hero/bootstrap")
        || (HttpMethods.IsPost(request.Method) && request.Path == "/__hero/bootstrap/claim");
}
```

No prefix/wildcard exception is allowed.

- [ ] **Step 2: Configure HostFilteringOptions in code**

In `Program.cs` add:

```csharp
builder.Services.Configure<HostFilteringOptions>(options =>
{
    options.AllowedHosts = ["127.0.0.1"];
    options.AllowEmptyHosts = false;
    options.IncludeFailureMessage = false;
});
```

Do not derive this from `builder.Configuration["AllowedHosts"]`.

- [ ] **Step 3: Register one process authority and order middleware**

Register the authority as a singleton created from `builder.Environment`, then build this order:

```csharp
app.UseHostFiltering();
app.UseMiddleware<LocalWebSessionMiddleware>();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>();
```

Host rejection must happen before session/bootstrap processing; session authorization must happen before Razor/dashboard execution.

- [ ] **Step 4: Run focused Host/unauthorized process tests**

```bash
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj -c Release --no-restore --filter "DirectDashboardGetWithoutSessionFailsClosed|HostHeaderOutsideCanonicalLoopbackIsRejected|WebProcessIgnoresExternalUrlOverrideAndBindsLoopbackOnly"
```

Expected: Host, unauthorized, and listener tests GREEN; bootstrap success remains RED until Task 4.

- [ ] **Step 5: Commit Host/session gate**

```bash
git add src/HeroPassport.Web/Program.cs src/HeroPassport.Web/Security/LocalWebSessionMiddleware.cs tests/HeroPassport.Web.Tests/WebProcessTests.cs
git commit -m "feat(web): enforce local host and session gate"
```

---

### Task 4: Implement the antiforgery-protected one-time bootstrap transition

**Files:**
- Create: `src/HeroPassport.Web/Security/LocalWebOriginPolicy.cs`
- Create: `src/HeroPassport.Web/Security/BootstrapEndpoint.cs`
- Create: `src/HeroPassport.Web/Components/Pages/Bootstrap.razor`
- Modify: `src/HeroPassport.Web/Program.cs`
- Modify: `tests/HeroPassport.Web.Tests/WebProcessTests.cs`

**Interfaces:**
- Consumes: `IAntiforgery`, `LocalWebSessionAuthority`, canonical request Host/origin.
- Produces: `BootstrapEndpoint.MapBootstrapClaim(IEndpointRouteBuilder)`, exact cookie issuance, `303 /`, and a state-free bootstrap page.

- [ ] **Step 1: Implement exact same-origin policy for the security transition**

Create a small pure helper:

```csharp
internal static class LocalWebOriginPolicy
{
    internal static bool IsAllowed(HttpRequest request)
    {
        if (request.Headers.TryGetValue("Sec-Fetch-Site", out var fetchSite)
            && fetchSite.Count == 1
            && fetchSite[0] is not ("same-origin" or "none"))
        {
            return false;
        }

        if (!request.Headers.TryGetValue("Origin", out var origin) || origin.Count == 0)
        {
            return true;
        }

        var expected = $"http://127.0.0.1:{request.Host.Port}";
        return origin.Count == 1 && string.Equals(origin[0], expected, StringComparison.Ordinal);
    }
}
```

Do not trust `X-Forwarded-*`; reverse proxy support is out of scope.

- [ ] **Step 2: Implement the bootstrap shell without product data**

Create `Bootstrap.razor`:

```razor
@page "/__hero/bootstrap"

<PageTitle>Hero Passport</PageTitle>

<form id="hero-bootstrap-form" method="post" action="/__hero/bootstrap/claim">
    <AntiforgeryToken />
    <input id="hero-bootstrap-capability" name="capability" type="hidden" />
    <noscript>JavaScript is required to open this local Hero Passport session.</noscript>
</form>

<script>
(() => {
  const raw = window.location.hash.startsWith('#') ? window.location.hash.slice(1) : '';
  history.replaceState(null, '', window.location.pathname);
  if (!raw) return;
  document.getElementById('hero-bootstrap-capability').value = raw;
  document.getElementById('hero-bootstrap-form').submit();
})();
</script>
```

Add response headers for this path from a narrowly scoped middleware or `OnStarting` branch before Razor execution:

```text
Cache-Control: no-store
Referrer-Policy: no-referrer
X-Content-Type-Options: nosniff
```

Do not include app/project state, external scripts, analytics, query parameters, local paths, or secret values.

- [ ] **Step 3: Map the single internal bootstrap POST**

Implement `BootstrapEndpoint.MapBootstrapClaim` so `Program.cs` contains no general product `MapPost` calls:

```csharp
internal static class BootstrapEndpoint
{
    private const int MaxCapabilityChars = 128;

    internal static IEndpointConventionBuilder MapBootstrapClaim(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost("/__hero/bootstrap/claim", HandleAsync);

    private static async Task HandleAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        LocalWebSessionAuthority authority)
    {
        if (!LocalWebOriginPolicy.IsAllowed(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!context.Request.HasFormContentType)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var values = form["capability"];
        var capability = values.Count == 1 ? values[0] : null;
        if (capability is null || capability.Length > MaxCapabilityChars || !authority.TryConsumeBootstrap(capability))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        context.Response.Cookies.Append(
            LocalWebSessionAuthority.CookieName,
            authority.SessionToken,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Secure = false,
                Path = "/",
                IsEssential = true,
            });
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = "/";
    }
}
```

Do not add return-url support.

- [ ] **Step 4: Map endpoint after antiforgery middleware and protect all other routes**

Composition target:

```csharp
app.UseHostFiltering();
app.UseMiddleware<LocalWebSessionMiddleware>();
app.UseAntiforgery();
app.MapBootstrapClaim();
app.MapStaticAssets();
app.MapRazorComponents<App>();
```

- [ ] **Step 5: Run bootstrap/CSRF/cookie/privacy process tests**

```bash
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj -c Release --no-restore
```

Expected: all Web tests GREEN except any lifecycle/browser-launch test reserved for Task 5.

- [ ] **Step 6: Commit bootstrap boundary**

```bash
git add src/HeroPassport.Web/Security src/HeroPassport.Web/Components/Pages/Bootstrap.razor src/HeroPassport.Web/Program.cs tests/HeroPassport.Web.Tests/WebProcessTests.cs
git commit -m "feat(web): add one-time browser bootstrap"
```

---

### Task 5: Make browser launch and process lifecycle fail closed

**Files:**
- Create: `src/HeroPassport.Web/Services/SystemBrowserLauncher.cs`
- Modify: `src/HeroPassport.Web/Program.cs`
- Modify: `tests/HeroPassport.Web.Tests/WebProcessTests.cs`

**Interfaces:**
- Consumes: final bound `IServerAddressesFeature`, `LocalWebSessionAuthority.BootstrapCapability`, environment and raw args.
- Produces: one production browser launch URI with fragment capability; Testing-only `--no-open-browser`; fatal production launch failure.

- [ ] **Step 1: Add a tiny browser launcher abstraction**

Implement:

```csharp
internal interface ISystemBrowserLauncher
{
    bool TryOpen(Uri uri);
}

internal sealed class SystemBrowserLauncher : ISystemBrowserLauncher
{
    public bool TryOpen(Uri uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }
}
```

Do not log `uri.AbsoluteUri` because it contains the capability fragment.

- [ ] **Step 2: Replace `RunAsync()` with explicit start/launch/wait lifecycle**

After endpoint mapping:

```csharp
await app.StartAsync();

var noOpenBrowser = args.Contains("--no-open-browser", StringComparer.Ordinal);
if (noOpenBrowser && !app.Environment.IsEnvironment("Testing"))
{
    await app.StopAsync();
    throw new InvalidOperationException("--no-open-browser is only available in the Testing environment.");
}

if (!noOpenBrowser)
{
    var addresses = app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()?.Addresses
        ?? throw new InvalidOperationException("Web server address is unavailable.");
    var address = addresses.Single(value => value.StartsWith("http://127.0.0.1:", StringComparison.Ordinal));
    var authority = app.Services.GetRequiredService<LocalWebSessionAuthority>();
    var launchUri = new Uri($"{address}/__hero/bootstrap#{authority.BootstrapCapability}");
    var launcher = app.Services.GetRequiredService<ISystemBrowserLauncher>();
    if (!launcher.TryOpen(launchUri))
    {
        await app.StopAsync();
        throw new InvalidOperationException("Could not open the local Hero Passport browser session.");
    }
}

await app.WaitForShutdownAsync();
```

If actual address formatting differs from the expected canonical address, fail closed rather than constructing an alternate Host.

- [ ] **Step 3: Prove test bypass cannot become a production bypass**

Add child-process tests:

- `ProductionRejectsNoOpenBrowserFlag` — Production + `--no-open-browser` exits non-zero and output contains no test/production secret.
- `ProductionRejectsDeterministicTestSecretEnvironment` — Production + either `HERO_PASSPORT_WEB_TEST_*` exits non-zero.
- normal Testing process with both deterministic secrets + `--no-open-browser` starts successfully.

The tests must assert only bounded error text and secret absence, never expect secrets to be logged.

- [ ] **Step 4: Run full Web tests**

```bash
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj -c Release --no-restore
```

Expected: GREEN.

- [ ] **Step 5: Commit lifecycle/launcher behavior**

```bash
git add src/HeroPassport.Web/Services/SystemBrowserLauncher.cs src/HeroPassport.Web/Program.cs tests/HeroPassport.Web.Tests/WebProcessTests.cs
git commit -m "feat(web): launch capability session fail closed"
```

---

### Task 6: Harden architecture guards against future security-surface drift

**Files:**
- Modify: `tests/HeroPassport.Architecture.Tests/ProjectDependencyTests.cs`
- Test: `tests/HeroPassport.Architecture.Tests/ProjectDependencyTests.cs`

**Interfaces:**
- Consumes: final Web source tree.
- Produces: repository-level guard allowing only the one bootstrap security POST while continuing to forbid product Minimal API/interactivity/persistence drift.

- [ ] **Step 1: Replace the broad `MapPost(` prohibition with an exact bootstrap exception**

Keep global rejection of:

```text
MapGet(
MapPut(
MapDelete(
MapPatch(
MapGroup(
AddInteractiveServerComponents
AddInteractiveWebAssemblyComponents
AddInteractiveServerRenderMode
AddInteractiveWebAssemblyRenderMode
```

Scan every Web `.cs` file for `MapPost(` and require:

```csharp
var postFiles = Directory.EnumerateFiles(webRoot, "*.cs", SearchOption.AllDirectories)
    .Where(path => File.ReadAllText(path).Contains("MapPost(", StringComparison.Ordinal))
    .Select(path => Path.GetRelativePath(webRoot, path).Replace('\\', '/'))
    .ToArray();
Assert.Equal(["Security/BootstrapEndpoint.cs"], postFiles);
```

Also assert `BootstrapEndpoint.cs` contains exactly the literal route `"/__hero/bootstrap/claim"` and no other product route literals mapped through `MapPost`.

- [ ] **Step 2: Extend Web package/dependency guard**

Continue requiring zero Web `PackageReference`s. Add forbidden framework/product tokens where they would signal scope drift:

```text
AddIdentity
AddDefaultIdentity
AddAuthentication(
AddOpenIdConnect
AddJwtBearer
AddCors
AllowAnyOrigin
UseForwardedHeaders
```

Do not ban types required by the approved Host/antiforgery/session design.

- [ ] **Step 3: Run architecture suite**

```bash
dotnet test tests/HeroPassport.Architecture.Tests/HeroPassport.Architecture.Tests.csproj -c Release --no-restore
```

Expected: GREEN.

- [ ] **Step 4: Commit the guard**

```bash
git add tests/HeroPassport.Architecture.Tests/ProjectDependencyTests.cs
git commit -m "test(architecture): bound local web security surface"
```

---

### Task 7: Align repository truth and perform exact-head qualification

**Files:**
- Modify: `docs/ARCHITECTURE.md`
- Modify: `docs/DEPLOYMENT-MODES.md`
- Modify: `docs/TESTING-QUALITY.md`
- Modify: `docs/ROADMAP.md`
- Review: all files changed relative to `main@f074ab7d0733f2fa590d95663415066939d4745c`

**Interfaces:**
- Consumes: fully GREEN 0.2-B behavior.
- Produces: accurate repository truth, issue/PR evidence, exact-head CI qualification.

- [ ] **Step 1: Update docs only to implemented facts**

Record:

```text
Browser launch capability -> one-time bootstrap claim -> process session cookie
Host = 127.0.0.1 only
Kestrel = IPv4 loopback dynamic port only
unauthorized product routes = 401
unsupported Host = 400
bootstrap/session state = process memory only
Web remains static SSR/read-only in 0.2-B
```

Keep local HTTPS, Web mutations, management/history polish, packaging/tray integration, remote access, and persistent sessions as future work.

- [ ] **Step 2: Run deterministic restore/build/test locally where a .NET SDK runner is available**

```bash
dotnet restore HeroPassport.slnx
dotnet build HeroPassport.slnx -c Release --no-restore
dotnet test tests/HeroPassport.Architecture.Tests/HeroPassport.Architecture.Tests.csproj -c Release --no-build
dotnet test tests/HeroPassport.Domain.Tests/HeroPassport.Domain.Tests.csproj -c Release --no-build
dotnet test tests/HeroPassport.Application.Tests/HeroPassport.Application.Tests.csproj -c Release --no-build
dotnet test tests/HeroPassport.Infrastructure.Tests/HeroPassport.Infrastructure.Tests.csproj -c Release --no-build
dotnet test tests/HeroPassport.Contract.Tests/HeroPassport.Contract.Tests.csproj -c Release --no-build
dotnet test tests/HeroPassport.App.Tests/HeroPassport.App.Tests.csproj -c Release --no-build
dotnet test tests/HeroPassport.Web.Tests/HeroPassport.Web.Tests.csproj -c Release --no-build
```

If the active environment has no .NET SDK, do not claim local verification; rely on exact-head GitHub Actions and state that explicitly.

- [ ] **Step 3: Perform a final privacy/security diff review**

Search the changed tree for accidental secret/logging/surface regressions:

```bash
git diff --check main...HEAD
git diff --stat main...HEAD
git grep -nE "HERO_PASSPORT_WEB_TEST_(BOOTSTRAP|SESSION)|LocalSession|Map(Get|Post|Put|Delete|Patch|Group)|AllowAnyOrigin|UseForwardedHeaders" -- src/HeroPassport.Web tests/HeroPassport.Web.Tests tests/HeroPassport.Architecture.Tests
```

Verify test-secret variable names occur only in the explicit Testing gate/tests and no literal production secret exists.

- [ ] **Step 4: Commit docs**

```bash
git add docs/ARCHITECTURE.md docs/DEPLOYMENT-MODES.md docs/TESTING-QUALITY.md docs/ROADMAP.md
git commit -m "docs(web): record 0.2-B local security boundary"
```

- [ ] **Step 5: Push branch and open a focused PR closing #38**

PR body must include:

- exact baseline `f074ab7d0733f2fa590d95663415066939d4745c`;
- RED test commit SHA;
- final exact head SHA;
- one-time bootstrap/session/Host/CSRF semantics;
- explicit non-goals/no-mutation statement;
- current official Microsoft documentation references;
- `Closes #38`.

- [ ] **Step 6: Require exact-head GitHub Actions evidence before ready/merge**

Verify on the exact PR head:

```text
required ci / build-test = success
Architecture = success
Domain = success
Application = success
Infrastructure = success
Contract = success
App = success
Web = success
Agent Skill evals = success
publish packaged app = success
Codex host smoke/lifecycle = success
packaged vertical E2E = success
release-platform = success where triggered
```

Do not infer success from an earlier commit.

- [ ] **Step 7: Re-read PR review threads and issue state before merge**

No unresolved blocking thread, no moved head, and no changed `main` that invalidates the qualified base/mergeability claim. If `main` moved, rebase/merge current main and rerun exact-head qualification before integration.
