using HeroPassport.Application.Runtime;

namespace HeroPassport.Web.Services;

public sealed class HeroPassportDashboardService
{
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

        return new HeroPassportDashboardViewModel(context, card);
    }
}

public sealed record HeroPassportDashboardViewModel(
    RuntimeContextResult Context,
    HeroCardResult? Card);
