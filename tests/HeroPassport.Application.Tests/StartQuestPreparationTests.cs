using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class StartQuestPreparationTests
{
    [Fact]
    public void PrepareStartQuestNormalizesWithoutStoreAccess()
    {
        var store = new ThrowOnStoreAccess();
        var application = new HeroPassportApplication(store, TimeProvider.System);
        var requestId = MutationRequestId.New();
        var heroId = HeroId.New();
        var project = new ProjectBindingContext("Demo", new string('a', 64), "project-identity/1");

        var prepared = application.PrepareStartQuest(
            new StartQuestRequest(
                requestId,
                heroId,
                "coding",
                "  Build   parser  ",
                "  Ship   bounded   parser  "),
            project);

        Assert.Equal(requestId, prepared.StartRequestId);
        Assert.Equal(heroId, prepared.HeroId);
        Assert.Equal("coding", prepared.QuestType);
        Assert.Equal("Build parser", prepared.Title);
        Assert.Equal("Ship bounded parser", prepared.Goal);
    }

    [Fact]
    public void PrepareStartQuestPreservesExistingValidationCodes()
    {
        var application = new HeroPassportApplication(new ThrowOnStoreAccess(), TimeProvider.System);
        var heroId = HeroId.New();
        var project = new ProjectBindingContext("Demo", new string('a', 64), "project-identity/1");

        var typeError = Assert.Throws<HeroPassportException>(() => application.PrepareStartQuest(
            new StartQuestRequest(MutationRequestId.New(), heroId, "invalid", "Title", "Goal"),
            project));
        Assert.Equal("HP110", typeError.Code);

        var titleError = Assert.Throws<HeroPassportException>(() => application.PrepareStartQuest(
            new StartQuestRequest(MutationRequestId.New(), heroId, "coding", "   ", "Goal"),
            project));
        Assert.Equal("HP100", titleError.Code);

        var projectError = Assert.Throws<HeroPassportException>(() => application.PrepareStartQuest(
            new StartQuestRequest(MutationRequestId.New(), heroId, "coding", "Title", "Goal"),
            project with { WorkspaceFingerprint = "bad" }));
        Assert.Equal("HP310", projectError.Code);
    }

    private sealed class ThrowOnStoreAccess : IHeroPassportStateStore
    {
        private static InvalidOperationException Accessed() => new("Preparation must not access the state store.");

        public Task<BootstrapResult> BootstrapAsync(BootstrapStoreCommand command, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Accessed();
        public Task<ConfigureResult> ConfigureAsync(ConfigureRequest request, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Accessed();
        public Task<RuntimeContextResult> GetRuntimeContextAsync(ProjectBindingContext project, CancellationToken cancellationToken = default) => throw Accessed();
        public Task<CreateHeroResult> CreateHeroAsync(CreateHeroStoreCommand command, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Accessed();
        public Task ActivateHeroAsync(HeroId heroId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Accessed();
        public Task<HeroListResult> ListHeroesAsync(CancellationToken cancellationToken = default) => throw Accessed();
        public Task<HeroPreferenceChangeResult> ActivateHeroPreferenceAsync(HeroId heroId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Accessed();
        public Task<HeroPreferenceChangeResult> ArchiveHeroAsync(HeroId heroId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Accessed();
        public Task<HeroPreferenceChangeResult> RestoreHeroAsync(HeroId heroId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Accessed();
        public Task DeleteHeroPermanentlyAsync(HeroId heroId, CancellationToken cancellationToken = default) => throw Accessed();
        public Task<HeroCardResult> GetCardAsync(HeroId heroId, ProjectBindingContext project, CancellationToken cancellationToken = default) => throw Accessed();
        public Task<StartQuestResult> StartQuestAsync(StartQuestStoreCommand command, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Accessed();
        public Task<FinishQuestResult> FinishQuestAsync(FinishQuestStoreCommand command, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Accessed();
        public Task<string> GetQuestLocaleAsync(QuestId questId, CancellationToken cancellationToken = default) => throw Accessed();
    }
}
