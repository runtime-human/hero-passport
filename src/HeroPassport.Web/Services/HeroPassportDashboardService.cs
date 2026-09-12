using HeroPassport.Application.Runtime;

namespace HeroPassport.Web.Services;

public sealed class HeroPassportDashboardService
{
    private const int MaxOpenQuestItems = 5;

    private readonly HeroPassportApplication _application;
    private readonly ProjectBindingContext _project;

    public HeroPassportDashboardService(
        HeroPassportApplication application,
        ProjectBindingContext project)
    {
        _application = application;
        _project = project;
    }

    public async Task<HeroPassportDashboardViewModel> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var context = await _application
            .GetRuntimeContextAsync(_project, cancellationToken)
            .ConfigureAwait(false);

        HeroCardResult? card = null;
        if (context.SetupCompleted && context.ActiveHero is not null)
        {
            card = await _application
                .GetCardAsync(context.ActiveHero.HeroId, _project, cancellationToken)
                .ConfigureAwait(false);
        }

        var openQuests = context.OpenQuests
            .Take(MaxOpenQuestItems)
            .Select(static quest => new HeroPassportDashboardOpenQuestViewModel(
                quest.Title,
                quest.QuestType))
            .ToArray();

        HeroPassportDashboardHeroViewModel? hero = null;
        HeroPassportDashboardProjectViewModel? project = null;
        if (card is not null)
        {
            hero = new HeroPassportDashboardHeroViewModel(
                card.Hero.Name,
                card.Hero.TotalXp,
                card.Hero.Level,
                card.Hero.RankKey,
                card.Hero.Trust,
                card.Hero.Strain,
                card.Hero.SuccessStreak,
                card.Hero.TopSkills
                    .Select(static skill => new HeroPassportDashboardSkillViewModel(
                        skill.SkillKey,
                        skill.Xp,
                        skill.Level))
                    .ToArray());

            project = new HeroPassportDashboardProjectViewModel(
                card.Project.DisplayName,
                card.Project.QuestsFinished,
                card.Project.TotalXpEarned);
        }

        return new HeroPassportDashboardViewModel(
            context.SetupCompleted,
            context.Project.DisplayName,
            hero,
            project,
            openQuests);
    }
}

public sealed record HeroPassportDashboardViewModel(
    bool SetupCompleted,
    string ProjectDisplayName,
    HeroPassportDashboardHeroViewModel? Hero,
    HeroPassportDashboardProjectViewModel? Project,
    IReadOnlyList<HeroPassportDashboardOpenQuestViewModel> OpenQuests);

public sealed record HeroPassportDashboardHeroViewModel(
    string Name,
    long TotalXp,
    int Level,
    string RankKey,
    int Trust,
    int Strain,
    long SuccessStreak,
    IReadOnlyList<HeroPassportDashboardSkillViewModel> TopSkills);

public sealed record HeroPassportDashboardProjectViewModel(
    string DisplayName,
    long QuestsFinished,
    long TotalXpEarned);

public sealed record HeroPassportDashboardOpenQuestViewModel(
    string Title,
    string QuestType);

public sealed record HeroPassportDashboardSkillViewModel(
    string SkillKey,
    long Xp,
    int Level);
