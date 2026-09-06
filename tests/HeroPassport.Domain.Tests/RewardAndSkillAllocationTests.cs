using HeroPassport.Domain.Engine;
using Xunit;

namespace HeroPassport.Domain.Tests;

public sealed class RewardAndSkillAllocationTests
{
    [Fact]
    public void CleanSuccessfulCodingMatchesRewardV2Golden()
    {
        var result = QuestRewardEngine.Calculate(
            "coding",
            "success",
            Signals(summaryScalarLength: 80, scopeViolations: 0, userCorrections: 0, testsStatus: "passed", testsEvidence: "observed"),
            "reward/2.0.0");

        Assert.Equal(60, result.BaseXp);
        Assert.Equal(35, result.BonusXp);
        Assert.Equal(0, result.PenaltyXp);
        Assert.Equal(95, result.RawXp);
        Assert.Equal(1000, result.OutcomePermille);
        Assert.Equal(95, result.XpGained);
        Assert.Equal("reward/2.0.0", result.RuleVersion);
        Assert.Collection(
            result.Components,
            component => Assert.Equal(new RewardComponent("observed_tests_passed", 10), component),
            component => Assert.Equal(new RewardComponent("clean_scope", 10), component),
            component => Assert.Equal(new RewardComponent("clear_summary", 10), component),
            component => Assert.Equal(new RewardComponent("no_user_corrections", 5), component));
    }

    [Theory]
    [InlineData("success", 95)]
    [InlineData("partial", 57)]
    [InlineData("blocked", 28)]
    [InlineData("failed", 9)]
    [InlineData("abandoned", 0)]
    public void CleanCodingOutcomeGoldensUseIntegerPermille(string outcome, long expectedXp)
    {
        var result = QuestRewardEngine.Calculate(
            "coding",
            outcome,
            Signals(summaryScalarLength: 80, scopeViolations: 0, userCorrections: 0, testsStatus: "passed", testsEvidence: "observed"),
            "reward/2.0.0");

        Assert.Equal(expectedXp, result.XpGained);
    }

    [Fact]
    public void PenaltiesAreCappedAndBonusesAreNotReintroduced()
    {
        var example = QuestRewardEngine.Calculate(
            "coding",
            "success",
            Signals(summaryScalarLength: 80, scopeViolations: 2, userCorrections: 1),
            "reward/2.0.0");

        Assert.Equal(55, example.RawXp);
        Assert.Equal(55, example.XpGained);
        Assert.Equal(10, example.BonusXp);
        Assert.Equal(15, example.PenaltyXp);

        var capped = QuestRewardEngine.Calculate(
            "coding",
            "success",
            Signals(summaryScalarLength: 10, scopeViolations: 20, userCorrections: 20),
            "reward/2.0.0");

        Assert.Equal(30, capped.RawXp);
        Assert.Equal(30, capped.PenaltyXp);
        Assert.Collection(
            capped.Components,
            component => Assert.Equal(new RewardComponent("scope_violations", -15), component),
            component => Assert.Equal(new RewardComponent("user_corrections", -15), component));
    }

    [Fact]
    public void ReportedTestsDoNotReceiveObservedTestsBonus()
    {
        var result = QuestRewardEngine.Calculate(
            "debugging",
            "success",
            Signals(summaryScalarLength: 80, scopeViolations: 0, userCorrections: 0, testsStatus: "passed", testsEvidence: "reported"),
            "reward/2.0.0");

        Assert.Equal(95, result.XpGained);
        Assert.DoesNotContain(result.Components, component => component.Key == "observed_tests_passed");
    }

    [Fact]
    public void UnsupportedRewardVersionIsRejected()
    {
        Assert.Throws<ArgumentException>(() => QuestRewardEngine.Calculate(
            "coding",
            "success",
            Signals(summaryScalarLength: 80),
            "reward/99"));
    }

    [Fact]
    public void SkillAllocationConservesQuestXpForOneTwoAndThreeSkills()
    {
        Assert.Equal(
            [new SkillXpAllocation("coding", 95)],
            SkillXpAllocator.Allocate(95, ["coding"], "skill-allocation/1.0.0"));

        Assert.Equal(
            [new SkillXpAllocation("coding", 57), new SkillXpAllocation("testing_awareness", 38)],
            SkillXpAllocator.Allocate(95, ["coding", "testing_awareness"], "skill-allocation/1.0.0"));

        Assert.Equal(
            [
                new SkillXpAllocation("coding", 47),
                new SkillXpAllocation("testing_awareness", 29),
                new SkillXpAllocation("scope_control", 19),
            ],
            SkillXpAllocator.Allocate(95, ["coding", "testing_awareness", "scope_control"], "skill-allocation/1.0.0"));
    }

    [Fact]
    public void SkillAllocationHandlesZeroXpAndRejectsInvalidShapeOrVersion()
    {
        Assert.Equal(
            [new SkillXpAllocation("coding", 0), new SkillXpAllocation("testing_awareness", 0), new SkillXpAllocation("scope_control", 0)],
            SkillXpAllocator.Allocate(0, ["coding", "testing_awareness", "scope_control"], "skill-allocation/1.0.0"));

        Assert.Throws<ArgumentException>(() => SkillXpAllocator.Allocate(10, [], "skill-allocation/1.0.0"));
        Assert.Throws<ArgumentException>(() => SkillXpAllocator.Allocate(10, ["coding", "coding"], "skill-allocation/1.0.0"));
        Assert.Throws<ArgumentException>(() => SkillXpAllocator.Allocate(10, ["coding"], "skill-allocation/99"));
    }

    private static QuestQualitySignals Signals(
        int summaryScalarLength,
        int scopeViolations = 0,
        int userCorrections = 0,
        string testsStatus = "not_run",
        string testsEvidence = "none") =>
        new(
            TestsMentioned: !string.Equals(testsStatus, "not_run", StringComparison.Ordinal),
            ScopeViolations: scopeViolations,
            UserCorrections: userCorrections,
            BuildStatus: "not_run",
            BuildEvidence: "none",
            TestsStatus: testsStatus,
            TestsEvidence: testsEvidence,
            SummaryScalarLength: summaryScalarLength);
}
