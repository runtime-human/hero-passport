using Microsoft.Data.Sqlite;
using System.Data;
using System.Globalization;

namespace HeroPassport.Infrastructure.Persistence;

public sealed record HeroPassportProjectionRebuildResult(
    int HeroesRebuilt,
    int HeroSkillsRebuilt,
    int HeroProjectStatsRebuilt,
    HeroPassportDatabaseDoctorReport Before,
    HeroPassportDatabaseDoctorReport After);

public static class HeroPassportProjectionRebuilder
{
    private static readonly HeroProjectionState InitialHeroState = new(0, 50, 20, 0);

    public static async Task<HeroPassportProjectionRebuildResult> RebuildAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var fullPath = Path.GetFullPath(databasePath);
        var before = await HeroPassportDatabaseDoctor
            .InspectAsync(fullPath, cancellationToken)
            .ConfigureAwait(false);
        EnsurePreconditions(before);

        int heroesRebuilt;
        int heroSkillsRebuilt;
        int heroProjectStatsRebuilt;

        await using (var connection = await HeroPassportDatabase
            .OpenConnectionAsync(fullPath, cancellationToken)
            .ConfigureAwait(false))
        await using (var transaction = connection.BeginTransaction(
            IsolationLevel.Serializable,
            deferred: false))
        {
            var heroIds = await ReadHeroIdsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            var quests = await ReadQuestsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            var reports = await ReadReportsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            var skills = await ReadSkillHistoryAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

            ValidateCanonicalQuestHistory(quests, reports);
            var heroStates = BuildHeroStates(heroIds, reports);
            var skillStates = BuildSkillStates(skills);
            var projectStates = BuildProjectStates(quests, reports);
            ValidateHeroXpAgainstEvents(heroStates, reports);

            await ReplaceProjectionsAsync(
                connection,
                transaction,
                heroStates,
                skillStates,
                projectStates,
                cancellationToken).ConfigureAwait(false);
            await ValidateIntegrityInsideTransactionAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            heroesRebuilt = heroStates.Count;
            heroSkillsRebuilt = skillStates.Count;
            heroProjectStatsRebuilt = projectStates.Count;
        }

        var after = await HeroPassportDatabaseDoctor
            .InspectAsync(fullPath, cancellationToken)
            .ConfigureAwait(false);
        EnsurePostconditions(after);

