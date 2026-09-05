using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class FinishQuestRestartReplayTests
{
    [Fact]
    public async Task FreshApplicationReplaysCommittedFinishWithoutRecalculation()
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
            var project = new ProjectBindingContext("Project", new string('9', 64), "project-identity/1");
            var quest = (await app.StartQuestAsync(
                new StartQuestRequest(
                    MutationRequestId.New(),
                    hero.HeroId,
                    "coding",
                    "Quest",
                    "Prove persisted Finish replay after an Application restart"),
                project,
                token)).Quest;
            var request = new FinishQuestRequest(
                MutationRequestId.New(),
                quest.QuestId,
                "success",
                "Persist the terminal Quest result and replay it after constructing a fresh Application instance.",
                new FinishQuestMetrics(true, 0, 0, "passed", "observed", "passed", "observed"),
                ["coding"]);

            var committed = await app.FinishQuestAsync(request, project, token);
            var replay = await TestRuntime.CreateApplication(path).FinishQuestAsync(request, project, token);

            Assert.False(committed.Replayed);
            Assert.True(replay.Replayed);
            Assert.False(replay.AlreadyFinalized);
            Assert.Equal(committed, replay with { Replayed = false });
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }
}
