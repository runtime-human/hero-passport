using HeroPassport.Domain.Primitives;

namespace HeroPassport.Application.Runtime;

public sealed class HeroPassportApplication(IHeroPassportStateStore store, TimeProvider timeProvider)
{
    private static readonly string[] Locales = ["ru-RU", "en-US"];
    private static readonly string[] PresentationStyles = ["rpg_engineering", "classic_rpg", "minimal"];
    private static readonly string[] QuestTypes = ["planning", "research", "coding", "review", "debugging", "documentation", "maintenance"];
    private static readonly string[] QuestResults = ["success", "partial", "blocked", "failed", "abandoned"];
    private static readonly string[] StatusValues = ["not_run", "passed", "failed", "unknown"];
    private static readonly string[] EvidenceValues = ["observed", "reported", "none"];
    private static readonly string[] SkillKeys =
    [
        "coding",
        "testing_awareness",
        "scope_control",
        "documentation",
        "tool_use",
        "planning",
        "research",
        "debugging",
        "review",
        "maintenance",
    ];

    public Task<BootstrapResult> BootstrapAsync(BootstrapRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var locale = RequireClosedValue(request.Locale, Locales, "locale");
        var heroName = SafeTextV1.Normalize(request.HeroName, 1, 64);
        var presentationStyle = RequireClosedValue(request.PresentationStyle, PresentationStyles, "presentationStyle");
        var hash = CanonicalMutationEncoder.HashBootstrap(
            locale,
            heroName,
            presentationStyle,
            request.AutoStartQuest,
            request.AutoFinishQuest);

        return store.BootstrapAsync(
            new BootstrapStoreCommand(
                request.BootstrapRequestId,
                HeroPassportVersions.MutationArgsVersion,
                hash,
                locale,
                heroName,
                presentationStyle,
                request.AutoStartQuest,
                request.AutoFinishQuest),
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public Task<ConfigureResult> ConfigureAsync(ConfigureRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validated = new ConfigureRequest(
            RequireClosedValue(request.Locale, Locales, "locale"),
            RequireClosedValue(request.PresentationStyle, PresentationStyles, "presentationStyle"),
            request.AutoStartQuest,
            request.AutoFinishQuest);

        return store.ConfigureAsync(validated, timeProvider.GetUtcNow(), cancellationToken);
    }

    public Task<RuntimeContextResult> GetRuntimeContextAsync(
        ProjectBindingContext project,
        CancellationToken cancellationToken = default) =>
        store.GetRuntimeContextAsync(ValidateProject(project), cancellationToken);

    public Task<CreateHeroResult> CreateHeroAsync(CreateHeroRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = SafeTextV1.Normalize(request.Name, 1, 64);
        var hash = CanonicalMutationEncoder.HashCreateHero(name);
        return store.CreateHeroAsync(
            new CreateHeroStoreCommand(
                request.CreateRequestId,
                HeroPassportVersions.MutationArgsVersion,
                hash,
                name),
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public Task ActivateHeroAsync(HeroId heroId, CancellationToken cancellationToken = default) =>
        store.ActivateHeroAsync(heroId, timeProvider.GetUtcNow(), cancellationToken);

    public Task<StartQuestResult> StartQuestAsync(
        StartQuestRequest request,
        ProjectBindingContext project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var questType = RequireQuestType(request.QuestType);
        var title = SafeTextV1.Normalize(request.Title, 1, 120);
        var goal = SafeTextV1.Normalize(request.Goal, 1, 500);
        return store.StartQuestAsync(
            new StartQuestStoreCommand(request.StartRequestId, request.HeroId, questType, title, goal),
            ValidateProject(project),
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public Task<FinishQuestResult> FinishQuestAsync(
        FinishQuestRequest request,
        ProjectBindingContext project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Metrics);
        ArgumentNullException.ThrowIfNull(request.SkillsUsed);

        var result = RequireQuestResult(request.Result);
        var summary = SafeTextV1.Normalize(request.Summary, 1, 2000);
        var metrics = ValidateMetrics(request.Metrics);
        var skills = ValidateSkills(request.SkillsUsed);
        var hash = CanonicalMutationEncoder.HashFinishQuest(
            request.QuestId,
            result,
            summary,
            metrics.TestsMentioned,
            metrics.ScopeViolations,
            metrics.UserCorrections,
            metrics.BuildStatus,
            metrics.BuildEvidence,
            metrics.TestsStatus,
            metrics.TestsEvidence,
            skills);

        return store.FinishQuestAsync(
            new FinishQuestStoreCommand(
                request.FinishRequestId,
                HeroPassportVersions.MutationArgsVersion,
                hash,
                request.QuestId,
                result,
                summary,
                metrics,
                skills),
            ValidateProject(project),
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    private static ProjectBindingContext ValidateProject(ProjectBindingContext project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var displayName = SafeTextV1.Normalize(project.DisplayName, 1, 120);
        if (!IsLowerHex64(project.WorkspaceFingerprint) ||
            !string.Equals(project.IdentityVersion, "project-identity/1", StringComparison.Ordinal))
        {
            throw new HeroPassportException("HP310", "Project binding is invalid.");
        }

        return project with { DisplayName = displayName };
    }

    private static string RequireQuestType(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        foreach (var candidate in QuestTypes)
        {
            if (string.Equals(candidate, value, StringComparison.Ordinal))
            {
                return value;
            }
        }

        throw new HeroPassportException("HP110", "Quest type is invalid.");
    }

    private static string RequireQuestResult(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        foreach (var candidate in QuestResults)
        {
            if (string.Equals(candidate, value, StringComparison.Ordinal))
            {
                return value;
            }
        }

        throw new HeroPassportException("HP111", "Quest result is invalid.");
    }

    private static FinishMetrics ValidateMetrics(FinishMetrics metrics)
    {
        if (metrics.ScopeViolations is < 0 or > 20 || metrics.UserCorrections is < 0 or > 20)
        {
            throw new HeroPassportException("HP120", "Quest metrics are invalid.");
        }

        var buildStatus = RequireMetricValue(metrics.BuildStatus, StatusValues);
        var buildEvidence = RequireMetricValue(metrics.BuildEvidence, EvidenceValues);
        var testsStatus = RequireMetricValue(metrics.TestsStatus, StatusValues);
        var testsEvidence = RequireMetricValue(metrics.TestsEvidence, EvidenceValues);

        ValidateStatusEvidence(buildStatus, buildEvidence);
        ValidateStatusEvidence(testsStatus, testsEvidence);
        if (!string.Equals(testsStatus, "not_run", StringComparison.Ordinal) && !metrics.TestsMentioned)
        {
            throw new HeroPassportException("HP120", "testsMentioned must be true when testsStatus is not not_run.");
        }

        return metrics with
        {
            BuildStatus = buildStatus,
            BuildEvidence = buildEvidence,
            TestsStatus = testsStatus,
            TestsEvidence = testsEvidence,
        };
    }

    private static void ValidateStatusEvidence(string status, string evidence)
    {
        var valid = status switch
        {
            "not_run" => string.Equals(evidence, "none", StringComparison.Ordinal),
            "passed" or "failed" => evidence is "observed" or "reported",
            "unknown" => evidence is "observed" or "reported" or "none",
            _ => false,
        };

        if (!valid)
        {
            throw new HeroPassportException("HP120", "Status/evidence metrics are inconsistent.");
        }
    }

    private static IReadOnlyList<string> ValidateSkills(IReadOnlyList<string> skills)
    {
        if (skills.Count is < 1 or > 3)
        {
            throw new HeroPassportException("HP112", "skillsUsed must contain between one and three skills.");
        }

        var validated = new string[skills.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < skills.Count; index++)
        {
            var skill = skills[index];
            if (!ContainsOrdinal(SkillKeys, skill) || !seen.Add(skill))
            {
                throw new HeroPassportException("HP112", "skillsUsed contains an invalid or duplicate skill.");
            }

            validated[index] = skill;
        }

        return validated;
    }

    private static string RequireMetricValue(string value, IReadOnlyList<string> allowed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        foreach (var candidate in allowed)
        {
            if (string.Equals(candidate, value, StringComparison.Ordinal))
            {
                return value;
            }
        }

        throw new HeroPassportException("HP120", "Quest metrics are invalid.");
    }

    private static bool ContainsOrdinal(IReadOnlyList<string> values, string value)
    {
        foreach (var candidate in values)
        {
            if (string.Equals(candidate, value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string RequireClosedValue(string value, IReadOnlyList<string> allowed, string fieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        foreach (var candidate in allowed)
        {
            if (string.Equals(candidate, value, StringComparison.Ordinal))
            {
                return value;
            }
        }

        throw new HeroPassportException("HP300", $"Invalid {fieldName}.");
    }

    private static bool IsLowerHex64(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!(character is >= '0' and <= '9' or >= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }
}
