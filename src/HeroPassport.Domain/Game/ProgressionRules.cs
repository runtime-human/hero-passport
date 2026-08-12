namespace HeroPassport.Domain.Game;

public sealed record ProgressionState(
    int Level,
    bool IsLevelCapped,
    long LevelXp,
    long? NextLevelXpRequired,
    string RuleVersion);

public static class HeroProgressionRules
{
    public const string RuleVersion = "hero-progression/2.0.0";

    private static readonly long[] Thresholds =
    [
        0, 100, 250, 450, 700, 1000, 1350, 1750, 2200, 2700,
        3250, 3850, 4500, 5200, 5950, 6700, 7450, 8200, 8950, 9700,
        10000, 10750, 11500, 12250, 13000, 13750, 14500, 15250, 16000, 16750,
        17500, 18250, 19000, 19750, 20500, 21250, 22000, 22750, 23500, 24250,
        25000, 25750, 26500, 27250, 28000, 28750, 29500, 30250, 31000, 31750,
    ];

    public static ProgressionState GetState(long totalXp) =>
        ProgressionCalculator.Calculate(totalXp, Thresholds, RuleVersion);
}

public static class SkillProgressionRules
{
    public const string RuleVersion = "skill-progression/2.0.0";

    private static readonly long[] Thresholds =
    [
        0, 50, 125, 225, 350, 500, 675, 875, 1100, 1350,
    ];

    public static ProgressionState GetState(long xp) =>
        ProgressionCalculator.Calculate(xp, Thresholds, RuleVersion);
}

public static class RankRules
{
    public const string RuleVersion = "rank/1.0.0";

    public static string GetRankKey(int heroLevel)
    {
        if (heroLevel is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(heroLevel));
        }

        return heroLevel switch
        {
            <= 4 => "code_squire",
            <= 9 => "code_knight",
            <= 19 => "senior_warrior",
            <= 34 => "staff_paladin",
            <= 49 => "principal_warlord",
            _ => "legendary_architect",
        };
    }
}

internal static class ProgressionCalculator
{
    public static ProgressionState Calculate(long xp, long[] thresholds, string ruleVersion)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(xp);
        ArgumentNullException.ThrowIfNull(thresholds);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleVersion);

        var levelIndex = FindLevelIndex(xp, thresholds);
        var capped = levelIndex == thresholds.Length - 1;
        var levelXp = checked(xp - thresholds[levelIndex]);
        long? nextLevelXpRequired = capped
            ? null
            : checked(thresholds[levelIndex + 1] - thresholds[levelIndex]);

        return new ProgressionState(
            levelIndex + 1,
            capped,
            levelXp,
            nextLevelXpRequired,
            ruleVersion);
    }

    private static int FindLevelIndex(long xp, long[] thresholds)
    {
        var low = 0;
        var high = thresholds.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (thresholds[middle] <= xp)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return high;
    }
}
