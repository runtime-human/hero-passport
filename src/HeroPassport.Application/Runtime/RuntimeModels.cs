using HeroPassport.Domain.Primitives;

namespace HeroPassport.Application.Runtime;

public sealed record SettingsSnapshot(
    string Locale,
    string PresentationStyle,
    bool AutoStartQuest,
    bool AutoFinishQuest);

public sealed record HeroSummary(
    HeroId HeroId,
    string Name,
    long TotalXp,
    int Level,
    string RankKey,
    int Trust,
    int Strain,
    bool Archived);

public sealed record BootstrapRequest(
    MutationRequestId BootstrapRequestId,
    string Locale,
    string HeroName,
    string PresentationStyle,
    bool AutoStartQuest,
    bool AutoFinishQuest);

public sealed record BootstrapResult(
    bool SetupCompleted,
    HeroSummary Hero,
    SettingsSnapshot Settings,
    bool Replayed);

public sealed record ConfigureRequest(
    string Locale,
    string PresentationStyle,
    bool AutoStartQuest,
    bool AutoFinishQuest);

public sealed record ConfigureResult(SettingsSnapshot Settings, bool Changed);

public sealed record CreateHeroRequest(MutationRequestId CreateRequestId, string Name);

public sealed record CreateHeroResult(HeroSummary Hero, bool Replayed);

public sealed record ProjectBindingContext(
    string DisplayName,
    string WorkspaceFingerprint,
    string IdentityVersion);

public sealed record ProjectContextSnapshot(string DisplayName);

public sealed record OpenQuestContext(
    QuestId QuestId,
    HeroId HeroId,
    string HeroName,
    string QuestType,
    string Title,
    string Goal,
    DateTimeOffset StartedAtUtc,
    string Locale);

public sealed record RuleVersions(
    string Reward,
    string HeroProgression,
    string SkillProgression,
    string SkillAllocation,
    string TrustStrain,
    string Streak,
    string Unlock,
    string Rank);

public sealed record RuntimeContextResult(
    string ProductVersion,
    string ContractVersion,
    string SkillContractVersion,
    bool SetupCompleted,
    SettingsSnapshot? Settings,
    HeroSummary? ActiveHero,
    ProjectContextSnapshot Project,
    IReadOnlyList<OpenQuestContext> OpenQuests,
    RuleVersions RuleVersions);

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
