using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class HeroCardTopSkillsTests
{
    [Fact]
    public async Task CardReturnsDeterministicGlobalAndProjectScopedTopThreeSkills()
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

            var projectA = new ProjectBindingContext("Project A", new string('a', 64), "project-identity/1");
            var projectB = new ProjectBindingContext("Project B", new string('b', 64), "project-identity/1");

            foreach (var skill in new[] { "review", "documentation", "coding", "planning" })
            {
                await FinishCleanQuestAsync(app, hero.HeroId, projectA, "coding", skill, token);
            }

            await FinishCleanQuestAsync(app, hero.HeroId, projectB, "debugging", "review", token);
            await FinishCleanQuestAsync(app, hero.HeroId, projectB, "debugging", "tool_use", token);

            var card = await app.GetCardAsync(hero.HeroId, projectA, token);

            Assert.Collection(
                card.Hero.TopSkills,
                skill => AssertSkill(skill, "review", 200, 3, 100),
                skill => AssertSkill(skill, "tool_use", 105, 2, 75),
                skill => AssertSkill(skill, "coding", 95, 2, 75));

            Assert.Collection(
                card.Project.TopSkills,
                skill => AssertSkill(skill, "coding", 95, 2, 75),
                skill => AssertSkill(skill, "documentation", 95, 2, 75),
                skill => AssertSkill(skill, "planning", 95, 2, 75));

            Assert.DoesNotContain(card.Project.TopSkills, static skill => skill.SkillKey == "review");
            Assert.DoesNotContain(card.Project.TopSkills, static skill => skill.SkillKey == "tool_use");
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    private static async Task FinishCleanQuestAsync(
        HeroPassportApplication app,
        HeroId heroId,
        ProjectBindingContext project,
        string questType,
        string skillKey,
        CancellationToken token)
    {
        var quest = (await app.StartQuestAsync(
            new StartQuestRequest(
                MutationRequestId.New(),
                heroId,
                questType,
                $"Qualify {skillKey}",
                $"Qualify deterministic Card top Skill projection for {skillKey}."),
            project,
            token)).Quest;

        await app.FinishQuestAsync(
            new FinishQuestRequest(
                MutationRequestId.New(),
                quest.QuestId,
                "success",
                "Completed deterministic Card top Skill qualification with observed passing tests and clean scope.",
                new FinishQuestMetrics(
                    TestsMentioned: true,
                    ScopeViolations: 0,
                    UserCorrections: 0,
                    BuildStatus: "not_run",
                    BuildEvidence: "none",
                    TestsStatus: "passed",
                    TestsEvidence: "observed"),
                [skillKey]),
            project,
            token);
    }

    private static void AssertSkill(
        CardSkillSnapshot skill,
        string expectedKey,
        long expectedXp,
        int expectedLevel,
        long expectedNextLevelXpRequired)
    {
        Assert.Equal(expectedKey, skill.SkillKey);
        Assert.Equal(expectedXp, skill.Xp);
        Assert.Equal(expectedLevel, skill.Level);
        Assert.False(skill.IsLevelCapped);
        Assert.Equal(expectedNextLevelXpRequired, skill.NextLevelXpRequired);
    }
}
