using HeroPassport.Domain.Primitives;

namespace HeroPassport.Domain.Engine;

public sealed record SkillProgressionResult(
    long XpBefore,
    long XpAfter,
    int LevelBefore,
    int LevelAfter,
    bool IsLevelCapped,
    long? NextLevelXpRequired,
    string RuleVersion);

public static class SkillProgressionRules
{
    public const string RuleVersion = "skill-progression/2.0.0";

    private static readonly long[] LevelThresholds =
    [
        0,
        50,
        125,
        225,
        350,
        500,
        675,
        875,
        1100,
        1350,
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

    public static SkillProgressionResult Apply(long xpBefore, long xpGained, string ruleVersion)
    {
        RequireVersion(ruleVersion);
        JsonSafeInteger.Require(xpBefore);
        JsonSafeInteger.Require(xpGained);

        var xpAfter = JsonSafeInteger.Require(checked(xpBefore + xpGained));
        var levelBefore = Level(xpBefore, ruleVersion);
        var levelAfter = Level(xpAfter, ruleVersion);
        var capped = levelAfter == LevelThresholds.Length;

        return new SkillProgressionResult(
            xpBefore,
            xpAfter,
            levelBefore,
            levelAfter,
            capped,
            NextLevelXpRequired(levelAfter, ruleVersion),
            RuleVersion);
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
            throw new ArgumentException("Unsupported Skill progression rule version.", nameof(ruleVersion));
        }
    }

    private static void RequireLevel(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, LevelThresholds.Length);
    }
}
