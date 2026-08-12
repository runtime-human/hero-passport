namespace HeroPassport.Domain.Game;

public enum QuestType
{
    Planning,
    Research,
    Coding,
    Review,
    Debugging,
    Documentation,
    Maintenance,
}

public enum QuestOutcome
{
    Success,
    Partial,
    Blocked,
    Failed,
    Abandoned,
}

public sealed record QuestRewardInput(
    QuestType QuestType,
    QuestOutcome Outcome,
    bool HasObservedTestsPassed,
    int ScopeViolations,
    int UserCorrections,
    int SummaryScalarLength);

public sealed record RewardComponent(string Key, int Delta);

public sealed record RewardBreakdown(
    int BaseXp,
    int BonusXp,
    int PenaltyXp,
    int RawXp,
    int OutcomePermille,
    int QuestXp,
    string RuleVersion,
    IReadOnlyList<RewardComponent> Components);

public static class RewardRules
{
    public const string RuleVersion = "reward/2.0.0";

    public static RewardBreakdown Calculate(QuestRewardInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.ScopeViolations is < 0 or > 20 ||
            input.UserCorrections is < 0 or > 20 ||
            input.SummaryScalarLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Reward attestations are outside their bounded range.");
        }

        var baseXp = BaseXp(input.QuestType);
        var components = new List<RewardComponent>
        {
            new("reward.base", baseXp),
        };

        var bonusXp = 0;
        if (input.HasObservedTestsPassed)
        {
            bonusXp += 10;
            components.Add(new RewardComponent("reward.observed_tests_passed", 10));
        }

        if (input.ScopeViolations == 0)
        {
            bonusXp += 10;
            components.Add(new RewardComponent("reward.clean_scope", 10));
        }

        if (input.SummaryScalarLength >= 40)
        {
            bonusXp += 10;
            components.Add(new RewardComponent("reward.summary", 10));
        }

        if (input.UserCorrections == 0)
        {
            bonusXp += 5;
            components.Add(new RewardComponent("reward.no_user_corrections", 5));
        }

        var scopePenalty = Math.Min(input.ScopeViolations, 3) * 5;
        var correctionPenalty = Math.Min(input.UserCorrections, 3) * 5;
        if (scopePenalty > 0)
        {
            components.Add(new RewardComponent("penalty.scope_violation", -scopePenalty));
        }

        if (correctionPenalty > 0)
        {
            components.Add(new RewardComponent("penalty.user_correction", -correctionPenalty));
        }

        var penaltyXp = checked(scopePenalty + correctionPenalty);
        var rawXp = Math.Max(0, checked(baseXp + bonusXp - penaltyXp));
        var outcomePermille = OutcomePermille(input.Outcome);
        var questXp = checked((rawXp * outcomePermille) / 1000);

        return new RewardBreakdown(
            baseXp,
            bonusXp,
            penaltyXp,
            rawXp,
            outcomePermille,
            questXp,
            RuleVersion,
            components);
    }

    public static int BaseXp(QuestType questType) => questType switch
    {
        QuestType.Planning => 30,
        QuestType.Research => 40,
        QuestType.Coding => 60,
        QuestType.Review => 50,
        QuestType.Debugging => 70,
        QuestType.Documentation => 40,
        QuestType.Maintenance => 40,
        _ => throw new ArgumentOutOfRangeException(nameof(questType)),
    };

    public static int OutcomePermille(QuestOutcome outcome) => outcome switch
    {
        QuestOutcome.Success => 1000,
        QuestOutcome.Partial => 600,
        QuestOutcome.Blocked => 300,
        QuestOutcome.Failed => 100,
        QuestOutcome.Abandoned => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };
}

public sealed record SkillXpAllocation(string SkillKey, int Xp, string RuleVersion);

public static class SkillAllocationRules
{
    public const string RuleVersion = "skill-allocation/1.0.0";

    private static readonly string[] CanonicalSkills =
    [
        "coding",
        "testing_awareness",
        "scope_control",
        "documentation",
        "tool_use",
        "planning",
        "research",
        "debugging",
        "review",
        "maintenance",
    ];

    public static IReadOnlyList<SkillXpAllocation> Allocate(int questXp, IReadOnlyList<string> skills)
    {
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentOutOfRangeException.ThrowIfNegative(questXp);

        if (skills.Count is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(skills), "One to three Skills are required.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var skill in skills)
        {
            if (!IsCanonicalSkill(skill) || !seen.Add(skill))
            {
                throw new ArgumentException("Skills must be unique canonical keys.", nameof(skills));
            }
        }

        var result = new SkillXpAllocation[skills.Count];
        if (skills.Count == 1)
        {
            result[0] = new SkillXpAllocation(skills[0], questXp, RuleVersion);
            return result;
        }

        if (skills.Count == 2)
        {
            var firstBoundary = checked((questXp * 60) / 100);
            result[0] = new SkillXpAllocation(skills[0], firstBoundary, RuleVersion);
            result[1] = new SkillXpAllocation(skills[1], questXp - firstBoundary, RuleVersion);
            return result;
        }

        var first = checked((questXp * 50) / 100);
        var secondBoundary = checked((questXp * 80) / 100);
        result[0] = new SkillXpAllocation(skills[0], first, RuleVersion);
        result[1] = new SkillXpAllocation(skills[1], secondBoundary - first, RuleVersion);
        result[2] = new SkillXpAllocation(skills[2], questXp - secondBoundary, RuleVersion);
        return result;
    }

    public static bool IsCanonicalSkill(string? skill)
    {
        if (skill is null)
        {
            return false;
        }

        foreach (var candidate in CanonicalSkills)
        {
            if (string.Equals(candidate, skill, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
