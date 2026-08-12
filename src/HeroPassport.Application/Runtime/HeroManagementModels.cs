using HeroPassport.Domain.Primitives;

namespace HeroPassport.Application.Runtime;

public sealed record HeroListItem(
    HeroId HeroId,
    string Name,
    bool Archived,
    bool Active,
    long TotalXp,
    int Level,
    string RankKey,
    int Trust,
    int Strain);

public sealed record ListHeroesResult(IReadOnlyList<HeroListItem> Heroes);

public sealed record HeroLifecycleResult(HeroSummary Hero, bool AlreadyInRequestedState);

public sealed record HeroCardSkill(
    string SkillKey,
    long Xp,
    int Level,
    bool IsLevelCapped,
    long? NextLevelXpRequired);

public sealed record HeroCardHero(
    HeroId HeroId,
    string Name,
    long TotalXp,
    int Level,
    bool IsLevelCapped,
    long LevelXp,
    long? NextLevelXpRequired,
    string RankKey,
    string? ActiveTitle,
    int Trust,
    int Strain,
    int SuccessStreak,
    IReadOnlyList<HeroCardSkill> TopSkills,
    IReadOnlyList<string> Traits,
    IReadOnlyList<string> Titles);

public sealed record HeroCardProject(
    string DisplayName,
    int QuestsStarted,
    int QuestsFinished,
    int QuestsSucceeded,
    long TotalXpEarned,
    int SuccessRatePermille,
    IReadOnlyList<HeroCardSkill> TopSkills);

public sealed record HeroCardResult(HeroCardHero Hero, HeroCardProject Project);
