using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using Microsoft.Data.Sqlite;
using System.Data;
using System.Globalization;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    public async Task<ListHeroesResult> ListHeroesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        var settings = await GetSettingsRowAsync(connection, null, cancellationToken).ConfigureAwait(false)
            ?? throw new HeroPassportException("HP900", "Application settings are unavailable.");
        if (!settings.SetupCompleted)
        {
            throw new HeroPassportException("HP001", "Setup is required.");
        }

        await using var command = CreateCommand(
            connection,
            null,
            """
            SELECT id,name,total_xp,trust,strain,archived_at_utc,created_at_utc
            FROM heroes
            ORDER BY
              CASE WHEN id=$activeId THEN 0 ELSE 1 END,
              CASE WHEN archived_at_utc IS NULL THEN 0 ELSE 1 END,
              created_at_utc ASC,
              id ASC;
            """,
            ("$activeId", settings.ActiveHeroId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var heroes = new List<HeroListItem>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var heroId = HeroId.Parse(reader.GetString(0));
            heroes.Add(new HeroListItem(
                heroId,
                reader.GetString(1),
                !reader.IsDBNull(5),
                string.Equals(settings.ActiveHeroId, heroId.ToString(), StringComparison.Ordinal),
                reader.GetInt64(2),
                1,
                "code_squire",
                reader.GetInt32(3),
                reader.GetInt32(4)));
        }

        return new ListHeroesResult(heroes);
    }

    public async Task<HeroLifecycleResult> ArchiveHeroAsync(
        HeroId heroId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
        var settings = await GetSettingsRowAsync(connection, transaction, cancellationToken).ConfigureAwait(false)
            ?? throw new HeroPassportException("HP900", "Application settings are unavailable.");
        if (!settings.SetupCompleted)
        {
            throw new HeroPassportException("HP001", "Setup is required.");
        }

        var hero = await FindHeroAsync(connection, transaction, heroId.ToString(), cancellationToken).ConfigureAwait(false)
            ?? throw new HeroPassportException("HP140", "Hero was not found.");
        if (hero.Archived)
        {
            transaction.Commit();
            return new HeroLifecycleResult(hero, true);
        }

        if (string.Equals(settings.ActiveHeroId, heroId.ToString(), StringComparison.Ordinal))
        {
            throw new HeroPassportException("HP145", "The active default Hero cannot be archived.");
        }

        await using (var openCommand = CreateCommand(
            connection,
            transaction,
            "SELECT 1 FROM quest_sessions WHERE hero_id=$heroId AND status='open' LIMIT 1;",
            ("$heroId", heroId.ToString())))
        {
            if (await openCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null)
            {
                throw new HeroPassportException("HP143", "Hero has an open Quest.");
            }
        }

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            "UPDATE heroes SET archived_at_utc=$archived,updated_at_utc=$updated WHERE id=$heroId;",
            cancellationToken,
            ("$archived", FormatTimestamp(now)),
            ("$updated", FormatTimestamp(now)),
            ("$heroId", heroId.ToString())).ConfigureAwait(false);
        transaction.Commit();
        return new HeroLifecycleResult(hero with { Archived = true }, false);
    }

    public async Task<HeroLifecycleResult> RestoreHeroAsync(
        HeroId heroId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
        var settings = await GetSettingsRowAsync(connection, transaction, cancellationToken).ConfigureAwait(false)
            ?? throw new HeroPassportException("HP900", "Application settings are unavailable.");
        if (!settings.SetupCompleted)
        {
            throw new HeroPassportException("HP001", "Setup is required.");
        }

        var hero = await FindHeroAsync(connection, transaction, heroId.ToString(), cancellationToken).ConfigureAwait(false)
            ?? throw new HeroPassportException("HP140", "Hero was not found.");
        if (!hero.Archived)
        {
            transaction.Commit();
            return new HeroLifecycleResult(hero, true);
        }

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            "UPDATE heroes SET archived_at_utc=NULL,updated_at_utc=$updated WHERE id=$heroId;",
            cancellationToken,
            ("$updated", FormatTimestamp(now)),
            ("$heroId", heroId.ToString())).ConfigureAwait(false);
        transaction.Commit();
        return new HeroLifecycleResult(hero with { Archived = false }, false);
    }

    public async Task<HeroCardResult> GetHeroCardAsync(
        HeroId heroId,
        ProjectBindingContext project,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        var settings = await GetSettingsRowAsync(connection, null, cancellationToken).ConfigureAwait(false)
            ?? throw new HeroPassportException("HP900", "Application settings are unavailable.");
        if (!settings.SetupCompleted)
        {
            throw new HeroPassportException("HP001", "Setup is required.");
        }

        await using var heroCommand = CreateCommand(
            connection,
            null,
            "SELECT name,total_xp,trust,strain,success_streak FROM heroes WHERE id=$heroId;",
            ("$heroId", heroId.ToString()));
        await using var heroReader = await heroCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await heroReader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new HeroPassportException("HP140", "Hero was not found.");
        }

        var heroName = heroReader.GetString(0);
        var totalXp = heroReader.GetInt64(1);
        var trust = heroReader.GetInt32(2);
        var strain = heroReader.GetInt32(3);
        var successStreak = heroReader.GetInt32(4);
        await heroReader.DisposeAsync().ConfigureAwait(false);

        var heroSkills = await GetHeroTopSkillsAsync(connection, heroId, cancellationToken).ConfigureAwait(false);
        var traits = await GetStringKeysAsync(
            connection,
            "SELECT trait_key FROM hero_traits WHERE hero_id=$heroId ORDER BY unlocked_at_utc ASC,trait_key ASC;",
            heroId,
            cancellationToken).ConfigureAwait(false);
        var titles = await GetStringKeysAsync(
            connection,
            "SELECT title_key FROM hero_titles WHERE hero_id=$heroId ORDER BY unlocked_at_utc ASC,title_key ASC;",
            heroId,
            cancellationToken).ConfigureAwait(false);
        var activeTitle = await GetActiveTitleAsync(connection, heroId, cancellationToken).ConfigureAwait(false);

        var projectId = await FindProjectIdAsync(connection, null, project.WorkspaceFingerprint, cancellationToken).ConfigureAwait(false);
        var projectStats = projectId is null
            ? new ProjectStats(0, 0, 0, 0)
            : await GetProjectStatsAsync(connection, heroId, projectId, cancellationToken).ConfigureAwait(false);
        var projectSkills = projectId is null
            ? []
            : await GetProjectTopSkillsAsync(connection, heroId, projectId, cancellationToken).ConfigureAwait(false);
        var successRate = projectStats.QuestsFinished == 0
            ? 0
            : checked((projectStats.QuestsSucceeded * 1000) / projectStats.QuestsFinished);

        return new HeroCardResult(
            new HeroCardHero(
                heroId,
                heroName,
                totalXp,
                1,
                false,
                totalXp,
                100,
                "code_squire",
                activeTitle,
                trust,
                strain,
                successStreak,
                heroSkills,
                traits,
                titles),
            new HeroCardProject(
                project.DisplayName,
                projectStats.QuestsStarted,
                projectStats.QuestsFinished,
                projectStats.QuestsSucceeded,
                projectStats.TotalXpEarned,
                successRate,
                projectSkills));
    }

    private static async Task<ProjectStats> GetProjectStatsAsync(
        SqliteConnection connection,
        HeroId heroId,
        string projectId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            null,
            "SELECT quests_started,quests_finished,quests_succeeded,total_xp_earned FROM hero_project_stats WHERE hero_id=$heroId AND project_id=$projectId;",
            ("$heroId", heroId.ToString()),
            ("$projectId", projectId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new ProjectStats(0, 0, 0, 0);
        }

        return new ProjectStats(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt64(3));
    }

    private static async Task<IReadOnlyList<HeroCardSkill>> GetHeroTopSkillsAsync(
        SqliteConnection connection,
        HeroId heroId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            null,
            "SELECT skill_key,xp FROM hero_skills WHERE hero_id=$heroId ORDER BY xp DESC,skill_key ASC LIMIT 3;",
            ("$heroId", heroId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<HeroCardSkill>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var xp = reader.GetInt64(1);
            result.Add(new HeroCardSkill(reader.GetString(0), xp, 1, false, 50));
        }

        return result;
    }

    private static async Task<IReadOnlyList<HeroCardSkill>> GetProjectTopSkillsAsync(
        SqliteConnection connection,
        HeroId heroId,
        string projectId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            null,
            """
            SELECT s.skill_key,SUM(s.xp_gained) AS xp
            FROM quest_report_skills s
            JOIN quest_reports r ON r.id=s.quest_report_id
            JOIN quest_sessions q ON q.id=r.quest_id
            WHERE q.hero_id=$heroId AND q.project_id=$projectId
            GROUP BY s.skill_key
            ORDER BY xp DESC,s.skill_key ASC
            LIMIT 3;
            """,
            ("$heroId", heroId.ToString()),
            ("$projectId", projectId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<HeroCardSkill>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var xp = reader.GetInt64(1);
            result.Add(new HeroCardSkill(reader.GetString(0), xp, 1, false, 50));
        }

        return result;
    }

    private static async Task<IReadOnlyList<string>> GetStringKeysAsync(
        SqliteConnection connection,
        string sql,
        HeroId heroId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, null, sql, ("$heroId", heroId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<string>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    private static async Task<string?> GetActiveTitleAsync(
        SqliteConnection connection,
        HeroId heroId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            null,
            """
            SELECT ht.title_key
            FROM hero_titles ht
            JOIN titles t ON t.title_key=ht.title_key
            WHERE ht.hero_id=$heroId
            ORDER BY t.priority DESC,ht.unlocked_at_utc DESC,ht.title_key DESC
            LIMIT 1;
            """,
            ("$heroId", heroId.ToString()));
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private sealed record ProjectStats(int QuestsStarted, int QuestsFinished, int QuestsSucceeded, long TotalXpEarned);
}
