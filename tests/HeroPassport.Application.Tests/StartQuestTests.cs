using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class StartQuestTests
{
    [Fact]
    public async Task StartReplaysSameIntentAndRejectsChangedProjectOrHero()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var application = CreateApplication(path);
            var bootstrap = await BootstrapAsync(application, cancellationToken);
            var secondHero = await application.CreateHeroAsync(
                new CreateHeroRequest(MutationRequestId.New(), "Second"),
                cancellationToken);
            var project = Project("Project A", 'a');
            var requestId = MutationRequestId.New();
            var request = new StartQuestRequest(requestId, bootstrap.Hero.HeroId, "coding", "Implement start", "Implement explicit project-bound StartQuest.");

            var first = await application.StartQuestAsync(request, project, cancellationToken);
            var replay = await application.StartQuestAsync(request, project, cancellationToken);

            Assert.False(first.Replayed);
            Assert.True(replay.Replayed);
            Assert.Equal(first.Quest.QuestId, replay.Quest.QuestId);
            Assert.Equal(bootstrap.Hero.HeroId, replay.Quest.HeroId);
            Assert.Equal("en-US", replay.Quest.Locale);

            var differentHero = request with { HeroId = secondHero.Hero.HeroId };
            var heroMismatch = await Assert.ThrowsAsync<HeroPassportException>(() => application.StartQuestAsync(differentHero, project, cancellationToken));
            Assert.Equal("HP135", heroMismatch.Code);

            var differentProject = Project("Project B", 'b');
            var projectMismatch = await Assert.ThrowsAsync<HeroPassportException>(() => application.StartQuestAsync(request, differentProject, cancellationToken));
            Assert.Equal("HP135", projectMismatch.Code);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task ExplicitHeroAndLocaleSnapshotDoNotFollowLaterActivePreferences()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var application = CreateApplication(path);
            var bootstrap = await BootstrapAsync(application, cancellationToken);
            var secondHero = await application.CreateHeroAsync(
                new CreateHeroRequest(MutationRequestId.New(), "Second"),
                cancellationToken);
            var project = Project("Project", 'c');
            var request = new StartQuestRequest(MutationRequestId.New(), bootstrap.Hero.HeroId, "coding", "Pinned owner", "Keep Quest ownership bound to the explicit Hero.");

            var started = await application.StartQuestAsync(request, project, cancellationToken);
            await application.ActivateHeroAsync(secondHero.Hero.HeroId, cancellationToken);
            await application.ConfigureAsync(new ConfigureRequest("ru-RU", "rpg_engineering", true, true), cancellationToken);
            var replay = await application.StartQuestAsync(request, project, cancellationToken);

            Assert.Equal(bootstrap.Hero.HeroId, started.Quest.HeroId);
            Assert.Equal(bootstrap.Hero.HeroId, replay.Quest.HeroId);
            Assert.Equal("en-US", started.Quest.Locale);
            Assert.Equal("en-US", replay.Quest.Locale);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task OneOpenQuestPerHeroProjectButOtherHeroOrProjectCanStart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var application = CreateApplication(path);
            var bootstrap = await BootstrapAsync(application, cancellationToken);
            var secondHero = await application.CreateHeroAsync(
                new CreateHeroRequest(MutationRequestId.New(), "Second"),
                cancellationToken);
            var projectA = Project("A", 'd');
            var projectB = Project("B", 'e');

            await application.StartQuestAsync(
                new StartQuestRequest(MutationRequestId.New(), bootstrap.Hero.HeroId, "coding", "First", "First goal"),
                projectA,
                cancellationToken);

            var conflict = await Assert.ThrowsAsync<HeroPassportException>(() => application.StartQuestAsync(
                new StartQuestRequest(MutationRequestId.New(), bootstrap.Hero.HeroId, "review", "Second", "Second goal"),
                projectA,
                cancellationToken));
            Assert.Equal("HP133", conflict.Code);

            var otherHero = await application.StartQuestAsync(
                new StartQuestRequest(MutationRequestId.New(), secondHero.Hero.HeroId, "review", "Other hero", "Independent Hero goal"),
                projectA,
                cancellationToken);
            var otherProject = await application.StartQuestAsync(
                new StartQuestRequest(MutationRequestId.New(), bootstrap.Hero.HeroId, "research", "Other project", "Independent Project goal"),
                projectB,
                cancellationToken);

            Assert.Equal(secondHero.Hero.HeroId, otherHero.Quest.HeroId);
            Assert.Equal(bootstrap.Hero.HeroId, otherProject.Quest.HeroId);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task RuntimeContextReturnsOpenQuestsAcrossAllHeroesInProject()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var application = CreateApplication(path);
            var bootstrap = await BootstrapAsync(application, cancellationToken);
            var secondHero = await application.CreateHeroAsync(
                new CreateHeroRequest(MutationRequestId.New(), "Second"),
                cancellationToken);
            var project = Project("Project", 'f');

            var first = await application.StartQuestAsync(
                new StartQuestRequest(MutationRequestId.New(), bootstrap.Hero.HeroId, "coding", "First", "First goal"),
                project,
                cancellationToken);
            var second = await application.StartQuestAsync(
                new StartQuestRequest(MutationRequestId.New(), secondHero.Hero.HeroId, "review", "Second", "Second goal"),
                project,
                cancellationToken);

            var context = await application.GetRuntimeContextAsync(project, cancellationToken);

            Assert.Equal(2, context.OpenQuests.Count);
            Assert.Equal(first.Quest.QuestId, context.OpenQuests[0].QuestId);
            Assert.Equal(second.Quest.QuestId, context.OpenQuests[1].QuestId);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task ConcurrentFreshStartsProduceOneQuestAndOneActiveQuestError()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var bootstrapApplication = CreateApplication(path);
            var bootstrap = await BootstrapAsync(bootstrapApplication, cancellationToken);
            var firstApplication = CreateApplication(path);
            var secondApplication = CreateApplication(path);
            var project = Project("Project", '1');

            var firstTask = CaptureAsync(() => firstApplication.StartQuestAsync(
                new StartQuestRequest(MutationRequestId.New(), bootstrap.Hero.HeroId, "coding", "One", "Goal one"),
                project,
                cancellationToken));
            var secondTask = CaptureAsync(() => secondApplication.StartQuestAsync(
                new StartQuestRequest(MutationRequestId.New(), bootstrap.Hero.HeroId, "coding", "Two", "Goal two"),
                project,
                cancellationToken));

            var results = await Task.WhenAll(firstTask, secondTask);

            Assert.Single(results, static result => result.Result is not null);
            var error = Assert.Single(results, static result => result.Error is not null).Error;
            Assert.Equal("HP133", Assert.IsType<HeroPassportException>(error).Code);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static async Task<BootstrapResult> BootstrapAsync(HeroPassportApplication application, CancellationToken cancellationToken) =>
        await application.BootstrapAsync(
            new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
            cancellationToken);

    private static ProjectBindingContext Project(string name, char fingerprintCharacter) =>
        new(name, new string(fingerprintCharacter, 64), "project-identity/1");

    private static HeroPassportApplication CreateApplication(string databasePath) =>
        new(new SqliteHeroPassportStateStore(databasePath), new FixedTimeProvider(new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero)));

    private static async Task<(StartQuestResult? Result, Exception? Error)> CaptureAsync(Func<Task<StartQuestResult>> action)
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

    private static string CreateDatabasePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hero-passport-start-tests", Guid.NewGuid().ToString("N"));
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
