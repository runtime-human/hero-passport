using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Web.Services;
using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class SkillProgressionServiceTests
{
    private static readonly ProjectBindingContext Project =
        new("Demo", new string('a', 64), "project-identity/1");

    [Fact]
    public async Task SetupRequiredStopsBeforeSkillRead()
    {
        var state = new FakeStateStore(Context(setupCompleted: false));
        var service = Service(state);

        var result = await service.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SkillProgressionPageStatus.SetupRequired, result.Status);
        Assert.Null(result.Page);
        Assert.Equal(1, state.ContextCalls);
        Assert.Equal(0, state.SkillCalls);
    }

    [Fact]
    public async Task ConfiguredContextWithoutActiveHeroIsBoundedInvalidState()
    {
        var state = new FakeStateStore(Context(setupCompleted: true, activeHero: null));
        var service = Service(state);

        var result = await service.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SkillProgressionPageStatus.Invalid, result.Status);
        Assert.Null(result.Page);
        Assert.Equal(1, state.ContextCalls);
        Assert.Equal(0, state.SkillCalls);
    }

    [Fact]
    public async Task ReadyMapsOnlyApprovedSkillPresentationFacts()
    {
        var hero = new HeroIdentitySnapshot(HeroId.New(), "Nova");
        var state = new FakeStateStore(Context(setupCompleted: true, activeHero: hero))
        {
            SkillResult = new HeroSkillProgressionReadResult(
                "Nova",
                "Canonical Demo",
                [
                    new SkillProgressionReadRow(
                        "coding",
                        new SkillProgressionReadSnapshot(152, 3, false, 27, 100),
                        new SkillProjectContributionReadSnapshot(57)),
                    new SkillProgressionReadRow(
                        "review",
                        new SkillProgressionReadSnapshot(0, 1, false, 0, 50),
                        new SkillProjectContributionReadSnapshot(0)),
                ])
        };
        var service = Service(state);

        var result = await service.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SkillProgressionPageStatus.Ready, result.Status);
        Assert.NotNull(result.Page);
        Assert.Equal("Nova", result.Page.HeroName);
        Assert.Equal("Canonical Demo", result.Page.ProjectDisplayName);
        Assert.Equal(2, result.Page.Skills.Count);
        Assert.Equal("coding", result.Page.Skills[0].SkillKey);
        Assert.Equal(152, result.Page.Skills[0].HeroXp);
        Assert.Equal(3, result.Page.Skills[0].HeroLevel);
        Assert.Equal(27, result.Page.Skills[0].HeroLevelXp);
        Assert.Equal(100, result.Page.Skills[0].HeroNextLevelXpRequired);
        Assert.Equal(57, result.Page.Skills[0].ProjectContributionXp);
        Assert.Equal(hero.HeroId, state.LastSkillHeroId);
        Assert.Equal(Project, state.LastSkillProject);
    }

    [Fact]
    public async Task HeroRemovedBetweenContextAndSkillReadIsBoundedInvalidState()
    {
        var hero = new HeroIdentitySnapshot(HeroId.New(), "Nova");
        var state = new FakeStateStore(Context(setupCompleted: true, activeHero: hero))
        {
            SkillErrorCode = "HP140",
        };
        var service = Service(state);

        var result = await service.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SkillProgressionPageStatus.Invalid, result.Status);
        Assert.Null(result.Page);
        Assert.Equal(1, state.ContextCalls);
        Assert.Equal(1, state.SkillCalls);
    }

    private static HeroPassportSkillProgressionService Service(FakeStateStore state) =>
        new(new HeroPassportApplication(state, TimeProvider.System), Project);

    private static RuntimeContextResult Context(
        bool setupCompleted,
        HeroIdentitySnapshot? activeHero = null) =>
        new(
            "0.2-test",
            "HP-MCP/2",
            "hero-passport-skill/1",
            setupCompleted,
            setupCompleted
                ? new SettingsSnapshot("en-US", "rpg_engineering", true, true)
                : null,
            setupCompleted ? activeHero : null,
            new ProjectContextSnapshot("Demo"),
            [],
            new RuleVersions("r", "h", "s", "a", "t", "st", "u", "rank"));

    private sealed class FakeStateStore(RuntimeContextResult runtimeContext) : IHeroPassportStateStore
    {
        public int ContextCalls { get; private set; }
        public int SkillCalls { get; private set; }
        public HeroId? LastSkillHeroId { get; private set; }
        public ProjectBindingContext? LastSkillProject { get; private set; }
        public string? SkillErrorCode { get; set; }
        public HeroSkillProgressionReadResult SkillResult { get; set; } =
            new("Nova", "Demo", []);

        public Task<RuntimeContextResult> GetRuntimeContextAsync(
            ProjectBindingContext project,
            CancellationToken cancellationToken = default)
        {
            ContextCalls++;
            return Task.FromResult(runtimeContext);
        }

        public Task<HeroSkillProgressionReadResult> GetSkillProgressionAsync(
            HeroId heroId,
            ProjectBindingContext project,
            CancellationToken cancellationToken = default)
        {
            SkillCalls++;
            LastSkillHeroId = heroId;
            LastSkillProject = project;
            if (SkillErrorCode is not null)
            {
                throw new HeroPassportException(SkillErrorCode, "bounded test failure");
            }

            return Task.FromResult(SkillResult);
        }

        private static InvalidOperationException Unused() => new("Unexpected state-store call.");

        public Task<BootstrapResult> BootstrapAsync(BootstrapStoreCommand command, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task<ConfigureResult> ConfigureAsync(ConfigureRequest request, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
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
