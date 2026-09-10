namespace HeroPassport.Domain.Engine;

public sealed record TrustStrainComponent(string Key, int TrustDelta, int StrainDelta);

public sealed record TrustStrainResult(
    int TrustBefore,
    int TrustAfter,
    int StrainBefore,
    int StrainAfter,
    IReadOnlyList<TrustStrainComponent> Components,
    string RuleVersion);

public static class TrustStrainRules
{
    public const string RuleVersion = "trust-strain/1.0.0";

    public static TrustStrainResult Apply(
        int trustBefore,
        int strainBefore,
        string result,
        QuestQualityFlags quality,
        int scopeViolations,
        int userCorrections,
        string ruleVersion)
    {
        RequireVersion(ruleVersion);
        RequireStat(trustBefore, nameof(trustBefore));
        RequireStat(strainBefore, nameof(strainBefore));
        RequireResult(result);
        ArgumentNullException.ThrowIfNull(quality);
        RequireBoundedCount(scopeViolations, nameof(scopeViolations));
        RequireBoundedCount(userCorrections, nameof(userCorrections));

        if (quality.HasCleanScope != (scopeViolations == 0) ||
            quality.HasNoUserCorrections != (userCorrections == 0))
        {
            throw new ArgumentException("Quest quality flags do not match the bounded counts.", nameof(quality));
        }

        if (string.Equals(result, "abandoned", StringComparison.Ordinal))
        {
            return new TrustStrainResult(
                trustBefore,
                trustBefore,
                strainBefore,
                strainBefore,
                Array.Empty<TrustStrainComponent>(),
                RuleVersion);
        }

        var components = new List<TrustStrainComponent>(6);
        AddOutcomeComponent(components, result);

        if (string.Equals(result, "success", StringComparison.Ordinal) &&
            quality.HasCleanScope &&
            quality.HasNoUserCorrections)
        {
            components.Add(new TrustStrainComponent("clean_success_bonus", 1, -1));
        }

        if (quality.HasObservedTestsPassed)
        {
            components.Add(new TrustStrainComponent("observed_tests_passed_bonus", 1, 0));
        }

        var positiveTrust = components.Sum(static component => Math.Max(0, component.TrustDelta));
        if (positiveTrust > 2)
        {
            components.Add(new TrustStrainComponent("positive_trust_cap_adjustment", 2 - positiveTrust, 0));
        }

        var scopePenalty = Math.Min(scopeViolations, 3);
        if (scopePenalty > 0)
        {
            components.Add(new TrustStrainComponent("scope_violation_penalty", -scopePenalty, scopePenalty));
        }

        var correctionPenalty = Math.Min(userCorrections, 3);
        if (correctionPenalty > 0)
        {
            components.Add(new TrustStrainComponent("user_correction_penalty", -correctionPenalty, correctionPenalty));
        }

        var trustDelta = components.Sum(static component => component.TrustDelta);
        var strainDelta = components.Sum(static component => component.StrainDelta);
        var trustAfter = Math.Clamp(checked(trustBefore + trustDelta), 0, 100);
        var strainAfter = Math.Clamp(checked(strainBefore + strainDelta), 0, 100);

        return new TrustStrainResult(
            trustBefore,
            trustAfter,
            strainBefore,
            strainAfter,
            components,
            RuleVersion);
    }

    private static void AddOutcomeComponent(List<TrustStrainComponent> components, string result)
    {
        switch (result)
        {
            case "success":
                components.Add(new TrustStrainComponent("success_outcome", 1, -1));
                break;
            case "partial":
                components.Add(new TrustStrainComponent("partial_outcome", 0, 1));
                break;
            case "failed":
                components.Add(new TrustStrainComponent("failed_outcome", 0, 2));
                break;
        }
    }

    private static void RequireVersion(string ruleVersion)
    {
        if (!string.Equals(ruleVersion, RuleVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported Trust/Strain rule version.", nameof(ruleVersion));
        }
    }

    private static void RequireStat(int value, string parameterName)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void RequireBoundedCount(int value, string parameterName)
    {
        if (value is < 0 or > 20)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void RequireResult(string result)
    {
        if (result is not ("success" or "partial" or "blocked" or "failed" or "abandoned"))
        {
            throw new ArgumentOutOfRangeException(nameof(result));
        }
    }
}
