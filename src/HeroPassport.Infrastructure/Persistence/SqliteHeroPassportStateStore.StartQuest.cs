using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using Microsoft.Data.Sqlite;
using System.Data;
using System.Globalization;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    public async Task<StartQuestResult> StartQuestAsync(
        StartQuestStoreCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);

        var project = await ResolveProjectCandidateAsync(connection, transaction, command.Project, cancellationToken).ConfigureAwait(false);
        var argsHash = CanonicalMutationEncoder.HashStartQuest(project.ProjectId, command.HeroId, command.QuestType, command.Title, command.Goal);
        var requestId = command.RequestId.ToString();
        var receipt = await ReceiptAsync(connection, transaction, "start_quest", requestId, cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            EnsureReceipt(receipt, command.ArgsEncodingVersion, argsHash);
            var quest = await StartedQuestAsync(connection, transaction, receipt.ResultEntityId ?? string.Empty, cancellationToken).ConfigureAwait(false);
            var hero = await HeroForStartAsync(connection, transaction, quest.HeroId, allowArchived: true, cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            return new StartQuestResult(quest, hero, true);
        }

        var settings = await SettingsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        if (!settings.SetupCompleted)
        {
            throw new HeroPassportException("HP001", "Setup is required.");
        }

        var explicitHero = await HeroForStartAsync(connection, transaction, command.HeroId, allowArchived: false, cancellationToken).ConfigureAwait(false);
        if (await HasOpenQuestAsync(connection, transaction, command.HeroId, project.ProjectId, cancellationToken).ConfigureAwait(false))
        {
            throw new HeroPassportException("HP133", "An open Quest already exists for this Hero and Project.");
        }

        var timestamp = Timestamp(now);
        if (!project.Exists)
        {
            await ExecuteAsync(
                connection,
                transaction,
                "INSERT INTO projects(id,display_name,workspace_fingerprint,identity_version,created_at_utc) VALUES($id,$name,$fingerprint,$version,$time);",
                cancellationToken,
                ("$id", project.ProjectId.ToString()),
                ("$name", command.Project.DisplayName),
                ("$fingerprint", command.Project.WorkspaceFingerprint),
                ("$version", command.Project.IdentityVersion),
                ("$time", timestamp)).ConfigureAwait(false);
        }

        var questId = QuestId.New();
        await ExecuteAsync(
            connection,
            transaction,
            "INSERT INTO quest_sessions(id,hero_id,project_id,quest_type,title,goal,locale,status,started_at_utc,finished_at_utc,created_at_utc) VALUES($id,$hero,$project,$type,$title,$goal,$locale,'open',$time,NULL,$time);",
            cancellationToken,
            ("$id", questId.ToString()),
            ("$hero", command.HeroId.ToString()),
            ("$project", project.ProjectId.ToString()),
            ("$type", command.QuestType),
            ("$title", command.Title),
            ("$goal", command.Goal),
            ("$locale", settings.Locale),
            ("$time", timestamp)).ConfigureAwait(false);

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO hero_project_stats(hero_id,project_id,quests_started,quests_finished,quests_succeeded,total_xp_earned,last_quest_at_utc)
            VALUES($hero,$project,1,0,0,0,$time)
            ON CONFLICT(hero_id,project_id) DO UPDATE SET
                quests_started=quests_started+1,
                last_quest_at_utc=excluded.last_quest_at_utc;
            """,
            cancellationToken,
            ("$hero", command.HeroId.ToString()),
            ("$project", project.ProjectId.ToString()),
            ("$time", timestamp)).ConfigureAwait(false);

        await ExecuteAsync(
            connection,
            transaction,
            "INSERT INTO mutation_receipts(operation_key,request_id,args_encoding_version,args_hash,result_kind,result_entity_id,project_id,hero_id,result_status,effective_at_utc) VALUES('start_quest',$request,$encoding,$hash,'quest_start',$quest,$project,$hero,'active',$time);",
            cancellationToken,
            ("$request", requestId),
            ("$encoding", command.ArgsEncodingVersion),
            ("$hash", argsHash),
            ("$quest", questId.ToString()),
            ("$project", project.ProjectId.ToString()),
            ("$hero", command.HeroId.ToString()),
            ("$time", timestamp)).ConfigureAwait(false);

        transaction.Commit();
        return new StartQuestResult(
            new StartedQuestSnapshot(questId, command.HeroId, command.QuestType, command.Title, command.Goal, now.ToUniversalTime(), settings.Locale),
            explicitHero,
            false);
    }

    private static async Task<ProjectCandidate> ResolveProjectCandidateAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ProjectBindingContext project,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            "SELECT id FROM projects WHERE workspace_fingerprint=$fingerprint;",
            ("$fingerprint", project.WorkspaceFingerprint));
        var existing = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return existing is string id
            ? new ProjectCandidate(ProjectId.Parse(id), true)
            : new ProjectCandidate(ProjectId.New(), false);
    }

    private static async Task<bool> HasOpenQuestAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HeroId heroId,
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            "SELECT EXISTS(SELECT 1 FROM quest_sessions WHERE hero_id=$hero AND project_id=$project AND status='open');",
            ("$hero", heroId.ToString()),
            ("$project", projectId.ToString()));
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture) != 0;
    }

    private static async Task<HeroIdentitySnapshot> HeroForStartAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HeroId heroId,
        bool allowArchived,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            "SELECT name,archived_at_utc FROM heroes WHERE id=$id;",
            ("$id", heroId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new HeroPassportException("HP140", "Hero was not found.");
        }

        if (!allowArchived && !reader.IsDBNull(1))
        {
            throw new HeroPassportException("HP141", "Hero is archived.");
        }

        return new HeroIdentitySnapshot(heroId, reader.GetString(0));
    }

    private static async Task<StartedQuestSnapshot> StartedQuestAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string questId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            "SELECT hero_id,quest_type,title,goal,started_at_utc,locale FROM quest_sessions WHERE id=$id;",
            ("$id", questId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new HeroPassportException("HP130", "Quest was not found.");
        }

        return new StartedQuestSnapshot(
            QuestId.Parse(questId),
            HeroId.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            DateTimeOffset.ParseExact(reader.GetString(4), "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            reader.GetString(5));
    }

    private sealed record ProjectCandidate(ProjectId ProjectId, bool Exists);
}
