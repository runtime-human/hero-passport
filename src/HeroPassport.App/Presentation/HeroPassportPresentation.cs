using HeroPassport.Application.Runtime;
using System.Globalization;
using System.Resources;

namespace HeroPassport.App.Presentation;

public sealed class HeroPassportPresentation
{
    private static readonly ResourceManager Resources = new(
        "HeroPassport.App.Presentation.HeroPassportResources",
        typeof(HeroPassportPresentation).Assembly);

    public string SkillLabel(string locale, string skillKey) =>
        Get(SkillResourceKey(skillKey), Culture(locale));

    public string RewardComponentLabel(string locale, string componentKey) =>
        Get(RewardResourceKey(componentKey), Culture(locale));

    public string RenderStart(string locale, string presentationStyle, string title, bool replayed)
    {
        ArgumentNullException.ThrowIfNull(title);
        var culture = Culture(locale);
        var resourceKey = (Style(presentationStyle), replayed) switch
        {
            ("minimal", false) => "Start_minimal",
            ("rpg_engineering", false) => "Start_rpg_engineering",
            ("classic_rpg", false) => "Start_classic_rpg",
            ("minimal", true) => "StartReplay_minimal",
            ("rpg_engineering", true) => "StartReplay_rpg_engineering",
            ("classic_rpg", true) => "StartReplay_classic_rpg",
            _ => throw new InvalidOperationException("Unsupported presentation state."),
        };
        return Format(resourceKey, culture, title);
    }

    public string RenderFinish(string locale, string presentationStyle, FinishQuestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var culture = Culture(locale);
        var style = Style(presentationStyle);
        var lines = new List<string>
        {
            Format(
                FinishResourceKey(style),
                culture,
                ResultLabel(result.Result, culture),
                result.Reward.XpGained,
                LevelText(result.HeroProgress.LevelAfter, result.HeroProgress.IsLevelCapped, culture)),
        };

        foreach (var component in result.Reward.Components)
        {
            lines.Add(Format(
                "RewardLine",
                culture,
                Get(RewardResourceKey(component.Key), culture),
                component.XpDelta));
        }

        foreach (var skill in result.SkillProgress)
        {
            lines.Add(Format(
                "SkillLine",
                culture,
                Get(SkillResourceKey(skill.SkillKey), culture),
                skill.XpGained,
                LevelText(skill.LevelAfter, skill.IsLevelCapped, culture)));
        }

        if (result.ActiveTitle is not null)
        {
            lines.Add(Format("ActiveTitleLine", culture, TitleLabel(result.ActiveTitle, culture)));
        }

        foreach (var milestone in result.Milestones)
        {
            lines.Add(RenderMilestone(style, milestone, culture));
        }

        var rendered = string.Join(Environment.NewLine, lines);
        return result.Replayed || result.AlreadyFinalized
            ? Format("FinishReplayPrefix", culture, rendered)
            : rendered;
    }

    public string RenderCard(string locale, string presentationStyle, HeroCardResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var culture = Culture(locale);
        var style = Style(presentationStyle);
        var hero = result.Hero;
        var lines = new List<string>
        {
            Format(
                CardResourceKey(style),
                culture,
                hero.Name,
                RankLabel(hero.RankKey, culture),
                LevelText(hero.Level, hero.IsLevelCapped, culture),
                hero.TotalXp),
            Format("CardStats", culture, hero.Trust, hero.Strain, hero.SuccessStreak),
        };

        if (hero.ActiveTitle is not null)
        {
            lines.Add(Format("ActiveTitleLine", culture, TitleLabel(hero.ActiveTitle, culture)));
        }

        foreach (var skill in hero.TopSkills)
        {
            lines.Add(Format(
                "CardSkillLine",
                culture,
                Get(SkillResourceKey(skill.SkillKey), culture),
                skill.Xp,
                LevelText(skill.Level, skill.IsLevelCapped, culture)));
        }

        lines.Add(Format(
            "ProjectStats",
            culture,
            result.Project.DisplayName,
            result.Project.QuestsFinished,
            result.Project.QuestsStarted,
            result.Project.TotalXpEarned));
        return string.Join(Environment.NewLine, lines);
    }

    private static string RenderMilestone(string style, MilestoneSnapshot milestone, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(milestone);
        var semantic = MilestoneSemantic(milestone, culture);
        return Format(MilestoneWrapperKey(style), culture, semantic);
    }

    private static string MilestoneSemantic(MilestoneSnapshot milestone, CultureInfo culture)
    {
        var parts = milestone.SemanticKey.Split(':', StringSplitOptions.None);
        return milestone.EventKey switch
        {
            "hero_level_changed" when parts is ["hero_level", var level] && int.TryParse(level, CultureInfo.InvariantCulture, out var value) =>
                Format("MilestoneHeroLevel", culture, value),
            "rank_changed" when parts is ["rank", var rank] =>
                Format("MilestoneRank", culture, RankLabel(rank, culture)),
            "skill_level_changed" when parts is ["skill_level", var skill, var level] && int.TryParse(level, CultureInfo.InvariantCulture, out var value) =>
                Format("MilestoneSkillLevel", culture, Get(SkillResourceKey(skill), culture), value),
            "streak_changed" when parts is ["streak", var streak] && long.TryParse(streak, CultureInfo.InvariantCulture, out var value) =>
                Format("MilestoneStreak", culture, value),
            "trait_unlocked" when parts is ["trait", var trait] =>
                Format("MilestoneTrait", culture, TraitLabel(trait, culture)),
            "title_unlocked" when parts is ["title", var title] =>
                Format("MilestoneTitle", culture, TitleLabel(title, culture)),
            _ => throw new ArgumentException("Unknown milestone semantic key.", nameof(milestone)),
        };
    }

