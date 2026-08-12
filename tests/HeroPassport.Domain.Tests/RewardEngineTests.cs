using HeroPassport.Domain.Game;
using Xunit;

namespace HeroPassport.Domain.Tests;

public sealed class RewardEngineTests
{
    [Theory]
    [InlineData(QuestOutcome.Success, 95)]
    [InlineData(QuestOutcome.Partial, 57)]
    [InlineData(QuestOutcome.Blocked, 28)]
    [InlineData(QuestOutcome.Failed, 9)]
    [InlineData(QuestOutcome.Abandoned, 0)]
    public void CleanCodingGoldensMatchRewardV2(QuestOutcome outcome, int expectedXp)
    {
        var result = RewardRules.Calculate(new QuestRewardInput(
            QuestType.Coding,
            outcome,
            HasObservedTestsPassed: true,
            ScopeViolations: 0,
            UserCorrections: 0,
            SummaryScalarLength: 80));

        Assert.Equal(60, result.BaseXp);
        Assert.Equal(35, result.BonusXp);
        Assert.Equal(0, result.PenaltyXp);
        Assert.Equal(95, result.RawXp);
        Assert.Equal(expectedXp, result.QuestXp);
        Assert.Equal("reward/2.0.0", result.RuleVersion);
    }

    [Fact]
    public void RewardPenaltiesAreBoundedAndBonusesAreConditional()
    {
        var result = RewardRules.Calculate(new QuestRewardInput(
            QuestType.Coding,
            QuestOutcome.Success,
            HasObservedTestsPassed: false,
            ScopeViolations: 2,
            UserCorrections: 1,
            SummaryScalarLength: 80));

        Assert.Equal(10, result.BonusXp);
        Assert.Equal(15, result.PenaltyXp);
        Assert.Equal(55, result.RawXp);
        Assert.Equal(55, result.QuestXp);
        Assert.Contains(result.Components, static component => component.Key == "reward.summary" && component.Delta == 10);
        Assert.Contains(result.Components, static component => component.Key == "penalty.scope_violation" && component.Delta == -10);
        Assert.Contains(result.Components, static component => component.Key == "penalty.user_correction" && component.Delta == -5);
    }

    [Fact]
    public void SkillAllocationUsesCumulativeFloorsAndConservesQuestXp()
    {
        var one = SkillAllocationRules.Allocate(95, ["coding"]);
        var two = SkillAllocationRules.Allocate(95, ["coding", "testing_awareness"]);
        var three = SkillAllocationRules.Allocate(95, ["coding", "testing_awareness", "scope_control"]);

        Assert.Equal([95], one.Select(static item => item.Xp));
        Assert.Equal([57, 38], two.Select(static item => item.Xp));
        Assert.Equal([47, 29, 19], three.Select(static item => item.Xp));
        Assert.Equal(95, three.Sum(static item => item.Xp));
        Assert.Equal("skill-allocation/1.0.0", three[0].RuleVersion);
    }

    [Fact]
    public void SkillAllocationRejectsInvalidShapeOrUnknownKeys()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillAllocationRules.Allocate(10, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillAllocationRules.Allocate(10, ["coding", "testing_awareness", "scope_control", "review"]));
        Assert.Throws<ArgumentException>(() => SkillAllocationRules.Allocate(10, ["coding", "coding"]));
        Assert.Throws<ArgumentException>(() => SkillAllocationRules.Allocate(10, ["not_a_skill"]));
    }
}
