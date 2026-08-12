using HeroPassport.Domain.Game;
using Xunit;

namespace HeroPassport.Domain.Tests;

public sealed class UnlockRulesTests
{
    [Theory]
    [InlineData(0, QuestOutcome.Success, 1)]
    [InlineData(4, QuestOutcome.Success, 5)]
    [InlineData(4, QuestOutcome.Partial, 0)]
    [InlineData(4, QuestOutcome.Blocked, 0)]
    [InlineData(4, QuestOutcome.Failed, 0)]
    [InlineData(4, QuestOutcome.Abandoned, 0)]
    public void StreakUsesOutcomeOnly(int previous, QuestOutcome outcome, int expected)
    {
        var result = StreakRules.Apply(previous, outcome);

        Assert.Equal(expected, result.After);
        Assert.Equal(previous, result.Before);
        Assert.Equal("streak/1.0.0", result.RuleVersion);
    }

    [Fact]
    public void TraitCatalogUnlocksOnlySatisfiedNewTraits()
    {
        var input = new UnlockEvaluationInput(
            HeroLevel: 4,
            SuccessStreak: 5,
            PreciseSuccessCount: 5,
            TestScoutSuccessCount: 5,
            ScopeCleanSuccessCount: 10,
            SkillLevels: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["coding"] = 3,
                ["testing_awareness"] = 3,
                ["scope_control"] = 3,
                ["documentation"] = 3,
                ["review"] = 3,
            },
            ExistingTraits: ["precise_executor"],
            ExistingTitles: [],
            UnlockedAtUtc: new DateTimeOffset(2026, 8, 13, 1, 0, 0, TimeSpan.Zero));

        var result = UnlockRules.Evaluate(input);

        Assert.Equal(
            ["test_scout", "scope_keeper", "steady_hand", "polyglot_crafter"],
            result.TraitsUnlocked);
        Assert.DoesNotContain("precise_executor", result.TraitsUnlocked);
        Assert.Null(result.ActiveTitle);
        Assert.Equal("unlock/2.0.0", result.RuleVersion);
    }

    [Fact]
    public void TitlesAreMonotonicAndHighestCatalogPriorityBecomesActive()
    {
        var older = new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 8, 13, 1, 0, 0, TimeSpan.Zero);
        var input = new UnlockEvaluationInput(
            HeroLevel: 10,
            SuccessStreak: 10,
            PreciseSuccessCount: 0,
            TestScoutSuccessCount: 0,
            ScopeCleanSuccessCount: 0,
            SkillLevels: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["coding"] = 5,
                ["testing_awareness"] = 5,
                ["scope_control"] = 5,
                ["documentation"] = 5,
                ["review"] = 5,
            },
            ExistingTraits: [],
            ExistingTitles: [new UnlockedTitle("rising_adventurer", older)],
            UnlockedAtUtc: now);

        var result = UnlockRules.Evaluate(input);

        Assert.Equal(
            ["veteran_of_the_merge", "skill_specialist", "unbroken_builder", "master_of_many_tools"],
            result.TitlesUnlocked.Select(static title => title.Key));
        Assert.Equal("master_of_many_tools", result.ActiveTitle?.Key);
        Assert.Equal(now, result.ActiveTitle?.UnlockedAtUtc);
        Assert.Contains(result.AllTitles, static title => title.Key == "rising_adventurer");
        Assert.Equal(5, result.AllTitles.Count);
    }

    [Fact]
    public void LowerPriorityNewTitleDoesNotDisplaceHigherPriorityExistingTitle()
    {
        var existing = new UnlockedTitle(
            "master_of_many_tools",
            new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero));
        var input = new UnlockEvaluationInput(
            HeroLevel: 10,
            SuccessStreak: 0,
            PreciseSuccessCount: 0,
            TestScoutSuccessCount: 0,
            ScopeCleanSuccessCount: 0,
            SkillLevels: new Dictionary<string, int>(StringComparer.Ordinal),
            ExistingTraits: [],
            ExistingTitles: [existing],
            UnlockedAtUtc: new DateTimeOffset(2026, 8, 13, 1, 0, 0, TimeSpan.Zero));

        var result = UnlockRules.Evaluate(input);

        Assert.Equal("master_of_many_tools", result.ActiveTitle?.Key);
        Assert.Contains(result.TitlesUnlocked, static title => title.Key == "rising_adventurer");
        Assert.Contains(result.TitlesUnlocked, static title => title.Key == "veteran_of_the_merge");
    }

    [Fact]
    public void UnlockEvaluationRejectsUnknownSkillsOrMalformedExistingKeys()
    {
        var now = new DateTimeOffset(2026, 8, 13, 1, 0, 0, TimeSpan.Zero);
        var unknownSkill = BaseInput(now) with
        {
            SkillLevels = new Dictionary<string, int>(StringComparer.Ordinal) { ["unknown"] = 5 },
        };
        var unknownTrait = BaseInput(now) with { ExistingTraits = ["unknown_trait"] };
        var unknownTitle = BaseInput(now) with { ExistingTitles = [new UnlockedTitle("unknown_title", now)] };

        Assert.Throws<ArgumentException>(() => UnlockRules.Evaluate(unknownSkill));
        Assert.Throws<ArgumentException>(() => UnlockRules.Evaluate(unknownTrait));
        Assert.Throws<ArgumentException>(() => UnlockRules.Evaluate(unknownTitle));
    }

    private static UnlockEvaluationInput BaseInput(DateTimeOffset now) =>
        new(
            HeroLevel: 1,
            SuccessStreak: 0,
            PreciseSuccessCount: 0,
            TestScoutSuccessCount: 0,
            ScopeCleanSuccessCount: 0,
            SkillLevels: new Dictionary<string, int>(StringComparer.Ordinal),
            ExistingTraits: [],
            ExistingTitles: [],
            UnlockedAtUtc: now);
}
