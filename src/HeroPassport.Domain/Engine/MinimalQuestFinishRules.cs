namespace HeroPassport.Domain.Engine;

public static class MinimalQuestFinishRules
{
    private const string HeroProgressionV2 = "hero-progression/2.0.0";

    private static readonly long[] HeroLevelThresholds =
    [
        0, 100, 250, 450, 700, 1000, 1350, 1750, 2200, 2700,
        3250, 3850, 4500, 5200, 5950, 6700, 7450, 8200, 8950, 9700,
        10000, 10750, 11500, 12250, 13000, 13750, 14500, 15250, 16000, 16750,
        17500, 18250, 19000, 19750, 20500, 21250, 22000, 22750, 23500, 24250,
        25000, 25750, 26500, 27250, 28000, 28750, 29500, 30250, 31000, 31750
    ];

    public static int BaseXp(string questType) => QuestRewardRules.BaseXp(questType);

    public static int OutcomePermille(string result) => QuestRewardRules.OutcomePermille(result);

    public static long QuestXp(int baseXp, int outcomePermille) =>
        checked((long)baseXp * outcomePermille / 1000L);

    public static int HeroLevel(long totalXp)
    {
        var level = 1;
        for (var index = 1; index < HeroLevelThresholds.Length; index++)
        {
            if (totalXp < HeroLevelThresholds[index])
            {
                break;
            }

            level = index + 1;
        }

        return level;
    }

    public static bool IsHeroLevelCapped(int level, string ruleVersion)
    {
        RequireHeroProgressionVersion(ruleVersion);
        RequireHeroLevel(level);
        return level == HeroLevelThresholds.Length;
    }

    public static long HeroLevelXp(long totalXp, int level, string ruleVersion)
    {
        RequireHeroProgressionVersion(ruleVersion);
        RequireHeroLevel(level);
        var threshold = HeroLevelThresholds[level - 1];
        ArgumentOutOfRangeException.ThrowIfLessThan(totalXp, threshold);
        return checked(totalXp - threshold);
    }

    public static long? NextHeroLevelXpRequired(int level, string ruleVersion)
    {
        RequireHeroProgressionVersion(ruleVersion);
        RequireHeroLevel(level);
        return level == HeroLevelThresholds.Length
            ? null
            : checked(HeroLevelThresholds[level] - HeroLevelThresholds[level - 1]);
    }

    public static string RankKey(int heroLevel) => heroLevel switch
    {
        <= 4 => "code_squire",
        <= 9 => "code_knight",
        <= 19 => "senior_warrior",
        <= 34 => "staff_paladin",
        <= 49 => "principal_warlord",
        _ => "legendary_architect"
    };

    private static void RequireHeroProgressionVersion(string ruleVersion)
    {
        if (!string.Equals(ruleVersion, HeroProgressionV2, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported Hero progression rule version.", nameof(ruleVersion));
        }
    }

    private static void RequireHeroLevel(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, HeroLevelThresholds.Length);
    }
}
