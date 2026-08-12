using HeroPassport.Domain.Game;
using Xunit;

namespace HeroPassport.Domain.Tests;

public sealed class ProgressionRulesTests
{
    [Theory]
    [InlineData(0, 1, 0, 100)]
    [InlineData(99, 1, 99, 100)]
    [InlineData(100, 2, 0, 150)]
    [InlineData(249, 2, 149, 150)]
    [InlineData(250, 3, 0, 200)]
    [InlineData(31000, 49, 0, 750)]
    [InlineData(31749, 49, 749, 750)]
    public void HeroProgressionUsesVersionedThresholdTable(long totalXp, int level, long levelXp, long nextLevelXpRequired)
    {
        var state = HeroProgressionRules.GetState(totalXp);

        Assert.Equal(level, state.Level);
        Assert.False(state.IsLevelCapped);
        Assert.Equal(levelXp, state.LevelXp);
        Assert.Equal(nextLevelXpRequired, state.NextLevelXpRequired);
        Assert.Equal("hero-progression/2.0.0", state.RuleVersion);
    }

    [Theory]
    [InlineData(31750, 0)]
    [InlineData(32000, 250)]
    public void HeroLevelFiftyIsDisplayCapWhileXpContinues(long totalXp, long levelXp)
    {
        var state = HeroProgressionRules.GetState(totalXp);

        Assert.Equal(50, state.Level);
        Assert.True(state.IsLevelCapped);
        Assert.Equal(levelXp, state.LevelXp);
        Assert.Null(state.NextLevelXpRequired);
    }

    [Theory]
    [InlineData(0, 1, 0, 50)]
    [InlineData(49, 1, 49, 50)]
    [InlineData(50, 2, 0, 75)]
    [InlineData(1349, 9, 249, 250)]
    public void SkillProgressionUsesVersionedThresholdTable(long xp, int level, long levelXp, long nextLevelXpRequired)
    {
        var state = SkillProgressionRules.GetState(xp);

        Assert.Equal(level, state.Level);
        Assert.False(state.IsLevelCapped);
        Assert.Equal(levelXp, state.LevelXp);
        Assert.Equal(nextLevelXpRequired, state.NextLevelXpRequired);
        Assert.Equal("skill-progression/2.0.0", state.RuleVersion);
    }

    [Theory]
    [InlineData(1350, 0)]
    [InlineData(1500, 150)]
    public void SkillLevelTenIsDisplayCapWhileXpContinues(long xp, long levelXp)
    {
        var state = SkillProgressionRules.GetState(xp);

        Assert.Equal(10, state.Level);
        Assert.True(state.IsLevelCapped);
        Assert.Equal(levelXp, state.LevelXp);
        Assert.Null(state.NextLevelXpRequired);
    }

    [Theory]
    [InlineData(1, "code_squire")]
    [InlineData(4, "code_squire")]
    [InlineData(5, "code_knight")]
    [InlineData(9, "code_knight")]
    [InlineData(10, "senior_warrior")]
    [InlineData(19, "senior_warrior")]
    [InlineData(20, "staff_paladin")]
    [InlineData(34, "staff_paladin")]
    [InlineData(35, "principal_warlord")]
    [InlineData(49, "principal_warlord")]
    [InlineData(50, "legendary_architect")]
    public void RankIsDerivedOnlyFromHeroLevel(int level, string expectedRank)
    {
        Assert.Equal(expectedRank, RankRules.GetRankKey(level));
        Assert.Equal("rank/1.0.0", RankRules.RuleVersion);
    }

    [Fact]
    public void ProgressionRejectsNegativeXpAndOutOfRangeRankLevel()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HeroProgressionRules.GetState(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillProgressionRules.GetState(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => RankRules.GetRankKey(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RankRules.GetRankKey(51));
    }
}
