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

public sealed record QuestRewardSnapshot(
    int BaseXp,
    int BonusXp,
    int PenaltyXp,
    int RawXp,
    int OutcomePermille,
    long XpGained,
    string RewardRuleVersion);

public sealed record HeroProgressSnapshot(
    HeroId HeroId,
    long TotalXpBefore,
    long TotalXpAfter,
    int LevelBefore,
    int LevelAfter,
    string RankBefore,
    string RankAfter,
    string HeroProgressionVersion,
    string RankRuleVersion);

public sealed record FinishQuestResult(
    QuestId QuestId,
    string Result,
    QuestRewardSnapshot Reward,
    HeroProgressSnapshot HeroProgress,
    bool Replayed,
    bool AlreadyFinalized);
