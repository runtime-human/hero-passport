namespace HeroPassport.Application.Runtime;

public static class HeroPassportVersions
{
    public const string ProductVersion = "0.1.0-dev";
    public const string ContractVersion = "HP-MCP/2";
    public const string SkillContractVersion = "hero-passport-skill/1";
    public const string MutationArgsVersion = "mutation-args/1";

    public static RuleVersions CurrentRules { get; } = new(
        "reward/2.0.0",
        "hero-progression/2.0.0",
        "skill-progression/2.0.0",
        "skill-allocation/1.0.0",
        "trust-strain/1.0.0",
        "streak/1.0.0",
        "unlock/2.0.0",
        "rank/1.0.0");
}
