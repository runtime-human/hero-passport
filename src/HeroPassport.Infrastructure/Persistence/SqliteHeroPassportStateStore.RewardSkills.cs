using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Engine;
using HeroPassport.Domain.Primitives;
using Microsoft.Data.Sqlite;
using System.Text;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    private static QuestRewardCalculation CalculateReward(
        FinishQuestStoreCommand command,
        string questType,
        string rewardRuleVersion) =>
        QuestRewardEngine.Calculate(
            questType,
            command.Result,
            new QuestQualitySignals(
                command.Metrics.TestsMentioned,
                command.Metrics.ScopeViolations,
                command.Metrics.UserCorrections,
                command.Metrics.BuildStatus,
                command.Metrics.BuildEvidence,
                command.Metrics.TestsStatus,
                command.Metrics.TestsEvidence,
                command.Summary.EnumerateRunes().Count()),
            rewardRuleVersion);

    private static async Task InsertRewardComponentsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        QuestReportId reportId,
        IReadOnlyList<RewardComponent> components,
        CancellationToken cancellationToken)
    {
        for (var ordinal = 0; ordinal < components.Count; ordinal++)
        {
            var component = components[ordinal];
            await ExecuteAsync(
                connection,
                transaction,
                "INSERT INTO quest_reward_components(quest_report_id,ordinal,component_key,xp_delta) VALUES($report,$ordinal,$key,$delta);",
                cancellationToken,
                ("$report", reportId.ToString()),
                ("$ordinal", ordinal),
                ("$key", component.Key),
                ("$delta", component.XpDelta)).ConfigureAwait(false);
        }
    }

    private static async Task ApplySkillAllocationsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        QuestReportId reportId,
        HeroId heroId,
        IReadOnlyList<SkillXpAllocation> allocations,
        string timestamp,
        CancellationToken cancellationToken)
    {
        for (var ordinal = 0; ordinal < allocations.Count; ordinal++)
        {
            var allocation = allocations[ordinal];
            var xpBefore = await HeroSkillXpAsync(
                connection,
                transaction,
                heroId,
                allocation.SkillKey,
                cancellationToken).ConfigureAwait(false);
            var xpAfter = JsonSafeInteger.Require(checked(xpBefore + allocation.XpGained));

            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO quest_report_skills(
                    quest_report_id,ordinal,skill_key,xp_gained,xp_before,xp_after,level_before,level_after)
                VALUES($report,$ordinal,$skill,$gained,$before,$after,NULL,NULL);
                """,
                cancellationToken,
                ("$report", reportId.ToString()),
                ("$ordinal", ordinal),
                ("$skill", allocation.SkillKey),
                ("$gained", allocation.XpGained),
                ("$before", xpBefore),
                ("$after", xpAfter)).ConfigureAwait(false);

            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO hero_skills(hero_id,skill_key,xp,updated_at_utc)
                VALUES($hero,$skill,$xp,$time)
                ON CONFLICT(hero_id,skill_key) DO UPDATE SET
                    xp=excluded.xp,
                    updated_at_utc=excluded.updated_at_utc;
                """,
                cancellationToken,
                ("$hero", heroId.ToString()),
                ("$skill", allocation.SkillKey),
                ("$xp", xpAfter),
                ("$time", timestamp)).ConfigureAwait(false);
        }
    }

    private static async Task<long> HeroSkillXpAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HeroId heroId,
        string skillKey,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            "SELECT xp FROM hero_skills WHERE hero_id=$hero AND skill_key=$skill;",
            ("$hero", heroId.ToString()),
            ("$skill", skillKey));
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is long xp
            ? xp
            : 0L;
    }
}
