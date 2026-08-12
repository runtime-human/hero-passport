using HeroPassport.Application.Runtime;
using Xunit;

namespace HeroPassport.Application.Tests;

internal static class FinishResultAssertions
{
    public static void EqualPersisted(FinishQuestResult expected, FinishQuestResult actual)
    {
        Assert.Equal(expected.QuestId, actual.QuestId);
        Assert.Equal(expected.Result, actual.Result);
        Assert.Equal(expected.Reward.BaseXp, actual.Reward.BaseXp);
        Assert.Equal(expected.Reward.BonusXp, actual.Reward.BonusXp);
        Assert.Equal(expected.Reward.PenaltyXp, actual.Reward.PenaltyXp);
        Assert.Equal(expected.Reward.RawXp, actual.Reward.RawXp);
        Assert.Equal(expected.Reward.OutcomePermille, actual.Reward.OutcomePermille);
        Assert.Equal(expected.Reward.XpGained, actual.Reward.XpGained);
        Assert.Equal(expected.Reward.RewardRuleVersion, actual.Reward.RewardRuleVersion);
        Assert.Equal(expected.Reward.Components.ToArray(), actual.Reward.Components.ToArray());
        Assert.Equal(expected.HeroProgress, actual.HeroProgress);
        Assert.Equal(expected.TrustStrain.TrustBefore, actual.TrustStrain.TrustBefore);
        Assert.Equal(expected.TrustStrain.TrustAfter, actual.TrustStrain.TrustAfter);
        Assert.Equal(expected.TrustStrain.StrainBefore, actual.TrustStrain.StrainBefore);
        Assert.Equal(expected.TrustStrain.StrainAfter, actual.TrustStrain.StrainAfter);
        Assert.Equal(expected.TrustStrain.RuleVersion, actual.TrustStrain.RuleVersion);
        Assert.Equal(expected.TrustStrain.Components.ToArray(), actual.TrustStrain.Components.ToArray());
        Assert.Equal(expected.Streak, actual.Streak);
        Assert.Equal(expected.SkillProgress.ToArray(), actual.SkillProgress.ToArray());
        Assert.Equal(expected.TraitsUnlocked.ToArray(), actual.TraitsUnlocked.ToArray());
        Assert.Equal(expected.TitlesUnlocked.ToArray(), actual.TitlesUnlocked.ToArray());
        Assert.Equal(expected.ActiveTitle, actual.ActiveTitle);
        Assert.Equal(expected.Milestones.ToArray(), actual.Milestones.ToArray());
    }
}
