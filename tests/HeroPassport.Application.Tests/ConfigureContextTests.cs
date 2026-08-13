using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class ConfigureContextTests
{
    [Fact]
    public async Task ConfigureIsSetupGatedAndBecomesNoOpWhenValuesMatch()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var request = new ConfigureRequest("en-US", "minimal", false, false);

            Assert.Equal("HP001", (await Assert.ThrowsAsync<HeroPassportException>(() => app.ConfigureAsync(request, token))).Code);
            await app.BootstrapAsync(new BootstrapRequest(MutationRequestId.New(), "ru-RU", "Nova", "rpg_engineering", true, true), token);

            Assert.True((await app.ConfigureAsync(request, token)).Changed);
            Assert.False((await app.ConfigureAsync(request, token)).Changed);
        }
        finally { TestRuntime.DeleteDatabase(path); }
    }

    [Fact]
    public async Task RuntimeContextBeforeSetupDoesNotPersistProjectAndAfterSetupHydratesActiveHero()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        var project = new ProjectBindingContext("Transient Project", new string('a', 64), "project-identity/1");
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);

            var before = await app.GetRuntimeContextAsync(project, token);
            Assert.False(before.SetupCompleted);
            Assert.Null(before.ActiveHero);
            Assert.Equal(0, await CountProjectsAsync(path, token));

            var bootstrap = await app.BootstrapAsync(new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true), token);
            var after = await app.GetRuntimeContextAsync(project, token);

            Assert.True(after.SetupCompleted);
            Assert.Equal(bootstrap.Hero, after.ActiveHero);
            Assert.Equal(0, await CountProjectsAsync(path, token));
        }
        finally { TestRuntime.DeleteDatabase(path); }
    }

    private static async Task<long> CountProjectsAsync(string path, CancellationToken token)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM projects;";
        return (long)(await command.ExecuteScalarAsync(token) ?? 0L);
    }
}
