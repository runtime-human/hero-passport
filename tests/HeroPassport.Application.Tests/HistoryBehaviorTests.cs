using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class HistoryBehaviorTests
{
    [Fact]
    public async Task HistoryValidatesProjectBeforeStoreAccess()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new RecordingStateStore();
        var app = new HeroPassportApplication(store, TimeProvider.System);
        var invalid = new ProjectBindingContext("Project", "bad", "project-identity/1");

        var listError = await Assert.ThrowsAsync<HeroPassportException>(() =>
            app.GetProjectQuestHistoryAsync(invalid, token));
        var detailError = await Assert.ThrowsAsync<HeroPassportException>(() =>
            app.GetQuestHistoryDetailAsync(QuestId.New(), invalid, token));

        Assert.Equal("HP310", listError.Code);
        Assert.Equal("HP310", detailError.Code);
        Assert.Equal(0, store.ListHistoryCalls);
        Assert.Equal(0, store.DetailHistoryCalls);
    }

    [Fact]
    public async Task HistoryDelegatesNormalizedProjectAndExactQuestSelector()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new RecordingStateStore();
        var app = new HeroPassportApplication(store, TimeProvider.System);
        var questId = QuestId.New();
        var project = new ProjectBindingContext(
            "  Demo   Project  ",
            new string('a', 64),
            "project-identity/1");

        var list = await app.GetProjectQuestHistoryAsync(project, token);
        var detail = await app.GetQuestHistoryDetailAsync(questId, project, token);

        Assert.Empty(list.Items);
        Assert.Null(detail);
        Assert.Equal(1, store.ListHistoryCalls);
        Assert.Equal(1, store.DetailHistoryCalls);
        Assert.NotNull(store.ListProject);
        Assert.NotNull(store.DetailProject);
        Assert.Equal("Demo Project", store.ListProject!.DisplayName);
        Assert.Equal("Demo Project", store.DetailProject!.DisplayName);
        Assert.Equal(project.WorkspaceFingerprint, store.ListProject.WorkspaceFingerprint);
        Assert.Equal(project.WorkspaceFingerprint, store.DetailProject.WorkspaceFingerprint);
        Assert.Equal(questId, store.DetailQuestId);
    }

    private sealed class RecordingStateStore : IHeroPassportStateStore
    {
        public int ListHistoryCalls { get; private set; }
        public int DetailHistoryCalls { get; private set; }
        public ProjectBindingContext? ListProject { get; private set; }
        public ProjectBindingContext? DetailProject { get; private set; }
        public QuestId? DetailQuestId { get; private set; }

        public Task<ProjectQuestHistoryResult> GetProjectQuestHistoryAsync(
            ProjectBindingContext project,
            CancellationToken cancellationToken = default)
        {
            ListHistoryCalls++;
            ListProject = project;
            return Task.FromResult(new ProjectQuestHistoryResult(project.DisplayName, []));
        }

        public Task<QuestHistoryDetailResult?> GetQuestHistoryDetailAsync(
            QuestId questId,
            ProjectBindingContext project,
            CancellationToken cancellationToken = default)
        {
            DetailHistoryCalls++;
            DetailQuestId = questId;
            DetailProject = project;
            return Task.FromResult<QuestHistoryDetailResult?>(null);
        }

        private static InvalidOperationException Unused() => new("Unexpected state-store call.");

        public Task<BootstrapResult> BootstrapAsync(BootstrapStoreCommand command, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task<ConfigureResult> ConfigureAsync(ConfigureRequest request, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task<RuntimeContextResult> GetRuntimeContextAsync(ProjectBindingContext project, CancellationToken cancellationToken = default) => throw Unused();
        public Task<CreateHeroResult> CreateHeroAsync(CreateHeroStoreCommand command, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task ActivateHeroAsync(HeroId heroId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task<HeroListResult> ListHeroesAsync(CancellationToken cancellationToken = default) => throw Unused();
        public Task<HeroPreferenceChangeResult> ActivateHeroPreferenceAsync(HeroId heroId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task<HeroPreferenceChangeResult> ArchiveHeroAsync(HeroId heroId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task<HeroPreferenceChangeResult> RestoreHeroAsync(HeroId heroId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task DeleteHeroPermanentlyAsync(HeroId heroId, CancellationToken cancellationToken = default) => throw Unused();
        public Task<HeroCardResult> GetCardAsync(HeroId heroId, ProjectBindingContext project, CancellationToken cancellationToken = default) => throw Unused();
        public Task<StartQuestResult> StartQuestAsync(StartQuestStoreCommand command, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task<FinishQuestResult> FinishQuestAsync(FinishQuestStoreCommand command, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task<string> GetQuestLocaleAsync(QuestId questId, CancellationToken cancellationToken = default) => throw Unused();
    }
}
