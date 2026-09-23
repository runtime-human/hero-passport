using HeroPassport.Application.Runtime;

namespace HeroPassport.Web.Services;

public sealed class HeroPassportSkillProgressionService
{
    private readonly HeroPassportApplication _application;
    private readonly ProjectBindingContext _project;

    public HeroPassportSkillProgressionService(
        HeroPassportApplication application,
        ProjectBindingContext project)
    {
        _application = application;
        _project = project;
    }

    public async Task<LoadSkillProgressionWebResult> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var context = await _application
            .GetRuntimeContextAsync(_project, cancellationToken)
            .ConfigureAwait(false);
        if (!context.SetupCompleted)
        {
            return new LoadSkillProgressionWebResult(SkillProgressionPageStatus.SetupRequired);
        }

        if (context.ActiveHero is null)
        {
            return new LoadSkillProgressionWebResult(SkillProgressionPageStatus.Invalid);
        }

        HeroSkillProgressionReadResult progression;
        try
        {
            progression = await _application
                .GetSkillProgressionAsync(
                    context.ActiveHero.HeroId,
                    _project,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HeroPassportException exception)
            when (string.Equals(exception.Code, "HP140", StringComparison.Ordinal))
        {
            return new LoadSkillProgressionWebResult(SkillProgressionPageStatus.Invalid);
        }

        var skills = progression.Skills
            .Select(static skill => new HeroPassportSkillProgressionItemViewModel(
                skill.SkillKey,
                skill.Hero.Xp,
                skill.Hero.Level,
                skill.Hero.IsLevelCapped,
                skill.Hero.LevelXp,
                skill.Hero.NextLevelXpRequired,
                skill.ProjectContribution.Xp))
            .ToArray();

        return new LoadSkillProgressionWebResult(
            SkillProgressionPageStatus.Ready,
            new HeroPassportSkillProgressionViewModel(
                progression.HeroName,
                progression.ProjectDisplayName,
                skills));
    }
}

public enum SkillProgressionPageStatus
{
    Ready,
    SetupRequired,
    Invalid,
}

public sealed record LoadSkillProgressionWebResult(
    SkillProgressionPageStatus Status,
    HeroPassportSkillProgressionViewModel? Page = null);

public sealed record HeroPassportSkillProgressionViewModel(
    string HeroName,
    string ProjectDisplayName,
    IReadOnlyList<HeroPassportSkillProgressionItemViewModel> Skills);

public sealed record HeroPassportSkillProgressionItemViewModel(
    string SkillKey,
    long HeroXp,
    int HeroLevel,
    bool HeroIsLevelCapped,
    long HeroLevelXp,
    long? HeroNextLevelXpRequired,
    long ProjectContributionXp);
