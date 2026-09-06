using HeroPassport.Domain.Primitives;

namespace HeroPassport.Application.Runtime;

public sealed record FinishQuestMetrics(
    bool TestsMentioned,
    int ScopeViolations,
    int UserCorrections,
    string BuildStatus,
    string BuildEvidence,
    string TestsStatus,
    string TestsEvidence);

public sealed record FinishQuestRequest(
    MutationRequestId FinishRequestId,
    QuestId QuestId,
    string Result,
    string Summary,
    FinishQuestMetrics Metrics,
    IReadOnlyList<string> SkillsUsed);

public sealed record RewardComponentSnapshot(string Key, long XpDelta);

public sealed record QuestRewardSnapshot(
    int BaseXp,
    int BonusXp,
    int PenaltyXp,
    int RawXp,
    int OutcomePermille,
    long XpGained,
    IReadOnlyList<RewardComponentSnapshot> Components,
    string RewardRuleVersion);

public sealed record HeroProgressSnapshot(
    HeroId HeroId,
    long TotalXpBefore,
    long TotalXpAfter,
    int LevelBefore,
    int LevelAfter,
    bool IsLevelCapped,
    long LevelXp,
    long? NextLevelXpRequired,
    string RankBefore,
    string RankAfter,
    string HeroProgressionVersion,
    string RankRuleVersion);

public sealed record TrustStrainComponentSnapshot(
    string Key,
    int TrustDelta,
    int StrainDelta);

public sealed record TrustStrainSnapshot(
    int TrustBefore,
    int TrustAfter,
    int StrainBefore,
    int StrainAfter,
    IReadOnlyList<TrustStrainComponentSnapshot> Components,
    string RuleVersion);

public sealed record StreakSnapshot(
    long Before,
    long After,
    string RuleVersion);

public sealed record SkillProgressSnapshot(
    string SkillKey,
    long XpGained,
    long XpAfter,
    int LevelBefore,
    int LevelAfter,
    bool IsLevelCapped,
    long? NextLevelXpRequired);

public sealed record MilestoneSnapshot(string EventKey, string SemanticKey);

public sealed record FinishQuestResult(
    QuestId QuestId,
    string Result,
    QuestRewardSnapshot Reward,
    HeroProgressSnapshot HeroProgress,
    TrustStrainSnapshot TrustStrain,
    StreakSnapshot Streak,
    IReadOnlyList<SkillProgressSnapshot> SkillProgress,
    IReadOnlyList<string> TraitsUnlocked,
    IReadOnlyList<string> TitlesUnlocked,
    string? ActiveTitle,
    IReadOnlyList<MilestoneSnapshot> Milestones,
    bool Replayed,
    bool AlreadyFinalized);
