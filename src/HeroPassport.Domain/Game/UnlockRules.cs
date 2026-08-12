namespace HeroPassport.Domain.Game;

public sealed record StreakResult(int Before, int After, string RuleVersion);

public static class StreakRules
{
    public const string RuleVersion = "streak/1.0.0";

    public static StreakResult Apply(int previous, QuestOutcome outcome)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(previous);
        var after = outcome == QuestOutcome.Success
            ? checked(previous + 1)
            : 0;
        return new StreakResult(previous, after, RuleVersion);
    }
}

public sealed record UnlockedTitle(string Key, DateTimeOffset UnlockedAtUtc);

public sealed record UnlockEvaluationInput(
    int HeroLevel,
    int SuccessStreak,
    int PreciseSuccessCount,
    int TestScoutSuccessCount,
    int ScopeCleanSuccessCount,
    IReadOnlyDictionary<string, int> SkillLevels,
    IReadOnlyCollection<string> ExistingTraits,
    IReadOnlyCollection<UnlockedTitle> ExistingTitles,
    DateTimeOffset UnlockedAtUtc);

public sealed record UnlockEvaluationResult(
    IReadOnlyList<string> TraitsUnlocked,
    IReadOnlyList<UnlockedTitle> TitlesUnlocked,
    IReadOnlyList<string> AllTraits,
    IReadOnlyList<UnlockedTitle> AllTitles,
    UnlockedTitle? ActiveTitle,
    string RuleVersion);

public static class UnlockRules
{
    public const string RuleVersion = "unlock/2.0.0";

    private static readonly string[] TraitCatalog =
    [
        "precise_executor",
        "test_scout",
        "scope_keeper",
        "steady_hand",
        "polyglot_crafter",
    ];

    private static readonly string[] TitleCatalog =
    [
        "rising_adventurer",
        "veteran_of_the_merge",
        "skill_specialist",
        "unbroken_builder",
        "master_of_many_tools",
    ];

