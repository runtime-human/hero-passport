using HeroPassport.Domain.Primitives;

namespace HeroPassport.Domain.Engine;

public sealed record StreakResult(long Before, long After, string RuleVersion);

public static class StreakRules
{
    public const string RuleVersion = "streak/1.0.0";

    public static StreakResult Apply(long before, string result, string ruleVersion)
    {
        RequireVersion(ruleVersion);
        JsonSafeInteger.Require(before);
        RequireResult(result);

        var after = string.Equals(result, "success", StringComparison.Ordinal)
            ? JsonSafeInteger.Require(checked(before + 1))
            : 0L;

        return new StreakResult(before, after, RuleVersion);
    }

    private static void RequireVersion(string ruleVersion)
    {
        if (!string.Equals(ruleVersion, RuleVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported Streak rule version.", nameof(ruleVersion));
        }
    }

    private static void RequireResult(string result)
    {
        if (result is not ("success" or "partial" or "blocked" or "failed" or "abandoned"))
        {
            throw new ArgumentOutOfRangeException(nameof(result));
        }
    }
}
