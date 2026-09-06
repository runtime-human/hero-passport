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

public sealed class QuestRewardSnapshot : IEquatable<QuestRewardSnapshot>
{
    public QuestRewardSnapshot(
        int baseXp,
        int bonusXp,
        int penaltyXp,
        int rawXp,
        int outcomePermille,
        long xpGained,
        IReadOnlyList<RewardComponentSnapshot> components,
        string rewardRuleVersion)
    {
        BaseXp = baseXp;
        BonusXp = bonusXp;
        PenaltyXp = penaltyXp;
        RawXp = rawXp;
        OutcomePermille = outcomePermille;
        XpGained = xpGained;
        Components = components;
        RewardRuleVersion = rewardRuleVersion;
    }

    public int BaseXp { get; }
    public int BonusXp { get; }
    public int PenaltyXp { get; }
    public int RawXp { get; }
    public int OutcomePermille { get; }
    public long XpGained { get; }
    public IReadOnlyList<RewardComponentSnapshot> Components { get; }
    public string RewardRuleVersion { get; }

    public bool Equals(QuestRewardSnapshot? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null &&
            BaseXp == other.BaseXp &&
            BonusXp == other.BonusXp &&
            PenaltyXp == other.PenaltyXp &&
            RawXp == other.RawXp &&
            OutcomePermille == other.OutcomePermille &&
            XpGained == other.XpGained &&
            string.Equals(RewardRuleVersion, other.RewardRuleVersion, StringComparison.Ordinal) &&
            Components.SequenceEqual(other.Components);
    }

    public override bool Equals(object? obj) => obj is QuestRewardSnapshot other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(BaseXp);
        hash.Add(BonusXp);
        hash.Add(PenaltyXp);
        hash.Add(RawXp);
        hash.Add(OutcomePermille);
        hash.Add(XpGained);
        hash.Add(RewardRuleVersion, StringComparer.Ordinal);
        foreach (var component in Components)
        {
            hash.Add(component);
        }
        return hash.ToHashCode();
    }
}

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
