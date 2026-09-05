using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Engine;
using HeroPassport.Domain.Primitives;
using Microsoft.Data.Sqlite;
using System.Data;
using System.Security.Cryptography;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    public async Task<FinishQuestResult> FinishQuestAsync(
        FinishQuestStoreCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);

        var projectId = await ExistingProjectIdAsync(connection, transaction, command.Project.WorkspaceFingerprint, cancellationToken).ConfigureAwait(false);
        if (projectId is null)
        {
            throw new HeroPassportException("HP134", "Quest does not belong to the current Project.");
        }

        var requestId = command.RequestId.ToString();
        var receipt = await ReceiptAsync(connection, transaction, "finish_quest", requestId, cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            EnsureReceipt(receipt, command.ArgsEncodingVersion, command.ArgsHash);
            var replayQuest = await QuestForFinishAsync(connection, transaction, command.QuestId, cancellationToken).ConfigureAwait(false);
            EnsureProject(replayQuest, projectId.Value);
            var replayReport = await ReportForQuestAsync(connection, transaction, command.QuestId, cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            return ResultFromReport(replayReport, replayed: true, alreadyFinalized: false);
        }

        var quest = await QuestForFinishAsync(connection, transaction, command.QuestId, cancellationToken).ConfigureAwait(false);
        EnsureProject(quest, projectId.Value);

        if (string.Equals(quest.Status, "finished", StringComparison.Ordinal))
        {
            var existingReport = await ReportForQuestAsync(connection, transaction, command.QuestId, cancellationToken).ConfigureAwait(false);
            if (!FinalizationMatches(existingReport, command.ArgsEncodingVersion, command.ArgsHash))
            {
                throw new HeroPassportException("HP136", "Quest was already finalized with different arguments.");
            }

            await InsertFinishReceiptAsync(
                connection,
                transaction,
                requestId,
                command.ArgsEncodingVersion,
                command.ArgsHash,
                command.QuestId,
                quest.HeroId,
                projectId.Value,
                Timestamp(now),
                cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            return ResultFromReport(existingReport, replayed: false, alreadyFinalized: true);
        }

        var settings = await SettingsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        if (!settings.SetupCompleted)
        {
            throw new HeroPassportException("HP001", "Setup is required.");
        }

        var hero = await HeroProgressRowAsync(connection, transaction, quest.HeroId, cancellationToken).ConfigureAwait(false);
        var baseXp = MinimalQuestFinishRules.BaseXp(quest.QuestType);
        var outcomePermille = MinimalQuestFinishRules.OutcomePermille(command.Result);
        var xpGained = MinimalQuestFinishRules.QuestXp(baseXp, outcomePermille);
        var totalXpAfter = JsonSafeInteger.Require(checked(hero.TotalXp + xpGained));
        var levelBefore = MinimalQuestFinishRules.HeroLevel(hero.TotalXp);
        var levelAfter = MinimalQuestFinishRules.HeroLevel(totalXpAfter);
        var rankBefore = MinimalQuestFinishRules.RankKey(levelBefore);
        var rankAfter = MinimalQuestFinishRules.RankKey(levelAfter);
        var rules = HeroPassportVersions.CurrentRules;
        var timestamp = Timestamp(now);
        var reportId = QuestReportId.New();

        await ExecuteAsync(
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
                $id,$quest,$result,$summary,$testsMentioned,$scopeViolations,$userCorrections,
                $buildStatus,$buildEvidence,$testsStatus,$testsEvidence,
                $encoding,$hash,
                $rewardVersion,$heroProgressionVersion,$skillProgressionVersion,$skillAllocationVersion,
                $trustStrainVersion,$streakVersion,$unlockVersion,$rankVersion,
                $baseXp,0,0,$baseXp,$outcomePermille,$xpGained,
                $totalBefore,$totalAfter,$levelBefore,$levelAfter,
                $rankBefore,$rankAfter,$trust,$trust,$strain,$strain,
                $streak,$streak,NULL,NULL,$time);
            """,
            cancellationToken,
            ("$id", reportId.ToString()),
            ("$quest", command.QuestId.ToString()),
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
            ("$rewardVersion", rules.Reward),
            ("$heroProgressionVersion", rules.HeroProgression),
            ("$skillProgressionVersion", rules.SkillProgression),
            ("$skillAllocationVersion", rules.SkillAllocation),
            ("$trustStrainVersion", rules.TrustStrain),
            ("$streakVersion", rules.Streak),
            ("$unlockVersion", rules.Unlock),
            ("$rankVersion", rules.Rank),
            ("$baseXp", baseXp),
            ("$outcomePermille", outcomePermille),
            ("$xpGained", xpGained),
            ("$totalBefore", hero.TotalXp),
            ("$totalAfter", totalXpAfter),
            ("$levelBefore", levelBefore),
            ("$levelAfter", levelAfter),
            ("$rankBefore", rankBefore),
            ("$rankAfter", rankAfter),
            ("$trust", hero.Trust),
            ("$strain", hero.Strain),
            ("$streak", hero.SuccessStreak),
            ("$time", timestamp)).ConfigureAwait(false);

        await ExecuteAsync(
            connection,
            transaction,
            "INSERT INTO xp_events(id,quest_id,hero_id,project_id,xp_delta,reward_rule_version,created_at_utc) VALUES($id,$quest,$hero,$project,$xp,$version,$time);",
            cancellationToken,
            ("$id", XpEventId.New().ToString()),
            ("$quest", command.QuestId.ToString()),
            ("$hero", quest.HeroId.ToString()),
            ("$project", projectId.Value.ToString()),
            ("$xp", xpGained),
            ("$version", rules.Reward),
            ("$time", timestamp)).ConfigureAwait(false);

        await InsertFinishReceiptAsync(
            connection,
            transaction,
            requestId,
            command.ArgsEncodingVersion,
            command.ArgsHash,
            command.QuestId,
            quest.HeroId,
            projectId.Value,
            timestamp,
            cancellationToken).ConfigureAwait(false);

        await ExecuteAsync(
            connection,
            transaction,
            "UPDATE heroes SET total_xp=$total,updated_at_utc=$time WHERE id=$hero;",
            cancellationToken,
            ("$total", totalXpAfter),
            ("$time", timestamp),
            ("$hero", quest.HeroId.ToString())).ConfigureAwait(false);

        await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE hero_project_stats
            SET quests_finished=quests_finished+1,
                quests_succeeded=quests_succeeded+$success,
                total_xp_earned=total_xp_earned+$xp,
                last_quest_at_utc=$time
            WHERE hero_id=$hero AND project_id=$project;
            """,
            cancellationToken,
            ("$success", string.Equals(command.Result, "success", StringComparison.Ordinal) ? 1 : 0),
            ("$xp", xpGained),
            ("$time", timestamp),
            ("$hero", quest.HeroId.ToString()),
            ("$project", projectId.Value.ToString())).ConfigureAwait(false);

        await ExecuteAsync(
            connection,
            transaction,
            "UPDATE quest_sessions SET status='finished',finished_at_utc=$time WHERE id=$quest AND status='open';",
            cancellationToken,
            ("$time", timestamp),
            ("$quest", command.QuestId.ToString())).ConfigureAwait(false);

        transaction.Commit();

        return new FinishQuestResult(
            command.QuestId,
            command.Result,
            new QuestRewardSnapshot(baseXp, 0, 0, baseXp, outcomePermille, xpGained, rules.Reward),
            new HeroProgressSnapshot(
                quest.HeroId,
                hero.TotalXp,
                totalXpAfter,
                levelBefore,
                levelAfter,
                rankBefore,
                rankAfter,
                rules.HeroProgression,
                rules.Rank),
            Replayed: false,
            AlreadyFinalized: false);
    }

    private static async Task<ProjectId?> ExistingProjectIdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string workspaceFingerprint,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            "SELECT id FROM projects WHERE workspace_fingerprint=$fingerprint;",
            ("$fingerprint", workspaceFingerprint));
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is string id
            ? ProjectId.Parse(id)
            : null;
    }

    private static async Task<FinishQuestRow> QuestForFinishAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        QuestId questId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            "SELECT hero_id,project_id,quest_type,status FROM quest_sessions WHERE id=$id;",
            ("$id", questId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new HeroPassportException("HP130", "Quest was not found.");
        }

        return new FinishQuestRow(
            HeroId.Parse(reader.GetString(0)),
            ProjectId.Parse(reader.GetString(1)),
            reader.GetString(2),
            reader.GetString(3));
    }

    private static void EnsureProject(FinishQuestRow quest, ProjectId projectId)
    {
        if (quest.ProjectId != projectId)
        {
            throw new HeroPassportException("HP134", "Quest does not belong to the current Project.");
        }
    }

    private static async Task<HeroProgressRow> HeroProgressRowAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HeroId heroId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            "SELECT total_xp,trust,strain,success_streak FROM heroes WHERE id=$id;",
            ("$id", heroId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new HeroPassportException("HP140", "Hero was not found.");
        }

        return new HeroProgressRow(reader.GetInt64(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt64(3));
    }

    private static async Task<FinishReportRow> ReportForQuestAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        QuestId questId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            """
            SELECT result,finalization_args_encoding_version,finalization_args_hash,
                   reward_rule_version,base_xp,bonus_xp,penalty_xp,raw_xp,outcome_permille,xp_gained,
                   hero_progression_version,rank_rule_version,hero_id,
                   hero_total_xp_before,hero_total_xp_after,hero_level_before,hero_level_after,rank_before,rank_after
            FROM quest_reports
            WHERE quest_id=$quest;
            """,
            ("$quest", questId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new HeroPassportException("HP130", "Quest finalization report was not found.");
        }

        return new FinishReportRow(
            questId,
            reader.GetString(0),
            reader.GetString(1),
            reader.GetFieldValue<byte[]>(2),
            reader.GetString(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.GetInt32(6),
            reader.GetInt32(7),
            reader.GetInt32(8),
            reader.GetInt64(9),
            reader.GetString(10),
            reader.GetString(11),
            HeroId.Parse(reader.GetString(12)),
            reader.GetInt64(13),
            reader.GetInt64(14),
            reader.GetInt32(15),
            reader.GetInt32(16),
            reader.GetString(17),
            reader.GetString(18));
    }

    private static bool FinalizationMatches(FinishReportRow report, string encodingVersion, byte[] hash) =>
        string.Equals(report.FinalizationEncodingVersion, encodingVersion, StringComparison.Ordinal) &&
        report.FinalizationHash.Length == hash.Length &&
        CryptographicOperations.FixedTimeEquals(report.FinalizationHash, hash);

    private static async Task InsertFinishReceiptAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string requestId,
        string encodingVersion,
        byte[] hash,
        QuestId questId,
        HeroId heroId,
        ProjectId projectId,
        string timestamp,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            connection,
            transaction,
            "INSERT INTO mutation_receipts(operation_key,request_id,args_encoding_version,args_hash,result_kind,result_entity_id,project_id,hero_id,result_status,effective_at_utc) VALUES('finish_quest',$request,$encoding,$hash,'quest_finish',$quest,$project,$hero,'active',$time);",
            cancellationToken,
            ("$request", requestId),
            ("$encoding", encodingVersion),
            ("$hash", hash),
            ("$quest", questId.ToString()),
            ("$project", projectId.ToString()),
            ("$hero", heroId.ToString()),
            ("$time", timestamp)).ConfigureAwait(false);
    }

    private static FinishQuestResult ResultFromReport(FinishReportRow report, bool replayed, bool alreadyFinalized) =>
        new(
            report.QuestId,
            report.Result,
            new QuestRewardSnapshot(
                report.BaseXp,
                report.BonusXp,
                report.PenaltyXp,
                report.RawXp,
                report.OutcomePermille,
                report.XpGained,
                report.RewardRuleVersion),
            new HeroProgressSnapshot(
                report.HeroId,
                report.TotalXpBefore,
                report.TotalXpAfter,
                report.LevelBefore,
                report.LevelAfter,
                report.RankBefore,
                report.RankAfter,
                report.HeroProgressionVersion,
                report.RankRuleVersion),
            replayed,
            alreadyFinalized);

    private sealed record FinishQuestRow(HeroId HeroId, ProjectId ProjectId, string QuestType, string Status);
    private sealed record HeroProgressRow(long TotalXp, int Trust, int Strain, long SuccessStreak);
    private sealed record FinishReportRow(
        QuestId QuestId,
        string Result,
        string FinalizationEncodingVersion,
        byte[] FinalizationHash,
        string RewardRuleVersion,
        int BaseXp,
        int BonusXp,
        int PenaltyXp,
        int RawXp,
        int OutcomePermille,
        long XpGained,
        string HeroProgressionVersion,
        string RankRuleVersion,
        HeroId HeroId,
        long TotalXpBefore,
        long TotalXpAfter,
        int LevelBefore,
        int LevelAfter,
        string RankBefore,
        string RankAfter);
}
