using HeroPassport.Domain.Primitives;

namespace HeroPassport.Application.Runtime;

public sealed record BootstrapStoreCommand(
    MutationRequestId RequestId,
    string ArgsEncodingVersion,
    byte[] ArgsHash,
    string Locale,
    string HeroName,
    string PresentationStyle,
    bool AutoStartQuest,
    bool AutoFinishQuest);

public sealed record CreateHeroStoreCommand(
    MutationRequestId RequestId,
    string ArgsEncodingVersion,
    byte[] ArgsHash,
    string Name);

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
}
