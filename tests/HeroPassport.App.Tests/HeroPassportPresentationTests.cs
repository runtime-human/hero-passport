using HeroPassport.App.Presentation;
using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Xunit;

namespace HeroPassport.App.Tests;

public sealed partial class HeroPassportPresentationTests
{
    [Fact]
    public void RussianCanonicalLabelsUseAcceptedTerminology()
    {
        Assert.Equal("Контроль", HeroPassportPresentation.SkillLabel("ru-RU", "scope_control"));
        Assert.Equal("Бонус за контроль", HeroPassportPresentation.RewardComponentLabel("ru-RU", "clean_scope_bonus"));
        Assert.Equal("Выход за задачу", HeroPassportPresentation.RewardComponentLabel("ru-RU", "scope_violation_penalty"));
    }

    [Fact]
    public void StartPresentationHasThreeLocalizedStyleVariants()
    {
        var minimal = HeroPassportPresentation.RenderStart("en-US", "minimal", "Ship localization", replayed: false);
        var engineering = HeroPassportPresentation.RenderStart("en-US", "rpg_engineering", "Ship localization", replayed: false);
        var classic = HeroPassportPresentation.RenderStart("en-US", "classic_rpg", "Ship localization", replayed: false);

        Assert.Equal("⚔ Ship localization", minimal);
        Assert.Contains("Quest", engineering, StringComparison.Ordinal);
        Assert.Contains("New quest", classic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, new[] { minimal, engineering, classic }.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void FinishAndCardPresentationRespectCapAndDoNotInventNextThreshold()
    {
        var finish = CappedFinish();
        var card = CappedCard(finish.HeroProgress.HeroId);

        var finishText = HeroPassportPresentation.RenderFinish("ru-RU", "rpg_engineering", finish);
        var cardText = HeroPassportPresentation.RenderCard("ru-RU", "minimal", card);

        Assert.Contains("+95 XP", finishText, StringComparison.Ordinal);
        Assert.Contains("ур. 50", finishText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("макс", finishText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("следующ", finishText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Контроль", finishText, StringComparison.Ordinal);
        Assert.Contains("Несломленный создатель", finishText, StringComparison.Ordinal);
        Assert.Contains("Легендарный архитектор", cardText, StringComparison.Ordinal);
        Assert.DoesNotContain("следующ", cardText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NeutralAndRussianResourcesHaveIdenticalKeysAndPlaceholderShapes()
    {
        var manager = new ResourceManager(
            "HeroPassport.App.Presentation.HeroPassportResources",
            typeof(HeroPassportPresentation).Assembly);
        using var neutral = manager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false);
        using var russian = manager.GetResourceSet(CultureInfo.GetCultureInfo("ru-RU"), createIfNotExists: true, tryParents: false);

        Assert.NotNull(neutral);
        Assert.NotNull(russian);
        var neutralValues = ResourceValues(neutral!);
        var russianValues = ResourceValues(russian!);
        Assert.Equal(neutralValues.Keys.Order(StringComparer.Ordinal), russianValues.Keys.Order(StringComparer.Ordinal));

        foreach (var key in neutralValues.Keys)
        {
            Assert.Equal(PlaceholderIndexes(neutralValues[key]), PlaceholderIndexes(russianValues[key]));
        }
    }

    private static FinishQuestResult CappedFinish()
    {
        var heroId = HeroId.New();
        return new FinishQuestResult(
            QuestId.New(),
            "success",
            new QuestRewardSnapshot(
                60,
                35,
                0,
                95,
                1000,
                95,
                [
                    new RewardComponentSnapshot("observed_tests_passed_bonus", 10),
                    new RewardComponentSnapshot("clean_scope_bonus", 10),
                    new RewardComponentSnapshot("clear_summary_bonus", 10),
                    new RewardComponentSnapshot("no_user_corrections_bonus", 5),
                ],
                "reward/2.0.0"),
            new HeroProgressSnapshot(
                heroId,
                31_750,
                31_845,
                50,
                50,
                true,
                95,
                null,
                "legendary_architect",
                "legendary_architect",
                "hero-progression/2.0.0",
                "rank/1.0.0"),
            new TrustStrainSnapshot(50, 52, 20, 18, [], "trust-strain/1.0.0"),
            new StreakSnapshot(9, 10, "streak/1.0.0"),
            [new SkillProgressSnapshot("scope_control", 95, 1_430, 9, 10, true, null)],
            ["scope_keeper"],
            ["unbroken_builder"],
            "unbroken_builder",
            [new MilestoneSnapshot("title_unlocked", "title:unbroken_builder")],
            false,
            false);
    }

    private static HeroCardResult CappedCard(HeroId heroId) => new(
        new HeroCardSnapshot(
            heroId,
            "Nova",
            31_845,
            50,
            true,
            95,
            null,
            "legendary_architect",
            "unbroken_builder",
            52,
            18,
            10,
            [new CardSkillSnapshot("scope_control", 1_430, 10, true, null)],
            ["scope_keeper"],
            ["unbroken_builder"]),
        new ProjectCardSnapshot("Hero Passport", 10, 10, 10, 950, 1000, []));

    private static Dictionary<string, string> ResourceValues(ResourceSet resourceSet)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in resourceSet)
        {
            result.Add((string)entry.Key, Assert.IsType<string>(entry.Value));
        }
        return result;
    }

    private static int[] PlaceholderIndexes(string value) => PlaceholderRegex()
        .Matches(value)
        .Select(static match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
        .Distinct()
        .Order()
        .ToArray();

    [GeneratedRegex(@"\{(\d+)(?:[^}]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();
}
