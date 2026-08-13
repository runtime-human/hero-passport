using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    public async Task<RuntimeContextResult> GetRuntimeContextAsync(ProjectBindingContext project, CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        var settings = await SettingsAsync(connection, null, cancellationToken).ConfigureAwait(false);
        HeroIdentitySnapshot? activeHero = null;
        if (settings.SetupCompleted && settings.ActiveHeroId is not null)
        {
            activeHero = await HeroAsync(connection, null, settings.ActiveHeroId, cancellationToken).ConfigureAwait(false);
        }

        string? projectId = null;
        var displayName = project.DisplayName;
        await using var projectCommand = Command(connection, null, "SELECT id,display_name FROM projects WHERE workspace_fingerprint=$fingerprint;", ("$fingerprint", project.WorkspaceFingerprint));
        await using (var reader = await projectCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                projectId = reader.GetString(0);
                displayName = reader.GetString(1);
            }
        }

        var openQuests = projectId is null ? [] : await OpenQuestsAsync(connection, projectId, cancellationToken).ConfigureAwait(false);
        return new RuntimeContextResult(
            HeroPassportVersions.ProductVersion,
            HeroPassportVersions.ContractVersion,
            HeroPassportVersions.SkillContractVersion,
            settings.SetupCompleted,
            settings.SetupCompleted ? settings.Snapshot() : null,
            activeHero,
            new ProjectContextSnapshot(displayName),
            openQuests,
            HeroPassportVersions.CurrentRules);
    }

    private static async Task<OpenQuestContext[]> OpenQuestsAsync(SqliteConnection connection, string projectId, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, null, "SELECT q.id,q.hero_id,h.name,q.quest_type,q.title,q.goal,q.started_at_utc,q.locale FROM quest_sessions q JOIN heroes h ON h.id=q.hero_id WHERE q.project_id=$project AND q.status='open' ORDER BY q.started_at_utc ASC,q.id ASC;", ("$project", projectId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var quests = new List<OpenQuestContext>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            quests.Add(new OpenQuestContext(
                QuestId.Parse(reader.GetString(0)),
                HeroId.Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                DateTimeOffset.ParseExact(reader.GetString(6), "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
                reader.GetString(7)));
        }

        return quests.ToArray();
    }
}
