using HeroPassport.Domain.Engine;
using HeroPassport.Domain.Primitives;
using Xunit;

namespace HeroPassport.Domain.Tests;

public sealed class TrustStrainStreakTests
{
    [Theory]
    [InlineData("success", 52, 18)]
    [InlineData("partial", 50, 21)]
    [InlineData("blocked", 50, 20)]
    [InlineData("failed", 50, 22)]
    [InlineData("abandoned", 50, 20)]
    public void OutcomeGoldensMatchTrustStrainV1(string result, int expectedTrust, int expectedStrain)
    {
        var quality = QuestQualityFlags.From(60, "not_run", "none", 0, 0);

        var applied = TrustStrainRules.Apply(
            50,
            20,
            result,
            quality,
            0,
            0,
            TrustStrainRules.RuleVersion);

        Assert.Equal(expectedTrust, applied.TrustAfter);
        Assert.Equal(expectedStrain, applied.StrainAfter);
    }

    [Fact]
    public void CleanObservedSuccessCapsPositiveTrustBeforeNegativeComponents()
    {
        var quality = QuestQualityFlags.From(60, "passed", "observed", 0, 0);

        var applied = TrustStrainRules.Apply(
            50,
            20,
            "success",
            quality,
            0,
            0,
            TrustStrainRules.RuleVersion);

        Assert.Equal(52, applied.TrustAfter);
        Assert.Equal(18, applied.StrainAfter);
        Assert.Collection(
            applied.Components,
            component => Assert.Equal(new TrustStrainComponent("success_outcome", 1, -1), component),
            component => Assert.Equal(new TrustStrainComponent("clean_success_bonus", 1, -1), component),
            component => Assert.Equal(new TrustStrainComponent("observed_tests_passed_bonus", 1, 0), component),
            component => Assert.Equal(new TrustStrainComponent("positive_trust_cap_adjustment", -1, 0), component));
    }

    [Fact]
    public void NegativeSignalCountsCapAtThreeEach()
    {
        var quality = QuestQualityFlags.From(60, "not_run", "none", 20, 20);

        var applied = TrustStrainRules.Apply(
            50,
            20,
            "success",
            quality,
            20,
            20,
            TrustStrainRules.RuleVersion);

        Assert.Equal(45, applied.TrustAfter);
        Assert.Equal(25, applied.StrainAfter);
        Assert.Collection(
            applied.Components,
            component => Assert.Equal(new TrustStrainComponent("success_outcome", 1, -1), component),
            component => Assert.Equal(new TrustStrainComponent("scope_violation_penalty", -3, 3), component),
            component => Assert.Equal(new TrustStrainComponent("user_correction_penalty", -3, 3), component));
    }

    [Fact]
    public void TrustAndStrainClampOnlyAfterAllQuestComponents()
    {
        var positive = QuestQualityFlags.From(60, "not_run", "none", 0, 0);
        var upperTrustLowerStrain = TrustStrainRules.Apply(
            100,
            0,
            "success",
            positive,
            0,
            0,
            TrustStrainRules.RuleVersion);
        Assert.Equal(100, upperTrustLowerStrain.TrustAfter);
        Assert.Equal(0, upperTrustLowerStrain.StrainAfter);

        var negative = QuestQualityFlags.From(60, "not_run", "none", 20, 20);
        var lowerTrustUpperStrain = TrustStrainRules.Apply(
            0,
            100,
            "failed",
            negative,
            20,
            20,
            TrustStrainRules.RuleVersion);
        Assert.Equal(0, lowerTrustUpperStrain.TrustAfter);
        Assert.Equal(100, lowerTrustUpperStrain.StrainAfter);
    }

    [Fact]
    public void AbandonedIgnoresOtherwisePositiveAndNegativeSignals()
    {
        var quality = QuestQualityFlags.From(60, "passed", "observed", 3, 3);

        var applied = TrustStrainRules.Apply(
            73,
            41,
            "abandoned",
            quality,
            3,
            3,
            TrustStrainRules.RuleVersion);

        Assert.Equal(73, applied.TrustAfter);
        Assert.Equal(41, applied.StrainAfter);
        Assert.Empty(applied.Components);
    }

    [Fact]
    public void TrustStrainGuardsVersionBoundsAndQualityConsistency()
    {
        var clean = QuestQualityFlags.From(60, "not_run", "none", 0, 0);

        Assert.Throws<ArgumentException>(() => TrustStrainRules.Apply(50, 20, "success", clean, 0, 0, "trust-strain/0"));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrustStrainRules.Apply(-1, 20, "success", clean, 0, 0, TrustStrainRules.RuleVersion));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrustStrainRules.Apply(50, 101, "success", clean, 0, 0, TrustStrainRules.RuleVersion));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrustStrainRules.Apply(50, 20, "unknown", clean, 0, 0, TrustStrainRules.RuleVersion));
        Assert.Throws<ArgumentException>(() => TrustStrainRules.Apply(50, 20, "success", clean, 1, 0, TrustStrainRules.RuleVersion));
    }

    [Theory]
    [InlineData("success", 7, 8)]
    [InlineData("partial", 7, 0)]
    [InlineData("blocked", 7, 0)]
    [InlineData("failed", 7, 0)]
    [InlineData("abandoned", 7, 0)]
    public void StreakV1IncrementsOnlySuccess(string result, long before, long expectedAfter)
    {
        var applied = StreakRules.Apply(before, result, StreakRules.RuleVersion);

        Assert.Equal(before, applied.Before);
        Assert.Equal(expectedAfter, applied.After);
        Assert.Equal(StreakRules.RuleVersion, applied.RuleVersion);
    }

    [Fact]
    public void StreakRejectsUnsupportedInputsAndJsonSafeOverflow()
    {
        Assert.Throws<ArgumentException>(() => StreakRules.Apply(0, "success", "streak/0"));
        Assert.Throws<ArgumentOutOfRangeException>(() => StreakRules.Apply(-1, "success", StreakRules.RuleVersion));
        Assert.Throws<ArgumentOutOfRangeException>(() => StreakRules.Apply(0, "unknown", StreakRules.RuleVersion));
        Assert.Throws<ArgumentOutOfRangeException>(() => StreakRules.Apply(JsonSafeInteger.Maximum, "success", StreakRules.RuleVersion));
    }
}
