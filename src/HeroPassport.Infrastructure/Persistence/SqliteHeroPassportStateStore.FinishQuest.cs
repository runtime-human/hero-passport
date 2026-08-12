using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Game;
using HeroPassport.Domain.Primitives;
using Microsoft.Data.Sqlite;
using System.Data;
using System.Security.Cryptography;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    public async Task<FinishQuestResult> FinishQuestAsync(
        FinishQuestStoreCommand command,
        ProjectBindingContext project,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);

        var existingReceipt = await GetReceiptAsync(
            connection,
            transaction,
            "finish_quest",
            command.RequestId.ToString(),
            cancellationToken).ConfigureAwait(false);
        if (existingReceipt is not null)
        {
            var currentProjectId = await FindProjectIdAsync(
                connection,
                transaction,
                project.WorkspaceFingerprint,
                cancellationToken).ConfigureAwait(false);
            if (existingReceipt.ProjectId is null ||
                !string.Equals(existingReceipt.ProjectId, currentProjectId, StringComparison.Ordinal))
            {
                throw new HeroPassportException("HP135", "The mutation request ID was already used with different context or arguments.");
            }

            EnsureReceiptMatches(existingReceipt, command.ArgsEncodingVersion, command.ArgsHash);
            var persistedReplay = await LoadPersistedFinishAsync(
                connection,
                transaction,
                command.QuestId,
                cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            return persistedReplay with { Replayed = true, AlreadyFinalized = true };
        }

        var quest = await LoadFinishQuestStateAsync(connection, transaction, command.QuestId, cancellationToken).ConfigureAwait(false);
        var boundProjectId = await FindProjectIdAsync(
            connection,
            transaction,
            project.WorkspaceFingerprint,
            cancellationToken).ConfigureAwait(false);
        if (!string.Equals(boundProjectId, quest.ProjectId, StringComparison.Ordinal))
        {
            throw new HeroPassportException("HP134", "Quest belongs to a different Project context.");
        }

        if (quest.Finished)
        {
            var persisted = await LoadPersistedFinishAsync(
                connection,
                transaction,
                command.QuestId,
                cancellationToken).ConfigureAwait(false);
            var finalization = await LoadFinalizationHashAsync(
                connection,
                transaction,
                command.QuestId,
                cancellationToken).ConfigureAwait(false);
            if (!string.Equals(finalization.EncodingVersion, command.ArgsEncodingVersion, StringComparison.Ordinal) ||
                finalization.ArgsHash.Length != command.ArgsHash.Length ||
                !CryptographicOperations.FixedTimeEquals(finalization.ArgsHash, command.ArgsHash))
            {
                throw new HeroPassportException("HP136", "Quest is already finalized with a different payload.");
            }

            await InsertReceiptAsync(
                connection,
                transaction,
                "finish_quest",
                command.RequestId.ToString(),
                command.ArgsEncodingVersion,
                command.ArgsHash,
                "quest_finish",
                command.QuestId.ToString(),
                quest.ProjectId,
                quest.HeroId.ToString(),
                FormatTimestamp(now),
                cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            return persisted with { Replayed = false, AlreadyFinalized = true };
        }

        var heroState = await LoadFinishHeroStateAsync(
            connection,
            transaction,
            quest.HeroId,
            cancellationToken).ConfigureAwait(false);
        var calculation = await CalculateFinishAsync(
            connection,
            transaction,
            quest,
            command,
            heroState,
            now,
            cancellationToken).ConfigureAwait(false);
        var timestamp = FormatTimestamp(now);
        var reportId = Guid.CreateVersion7().ToString("D");
        var xpEventId = Guid.CreateVersion7().ToString("D");

        await InsertQuestReportAsync(
            connection,
            transaction,
            reportId,
            command,
            calculation,
            timestamp,
            cancellationToken).ConfigureAwait(false);
        await PersistRewardComponentsAsync(
            connection,
            transaction,
            reportId,
            calculation.Reward.Components,
            cancellationToken).ConfigureAwait(false);
        await PersistTrustStrainComponentsAsync(
            connection,
            transaction,
            reportId,
            calculation.TrustStrain.Components,
            cancellationToken).ConfigureAwait(false);
        await PersistSkillProgressAsync(
            connection,
            transaction,
            reportId,
            quest.HeroId,
            calculation.SkillProgress,
            timestamp,
            cancellationToken).ConfigureAwait(false);
        await PersistUnlocksAsync(
            connection,
            transaction,
            command.QuestId,
            quest.HeroId,
            calculation.Unlocks,
            timestamp,
            cancellationToken).ConfigureAwait(false);
        await PersistMilestonesAsync(
            connection,
            transaction,
            reportId,
            calculation.Milestones,
            cancellationToken).ConfigureAwait(false);

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            "INSERT INTO xp_events(id,quest_id,hero_id,project_id,xp_delta,reward_rule_version,created_at_utc) VALUES($id,$questId,$heroId,$projectId,$xp,$rule,$created);",
            cancellationToken,
            ("$id", xpEventId),
            ("$questId", command.QuestId.ToString()),
            ("$heroId", quest.HeroId.ToString()),
            ("$projectId", quest.ProjectId),
            ("$xp", calculation.Reward.QuestXp),
            ("$rule", calculation.Reward.RuleVersion),
            ("$created", timestamp)).ConfigureAwait(false);

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            "UPDATE heroes SET total_xp=$xp,trust=$trust,strain=$strain,success_streak=$streak,updated_at_utc=$updated WHERE id=$heroId;",
            cancellationToken,
            ("$xp", calculation.HeroProgress.TotalXpAfter),
            ("$trust", calculation.TrustStrain.TrustAfter),
            ("$strain", calculation.TrustStrain.StrainAfter),
            ("$streak", calculation.Streak.After),
            ("$updated", timestamp),
            ("$heroId", quest.HeroId.ToString())).ConfigureAwait(false);

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            "UPDATE hero_project_stats SET quests_finished=quests_finished+1,quests_succeeded=quests_succeeded+$success,total_xp_earned=total_xp_earned+$xp,last_quest_at_utc=$updated WHERE hero_id=$heroId AND project_id=$projectId;",
            cancellationToken,
            ("$success", string.Equals(command.Result, "success", StringComparison.Ordinal) ? 1 : 0),
            ("$xp", calculation.Reward.QuestXp),
            ("$updated", timestamp),
            ("$heroId", quest.HeroId.ToString()),
            ("$projectId", quest.ProjectId)).ConfigureAwait(false);

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            "UPDATE quest_sessions SET status='finished',finished_at_utc=$finished WHERE id=$questId;",
            cancellationToken,
            ("$finished", timestamp),
            ("$questId", command.QuestId.ToString())).ConfigureAwait(false);

        await InsertReceiptAsync(
            connection,
            transaction,
            "finish_quest",
            command.RequestId.ToString(),
            command.ArgsEncodingVersion,
            command.ArgsHash,
            "quest_finish",
            command.QuestId.ToString(),
            quest.ProjectId,
            quest.HeroId.ToString(),
            timestamp,
            cancellationToken).ConfigureAwait(false);

        transaction.Commit();
        return ToFinishResult(command.QuestId, command.Result, calculation, replayed: false, alreadyFinalized: false);
    }

    private static async Task InsertQuestReportAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string reportId,
        FinishQuestStoreCommand command,
        FinishCalculation calculation,
        string timestamp,
        CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            """
            INSERT INTO quest_reports(
                id,quest_id,result,summary,tests_mentioned,scope_violations,user_corrections,
                build_status,build_evidence,tests_status,tests_evidence,
                finalization_args_encoding_version,finalization_args_hash,
                reward_rule_version,hero_progression_version,skill_progression_version,skill_allocation_version,
                trust_strain_rule_version,streak_rule_version,unlock_rule_version,rank_rule_version,
                base_xp,bonus_xp,penalty_xp,raw_xp,outcome_permille,xp_gained,
                hero_total_xp_before,hero_total_xp_after,hero_level_before,hero_level_after,
                rank_before,rank_after,trust_before,trust_after,strain_before,strain_after,
                streak_before,streak_after,active_title_before,active_title_after,created_at_utc)
            VALUES(
                $id,$questId,$result,$summary,$testsMentioned,$scopeViolations,$userCorrections,
                $buildStatus,$buildEvidence,$testsStatus,$testsEvidence,
                $encoding,$hash,
                $rewardVersion,$heroProgression,$skillProgression,$skillAllocation,
                $trustStrain,$streak,$unlock,$rankVersion,
                $baseXp,$bonusXp,$penaltyXp,$rawXp,$permille,$xpGained,
                $xpBefore,$xpAfter,$levelBefore,$levelAfter,
                $rankBefore,$rankAfter,$trustBefore,$trustAfter,$strainBefore,$strainAfter,
                $streakBefore,$streakAfter,$titleBefore,$titleAfter,$created);
            """,
            cancellationToken,
            ("$id", reportId),
            ("$questId", command.QuestId.ToString()),
            ("$result", command.Result),
            ("$summary", command.Summary),
            ("$testsMentioned", command.Metrics.TestsMentioned ? 1 : 0),
            ("$scopeViolations", command.Metrics.ScopeViolations),
            ("$userCorrections", command.Metrics.UserCorrections),
            ("$buildStatus", command.Metrics.BuildStatus),
            ("$buildEvidence", command.Metrics.BuildEvidence),
            ("$testsStatus", command.Metrics.TestsStatus),
            ("$testsEvidence", command.Metrics.TestsEvidence),
            ("$encoding", command.ArgsEncodingVersion),
            ("$hash", command.ArgsHash),
            ("$rewardVersion", calculation.Reward.RuleVersion),
            ("$heroProgression", HeroProgressionRules.RuleVersion),
            ("$skillProgression", SkillProgressionRules.RuleVersion),
            ("$skillAllocation", SkillAllocationRules.RuleVersion),
            ("$trustStrain", TrustStrainRules.RuleVersion),
            ("$streak", StreakRules.RuleVersion),
            ("$unlock", UnlockRules.RuleVersion),
            ("$rankVersion", RankRules.RuleVersion),
            ("$baseXp", calculation.Reward.BaseXp),
            ("$bonusXp", calculation.Reward.BonusXp),
            ("$penaltyXp", calculation.Reward.PenaltyXp),
            ("$rawXp", calculation.Reward.RawXp),
            ("$permille", calculation.Reward.OutcomePermille),
            ("$xpGained", calculation.Reward.QuestXp),
            ("$xpBefore", calculation.HeroProgress.TotalXpBefore),
            ("$xpAfter", calculation.HeroProgress.TotalXpAfter),
            ("$levelBefore", calculation.HeroProgress.LevelBefore),
            ("$levelAfter", calculation.HeroProgress.LevelAfter),
            ("$rankBefore", calculation.HeroProgress.RankBefore),
            ("$rankAfter", calculation.HeroProgress.RankAfter),
            ("$trustBefore", calculation.TrustStrain.TrustBefore),
            ("$trustAfter", calculation.TrustStrain.TrustAfter),
            ("$strainBefore", calculation.TrustStrain.StrainBefore),
            ("$strainAfter", calculation.TrustStrain.StrainAfter),
            ("$streakBefore", calculation.Streak.Before),
            ("$streakAfter", calculation.Streak.After),
            ("$titleBefore", calculation.ActiveTitleBefore),
            ("$titleAfter", calculation.Unlocks.ActiveTitle?.Key),
            ("$created", timestamp)).ConfigureAwait(false);
    }

    private static async Task PersistRewardComponentsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string reportId,
        IReadOnlyList<RewardComponent> components,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < components.Count; index++)
        {
            var component = components[index];
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                "INSERT INTO quest_reward_components(quest_report_id,ordinal,component_key,xp_delta) VALUES($reportId,$ordinal,$key,$delta);",
                cancellationToken,
                ("$reportId", reportId),
                ("$ordinal", index),
                ("$key", component.Key),
                ("$delta", component.Delta)).ConfigureAwait(false);
        }
    }

    private static async Task PersistTrustStrainComponentsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string reportId,
        IReadOnlyList<TrustStrainComponent> components,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < components.Count; index++)
        {
            var component = components[index];
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                "INSERT INTO quest_trust_strain_components(quest_report_id,ordinal,component_key,trust_delta,strain_delta) VALUES($reportId,$ordinal,$key,$trust,$strain);",
                cancellationToken,
                ("$reportId", reportId),
                ("$ordinal", index),
                ("$key", component.Key),
                ("$trust", component.TrustDelta),
                ("$strain", component.StrainDelta)).ConfigureAwait(false);
        }
    }

    private static async Task PersistSkillProgressAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string reportId,
        HeroId heroId,
        IReadOnlyList<SkillProgressSummary> skills,
        string timestamp,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < skills.Count; index++)
        {
            var skill = skills[index];
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                """
                INSERT INTO hero_skills(hero_id,skill_key,xp,updated_at_utc)
                VALUES($heroId,$skillKey,$xp,$updated)
                ON CONFLICT(hero_id,skill_key) DO UPDATE SET xp=excluded.xp,updated_at_utc=excluded.updated_at_utc;
                """,
                cancellationToken,
                ("$heroId", heroId.ToString()),
                ("$skillKey", skill.SkillKey),
                ("$xp", skill.XpAfter),
                ("$updated", timestamp)).ConfigureAwait(false);
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                "INSERT INTO quest_report_skills(quest_report_id,ordinal,skill_key,xp_gained,xp_before,xp_after,level_before,level_after) VALUES($reportId,$ordinal,$skillKey,$gained,$before,$after,$levelBefore,$levelAfter);",
                cancellationToken,
                ("$reportId", reportId),
                ("$ordinal", index),
                ("$skillKey", skill.SkillKey),
                ("$gained", skill.XpGained),
                ("$before", skill.XpBefore),
                ("$after", skill.XpAfter),
                ("$levelBefore", skill.LevelBefore),
                ("$levelAfter", skill.LevelAfter)).ConfigureAwait(false);
        }
    }

    private static async Task PersistUnlocksAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        QuestId questId,
        HeroId heroId,
        UnlockEvaluationResult unlocks,
        string timestamp,
        CancellationToken cancellationToken)
    {
        foreach (var trait in unlocks.TraitsUnlocked)
        {
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                "INSERT INTO hero_traits(hero_id,trait_key,unlocked_at_utc,source_quest_id) VALUES($heroId,$key,$unlocked,$questId);",
                cancellationToken,
                ("$heroId", heroId.ToString()),
                ("$key", trait),
                ("$unlocked", timestamp),
                ("$questId", questId.ToString())).ConfigureAwait(false);
        }

        foreach (var title in unlocks.TitlesUnlocked)
        {
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                "INSERT INTO hero_titles(hero_id,title_key,unlocked_at_utc,source_quest_id) VALUES($heroId,$key,$unlocked,$questId);",
                cancellationToken,
                ("$heroId", heroId.ToString()),
                ("$key", title.Key),
                ("$unlocked", FormatTimestamp(title.UnlockedAtUtc)),
                ("$questId", questId.ToString())).ConfigureAwait(false);
        }
    }

    private static async Task PersistMilestonesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string reportId,
        IReadOnlyList<MilestoneSummary> milestones,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < milestones.Count; index++)
        {
            var milestone = milestones[index];
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                "INSERT INTO quest_milestones(quest_report_id,ordinal,event_key,semantic_key) VALUES($reportId,$ordinal,$event,$semantic);",
                cancellationToken,
                ("$reportId", reportId),
                ("$ordinal", index),
                ("$event", milestone.EventKey),
                ("$semantic", milestone.SemanticKey)).ConfigureAwait(false);
        }
    }

    private static FinishQuestResult ToFinishResult(
        QuestId questId,
        string result,
        FinishCalculation calculation,
        bool replayed,
        bool alreadyFinalized) =>
        new(
            questId,
            result,
            replayed,
            alreadyFinalized,
            new RewardSummary(
                calculation.Reward.BaseXp,
                calculation.Reward.BonusXp,
                calculation.Reward.PenaltyXp,
                calculation.Reward.RawXp,
                calculation.Reward.OutcomePermille,
                calculation.Reward.QuestXp,
                calculation.Reward.RuleVersion,
                calculation.Reward.Components.Select(static component => new RewardComponentSummary(component.Key, component.Delta)).ToArray()),
            calculation.HeroProgress,
            new TrustStrainSummary(
                calculation.TrustStrain.TrustBefore,
                calculation.TrustStrain.TrustAfter,
                calculation.TrustStrain.StrainBefore,
                calculation.TrustStrain.StrainAfter,
                calculation.TrustStrain.RuleVersion,
                calculation.TrustStrain.Components.Select(static component => new TrustStrainComponentSummary(component.Key, component.TrustDelta, component.StrainDelta)).ToArray()),
            new StreakProgressSummary(calculation.Streak.Before, calculation.Streak.After, calculation.Streak.RuleVersion),
            calculation.SkillProgress,
            calculation.Unlocks.TraitsUnlocked,
            calculation.Unlocks.TitlesUnlocked.Select(static title => title.Key).ToArray(),
            calculation.Unlocks.ActiveTitle?.Key,
            calculation.Milestones);

    private static async Task<FinishQuestState> LoadFinishQuestStateAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        QuestId questId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            "SELECT hero_id,project_id,quest_type,status FROM quest_sessions WHERE id=$id;",
            ("$id", questId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new HeroPassportException("HP130", "Quest was not found.");
        }

        return new FinishQuestState(
            HeroId.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            string.Equals(reader.GetString(3), "finished", StringComparison.Ordinal));
    }

    private static async Task<FinishHeroState> LoadFinishHeroStateAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HeroId heroId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            "SELECT total_xp,trust,strain,success_streak FROM heroes WHERE id=$id;",
            ("$id", heroId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new HeroPassportException("HP140", "Hero was not found.");
        }

        return new FinishHeroState(
            reader.GetInt64(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3));
    }

    private static async Task<FinalizationHash> LoadFinalizationHashAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        QuestId questId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            "SELECT finalization_args_encoding_version,finalization_args_hash FROM quest_reports WHERE quest_id=$questId;",
            ("$questId", questId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new HeroPassportException("HP900", "Finalized Quest report is unavailable.");
        }

        return new FinalizationHash(reader.GetString(0), reader.GetFieldValue<byte[]>(1));
    }

    private static async Task<FinishQuestResult> LoadPersistedFinishAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        QuestId questId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            """
            SELECT q.hero_id,r.id,r.result,
                   r.base_xp,r.bonus_xp,r.penalty_xp,r.raw_xp,r.outcome_permille,r.xp_gained,r.reward_rule_version,
                   r.hero_total_xp_before,r.hero_total_xp_after,r.hero_level_before,r.hero_level_after,r.hero_progression_version,
                   r.rank_before,r.rank_after,
                   r.trust_before,r.trust_after,r.strain_before,r.strain_after,r.trust_strain_rule_version,
                   r.streak_before,r.streak_after,r.streak_rule_version,r.skill_progression_version,
                   r.active_title_after
            FROM quest_reports r
            JOIN quest_sessions q ON q.id=r.quest_id
            WHERE r.quest_id=$questId;
            """,
            ("$questId", questId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new HeroPassportException("HP900", "Finalized Quest report is unavailable.");
        }

        var heroId = HeroId.Parse(reader.GetString(0));
        var reportId = reader.GetString(1);
        var result = reader.GetString(2);
        var rewardComponents = await LoadPersistedRewardComponentsAsync(connection, transaction, reportId, cancellationToken).ConfigureAwait(false);
        var trustComponents = await LoadPersistedTrustStrainComponentsAsync(connection, transaction, reportId, cancellationToken).ConfigureAwait(false);
        var skillProgressionVersion = reader.GetString(25);
        var skillProgress = await LoadPersistedSkillProgressAsync(connection, transaction, reportId, skillProgressionVersion, cancellationToken).ConfigureAwait(false);
        var milestones = await LoadPersistedMilestonesAsync(connection, transaction, reportId, cancellationToken).ConfigureAwait(false);
        var heroAfter = GetHeroProgressionState(reader.GetString(14), reader.GetInt64(11));
        var activeTitle = reader.IsDBNull(26) ? null : reader.GetString(26);

        return new FinishQuestResult(
            questId,
            result,
            false,
            true,
            new RewardSummary(
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                reader.GetInt32(8),
                reader.GetString(9),
                rewardComponents),
            new HeroProgressSummary(
                heroId,
                reader.GetInt64(10),
                reader.GetInt64(11),
                reader.GetInt32(12),
                reader.GetInt32(13),
                heroAfter.IsLevelCapped,
                heroAfter.LevelXp,
                heroAfter.NextLevelXpRequired,
                reader.GetString(15),
                reader.GetString(16)),
            new TrustStrainSummary(
                reader.GetInt32(17),
                reader.GetInt32(18),
                reader.GetInt32(19),
                reader.GetInt32(20),
                reader.GetString(21),
                trustComponents),
            new StreakProgressSummary(reader.GetInt32(22), reader.GetInt32(23), reader.GetString(24)),
            skillProgress,
            milestones.Where(static milestone => string.Equals(milestone.EventKey, "trait.unlocked", StringComparison.Ordinal))
                .Select(static milestone => milestone.SemanticKey["trait.".Length..]).ToArray(),
            milestones.Where(static milestone => string.Equals(milestone.EventKey, "title.unlocked", StringComparison.Ordinal))
                .Select(static milestone => milestone.SemanticKey["title.".Length..]).ToArray(),
            activeTitle,
            milestones);
    }

    private static async Task<IReadOnlyList<RewardComponentSummary>> LoadPersistedRewardComponentsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string reportId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            "SELECT component_key,xp_delta FROM quest_reward_components WHERE quest_report_id=$reportId ORDER BY ordinal ASC;",
            ("$reportId", reportId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<RewardComponentSummary>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new RewardComponentSummary(reader.GetString(0), reader.GetInt32(1)));
        }

        return result;
    }

    private static async Task<IReadOnlyList<TrustStrainComponentSummary>> LoadPersistedTrustStrainComponentsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string reportId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            "SELECT component_key,trust_delta,strain_delta FROM quest_trust_strain_components WHERE quest_report_id=$reportId ORDER BY ordinal ASC;",
            ("$reportId", reportId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<TrustStrainComponentSummary>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new TrustStrainComponentSummary(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2)));
        }

        return result;
    }

    private static async Task<IReadOnlyList<SkillProgressSummary>> LoadPersistedSkillProgressAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string reportId,
        string progressionVersion,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            "SELECT skill_key,xp_gained,xp_before,xp_after,level_before,level_after FROM quest_report_skills WHERE quest_report_id=$reportId ORDER BY ordinal ASC;",
            ("$reportId", reportId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<SkillProgressSummary>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var xpAfter = reader.GetInt64(3);
            var after = GetSkillProgressionState(progressionVersion, xpAfter);
            result.Add(new SkillProgressSummary(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetInt64(2),
                xpAfter,
                reader.GetInt32(4),
                reader.GetInt32(5),
                after.IsLevelCapped,
                after.NextLevelXpRequired));
        }

        return result;
    }

    private static async Task<IReadOnlyList<MilestoneSummary>> LoadPersistedMilestonesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string reportId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            "SELECT event_key,semantic_key FROM quest_milestones WHERE quest_report_id=$reportId ORDER BY ordinal ASC;",
            ("$reportId", reportId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<MilestoneSummary>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new MilestoneSummary(reader.GetString(0), reader.GetString(1)));
        }

        return result;
    }

    private static ProgressionState GetHeroProgressionState(string version, long totalXp) =>
        string.Equals(version, HeroProgressionRules.RuleVersion, StringComparison.Ordinal)
            ? HeroProgressionRules.GetState(totalXp)
            : throw new HeroPassportException("HP900", "Stored Hero progression version is unsupported by this build.");

    private static ProgressionState GetSkillProgressionState(string version, long xp) =>
        string.Equals(version, SkillProgressionRules.RuleVersion, StringComparison.Ordinal)
            ? SkillProgressionRules.GetState(xp)
            : throw new HeroPassportException("HP900", "Stored Skill progression version is unsupported by this build.");

    private sealed record FinishQuestState(HeroId HeroId, string ProjectId, string QuestType, bool Finished);
    private sealed record FinishHeroState(long TotalXp, int Trust, int Strain, int SuccessStreak);
    private sealed record FinalizationHash(string EncodingVersion, byte[] ArgsHash);
}
