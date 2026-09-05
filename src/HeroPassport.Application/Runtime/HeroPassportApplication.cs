using HeroPassport.Domain.Primitives;

namespace HeroPassport.Application.Runtime;

public sealed class HeroPassportApplication(IHeroPassportStateStore store, TimeProvider timeProvider)
{
    private static readonly string[] Locales = ["ru-RU", "en-US"];
    private static readonly string[] PresentationStyles = ["rpg_engineering", "classic_rpg", "minimal"];

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
        catch (ArgumentException exception)
        {
            throw new HeroPassportException("HP310", "Project binding is invalid.", exception);
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

    private static string NormalizeRequestText(string? value, int minimumScalars, int maximumScalars, string fieldName)
    {
        try
        {
            return SafeTextV1.Normalize(value!, minimumScalars, maximumScalars);
        }
        catch (ArgumentException exception)
        {
            throw new HeroPassportException("HP100", $"Invalid {fieldName}.", exception);
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
