using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Game;
using HeroPassport.Domain.Primitives;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    private static async Task<FinishCalculation> CalculateFinishAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        FinishQuestState quest,
        FinishQuestStoreCommand command,
        FinishHeroState heroState,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var questType = ParseQuestType(quest.QuestType);
        var outcome = ParseQuestOutcome(command.Result);
        var observedTestsPassed =
            string.Equals(command.Metrics.TestsStatus, "passed", StringComparison.Ordinal) &&
            string.Equals(command.Metrics.TestsEvidence, "observed", StringComparison.Ordinal);
        var summaryScalarLength = command.Summary.EnumerateRunes().Count();
        var reward = RewardRules.Calculate(new QuestRewardInput(
            questType,
            outcome,
            observedTestsPassed,
            command.Metrics.ScopeViolations,
            command.Metrics.UserCorrections,
            summaryScalarLength));

        var totalXpAfter = JsonSafeInteger.Require(checked(heroState.TotalXp + reward.QuestXp));
        var heroBefore = HeroProgressionRules.GetState(heroState.TotalXp);
        var heroAfter = HeroProgressionRules.GetState(totalXpAfter);
        var rankBefore = RankRules.GetRankKey(heroBefore.Level);
        var rankAfter = RankRules.GetRankKey(heroAfter.Level);
        var heroProgress = new HeroProgressSummary(
            quest.HeroId,
            heroState.TotalXp,
            totalXpAfter,
            heroBefore.Level,
            heroAfter.Level,
            heroAfter.IsLevelCapped,
            heroAfter.LevelXp,
            heroAfter.NextLevelXpRequired,
            rankBefore,
            rankAfter);

        var trustStrain = TrustStrainRules.Calculate(
            heroState.Trust,
            heroState.Strain,
            new TrustStrainInput(
                outcome,
                observedTestsPassed,
                command.Metrics.ScopeViolations,
                command.Metrics.UserCorrections));
        var streak = StreakRules.Apply(heroState.SuccessStreak, outcome);

        var existingSkillXp = await LoadHeroSkillXpAsync(
            connection,
            transaction,
            quest.HeroId,
            cancellationToken).ConfigureAwait(false);
        var allocations = SkillAllocationRules.Allocate(reward.QuestXp, command.SkillsUsed);
        var skillProgress = new List<SkillProgressSummary>(allocations.Count);
        foreach (var allocation in allocations)
        {
            var xpBefore = existingSkillXp.GetValueOrDefault(allocation.SkillKey);
            var xpAfter = JsonSafeInteger.Require(checked(xpBefore + allocation.Xp));
            var before = SkillProgressionRules.GetState(xpBefore);
            var after = SkillProgressionRules.GetState(xpAfter);
            existingSkillXp[allocation.SkillKey] = xpAfter;
            skillProgress.Add(new SkillProgressSummary(
                allocation.SkillKey,
                allocation.Xp,
                xpBefore,
                xpAfter,
                before.Level,
                after.Level,
                after.IsLevelCapped,
                after.NextLevelXpRequired));
        }

        var skillLevels = existingSkillXp.ToDictionary(
            static pair => pair.Key,
            static pair => SkillProgressionRules.GetState(pair.Value).Level,
            StringComparer.Ordinal);
        var existingTraits = await LoadExistingTraitsAsync(
            connection,
            transaction,
            quest.HeroId,
            cancellationToken).ConfigureAwait(false);
        var existingTitles = await LoadExistingTitlesAsync(
            connection,
            transaction,
            quest.HeroId,
            cancellationToken).ConfigureAwait(false);
        var activeTitleBefore = SelectActiveTitle(existingTitles)?.Key;

        var preciseCount = await CountHistoricalBehaviorAsync(
            connection,
            transaction,
            quest.HeroId,
            "r.result='success' AND r.scope_violations=0 AND r.user_corrections=0",
            cancellationToken).ConfigureAwait(false);
        if (outcome == QuestOutcome.Success && command.Metrics.ScopeViolations == 0 && command.Metrics.UserCorrections == 0)
        {
            preciseCount = checked(preciseCount + 1);
        }

        var testScoutCount = await CountHistoricalBehaviorAsync(
            connection,
            transaction,
            quest.HeroId,
            "r.result='success' AND q.quest_type IN ('coding','debugging') AND r.tests_status='passed' AND r.tests_evidence='observed'",
            cancellationToken).ConfigureAwait(false);
        if (outcome == QuestOutcome.Success &&
            questType is QuestType.Coding or QuestType.Debugging &&
            observedTestsPassed)
        {
            testScoutCount = checked(testScoutCount + 1);
        }

        var scopeCleanCount = await CountHistoricalBehaviorAsync(
            connection,
            transaction,
            quest.HeroId,
            "r.result='success' AND r.scope_violations=0",
            cancellationToken).ConfigureAwait(false);
        if (outcome == QuestOutcome.Success && command.Metrics.ScopeViolations == 0)
        {
            scopeCleanCount = checked(scopeCleanCount + 1);
        }

        var unlocks = UnlockRules.Evaluate(new UnlockEvaluationInput(
            heroAfter.Level,
            streak.After,
            preciseCount,
            testScoutCount,
            scopeCleanCount,
            skillLevels,
            existingTraits,
            existingTitles,
            now));
        var milestones = BuildMilestones(heroBefore, heroAfter, rankBefore, rankAfter, skillProgress, streak, unlocks);

        return new FinishCalculation(
            reward,
            heroProgress,
            trustStrain,
            streak,
            skillProgress,
            unlocks,
            activeTitleBefore,
            milestones,
            existingSkillXp);
    }

    private static async Task<Dictionary<string, long>> LoadHeroSkillXpAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HeroId heroId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            "SELECT skill_key,xp FROM hero_skills WHERE hero_id=$heroId;",
            ("$heroId", heroId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(reader.GetString(0), reader.GetInt64(1));
        }

        return result;
    }

    private static async Task<string[]> LoadExistingTraitsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HeroId heroId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            "SELECT trait_key FROM hero_traits WHERE hero_id=$heroId ORDER BY unlocked_at_utc ASC,trait_key ASC;",
            ("$heroId", heroId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<string>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(reader.GetString(0));
        }

        return [.. result];
    }

    private static async Task<UnlockedTitle[]> LoadExistingTitlesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HeroId heroId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            "SELECT title_key,unlocked_at_utc FROM hero_titles WHERE hero_id=$heroId ORDER BY unlocked_at_utc ASC,title_key ASC;",
            ("$heroId", heroId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<UnlockedTitle>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new UnlockedTitle(reader.GetString(0), ParseTimestamp(reader.GetString(1))));
        }

        return [.. result];
    }

    private static UnlockedTitle? SelectActiveTitle(IEnumerable<UnlockedTitle> titles) =>
        titles
            .OrderByDescending(static title => UnlockRules.GetTitlePriority(title.Key))
            .ThenByDescending(static title => title.UnlockedAtUtc)
            .ThenByDescending(static title => title.Key, StringComparer.Ordinal)
            .FirstOrDefault();

    private static async Task<int> CountHistoricalBehaviorAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HeroId heroId,
        string predicate,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            $"SELECT COUNT(*) FROM quest_reports r JOIN quest_sessions q ON q.id=r.quest_id WHERE q.hero_id=$heroId AND {predicate};",
            ("$heroId", heroId.ToString()));
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static List<MilestoneSummary> BuildMilestones(
        ProgressionState heroBefore,
        ProgressionState heroAfter,
        string rankBefore,
        string rankAfter,
        IReadOnlyList<SkillProgressSummary> skillProgress,
        StreakResult streak,
        UnlockEvaluationResult unlocks)
    {
        var milestones = new List<MilestoneSummary>();
        if (heroAfter.Level > heroBefore.Level)
        {
            milestones.Add(new MilestoneSummary("hero.level_up", $"hero.level.{heroAfter.Level.ToString(CultureInfo.InvariantCulture)}"));
        }

        if (!string.Equals(rankBefore, rankAfter, StringComparison.Ordinal))
        {
            milestones.Add(new MilestoneSummary("hero.rank_up", $"rank.{rankAfter}"));
        }

        foreach (var skill in skillProgress)
        {
            if (skill.LevelAfter > skill.LevelBefore)
            {
                milestones.Add(new MilestoneSummary(
                    "skill.level_up",
                    $"skill.{skill.SkillKey}.level.{skill.LevelAfter.ToString(CultureInfo.InvariantCulture)}"));
            }
        }

        if (streak.After is 5 or 10)
        {
            milestones.Add(new MilestoneSummary("streak.milestone", $"streak.{streak.After.ToString(CultureInfo.InvariantCulture)}"));
        }

        foreach (var trait in unlocks.TraitsUnlocked)
        {
            milestones.Add(new MilestoneSummary("trait.unlocked", $"trait.{trait}"));
        }

        foreach (var title in unlocks.TitlesUnlocked)
        {
            milestones.Add(new MilestoneSummary("title.unlocked", $"title.{title.Key}"));
        }

        return milestones;
    }

    private static QuestType ParseQuestType(string value) => value switch
    {
        "planning" => QuestType.Planning,
        "research" => QuestType.Research,
        "coding" => QuestType.Coding,
        "review" => QuestType.Review,
        "debugging" => QuestType.Debugging,
        "documentation" => QuestType.Documentation,
        "maintenance" => QuestType.Maintenance,
        _ => throw new HeroPassportException("HP110", "Quest type is invalid."),
    };

    private static QuestOutcome ParseQuestOutcome(string value) => value switch
    {
        "success" => QuestOutcome.Success,
        "partial" => QuestOutcome.Partial,
        "blocked" => QuestOutcome.Blocked,
        "failed" => QuestOutcome.Failed,
        "abandoned" => QuestOutcome.Abandoned,
        _ => throw new HeroPassportException("HP111", "Quest result is invalid."),
    };

    private sealed record FinishCalculation(
        RewardBreakdown Reward,
        HeroProgressSummary HeroProgress,
        TrustStrainResult TrustStrain,
        StreakResult Streak,
        IReadOnlyList<SkillProgressSummary> SkillProgress,
        UnlockEvaluationResult Unlocks,
        string? ActiveTitleBefore,
        IReadOnlyList<MilestoneSummary> Milestones,
        IReadOnlyDictionary<string, long> SkillXpAfter);
}
