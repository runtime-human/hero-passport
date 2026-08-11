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

        var reward = MinimalQuestReward.Calculate(quest.QuestType, command.Result);
        var heroState = await LoadFinishHeroStateAsync(
            connection,
            transaction,
            quest.HeroId,
            cancellationToken).ConfigureAwait(false);
        var totalXpAfter = JsonSafeInteger.Require(checked(heroState.TotalXp + reward.XpGained));
        var timestamp = FormatTimestamp(now);
        var reportId = Guid.CreateVersion7().ToString("D");
        var xpEventId = Guid.CreateVersion7().ToString("D");

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
                $baseXp,0,0,$rawXp,$permille,$xpGained,
                $xpBefore,$xpAfter,1,1,
                'code_squire','code_squire',$trust,$trust,$strain,$strain,
                $streakBefore,$streakBefore,NULL,NULL,$created);
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
            ("$rewardVersion", reward.RuleVersion),
            ("$heroProgression", HeroPassportVersions.CurrentRules.HeroProgression),
            ("$skillProgression", HeroPassportVersions.CurrentRules.SkillProgression),
            ("$skillAllocation", HeroPassportVersions.CurrentRules.SkillAllocation),
            ("$trustStrain", HeroPassportVersions.CurrentRules.TrustStrain),
            ("$streak", HeroPassportVersions.CurrentRules.Streak),
            ("$unlock", HeroPassportVersions.CurrentRules.Unlock),
            ("$rankVersion", HeroPassportVersions.CurrentRules.Rank),
            ("$baseXp", reward.BaseXp),
            ("$rawXp", reward.RawXp),
            ("$permille", reward.OutcomePermille),
            ("$xpGained", reward.XpGained),
            ("$xpBefore", heroState.TotalXp),
            ("$xpAfter", totalXpAfter),
            ("$trust", heroState.Trust),
            ("$strain", heroState.Strain),
            ("$streakBefore", heroState.SuccessStreak),
            ("$created", timestamp)).ConfigureAwait(false);

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            "INSERT INTO xp_events(id,quest_id,hero_id,project_id,xp_delta,reward_rule_version,created_at_utc) VALUES($id,$questId,$heroId,$projectId,$xp,$rule,$created);",
            cancellationToken,
            ("$id", xpEventId),
            ("$questId", command.QuestId.ToString()),
            ("$heroId", quest.HeroId.ToString()),
            ("$projectId", quest.ProjectId),
            ("$xp", reward.XpGained),
            ("$rule", reward.RuleVersion),
            ("$created", timestamp)).ConfigureAwait(false);

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            "UPDATE heroes SET total_xp=$xp,updated_at_utc=$updated WHERE id=$heroId;",
            cancellationToken,
            ("$xp", totalXpAfter),
            ("$updated", timestamp),
            ("$heroId", quest.HeroId.ToString())).ConfigureAwait(false);

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            "UPDATE hero_project_stats SET quests_finished=quests_finished+1,quests_succeeded=quests_succeeded+$success,total_xp_earned=total_xp_earned+$xp,last_quest_at_utc=$updated WHERE hero_id=$heroId AND project_id=$projectId;",
            cancellationToken,
            ("$success", string.Equals(command.Result, "success", StringComparison.Ordinal) ? 1 : 0),
            ("$xp", reward.XpGained),
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
        return new FinishQuestResult(
            command.QuestId,
            command.Result,
            false,
            false,
            new RewardSummary(
                reward.BaseXp,
                reward.BonusXp,
                reward.PenaltyXp,
                reward.RawXp,
                reward.OutcomePermille,
                reward.XpGained,
                reward.RuleVersion),
            new HeroProgressSummary(
                quest.HeroId,
                heroState.TotalXp,
                totalXpAfter,
                1,
                1,
                "code_squire",
                "code_squire"));
    }

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
            SELECT q.hero_id,r.result,r.base_xp,r.bonus_xp,r.penalty_xp,r.raw_xp,r.outcome_permille,r.xp_gained,
                   r.reward_rule_version,r.hero_total_xp_before,r.hero_total_xp_after,r.hero_level_before,r.hero_level_after,
                   r.rank_before,r.rank_after
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

        return new FinishQuestResult(
            questId,
            reader.GetString(1),
            false,
            true,
            new RewardSummary(
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                reader.GetString(8)),
            new HeroProgressSummary(
                HeroId.Parse(reader.GetString(0)),
                reader.GetInt64(9),
                reader.GetInt64(10),
                reader.GetInt32(11),
                reader.GetInt32(12),
                reader.GetString(13),
                reader.GetString(14)));
    }

    private sealed record FinishQuestState(HeroId HeroId, string ProjectId, string QuestType, bool Finished);
    private sealed record FinishHeroState(long TotalXp, int Trust, int Strain, int SuccessStreak);
    private sealed record FinalizationHash(string EncodingVersion, byte[] ArgsHash);
}
