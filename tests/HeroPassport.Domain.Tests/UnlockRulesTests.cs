using HeroPassport.Domain.Engine;
using Xunit;

namespace HeroPassport.Domain.Tests;

public sealed class UnlockRulesTests
{
    [Fact]
    public void ExactTraitAndTitleThresholdsUnlockMonotonically()
    {
        var context = new UnlockEvaluationContext(
            HeroLevelBefore: 4,
            HeroLevelAfter: 5,
            RankBefore: "code_squire",
            RankAfter: "code_knight",
            SuccessStreakBefore: 4,
            SuccessStreakAfter: 5,
            PreciseExecutorSuccessesAfter: 5,
            TestScoutSuccessesAfter: 5,
            ScopeKeeperSuccessesAfter: 10,
            SkillsAfter:
            [
                new UnlockSkillState("coding", 5),
                new UnlockSkillState("testing_awareness", 3),
                new UnlockSkillState("scope_control", 3),
                new UnlockSkillState("documentation", 3),
                new UnlockSkillState("tool_use", 3),
            ],
            SkillLevelChanges: [new UnlockSkillLevelChange("coding", 4, 5)],
            ExistingTraits: [],
            ExistingTitles: []);

        var result = UnlockRules.Evaluate(context, UnlockRules.RuleVersion);

        Assert.Equal(
            ["precise_executor", "test_scout", "scope_keeper", "steady_hand", "polyglot_crafter"],
            result.TraitsUnlocked);
        Assert.Equal(["rising_adventurer", "skill_specialist"], result.TitlesUnlocked);

        var repeated = UnlockRules.Evaluate(
            context with
            {
                ExistingTraits = result.TraitsUnlocked,
                ExistingTitles = result.TitlesUnlocked,
            },
            UnlockRules.RuleVersion);

        Assert.Empty(repeated.TraitsUnlocked);
        Assert.Empty(repeated.TitlesUnlocked);
    }

    [Fact]
    public void HigherThresholdTitlesAndFiveLevelFiveSkillsUseCurrentPostQuestState()
    {
        var context = new UnlockEvaluationContext(
            HeroLevelBefore: 9,
            HeroLevelAfter: 10,
            RankBefore: "code_knight",
            RankAfter: "senior_warrior",
            SuccessStreakBefore: 9,
            SuccessStreakAfter: 10,
            PreciseExecutorSuccessesAfter: 0,
            TestScoutSuccessesAfter: 0,
            ScopeKeeperSuccessesAfter: 0,
            SkillsAfter:
            [
                new UnlockSkillState("coding", 5),
                new UnlockSkillState("testing_awareness", 5),
                new UnlockSkillState("scope_control", 5),
                new UnlockSkillState("documentation", 5),
                new UnlockSkillState("tool_use", 5),
            ],
            SkillLevelChanges: [],
            ExistingTraits: [],
            ExistingTitles: ["rising_adventurer", "skill_specialist"]);

        var result = UnlockRules.Evaluate(context, UnlockRules.RuleVersion);

        Assert.Empty(result.TraitsUnlocked);
        Assert.Equal(
            ["veteran_of_the_merge", "unbroken_builder", "master_of_many_tools"],
            result.TitlesUnlocked);
    }

    [Fact]
    public void SemanticMilestonesAreDeterministicAndPresentationFree()
    {
        var context = new UnlockEvaluationContext(
            HeroLevelBefore: 4,
            HeroLevelAfter: 5,
            RankBefore: "code_squire",
            RankAfter: "code_knight",
            SuccessStreakBefore: 4,
            SuccessStreakAfter: 5,
            PreciseExecutorSuccessesAfter: 0,
            TestScoutSuccessesAfter: 0,
            ScopeKeeperSuccessesAfter: 0,
            SkillsAfter: [new UnlockSkillState("coding", 5)],
            SkillLevelChanges: [new UnlockSkillLevelChange("coding", 4, 5)],
            ExistingTraits: [],
            ExistingTitles: []);

        var result = UnlockRules.Evaluate(context, UnlockRules.RuleVersion);

        Assert.Equal(
            [
                new UnlockMilestone("hero_level_changed", "hero_level:5"),
                new UnlockMilestone("rank_changed", "rank:code_knight"),
                new UnlockMilestone("skill_level_changed", "skill_level:coding:5"),
                new UnlockMilestone("streak_changed", "streak:5"),
                new UnlockMilestone("trait_unlocked", "trait:steady_hand"),
                new UnlockMilestone("title_unlocked", "title:rising_adventurer"),
                new UnlockMilestone("title_unlocked", "title:skill_specialist"),
            ],
            result.Milestones);
    }

    [Fact]
    public void ActiveTitleUsesFixedPriorityBeforeUnlockTimeAndKey()
    {
        var states = new[]
        {
            new TitleUnlockState("rising_adventurer", new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero)),
            new TitleUnlockState("skill_specialist", new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero)),
            new TitleUnlockState("master_of_many_tools", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
        };

        Assert.Equal("master_of_many_tools", UnlockRules.SelectActiveTitle(states, UnlockRules.RuleVersion));
        Assert.Null(UnlockRules.SelectActiveTitle([], UnlockRules.RuleVersion));
    }

    [Fact]
    public void UnlockRulesRejectUnknownVersionsKeysAndInvalidCounts()
    {
        var valid = new UnlockEvaluationContext(
            HeroLevelBefore: 1,
            HeroLevelAfter: 1,
            RankBefore: "code_squire",
            RankAfter: "code_squire",
            SuccessStreakBefore: 0,
            SuccessStreakAfter: 0,
            PreciseExecutorSuccessesAfter: 0,
            TestScoutSuccessesAfter: 0,
            ScopeKeeperSuccessesAfter: 0,
            SkillsAfter: [],
            SkillLevelChanges: [],
            ExistingTraits: [],
            ExistingTitles: []);

        Assert.Throws<ArgumentException>(() => UnlockRules.Evaluate(valid, "unlock/0"));
        Assert.Throws<ArgumentOutOfRangeException>(() => UnlockRules.Evaluate(valid with { PreciseExecutorSuccessesAfter = -1 }, UnlockRules.RuleVersion));
        Assert.Throws<ArgumentException>(() => UnlockRules.Evaluate(valid with { SkillsAfter = [new UnlockSkillState("unknown", 1)] }, UnlockRules.RuleVersion));
        Assert.Throws<ArgumentException>(() => UnlockRules.Evaluate(valid with { ExistingTraits = ["unknown"] }, UnlockRules.RuleVersion));
        Assert.Throws<ArgumentException>(() => UnlockRules.SelectActiveTitle(
            [new TitleUnlockState("unknown", new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero))],
            UnlockRules.RuleVersion));
    }
}
