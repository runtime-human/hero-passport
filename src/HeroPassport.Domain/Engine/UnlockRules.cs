using HeroPassport.Domain.Primitives;

namespace HeroPassport.Domain.Engine;

public sealed record UnlockSkillState(string SkillKey, int Level);

public sealed record UnlockSkillLevelChange(string SkillKey, int LevelBefore, int LevelAfter);

public sealed record UnlockMilestone(string EventKey, string SemanticKey);

public sealed record TitleUnlockState(string TitleKey, DateTimeOffset UnlockedAtUtc);

public sealed record UnlockTitleDefinition(string TitleKey, int Priority);

public sealed record UnlockEvaluationContext(
    int HeroLevelBefore,
    int HeroLevelAfter,
    string RankBefore,
    string RankAfter,
    long SuccessStreakBefore,
    long SuccessStreakAfter,
    long PreciseExecutorSuccessesAfter,
    long TestScoutSuccessesAfter,
    long ScopeKeeperSuccessesAfter,
    IReadOnlyList<UnlockSkillState> SkillsAfter,
    IReadOnlyList<UnlockSkillLevelChange> SkillLevelChanges,
    IReadOnlyList<string> ExistingTraits,
    IReadOnlyList<string> ExistingTitles);

public sealed record UnlockResult(
    IReadOnlyList<string> TraitsUnlocked,
    IReadOnlyList<string> TitlesUnlocked,
    IReadOnlyList<UnlockMilestone> Milestones,
    string RuleVersion);

public static class UnlockRules
{
    public const string RuleVersion = "unlock/2.0.0";

    public static IReadOnlyList<string> TraitKeys { get; } =
    [
        "precise_executor",
        "test_scout",
        "scope_keeper",
        "steady_hand",
        "polyglot_crafter",
    ];

    public static IReadOnlyList<UnlockTitleDefinition> TitleCatalog { get; } =
    [
        new("rising_adventurer", 1),
        new("veteran_of_the_merge", 2),
        new("skill_specialist", 3),
        new("unbroken_builder", 4),
        new("master_of_many_tools", 5),
    ];

    public static UnlockResult Evaluate(UnlockEvaluationContext context, string ruleVersion)
    {
        RequireVersion(ruleVersion);
        ArgumentNullException.ThrowIfNull(context);
        ValidateContext(context);

        var existingTraits = context.ExistingTraits.ToHashSet(StringComparer.Ordinal);
        var existingTitles = context.ExistingTitles.ToHashSet(StringComparer.Ordinal);
        var traits = new List<string>(TraitKeys.Count);
        var titles = new List<string>(TitleCatalog.Count);

        AddTraitIf(
            traits,
            existingTraits,
            "precise_executor",
            context.PreciseExecutorSuccessesAfter >= 5);
        AddTraitIf(
            traits,
            existingTraits,
            "test_scout",
            context.TestScoutSuccessesAfter >= 5);
        AddTraitIf(
            traits,
            existingTraits,
            "scope_keeper",
            context.ScopeKeeperSuccessesAfter >= 10);
        AddTraitIf(
            traits,
            existingTraits,
            "steady_hand",
            context.SuccessStreakAfter >= 5);
        AddTraitIf(
            traits,
            existingTraits,
            "polyglot_crafter",
            context.SkillsAfter.Count(static skill => skill.Level >= 3) >= 5);

        AddTitleIf(
            titles,
            existingTitles,
            "rising_adventurer",
            context.HeroLevelAfter >= 5);
        AddTitleIf(
            titles,
            existingTitles,
            "veteran_of_the_merge",
            context.HeroLevelAfter >= 10);
        AddTitleIf(
            titles,
            existingTitles,
            "skill_specialist",
            context.SkillsAfter.Any(static skill => skill.Level >= 5));
        AddTitleIf(
            titles,
            existingTitles,
            "unbroken_builder",
            context.SuccessStreakAfter >= 10);
        AddTitleIf(
            titles,
            existingTitles,
            "master_of_many_tools",
            context.SkillsAfter.Count(static skill => skill.Level >= 5) >= 5);

        return new UnlockResult(
            traits.ToArray(),
            titles.ToArray(),
            Milestones(context, traits, titles),
            RuleVersion);
    }

