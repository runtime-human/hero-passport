namespace HeroPassport.Application.Runtime;

public sealed record SkillProgressionReadSnapshot(
    long Xp,
    int Level,
    bool IsLevelCapped,
    long LevelXp,
    long? NextLevelXpRequired);

public sealed record SkillProjectContributionReadSnapshot(long Xp);

public sealed record SkillProgressionReadRow(
    string SkillKey,
    SkillProgressionReadSnapshot Hero,
    SkillProjectContributionReadSnapshot ProjectContribution);

public sealed record HeroSkillProgressionReadResult(
    string HeroName,
    string ProjectDisplayName,
    IReadOnlyList<SkillProgressionReadRow> Skills);