    private static CultureInfo Culture(string locale) => locale switch
    {
        "en-US" => CultureInfo.GetCultureInfo("en-US"),
        "ru-RU" => CultureInfo.GetCultureInfo("ru-RU"),
        _ => throw new ArgumentException("Unsupported locale.", nameof(locale)),
    };

    private static string Style(string presentationStyle) => presentationStyle switch
    {
        "minimal" => presentationStyle,
        "rpg_engineering" => presentationStyle,
        "classic_rpg" => presentationStyle,
        _ => throw new ArgumentException("Unsupported presentation style.", nameof(presentationStyle)),
    };

    private static string LevelText(int level, bool capped, CultureInfo culture) =>
        Format(capped ? "LevelCapped" : "LevelPlain", culture, level);

    private static string ResultLabel(string result, CultureInfo culture) => result switch
    {
        "success" => Get("Result_success", culture),
        "partial" => Get("Result_partial", culture),
        "blocked" => Get("Result_blocked", culture),
        "failed" => Get("Result_failed", culture),
        "abandoned" => Get("Result_abandoned", culture),
        _ => throw new ArgumentException("Unknown Quest result.", nameof(result)),
    };

    private static string RankLabel(string rankKey, CultureInfo culture) =>
        Get(RankResourceKey(rankKey), culture);

    private static string TraitLabel(string traitKey, CultureInfo culture) =>
        Get(TraitResourceKey(traitKey), culture);

    private static string TitleLabel(string titleKey, CultureInfo culture) =>
        Get(TitleResourceKey(titleKey), culture);

    private static string FinishResourceKey(string style) => style switch
    {
        "minimal" => "Finish_minimal",
        "rpg_engineering" => "Finish_rpg_engineering",
        "classic_rpg" => "Finish_classic_rpg",
        _ => throw new InvalidOperationException("Unsupported presentation style."),
    };

    private static string CardResourceKey(string style) => style switch
    {
        "minimal" => "Card_minimal",
        "rpg_engineering" => "Card_rpg_engineering",
        "classic_rpg" => "Card_classic_rpg",
        _ => throw new InvalidOperationException("Unsupported presentation style."),
    };

    private static string MilestoneWrapperKey(string style) => style switch
    {
        "minimal" => "MilestoneWrap_minimal",
        "rpg_engineering" => "MilestoneWrap_rpg_engineering",
        "classic_rpg" => "MilestoneWrap_classic_rpg",
        _ => throw new InvalidOperationException("Unsupported presentation style."),
    };

    private static string SkillResourceKey(string skillKey) => skillKey switch
    {
        "coding" => "Skill_coding",
        "testing_awareness" => "Skill_testing_awareness",
        "scope_control" => "Skill_scope_control",
        "documentation" => "Skill_documentation",
        "tool_use" => "Skill_tool_use",
        "planning" => "Skill_planning",
        "research" => "Skill_research",
        "debugging" => "Skill_debugging",
        "review" => "Skill_review",
        "maintenance" => "Skill_maintenance",
        _ => throw new ArgumentException("Unknown Skill key.", nameof(skillKey)),
    };

    private static string RewardResourceKey(string componentKey) => componentKey switch
    {
        "observed_tests_passed_bonus" => "Reward_observed_tests_passed_bonus",
        "clean_scope_bonus" => "Reward_clean_scope_bonus",
        "clear_summary_bonus" => "Reward_clear_summary_bonus",
        "no_user_corrections_bonus" => "Reward_no_user_corrections_bonus",
        "scope_violation_penalty" => "Reward_scope_violation_penalty",
        "user_correction_penalty" => "Reward_user_correction_penalty",
        _ => throw new ArgumentException("Unknown reward component key.", nameof(componentKey)),
    };

    private static string RankResourceKey(string rankKey) => rankKey switch
    {
        "code_squire" => "Rank_code_squire",
        "code_knight" => "Rank_code_knight",
        "senior_warrior" => "Rank_senior_warrior",
        "staff_paladin" => "Rank_staff_paladin",
        "principal_warlord" => "Rank_principal_warlord",
        "legendary_architect" => "Rank_legendary_architect",
        _ => throw new ArgumentException("Unknown Rank key.", nameof(rankKey)),
    };

    private static string TraitResourceKey(string traitKey) => traitKey switch
    {
        "precise_executor" => "Trait_precise_executor",
        "test_scout" => "Trait_test_scout",
        "scope_keeper" => "Trait_scope_keeper",
        "steady_hand" => "Trait_steady_hand",
        "polyglot_crafter" => "Trait_polyglot_crafter",
        _ => throw new ArgumentException("Unknown Trait key.", nameof(traitKey)),
    };

    private static string TitleResourceKey(string titleKey) => titleKey switch
    {
        "rising_adventurer" => "Title_rising_adventurer",
        "veteran_of_the_merge" => "Title_veteran_of_the_merge",
        "skill_specialist" => "Title_skill_specialist",
        "unbroken_builder" => "Title_unbroken_builder",
        "master_of_many_tools" => "Title_master_of_many_tools",
        _ => throw new ArgumentException("Unknown Title key.", nameof(titleKey)),
    };

    private static string Get(string resourceKey, CultureInfo culture) =>
        Resources.GetString(resourceKey, culture) ??
        throw new InvalidOperationException($"Missing presentation resource '{resourceKey}'.");

    private static string Format(string resourceKey, CultureInfo culture, params object?[] arguments) =>
        string.Format(culture, Get(resourceKey, culture), arguments);
}
