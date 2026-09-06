using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Engine;
using HeroPassport.Domain.Primitives;
using Microsoft.Data.Sqlite;
using System.Text;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    private sealed record RewardComponentRow(string Key, long XpDelta);

    private static QuestRewardResult CalculateReward(FinishQuestStoreCommand command, string questType)
    {
        var quality = Quality(command);
        return QuestRewardRules.Evaluate(
            questType, command.Result, quality,
            command.Metrics.ScopeViolations, command.Metrics.UserCorrections);
    }

    private static QuestQualityFlags Quality(FinishQuestStoreCommand command) =>
        QuestQualityFlags.From(
            UnicodeScalarLength(command.Summary), command.Metrics.TestsStatus,
            command.Metrics.TestsEvidence, command.Metrics.ScopeViolations,
            command.Metrics.UserCorrections);

    private static List<RewardComponentRow> RewardComponents(FinishQuestStoreCommand command)
    {
        var quality = Quality(command);
        var components = new List<RewardComponentRow>(6);
        if (quality.HasObservedTestsPassed) components.Add(new("observed_tests_passed_bonus", 10));
        if (quality.HasCleanScope) components.Add(new("clean_scope_bonus", 10));
        if (quality.HasClearSummary) components.Add(new("clear_summary_bonus", 10));
        if (quality.HasNoUserCorrections) components.Add(new("no_user_corrections_bonus", 5));
        if (command.Metrics.ScopeViolations > 0)
            components.Add(new("scope_violation_penalty", -Math.Min(command.Metrics.ScopeViolations, 3) * 5L));
        if (command.Metrics.UserCorrections > 0)
            components.Add(new("user_correction_penalty", -Math.Min(command.Metrics.UserCorrections, 3) * 5L));
        return components;
    }

    private static async Task InsertRewardComponentsAsync(
        SqliteConnection connection, SqliteTransaction transaction, QuestReportId reportId,
        IReadOnlyList<RewardComponentRow> components, CancellationToken cancellationToken)
    {
        for (var ordinal = 0; ordinal < components.Count; ordinal++)
        {
            var component = components[ordinal];
            await ExecuteAsync(connection, transaction,
                "INSERT INTO quest_reward_components(quest_report_id,ordinal,component_key,xp_delta) VALUES($report,$ordinal,$key,$delta);",
                cancellationToken, ("$report", reportId.ToString()), ("$ordinal", ordinal),
                ("$key", component.Key), ("$delta", component.XpDelta)).ConfigureAwait(false);
        }
    }

    private static async Task<IReadOnlyList<SkillProgressSnapshot>> ApplySkillAllocationsAsync(
        SqliteConnection connection, SqliteTransaction transaction, QuestReportId reportId, HeroId heroId,
        IReadOnlyList<SkillXpAllocation> allocations, string skillProgressionVersion,
        string timestamp, CancellationToken cancellationToken)
    {
        var snapshots = new List<SkillProgressSnapshot>(allocations.Count);
        for (var ordinal = 0; ordinal < allocations.Count; ordinal++)
        {
            var allocation = allocations[ordinal];
            var xpBefore = await HeroSkillXpAsync(connection, transaction, heroId, allocation.SkillKey, cancellationToken).ConfigureAwait(false);
            var progression = SkillProgressionRules.Apply(xpBefore, allocation.XpGained, skillProgressionVersion);

            await ExecuteAsync(connection, transaction, """
                INSERT INTO quest_report_skills(
                    quest_report_id,ordinal,skill_key,xp_gained,xp_before,xp_after,level_before,level_after)
                VALUES($report,$ordinal,$skill,$gained,$before,$after,$levelBefore,$levelAfter);
                """, cancellationToken,
                ("$report", reportId.ToString()), ("$ordinal", ordinal), ("$skill", allocation.SkillKey),
                ("$gained", allocation.XpGained), ("$before", progression.XpBefore), ("$after", progression.XpAfter),
                ("$levelBefore", progression.LevelBefore), ("$levelAfter", progression.LevelAfter)).ConfigureAwait(false);

            await ExecuteAsync(connection, transaction, """
                INSERT INTO hero_skills(hero_id,skill_key,xp,updated_at_utc)
                VALUES($hero,$skill,$xp,$time)
                ON CONFLICT(hero_id,skill_key) DO UPDATE SET xp=excluded.xp,updated_at_utc=excluded.updated_at_utc;
                """, cancellationToken,
                ("$hero", heroId.ToString()), ("$skill", allocation.SkillKey),
                ("$xp", progression.XpAfter), ("$time", timestamp)).ConfigureAwait(false);

            snapshots.Add(new SkillProgressSnapshot(
                allocation.SkillKey, allocation.XpGained, progression.XpAfter,
                progression.LevelBefore, progression.LevelAfter,
                progression.IsLevelCapped, progression.NextLevelXpRequired));
        }
        return snapshots;
    }

    private static async Task<IReadOnlyList<SkillProgressSnapshot>> SkillProgressForReportAsync(
        SqliteConnection connection, SqliteTransaction transaction, QuestReportId reportId,
        string skillProgressionVersion, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction, """
            SELECT skill_key,xp_gained,xp_after,level_before,level_after
            FROM quest_report_skills WHERE quest_report_id=$report ORDER BY ordinal;
            """, ("$report", reportId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var snapshots = new List<SkillProgressSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var levelAfter = reader.GetInt32(4);
            snapshots.Add(new SkillProgressSnapshot(
                reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt32(3), levelAfter,
                levelAfter == 10, SkillProgressionRules.NextLevelXpRequired(levelAfter, skillProgressionVersion)));
        }
        return snapshots;
    }

    private static async Task<long> HeroSkillXpAsync(
        SqliteConnection connection, SqliteTransaction transaction, HeroId heroId,
        string skillKey, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction,
            "SELECT xp FROM hero_skills WHERE hero_id=$hero AND skill_key=$skill;",
            ("$hero", heroId.ToString()), ("$skill", skillKey));
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is long xp ? xp : 0L;
    }

    private static int UnicodeScalarLength(string value)
    {
        var count = 0;
        foreach (var _ in value.EnumerateRunes()) count++;
        return count;
    }
}
