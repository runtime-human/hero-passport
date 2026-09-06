using HeroPassport.Domain.Engine;

namespace HeroPassport.Application.Runtime;

public static class HeroPassportVersions
{
    public const string ProductVersion = "0.1.0-dev";
    public const string ContractVersion = "HP-MCP/2";
    public const string SkillContractVersion = "hero-passport-skill/1";
    public const string MutationArgsVersion = "mutation-args/1";

    public static RuleVersions CurrentRules { get; } = new(
        QuestRewardRules.RuleVersion,
        "hero-progression/2.0.0",
        SkillProgressionRules.RuleVersion,
        SkillAllocationRules.RuleVersion,
        TrustStrainRules.RuleVersion,
        StreakRules.RuleVersion,
        "unlock/2.0.0",
        "rank/1.0.0");
}
