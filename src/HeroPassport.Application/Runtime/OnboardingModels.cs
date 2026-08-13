using HeroPassport.Domain.Primitives;

namespace HeroPassport.Application.Runtime;

public sealed record SettingsSnapshot(
    string Locale,
    string PresentationStyle,
    bool AutoStartQuest,
    bool AutoFinishQuest);

public sealed record HeroIdentitySnapshot(HeroId HeroId, string Name);

public sealed record BootstrapRequest(
    MutationRequestId BootstrapRequestId,
    string Locale,
    string HeroName,
    string PresentationStyle,
    bool AutoStartQuest,
    bool AutoFinishQuest);

public sealed record BootstrapResult(
    HeroIdentitySnapshot Hero,
    SettingsSnapshot Settings,
    bool Replayed);

public sealed record ConfigureRequest(
    string Locale,
    string PresentationStyle,
    bool AutoStartQuest,
    bool AutoFinishQuest);

public sealed record ConfigureResult(SettingsSnapshot Settings, bool Changed);

public sealed record CreateHeroRequest(MutationRequestId CreateRequestId, string Name);

public sealed record CreateHeroResult(HeroIdentitySnapshot Hero, bool Replayed);
