using HeroPassport.Domain.Game;
using Xunit;

namespace HeroPassport.Domain.Tests;

public sealed class TrustStrainRulesTests
{
    [Fact]
    public void CleanSuccessWithObservedTestsUsesPositiveCaps()
    {
        var result = TrustStrainRules.Calculate(
            50,
            20,
            new TrustStrainInput(QuestOutcome.Success, ObservedTestsPassed: true, ScopeViolations: 0, UserCorrections: 0));

        Assert.Equal(2, result.TrustDelta);
        Assert.Equal(-2, result.StrainDelta);
        Assert.Equal(52, result.TrustAfter);
        Assert.Equal(18, result.StrainAfter);
        Assert.Equal("trust-strain/1.0.0", result.RuleVersion);
        Assert.Equal(result.TrustDelta, result.Components.Sum(static component => component.TrustDelta));
        Assert.Equal(result.StrainDelta, result.Components.Sum(static component => component.StrainDelta));
        Assert.Contains(result.Components, static component => component.Key == "trust.positive_cap" && component.TrustDelta == -1);
    }

    [Theory]
    [InlineData(QuestOutcome.Partial, 0, 1)]
    [InlineData(QuestOutcome.Blocked, 0, 0)]
    [InlineData(QuestOutcome.Failed, 0, 2)]
    public void OutcomeBaselinesMatchVersionedTable(QuestOutcome outcome, int trustDelta, int strainDelta)
    {
        var result = TrustStrainRules.Calculate(
            50,
            20,
            new TrustStrainInput(outcome, ObservedTestsPassed: false, ScopeViolations: 0, UserCorrections: 0));

        Assert.Equal(trustDelta, result.TrustDelta);
        Assert.Equal(strainDelta, result.StrainDelta);
    }

    [Fact]
    public void FailedQuestWithOneCorrectionMatchesGoldenVector()
    {
        var result = TrustStrainRules.Calculate(
            50,
            20,
            new TrustStrainInput(QuestOutcome.Failed, ObservedTestsPassed: false, ScopeViolations: 0, UserCorrections: 1));

        Assert.Equal(-1, result.TrustDelta);
        Assert.Equal(3, result.StrainDelta);
        Assert.Equal(49, result.TrustAfter);
        Assert.Equal(23, result.StrainAfter);
    }

    [Fact]
    public void AbandonedIsHardNeutralEvenWhenOtherSignalsArePresent()
    {
        var result = TrustStrainRules.Calculate(
            50,
            20,
            new TrustStrainInput(QuestOutcome.Abandoned, ObservedTestsPassed: true, ScopeViolations: 20, UserCorrections: 20));

        Assert.Equal(0, result.TrustDelta);
        Assert.Equal(0, result.StrainDelta);
        Assert.Equal(50, result.TrustAfter);
        Assert.Equal(20, result.StrainAfter);
        Assert.Empty(result.Components);
    }

    [Fact]
    public void NegativeSignalsCapAtThreeEachAndFinalValuesClampOnce()
    {
        var result = TrustStrainRules.Calculate(
            2,
            95,
            new TrustStrainInput(QuestOutcome.Failed, ObservedTestsPassed: false, ScopeViolations: 20, UserCorrections: 20));

        Assert.Equal(-2, result.TrustDelta);
        Assert.Equal(5, result.StrainDelta);
        Assert.Equal(0, result.TrustAfter);
        Assert.Equal(100, result.StrainAfter);
        Assert.Contains(result.Components, static component => component.Key == "signal.scope_violation" && component.TrustDelta == -3 && component.StrainDelta == 3);
        Assert.Contains(result.Components, static component => component.Key == "signal.user_correction" && component.TrustDelta == -3 && component.StrainDelta == 3);
    }

    [Fact]
    public void InvalidStateOrSignalBoundsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TrustStrainRules.Calculate(-1, 20, new TrustStrainInput(QuestOutcome.Success, false, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrustStrainRules.Calculate(50, 101, new TrustStrainInput(QuestOutcome.Success, false, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrustStrainRules.Calculate(50, 20, new TrustStrainInput(QuestOutcome.Success, false, 21, 0)));
    }
}
