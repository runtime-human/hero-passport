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
    string ArgsEncodingVersion,
    HeroId HeroId,
    string QuestType,
    string Title,
    string Goal,
    ProjectBindingContext Project);

public sealed record FinishQuestStoreCommand(
    MutationRequestId RequestId,
    string ArgsEncodingVersion,
    byte[] ArgsHash,
    QuestId QuestId,
    string Result,
    string Summary,
    FinishQuestMetrics Metrics,
    IReadOnlyList<string> SkillsUsed,
    ProjectBindingContext Project);
