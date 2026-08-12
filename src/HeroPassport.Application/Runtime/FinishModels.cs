using HeroPassport.Domain.Primitives;

namespace HeroPassport.Application.Runtime;

public sealed record FinishMetrics(
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
    FinishMetrics Metrics,
    IReadOnlyList<string> SkillsUsed);

public sealed record RewardComponentSummary(string Key, int XpDelta);

public sealed record RewardSummary(
    int BaseXp,
    int BonusXp,
    int PenaltyXp,
    int RawXp,
    int OutcomePermille,
    int XpGained,
    string RewardRuleVersion,
    IReadOnlyList<RewardComponentSummary> Components);

public sealed record HeroProgressSummary(
    HeroId HeroId,
    long TotalXpBefore,
    long TotalXpAfter,
    int LevelBefore,
    int LevelAfter,
    bool IsLevelCapped,
    long LevelXp,
    long? NextLevelXpRequired,
    string RankBefore,
    string RankAfter);

public sealed record TrustStrainComponentSummary(string Key, int TrustDelta, int StrainDelta);

public sealed record TrustStrainSummary(
    int TrustBefore,
    int TrustAfter,
    int StrainBefore,
    int StrainAfter,
    string RuleVersion,
    IReadOnlyList<TrustStrainComponentSummary> Components);

public sealed record StreakProgressSummary(int Before, int After, string RuleVersion);

public sealed record SkillProgressSummary(
    string SkillKey,
    int XpGained,
    long XpBefore,
    long XpAfter,
    int LevelBefore,
    int LevelAfter,
    bool IsLevelCapped,
    long? NextLevelXpRequired);

public sealed record MilestoneSummary(string EventKey, string SemanticKey);

public sealed record FinishQuestResult(
    QuestId QuestId,
    string Result,
    bool Replayed,
    bool AlreadyFinalized,
    RewardSummary Reward,
    HeroProgressSummary HeroProgress,
    TrustStrainSummary TrustStrain,
    StreakProgressSummary Streak,
    IReadOnlyList<SkillProgressSummary> SkillProgress,
    IReadOnlyList<string> TraitsUnlocked,
    IReadOnlyList<string> TitlesUnlocked,
    string? ActiveTitle,
    IReadOnlyList<MilestoneSummary> Milestones);
