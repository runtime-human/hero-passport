using HeroPassport.Domain.Engine;
using Xunit;

namespace HeroPassport.Domain.Tests;

public sealed class SkillProgressionTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(49, 1)]
    [InlineData(50, 2)]
    [InlineData(124, 2)]
    [InlineData(125, 3)]
    [InlineData(224, 3)]
    [InlineData(225, 4)]
    [InlineData(349, 4)]
    [InlineData(350, 5)]
    [InlineData(499, 5)]
    [InlineData(500, 6)]
    [InlineData(674, 6)]
    [InlineData(675, 7)]
    [InlineData(874, 7)]
    [InlineData(875, 8)]
    [InlineData(1099, 8)]
    [InlineData(1100, 9)]
    [InlineData(1349, 9)]
    [InlineData(1350, 10)]
    [InlineData(9007199254740991, 10)]
    public void LevelMatchesSkillProgressionV2Thresholds(long totalXp, int expectedLevel)
    {
        Assert.Equal(expectedLevel, SkillProgressionRules.Level(totalXp, "skill-progression/2.0.0"));
    }

    [Fact]
    public void ProgressionSnapshotUsesCheckedTotalXpAndThresholdDeltas()
    {
        var progress = SkillProgressionRules.Apply(
            xpBefore: 0,
            xpGained: 95,
            "skill-progression/2.0.0");

        Assert.Equal(0, progress.XpBefore);
        Assert.Equal(95, progress.XpAfter);
        Assert.Equal(1, progress.LevelBefore);
        Assert.Equal(2, progress.LevelAfter);
        Assert.False(progress.IsLevelCapped);
        Assert.Equal(75, progress.NextLevelXpRequired);
        Assert.Equal("skill-progression/2.0.0", progress.RuleVersion);
    }

    [Fact]
    public void LevelTenIsDisplayCapWhileXpContinuesAccumulating()
    {
        var reachesCap = SkillProgressionRules.Apply(1300, 50, "skill-progression/2.0.0");
        var beyondCap = SkillProgressionRules.Apply(1350, 5000, "skill-progression/2.0.0");

        Assert.Equal(10, reachesCap.LevelAfter);
        Assert.True(reachesCap.IsLevelCapped);
        Assert.Null(reachesCap.NextLevelXpRequired);

        Assert.Equal(6350, beyondCap.XpAfter);
        Assert.Equal(10, beyondCap.LevelBefore);
        Assert.Equal(10, beyondCap.LevelAfter);
        Assert.True(beyondCap.IsLevelCapped);
        Assert.Null(beyondCap.NextLevelXpRequired);
    }

    [Fact]
    public void UnsupportedVersionNegativeXpAndJsonSafeOverflowAreRejected()
    {
        Assert.Throws<ArgumentException>(() => SkillProgressionRules.Level(0, "skill-progression/1.0.0"));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillProgressionRules.Level(-1, "skill-progression/2.0.0"));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillProgressionRules.Apply(0, -1, "skill-progression/2.0.0"));
        Assert.Throws<OverflowException>(() => SkillProgressionRules.Apply(9_007_199_254_740_991L, 1, "skill-progression/2.0.0"));
    }
}
