namespace HeroPassport.Domain.Engine;

public sealed class QuestQualityFlags
{
    private QuestQualityFlags(
        bool hasObservedTestsPassed,
        bool hasCleanScope,
        bool hasClearSummary,
        bool hasNoUserCorrections)
    {
        HasObservedTestsPassed = hasObservedTestsPassed;
        HasCleanScope = hasCleanScope;
        HasClearSummary = hasClearSummary;
        HasNoUserCorrections = hasNoUserCorrections;
    }

    public bool HasObservedTestsPassed { get; }

    public bool HasCleanScope { get; }

    public bool HasClearSummary { get; }

    public bool HasNoUserCorrections { get; }

    public static QuestQualityFlags From(
        int summaryScalarLength,
        string testsStatus,
        string testsEvidence,
        int scopeViolations,
        int userCorrections)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(summaryScalarLength);
        RequireBoundedCount(scopeViolations, nameof(scopeViolations));
        RequireBoundedCount(userCorrections, nameof(userCorrections));
        RequireTestsAttestation(testsStatus, testsEvidence);

        return new QuestQualityFlags(
            string.Equals(testsStatus, "passed", StringComparison.Ordinal) &&
            string.Equals(testsEvidence, "observed", StringComparison.Ordinal),
            scopeViolations == 0,
            summaryScalarLength >= 40,
            userCorrections == 0);
    }

    private static void RequireBoundedCount(int value, string parameterName)
    {
        if (value is < 0 or > 20)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void RequireTestsAttestation(string testsStatus, string testsEvidence)
    {
        var statusIsKnown = testsStatus is "not_run" or "passed" or "failed" or "unknown";
        var evidenceIsKnown = testsEvidence is "observed" or "reported" or "none";
        var pairIsConsistent = testsStatus switch
        {
            "not_run" => string.Equals(testsEvidence, "none", StringComparison.Ordinal),
            "passed" or "failed" => !string.Equals(testsEvidence, "none", StringComparison.Ordinal),
            "unknown" => true,
            _ => false,
        };

        if (!statusIsKnown || !evidenceIsKnown || !pairIsConsistent)
        {
            throw new ArgumentException("Tests attestation is not canonical.");
        }
    }
}
