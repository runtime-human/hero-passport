namespace HeroPassport.Application.Runtime;

public sealed record SkillProgressionReadSnapshot(
    string SkillKey,
    long Xp,
    int Level,
    bool IsLevelCapped,
    long LevelXp,
    long? NextLevelXpRequired);

public sealed record SkillProgressionReadRow(
    string SkillKey,
    SkillProgressionReadSnapshot Hero,
    SkillProgressionReadSnapshot Project);

public sealed record HeroSkillProgressionReadResult(
    string HeroName,
    string ProjectDisplayName,
    IReadOnlyList<SkillProgressionReadRow> Skills);
