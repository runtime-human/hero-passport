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
            var project = new ProjectBindingContext("Trust Project", new string('c', 64), "project-identity/1");
            var quest = (await app.StartQuestAsync(
                new StartQuestRequest(
                    MutationRequestId.New(),
                    hero.HeroId,
                    "coding",
                    "Qualify RPG state",
                    "Persist deterministic Trust, Strain and Success Streak progression."),
                project,
                token)).Quest;

            var finish = await app.FinishQuestAsync(
                new FinishQuestRequest(
                    MutationRequestId.New(),
                    quest.QuestId,
                    "success",
                    "Completed the deterministic Trust, Strain and Success Streak slice with directly observed passing tests.",
                    new FinishQuestMetrics(
                        TestsMentioned: true,
                        ScopeViolations: 0,
                        UserCorrections: 0,
                        BuildStatus: "passed",
                        BuildEvidence: "observed",
                        TestsStatus: "passed",
                        TestsEvidence: "observed"),
                    ["coding"]),
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

            var card = await app.GetHeroCardAsync(hero.HeroId, project, token);
            Assert.Equal(52, card.Hero.Trust);
            Assert.Equal(18, card.Hero.Strain);
            Assert.Equal(1, card.Hero.SuccessStreak);
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

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