    public static string? SelectActiveTitle(IReadOnlyList<TitleUnlockState> titles, string ruleVersion)
    {
        RequireVersion(ruleVersion);
        ArgumentNullException.ThrowIfNull(titles);
        if (titles.Count == 0)
        {
            return null;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        TitleUnlockState? best = null;
        var bestPriority = int.MinValue;
        foreach (var title in titles)
        {
            ArgumentNullException.ThrowIfNull(title);
            var priority = Priority(title.TitleKey);
            if (!seen.Add(title.TitleKey))
            {
                throw new ArgumentException("Title unlock states must contain distinct canonical title keys.", nameof(titles));
            }

            if (best is null ||
                priority > bestPriority ||
                (priority == bestPriority && title.UnlockedAtUtc > best.UnlockedAtUtc) ||
                (priority == bestPriority && title.UnlockedAtUtc == best.UnlockedAtUtc &&
                    string.CompareOrdinal(title.TitleKey, best.TitleKey) > 0))
            {
                best = title;
                bestPriority = priority;
            }
        }

        return best!.TitleKey;
    }

    public static int TitlePriority(string titleKey, string ruleVersion)
    {
        RequireVersion(ruleVersion);
        return Priority(titleKey);
    }

    private static UnlockMilestone[] Milestones(
        UnlockEvaluationContext context,
        IReadOnlyList<string> traitsUnlocked,
        IReadOnlyList<string> titlesUnlocked)
    {
        var milestones = new List<UnlockMilestone>();
        if (context.HeroLevelAfter != context.HeroLevelBefore)
        {
            milestones.Add(new UnlockMilestone("hero_level_changed", $"hero_level:{context.HeroLevelAfter}"));
        }

        if (!string.Equals(context.RankAfter, context.RankBefore, StringComparison.Ordinal))
        {
            milestones.Add(new UnlockMilestone("rank_changed", $"rank:{context.RankAfter}"));
        }

        foreach (var skill in context.SkillLevelChanges)
        {
            if (skill.LevelAfter != skill.LevelBefore)
            {
                milestones.Add(new UnlockMilestone(
                    "skill_level_changed",
                    $"skill_level:{skill.SkillKey}:{skill.LevelAfter}"));
            }
        }

        if (context.SuccessStreakAfter != context.SuccessStreakBefore)
        {
            milestones.Add(new UnlockMilestone("streak_changed", $"streak:{context.SuccessStreakAfter}"));
        }

        foreach (var trait in traitsUnlocked)
        {
            milestones.Add(new UnlockMilestone("trait_unlocked", $"trait:{trait}"));
        }

        foreach (var title in titlesUnlocked)
        {
            milestones.Add(new UnlockMilestone("title_unlocked", $"title:{title}"));
        }

        return milestones.ToArray();
    }

    private static void ValidateContext(UnlockEvaluationContext context)
    {
        RequireHeroLevel(context.HeroLevelBefore, nameof(context.HeroLevelBefore));
        RequireHeroLevel(context.HeroLevelAfter, nameof(context.HeroLevelAfter));
        if (context.HeroLevelAfter < context.HeroLevelBefore)
        {
            throw new ArgumentException("Hero level cannot decrease during Finish.", nameof(context));
        }

        RequireRank(context.RankBefore, nameof(context.RankBefore));
        RequireRank(context.RankAfter, nameof(context.RankAfter));
        JsonSafeInteger.Require(context.SuccessStreakBefore);
        JsonSafeInteger.Require(context.SuccessStreakAfter);
        JsonSafeInteger.Require(context.PreciseExecutorSuccessesAfter);
        JsonSafeInteger.Require(context.TestScoutSuccessesAfter);
        JsonSafeInteger.Require(context.ScopeKeeperSuccessesAfter);

        ArgumentNullException.ThrowIfNull(context.SkillsAfter);
        ArgumentNullException.ThrowIfNull(context.SkillLevelChanges);
        ArgumentNullException.ThrowIfNull(context.ExistingTraits);
        ArgumentNullException.ThrowIfNull(context.ExistingTitles);

        var skillKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var skill in context.SkillsAfter)
        {
            ArgumentNullException.ThrowIfNull(skill);
            RequireSkill(skill.SkillKey, nameof(context.SkillsAfter));
            RequireSkillLevel(skill.Level, nameof(context.SkillsAfter));
            if (!skillKeys.Add(skill.SkillKey))
            {
                throw new ArgumentException("Post-Quest Skill states must contain distinct canonical keys.", nameof(context));
            }
        }

        var changedSkillKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var skill in context.SkillLevelChanges)
        {
            ArgumentNullException.ThrowIfNull(skill);
            RequireSkill(skill.SkillKey, nameof(context.SkillLevelChanges));
            RequireSkillLevel(skill.LevelBefore, nameof(context.SkillLevelChanges));
            RequireSkillLevel(skill.LevelAfter, nameof(context.SkillLevelChanges));
            if (skill.LevelAfter < skill.LevelBefore || !changedSkillKeys.Add(skill.SkillKey))
            {
                throw new ArgumentException("Skill level changes must be distinct and monotonic.", nameof(context));
            }
        }

