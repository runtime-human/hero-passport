using System.Net;
using HeroPassport.Web.Components;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Listen(IPAddress.Loopback, 0);
});
builder.Services.AddRazorComponents();

var app = builder.Build();
app.MapRazorComponents<App>();

await app.RunAsync();
