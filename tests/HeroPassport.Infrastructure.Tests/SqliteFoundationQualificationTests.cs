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
            await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, cancellationToken);
            Assert.Equal(1L, await ScalarLongAsync(connection, "SELECT COUNT(*) FROM app_settings;", cancellationToken));

            var factory = new HeroPassportDbContextFactory(path);
            await using var context = factory.CreateDbContext();
            Assert.Empty(await context.Database.GetPendingMigrationsAsync(cancellationToken));
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
            var factory = new HeroPassportDbContextFactory(path);

            await using (var context = factory.CreateDbContext())
            {
                context.Set<Dictionary<string, object>>("HeroPassport.Storage.QuestSession").Add(NewQuest(
                    "01900000-0000-7000-8000-000000000010",
                    "01900000-0000-7000-8000-000000000011",
                    "01900000-0000-7000-8000-000000000012",
                    "open",
                    null));
                await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(cancellationToken));
            }

            const string heroId = "01900000-0000-7000-8000-000000000021";
            const string projectId = "01900000-0000-7000-8000-000000000022";
            await using (var context = factory.CreateDbContext())
            {
                context.Set<Dictionary<string, object>>("HeroPassport.Storage.Hero").Add(new()
                {
                    ["id"] = heroId,
                    ["name"] = "Hero",
                    ["total_xp"] = 0L,
                    ["trust"] = 50,
                    ["strain"] = 20,
                    ["success_streak"] = 0L,
                    ["created_at_utc"] = "2026-08-13T00:00:00.000Z",
                    ["updated_at_utc"] = "2026-08-13T00:00:00.000Z",
                });
                context.Set<Dictionary<string, object>>("HeroPassport.Storage.Project").Add(new()
                {
                    ["id"] = projectId,
                    ["display_name"] = "Project",
                    ["workspace_fingerprint"] = new string('b', 64),
                    ["identity_version"] = "project-identity/1",
                    ["created_at_utc"] = "2026-08-13T00:00:00.000Z",
                });
                context.Set<Dictionary<string, object>>("HeroPassport.Storage.QuestSession").Add(NewQuest(
                    "01900000-0000-7000-8000-000000000023",
                    heroId,
                    projectId,
                    "finished",
                    null));
                await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(cancellationToken));
            }
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static Dictionary<string, object> NewQuest(string id, string heroId, string projectId, string status, string? finishedAt)
    {
        var values = new Dictionary<string, object>
        {
            ["id"] = id,
            ["hero_id"] = heroId,
            ["project_id"] = projectId,
            ["quest_type"] = "coding",
            ["title"] = "Quest",
            ["goal"] = "Goal",
            ["locale"] = "en-US",
            ["status"] = status,
            ["started_at_utc"] = "2026-08-13T00:00:00.000Z",
            ["created_at_utc"] = "2026-08-13T00:00:00.000Z",
        };
        if (finishedAt is not null)
        {
            values["finished_at_utc"] = finishedAt;
        }
        return values;
    }

    private static async Task AssertConnectionPolicyAsync(string path, CancellationToken cancellationToken)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, cancellationToken);
        Assert.Equal(2L, await ScalarLongAsync(connection, "PRAGMA synchronous;", cancellationToken));
        Assert.Equal(1L, await ScalarLongAsync(connection, "PRAGMA foreign_keys;", cancellationToken));
        Assert.Equal(0L, await ScalarLongAsync(connection, "PRAGMA trusted_schema;", cancellationToken));
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
