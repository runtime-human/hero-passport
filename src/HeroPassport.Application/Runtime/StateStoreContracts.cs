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

public sealed record StartQuestStoreCommand(
    MutationRequestId RequestId,
    HeroId HeroId,
    string QuestType,
    string Title,
    string Goal);

public sealed record FinishQuestStoreCommand(
    MutationRequestId RequestId,
    string ArgsEncodingVersion,
    byte[] ArgsHash,
    QuestId QuestId,
    string Result,
    string Summary,
    FinishMetrics Metrics,
    IReadOnlyList<string> SkillsUsed);

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

    Task<ListHeroesResult> ListHeroesAsync(CancellationToken cancellationToken = default);

    Task ActivateHeroAsync(
        HeroId heroId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<HeroLifecycleResult> ArchiveHeroAsync(
        HeroId heroId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<HeroLifecycleResult> RestoreHeroAsync(
        HeroId heroId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<StartQuestResult> StartQuestAsync(
        StartQuestStoreCommand command,
        ProjectBindingContext project,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<FinishQuestResult> FinishQuestAsync(
        FinishQuestStoreCommand command,
        ProjectBindingContext project,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<HeroCardResult> GetHeroCardAsync(
        HeroId heroId,
        ProjectBindingContext project,
        CancellationToken cancellationToken = default);
}
