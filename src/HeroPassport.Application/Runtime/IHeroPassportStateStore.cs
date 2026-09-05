using HeroPassport.Domain.Primitives;

namespace HeroPassport.Application.Runtime;

public interface IHeroPassportStateStore
{
    Task<BootstrapResult> BootstrapAsync(
        BootstrapStoreCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<ConfigureResult> ConfigureAsync(
        ConfigureRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<RuntimeContextResult> GetRuntimeContextAsync(
        ProjectBindingContext project,
        CancellationToken cancellationToken = default);

    Task<CreateHeroResult> CreateHeroAsync(
        CreateHeroStoreCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task ActivateHeroAsync(
        HeroId heroId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<HeroListResult> ListHeroesAsync(CancellationToken cancellationToken = default);

    Task<HeroPreferenceChangeResult> ActivateHeroPreferenceAsync(
        HeroId heroId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<HeroPreferenceChangeResult> ArchiveHeroAsync(
        HeroId heroId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<HeroPreferenceChangeResult> RestoreHeroAsync(
        HeroId heroId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<HeroCardResult> GetCardAsync(
        HeroId heroId,
        ProjectBindingContext project,
        CancellationToken cancellationToken = default);

    Task<StartQuestResult> StartQuestAsync(
        StartQuestStoreCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<FinishQuestResult> FinishQuestAsync(
        FinishQuestStoreCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
