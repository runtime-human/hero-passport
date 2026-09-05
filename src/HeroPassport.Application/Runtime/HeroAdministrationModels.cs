using HeroPassport.Domain.Primitives;

namespace HeroPassport.Application.Runtime;

public sealed record HeroListItemSnapshot(
    HeroId HeroId,
    string Name,
    bool Archived,
    bool Active,
    long TotalXp,
    int Level,
    string RankKey,
    int Trust,
    int Strain);

public sealed record HeroListResult(IReadOnlyList<HeroListItemSnapshot> Heroes);

public sealed record HeroPreferenceChangeResult(HeroListItemSnapshot Hero, bool Changed);

public sealed record CardSkillSnapshot(string SkillKey, long Xp, int Level);

public sealed record HeroCardSnapshot(
    HeroId HeroId,
    string Name,
    long TotalXp,
    int Level,
    string RankKey,
    int Trust,
    int Strain,
    long SuccessStreak,
    IReadOnlyList<CardSkillSnapshot> TopSkills,
    IReadOnlyList<string> Traits,
    IReadOnlyList<string> Titles);

public sealed record ProjectCardSnapshot(
    string DisplayName,
    long QuestsStarted,
    long QuestsFinished,
    long QuestsSucceeded,
    long TotalXpEarned,
    int SuccessRatePermille,
    IReadOnlyList<CardSkillSnapshot> TopSkills);

public sealed record HeroCardResult(HeroCardSnapshot Hero, ProjectCardSnapshot Project);
