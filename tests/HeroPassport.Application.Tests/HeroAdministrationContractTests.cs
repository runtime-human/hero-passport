using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class HeroAdministrationContractTests
{
    [Fact]
    public async Task ActivatingArchivedHeroReturnsHp141ForRestoreRecovery()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            _ = await app.BootstrapAsync(new BootstrapRequest(MutationRequestId.New(), "en-US", "First", "rpg_engineering", true, true), token);
            var second = (await app.CreateHeroAsync(new CreateHeroRequest(MutationRequestId.New(), "Second"), token)).Hero;
            _ = await app.ArchiveHeroAsync(second.HeroId, token);

            var error = await Assert.ThrowsAsync<HeroPassportException>(() => app.ActivateHeroPreferenceAsync(second.HeroId, token));
            Assert.Equal("HP141", error.Code);
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }
}
