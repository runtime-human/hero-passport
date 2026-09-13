using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Web.Services;
using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class StartQuestServiceTests
{
    private static readonly ProjectBindingContext Project =
        new("Demo", new string('a', 64), "project-identity/1");

    [Fact]
    public async Task PrepareNormalizesAndStoresIntentWithoutMutation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var hero = new HeroIdentitySnapshot(HeroId.New(), "Ada");
        var state = new FakeStateStore(Context(hero));
        var application = new HeroPassportApplication(state, TimeProvider.System);
        var pending = new PendingStartQuestStore(TimeProvider.System);
        var service = new HeroPassportStartQuestService(application, Project, pending);

        var result = await service.PrepareAsync(
            new StartQuestForm
            {
                QuestType = "coding",
                Title = "  Build   parser  ",
                Goal = "  Ship   bounded   parser  ",
            },
            cancellationToken);

        Assert.Equal(PrepareStartQuestWebStatus.Prepared, result.Status);
        Assert.NotNull(result.Handle);
        Assert.Equal(0, state.StartCalls);
        var lookup = pending.Lookup(result.Handle);
        Assert.Equal(PendingStartQuestAccessStatus.Found, lookup.Status);
        Assert.NotNull(lookup.Entry);
        Assert.Equal(hero.HeroId, lookup.Entry.Prepared.HeroId);
        Assert.Equal("Build parser", lookup.Entry.Prepared.Title);
        Assert.Equal("Ship bounded parser", lookup.Entry.Prepared.Goal);
        Assert.Equal("Ada", lookup.Entry.HeroName);
        Assert.Equal("Demo", lookup.Entry.ProjectDisplayName);
    }

    [Fact]
    public async Task PrepareRefusesCurrentHeroWithOpenQuestWithoutMutation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var hero = new HeroIdentitySnapshot(HeroId.New(), "Ada");
        var open = new OpenQuestContext(
            QuestId.New(), hero.HeroId, hero.Name, "coding", "Existing", "Goal",
            DateTimeOffset.UtcNow, "en-US");
        var state = new FakeStateStore(Context(hero, [open]));
        var service = new HeroPassportStartQuestService(
            new HeroPassportApplication(state, TimeProvider.System),
            Project,
            new PendingStartQuestStore(TimeProvider.System));

        var result = await service.PrepareAsync(ValidForm(), cancellationToken);

        Assert.Equal(PrepareStartQuestWebStatus.Conflict, result.Status);
        Assert.Null(result.Handle);
        Assert.Equal(0, state.StartCalls);
    }

    [Fact]
    public async Task CommitUsesPreparedHeroAndRequestIdAfterActiveHeroChanges()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var heroA = new HeroIdentitySnapshot(HeroId.New(), "Ada");
        var heroB = new HeroIdentitySnapshot(HeroId.New(), "Grace");
        var state = new FakeStateStore(Context(heroA));
        var pending = new PendingStartQuestStore(TimeProvider.System);
        var service = new HeroPassportStartQuestService(
            new HeroPassportApplication(state, TimeProvider.System),
            Project,
            pending);

        var prepared = await service.PrepareAsync(ValidForm(), cancellationToken);
        Assert.Equal(PrepareStartQuestWebStatus.Prepared, prepared.Status);
        Assert.NotNull(prepared.Handle);
        var captured = pending.Lookup(prepared.Handle).Entry!.Prepared;
        state.RuntimeContext = Context(heroB);

        var committed = await service.CommitAsync(prepared.Handle, cancellationToken);

        Assert.Equal(CommitStartQuestWebStatus.Success, committed.Status);
        Assert.Equal(1, state.StartCalls);
        Assert.NotNull(state.LastStart);
        Assert.Equal(heroA.HeroId, state.LastStart.HeroId);
        Assert.Equal(captured.StartRequestId, state.LastStart.RequestId);
    }

    [Fact]
    public async Task DuplicateSuccessfulConfirmDoesNotCallApplicationTwice()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var hero = new HeroIdentitySnapshot(HeroId.New(), "Ada");
        var state = new FakeStateStore(Context(hero));
        var pending = new PendingStartQuestStore(TimeProvider.System);
        var service = new HeroPassportStartQuestService(
            new HeroPassportApplication(state, TimeProvider.System),
            Project,
            pending);
        var prepared = await service.PrepareAsync(ValidForm(), cancellationToken);

        var first = await service.CommitAsync(prepared.Handle!, cancellationToken);
        var duplicate = await service.CommitAsync(prepared.Handle!, cancellationToken);

        Assert.Equal(CommitStartQuestWebStatus.Success, first.Status);
        Assert.Equal(CommitStartQuestWebStatus.Success, duplicate.Status);
        Assert.Equal(1, state.StartCalls);
    }

    [Fact]
    public async Task OpenQuestConflictIsBoundedAndRetainsPreparedRequestForRetry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var hero = new HeroIdentitySnapshot(HeroId.New(), "Ada");
        var state = new FakeStateStore(Context(hero)) { StartErrorCode = "HP133" };
        var pending = new PendingStartQuestStore(TimeProvider.System);
        var service = new HeroPassportStartQuestService(
            new HeroPassportApplication(state, TimeProvider.System),
            Project,
            pending);
        var prepared = await service.PrepareAsync(ValidForm(), cancellationToken);
        var requestId = pending.Lookup(prepared.Handle!).Entry!.Prepared.StartRequestId;

        var conflict = await service.CommitAsync(prepared.Handle!, cancellationToken);
        state.StartErrorCode = null;
        var retried = await service.CommitAsync(prepared.Handle!, cancellationToken);

        Assert.Equal(CommitStartQuestWebStatus.Conflict, conflict.Status);
        Assert.Equal(CommitStartQuestWebStatus.Success, retried.Status);
        Assert.Equal(2, state.StartCalls);
        Assert.Equal(requestId, state.LastStart!.RequestId);
    }

    private static StartQuestForm ValidForm() => new()
    {
        QuestType = "coding",
        Title = "Build parser",
        Goal = "Ship bounded parser",
    };

    private static RuntimeContextResult Context(
        HeroIdentitySnapshot? activeHero,
        IReadOnlyList<OpenQuestContext>? openQuests = null) =>
        new(
            "0.2-test",
            "HP-MCP/2",
            "hero-passport-skill/1",
            activeHero is not null,
            activeHero is null ? null : new SettingsSnapshot("en-US", "rpg_engineering", true, true),
            activeHero,
            new ProjectContextSnapshot("Demo"),
            openQuests ?? [],
            new RuleVersions("r", "h", "s", "a", "t", "st", "u", "rank"));

    private sealed class FakeStateStore(RuntimeContextResult runtimeContext) : IHeroPassportStateStore
    {
        public RuntimeContextResult RuntimeContext { get; set; } = runtimeContext;
        public int StartCalls { get; private set; }
        public StartQuestStoreCommand? LastStart { get; private set; }
        public string? StartErrorCode { get; set; }

        public Task<RuntimeContextResult> GetRuntimeContextAsync(
            ProjectBindingContext project,
            CancellationToken cancellationToken = default) => Task.FromResult(RuntimeContext);

        public Task<StartQuestResult> StartQuestAsync(
            StartQuestStoreCommand command,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            StartCalls++;
            LastStart = command;
            if (StartErrorCode is not null)
            {
                throw new HeroPassportException(StartErrorCode, "bounded test conflict");
            }

            return Task.FromResult(new StartQuestResult(
                new StartedQuestSnapshot(
                    QuestId.New(), command.HeroId, command.QuestType, command.Title, command.Goal,
                    now, "en-US"),
                new HeroIdentitySnapshot(command.HeroId, "Prepared Hero"),
                false));
        }

        private static InvalidOperationException Unused() => new("Unexpected state-store call.");

        public Task<BootstrapResult> BootstrapAsync(BootstrapStoreCommand command, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task<ConfigureResult> ConfigureAsync(ConfigureRequest request, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task<CreateHeroResult> CreateHeroAsync(CreateHeroStoreCommand command, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task ActivateHeroAsync(HeroId heroId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task<HeroListResult> ListHeroesAsync(CancellationToken cancellationToken = default) => throw Unused();
        public Task<HeroPreferenceChangeResult> ActivateHeroPreferenceAsync(HeroId heroId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task<HeroPreferenceChangeResult> ArchiveHeroAsync(HeroId heroId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task<HeroPreferenceChangeResult> RestoreHeroAsync(HeroId heroId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task DeleteHeroPermanentlyAsync(HeroId heroId, CancellationToken cancellationToken = default) => throw Unused();
        public Task<HeroCardResult> GetCardAsync(HeroId heroId, ProjectBindingContext project, CancellationToken cancellationToken = default) => throw Unused();
        public Task<FinishQuestResult> FinishQuestAsync(FinishQuestStoreCommand command, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
        public Task<string> GetQuestLocaleAsync(QuestId questId, CancellationToken cancellationToken = default) => throw Unused();
    }
}
