using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Globalization;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class BootstrapAndContextTests
{
    [Fact]
    public async Task BootstrapReplaysAndRejectsChangedArguments()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var application = CreateApplication(path);
            var requestId = MutationRequestId.New();
            var request = new BootstrapRequest(requestId, "ru-RU", "Nova", "rpg_engineering", true, true);

            var first = await application.BootstrapAsync(request, cancellationToken);
            var replay = await application.BootstrapAsync(request, cancellationToken);

            Assert.True(first.SetupCompleted);
            Assert.False(first.Replayed);
            Assert.True(replay.Replayed);
            Assert.Equal(first.Hero.HeroId, replay.Hero.HeroId);
            Assert.Equal("Nova", replay.Hero.Name);

            var changed = request with { HeroName = "Other" };
            var mismatch = await Assert.ThrowsAsync<HeroPassportException>(() => application.BootstrapAsync(changed, cancellationToken));
            Assert.Equal("HP135", mismatch.Code);

            var fresh = request with { BootstrapRequestId = MutationRequestId.New() };
            var alreadyCompleted = await Assert.ThrowsAsync<HeroPassportException>(() => application.BootstrapAsync(fresh, cancellationToken));
            Assert.Equal("HP002", alreadyCompleted.Code);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task ConfigureIsGatedThenIdempotentAfterSetup()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var application = CreateApplication(path);
            var configure = new ConfigureRequest("en-US", "minimal", false, false);

            var setupRequired = await Assert.ThrowsAsync<HeroPassportException>(() => application.ConfigureAsync(configure, cancellationToken));
            Assert.Equal("HP001", setupRequired.Code);

            await application.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "ru-RU", "Nova", "rpg_engineering", true, true),
                cancellationToken);

            var changed = await application.ConfigureAsync(configure, cancellationToken);
            var noOp = await application.ConfigureAsync(configure, cancellationToken);

            Assert.True(changed.Changed);
            Assert.False(noOp.Changed);
            Assert.Equal("en-US", noOp.Settings.Locale);
            Assert.Equal("minimal", noOp.Settings.PresentationStyle);
            Assert.False(noOp.Settings.AutoStartQuest);
            Assert.False(noOp.Settings.AutoFinishQuest);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task RuntimeContextIsAvailableBeforeSetupAndDoesNotCreateProjectRows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var application = CreateApplication(path);
            var project = new ProjectBindingContext("Transient Project", new string('a', 64), "project-identity/1");

            var before = await application.GetRuntimeContextAsync(project, cancellationToken);
            Assert.False(before.SetupCompleted);
            Assert.Null(before.Settings);
            Assert.Null(before.ActiveHero);
            Assert.Equal("Transient Project", before.Project.DisplayName);
            Assert.Empty(before.OpenQuests);
            Assert.Equal(0L, await CountProjectsAsync(path, cancellationToken));

            await application.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
                cancellationToken);

            var after = await application.GetRuntimeContextAsync(project, cancellationToken);
            Assert.True(after.SetupCompleted);
            Assert.NotNull(after.Settings);
            Assert.Equal("Nova", after.ActiveHero?.Name);
            Assert.Equal(0L, await CountProjectsAsync(path, cancellationToken));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task CreateHeroDoesNotActivateUntilExplicitActivation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var application = CreateApplication(path);
            var project = new ProjectBindingContext("Project", new string('b', 64), "project-identity/1");
            var bootstrap = await application.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
                cancellationToken);

            var createRequest = new CreateHeroRequest(MutationRequestId.New(), "CodeMage");
            var created = await application.CreateHeroAsync(createRequest, cancellationToken);
            var replay = await application.CreateHeroAsync(createRequest, cancellationToken);
            var beforeActivation = await application.GetRuntimeContextAsync(project, cancellationToken);

            Assert.False(created.Replayed);
            Assert.True(replay.Replayed);
            Assert.Equal(created.Hero.HeroId, replay.Hero.HeroId);
            Assert.Equal(bootstrap.Hero.HeroId, beforeActivation.ActiveHero?.HeroId);

            await application.ActivateHeroAsync(created.Hero.HeroId, cancellationToken);
            var afterActivation = await application.GetRuntimeContextAsync(project, cancellationToken);
            Assert.Equal(created.Hero.HeroId, afterActivation.ActiveHero?.HeroId);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task ConcurrentFreshBootstrapsCreateExactlyOneInitialHero()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var firstApplication = CreateApplication(path);
            var secondApplication = CreateApplication(path);

            var firstTask = CaptureAsync(() => firstApplication.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "One", "rpg_engineering", true, true),
                cancellationToken));
            var secondTask = CaptureAsync(() => secondApplication.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Two", "rpg_engineering", true, true),
                cancellationToken));

            var results = await Task.WhenAll(firstTask, secondTask);

            Assert.Single(results.Where(static result => result.Result is not null));
            var error = Assert.Single(results.Where(static result => result.Error is not null)).Error;
            Assert.Equal("HP002", Assert.IsType<HeroPassportException>(error).Code);
            Assert.Equal(1L, await CountRowsAsync(path, "heroes", cancellationToken));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static HeroPassportApplication CreateApplication(string databasePath) =>
        new(new SqliteHeroPassportStateStore(databasePath), new FixedTimeProvider(new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero)));

    private static async Task<(BootstrapResult? Result, Exception? Error)> CaptureAsync(Func<Task<BootstrapResult>> action)
    {
        try
        {
            return (await action(), null);
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
    }

    private static async Task<long> CountProjectsAsync(string path, CancellationToken cancellationToken) =>
        await CountRowsAsync(path, "projects", cancellationToken);

    private static async Task<long> CountRowsAsync(string path, string table, CancellationToken cancellationToken)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static string CreateDatabasePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hero-passport-application-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "hero-passport.db");
    }

    private static void DeleteDatabase(string path)
    {
        SqliteConnection.ClearAllPools();
        var directory = Path.GetDirectoryName(path);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
