using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class HeroPreferenceTests
{
    [Fact]
    public async Task CreateHeroReplaysWithoutChangingActiveHeroUntilActivation()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        var project = new ProjectBindingContext("Project", new string('b', 64), "project-identity/1");
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var initial = await app.BootstrapAsync(new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true), token);
            var request = new CreateHeroRequest(MutationRequestId.New(), "CodeMage");

            var created = await app.CreateHeroAsync(request, token);
            var replay = await app.CreateHeroAsync(request, token);
            Assert.True(replay.Replayed);
            Assert.Equal(created.Hero, replay.Hero);
            Assert.Equal(initial.Hero, (await app.GetRuntimeContextAsync(project, token)).ActiveHero);

            Assert.Equal("HP135", (await Assert.ThrowsAsync<HeroPassportException>(() => app.CreateHeroAsync(request with { Name = "Other" }, token))).Code);
            await app.ActivateHeroAsync(created.Hero.HeroId, token);
            Assert.Equal(created.Hero, (await app.GetRuntimeContextAsync(project, token)).ActiveHero);
        }
        finally { TestRuntime.DeleteDatabase(path); }
    }
}
