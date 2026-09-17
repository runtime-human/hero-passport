using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    public async Task<ProjectQuestHistoryResult> GetProjectQuestHistoryAsync(
        ProjectBindingContext project,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase
            .OpenConnectionAsync(_databasePath, cancellationToken)
            .ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(deferred: true);
        var existingProject = await FindHistoryProjectAsync(
                connection,
                transaction,
                project.WorkspaceFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (existingProject is null)
        {
            transaction.Commit();
            return new ProjectQuestHistoryResult(
                project.DisplayName,
                Array.Empty<ProjectQuestHistoryItem>());
        }

        var items = await ProjectQuestHistoryItemsAsync(
                connection,
                transaction,
                existingProject.Id,
                cancellationToken)
            .ConfigureAwait(false);
        transaction.Commit();
        return new ProjectQuestHistoryResult(existingProject.DisplayName, items);
    }

    public async Task<QuestHistoryDetailResult?> GetQuestHistoryDetailAsync(
        QuestId questId,
        ProjectBindingContext project,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase
            .OpenConnectionAsync(_databasePath, cancellationToken)
            .ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(deferred: true);
        var existingProject = await FindHistoryProjectAsync(
                connection,
                transaction,
                project.WorkspaceFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (existingProject is null)
        {
            transaction.Commit();
            return null;
        }

        var row = await QuestHistoryDetailRowAsync(
                connection,
                transaction,
                questId,
                existingProject.Id,
                cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            transaction.Commit();
            return null;
        }

        QuestHistoryReport? report = null;
        if (row.Report is { } reportRow)
        {
            var skills = await QuestHistorySkillsAsync(
                    connection,
                    transaction,
                    reportRow.ReportId,
                    cancellationToken)
                .ConfigureAwait(false);
            report = new QuestHistoryReport(
                reportRow.Result,
                reportRow.Summary,
                reportRow.TestsMentioned,
                reportRow.ScopeViolations,
                reportRow.UserCorrections,
                reportRow.BuildStatus,
                reportRow.BuildEvidence,
                reportRow.TestsStatus,
                reportRow.TestsEvidence,
                reportRow.XpGained,
                skills);
        }

        transaction.Commit();
        return new QuestHistoryDetailResult(
            row.QuestId,
            row.HeroName,
            existingProject.DisplayName,
            row.QuestType,
            row.Title,
            row.Goal,
            row.Status,
            row.StartedAtUtc,
            row.FinishedAtUtc,
            report);
    }

    private static async Task<HistoryProjectRow?> FindHistoryProjectAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string workspaceFingerprint,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            "SELECT id,display_name FROM projects WHERE workspace_fingerprint=$fingerprint LIMIT 1;",
            ("$fingerprint", workspaceFingerprint));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new HistoryProjectRow(reader.GetString(0), reader.GetString(1));
    }

    private static async Task<ProjectQuestHistoryItem[]> ProjectQuestHistoryItemsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string projectId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            """
            SELECT
                q.id,
                h.name,
                q.quest_type,
                q.title,
                q.status,
                r.result,
                r.xp_gained,
                q.started_at_utc,
                q.finished_at_utc
            FROM quest_sessions q
            JOIN heroes h ON h.id = q.hero_id
            LEFT JOIN quest_reports r ON r.quest_id = q.id
            WHERE q.project_id = $project
            ORDER BY q.started_at_utc DESC, q.id DESC
            LIMIT 25;
            """,
            ("$project", projectId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var items = new List<ProjectQuestHistoryItem>(25);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(new ProjectQuestHistoryItem(
                QuestId.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetInt64(6),
                ParseHistoryUtc(reader.GetString(7)),
                reader.IsDBNull(8) ? null : ParseHistoryUtc(reader.GetString(8))));
        }

        return items.ToArray();
    }

    private static async Task<QuestHistoryDetailRow?> QuestHistoryDetailRowAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        QuestId questId,
        string projectId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            """
            SELECT
                q.id,
                h.name,
                q.quest_type,
                q.title,
                q.goal,
                q.status,
                q.started_at_utc,
                q.finished_at_utc,
                r.id,
                r.result,
                r.summary,
                r.tests_mentioned,
                r.scope_violations,
                r.user_corrections,
                r.build_status,
                r.build_evidence,
                r.tests_status,
                r.tests_evidence,
                r.xp_gained
            FROM quest_sessions q
            JOIN heroes h ON h.id = q.hero_id
            LEFT JOIN quest_reports r ON r.quest_id = q.id
            WHERE q.id = $quest AND q.project_id = $project
            LIMIT 1;
            """,
            ("$quest", questId.ToString()),
            ("$project", projectId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        QuestHistoryReportRow? report = null;
        if (!reader.IsDBNull(8))
        {
            report = new QuestHistoryReportRow(
                reader.GetString(8),
                reader.GetString(9),
                reader.GetString(10),
                reader.GetInt64(11) != 0,
                reader.GetInt32(12),
                reader.GetInt32(13),
                reader.GetString(14),
                reader.GetString(15),
                reader.GetString(16),
                reader.GetString(17),
                reader.GetInt64(18));
        }

        return new QuestHistoryDetailRow(
            QuestId.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            ParseHistoryUtc(reader.GetString(6)),
            reader.IsDBNull(7) ? null : ParseHistoryUtc(reader.GetString(7)),
            report);
    }

    private static async Task<string[]> QuestHistorySkillsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string reportId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            """
            SELECT skill_key
            FROM quest_report_skills
            WHERE quest_report_id = $report
            ORDER BY ordinal
            LIMIT 3;
            """,
            ("$report", reportId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var skills = new List<string>(3);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            skills.Add(reader.GetString(0));
        }

        return skills.ToArray();
    }

    private static DateTimeOffset ParseHistoryUtc(string value) =>
        DateTimeOffset.ParseExact(
            value,
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    private sealed record HistoryProjectRow(string Id, string DisplayName);

    private sealed record QuestHistoryDetailRow(
        QuestId QuestId,
        string HeroName,
        string QuestType,
        string Title,
        string Goal,
        string Status,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset? FinishedAtUtc,
        QuestHistoryReportRow? Report);

    private sealed record QuestHistoryReportRow(
        string ReportId,
        string Result,
        string Summary,
        bool TestsMentioned,
        int ScopeViolations,
        int UserCorrections,
        string BuildStatus,
        string BuildEvidence,
        string TestsStatus,
        string TestsEvidence,
        long XpGained);
}