    private static readonly IReadOnlyDictionary<string, int> TitlePriority =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["rising_adventurer"] = 1,
            ["veteran_of_the_merge"] = 2,
            ["skill_specialist"] = 3,
            ["unbroken_builder"] = 4,
            ["master_of_many_tools"] = 5,
        };

    public static UnlockEvaluationResult Evaluate(UnlockEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateInput(input);

        var existingTraits = new HashSet<string>(input.ExistingTraits, StringComparer.Ordinal);
        var traitsUnlocked = new List<string>();
        TryUnlockTrait("precise_executor", input.PreciseSuccessCount >= 5, existingTraits, traitsUnlocked);
        TryUnlockTrait("test_scout", input.TestScoutSuccessCount >= 5, existingTraits, traitsUnlocked);
        TryUnlockTrait("scope_keeper", input.ScopeCleanSuccessCount >= 10, existingTraits, traitsUnlocked);
        TryUnlockTrait("steady_hand", input.SuccessStreak >= 5, existingTraits, traitsUnlocked);
        TryUnlockTrait("polyglot_crafter", CountSkillsAtLeast(input.SkillLevels, 3) >= 5, existingTraits, traitsUnlocked);

        var titlesByKey = new Dictionary<string, UnlockedTitle>(StringComparer.Ordinal);
        foreach (var title in input.ExistingTitles)
        {
            titlesByKey.Add(title.Key, title);
        }

        var titlesUnlocked = new List<UnlockedTitle>();
        TryUnlockTitle("rising_adventurer", input.HeroLevel >= 5, input.UnlockedAtUtc, titlesByKey, titlesUnlocked);
        TryUnlockTitle("veteran_of_the_merge", input.HeroLevel >= 10, input.UnlockedAtUtc, titlesByKey, titlesUnlocked);
        TryUnlockTitle("skill_specialist", HasSkillAtLeast(input.SkillLevels, 5), input.UnlockedAtUtc, titlesByKey, titlesUnlocked);
        TryUnlockTitle("unbroken_builder", input.SuccessStreak >= 10, input.UnlockedAtUtc, titlesByKey, titlesUnlocked);
        TryUnlockTitle("master_of_many_tools", CountSkillsAtLeast(input.SkillLevels, 5) >= 5, input.UnlockedAtUtc, titlesByKey, titlesUnlocked);

        var allTraits = TraitCatalog.Where(existingTraits.Contains).ToArray();
        var allTitles = titlesByKey.Values
            .OrderBy(static title => TitlePriority[title.Key])
            .ThenBy(static title => title.UnlockedAtUtc)
            .ThenBy(static title => title.Key, StringComparer.Ordinal)
            .ToArray();
        var activeTitle = titlesByKey.Values
            .OrderByDescending(static title => TitlePriority[title.Key])
            .ThenByDescending(static title => title.UnlockedAtUtc)
            .ThenByDescending(static title => title.Key, StringComparer.Ordinal)
            .FirstOrDefault();

        return new UnlockEvaluationResult(
            traitsUnlocked,
            titlesUnlocked,
            allTraits,
            allTitles,
            activeTitle,
            RuleVersion);
    }

    public static int GetTitlePriority(string titleKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titleKey);
        return TitlePriority.TryGetValue(titleKey, out var priority)
            ? priority
            : throw new ArgumentException("Unknown title key.", nameof(titleKey));
    }

    private static void ValidateInput(UnlockEvaluationInput input)
    {
        if (input.HeroLevel is < 1 or > 50 ||
            input.SuccessStreak < 0 ||
            input.PreciseSuccessCount < 0 ||
            input.TestScoutSuccessCount < 0 ||
            input.ScopeCleanSuccessCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input));
        }

        ArgumentNullException.ThrowIfNull(input.SkillLevels);
        ArgumentNullException.ThrowIfNull(input.ExistingTraits);
        ArgumentNullException.ThrowIfNull(input.ExistingTitles);

        foreach (var pair in input.SkillLevels)
        {
            if (!SkillAllocationRules.IsCanonicalSkill(pair.Key) || pair.Value is < 1 or > 10)
            {
                throw new ArgumentException("Skill levels contain an unknown key or invalid level.", nameof(input));
            }
        }

        var traitSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var trait in input.ExistingTraits)
        {
            if (!ContainsOrdinal(TraitCatalog, trait) || !traitSet.Add(trait))
            {
                throw new ArgumentException("Existing traits contain an unknown or duplicate key.", nameof(input));
            }
        }

        var titleSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var title in input.ExistingTitles)
        {
            if (title is null || !ContainsOrdinal(TitleCatalog, title.Key) || !titleSet.Add(title.Key))
            {
                throw new ArgumentException("Existing titles contain an unknown or duplicate key.", nameof(input));
            }
        }
    }

    private static void TryUnlockTrait(
        string key,
        bool condition,
        ISet<string> allTraits,
        ICollection<string> newlyUnlocked)
    {
        if (condition && allTraits.Add(key))
        {
            newlyUnlocked.Add(key);
        }
    }

    private static void TryUnlockTitle(
        string key,
        bool condition,
        DateTimeOffset unlockedAtUtc,
        IDictionary<string, UnlockedTitle> allTitles,
        ICollection<UnlockedTitle> newlyUnlocked)
    {
        if (!condition || allTitles.ContainsKey(key))
        {
            return;
        }

        var unlocked = new UnlockedTitle(key, unlockedAtUtc);
        allTitles.Add(key, unlocked);
        newlyUnlocked.Add(unlocked);
    }

    private static int CountSkillsAtLeast(IReadOnlyDictionary<string, int> skillLevels, int level) =>
        skillLevels.Values.Count(value => value >= level);

    private static bool HasSkillAtLeast(IReadOnlyDictionary<string, int> skillLevels, int level) =>
        skillLevels.Values.Any(value => value >= level);

    private static bool ContainsOrdinal(IReadOnlyList<string> values, string value)
    {
        foreach (var candidate in values)
        {
            if (string.Equals(candidate, value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
