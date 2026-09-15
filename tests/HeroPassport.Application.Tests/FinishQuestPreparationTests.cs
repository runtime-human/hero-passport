using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class FinishQuestPreparationTests
{
    private static readonly ProjectBindingContext Project =
        new("Demo", new string('a', 64), "project-identity/1");

    [Fact]
    public void PrepareFinishQuestNormalizesAndDefensivelyCopiesWithoutStoreAccess()
    {
        var requestId = MutationRequestId.New();
        var questId = QuestId.New();
        var skills = new List<string> { "coding", "testing_awareness" };
        var request = new FinishQuestRequest(
            requestId,
            questId,
            "success",
            "  Ship   bounded   parser  ",
            new FinishQuestMetrics(true, 0, 1, "passed", "observed", "passed", "observed"),
            skills);

        var prepared = HeroPassportApplication.PrepareFinishQuest(request, Project);
        skills[0] = "debugging";

        Assert.Equal(requestId, prepared.FinishRequestId);
        Assert.Equal(questId, prepared.QuestId);
        Assert.Equal("success", prepared.Result);
        Assert.Equal("Ship bounded parser", prepared.Summary);
        Assert.Equal(request.Metrics, prepared.Metrics);
        Assert.Equal(["coding", "testing_awareness"], prepared.SkillsUsed);
    }

    [Fact]
    public void PrepareFinishQuestPreservesExistingValidationCodes()
    {
        var questId = QuestId.New();
        var metrics = new FinishQuestMetrics(false, 0, 0, "not_run", "none", "not_run", "none");

        var resultError = Assert.Throws<HeroPassportException>(() =>
            HeroPassportApplication.PrepareFinishQuest(
                new FinishQuestRequest(MutationRequestId.New(), questId, "invalid", "Summary", metrics, ["coding"]),
                Project));
        Assert.Equal("HP111", resultError.Code);

        var summaryError = Assert.Throws<HeroPassportException>(() =>
            HeroPassportApplication.PrepareFinishQuest(
                new FinishQuestRequest(MutationRequestId.New(), questId, "success", "   ", metrics, ["coding"]),
                Project));
        Assert.Equal("HP100", summaryError.Code);

        var metricsError = Assert.Throws<HeroPassportException>(() =>
            HeroPassportApplication.PrepareFinishQuest(
                new FinishQuestRequest(
                    MutationRequestId.New(), questId, "success", "Summary",
                    metrics with { ScopeViolations = 21 }, ["coding"]),
                Project));
        Assert.Equal("HP120", metricsError.Code);

        var skillsError = Assert.Throws<HeroPassportException>(() =>
            HeroPassportApplication.PrepareFinishQuest(
                new FinishQuestRequest(MutationRequestId.New(), questId, "success", "Summary", metrics, ["coding", "coding"]),
                Project));
        Assert.Equal("HP112", skillsError.Code);

        var projectError = Assert.Throws<HeroPassportException>(() =>
            HeroPassportApplication.PrepareFinishQuest(
                new FinishQuestRequest(MutationRequestId.New(), questId, "success", "Summary", metrics, ["coding"]),
                Project with { WorkspaceFingerprint = "bad" }));
        Assert.Equal("HP310", projectError.Code);
    }
}
