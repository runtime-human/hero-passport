using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Web.Services;
using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class HistoryServiceTests
{
    private static readonly ProjectBindingContext Project =
        new("Demo", new string('a', 64), "project-identity/1");

    [Fact]
    public async Task InvalidDetailRouteFailsBeforeRuntimeOrHistoryLookup()
    {
        var state = new FakeStateStore(Context(setupCompleted: true));
        var service = Service(state);

        var result = await service.LoadDetailAsync(
            "not-a-quest",
            TestContext.Current.CancellationToken);

        Assert.Equal(QuestHistoryPageStatus.Invalid, result.Status);
        Assert.Null(result.Page);
        Assert.Equal(0, state.ContextCalls);
        Assert.Equal(0, state.DetailCalls);
    }

    [Fact]
    public async Task SetupRequiredListStopsBeforeHistoryLookup()
    {
        var state = new FakeStateStore(Context(setupCompleted: false));
        var service = Service(state);

        var result = await service.LoadListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(QuestHistoryPageStatus.SetupRequired, result.Status);
        Assert.Null(result.Page);
        Assert.Equal(1, state.ContextCalls);
        Assert.Equal(0, state.ListCalls);
    }

    [Fact]
    public async Task SetupRequiredDetailParsesSelectorThenStopsBeforeHistoryLookup()
    {
        var state = new FakeStateStore(Context(setupCompleted: false));
        var service = Service(state);

        var result = await service.LoadDetailAsync(
            QuestId.New().ToString(),
            TestContext.Current.CancellationToken);

        Assert.Equal(QuestHistoryPageStatus.SetupRequired, result.Status);
        Assert.Null(result.Page);
        Assert.Equal(1, state.ContextCalls);
        Assert.Equal(0, state.DetailCalls);
    }

    [Fact]
    public async Task ReadyListMapsOnlyApprovedPresentationFactsAndPreservesOrder()
    {
        var newer = QuestId.New();
        var older = QuestId.New();
        var state = new FakeStateStore(Context(setupCompleted: true))
        {
            ListResult = new ProjectQuestHistoryResult(
                "Canonical Demo",
                [
                    new ProjectQuestHistoryItem(
                        newer,
                        "Nova",
                        "coding",
                        "Newest Quest",
                        "open",
                        null,
                        null,
                        new DateTimeOffset(2026, 9, 17, 10, 0, 0, TimeSpan.Zero),
                        null),
                    new ProjectQuestHistoryItem(
                        older,
                        "Bolt",
                        "review",
                        "Older Quest",
                        "finished",
                        "success",
                        95,
                        new DateTimeOffset(2026, 9, 17, 9, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 9, 17, 9, 30, 0, TimeSpan.Zero)),
                ])
        };
        var service = Service(state);

        var result = await service.LoadListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(QuestHistoryPageStatus.Ready, result.Status);
        Assert.NotNull(result.Page);
        Assert.Equal("Canonical Demo", result.Page.ProjectDisplayName);
        Assert.Equal(2, result.Page.Items.Count);
        Assert.Equal(newer.ToString(), result.Page.Items[0].QuestId);
        Assert.Equal("Newest Quest", result.Page.Items[0].Title);
        Assert.Equal(older.ToString(), result.Page.Items[1].QuestId);
        Assert.Equal("success", result.Page.Items[1].Result);
        Assert.Equal(95, result.Page.Items[1].XpGained);
        Assert.Equal(1, state.ContextCalls);
        Assert.Equal(1, state.ListCalls);
        Assert.Equal(Project, state.LastListProject);
    }

    [Fact]
    public async Task CanonicalUnavailableDetailIsNotFound()
    {
        var state = new FakeStateStore(Context(setupCompleted: true));
        var service = Service(state);

        var result = await service.LoadDetailAsync(
            QuestId.New().ToString(),
            TestContext.Current.CancellationToken);

        Assert.Equal(QuestHistoryPageStatus.NotFound, result.Status);
        Assert.Null(result.Page);
        Assert.Equal(1, state.ContextCalls);
        Assert.Equal(1, state.DetailCalls);
    }

    [Fact]
    public async Task ReadyDetailMapsBoundedReportWithoutPersistenceIdentity()
    {
        var questId = QuestId.New();
        var started = new DateTimeOffset(2026, 9, 17, 9, 0, 0, TimeSpan.Zero);
        var finished = started.AddMinutes(30);
        var state = new FakeStateStore(Context(setupCompleted: true))
        {
            DetailResult = new QuestHistoryDetailResult(
                questId,
                "Nova",
                "Canonical Demo",
                "review",
                "Review history",
                "Qualify the bounded history surface.",
                "finished",
                started,
                finished,
                new QuestHistoryReport(
                    "partial",
                    "History detail remains bounded and source-private.",
                    true,
                    1,
                    2,
                    "passed",
                    "observed",
                    "failed",
                    "observed",
                    47,
                    ["review", "scope_control"]))
        };
        var service = Service(state);

        var result = await service.LoadDetailAsync(
            questId.ToString(),
            TestContext.Current.CancellationToken);

        Assert.Equal(QuestHistoryPageStatus.Ready, result.Status);
        Assert.NotNull(result.Page);
        Assert.Equal("Nova", result.Page.HeroName);
        Assert.Equal("Canonical Demo", result.Page.ProjectDisplayName);
        Assert.Equal("Review history", result.Page.Title);
        Assert.Equal("Qualify the bounded history surface.", result.Page.Goal);
        Assert.Equal(finished, result.Page.FinishedAtUtc);
        Assert.NotNull(result.Page.Report);
        Assert.Equal("partial", result.Page.Report.Result);
        Assert.Equal(47, result.Page.Report.XpGained);
        Assert.Equal(["review", "scope_control"], result.Page.Report.SkillsUsed);
        Assert.Equal(1, state.DetailCalls);
        Assert.Equal(questId, state.LastDetailQuestId);
        Assert.Equal(Project, state.LastDetailProject);
    }

    private static HeroPassportHistoryService Service(FakeStateStore state) =>
        new(new HeroPassportApplication(state, TimeProvider.System), Project);

    private static RuntimeContextResult Context(bool setupCompleted) =>
        new(
            "0.2-test",
            "HP-MCP/2",
            "hero-passport-skill/1",
            setupCompleted,
            setupCompleted
                ? new SettingsSnapshot("en-US", "rpg_engineering", true, true)
                : null,
            setupCompleted
                ? new HeroIdentitySnapshot(HeroId.New(), "Default")
                : null,
            new ProjectContextSnapshot("Demo"),
            [],
            new RuleVersions("r", "h", "s", "a", "t", "st", "u", "rank"));

    private sealed class FakeStateStore(RuntimeContextResult runtimeContext) : IHeroPassportStateStore
    {
        public int ContextCalls { get; private set; }
        public int ListCalls { get; private set; }
        public int DetailCalls { get; private set; }
        public ProjectBindingContext? LastListProject { get; private set; }
        public QuestId? LastDetailQuestId { get; private set; }
        public ProjectBindingContext? LastDetailProject { get; private set; }
        public ProjectQuestHistoryResult ListResult { get; set; } =
            new("Demo", []);
        public QuestHistoryDetailResult? DetailResult { get; set; }

        public Task<RuntimeContextResult> GetRuntimeContextAsync(
            ProjectBindingContext project,
            CancellationToken cancellationToken = default)
        {
            ContextCalls++;
            return Task.FromResult(runtimeContext);
        }

        public Task<ProjectQuestHistoryResult> GetProjectQuestHistoryAsync(
            ProjectBindingContext project,
            CancellationToken cancellationToken = default)
        {
            ListCalls++;
            LastListProject = project;
            return Task.FromResult(ListResult);
        }

        public Task<QuestHistoryDetailResult?> GetQuestHistoryDetailAsync(
            QuestId questId,
            ProjectBindingContext project,
            CancellationToken cancellationToken = default)
        {
            DetailCalls++;
            LastDetailQuestId = questId;
            LastDetailProject = project;
            return Task.FromResult(DetailResult);
        }

        private static InvalidOperationException Unused() => new("Unexpected state-store call.");

        public Task<BootstrapResult> BootstrapAsync(BootstrapStoreCommand command, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task<ConfigureResult> ConfigureAsync(ConfigureRequest request, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task<HeroSkillProgressionReadResult> GetSkillProgressionAsync(HeroId heroId, ProjectBindingContext project, CancellationToken cancellationToken = default) => throw Unused();
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
