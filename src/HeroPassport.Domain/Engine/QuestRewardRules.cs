namespace HeroPassport.Domain.Engine;

public sealed record QuestRewardResult(
    int BaseXp,
    int BonusXp,
    int PenaltyXp,
    int RawXp,
    int OutcomePermille,
    long XpGained,
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
        var bonusXp =
            (quality.HasObservedTestsPassed ? 10 : 0) +
            (quality.HasCleanScope ? 10 : 0) +
            (quality.HasClearSummary ? 10 : 0) +
            (quality.HasNoUserCorrections ? 5 : 0);
        var penaltyXp =
            (Math.Min(scopeViolations, 3) * 5) +
            (Math.Min(userCorrections, 3) * 5);
        var rawXp = Math.Max(0, checked(baseXp + bonusXp - penaltyXp));
        var xpGained = checked((long)rawXp * outcomePermille / 1000L);

        return new QuestRewardResult(
            baseXp,
            bonusXp,
            penaltyXp,
            rawXp,
            outcomePermille,
            xpGained,
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

    private static void RequireBoundedCount(int value, string parameterName)
    {
        if (value is < 0 or > 20)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
