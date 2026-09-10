using HeroPassport.Domain.Primitives;

namespace HeroPassport.Domain.Engine;

public static class HeroProgressionRules
{
    public const string RuleVersion = "hero-progression/2.0.0";

    private static readonly long[] LevelThresholds =
    [
        0, 100, 250, 450, 700, 1000, 1350, 1750, 2200, 2700,
        3250, 3850, 4500, 5200, 5950, 6700, 7450, 8200, 8950, 9700,
        10000, 10750, 11500, 12250, 13000, 13750, 14500, 15250, 16000, 16750,
        17500, 18250, 19000, 19750, 20500, 21250, 22000, 22750, 23500, 24250,
        25000, 25750, 26500, 27250, 28000, 28750, 29500, 30250, 31000, 31750,
    ];

    public static int Level(long totalXp, string ruleVersion)
    {
        RequireVersion(ruleVersion);
        JsonSafeInteger.Require(totalXp);

        var level = 1;
        for (var index = 1; index < LevelThresholds.Length; index++)
        {
            if (totalXp < LevelThresholds[index])
            {
                break;
            }

            level = index + 1;
        }

        return level;
    }

    public static HeroProgressionResult Apply(long totalXpBefore, long xpGained, string ruleVersion)
    {
        RequireVersion(ruleVersion);
        JsonSafeInteger.Require(totalXpBefore);
        JsonSafeInteger.Require(xpGained);

        var totalXpAfter = JsonSafeInteger.Require(checked(totalXpBefore + xpGained));
        var levelBefore = Level(totalXpBefore, ruleVersion);
        var levelAfter = Level(totalXpAfter, ruleVersion);

        return new HeroProgressionResult(
            totalXpBefore,
            totalXpAfter,
            levelBefore,
            levelAfter,
            IsLevelCapped(levelAfter, ruleVersion),
            LevelXp(totalXpAfter, levelAfter, ruleVersion),
            NextLevelXpRequired(levelAfter, ruleVersion),
            RuleVersion);
    }

    public static bool IsLevelCapped(int level, string ruleVersion)
    {
        RequireVersion(ruleVersion);
        RequireLevel(level);
        return level == LevelThresholds.Length;
    }

    public static long LevelXp(long totalXp, int level, string ruleVersion)
    {
        RequireVersion(ruleVersion);
        JsonSafeInteger.Require(totalXp);
        RequireLevel(level);

        var threshold = LevelThresholds[level - 1];
        ArgumentOutOfRangeException.ThrowIfLessThan(totalXp, threshold);
        return checked(totalXp - threshold);
    }

    public static long? NextLevelXpRequired(int level, string ruleVersion)
    {
        RequireVersion(ruleVersion);
        RequireLevel(level);

        return level == LevelThresholds.Length
            ? null
            : checked(LevelThresholds[level] - LevelThresholds[level - 1]);
    }

    private static void RequireVersion(string ruleVersion)
    {
        if (!string.Equals(ruleVersion, RuleVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported Hero progression rule version.", nameof(ruleVersion));
        }
    }

    private static void RequireLevel(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, LevelThresholds.Length);
    }
}

public sealed record HeroProgressionResult(
    long TotalXpBefore,
    long TotalXpAfter,
    int LevelBefore,
    int LevelAfter,
    bool IsLevelCapped,
    long LevelXp,
    long? NextLevelXpRequired,
    string RuleVersion);
