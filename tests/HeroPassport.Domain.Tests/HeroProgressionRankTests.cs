using HeroPassport.Domain.Engine;
using HeroPassport.Domain.Primitives;
using Xunit;

namespace HeroPassport.Domain.Tests;

public sealed class HeroProgressionRankTests
{
    private const string HeroVersion = "hero-progression/2.0.0";
    private const string RankVersion = "rank/1.0.0";

    private static readonly long[] Thresholds =
    [
        0, 100, 250, 450, 700, 1000, 1350, 1750, 2200, 2700,
        3250, 3850, 4500, 5200, 5950, 6700, 7450, 8200, 8950, 9700,
        10000, 10750, 11500, 12250, 13000, 13750, 14500, 15250, 16000, 16750,
        17500, 18250, 19000, 19750, 20500, 21250, 22000, 22750, 23500, 24250,
        25000, 25750, 26500, 27250, 28000, 28750, 29500, 30250, 31000, 31750,
    ];

    public static IEnumerable<object[]> ThresholdCases()
    {
        for (var index = 0; index < Thresholds.Length; index++)
        {
            yield return [Thresholds[index], index + 1];
        }
    }

    [Theory]
    [MemberData(nameof(ThresholdCases))]
    public void ExactThresholdsMapToExpectedHeroLevel(long threshold, int expectedLevel)
    {
        Assert.Equal(expectedLevel, HeroProgressionRules.Level(threshold, HeroVersion));
    }

    [Fact]
    public void EveryThresholdBoundaryIsStableOnBothSides()
    {
        for (var index = 1; index < Thresholds.Length; index++)
        {
            var threshold = Thresholds[index];
            Assert.Equal(index, HeroProgressionRules.Level(threshold - 1, HeroVersion));
            Assert.Equal(index + 1, HeroProgressionRules.Level(threshold, HeroVersion));
            Assert.Equal(index + 1, HeroProgressionRules.Level(threshold + 1, HeroVersion));
        }
    }

    [Fact]
    public void ApplyReturnsPostQuestSnapshotAndLevelRelativeProgress()
    {
        var result = HeroProgressionRules.Apply(699, 1, HeroVersion);

        Assert.Equal(699, result.TotalXpBefore);
        Assert.Equal(700, result.TotalXpAfter);
        Assert.Equal(4, result.LevelBefore);
        Assert.Equal(5, result.LevelAfter);
        Assert.False(result.IsLevelCapped);
        Assert.Equal(0, result.LevelXp);
        Assert.Equal(300, result.NextLevelXpRequired);
        Assert.Equal(HeroVersion, result.RuleVersion);
    }

    [Fact]
    public void LevelFiftyIsDisplayCapWhileJsonSafeXpContinuesAccumulating()
    {
        var atCap = HeroProgressionRules.Apply(31_749, 1, HeroVersion);
        var beyondCap = HeroProgressionRules.Apply(31_750, 10_000, HeroVersion);
        var maximum = HeroProgressionRules.Apply(JsonSafeInteger.Maximum, 0, HeroVersion);

        Assert.Equal(50, atCap.LevelAfter);
        Assert.True(atCap.IsLevelCapped);
        Assert.Null(atCap.NextLevelXpRequired);

        Assert.Equal(41_750, beyondCap.TotalXpAfter);
        Assert.Equal(50, beyondCap.LevelBefore);
        Assert.Equal(50, beyondCap.LevelAfter);
        Assert.True(beyondCap.IsLevelCapped);
        Assert.Equal(10_000, beyondCap.LevelXp);
        Assert.Null(beyondCap.NextLevelXpRequired);

        Assert.Equal(50, maximum.LevelAfter);
        Assert.Equal(JsonSafeInteger.Maximum - 31_750, maximum.LevelXp);
        Assert.Null(maximum.NextLevelXpRequired);
    }

    [Fact]
    public void LevelRelativeXpAndNextRequirementUseVersionedThresholdTable()
    {
        for (var level = 1; level <= Thresholds.Length; level++)
        {
            var threshold = Thresholds[level - 1];
            Assert.Equal(0, HeroProgressionRules.LevelXp(threshold, level, HeroVersion));
            Assert.Equal(level == 50, HeroProgressionRules.IsLevelCapped(level, HeroVersion));

            if (level == 50)
            {
                Assert.Null(HeroProgressionRules.NextLevelXpRequired(level, HeroVersion));
                continue;
            }

            var width = Thresholds[level] - threshold;
            Assert.Equal(width, HeroProgressionRules.NextLevelXpRequired(level, HeroVersion));
            Assert.Equal(width - 1, HeroProgressionRules.LevelXp(Thresholds[level] - 1, level, HeroVersion));
        }
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
    public void RankBoundariesMatchRankV1(int heroLevel, string expectedRank)
    {
        Assert.Equal(expectedRank, RankRules.Key(heroLevel, RankVersion));
    }

    [Fact]
    public void InvalidVersionsRangesAndJsonSafeOverflowAreRejected()
    {
        Assert.Throws<ArgumentException>(() => HeroProgressionRules.Level(0, "hero-progression/1.0.0"));
        Assert.Throws<ArgumentException>(() => RankRules.Key(1, "rank/0.9.0"));
        Assert.Throws<ArgumentOutOfRangeException>(() => HeroProgressionRules.Level(-1, HeroVersion));
        Assert.Throws<ArgumentOutOfRangeException>(() => HeroProgressionRules.Level(JsonSafeInteger.Maximum + 1, HeroVersion));
        Assert.Throws<ArgumentOutOfRangeException>(() => HeroProgressionRules.Apply(0, -1, HeroVersion));
        Assert.Throws<ArgumentOutOfRangeException>(() => HeroProgressionRules.Apply(JsonSafeInteger.Maximum, 1, HeroVersion));
        Assert.Throws<ArgumentOutOfRangeException>(() => HeroProgressionRules.IsLevelCapped(0, HeroVersion));
        Assert.Throws<ArgumentOutOfRangeException>(() => HeroProgressionRules.IsLevelCapped(51, HeroVersion));
        Assert.Throws<ArgumentOutOfRangeException>(() => HeroProgressionRules.LevelXp(99, 2, HeroVersion));
        Assert.Throws<ArgumentOutOfRangeException>(() => RankRules.Key(0, RankVersion));
        Assert.Throws<ArgumentOutOfRangeException>(() => RankRules.Key(51, RankVersion));
    }
}
