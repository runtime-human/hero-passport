using System.Net;
using HeroPassport.Application.Runtime;
using HeroPassport.Infrastructure.Persistence;
using HeroPassport.Infrastructure.ProjectIdentity;
using HeroPassport.Infrastructure.Runtime;
using HeroPassport.Web.Components;
using HeroPassport.Web.Security;
using HeroPassport.Web.Services;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.HostFiltering;

var noOpenBrowser = args.Any(static arg =>
    string.Equals(arg, "--no-open-browser", StringComparison.Ordinal));
var builderArgs = args
    .Where(static arg => !string.Equals(arg, "--no-open-browser", StringComparison.Ordinal))
    .ToArray();

var builder = WebApplication.CreateBuilder(builderArgs);
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Listen(IPAddress.Loopback, 0);
});
if (builder.Environment.IsEnvironment("Testing"))
{
    builder.WebHost.UseStaticWebAssets();
}

if (noOpenBrowser && !builder.Environment.IsEnvironment("Testing"))
{
    throw new InvalidOperationException(
        "--no-open-browser is only available in the Testing environment.");
}

builder.Services.AddRazorComponents();
builder.Services.PostConfigure<HostFilteringOptions>(options =>
{
    options.AllowedHosts = ["127.0.0.1"];
    options.AllowEmptyHosts = false;
    options.IncludeFailureMessage = false;
});

var sessionAuthority = LocalWebSessionAuthority.Create(builder.Environment);
builder.Services.AddSingleton(sessionAuthority);
builder.Services.AddSingleton<ISystemBrowserLauncher, SystemBrowserLauncher>();

var databasePath = HeroPassportRuntimePaths.ResolveDatabasePath();
await HeroPassportDatabase.InitializeAsync(databasePath);
var installationSalt = await HeroPassportDatabase.ReadProjectIdentitySaltAsync(databasePath);
var resolvedProject = await ProjectIdentityResolver.ResolveAsync(
    builder.Configuration["project-root"],
    Directory.GetCurrentDirectory(),
    installationSalt);
var project = new ProjectBindingContext(
    resolvedProject.DisplayName,
    resolvedProject.WorkspaceFingerprint,
    resolvedProject.IdentityVersion);
var application = new HeroPassportApplication(
    new SqliteHeroPassportStateStore(databasePath),
    TimeProvider.System);

builder.Services.AddSingleton(application);
builder.Services.AddSingleton(project);
builder.Services.AddSingleton<HeroPassportDashboardService>();

var app = builder.Build();
app.UseHostFiltering();
app.UseMiddleware<BootstrapResponseHeadersMiddleware>();
app.UseMiddleware<LocalWebSessionMiddleware>();
app.UseAntiforgery();
app.MapBootstrapClaim();
app.MapStaticAssets();
app.MapRazorComponents<App>();

await app.StartAsync();

if (!noOpenBrowser)
{
    var server = app.Services.GetRequiredService<IServer>();
    var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses
        ?? throw new InvalidOperationException("Web server address is unavailable.");
    var address = addresses
        .Select(static value => new Uri(value, UriKind.Absolute))
        .Single(static uri =>
            uri.Scheme == Uri.UriSchemeHttp
            && string.Equals(uri.Host, IPAddress.Loopback.ToString(), StringComparison.Ordinal));
    var launchUri = new UriBuilder(address)
    {
        Path = "/__hero/bootstrap",
        Query = string.Empty,
        Fragment = sessionAuthority.BootstrapCapability,
    }.Uri;
    var browserLauncher = app.Services.GetRequiredService<ISystemBrowserLauncher>();
    if (!browserLauncher.TryOpen(launchUri))
    {
        await app.StopAsync();
        throw new InvalidOperationException(
            "Could not open the local Hero Passport browser session.");
    }
}

await app.WaitForShutdownAsync();
