namespace HeroPassport.Domain.Engine;

public static class HeroProgressionRules
{
    public const string RuleVersion = "hero-progression/2.0.0";

    public static int Level(long totalXp, string ruleVersion) =>
        throw new NotImplementedException();

    public static HeroProgressionResult Apply(long totalXpBefore, long xpGained, string ruleVersion) =>
        throw new NotImplementedException();

    public static bool IsLevelCapped(int level, string ruleVersion) =>
        throw new NotImplementedException();

    public static long LevelXp(long totalXp, int level, string ruleVersion) =>
        throw new NotImplementedException();

    public static long? NextLevelXpRequired(int level, string ruleVersion) =>
        throw new NotImplementedException();
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
