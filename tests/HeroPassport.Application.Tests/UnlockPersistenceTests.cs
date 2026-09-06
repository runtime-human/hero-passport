using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class UnlockPersistenceTests
{
    [Fact]
    public async Task RealQuestProgressionUnlocksMonotonicTraitsTitlesAndReplaysImmutableMilestones()
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
            var project = new ProjectBindingContext("Unlock Project", new string('f', 64), "project-identity/1");

            FinishQuestResult? fourth = null;
            FinishQuestResult? fifth = null;
            for (var questNumber = 1; questNumber <= 5; questNumber++)
            {
                var quest = (await app.StartQuestAsync(
                    new StartQuestRequest(
                        MutationRequestId.New(),
                        hero.HeroId,
                        "coding",
                        $"Unlock qualification {questNumber}",
                        "Qualify deterministic monotonic unlock persistence through normal Quest progression."),
                    project,
                    token)).Quest;

                var finished = await app.FinishQuestAsync(
                    FinishRequest(quest.QuestId),
                    project,
                    token);

                if (questNumber == 4)
                {
                    fourth = finished;
                }
                else if (questNumber == 5)
                {
                    fifth = finished;
                }
            }

            fourth = Assert.IsType<FinishQuestResult>(fourth);
            Assert.Empty(fourth.TraitsUnlocked);
            Assert.Equal(["skill_specialist"], fourth.TitlesUnlocked);
            Assert.Equal("skill_specialist", fourth.ActiveTitle);
            Assert.Contains(fourth.Milestones, static milestone =>
                milestone == new MilestoneSnapshot("title_unlocked", "title:skill_specialist"));

            fifth = Assert.IsType<FinishQuestResult>(fifth);
            Assert.Equal(["precise_executor", "test_scout", "steady_hand"], fifth.TraitsUnlocked);
            Assert.Empty(fifth.TitlesUnlocked);
            Assert.Equal("skill_specialist", fifth.ActiveTitle);
            Assert.Contains(fifth.Milestones, static milestone =>
                milestone == new MilestoneSnapshot("trait_unlocked", "trait:precise_executor"));
            Assert.Contains(fifth.Milestones, static milestone =>
                milestone == new MilestoneSnapshot("trait_unlocked", "trait:test_scout"));
            Assert.Contains(fifth.Milestones, static milestone =>
                milestone == new MilestoneSnapshot("trait_unlocked", "trait:steady_hand"));

            var replay = await app.FinishQuestAsync(FinishRequest(fifth.QuestId) with
            {
                FinishRequestId = MutationRequestId.New(),
            }, project, token);
            Assert.True(replay.AlreadyFinalized);
            Assert.Equal(fifth.TraitsUnlocked, replay.TraitsUnlocked);
            Assert.Equal(fifth.TitlesUnlocked, replay.TitlesUnlocked);
            Assert.Equal(fifth.ActiveTitle, replay.ActiveTitle);
            Assert.Equal(fifth.Milestones, replay.Milestones);

            var card = await app.GetCardAsync(hero.HeroId, project, token);
            Assert.Equal(["precise_executor", "steady_hand", "test_scout"], card.Hero.Traits);
            Assert.Equal(["skill_specialist"], card.Hero.Titles);
            Assert.Equal("skill_specialist", card.Hero.ActiveTitle);
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    private static FinishQuestRequest FinishRequest(QuestId questId) =>
        new(
            MutationRequestId.New(),
            questId,
            "success",
            "Completed the unlock qualification Quest with observed tests and clean scope for deterministic progression.",
            new FinishQuestMetrics(
                TestsMentioned: true,
                ScopeViolations: 0,
                UserCorrections: 0,
                BuildStatus: "not_run",
                BuildEvidence: "none",
                TestsStatus: "passed",
                TestsEvidence: "observed"),
            ["coding"]);
}
