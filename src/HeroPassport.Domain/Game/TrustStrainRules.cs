namespace HeroPassport.Domain.Game;

public sealed record TrustStrainInput(
    QuestOutcome Outcome,
    bool ObservedTestsPassed,
    int ScopeViolations,
    int UserCorrections);

public sealed record TrustStrainComponent(string Key, int TrustDelta, int StrainDelta);

public sealed record TrustStrainResult(
    int TrustBefore,
    int TrustAfter,
    int TrustDelta,
    int StrainBefore,
    int StrainAfter,
    int StrainDelta,
    string RuleVersion,
    IReadOnlyList<TrustStrainComponent> Components);

public static class TrustStrainRules
{
    public const string RuleVersion = "trust-strain/1.0.0";
    public const int InitialTrust = 50;
    public const int InitialStrain = 20;

    public static TrustStrainResult Calculate(int trustBefore, int strainBefore, TrustStrainInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateStat(trustBefore, nameof(trustBefore));
        ValidateStat(strainBefore, nameof(strainBefore));
        if (input.ScopeViolations is < 0 or > 20 || input.UserCorrections is < 0 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Trust/Strain attestations are outside their bounded range.");
        }

        if (input.Outcome == QuestOutcome.Abandoned)
        {
            return new TrustStrainResult(
                trustBefore,
                trustBefore,
                0,
                strainBefore,
                strainBefore,
                0,
                RuleVersion,
                []);
        }

        var components = new List<TrustStrainComponent>();
        AddOutcomeComponent(input.Outcome, components);

        if (input.Outcome == QuestOutcome.Success && input.ScopeViolations == 0 && input.UserCorrections == 0)
        {
            components.Add(new TrustStrainComponent("quality.clean_success", 1, -1));
        }

        if (input.ObservedTestsPassed)
        {
            components.Add(new TrustStrainComponent("quality.observed_tests_passed", 1, 0));
        }

        ApplyPositiveCaps(components);

        var scopeCount = Math.Min(input.ScopeViolations, 3);
        if (scopeCount > 0)
        {
            components.Add(new TrustStrainComponent("signal.scope_violation", -scopeCount, scopeCount));
        }

        var correctionCount = Math.Min(input.UserCorrections, 3);
        if (correctionCount > 0)
        {
            components.Add(new TrustStrainComponent("signal.user_correction", -correctionCount, correctionCount));
        }

        var rawTrustDelta = components.Sum(static component => component.TrustDelta);
        var rawStrainDelta = components.Sum(static component => component.StrainDelta);
        var trustAfter = Math.Clamp(checked(trustBefore + rawTrustDelta), 0, 100);
        var strainAfter = Math.Clamp(checked(strainBefore + rawStrainDelta), 0, 100);
        var effectiveTrustDelta = trustAfter - trustBefore;
        var effectiveStrainDelta = strainAfter - strainBefore;

        var trustClamp = effectiveTrustDelta - rawTrustDelta;
        var strainClamp = effectiveStrainDelta - rawStrainDelta;
        if (trustClamp != 0 || strainClamp != 0)
        {
            components.Add(new TrustStrainComponent("stat.clamp", trustClamp, strainClamp));
        }

        return new TrustStrainResult(
            trustBefore,
            trustAfter,
            effectiveTrustDelta,
            strainBefore,
            strainAfter,
            effectiveStrainDelta,
            RuleVersion,
            components);
    }

    private static void AddOutcomeComponent(QuestOutcome outcome, ICollection<TrustStrainComponent> components)
    {
        switch (outcome)
        {
            case QuestOutcome.Success:
                components.Add(new TrustStrainComponent("outcome.success", 1, -1));
                break;
            case QuestOutcome.Partial:
                components.Add(new TrustStrainComponent("outcome.partial", 0, 1));
                break;
            case QuestOutcome.Blocked:
                break;
            case QuestOutcome.Failed:
                components.Add(new TrustStrainComponent("outcome.failed", 0, 2));
                break;
            case QuestOutcome.Abandoned:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome));
        }
    }

    private static void ApplyPositiveCaps(ICollection<TrustStrainComponent> components)
    {
        var positiveTrust = components.Sum(static component => Math.Max(component.TrustDelta, 0));
        if (positiveTrust > 2)
        {
            components.Add(new TrustStrainComponent("trust.positive_cap", 2 - positiveTrust, 0));
        }

        var strainRecovery = components.Sum(static component => Math.Min(component.StrainDelta, 0));
        if (strainRecovery < -2)
        {
            components.Add(new TrustStrainComponent("strain.recovery_cap", 0, -2 - strainRecovery));
        }
    }

    private static void ValidateStat(int value, string parameterName)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
