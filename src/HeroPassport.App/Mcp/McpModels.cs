using HeroPassport.Application.Runtime;

namespace HeroPassport.App.Mcp;

public sealed record McpSettings(string Locale, string PresentationStyle, bool AutoStartQuest, bool AutoFinishQuest);

public sealed record McpHero(
    string HeroId,
    string Name,
    long TotalXp,
    int Level,
    string RankKey,
    int Trust,
    int Strain,
    bool Archived);

public sealed record McpQuest(
    string QuestId,
    string HeroId,
    string QuestType,
    string Title,
    string Goal,
    string StartedAtUtc,
    string Locale);

public sealed record McpBootstrapResult(bool SetupCompleted, McpHero Hero, McpSettings Settings, bool Replayed, string DisplayText);
public sealed record McpConfigureResult(McpSettings Settings, bool Changed, string DisplayText);
public sealed record McpCreateHeroResult(McpHero Hero, bool Replayed, string DisplayText);
public sealed record McpHeroListItem(string HeroId, string Name, bool Archived, bool Active, long TotalXp, int Level, string RankKey, int Trust, int Strain);
public sealed record McpListHeroesResult(IReadOnlyList<McpHeroListItem> Heroes, string DisplayText);
public sealed record McpLifecycleResult(McpHero Hero, bool AlreadyInRequestedState, string DisplayText);
public sealed record McpActivationResult(string HeroId, bool Active, string DisplayText);
public sealed record McpStartQuestResult(McpQuest Quest, McpHero Hero, bool Replayed, string DisplayText);

public sealed record McpOpenQuest(
    string QuestId,
    string HeroId,
    string HeroName,
    string QuestType,
    string Title,
    string Goal,
    string StartedAtUtc,
    string Locale);

public sealed record McpProjectContext(string DisplayName);

public sealed record McpRuntimeContextResult(
    string ProductVersion,
    string ContractVersion,
    string SkillContractVersion,
    bool SetupCompleted,
    McpSettings? Settings,
    McpHero? ActiveHero,
    McpProjectContext Project,
    IReadOnlyList<McpOpenQuest> OpenQuests,
    RuleVersions RuleVersions,
    string DisplayText);

public sealed record McpFinishMetricsInput(
    bool TestsMentioned,
    int ScopeViolations,
    int UserCorrections,
    string BuildStatus,
    string BuildEvidence,
    string TestsStatus,
    string TestsEvidence);

public sealed record McpReward(
    int BaseXp,
    int BonusXp,
    int PenaltyXp,
    int RawXp,
    int OutcomePermille,
    int XpGained,
    string RewardRuleVersion);

public sealed record McpHeroProgress(
    string HeroId,
    long TotalXpBefore,
    long TotalXpAfter,
    int LevelBefore,
    int LevelAfter,
    string RankBefore,
    string RankAfter);

public sealed record McpFinishQuestResult(
    string QuestId,
    string Result,
    bool Replayed,
    bool AlreadyFinalized,
    McpReward Reward,
    McpHeroProgress HeroProgress,
    string DisplayText);

public sealed record McpHeroCardSkill(string SkillKey, long Xp, int Level, bool IsLevelCapped, int? NextLevelXpRequired);
public sealed record McpHeroCardHero(
    string HeroId,
    string Name,
    long TotalXp,
    int Level,
    bool IsLevelCapped,
    long LevelXp,
    int? NextLevelXpRequired,
    string RankKey,
    string ActiveTitle,
    int Trust,
    int Strain,
    int SuccessStreak,
    IReadOnlyList<McpHeroCardSkill> TopSkills,
    IReadOnlyList<string> Traits,
    IReadOnlyList<string> Titles);

public sealed record McpHeroCardProject(
    string DisplayName,
    int QuestsStarted,
    int QuestsFinished,
    int QuestsSucceeded,
    long TotalXpEarned,
    int SuccessRatePermille,
    IReadOnlyList<McpHeroCardSkill> TopSkills);

public sealed record McpHeroCardResult(McpHeroCardHero Hero, McpHeroCardProject Project, string DisplayText);
