using HeroPassport.Domain.Primitives;

namespace HeroPassport.Application.Runtime;

public sealed partial class HeroPassportApplication
{
    public Task<ListHeroesResult> ListHeroesAsync(CancellationToken cancellationToken = default) =>
        store.ListHeroesAsync(cancellationToken);

    public Task<HeroLifecycleResult> ArchiveHeroAsync(
        HeroId heroId,
        CancellationToken cancellationToken = default) =>
        store.ArchiveHeroAsync(heroId, timeProvider.GetUtcNow(), cancellationToken);

    public Task<HeroLifecycleResult> RestoreHeroAsync(
        HeroId heroId,
        CancellationToken cancellationToken = default) =>
        store.RestoreHeroAsync(heroId, timeProvider.GetUtcNow(), cancellationToken);

    public Task<HeroCardResult> GetHeroCardAsync(
        HeroId heroId,
        ProjectBindingContext project,
        CancellationToken cancellationToken = default) =>
        store.GetHeroCardAsync(heroId, ValidateProject(project), cancellationToken);
}
