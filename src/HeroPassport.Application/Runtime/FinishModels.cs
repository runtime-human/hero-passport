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

public sealed record RewardSummary(
    int BaseXp,
    int BonusXp,
    int PenaltyXp,
    int RawXp,
    int OutcomePermille,
    int XpGained,
    string RewardRuleVersion);

public sealed record HeroProgressSummary(
    HeroId HeroId,
    long TotalXpBefore,
    long TotalXpAfter,
    int LevelBefore,
    int LevelAfter,
    string RankBefore,
    string RankAfter);

public sealed record FinishQuestResult(
    QuestId QuestId,
    string Result,
    bool Replayed,
    bool AlreadyFinalized,
    RewardSummary Reward,
    HeroProgressSummary HeroProgress);
