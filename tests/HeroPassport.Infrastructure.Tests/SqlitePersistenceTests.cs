using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Xunit;

namespace HeroPassport.Infrastructure.Tests;

public sealed class SqlitePersistenceTests
{
    [Fact]
    public async Task InitializeCreatesQualifiedFileBackedDatabase()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            await using var context = new HeroPassportDbContextFactory(path).CreateDbContext();
            var declaredMigrationCount = context.Database.GetMigrations().LongCount();
            await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, cancellationToken);

            Assert.Equal("wal", await ScalarStringAsync(connection, "PRAGMA journal_mode;", cancellationToken));
            Assert.Equal(2L, await ScalarLongAsync(connection, "PRAGMA synchronous;", cancellationToken));
            Assert.Equal(1L, await ScalarLongAsync(connection, "PRAGMA foreign_keys;", cancellationToken));
            Assert.Equal(0L, await ScalarLongAsync(connection, "PRAGMA trusted_schema;", cancellationToken));
            Assert.True(ParseSqliteVersion(await ScalarStringAsync(connection, "SELECT sqlite_version();", cancellationToken)) >= new Version(3, 53, 4));
            Assert.Equal(1L, await ScalarLongAsync(connection, "SELECT COUNT(*) FROM app_settings WHERE id = 1;", cancellationToken));
            Assert.Equal(declaredMigrationCount, await ScalarLongAsync(connection, "SELECT COUNT(*) FROM __EFMigrationsHistory;", cancellationToken));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task PhysicalChecksRejectInvalidState()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, cancellationToken);

            await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(connection,
                "INSERT INTO app_settings(id, setup_completed, active_hero_id, locale, presentation_style, auto_start_quest, auto_finish_quest, project_identity_salt_v1, config_version, created_at_utc, updated_at_utc) " +
                "VALUES(2,0,NULL,'en-US','rpg_engineering',1,1,randomblob(32),1,'2026-08-11T00:00:00.000Z','2026-08-11T00:00:00.000Z');",
                cancellationToken));

            await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(connection,
                "INSERT INTO heroes(id,name,total_xp,trust,strain,success_streak,created_at_utc,updated_at_utc) " +
                "VALUES('01900000-0000-7000-8000-000000000001','Hero',0,101,20,0,'2026-08-11T00:00:00.000Z','2026-08-11T00:00:00.000Z');",
                cancellationToken));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task PartialIndexAllowsOnlyOneOpenQuestPerHeroAndProject()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, cancellationToken);

            const string heroId = "01900000-0000-7000-8000-000000000001";
            const string projectId = "01900000-0000-7000-8000-000000000002";

            await ExecuteAsync(connection,
                $"INSERT INTO heroes(id,name,total_xp,trust,strain,success_streak,created_at_utc,updated_at_utc) VALUES('{heroId}','Hero',0,50,20,0,'2026-08-11T00:00:00.000Z','2026-08-11T00:00:00.000Z');",
                cancellationToken);
            await ExecuteAsync(connection,
                $"INSERT INTO projects(id,display_name,workspace_fingerprint,identity_version,created_at_utc) VALUES('{projectId}','Project','{new string('a', 64)}','project-identity/1','2026-08-11T00:00:00.000Z');",
                cancellationToken);
            await ExecuteAsync(connection,
                $"INSERT INTO quest_sessions(id,hero_id,project_id,quest_type,title,goal,locale,status,started_at_utc,finished_at_utc,created_at_utc) VALUES('01900000-0000-7000-8000-000000000003','{heroId}','{projectId}','coding','First','Goal','en-US','open','2026-08-11T00:00:00.000Z',NULL,'2026-08-11T00:00:00.000Z');",
                cancellationToken);

            await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(connection,
                $"INSERT INTO quest_sessions(id,hero_id,project_id,quest_type,title,goal,locale,status,started_at_utc,finished_at_utc,created_at_utc) VALUES('01900000-0000-7000-8000-000000000004','{heroId}','{projectId}','coding','Second','Goal','en-US','open','2026-08-11T00:00:01.000Z',NULL,'2026-08-11T00:00:01.000Z');",
                cancellationToken));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task FinishTablesHaveUniqueQuestBackstops()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, cancellationToken);

            Assert.True(await HasUniqueIndexAsync(connection, "quest_reports", "ux_quest_reports_quest_id", cancellationToken));
            Assert.True(await HasUniqueIndexAsync(connection, "xp_events", "ux_xp_events_quest_id", cancellationToken));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static async Task<bool> HasUniqueIndexAsync(
        SqliteConnection connection,
        string table,
        string indexName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA index_list('{table}');";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), indexName, StringComparison.Ordinal) && reader.GetInt64(2) == 1)
            {
                return true;
            }
        }

        return false;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> ScalarLongAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task<string> ScalarStringAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture)!;
    }

    private static Version ParseSqliteVersion(string value)
    {
        var parts = value.Split('.');
        return new Version(
            int.Parse(parts[0], CultureInfo.InvariantCulture),
            int.Parse(parts[1], CultureInfo.InvariantCulture),
            int.Parse(parts[2], CultureInfo.InvariantCulture));
    }

    private static string CreateDatabasePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hero-passport-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "hero-passport.db");
    }

    private static void DeleteDatabase(string path)
    {
        SqliteConnection.ClearAllPools();
        var directory = Path.GetDirectoryName(path);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
