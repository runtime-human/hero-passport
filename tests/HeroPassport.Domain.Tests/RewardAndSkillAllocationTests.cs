using HeroPassport.Domain.Engine;
using Xunit;

namespace HeroPassport.Domain.Tests;

public sealed class RewardAndSkillAllocationTests
{
    [Fact]
    public void QualityFlagsUseOnlyBoundedCanonicalSignals()
    {
        var cleanObserved = QuestQualityFlags.From(
            summaryScalarLength: 40,
            testsStatus: "passed",
            testsEvidence: "observed",
            scopeViolations: 0,
            userCorrections: 0);
        var reported = QuestQualityFlags.From(
            summaryScalarLength: 39,
            testsStatus: "passed",
            testsEvidence: "reported",
            scopeViolations: 1,
            userCorrections: 1);

        Assert.True(cleanObserved.HasObservedTestsPassed);
        Assert.True(cleanObserved.HasCleanScope);
        Assert.True(cleanObserved.HasClearSummary);
        Assert.True(cleanObserved.HasNoUserCorrections);

        Assert.False(reported.HasObservedTestsPassed);
        Assert.False(reported.HasCleanScope);
        Assert.False(reported.HasClearSummary);
        Assert.False(reported.HasNoUserCorrections);
    }

    [Theory]
    [InlineData("planning", 30)]
    [InlineData("research", 40)]
    [InlineData("coding", 60)]
    [InlineData("review", 50)]
    [InlineData("debugging", 70)]
    [InlineData("documentation", 40)]
    [InlineData("maintenance", 40)]
    public void QuestTypeBaseXpMatchesCanonicalTable(string questType, int expectedBaseXp)
    {
        Assert.Equal(expectedBaseXp, QuestRewardRules.BaseXp(questType));
    }

    [Theory]
    [InlineData("success", 95)]
    [InlineData("partial", 57)]
    [InlineData("blocked", 28)]
    [InlineData("failed", 9)]
    [InlineData("abandoned", 0)]
    public void CleanCodingRewardMatchesCanonicalGoldens(string result, long expectedXp)
    {
        var reward = QuestRewardRules.Evaluate(
            "coding",
            result,
            QuestQualityFlags.From(80, "passed", "observed", 0, 0),
            scopeViolations: 0,
            userCorrections: 0);

        Assert.Equal(60, reward.BaseXp);
        Assert.Equal(35, reward.BonusXp);
        Assert.Equal(0, reward.PenaltyXp);
        Assert.Equal(95, reward.RawXp);
        Assert.Equal(expectedXp, reward.XpGained);
        Assert.Equal("reward/2.0.0", reward.RuleVersion);
    }

    [Fact]
    public void RewardPenaltiesAreCappedAndAppliedBeforeOutcomeMultiplier()
    {
        var reward = QuestRewardRules.Evaluate(
            "coding",
            "success",
            QuestQualityFlags.From(80, "not_run", "none", 2, 1),
            scopeViolations: 2,
            userCorrections: 1);
        var capped = QuestRewardRules.Evaluate(
            "debugging",
            "success",
            QuestQualityFlags.From(10, "not_run", "none", 20, 20),
            scopeViolations: 20,
            userCorrections: 20);

        Assert.Equal(55, reward.XpGained);
        Assert.Equal(15, reward.PenaltyXp);

        Assert.Equal(70, capped.BaseXp);
        Assert.Equal(0, capped.BonusXp);
        Assert.Equal(30, capped.PenaltyXp);
        Assert.Equal(40, capped.RawXp);
        Assert.Equal(40, capped.XpGained);
    }

    [Fact]
    public void SkillAllocationUsesCumulativeFloorsAndConservesQuestXp()
    {
        var one = SkillAllocationRules.Allocate(95, ["coding"]);
        var two = SkillAllocationRules.Allocate(95, ["coding", "testing_awareness"]);
        var three = SkillAllocationRules.Allocate(95, ["coding", "testing_awareness", "scope_control"]);
        var tiny = SkillAllocationRules.Allocate(1, ["coding", "testing_awareness", "scope_control"]);

        Assert.Equal([95L], one.Select(static item => item.XpGained));
        Assert.Equal([57L, 38L], two.Select(static item => item.XpGained));
        Assert.Equal([47L, 29L, 19L], three.Select(static item => item.XpGained));
        Assert.Equal([0L, 0L, 1L], tiny.Select(static item => item.XpGained));

        Assert.Equal(["coding", "testing_awareness", "scope_control"], three.Select(static item => item.SkillKey));
        Assert.Equal(95, three.Sum(static item => item.XpGained));
        Assert.Equal("skill-allocation/1.0.0", three[0].RuleVersion);
    }

    [Fact]
    public void SkillAllocationConservesJsonSafeMaximumWithoutOverflow()
    {
        const long max = 9_007_199_254_740_991L;

        var allocations = SkillAllocationRules.Allocate(max, ["coding", "testing_awareness", "scope_control"]);

        Assert.Equal(max, allocations.Sum(static item => item.XpGained));
        Assert.All(allocations, static item => Assert.True(item.XpGained >= 0));
    }

    [Fact]
    public void DomainGuardsRejectUnsupportedRewardAndSkillInputs()
    {
        var flags = QuestQualityFlags.From(40, "passed", "observed", 0, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => QuestRewardRules.Evaluate("unknown", "success", flags, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => QuestRewardRules.Evaluate("coding", "unknown", flags, 0, 0));
        Assert.Throws<ArgumentException>(() => QuestRewardRules.Evaluate("coding", "success", flags, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => QuestQualityFlags.From(-1, "not_run", "none", 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => QuestQualityFlags.From(1, "not_run", "none", 21, 0));
        Assert.Throws<ArgumentException>(() => QuestQualityFlags.From(1, "passed", "none", 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillAllocationRules.Allocate(-1, ["coding"]));
        Assert.Throws<ArgumentException>(() => SkillAllocationRules.Allocate(1, []));
        Assert.Throws<ArgumentException>(() => SkillAllocationRules.Allocate(1, ["coding", "coding"]));
        Assert.Throws<ArgumentException>(() => SkillAllocationRules.Allocate(1, ["unknown"]));
        Assert.Throws<ArgumentException>(() => SkillAllocationRules.Allocate(1, ["coding", "testing_awareness", "scope_control", "review"]));
    }
}
