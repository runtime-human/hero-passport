using HeroPassport.Domain.Primitives;

namespace HeroPassport.Application.Runtime;

public sealed class HeroPassportApplication(IHeroPassportStateStore store, TimeProvider timeProvider)
{
    private static readonly string[] Locales = ["ru-RU", "en-US"];
    private static readonly string[] PresentationStyles = ["rpg_engineering", "classic_rpg", "minimal"];
    private static readonly string[] QuestTypes = ["planning", "research", "coding", "review", "debugging", "documentation", "maintenance"];
    private static readonly string[] QuestResults = ["success", "partial", "blocked", "failed", "abandoned"];
    private static readonly string[] MetricStatuses = ["not_run", "passed", "failed", "unknown"];
    private static readonly string[] MetricEvidence = ["observed", "reported", "none"];
    private static readonly string[] SkillKeys = ["coding", "testing_awareness", "scope_control", "documentation", "tool_use", "planning", "research", "debugging", "review", "maintenance"];

    public Task<BootstrapResult> BootstrapAsync(BootstrapRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var locale = RequireClosedValue(request.Locale, Locales, "locale");
        var heroName = NormalizeRequestText(request.HeroName, 1, 64, "heroName");
        var presentationStyle = RequireClosedValue(request.PresentationStyle, PresentationStyles, "presentationStyle");
        var hash = CanonicalMutationEncoder.HashBootstrap(locale, heroName, presentationStyle, request.AutoStartQuest, request.AutoFinishQuest);
        return store.BootstrapAsync(
            new BootstrapStoreCommand(request.BootstrapRequestId, HeroPassportVersions.MutationArgsVersion, hash, locale, heroName, presentationStyle, request.AutoStartQuest, request.AutoFinishQuest),
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

    public Task<RuntimeContextResult> GetRuntimeContextAsync(ProjectBindingContext project, CancellationToken cancellationToken = default) =>
        store.GetRuntimeContextAsync(ValidateProject(project), cancellationToken);

    public Task<CreateHeroResult> CreateHeroAsync(CreateHeroRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = NormalizeRequestText(request.Name, 1, 64, "name");
        var hash = CanonicalMutationEncoder.HashCreateHero(name);
        return store.CreateHeroAsync(
            new CreateHeroStoreCommand(request.CreateRequestId, HeroPassportVersions.MutationArgsVersion, hash, name),
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
        var validatedProject = ValidateProject(project);
        var questType = RequireQuestType(request.QuestType);
        var title = NormalizeRequestText(request.Title, 1, 120, "title");
        var goal = NormalizeRequestText(request.Goal, 1, 500, "goal");

        return store.StartQuestAsync(
            new StartQuestStoreCommand(
                request.StartRequestId,
                HeroPassportVersions.MutationArgsVersion,
                request.HeroId,
                questType,
                title,
                goal,
                validatedProject),
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public Task<FinishQuestResult> FinishQuestAsync(
        FinishQuestRequest request,
        ProjectBindingContext project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validatedProject = ValidateProject(project);
        var result = RequireFinishResult(request.Result);
        var summary = NormalizeRequestText(request.Summary, 1, 2000, "summary");
        var metrics = ValidateFinishMetrics(request.Metrics);
        var skillsUsed = ValidateSkills(request.SkillsUsed);
        var argsHash = CanonicalMutationEncoder.HashFinishQuest(
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
            skillsUsed);

        return store.FinishQuestAsync(
            new FinishQuestStoreCommand(
                request.FinishRequestId,
                HeroPassportVersions.MutationArgsVersion,
                argsHash,
                request.QuestId,
                result,
                summary,
                metrics,
                skillsUsed,
                validatedProject),
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    private static ProjectBindingContext ValidateProject(ProjectBindingContext project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!IsLowerHex64(project.WorkspaceFingerprint) ||
            !string.Equals(project.IdentityVersion, "project-identity/1", StringComparison.Ordinal))
        {
            throw new HeroPassportException("HP310", "Project binding is invalid.");
        }

        string displayName;
        try
        {
            displayName = SafeTextV1.Normalize(project.DisplayName, 1, 120);
        }
        catch (ArgumentException)
        {
            throw new HeroPassportException("HP310", "Project binding is invalid.");
        }

        return project with { DisplayName = displayName };
    }

    private static string RequireClosedValue(string? value, IReadOnlyList<string> allowed, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new HeroPassportException("HP300", $"Invalid {fieldName}.");
        }

        foreach (var candidate in allowed)
        {
            if (string.Equals(candidate, value, StringComparison.Ordinal))
            {
                return value;
            }
        }

        throw new HeroPassportException("HP300", $"Invalid {fieldName}.");
    }

    private static string RequireQuestType(string? value)
    {
        if (IsAllowed(value, QuestTypes))
        {
            return value!;
        }

        throw new HeroPassportException("HP110", "Quest type is invalid.");
    }

    private static string RequireFinishResult(string? value)
    {
        if (IsAllowed(value, QuestResults))
        {
            return value!;
        }

        throw new HeroPassportException("HP111", "Quest result is invalid.");
    }

    private static FinishQuestMetrics ValidateFinishMetrics(FinishQuestMetrics? metrics)
    {
        if (metrics is null ||
            metrics.ScopeViolations is < 0 or > 20 ||
            metrics.UserCorrections is < 0 or > 20 ||
            !IsAllowed(metrics.BuildStatus, MetricStatuses) ||
            !IsAllowed(metrics.BuildEvidence, MetricEvidence) ||
            !IsAllowed(metrics.TestsStatus, MetricStatuses) ||
            !IsAllowed(metrics.TestsEvidence, MetricEvidence))
        {
            throw new HeroPassportException("HP120", "Quest metrics are invalid.");
        }

        return metrics;
    }

    private static string[] ValidateSkills(IReadOnlyList<string>? skillsUsed)
    {
        if (skillsUsed is null || skillsUsed.Count is < 1 or > 3)
        {
            throw new HeroPassportException("HP112", "Quest skills are invalid.");
        }

        var validated = new string[skillsUsed.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < skillsUsed.Count; index++)
        {
            var skill = skillsUsed[index];
            if (!IsAllowed(skill, SkillKeys) || !seen.Add(skill))
            {
                throw new HeroPassportException("HP112", "Quest skills are invalid.");
            }

            validated[index] = skill;
        }

        return validated;
    }

    private static bool IsAllowed(string? value, IReadOnlyList<string> allowed)
    {
        if (value is null)
        {
            return false;
        }

        foreach (var candidate in allowed)
        {
            if (string.Equals(candidate, value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeRequestText(string? value, int minimumScalars, int maximumScalars, string fieldName)
    {
        try
        {
            return SafeTextV1.Normalize(value!, minimumScalars, maximumScalars);
        }
        catch (ArgumentException)
        {
            throw new HeroPassportException("HP100", $"Invalid {fieldName}.");
        }
    }

    private static bool IsLowerHex64(string? value)
    {
        if (value is null || value.Length != 64)
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
