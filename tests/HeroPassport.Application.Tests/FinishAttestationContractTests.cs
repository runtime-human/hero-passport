using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class FinishAttestationContractTests
{
    [Fact]
    public async Task CrossFieldAttestationContradictionsReturnHp120BeforePersistence()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var hero = (await app.BootstrapAsync(new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true), token)).Hero;
            var project = new ProjectBindingContext("Project", new string('c', 64), "project-identity/1");
            var quest = (await app.StartQuestAsync(
                new StartQuestRequest(MutationRequestId.New(), hero.HeroId, "coding", "Quest", "Validate bounded Finish attestations"),
                project,
                token)).Quest;

            var valid = new FinishQuestRequest(
                MutationRequestId.New(),
                quest.QuestId,
                "success",
                "This summary is deliberately long enough to satisfy the normal SafeText boundary.",
                new FinishQuestMetrics(true, 0, 0, "passed", "observed", "passed", "observed"),
                ["coding"]);

            await AssertHp120Async(app, project, valid with
            {
                Metrics = valid.Metrics with { BuildStatus = "not_run", BuildEvidence = "observed" }
            }, token);
            await AssertHp120Async(app, project, valid with
            {
                FinishRequestId = MutationRequestId.New(),
                Metrics = valid.Metrics with { BuildStatus = "passed", BuildEvidence = "none" }
            }, token);
            await AssertHp120Async(app, project, valid with
            {
                FinishRequestId = MutationRequestId.New(),
                Metrics = valid.Metrics with { TestsMentioned = false }
            }, token);
            await AssertHp120Async(app, project, valid with
            {
                FinishRequestId = MutationRequestId.New(),
                Metrics = valid.Metrics with { TestsStatus = "not_run", TestsEvidence = "reported" }
            }, token);

            var context = await app.GetRuntimeContextAsync(project, token);
            Assert.Single(context.OpenQuests);
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    private static async Task AssertHp120Async(
        HeroPassportApplication app,
        ProjectBindingContext project,
        FinishQuestRequest request,
        CancellationToken token)
    {
        var error = await Assert.ThrowsAsync<HeroPassportException>(() => app.FinishQuestAsync(request, project, token));
        Assert.Equal("HP120", error.Code);
    }
}
