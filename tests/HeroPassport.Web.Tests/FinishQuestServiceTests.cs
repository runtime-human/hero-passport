using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Web.Services;
using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class FinishQuestServiceTests
{
    private static readonly ProjectBindingContext Project =
        new("Demo", new string('a', 64), "project-identity/1");

    [Fact]
    public async Task InvalidRouteFailsBeforeRuntimeLookup()
    {
        var state = new FakeStateStore(Context());
        var service = Service(state, out _);

        var result = await service.LoadAsync("not-a-quest", TestContext.Current.CancellationToken);

        Assert.Equal(FinishQuestPageStatus.Invalid, result.Status);
        Assert.Equal(0, state.ContextCalls);
    }

    [Fact]
    public async Task CanonicalUnavailableQuestIsNotFound()
    {
        var state = new FakeStateStore(Context());
        var service = Service(state, out _);

        var result = await service.LoadAsync(QuestId.New().ToString(), TestContext.Current.CancellationToken);

        Assert.Equal(FinishQuestPageStatus.NotFound, result.Status);
        Assert.Equal(1, state.ContextCalls);
    }

    [Fact]
    public async Task PrepareNormalizesAndStoresIntentWithoutFinishMutation()
    {
        var quest = OpenQuest();
        var state = new FakeStateStore(Context([quest]));
        var service = Service(state, out var pending);

        var result = await service.PrepareAsync(
            quest.QuestId.ToString(),
            ValidForm(summary: "  Ship   bounded   parser  "),
            TestContext.Current.CancellationToken);

        Assert.Equal(PrepareFinishQuestWebStatus.Prepared, result.Status);
        Assert.NotNull(result.Handle);
        Assert.Equal(0, state.FinishCalls);
        var lookup = pending.Lookup(result.Handle);
        Assert.Equal(PendingFinishQuestAccessStatus.Found, lookup.Status);
        Assert.NotNull(lookup.Entry);
        Assert.Equal(quest.QuestId, lookup.Entry.Prepared.QuestId);
        Assert.Equal("Ship bounded parser", lookup.Entry.Prepared.Summary);
        Assert.Equal(["coding", "testing_awareness"], lookup.Entry.Prepared.SkillsUsed);
        Assert.Equal(quest.HeroName, lookup.Entry.HeroName);
        Assert.Equal(quest.Title, lookup.Entry.QuestTitle);
    }

    [Fact]
    public async Task CommitUsesPreparedRequestAndDuplicateConfirmDoesNotFinishTwice()
    {
        var quest = OpenQuest();
        var state = new FakeStateStore(Context([quest]));
        var service = Service(state, out var pending);
        var prepared = await service.PrepareAsync(
            quest.QuestId.ToString(), ValidForm(), TestContext.Current.CancellationToken);
        var requestId = pending.Lookup(prepared.Handle!).Entry!.Prepared.FinishRequestId;

        var first = await service.CommitAsync(prepared.Handle!, TestContext.Current.CancellationToken);
        var duplicate = await service.CommitAsync(prepared.Handle!, TestContext.Current.CancellationToken);

        Assert.Equal(CommitFinishQuestWebStatus.Success, first.Status);
        Assert.Equal(CommitFinishQuestWebStatus.Success, duplicate.Status);
        Assert.Equal(1, state.FinishCalls);
        Assert.Single(state.FinishCommands);
        Assert.Equal(requestId, state.FinishCommands[0].RequestId);
    }

    [Fact]
    public async Task TerminalFinalizationConflictRemovesPendingHandle()
    {
        var quest = OpenQuest();
        var state = new FakeStateStore(Context([quest])) { FinishErrorCode = "HP136" };
        var service = Service(state, out var pending);
        var prepared = await service.PrepareAsync(
            quest.QuestId.ToString(), ValidForm(), TestContext.Current.CancellationToken);

        var conflict = await service.CommitAsync(prepared.Handle!, TestContext.Current.CancellationToken);

        Assert.Equal(CommitFinishQuestWebStatus.Conflict, conflict.Status);
        Assert.Equal(PendingFinishQuestAccessStatus.Gone, pending.Lookup(prepared.Handle).Status);
        Assert.Equal(1, state.FinishCalls);
    }

    [Fact]
    public async Task UnknownPostCommitFailureReleasesSameFinishRequestForReplay()
    {
        var quest = OpenQuest();
        var state = new FakeStateStore(Context([quest])) { ThrowFirstLocaleRead = true };
        var service = Service(state, out var pending);
        var prepared = await service.PrepareAsync(
            quest.QuestId.ToString(), ValidForm(), TestContext.Current.CancellationToken);
        var requestId = pending.Lookup(prepared.Handle!).Entry!.Prepared.FinishRequestId;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CommitAsync(prepared.Handle!, TestContext.Current.CancellationToken));

        Assert.Equal(PendingFinishQuestAccessStatus.Found, pending.Lookup(prepared.Handle).Status);
        var replay = await service.CommitAsync(prepared.Handle!, TestContext.Current.CancellationToken);

        Assert.Equal(CommitFinishQuestWebStatus.Success, replay.Status);
        Assert.Equal(2, state.FinishCalls);
        Assert.Equal(2, state.FinishCommands.Count);
        Assert.All(state.FinishCommands, command => Assert.Equal(requestId, command.RequestId));
        Assert.Equal(PendingFinishQuestAccessStatus.Committed, pending.Lookup(prepared.Handle).Status);
    }

    [Fact]
    public async Task PrepareRejectsStaleTargetAndInvalidAttestationWithoutMutation()
    {
        var quest = OpenQuest();
        var state = new FakeStateStore(Context([quest]));
        var service = Service(state, out _);

        var stale = await service.PrepareAsync(
            QuestId.New().ToString(), ValidForm(), TestContext.Current.CancellationToken);
        var invalid = await service.PrepareAsync(
            quest.QuestId.ToString(),
            InvalidAttestationForm(),
            TestContext.Current.CancellationToken);

        Assert.Equal(PrepareFinishQuestWebStatus.NotFound, stale.Status);
        Assert.Equal(PrepareFinishQuestWebStatus.ValidationError, invalid.Status);
        Assert.Equal(0, state.FinishCalls);
    }

    private static HeroPassportFinishQuestService Service(
        FakeStateStore state,
        out PendingFinishQuestStore pending)
    {
        pending = new PendingFinishQuestStore(TimeProvider.System);
        return new HeroPassportFinishQuestService(
            new HeroPassportApplication(state, TimeProvider.System),
            Project,
            pending);
    }

    private static OpenQuestContext OpenQuest()
    {
        var heroId = HeroId.New();
        return new OpenQuestContext(
            QuestId.New(),
            heroId,
            "Ada",
            "coding",
            "Build parser",
            "Ship bounded parser",
            DateTimeOffset.UtcNow,
            "en-US");
    }

    private static FinishQuestForm ValidForm(string summary = "Done") => new()
    {
        Result = "success",
        Summary = summary,
        TestsMentioned = true,
        ScopeViolations = 0,
        UserCorrections = 0,
        BuildStatus = "passed",
        BuildEvidence = "observed",
        TestsStatus = "passed",
        TestsEvidence = "observed",
        SkillsUsed = ["coding", "testing_awareness"],
    };

    private static FinishQuestForm InvalidAttestationForm() => new()
    {
        Result = "success",
        Summary = "Done",
        TestsMentioned = false,
        ScopeViolations = 0,
        UserCorrections = 0,
        BuildStatus = "passed",
        BuildEvidence = "observed",
        TestsStatus = "passed",
        TestsEvidence = "observed",
        SkillsUsed = ["coding"],
    };

    private static RuntimeContextResult Context(IReadOnlyList<OpenQuestContext>? quests = null) =>
        new(
            "0.2-test",
            "HP-MCP/2",
            "hero-passport-skill/1",
            true,
            new SettingsSnapshot("en-US", "rpg_engineering", true, true),
            new HeroIdentitySnapshot(HeroId.New(), "Default"),
            new ProjectContextSnapshot("Demo"),
            quests ?? [],
            new RuleVersions("r", "h", "s", "a", "t", "st", "u", "rank"));

    private sealed class FakeStateStore(RuntimeContextResult runtimeContext) : IHeroPassportStateStore
    {
        public int ContextCalls { get; private set; }
        public int FinishCalls { get; private set; }
        public List<FinishQuestStoreCommand> FinishCommands { get; } = [];
        public string? FinishErrorCode { get; set; }
        public bool ThrowFirstLocaleRead { get; set; }
        private int _localeReads;

        public Task<RuntimeContextResult> GetRuntimeContextAsync(
            ProjectBindingContext project,
            CancellationToken cancellationToken = default)
        {
            ContextCalls++;
            return Task.FromResult(runtimeContext);
        }

        public Task<FinishQuestResult> FinishQuestAsync(
            FinishQuestStoreCommand command,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            FinishCalls++;
            FinishCommands.Add(command);
            if (FinishErrorCode is not null)
            {
                throw new HeroPassportException(FinishErrorCode, "bounded test conflict");
            }

            return Task.FromResult(Result(command.QuestId));
        }

        public Task<string> GetQuestLocaleAsync(
            QuestId questId,
            CancellationToken cancellationToken = default)
        {
            _localeReads++;
            if (ThrowFirstLocaleRead && _localeReads == 1)
            {
                throw new InvalidOperationException("Simulated response-tail failure after durable Finish.");
            }

            return Task.FromResult("en-US");
        }

        private static FinishQuestResult Result(QuestId questId) =>
            new(
                questId,
                "success",
                new QuestRewardSnapshot(100, 0, 0, 100, 1000, 100, [], "reward/2.0.0"),
                new HeroProgressSnapshot(
                    HeroId.New(), 0, 100, 1, 1, false, 100, 100,
                    "novice", "novice", "hero-progression/2.0.0", "rank/1.0.0"),
                new TrustStrainSnapshot(50, 50, 20, 20, [], "trust-strain/1.0.0"),
                new StreakSnapshot(0, 1, "streak/1.0.0"),
                [],
                [],
                [],
                null,
                [],
                false,
                false);

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
        public Task<StartQuestResult> StartQuestAsync(StartQuestStoreCommand command, DateTimeOffset now, CancellationToken cancellationToken = default) => throw Unused();
    }
}
