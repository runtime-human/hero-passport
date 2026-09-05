using HeroPassport.Domain.Primitives;

namespace HeroPassport.Application.Runtime;

public sealed record StartQuestRequest(
    MutationRequestId StartRequestId,
    HeroId HeroId,
    string QuestType,
    string Title,
    string Goal);

public sealed record StartedQuestSnapshot(
    QuestId QuestId,
    HeroId HeroId,
    string QuestType,
    string Title,
    string Goal,
    DateTimeOffset StartedAtUtc,
    string Locale);

public sealed record StartQuestResult(
    StartedQuestSnapshot Quest,
    HeroIdentitySnapshot Hero,
    bool Replayed);
