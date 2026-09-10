namespace HeroPassport.Domain.Engine;

public static class RankRules
{
    public const string RuleVersion = "rank/1.0.0";

    public static string Key(int heroLevel, string ruleVersion)
    {
        RequireVersion(ruleVersion);
        ArgumentOutOfRangeException.ThrowIfLessThan(heroLevel, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(heroLevel, 50);

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

    private static void RequireVersion(string ruleVersion)
    {
        if (!string.Equals(ruleVersion, RuleVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported Rank rule version.", nameof(ruleVersion));
        }
    }
}
