using HeroPassport.Domain.Primitives;

namespace HeroPassport.Domain.Engine;

public sealed record QuestQualitySignals(
    bool TestsMentioned,
    int ScopeViolations,
    int UserCorrections,
    string BuildStatus,
    string BuildEvidence,
    string TestsStatus,
    string TestsEvidence,
    int SummaryScalarLength);

public sealed record RewardComponent(string Key, int XpDelta);

public sealed record QuestRewardCalculation(
    int BaseXp,
    int BonusXp,
    int PenaltyXp,
    int RawXp,
    int OutcomePermille,
    long XpGained,
    IReadOnlyList<RewardComponent> Components,
    string RuleVersion);

public sealed record SkillXpAllocation(string SkillKey, long XpGained);

public static class QuestRewardEngine
{
    public const string RuleVersion = "reward/2.0.0";

    public static QuestRewardCalculation Calculate(
        string questType,
        string result,
        QuestQualitySignals signals,
        string ruleVersion)
    {
        ArgumentNullException.ThrowIfNull(signals);
        RequireVersion(ruleVersion);
        RequireSignals(signals);

        var baseXp = MinimalQuestFinishRules.BaseXp(questType);
        var outcomePermille = MinimalQuestFinishRules.OutcomePermille(result);
        var components = new List<RewardComponent>(capacity: 6);

        if (string.Equals(signals.TestsStatus, "passed", StringComparison.Ordinal) &&
            string.Equals(signals.TestsEvidence, "observed", StringComparison.Ordinal))
        {
            components.Add(new RewardComponent("observed_tests_passed", 10));
        }

        if (signals.ScopeViolations == 0)
        {
            components.Add(new RewardComponent("clean_scope", 10));
        }

        if (signals.SummaryScalarLength >= 40)
        {
            components.Add(new RewardComponent("clear_summary", 10));
        }

        if (signals.UserCorrections == 0)
        {
            components.Add(new RewardComponent("no_user_corrections", 5));
        }

        if (signals.ScopeViolations > 0)
        {
            components.Add(new RewardComponent("scope_violations", -Math.Min(signals.ScopeViolations, 3) * 5));
        }

        if (signals.UserCorrections > 0)
        {
            components.Add(new RewardComponent("user_corrections", -Math.Min(signals.UserCorrections, 3) * 5));
        }

        var bonusXp = components.Where(static component => component.XpDelta > 0).Sum(static component => component.XpDelta);
        var penaltyXp = -components.Where(static component => component.XpDelta < 0).Sum(static component => component.XpDelta);
        var rawXp = Math.Max(0, checked(baseXp + bonusXp - penaltyXp));
        var xpGained = MinimalQuestFinishRules.QuestXp(rawXp, outcomePermille);
        JsonSafeInteger.Require(xpGained);

        return new QuestRewardCalculation(
            baseXp,
            bonusXp,
            penaltyXp,
            rawXp,
            outcomePermille,
            xpGained,
            components.ToArray(),
            RuleVersion);
    }

    private static void RequireVersion(string ruleVersion)
    {
        if (!string.Equals(ruleVersion, RuleVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported reward rule version.", nameof(ruleVersion));
        }
    }

    private static void RequireSignals(QuestQualitySignals signals)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(signals.ScopeViolations, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(signals.ScopeViolations, 20);
        ArgumentOutOfRangeException.ThrowIfLessThan(signals.UserCorrections, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(signals.UserCorrections, 20);
        ArgumentOutOfRangeException.ThrowIfLessThan(signals.SummaryScalarLength, 0);
    }
}

public static class SkillXpAllocator
{
    public const string RuleVersion = "skill-allocation/1.0.0";

    public static IReadOnlyList<SkillXpAllocation> Allocate(
        long questXp,
        IReadOnlyList<string> orderedSkills,
        string ruleVersion)
    {
        ArgumentNullException.ThrowIfNull(orderedSkills);
        RequireVersion(ruleVersion);
        JsonSafeInteger.Require(questXp);

        if (orderedSkills.Count is < 1 or > 3)
        {
            throw new ArgumentException("Skill allocation requires one to three Skills.", nameof(orderedSkills));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var skill in orderedSkills)
        {
            if (string.IsNullOrWhiteSpace(skill) || !seen.Add(skill))
            {
                throw new ArgumentException("Skill allocation requires unique canonical Skills.", nameof(orderedSkills));
            }
        }

        return orderedSkills.Count switch
        {
            1 => [new SkillXpAllocation(orderedSkills[0], questXp)],
            2 => AllocateTwo(questXp, orderedSkills),
            3 => AllocateThree(questXp, orderedSkills),
            _ => throw new InvalidOperationException("Unreachable Skill allocation shape."),
        };
    }

    private static SkillXpAllocation[] AllocateTwo(long questXp, IReadOnlyList<string> skills)
    {
        var first = checked(questXp * 60L / 100L);
        return
        [
            new SkillXpAllocation(skills[0], first),
            new SkillXpAllocation(skills[1], checked(questXp - first)),
        ];
    }

    private static SkillXpAllocation[] AllocateThree(long questXp, IReadOnlyList<string> skills)
    {
        var firstBoundary = checked(questXp * 50L / 100L);
        var secondBoundary = checked(questXp * 80L / 100L);
        return
        [
            new SkillXpAllocation(skills[0], firstBoundary),
            new SkillXpAllocation(skills[1], checked(secondBoundary - firstBoundary)),
            new SkillXpAllocation(skills[2], checked(questXp - secondBoundary)),
        ];
    }

    private static void RequireVersion(string ruleVersion)
    {
        if (!string.Equals(ruleVersion, RuleVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported Skill allocation rule version.", nameof(ruleVersion));
        }
    }
}