        RequireKnownDistinct(context.ExistingTraits, TraitKeys, "Trait", nameof(context.ExistingTraits));
        RequireKnownDistinct(
            context.ExistingTitles,
            TitleCatalog.Select(static title => title.TitleKey),
            "Title",
            nameof(context.ExistingTitles));
    }

    private static void AddTraitIf(
        ICollection<string> destination,
        IReadOnlySet<string> existing,
        string key,
        bool condition)
    {
        if (condition && !existing.Contains(key))
        {
            destination.Add(key);
        }
    }

    private static void AddTitleIf(
        ICollection<string> destination,
        IReadOnlySet<string> existing,
        string key,
        bool condition)
    {
        if (condition && !existing.Contains(key))
        {
            destination.Add(key);
        }
    }

    private static void RequireKnownDistinct(
        IEnumerable<string> values,
        IEnumerable<string> canonical,
        string kind,
        string parameterName)
    {
        var allowed = canonical.ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (!allowed.Contains(value) || !seen.Add(value))
            {
                throw new ArgumentException($"{kind} keys must be distinct and canonical.", parameterName);
            }
        }
    }

    private static int Priority(string titleKey)
    {
        foreach (var title in TitleCatalog)
        {
            if (string.Equals(title.TitleKey, titleKey, StringComparison.Ordinal))
            {
                return title.Priority;
            }
        }

        throw new ArgumentException("Unknown Title key.", nameof(titleKey));
    }

    private static void RequireVersion(string ruleVersion)
    {
        if (!string.Equals(ruleVersion, RuleVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported unlock rule version.", nameof(ruleVersion));
        }
    }

    private static void RequireHeroLevel(int level, string parameterName)
    {
        if (level is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void RequireSkillLevel(int level, string parameterName)
    {
        if (level is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void RequireRank(string rankKey, string parameterName)
    {
        if (rankKey is not (
            "code_squire" or
            "code_knight" or
            "senior_warrior" or
            "staff_paladin" or
            "principal_warlord" or
            "legendary_architect"))
        {
            throw new ArgumentException("Unknown Rank key.", parameterName);
        }
    }

    private static void RequireSkill(string skillKey, string parameterName)
    {
        if (skillKey is not (
            "coding" or
            "testing_awareness" or
            "scope_control" or
            "documentation" or
            "tool_use" or
            "planning" or
            "research" or
            "debugging" or
            "review" or
            "maintenance"))
        {
            throw new ArgumentException("Unknown Skill key.", parameterName);
        }
    }
}
