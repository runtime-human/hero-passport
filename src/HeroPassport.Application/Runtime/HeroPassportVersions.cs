using HeroPassport.Domain.Engine;
using System.Reflection;

namespace HeroPassport.Application.Runtime;

public static class HeroPassportVersions
{
    public static string ProductVersion { get; } = ResolveProductVersion();
    public const string ContractVersion = "HP-MCP/2";
    public const string SkillContractVersion = "hero-passport-skill/1";
    public const string MutationArgsVersion = "mutation-args/1";

    public static RuleVersions CurrentRules { get; } = new(
        QuestRewardRules.RuleVersion,
        HeroProgressionRules.RuleVersion,
        SkillProgressionRules.RuleVersion,
        SkillAllocationRules.RuleVersion,
        TrustStrainRules.RuleVersion,
        StreakRules.RuleVersion,
        UnlockRules.RuleVersion,
        RankRules.RuleVersion);

    private static string ResolveProductVersion()
    {
        var informationalVersion = typeof(HeroPassportVersions)
            .Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            throw new InvalidOperationException("Hero Passport assembly informational version is missing.");
        }

        return informationalVersion;
    }
}
