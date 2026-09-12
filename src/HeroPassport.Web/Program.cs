using HeroPassport.Web.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents();

var app = builder.Build();
app.MapRazorComponents<App>();

await app.RunAsync();
