namespace HeroPassport.Domain.Engine;

public sealed record QuestRewardComponent(string Key, long XpDelta);

public sealed record QuestRewardResult(
    int BaseXp,
    int BonusXp,
    int PenaltyXp,
    int RawXp,
    int OutcomePermille,
    long XpGained,
    IReadOnlyList<QuestRewardComponent> Components,
    string RuleVersion);

public static class QuestRewardRules
{
    public const string RuleVersion = "reward/2.0.0";

    public static QuestRewardResult Evaluate(
        string questType,
        string result,
        QuestQualityFlags quality,
        int scopeViolations,
        int userCorrections)
    {
        ArgumentNullException.ThrowIfNull(quality);
        RequireBoundedCount(scopeViolations, nameof(scopeViolations));
        RequireBoundedCount(userCorrections, nameof(userCorrections));

        if (quality.HasCleanScope != (scopeViolations == 0) ||
            quality.HasNoUserCorrections != (userCorrections == 0))
        {
            throw new ArgumentException("Quest quality flags do not match the bounded counts.", nameof(quality));
        }

        var baseXp = BaseXp(questType);
        var outcomePermille = OutcomePermille(result);
        var components = Components(quality, scopeViolations, userCorrections);
        var bonusXp = 0;
        var penaltyXp = 0;
        foreach (var component in components)
        {
            if (component.XpDelta > 0)
            {
                bonusXp = checked(bonusXp + (int)component.XpDelta);
            }
            else
            {
                penaltyXp = checked(penaltyXp - (int)component.XpDelta);
            }
        }

        var rawXp = Math.Max(0, checked(baseXp + bonusXp - penaltyXp));
        var xpGained = checked((long)rawXp * outcomePermille / 1000L);

        return new QuestRewardResult(
            baseXp,
            bonusXp,
            penaltyXp,
            rawXp,
            outcomePermille,
            xpGained,
            components,
            RuleVersion);
    }

    public static int BaseXp(string questType) => questType switch
    {
        "planning" => 30,
        "research" => 40,
        "coding" => 60,
        "review" => 50,
        "debugging" => 70,
        "documentation" => 40,
        "maintenance" => 40,
        _ => throw new ArgumentOutOfRangeException(nameof(questType)),
    };

    public static int OutcomePermille(string result) => result switch
    {
        "success" => 1000,
        "partial" => 600,
        "blocked" => 300,
        "failed" => 100,
        "abandoned" => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(result)),
    };

    private static List<QuestRewardComponent> Components(
        QuestQualityFlags quality,
        int scopeViolations,
        int userCorrections)
    {
        var components = new List<QuestRewardComponent>(6);
        if (quality.HasObservedTestsPassed) components.Add(new("observed_tests_passed_bonus", 10));
        if (quality.HasCleanScope) components.Add(new("clean_scope_bonus", 10));
        if (quality.HasClearSummary) components.Add(new("clear_summary_bonus", 10));
        if (quality.HasNoUserCorrections) components.Add(new("no_user_corrections_bonus", 5));
        if (scopeViolations > 0)
            components.Add(new("scope_violation_penalty", -Math.Min(scopeViolations, 3) * 5L));
        if (userCorrections > 0)
            components.Add(new("user_correction_penalty", -Math.Min(userCorrections, 3) * 5L));
        return components;
    }

    private static void RequireBoundedCount(int value, string parameterName)
    {
        if (value is < 0 or > 20)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
