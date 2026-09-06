using HeroPassport.Domain.Primitives;

namespace HeroPassport.Domain.Engine;

public sealed record SkillXpAllocation(string SkillKey, long XpGained, string RuleVersion);

public static class SkillAllocationRules
{
    public const string RuleVersion = "skill-allocation/1.0.0";

    public static IReadOnlyList<SkillXpAllocation> Allocate(long questXp, IReadOnlyList<string> skillsUsed)
    {
        JsonSafeInteger.Require(questXp);
        ArgumentNullException.ThrowIfNull(skillsUsed);
        if (skillsUsed.Count is < 1 or > 3)
        {
            throw new ArgumentException("Skill allocation requires one to three canonical skills.", nameof(skillsUsed));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < skillsUsed.Count; index++)
        {
            var skill = skillsUsed[index];
            if (!IsCanonicalSkill(skill) || !seen.Add(skill))
            {
                throw new ArgumentException("Skill allocation requires distinct canonical skills.", nameof(skillsUsed));
            }
        }

        var allocations = new SkillXpAllocation[skillsUsed.Count];
        switch (skillsUsed.Count)
        {
            case 1:
                allocations[0] = new SkillXpAllocation(skillsUsed[0], questXp, RuleVersion);
                break;

            case 2:
            {
                var first = PercentageFloor(questXp, 60);
                allocations[0] = new SkillXpAllocation(skillsUsed[0], first, RuleVersion);
                allocations[1] = new SkillXpAllocation(skillsUsed[1], checked(questXp - first), RuleVersion);
                break;
            }

            case 3:
            {
                var first = PercentageFloor(questXp, 50);
                var firstTwo = PercentageFloor(questXp, 80);
                allocations[0] = new SkillXpAllocation(skillsUsed[0], first, RuleVersion);
                allocations[1] = new SkillXpAllocation(skillsUsed[1], checked(firstTwo - first), RuleVersion);
                allocations[2] = new SkillXpAllocation(skillsUsed[2], checked(questXp - firstTwo), RuleVersion);
                break;
            }
        }

        return allocations;
    }

    private static long PercentageFloor(long value, int percent) =>
        checked(value * percent / 100L);

    private static bool IsCanonicalSkill(string? skill) => skill is
        "coding" or
        "testing_awareness" or
        "scope_control" or
        "documentation" or
        "tool_use" or
        "planning" or
        "research" or
        "debugging" or
        "review" or
        "maintenance";
}
