using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Xunit;

namespace HeroPassport.Infrastructure.Tests;

public sealed class SqliteFoundationQualificationTests
{
    [Fact]
    public async Task EfModelMatchesCommittedMigration()
    {
        var path = CreateDatabasePath();
        try
        {
            var factory = new HeroPassportDbContextFactory(path);
            await using var context = factory.CreateDbContext();

            Assert.NotNull(context.Model.FindEntityType("HeroPassport.Storage.MutationReceipt"));
            Assert.NotNull(context.Model.FindEntityType("HeroPassport.Storage.HeroProjectStats"));
            Assert.NotNull(context.Model.FindEntityType("HeroPassport.Storage.QuestReport"));
            Assert.NotNull(context.Model.FindEntityType("HeroPassport.Storage.QuestTrustStrainComponent"));
            Assert.NotNull(context.Model.FindEntityType("HeroPassport.Storage.XpEvent"));
            Assert.False(context.Database.HasPendingModelChanges());
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task RepeatedInitializationPreservesSingletonInstallationSalt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var firstSalt = await HeroPassportDatabase.ReadProjectIdentitySaltAsync(path, cancellationToken);

            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var secondSalt = await HeroPassportDatabase.ReadProjectIdentitySaltAsync(path, cancellationToken);

            Assert.Equal(firstSalt, secondSalt);
            await using var context = new HeroPassportDbContextFactory(path).CreateDbContext();
            var declaredMigrationCount = context.Database.GetMigrations().LongCount();
            await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, cancellationToken);
            Assert.Equal(1L, await ScalarLongAsync(connection, "SELECT COUNT(*) FROM app_settings;", cancellationToken));
            Assert.Equal(declaredMigrationCount, await ScalarLongAsync(connection, "SELECT COUNT(*) FROM __EFMigrationsHistory;", cancellationToken));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task ConnectionPolicySurvivesPooledReopenAndPoolClear()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);

            await AssertConnectionPolicyAsync(path, cancellationToken);
            await AssertConnectionPolicyAsync(path, cancellationToken);

            SqliteConnection.ClearAllPools();
            await AssertConnectionPolicyAsync(path, cancellationToken);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task ForeignKeysAndQuestStateAreEnforcedBySQLite()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, cancellationToken);

            await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(
                connection,
                "INSERT INTO quest_sessions(id,hero_id,project_id,quest_type,title,goal,locale,status,started_at_utc,finished_at_utc,created_at_utc) " +
                "VALUES('01900000-0000-7000-8000-000000000010','01900000-0000-7000-8000-000000000011','01900000-0000-7000-8000-000000000012','coding','Quest','Goal','en-US','open','2026-08-13T00:00:00.000Z',NULL,'2026-08-13T00:00:00.000Z');",
                cancellationToken));

            const string heroId = "01900000-0000-7000-8000-000000000021";
            const string projectId = "01900000-0000-7000-8000-000000000022";
            await ExecuteAsync(
                connection,
                $"INSERT INTO heroes(id,name,total_xp,trust,strain,success_streak,created_at_utc,updated_at_utc) VALUES('{heroId}','Hero',0,50,20,0,'2026-08-13T00:00:00.000Z','2026-08-13T00:00:00.000Z');",
                cancellationToken);
            await ExecuteAsync(
                connection,
                $"INSERT INTO projects(id,display_name,workspace_fingerprint,identity_version,created_at_utc) VALUES('{projectId}','Project','{new string('b', 64)}','project-identity/1','2026-08-13T00:00:00.000Z');",
                cancellationToken);

            await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(
                connection,
                $"INSERT INTO quest_sessions(id,hero_id,project_id,quest_type,title,goal,locale,status,started_at_utc,finished_at_utc,created_at_utc) VALUES('01900000-0000-7000-8000-000000000023','{heroId}','{projectId}','coding','Quest','Goal','en-US','finished','2026-08-13T00:00:00.000Z',NULL,'2026-08-13T00:00:00.000Z');",
                cancellationToken));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static async Task AssertConnectionPolicyAsync(string path, CancellationToken cancellationToken)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, cancellationToken);
        Assert.Equal(2L, await ScalarLongAsync(connection, "PRAGMA synchronous;", cancellationToken));
        Assert.Equal(1L, await ScalarLongAsync(connection, "PRAGMA foreign_keys;", cancellationToken));
        Assert.Equal(0L, await ScalarLongAsync(connection, "PRAGMA trusted_schema;", cancellationToken));
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
