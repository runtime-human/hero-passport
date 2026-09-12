using System.Net;
using HeroPassport.Application.Runtime;
using HeroPassport.Infrastructure.Persistence;
using HeroPassport.Infrastructure.ProjectIdentity;
using HeroPassport.Infrastructure.Runtime;
using HeroPassport.Web.Components;
using HeroPassport.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Listen(IPAddress.Loopback, 0);
});
builder.Services.AddRazorComponents();

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
app.UseStaticFiles();
app.MapRazorComponents<App>();

await app.RunAsync();
