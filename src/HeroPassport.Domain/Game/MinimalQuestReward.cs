namespace HeroPassport.Domain.Game;

public sealed record MinimalRewardResult(
    int BaseXp,
    int BonusXp,
    int PenaltyXp,
    int RawXp,
    int OutcomePermille,
    int XpGained,
    string RuleVersion);

public static class MinimalQuestReward
{
    public const string RuleVersion = "reward/vertical-slice/1";

    public static MinimalRewardResult Calculate(string questType, string result)
    {
        var baseXp = questType switch
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

        var outcomePermille = result switch
        {
            "success" => 1000,
            "partial" => 600,
            "blocked" => 300,
            "failed" => 100,
            "abandoned" => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };

        var xpGained = checked((baseXp * outcomePermille) / 1000);
        return new MinimalRewardResult(baseXp, 0, 0, baseXp, outcomePermille, xpGained, RuleVersion);
    }
}
