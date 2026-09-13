using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class StartQuestPreparationTests
{
    [Fact]
    public void PrepareStartQuestNormalizesWithoutStoreAccess()
    {
        var requestId = MutationRequestId.New();
        var heroId = HeroId.New();
        var project = new ProjectBindingContext("Demo", new string('a', 64), "project-identity/1");

        var prepared = HeroPassportApplication.PrepareStartQuest(
            new StartQuestRequest(
                requestId,
                heroId,
                "coding",
                "  Build   parser  ",
                "  Ship   bounded   parser  "),
            project);

        Assert.Equal(requestId, prepared.StartRequestId);
        Assert.Equal(heroId, prepared.HeroId);
        Assert.Equal("coding", prepared.QuestType);
        Assert.Equal("Build parser", prepared.Title);
        Assert.Equal("Ship bounded parser", prepared.Goal);
    }

    [Fact]
    public void PrepareStartQuestPreservesExistingValidationCodes()
    {
        var heroId = HeroId.New();
        var project = new ProjectBindingContext("Demo", new string('a', 64), "project-identity/1");

        var typeError = Assert.Throws<HeroPassportException>(() => HeroPassportApplication.PrepareStartQuest(
            new StartQuestRequest(MutationRequestId.New(), heroId, "invalid", "Title", "Goal"),
            project));
        Assert.Equal("HP110", typeError.Code);

        var titleError = Assert.Throws<HeroPassportException>(() => HeroPassportApplication.PrepareStartQuest(
            new StartQuestRequest(MutationRequestId.New(), heroId, "coding", "   ", "Goal"),
            project));
        Assert.Equal("HP100", titleError.Code);

        var projectError = Assert.Throws<HeroPassportException>(() => HeroPassportApplication.PrepareStartQuest(
            new StartQuestRequest(MutationRequestId.New(), heroId, "coding", "Title", "Goal"),
            project with { WorkspaceFingerprint = "bad" }));
        Assert.Equal("HP310", projectError.Code);
    }
}
