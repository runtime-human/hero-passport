using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class TrustStrainStreakPersistenceTests
{
    [Fact]
    public async Task CleanObservedSuccessPersistsCanonicalTrustStrainAndStreak()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var hero = (await app.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
                token)).Hero;
            var project = Project('c');
            var quest = await StartAsync(app, hero.HeroId, project, token);

            var finish = await app.FinishQuestAsync(
                FinishRequest(quest.QuestId, "success", observedTests: true, scopeViolations: 0, userCorrections: 0),
                project,
                token);

            Assert.Equal(50, finish.TrustStrain.TrustBefore);
            Assert.Equal(52, finish.TrustStrain.TrustAfter);
            Assert.Equal(20, finish.TrustStrain.StrainBefore);
            Assert.Equal(18, finish.TrustStrain.StrainAfter);
            Assert.Equal("trust-strain/1.0.0", finish.TrustStrain.RuleVersion);
            Assert.Collection(
                finish.TrustStrain.Components,
                component => AssertComponent(component, "success_outcome", 1, -1),
                component => AssertComponent(component, "clean_success_bonus", 1, -1),
                component => AssertComponent(component, "observed_tests_passed_bonus", 1, 0),
                component => AssertComponent(component, "positive_trust_cap_adjustment", -1, 0));

            Assert.Equal(0, finish.Streak.Before);
            Assert.Equal(1, finish.Streak.After);
            Assert.Equal("streak/1.0.0", finish.Streak.RuleVersion);

            var card = await app.GetCardAsync(hero.HeroId, project, token);
            Assert.Equal(52, card.Hero.Trust);
            Assert.Equal(18, card.Hero.Strain);
            Assert.Equal(1, card.Hero.SuccessStreak);
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Theory]
    [InlineData("partial", false, 0, 0, 50, 21, 0, "partial_outcome")]
    [InlineData("failed", false, 0, 1, 49, 23, 0, "failed_outcome,user_correction_penalty")]
    [InlineData("blocked", false, 0, 0, 50, 20, 0, "")]
    [InlineData("abandoned", true, 3, 3, 50, 20, 0, "")]
    public async Task OutcomeGoldensUseBoundedSignalsAndAbandonedIsNeutral(
        string result,
        bool observedTests,
        int scopeViolations,
        int userCorrections,
        int expectedTrust,
        int expectedStrain,
        long expectedStreak,
        string expectedComponentKeys)
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var hero = (await app.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
                token)).Hero;
            var project = Project('d');
            var quest = await StartAsync(app, hero.HeroId, project, token);

            var finish = await app.FinishQuestAsync(
                FinishRequest(quest.QuestId, result, observedTests, scopeViolations, userCorrections),
                project,
                token);

            Assert.Equal(expectedTrust, finish.TrustStrain.TrustAfter);
            Assert.Equal(expectedStrain, finish.TrustStrain.StrainAfter);
            Assert.Equal(expectedStreak, finish.Streak.After);
            Assert.Equal(
                expectedComponentKeys.Length == 0 ? [] : expectedComponentKeys.Split(','),
                finish.TrustStrain.Components.Select(static component => component.Key).ToArray());
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task SuccessStreakIncrementsAcrossSuccessesAndResetsOnNonSuccess()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var hero = (await app.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
                token)).Hero;
            var project = Project('e');

            var first = await FinishNewQuestAsync(app, hero.HeroId, project, "success", token);
            var second = await FinishNewQuestAsync(app, hero.HeroId, project, "success", token);
            var third = await FinishNewQuestAsync(app, hero.HeroId, project, "partial", token);

            Assert.Equal((0L, 1L), (first.Streak.Before, first.Streak.After));
            Assert.Equal((1L, 2L), (second.Streak.Before, second.Streak.After));
            Assert.Equal((2L, 0L), (third.Streak.Before, third.Streak.After));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    private static async Task<FinishQuestResult> FinishNewQuestAsync(
        HeroPassportApplication app,
        HeroId heroId,
        ProjectBindingContext project,
        string result,
        CancellationToken token)
    {
        var quest = await StartAsync(app, heroId, project, token);
        return await app.FinishQuestAsync(
            FinishRequest(quest.QuestId, result, observedTests: false, scopeViolations: 0, userCorrections: 0),
            project,
            token);
    }

    private static async Task<QuestSnapshot> StartAsync(
        HeroPassportApplication app,
        HeroId heroId,
        ProjectBindingContext project,
        CancellationToken token) =>
        (await app.StartQuestAsync(
            new StartQuestRequest(
                MutationRequestId.New(),
                heroId,
                "coding",
                "Qualify RPG state",
                "Persist deterministic Trust, Strain and Success Streak progression."),
            project,
            token)).Quest;

    private static FinishQuestRequest FinishRequest(
        QuestId questId,
        string result,
        bool observedTests,
        int scopeViolations,
        int userCorrections) =>
        new(
            MutationRequestId.New(),
            questId,
            result,
            "Completed the deterministic Trust, Strain and Success Streak slice with a clear bounded summary.",
            new FinishQuestMetrics(
                TestsMentioned: observedTests,
                ScopeViolations: scopeViolations,
                UserCorrections: userCorrections,
                BuildStatus: "not_run",
                BuildEvidence: "none",
                TestsStatus: observedTests ? "passed" : "not_run",
                TestsEvidence: observedTests ? "observed" : "none"),
            ["coding"]);

    private static ProjectBindingContext Project(char fingerprintCharacter) =>
        new("Trust Project", new string(fingerprintCharacter, 64), "project-identity/1");

    private static void AssertComponent(
        TrustStrainComponentSnapshot component,
        string key,
        int trustDelta,
        int strainDelta)
    {
        Assert.Equal(key, component.Key);
        Assert.Equal(trustDelta, component.TrustDelta);
        Assert.Equal(strainDelta, component.StrainDelta);
    }
}
