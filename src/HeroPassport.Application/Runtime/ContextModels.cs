using HeroPassport.Domain.Primitives;

namespace HeroPassport.Application.Runtime;

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
    HeroIdentitySnapshot? ActiveHero,
    ProjectContextSnapshot Project,
    IReadOnlyList<OpenQuestContext> OpenQuests,
    RuleVersions RuleVersions);
