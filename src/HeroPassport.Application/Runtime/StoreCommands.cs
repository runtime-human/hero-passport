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
