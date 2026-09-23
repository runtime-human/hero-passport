using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class SkillProgressionReadContractTests
{
    [Fact]
    public async Task SkillProgressionDelegatesExactHeroAndNormalizedProject()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new RecordingStateStore();
        var app = new HeroPassportApplication(store, TimeProvider.System);
        var heroId = HeroId.New();
        var project = new ProjectBindingContext(
            "  Demo   Project  ",
            new string('a', 64),
            "project-identity/1");

        var result = await app.GetSkillProgressionAsync(heroId, project, token);

        Assert.Equal("Nova", result.HeroName);
        Assert.Single(result.Skills);
        Assert.Equal("coding", result.Skills[0].SkillKey);
        Assert.Equal(50, result.Skills[0].Hero.Xp);
        Assert.Equal(2, result.Skills[0].Hero.Level);
        Assert.Equal(12, result.Skills[0].ProjectContribution.Xp);
        Assert.Equal(1, store.SkillProgressionCalls);
        Assert.Equal(heroId, store.SkillProgressionHeroId);
        Assert.NotNull(store.SkillProgressionProject);
        Assert.Equal("Demo Project", store.SkillProgressionProject!.DisplayName);
        Assert.Equal(project.WorkspaceFingerprint, store.SkillProgressionProject.WorkspaceFingerprint);
        Assert.Equal(project.IdentityVersion, store.SkillProgressionProject.IdentityVersion);
    }

    [Theory]
    [InlineData("bad", "project-identity/1")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "project-identity/0")]
    public async Task SkillProgressionRejectsInvalidProjectBeforeStoreAccess(
        string fingerprint,
        string identityVersion)
    {
        var token = TestContext.Current.CancellationToken;
        var store = new RecordingStateStore();
        var app = new HeroPassportApplication(store, TimeProvider.System);
        var project = new ProjectBindingContext("Project", fingerprint, identityVersion);

        var error = await Assert.ThrowsAsync<HeroPassportException>(() =>
            app.GetSkillProgressionAsync(HeroId.New(), project, token));

        Assert.Equal("HP310", error.Code);
        Assert.Equal(0, store.SkillProgressionCalls);
    }

    private sealed class RecordingStateStore : IHeroPassportStateStore
    {
        public int SkillProgressionCalls { get; private set; }
        public HeroId? SkillProgressionHeroId { get; private set; }
        public ProjectBindingContext? SkillProgressionProject { get; private set; }

        public Task<HeroSkillProgressionReadResult> GetSkillProgressionAsync(
            HeroId heroId,
            ProjectBindingContext project,
            CancellationToken cancellationToken = default)
        {
            SkillProgressionCalls++;
            SkillProgressionHeroId = heroId;
            SkillProgressionProject = project;
            return Task.FromResult(new HeroSkillProgressionReadResult(
                "Nova",
                project.DisplayName,
                [
                    new SkillProgressionReadRow(
                        "coding",
                        new SkillProgressionReadSnapshot(
                            Xp: 50,
                            Level: 2,
                            IsLevelCapped: false,
                            LevelXp: 0,
                            NextLevelXpRequired: 75),
                        new SkillProjectContributionReadSnapshot(Xp: 12)),
                ]));
        }

        private static InvalidOperationException Unused() => new("Unexpected state-store call.");

        public Task<BootstrapResult> BootstrapAsync(BootstrapStoreCommand command, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task<ConfigureResult> ConfigureAsync(ConfigureRequest request, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task<RuntimeContextResult> GetRuntimeContextAsync(ProjectBindingContext project, CancellationToken cancellationToken = default) => throw Unused();
        public Task<ProjectQuestHistoryResult> GetProjectQuestHistoryAsync(ProjectBindingContext project, CancellationToken cancellationToken = default) => throw Unused();
        public Task<QuestHistoryDetailResult?> GetQuestHistoryDetailAsync(QuestId questId, ProjectBindingContext project, CancellationToken cancellationToken = default) => throw Unused();
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
