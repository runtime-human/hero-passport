using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using Microsoft.Data.Sqlite;
using System.Data;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    public async Task<StartQuestResult> StartQuestAsync(
        StartQuestStoreCommand command,
        ProjectBindingContext project,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);

        var receipt = await GetReceiptAsync(connection, transaction, "start_quest", command.RequestId.ToString(), cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            if (receipt.ProjectId is null || receipt.HeroId is null ||
                !string.Equals(receipt.HeroId, command.HeroId.ToString(), StringComparison.Ordinal))
            {
                throw new HeroPassportException("HP135", "The mutation request ID was already used with different context or arguments.");
            }

            var currentProjectId = await FindProjectIdAsync(connection, transaction, project.WorkspaceFingerprint, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(currentProjectId, receipt.ProjectId, StringComparison.Ordinal))
            {
                throw new HeroPassportException("HP135", "The mutation request ID was already used with different context or arguments.");
            }

            var hash = CanonicalMutationEncoder.HashStartQuest(
                ProjectId.Parse(receipt.ProjectId),
                command.HeroId,
                command.QuestType,
                command.Title,
                command.Goal);
            EnsureReceiptMatches(receipt, HeroPassportVersions.MutationArgsVersion, hash);
            var quest = await GetQuestRequiredAsync(connection, transaction, receipt.ResultEntityId, cancellationToken).ConfigureAwait(false);
            var hero = await GetHeroRequiredAsync(connection, transaction, receipt.HeroId, cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            return new StartQuestResult(quest, hero, true);
        }

        var settings = await GetSettingsRowAsync(connection, transaction, cancellationToken).ConfigureAwait(false)
            ?? throw new HeroPassportException("HP900", "Application settings are unavailable.");
        if (!settings.SetupCompleted)
        {
            throw new HeroPassportException("HP001", "Setup is required.");
        }

        var hero = await FindHeroAsync(connection, transaction, command.HeroId.ToString(), cancellationToken).ConfigureAwait(false)
            ?? throw new HeroPassportException("HP140", "Hero was not found.");
        if (hero.Archived)
        {
            throw new HeroPassportException("HP141", "Hero is archived.");
        }

        var projectIdText = await FindProjectIdAsync(connection, transaction, project.WorkspaceFingerprint, cancellationToken).ConfigureAwait(false);
        ProjectId projectId;
        var timestamp = FormatTimestamp(now);
        if (projectIdText is null)
        {
            projectId = ProjectId.New();
            projectIdText = projectId.ToString();
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                "INSERT INTO projects(id,display_name,workspace_fingerprint,identity_version,created_at_utc) VALUES($id,$display,$fingerprint,$identity,$created);",
                cancellationToken,
                ("$id", projectIdText),
                ("$display", project.DisplayName),
                ("$fingerprint", project.WorkspaceFingerprint),
                ("$identity", project.IdentityVersion),
                ("$created", timestamp)).ConfigureAwait(false);
        }
        else
        {
            projectId = ProjectId.Parse(projectIdText);
        }

        if (await HasOpenQuestAsync(connection, transaction, command.HeroId, projectId, cancellationToken).ConfigureAwait(false))
        {
            throw new HeroPassportException("HP133", "An open Quest already exists for this Hero and Project.");
        }

        var argsHash = CanonicalMutationEncoder.HashStartQuest(
            projectId,
            command.HeroId,
            command.QuestType,
            command.Title,
            command.Goal);
        var questId = QuestId.New();
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            "INSERT INTO quest_sessions(id,hero_id,project_id,quest_type,title,goal,locale,status,started_at_utc,finished_at_utc,created_at_utc) VALUES($id,$heroId,$projectId,$type,$title,$goal,$locale,'open',$started,NULL,$created);",
            cancellationToken,
            ("$id", questId.ToString()),
            ("$heroId", command.HeroId.ToString()),
            ("$projectId", projectIdText),
            ("$type", command.QuestType),
            ("$title", command.Title),
            ("$goal", command.Goal),
            ("$locale", settings.Locale),
            ("$started", timestamp),
            ("$created", timestamp)).ConfigureAwait(false);

        await InsertReceiptAsync(
            connection,
            transaction,
            "start_quest",
            command.RequestId.ToString(),
            HeroPassportVersions.MutationArgsVersion,
            argsHash,
            "quest_start",
            questId.ToString(),
            projectIdText,
            command.HeroId.ToString(),
            timestamp,
            cancellationToken).ConfigureAwait(false);

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            "INSERT INTO hero_project_stats(hero_id,project_id,quests_started,quests_finished,quests_succeeded,total_xp_earned,last_quest_at_utc) VALUES($heroId,$projectId,1,0,0,0,$last) ON CONFLICT(hero_id,project_id) DO UPDATE SET quests_started=hero_project_stats.quests_started+1,last_quest_at_utc=excluded.last_quest_at_utc;",
            cancellationToken,
            ("$heroId", command.HeroId.ToString()),
            ("$projectId", projectIdText),
            ("$last", timestamp)).ConfigureAwait(false);

        transaction.Commit();
        return new StartQuestResult(
            new QuestSummary(questId, command.HeroId, command.QuestType, command.Title, command.Goal, now.ToUniversalTime(), settings.Locale),
            hero,
            false);
    }

    private static async Task<bool> HasOpenQuestAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HeroId heroId,
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            "SELECT 1 FROM quest_sessions WHERE hero_id=$heroId AND project_id=$projectId AND status='open' LIMIT 1;",
            ("$heroId", heroId.ToString()),
            ("$projectId", projectId.ToString()));
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is not null and not DBNull;
    }
}