        return new HeroPassportProjectionRebuildResult(
            heroesRebuilt,
            heroSkillsRebuilt,
            heroProjectStatsRebuilt,
            before,
            after);
    }

    private static void EnsurePreconditions(HeroPassportDatabaseDoctorReport report)
    {
        if (!report.DatabaseExists)
        {
            throw new InvalidOperationException("Hero Passport database is not initialized.");
        }

        if (!report.Healthy ||
            !string.Equals(report.MigrationState, "current", StringComparison.Ordinal) ||
            report.MigrationLockSuspected ||
            !report.QuickCheckPassed ||
            report.ForeignKeyViolationCount != 0)
        {
            throw new InvalidOperationException(
                "Projection rebuild refused because database policy, migration state, or integrity checks are unsafe.");
        }
    }

    private static void EnsurePostconditions(HeroPassportDatabaseDoctorReport report)
    {
        if (!report.Healthy ||
            !string.Equals(report.MigrationState, "current", StringComparison.Ordinal) ||
            report.MigrationLockSuspected ||
            !report.QuickCheckPassed ||
            report.ForeignKeyViolationCount != 0)
        {
            throw new InvalidOperationException(
                "Projection rebuild committed but post-rebuild database validation failed.");
        }
    }

    private static async Task<List<string>> ReadHeroIdsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, "SELECT id FROM heroes ORDER BY id;");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var heroIds = new List<string>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            heroIds.Add(reader.GetString(0));
        }

        return heroIds;
    }

    private static async Task<List<QuestProjectionRow>> ReadQuestsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            """
            SELECT id,hero_id,project_id,status,started_at_utc,finished_at_utc
            FROM quest_sessions
            ORDER BY id;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<QuestProjectionRow>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new QuestProjectionRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return rows;
    }

    private static async Task<List<ReportProjectionRow>> ReadReportsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            """
            SELECT r.id,r.quest_id,q.hero_id,q.project_id,r.result,
                   r.xp_gained,r.hero_total_xp_before,r.hero_total_xp_after,
                   r.trust_before,r.trust_after,r.strain_before,r.strain_after,
                   r.streak_before,r.streak_after,r.created_at_utc,
                   x.xp_delta
            FROM quest_reports AS r
            INNER JOIN quest_sessions AS q ON q.id=r.quest_id
            LEFT JOIN xp_events AS x ON x.quest_id=r.quest_id
            ORDER BY r.id;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<ReportProjectionRow>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (reader.IsDBNull(15))
            {
                throw new InvalidOperationException("Canonical finished Quest history is missing its XP event.");
            }

            var xpGained = reader.GetInt64(5);
            var xpDelta = reader.GetInt64(15);
            if (xpGained != xpDelta)
            {
                throw new InvalidOperationException("Canonical Quest report and XP event disagree.");
            }

            rows.Add(new ReportProjectionRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                xpDelta,
                new HeroProjectionState(
                    reader.GetInt64(6),
                    reader.GetInt32(8),
                    reader.GetInt32(10),
                    reader.GetInt64(12)),
                new HeroProjectionState(
                    reader.GetInt64(7),
                    reader.GetInt32(9),
                    reader.GetInt32(11),
                    reader.GetInt64(13)),
                reader.GetString(14)));
        }

        return rows;
    }

    private static async Task<List<SkillProjectionHistoryRow>> ReadSkillHistoryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            """
            SELECT q.hero_id,s.skill_key,s.xp_before,s.xp_after,r.created_at_utc,r.id,s.ordinal
            FROM quest_report_skills AS s
            INNER JOIN quest_reports AS r ON r.id=s.quest_report_id
            INNER JOIN quest_sessions AS q ON q.id=r.quest_id
            ORDER BY q.hero_id,s.skill_key,r.id,s.ordinal;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<SkillProjectionHistoryRow>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new SkillProjectionHistoryRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2),
                reader.GetInt64(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetInt32(6)));
        }

        return rows;
    }

    private static void ValidateCanonicalQuestHistory(
        List<QuestProjectionRow> quests,
        List<ReportProjectionRow> reports)
    {
        var reportsByQuest = reports.ToDictionary(static report => report.QuestId, StringComparer.Ordinal);
        foreach (var quest in quests)
        {
            var finished = string.Equals(quest.Status, "finished", StringComparison.Ordinal);
            var hasReport = reportsByQuest.TryGetValue(quest.QuestId, out var report);
            if (finished != hasReport)
            {
                throw new InvalidOperationException(
                    "Canonical Quest status and report history are inconsistent.");
            }

            if (report is not null &&
                (!string.Equals(report.HeroId, quest.HeroId, StringComparison.Ordinal) ||
                 !string.Equals(report.ProjectId, quest.ProjectId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Canonical Quest report ownership is inconsistent.");
            }
        }
    }

    private static List<HeroProjectionTarget> BuildHeroStates(
        List<string> heroIds,
        List<ReportProjectionRow> reports)
    {
        var targets = new List<HeroProjectionTarget>(heroIds.Count);
        foreach (var heroId in heroIds)
        {
            var remaining = reports
                .Where(report => string.Equals(report.HeroId, heroId, StringComparison.Ordinal))
                .ToList();
            var state = InitialHeroState;

            while (remaining.Count > 0)
            {
                var candidates = remaining.Where(report => report.Before == state).ToList();
                if (candidates.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Canonical Hero progression history has a discontinuity.");
                }

                foreach (var noOp in candidates.Where(report => report.After == state).ToArray())
                {
                    remaining.Remove(noOp);
                }

                var changing = candidates.Where(report => report.After != state).ToList();
                if (changing.Count > 1)
                {
                    throw new InvalidOperationException(
                        "Canonical Hero progression history is ambiguous.");
                }

                if (changing.Count == 1)
                {
                    var next = changing[0];
                    state = next.After;
                    remaining.Remove(next);
                }
                else if (remaining.Count > 0 && candidates.All(report => report.After == report.Before))
                {
                    continue;
                }
            }

            targets.Add(new HeroProjectionTarget(heroId, state));
        }

        return targets;
    }

    private static List<SkillProjectionTarget> BuildSkillStates(List<SkillProjectionHistoryRow> history)
    {
        var targets = new List<SkillProjectionTarget>();
        foreach (var group in history.GroupBy(
            static row => (row.HeroId, row.SkillKey)))
        {
            var remaining = group.ToList();
            long xp = 0;
            var updatedAtUtc = remaining[0].CreatedAtUtc;
            foreach (var row in remaining)
            {
                updatedAtUtc = MaxTimestamp(updatedAtUtc, row.CreatedAtUtc);
            }

            while (remaining.Count > 0)
            {
                var candidates = remaining.Where(row => row.XpBefore == xp).ToList();
                if (candidates.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Canonical Skill progression history has a discontinuity.");
                }

                foreach (var noOp in candidates.Where(row => row.XpAfter == xp).ToArray())
                {
                    remaining.Remove(noOp);
                }

                var changing = candidates.Where(row => row.XpAfter != xp).ToList();
                if (changing.Count > 1)
                {
                    throw new InvalidOperationException(
                        "Canonical Skill progression history is ambiguous.");
                }

                if (changing.Count == 1)
                {
                    var next = changing[0];
                    xp = next.XpAfter;
                    remaining.Remove(next);
                }
                else if (remaining.Count > 0 && candidates.All(row => row.XpAfter == row.XpBefore))
                {
                    continue;
                }
            }

            targets.Add(new SkillProjectionTarget(
                group.Key.HeroId,
                group.Key.SkillKey,
                xp,
                updatedAtUtc));
        }

        return targets;
    }

    private static List<ProjectProjectionTarget> BuildProjectStates(
        List<QuestProjectionRow> quests,
        List<ReportProjectionRow> reports)
    {
        var reportByQuest = reports.ToDictionary(static report => report.QuestId, StringComparer.Ordinal);
        var targets = new List<ProjectProjectionTarget>();

        foreach (var group in quests.GroupBy(static quest => (quest.HeroId, quest.ProjectId)))
        {
            long started = 0;
            long finished = 0;
            long succeeded = 0;
            long totalXp = 0;
            string? lastQuestAtUtc = null;

            foreach (var quest in group)
            {
                started = checked(started + 1);
                lastQuestAtUtc = MaxTimestamp(lastQuestAtUtc, quest.StartedAtUtc);
                if (!string.IsNullOrWhiteSpace(quest.FinishedAtUtc))
                {
                    lastQuestAtUtc = MaxTimestamp(lastQuestAtUtc, quest.FinishedAtUtc);
                }

                if (!reportByQuest.TryGetValue(quest.QuestId, out var report))
                {
                    continue;
                }

                finished = checked(finished + 1);
                if (string.Equals(report.Result, "success", StringComparison.Ordinal))
                {
                    succeeded = checked(succeeded + 1);
                }

                totalXp = checked(totalXp + report.XpDelta);
            }

            targets.Add(new ProjectProjectionTarget(
                group.Key.HeroId,
                group.Key.ProjectId,
                started,
                finished,
                succeeded,
                totalXp,
                lastQuestAtUtc));
        }

        return targets;
    }

    private static void ValidateHeroXpAgainstEvents(
        List<HeroProjectionTarget> heroes,
        List<ReportProjectionRow> reports)
    {
        foreach (var hero in heroes)
        {
            var eventTotal = reports
                .Where(report => string.Equals(report.HeroId, hero.HeroId, StringComparison.Ordinal))
                .Aggregate(0L, static (total, report) => checked(total + report.XpDelta));
            if (eventTotal != hero.State.TotalXp)
            {
                throw new InvalidOperationException(
                    "Canonical Hero XP history does not match persisted progression snapshots.");
            }
        }
    }

    private static async Task ReplaceProjectionsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        List<HeroProjectionTarget> heroes,
        List<SkillProjectionTarget> skills,
        List<ProjectProjectionTarget> projects,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, transaction, "DELETE FROM hero_skills;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "DELETE FROM hero_project_stats;", cancellationToken).ConfigureAwait(false);

        foreach (var hero in heroes)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                UPDATE heroes
                SET total_xp=$xp,trust=$trust,strain=$strain,success_streak=$streak
                WHERE id=$hero;
                """,
                cancellationToken,
                ("$xp", hero.State.TotalXp),
                ("$trust", hero.State.Trust),
                ("$strain", hero.State.Strain),
                ("$streak", hero.State.SuccessStreak),
                ("$hero", hero.HeroId)).ConfigureAwait(false);
        }

        foreach (var skill in skills)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO hero_skills(hero_id,skill_key,xp,updated_at_utc)
                VALUES($hero,$skill,$xp,$time);
                """,
                cancellationToken,
                ("$hero", skill.HeroId),
                ("$skill", skill.SkillKey),
                ("$xp", skill.Xp),
                ("$time", skill.UpdatedAtUtc)).ConfigureAwait(false);
        }

        foreach (var project in projects)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO hero_project_stats(
                    hero_id,project_id,quests_started,quests_finished,quests_succeeded,total_xp_earned,last_quest_at_utc)
                VALUES($hero,$project,$started,$finished,$succeeded,$xp,$last);
                """,
                cancellationToken,
                ("$hero", project.HeroId),
                ("$project", project.ProjectId),
                ("$started", project.QuestsStarted),
                ("$finished", project.QuestsFinished),
                ("$succeeded", project.QuestsSucceeded),
                ("$xp", project.TotalXpEarned),
                ("$last", project.LastQuestAtUtc)).ConfigureAwait(false);
        }
    }

    private static async Task ValidateIntegrityInsideTransactionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using (var quickCheck = CreateCommand(connection, transaction, "PRAGMA quick_check;"))
        {
            var value = Convert.ToString(
                await quickCheck.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                CultureInfo.InvariantCulture);
            if (!string.Equals(value, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Projection rebuild failed SQLite quick_check before commit.");
            }
        }

        await using var foreignKeyCheck = CreateCommand(connection, transaction, "PRAGMA foreign_key_check;");
        await using var reader = await foreignKeyCheck.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Projection rebuild failed SQLite foreign_key_check before commit.");
        }
    }

    private static SqliteCommand CreateCommand(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        return command;
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = CreateCommand(connection, transaction, sql, parameters);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string MaxTimestamp(string? current, string candidate) =>
        current is null || string.CompareOrdinal(candidate, current) > 0 ? candidate : current;

    private sealed record HeroProjectionState(long TotalXp, int Trust, int Strain, long SuccessStreak);

    private sealed record HeroProjectionTarget(string HeroId, HeroProjectionState State);

    private sealed record QuestProjectionRow(
        string QuestId,
        string HeroId,
        string ProjectId,
        string Status,
        string StartedAtUtc,
        string? FinishedAtUtc);

    private sealed record ReportProjectionRow(
        string ReportId,
        string QuestId,
        string HeroId,
        string ProjectId,
        string Result,
        long XpDelta,
        HeroProjectionState Before,
        HeroProjectionState After,
        string CreatedAtUtc);

    private sealed record SkillProjectionHistoryRow(
        string HeroId,
        string SkillKey,
        long XpBefore,
        long XpAfter,
        string CreatedAtUtc,
        string ReportId,
        int Ordinal);

    private sealed record SkillProjectionTarget(
        string HeroId,
        string SkillKey,
        long Xp,
        string UpdatedAtUtc);

    private sealed record ProjectProjectionTarget(
        string HeroId,
        string ProjectId,
        long QuestsStarted,
        long QuestsFinished,
        long QuestsSucceeded,
        long TotalXpEarned,
        string? LastQuestAtUtc);
}
