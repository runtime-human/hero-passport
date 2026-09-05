using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Engine;
using HeroPassport.Domain.Primitives;
using Microsoft.Data.Sqlite;
using System.Data;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    public async Task<HeroListResult> ListHeroesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        var settings = await SettingsAsync(connection, null, cancellationToken).ConfigureAwait(false);
        RequireSetup(settings);

        await using var command = Command(
            connection,
            null,
            """
            SELECT id,name,total_xp,trust,strain,archived_at_utc
            FROM heroes
            ORDER BY CASE WHEN id=$active THEN 0 ELSE 1 END,
                     CASE WHEN archived_at_utc IS NULL THEN 0 ELSE 1 END,
                     created_at_utc ASC,
                     id ASC;
            """,
            ("$active", settings.ActiveHeroId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var heroes = new List<HeroListItemSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            heroes.Add(ToListSnapshot(ReadHeroRow(reader), settings.ActiveHeroId));
        }

        return new HeroListResult(heroes.ToArray());
    }

    public async Task<HeroPreferenceChangeResult> ActivateHeroPreferenceAsync(
        HeroId heroId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
        var settings = await SettingsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        RequireSetup(settings);
        var hero = await AdministrationHeroAsync(connection, transaction, heroId, cancellationToken).ConfigureAwait(false);
        if (hero.Archived)
        {
            throw new HeroPassportException("HP141", "Hero is archived.");
        }

        var changed = !string.Equals(settings.ActiveHeroId, heroId.ToString(), StringComparison.Ordinal);
        if (changed)
        {
            await ExecuteAsync(
                connection,
                transaction,
                "UPDATE app_settings SET active_hero_id=$hero,config_version=config_version+1,updated_at_utc=$time WHERE id=1;",
                cancellationToken,
                ("$hero", heroId.ToString()),
                ("$time", Timestamp(now))).ConfigureAwait(false);
        }

        transaction.Commit();
        return new HeroPreferenceChangeResult(ToListSnapshot(hero, heroId.ToString()), changed);
    }

    public async Task<HeroPreferenceChangeResult> ArchiveHeroAsync(
        HeroId heroId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
        var settings = await SettingsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        RequireSetup(settings);
        var hero = await AdministrationHeroAsync(connection, transaction, heroId, cancellationToken).ConfigureAwait(false);
        if (hero.Archived)
        {
            transaction.Commit();
            return new HeroPreferenceChangeResult(ToListSnapshot(hero, settings.ActiveHeroId), false);
        }

        if (string.Equals(settings.ActiveHeroId, heroId.ToString(), StringComparison.Ordinal))
        {
            throw new HeroPassportException("HP145", "The active Hero cannot be archived.");
        }

        await using (var openQuest = Command(
            connection,
            transaction,
            "SELECT EXISTS(SELECT 1 FROM quest_sessions WHERE hero_id=$hero AND status='open');",
            ("$hero", heroId.ToString())))
        {
            if (Convert.ToInt64(await openQuest.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture) != 0)
            {
                throw new HeroPassportException("HP143", "Hero has an open Quest.");
            }
        }

        var timestamp = Timestamp(now);
        await ExecuteAsync(
            connection,
            transaction,
            "UPDATE heroes SET archived_at_utc=$time,updated_at_utc=$time WHERE id=$hero AND archived_at_utc IS NULL;",
            cancellationToken,
            ("$time", timestamp),
            ("$hero", heroId.ToString())).ConfigureAwait(false);
        transaction.Commit();
        return new HeroPreferenceChangeResult(ToListSnapshot(hero with { Archived = true }, settings.ActiveHeroId), true);
    }

    public async Task<HeroPreferenceChangeResult> RestoreHeroAsync(
        HeroId heroId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
        var settings = await SettingsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        RequireSetup(settings);
        var hero = await AdministrationHeroAsync(connection, transaction, heroId, cancellationToken).ConfigureAwait(false);
        if (!hero.Archived)
        {
            transaction.Commit();
            return new HeroPreferenceChangeResult(ToListSnapshot(hero, settings.ActiveHeroId), false);
        }

        await ExecuteAsync(
            connection,
            transaction,
            "UPDATE heroes SET archived_at_utc=NULL,updated_at_utc=$time WHERE id=$hero;",
            cancellationToken,
            ("$time", Timestamp(now)),
            ("$hero", heroId.ToString())).ConfigureAwait(false);
        transaction.Commit();
        return new HeroPreferenceChangeResult(ToListSnapshot(hero with { Archived = false }, settings.ActiveHeroId), true);
    }

    public async Task<HeroCardResult> GetCardAsync(
        HeroId heroId,
        ProjectBindingContext project,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        var settings = await SettingsAsync(connection, null, cancellationToken).ConfigureAwait(false);
        RequireSetup(settings);
        var hero = await AdministrationHeroAsync(connection, null, heroId, cancellationToken).ConfigureAwait(false);

        var displayName = project.DisplayName;
        string? projectId = null;
        await using (var projectCommand = Command(
            connection,
            null,
            "SELECT id,display_name FROM projects WHERE workspace_fingerprint=$fingerprint;",
            ("$fingerprint", project.WorkspaceFingerprint)))
        await using (var reader = await projectCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                projectId = reader.GetString(0);
                displayName = reader.GetString(1);
            }
        }

        long questsStarted = 0;
        long questsFinished = 0;
        long questsSucceeded = 0;
        long totalXpEarned = 0;
        if (projectId is not null)
        {
            await using var statsCommand = Command(
                connection,
                null,
                "SELECT quests_started,quests_finished,quests_succeeded,total_xp_earned FROM hero_project_stats WHERE hero_id=$hero AND project_id=$project;",
                ("$hero", heroId.ToString()),
                ("$project", projectId));
            await using var reader = await statsCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                questsStarted = reader.GetInt64(0);
                questsFinished = reader.GetInt64(1);
                questsSucceeded = reader.GetInt64(2);
                totalXpEarned = reader.GetInt64(3);
            }
        }

        var successRatePermille = questsFinished == 0
            ? 0
            : checked((int)(((decimal)questsSucceeded * 1000m) / questsFinished));
        var rules = HeroPassportVersions.CurrentRules;
        var level = MinimalQuestFinishRules.HeroLevel(hero.TotalXp);
        var isLevelCapped = MinimalQuestFinishRules.IsHeroLevelCapped(level, rules.HeroProgression);
        var levelXp = MinimalQuestFinishRules.HeroLevelXp(hero.TotalXp, level, rules.HeroProgression);
        var nextLevelXpRequired = MinimalQuestFinishRules.NextHeroLevelXpRequired(level, rules.HeroProgression);
        return new HeroCardResult(
            new HeroCardSnapshot(
                hero.HeroId,
                hero.Name,
                hero.TotalXp,
                level,
                isLevelCapped,
                levelXp,
                nextLevelXpRequired,
                MinimalQuestFinishRules.RankKey(level),
                ActiveTitle: null,
                hero.Trust,
                hero.Strain,
                hero.SuccessStreak,
                Array.Empty<CardSkillSnapshot>(),
                Array.Empty<string>(),
                Array.Empty<string>()),
            new ProjectCardSnapshot(
                displayName,
                questsStarted,
                questsFinished,
                questsSucceeded,
                totalXpEarned,
                successRatePermille,
                Array.Empty<CardSkillSnapshot>()));
    }

    private static void RequireSetup(SettingsRow settings)
    {
        if (!settings.SetupCompleted)
        {
            throw new HeroPassportException("HP001", "Setup is required.");
        }
    }

    private static async Task<AdministrationHeroRow> AdministrationHeroAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        HeroId heroId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            "SELECT id,name,total_xp,trust,strain,success_streak,archived_at_utc FROM heroes WHERE id=$id;",
            ("$id", heroId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new HeroPassportException("HP140", "Hero was not found.");
        }

        return new AdministrationHeroRow(
            HeroId.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetInt64(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt64(5),
            !reader.IsDBNull(6));
    }

    private static AdministrationHeroRow ReadHeroRow(SqliteDataReader reader) =>
        new(
            HeroId.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetInt64(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            0,
            !reader.IsDBNull(5));

    private static HeroListItemSnapshot ToListSnapshot(AdministrationHeroRow hero, string? activeHeroId)
    {
        var level = MinimalQuestFinishRules.HeroLevel(hero.TotalXp);
        return new HeroListItemSnapshot(
            hero.HeroId,
            hero.Name,
            hero.Archived,
            string.Equals(activeHeroId, hero.HeroId.ToString(), StringComparison.Ordinal),
            hero.TotalXp,
            level,
            MinimalQuestFinishRules.RankKey(level),
            hero.Trust,
            hero.Strain);
    }

    private sealed record AdministrationHeroRow(
        HeroId HeroId,
        string Name,
        long TotalXp,
        int Trust,
        int Strain,
        long SuccessStreak,
        bool Archived);
}
